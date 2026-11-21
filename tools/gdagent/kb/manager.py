"""manager —— 把 store/embed/search 编排成 CLI 动词（import/search/stats/drop/compact/rebuild/reindex）。

设计要点：
- 所有写动词都遵循"读当前代 → 内存算新 chunks+vectors → write_generation 原子切代"，
  绝不在旧代上原地改（半写不可见）。
- import 前先做整批预检（front matter/游戏/日期任一不合 → 一字节都不写），杜绝半批入库。
- 向量复用：embed_sha 命中缓存（且模型未换）就跳过 embed 调用，省额度；只有新块/变更块才 embed。
- embed/rerank 通过参数注入：自检喂 FakeEmbed，正式跑喂 embed.embed_texts。
"""
import datetime
import os
from pathlib import Path

from . import chunk as C
from . import embed as E
from . import search as S
from . import store as ST


def _today(path):
    try:
        return datetime.datetime.utcfromtimestamp(os.path.getmtime(str(path))).strftime("%Y-%m-%d")
    except OSError:
        return "1970-01-01"


def _collect_files(target):
    p = Path(target)
    if p.is_dir():
        return sorted(p.rglob("*.md"))
    if p.is_file():
        return [p]
    return []


def _rel_source(root, path):
    try:
        rel = os.path.relpath(str(path), str(root)).replace("\\", "/")
    except ValueError:
        rel = os.path.basename(str(path))
    return os.path.splitext(rel)[0]


# ---------------- stats ----------------

def stats(store):
    chunks, vectors, header = store.read_current()
    per_game = {}
    for c in chunks:
        if c.get("tombstone"):
            continue
        per_game[c["game"]] = per_game.get(c["game"], 0) + 1
    return {
        "current_gen": (store.current_gen() or [None])[0],
        "dim": header.get("dim", 0),
        "embed_model": header.get("embed_model_name", ""),
        "total_rows": len(chunks),
        "live_rows": ST.live_count(chunks),
        "tombstones": len(chunks) - ST.live_count(chunks),
        "per_game": per_game,
        "created_ts": header.get("created_ts", 0),
        "current_exists": bool(store.current_gen()),
    }


# ---------------- import ----------------

def import_paths(store, cfg, root, target, game=None, source_prefix=None, embed_fn=None):
    files = _collect_files(target)
    if not files:
        return {"ok": False, "why": "没找到 .md 文件：%s" % target, "added": 0}

    # 1) 整批预检：解析 + 切块，任一文件报错就中止（不写盘）
    parsed = []
    errs = []
    for f in files:
        try:
            text = f.read_text(encoding="utf-8")
            meta, _ = C.parse_front_matter(text)
            g = (meta.get("game") or game or "").strip()
            upd = (meta.get("updated") or _today(f)).strip()
            src = (meta.get("source") or source_prefix or _rel_source(root, f)).strip()
            recs = C.chunk_document(text, g, src, upd)  # 校验 game/updated，非法抛 ValueError
            parsed.append((src, recs))
        except ValueError as e:
            errs.append("%s：%s" % (f.name, e))
        except Exception as e:  # 读文件/编码炸了也算预检失败
            errs.append("%s：%s" % (f.name, e))
    if errs:
        return {"ok": False, "why": "整批预检未通过，未写入任何块", "errors": errs, "added": 0}

    # 2) 读当前代 + 向量缓存
    chunks, vectors, header = store.read_current()
    same_model = header.get("embed_model_name") == cfg.embed_model and bool(chunks)
    cache = ST.build_vector_cache(chunks, vectors) if same_model else {}

    # 3) 收集需 embed 的唯一 embed_text
    need = {}
    for _src, recs in parsed:
        for r in recs:
            if r["embed_sha"] not in cache and r["embed_sha"] not in need:
                need[r["embed_sha"]] = C.embed_text(r)
    new_vecs = {}
    if need:
        texts = list(need.values())
        shas = list(need.keys())
        try:
            vecs = (embed_fn or E.embed_texts)(texts, cfg)
        except E.EmbedError as e:
            return {"ok": False, "why": "embed 失败，整批未写入：%s" % e, "added": 0}
        for sha_, v in zip(shas, vecs):
            new_vecs[sha_] = v

    # 4) 应用：逐 source 整批替换（tombstone 旧的 + 追加新的）
    out_chunks = [dict(c) for c in chunks]
    out_vectors = [list(v) for v in vectors]
    touched_sources = []
    for src, recs in parsed:
        if src not in touched_sources:
            touched_sources.append(src)
    for src in touched_sources:
        out_chunks = ST.tombstone_source(out_chunks, src)
    for src, recs in parsed:
        for r in recs:
            out_chunks.append(r)
            out_vectors.append(cache.get(r["embed_sha"]) or new_vecs.get(r["embed_sha"]))
    # 对齐校验（漏向量=bug，宁可拒写）
    if any(v is None for v in out_vectors):
        return {"ok": False, "why": "内部错误：有块没拿到向量，拒绝写代", "added": 0}

    dim = len(out_vectors[0]) if out_vectors else 0
    store.write_generation(out_chunks, out_vectors, cfg.embed_model, dim, normalized=False)
    added = sum(len(recs) for _s, recs in parsed)
    return {
        "ok": True, "why": "", "added": added,
        "sources": touched_sources,
        "embedded_new": len(new_vecs), "reused": added - len(new_vecs),
        "rows_after": len(out_chunks),
    }


# ---------------- search ----------------

def do_search(store, cfg, query, game=None, top=6, rerank_on=True, embed_fn=None, transport=None):
    chunks, vectors, header = store.read_current()
    if not chunks:
        return {"ok": True, "hits": [], "why": "知识库为空，先 kb import"}
    match, why = _model_ok(header, cfg)
    if not match:
        return {"ok": False, "hits": [], "why": why}
    try:
        qv = (embed_fn or E.embed_texts)([query], cfg)[0]
    except E.EmbedError as e:
        return {"ok": False, "hits": [], "why": "query embed 失败：%s" % e}
    scope_note = "" if game else "（未指定 game，只搜 general）"
    # 候选池取 max(rerank_candidates, top*2)：给精排足够料（见 config.rerank_candidates 注释）
    pool_k = max(getattr(cfg, "rerank_candidates", 0), top * 2, top)
    hits = S.topk(chunks, vectors, qv, game=game, k=pool_k)
    rerank_applied = False
    if rerank_on and cfg.rerank_model and hits:
        docs = [{"text": h[0]["text"]} for h in hits]
        try:
            order = S.rerank(query, docs, cfg, transport=transport)
            ranked = [(hits[i][0], hits[i][1], s) for i, s in order if 0 <= i < len(hits)]
            floor = getattr(cfg, "rerank_min", 0.30)
            kept = [(c, s) for c, s, rs in ranked if rs >= floor]
            if len(kept) < len(ranked):
                scope_note += "｜rerank 相关性<%g 裁掉 %d 条" % (floor, len(ranked) - len(kept))
            hits = kept
            rerank_applied = True
        except S.RerankError as e:
            # 降级：回落向量序，不报错，仅在 why 里注明
            scope_note += " ｜rerank 未生效(%s)，已回取向量相似度序" % e
    hits = hits[:top]
    return {
        "ok": True,
        "hits": [{
            "id": c["id"], "game": c["game"], "title": c["title"], "text": c["text"],
            "score": round(s, 4), "source": c["source"], "updated": c["updated"],
        } for c, s in hits],
        "rerank": rerank_applied,
        "why": scope_note.strip("｜ "),
    }


def _model_ok(header, cfg):
    from . import vectors as V
    if not header:
        return True, ""
    return V.model_matches(header, cfg.embed_model)


# ---------------- drop / compact / rebuild / reindex ----------------

def drop_game(store, cfg, game, yes=False):
    chunks, vectors, header = store.read_current()
    if game not in C.GAMES:
        return {"ok": False, "why": "game 必须是 %s 之一" % "/".join(C.GAMES)}
    doomed = sum(1 for c in chunks if c.get("game") == game)
    if not yes:
        return {"ok": True, "why": "未加 --yes，仅回显不执行", "would_delete": doomed, "dry": True}
    if doomed == 0:
        return {"ok": True, "why": "该 game 无块可删", "deleted": 0}
    nc, nv = ST.drop_game(chunks, vectors, game)
    dim = len(nv[0]) if nv else header.get("dim", 0)
    store.write_generation(nc, nv, header.get("embed_model_name", cfg.embed_model), dim, normalized=False)
    return {"ok": True, "why": "", "deleted": doomed, "rows_after": len(nc)}


def compact(store, cfg):
    chunks, vectors, header = store.read_current()
    before = len(chunks)
    nc, nv = ST.compact(chunks, vectors)
    dim = len(nv[0]) if nv else header.get("dim", 0)
    store.write_generation(nc, nv, header.get("embed_model_name", cfg.embed_model), dim, normalized=False)
    return {"ok": True, "why": "", "removed": before - len(nc), "rows_after": len(nc)}


def _reembed(store, cfg, embed_fn, reuse):
    chunks, vectors, header = store.read_current()
    if not chunks:
        return {"ok": True, "why": "知识库为空", "embedded": 0}
    cache = ST.build_vector_cache(chunks, vectors) if reuse else {}
    keep_c, keep_v = [], []
    need_map = {}
    for c in chunks:
        if c.get("tombstone"):
            continue
        if reuse and c["embed_sha"] in cache:
            keep_c.append(dict(c))
            keep_v.append(cache[c["embed_sha"]])
        else:
            keep_c.append(dict(c))
            need_map[c["embed_sha"]] = C.embed_text(c)
            keep_v.append(None)
    embedded = 0
    if need_map:
        shas = list(need_map.keys())
        texts = [need_map[s] for s in shas]
        try:
            vecs = (embed_fn or E.embed_texts)(texts, cfg)
        except E.EmbedError as e:
            return {"ok": False, "why": "embed 失败，未写代：%s" % e}
        for s, v in zip(shas, vecs):
            embedded += 1
            for j, c in enumerate(keep_c):
                if c["embed_sha"] == s and keep_v[j] is None:
                    keep_v[j] = v
    if any(v is None for v in keep_v):
        return {"ok": False, "why": "内部错误：重算后仍有块无向量，拒绝写代"}
    dim = len(keep_v[0]) if keep_v else 0
    store.write_generation(keep_c, keep_v, cfg.embed_model, dim, normalized=False)
    return {"ok": True, "why": "", "embedded": embedded, "rows": len(keep_c), "mode": "reindex" if reuse else "rebuild"}


def rebuild(store, cfg, embed_fn=None):
    return _reembed(store, cfg, embed_fn, reuse=False)


def reindex(store, cfg, embed_fn=None):
    return _reembed(store, cfg, embed_fn, reuse=True)
