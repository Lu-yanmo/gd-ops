"""markdown 切块（纯函数）：front matter → 元数据；正文 → 一块只讲一件事的 chunk 记录。

切块口径（kb-plan §1）：按 markdown 标题分段，单块软目标 300~500 字、硬上限 800 字；
超上限的段按空行分组再切。每块头部拼「【游戏|条目标题】」后送去 embed（显著提升命中率）。

双哈希（P0/P1-3）：
  content_sha = sha256(正文)            —— 判断"这条知识正文是否重复"
  embed_sha   = sha256(实际 embed 文本) —— 判断"要不要重烧 embed 额度"（标题/game 变了它也会变）
id = sha256(source + '#' + 段序号)[:16]：同一 source 整批重导 → id 稳定 → 旧块可精确作废。
"""
import hashlib
import re

GAMES = ("ark", "r1999", "general")
VALID_UPDATED = re.compile(r"^\d{4}-\d{2}-\d{2}$")
_HEADING = re.compile(r"^(#{1,6})\s+(.*)$")
_SOFT_MAX = 800          # 单块硬上限：超过就按空行再切
_MIN_MERGE = 120         # 相邻小段合并阈值，避免碎块


def sha(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest()


def parse_front_matter(text):
    """返回 (meta, body)。无 front matter 时 meta={}、body=原文。解析失败抛 ValueError。"""
    if not text.startswith("---"):
        return {}, text
    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return {}, text
    for i in range(1, len(lines)):
        if lines[i].strip() == "---":
            meta = {}
            for raw in lines[1:i]:
                raw = raw.strip()
                if not raw or raw.startswith("#") or ":" not in raw:
                    continue
                k, v = raw.split(":", 1)
                meta[k.strip().lower()] = v.strip()
            body = "\n".join(lines[i + 1:])
            return meta, body
    raise ValueError("front matter 没有收尾的 ---")


def embed_text(chunk):
    return "【%s|%s】\n%s" % (chunk["game"], chunk["title"], chunk["text"])


def _split_long(section, title):
    """单段超软上限：按空行分组，累积到 ~_SOFT_MAX 就断一块。"""
    if len(section) <= _SOFT_MAX:
        return [section]
    paras = re.split(r"\n\s*\n", section)
    blocks, cur = [], ""
    for p in paras:
        if cur and len(cur) + len(p) + 2 > _SOFT_MAX:
            blocks.append(cur)
            cur = p
        else:
            cur = (cur + "\n\n" + p) if cur else p
    if cur.strip():
        blocks.append(cur)
    return blocks or [section]


def split_body(body):
    """正文 → [(title, text)]，按标题切段，无标题的开头块 title=''。"""
    out = []
    cur_title, cur_lines = "", []
    for line in body.splitlines():
        m = _HEADING.match(line)
        if m:
            if "\n".join(cur_lines).strip():
                out.append((cur_title, "\n".join(cur_lines).strip()))
            cur_title = m.group(2).strip()
            cur_lines = [line]
        else:
            cur_lines.append(line)
    if "\n".join(cur_lines).strip():
        out.append((cur_title, "\n".join(cur_lines).strip()))
    # 合并过小段（少于 _MIN_MERGE 且非首段）到前一段，减碎块
    merged = []
    for title, text in out:
        if merged and len(text) < _MIN_MERGE:
            pt, ptext = merged[-1]
            merged[-1] = (pt, ptext + "\n\n" + text)
        else:
            merged.append((title, text))
    return merged


def chunk_document(text, game, source, updated):
    """一份 md 原文 → chunk 记录列表。game/updated 非法抛 ValueError（供 import 整批预检）。"""
    if game not in GAMES:
        raise ValueError("game 必须是 %s 之一，收到 %r" % ("/".join(GAMES), game))
    if not VALID_UPDATED.match(updated or ""):
        raise ValueError("updated 需为 YYYY-MM-DD，收到 %r" % updated)
    meta, body = parse_front_matter(text)
    doc_title = meta.get("title") or ""
    sections = split_body(body)
    chunks = []
    seq = 0
    for title, section in sections:
        full_title = title or doc_title
        for piece in _split_long(section, full_title):
            piece = piece.strip()
            if not piece:
                continue
            cid = sha("%s#%d" % (source, seq))[:16]
            rec = {
                "id": cid,
                "game": game,
                "title": full_title,
                "text": piece,
                "source": source,
                "updated": updated,
                "tombstone": False,
            }
            rec["content_sha"] = sha(piece)
            rec["embed_sha"] = sha(embed_text(rec))
            chunks.append(rec)
            seq += 1
    return chunks
