"""代际存储（P0-2 定案）：gen-<N>/ + CURRENT 指针，写全是"产出完整新代再切指针"。

一切写动词（import/drop/compact/rebuild/reindex）都：读当前代 → 内存里算出新 chunks+vectors →
write_generation() 落一个全新 gen 目录（两文件各自 .tmp→os.replace，checksum 验过）→ 最后才把
CURRENT 指向新代。中途失败留下的是无人引用的烂尾 gen，下次启动顺手清理 —— 两文件成对提交的
时序问题从根上不存在。读取永远经 CURRENT，拿到的必是完整一代。

CURRENT 切走后保留上一代，`kb rollback` 把指针拨回去（限一代之内）。

纯函数（tombstone 合并、向量复用）与活函数（文件读写）分开，前者可喂字典直接断言。
"""
import os
import re
import shutil
import tempfile
from pathlib import Path

from . import vectors as V

_CUR = "CURRENT"
_GEN = re.compile(r"^gen-(\d+)$")
_PAD = 6


# ---------------- 纯函数 ----------------

def build_vector_cache(chunks, vectors):
    """embed_sha → 向量行（只认未作废块，先到先得）。供 reindex/import 复用、省 embed 额度。"""
    cache = {}
    for c, vec in zip(chunks, vectors):
        if c.get("tombstone"):
            continue
        cache.setdefault(c["embed_sha"], list(vec))
    return cache


def tombstone_source(chunks, source):
    """把某 source 的现存未作废块标记 tombstone（同 source 整批替换的前半）。返回新列表，不改原。"""
    out = []
    for c in chunks:
        if c.get("source") == source and not c.get("tombstone"):
            c = dict(c)
            c["tombstone"] = True
        out.append(dict(c))
    return out


def drop_game(chunks, vectors, game):
    """剔除某 game 的全部块（含 tombstone），返回 (chunks, vectors) 已对齐。drop 用它写新代。"""
    nc, nv = [], []
    for c, vec in zip(chunks, vectors):
        if c.get("game") == game:
            continue
        nc.append(dict(c))
        nv.append(list(vec))
    return nc, nv


def compact(chunks, vectors):
    """压实：物理删除 tombstone 块及其向量行。返回 (chunks, vectors)。"""
    nc, nv = [], []
    for c, vec in zip(chunks, vectors):
        if c.get("tombstone"):
            continue
        nc.append(dict(c))
        nv.append(list(vec))
    return nc, nv


def live_count(chunks):
    return sum(1 for c in chunks if not c.get("tombstone"))


# ---------------- 活函数（文件层） ----------------

def _write_bytes_atomic(path, data):
    d = path.parent
    d.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(dir=str(d), prefix=".tmp-")
    try:
        with os.fdopen(fd, "wb") as fh:
            fh.write(data)
            fh.flush()
            os.fsync(fh.fileno())
        os.replace(tmp, str(path))
    finally:
        if os.path.exists(tmp):
            try:
                os.remove(tmp)
            except OSError:
                pass


class Store:
    def __init__(self, kb_dir):
        self.dir = Path(kb_dir)

    # -- 代目录发现 --
    def _gen_dirs(self):
        if not self.dir.is_dir():
            return []
        out = []
        for name in os.listdir(str(self.dir)):
            m = _GEN.match(name)
            if m:
                out.append((int(m.group(1)), self.dir / name))
        return sorted(out)

    def _gen_complete(self, gdir):
        return (gdir / "chunks.jsonl").is_file() and (gdir / "vectors.bin").is_file()

    def current_gen(self):
        cur = self.dir / _CUR
        if not cur.is_file():
            return None
        name = cur.read_text(encoding="utf-8").strip()
        m = _GEN.match(name)
        if not m:
            return None
        gdir = self.dir / name
        return (int(m.group(1)), gdir) if self._gen_complete(gdir) else None

    def sweep_partial(self):
        """只保留【当前代 + 上一代】，其余（含写一半的烂尾 gen）一律删除。返回删除个数。"""
        cur = self.current_gen()
        keep = set()
        if cur:
            keep.add(cur[0])
        complete = [n for n, gd in self._gen_dirs() if self._gen_complete(gd)]
        if cur:
            prevs = [n for n in complete if n < cur[0]]
            if prevs:
                keep.add(max(prevs))
        removed = 0
        for n, gdir in self._gen_dirs():
            if n in keep:
                continue
            shutil.rmtree(str(gdir), ignore_errors=True)
            removed += 1
        return removed

    # -- 读 --
    def read_current(self):
        cur = self.current_gen()
        if not cur:
            return [], [], {}
        _, gdir = cur
        chunks = self._read_chunks(gdir / "chunks.jsonl")
        data = (gdir / "vectors.bin").read_bytes()
        header, vecs = V.decode(data)
        return chunks, vecs, header

    @staticmethod
    def _read_chunks(path):
        import json
        out = []
        if not path.is_file():
            return out
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line:
                out.append(json.loads(line))
        return out

    # -- 写：产出全新代 + 切 CURRENT --
    def write_generation(self, chunks, vectors, embed_model_name, dim, normalized=False):
        assert len(chunks) == len(vectors), "chunks 与 vectors 行数不齐，拒绝写代"
        nums = [n for n, _ in self._gen_dirs()]
        new_n = (max(nums) + 1) if nums else 1
        gdir = self.dir / ("gen-%0*d" % (_PAD, new_n))
        gdir.mkdir(parents=True, exist_ok=True)
        import json
        cj = "\n".join(json.dumps(c, ensure_ascii=True, sort_keys=True) for c in chunks)
        _write_bytes_atomic(gdir / "chunks.jsonl", (cj + "\n").encode("utf-8") if cj else b"")
        blob = V.encode(vectors, embed_model_name, dim, normalized=normalized)
        _write_bytes_atomic(gdir / "vectors.bin", blob)
        # 回读验证 checksum/对齐，验过才敢切指针
        self._validate(gdir, len(chunks))
        _write_bytes_atomic(self.dir / _CUR, ("gen-%0*d" % (_PAD, new_n)).encode("ascii"))
        self.sweep_partial()
        return new_n

    @staticmethod
    def _validate(gdir, expect_count):
        import json
        n_chunks = sum(1 for ln in (gdir / "chunks.jsonl").read_text(encoding="utf-8").splitlines() if ln.strip())
        if n_chunks != expect_count:
            raise V.VectorFileError("chunks 行数 %d ≠ 期望 %d" % (n_chunks, expect_count))
        header, rows = V.decode((gdir / "vectors.bin").read_bytes())
        if len(rows) != expect_count:
            raise V.VectorFileError("向量行数 %d ≠ chunks 行数 %d" % (len(rows), expect_count))

    def rollback(self):
        """CURRENT 拨回上一代（限一代）。返回 (True, 目标代) 或 (False, 原因)。"""
        cur = self.current_gen()
        if not cur:
            return False, "没有当前代，回滚无从谈起"
        prevs = [n for n, gd in self._gen_dirs() if n < cur[0] and self._gen_complete(gd)]
        if not prevs:
            return False, "只保一代，没有可回退的上一代"
        target = max(prevs)
        _write_bytes_atomic(self.dir / _CUR, ("gen-%0*d" % (_PAD, target)).encode("ascii"))
        return True, target
