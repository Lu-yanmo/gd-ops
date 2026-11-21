"""检索：向量余弦 topK（game 硬过滤）+ rerank（可插拔、失败降级回向量序）。

game 过滤语义（kb-plan §1，v2 定死）：
  - 指定 game X → 候选 = X + general；
  - 不指定 → 只搜 general（跨游戏必须显式，防方舟"理智"串进 1999）。
tombstone 块永不进候选。分数 = 真实余弦（未假设归一化；normalized 只是元信息）。

rerank：DashScope 原生形状；未配置 kbRerankModel 或调用失败 → 原样返回向量序（降级，不报错）。
纯函数（filter_scope/cosine/topk）不联网，自检直接喂向量断言。
"""
import json
import urllib.error
import urllib.request

try:
    import numpy as _np
except Exception:  # numpy 缺失也能跑，纯 Python 兜底
    _np = None


def filter_scope(game):
    """给定检索 game，返回允许进入候选的 game 集合（含 general），或 None 表示不过滤由调用方定。

    game 可以是单个字符串，或字符串列表（P5 联合检索：合体上下文一次搜全域，
    如 ["r1999","ark"] → {r1999, ark, general}）。域永远由引擎注入，模型无从指定。
    """
    if isinstance(game, (list, tuple, set, frozenset)):
        named = {str(g).strip() for g in game if str(g).strip()}
        if not named:
            return {"general"}
        return named | {"general"}
    if game:
        return {game, "general"}
    return {"general"}


def cosine(a, b):
    dot = 0.0
    na = 0.0
    nb = 0.0
    for x, y in zip(a, b):
        dot += x * y
        na += x * x
        nb += y * y
    if na == 0 or nb == 0:
        return 0.0
    return dot / (na ** 0.5 * nb ** 0.5)


def topk(chunks, vectors, query_vec, game=None, k=6, use_numpy=None):
    """返回 [(chunk, score)]，已按分数降序、过滤 tombstone+game。纯函数、不联网。

    use_numpy: True 强制 numpy、False 强制纯 Python、None=有 numpy 就用（默认）。
    """
    scope = filter_scope(game)
    idx = [i for i, c in enumerate(chunks)
           if (not c.get("tombstone")) and c.get("game") in scope]
    if not idx:
        return []
    want_np = (_np is not None) if use_numpy is None else (use_numpy and _np is not None)
    scored = []
    if want_np:
        mat = _np.asarray([vectors[i] for i in idx], dtype=_np.float32)
        q = _np.asarray(query_vec, dtype=_np.float32)
        qn = float(_np.linalg.norm(q)) or 1.0
        norms = _np.linalg.norm(mat, axis=1)
        norms[norms == 0] = 1.0
        sims = (mat @ q) / (norms * qn)
        for j, i in enumerate(idx):
            scored.append((i, float(sims[j])))
    else:
        for i in idx:
            scored.append((i, cosine(vectors[i], query_vec)))
    scored.sort(key=lambda t: t[1], reverse=True)
    return [(chunks[i], s) for i, s in scored[:k]]


# ---------------- rerank（活函数，可降级） ----------------

class RerankError(Exception):
    pass


def _rerank_url(base):
    # DashScope 原生 rerank 端点；base 是 compatible-mode 时换到 api/v1 原生路径。
    b = (base or "").rstrip("/")
    if "/compatible-mode/" in b:
        b = b.replace("/compatible-mode/v1", "")
    return b.rstrip("/") + "/api/v1/services/rerank/text-rerank/text-rerank"


def rerank(query, docs, cfg, transport=None):
    """docs=[{text,...}] → 按 relevance 降序返回 [(doc_index, score)]；失败/未配置抛 RerankError。
    调用方负责降级：捕获后回向量序。"""
    if not cfg.rerank_model:
        raise RerankError("未配置 kbRerankModel")
    key, _ = cfg.api_key()
    if not key:
        raise RerankError("没有 rerank key")
    payload = {
        "model": cfg.rerank_model,
        "input": {"query": query, "documents": [d["text"] for d in docs]},
        "parameters": {"return_documents": False, "top_n": len(docs)},
    }
    transport = transport or _default_transport
    url = _rerank_url(cfg.base_url)
    status, body = transport(url, payload, key, cfg.timeout_seconds)
    if not (200 <= status < 300):
        raise RerankError("rerank HTTP %d" % status)
    try:
        parsed = json.loads(body or "{}")
    except ValueError:
        raise RerankError("rerank 回的不是 JSON")
    results = (parsed.get("output") or {}).get("results")
    if not isinstance(results, list):
        raise RerankError("rerank 响应缺 output.results")
    out = []
    for r in results:
        out.append((int(r.get("index", -1)), float(r.get("relevance_score", r.get("score", 0.0)))))
    return out


def _default_transport(url, payload, key, timeout):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, method="POST", headers={
        "Authorization": "Bearer %s" % key,
        "Content-Type": "application/json",
    })
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.getcode(), resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
