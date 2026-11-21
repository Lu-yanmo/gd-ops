"""gdagent.kb 离线自检：不联网、不读本机配置，全部喂临时目录 + FakeEmbed。

跑法（tools 目录下）： python -m unittest discover -s gdagent/tests -t . 2>&1
或：                  python gdagent/tests/test_kb.py
"""
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

from gdagent import config as CFG                      # noqa: E402
from gdagent.kb import chunk as C                      # noqa: E402
from gdagent.kb import vectors as V                    # noqa: E402
from gdagent.kb import store as ST                     # noqa: E402
from gdagent.kb import search as S                     # noqa: E402
from gdagent.kb import embed as E                      # noqa: E402
from gdagent.kb import manager as M                    # noqa: E402


def make_cfg(**rt):
    rt.setdefault("kbEmbedModel", "fake-model")
    env = dict(rt.pop("_env", {}))
    env.setdefault("KB_API_KEY", "testkey")
    return CFG.Config({"runtime": rt}, environ=env)


MD_A = """---\ngame: ark\nupdated: 2026-01-05\ntitle: 理智机制\n---\n# 理智\n理智是方舟的核心资源，随时间自然恢复，每6分钟恢复1点。\n消耗理智来作战获取材料。
"""
MD_B = """# 密合效应
启示与密合是 1999 的机制。密合值满了会触发额外效果。
"""


# ---------------- vectors ----------------

class TestVectors(unittest.TestCase):
    def test_roundtrip(self):
        vs = [[1.0, 2.0, 3.0], [0.5, 0.5, 0.5]]
        blob = V.encode(vs, "m1", 3, created_ts=100)
        header, rows = V.decode(blob)
        self.assertEqual(header["embed_model_name"], "m1")
        self.assertEqual(header["dim"], 3)
        self.assertEqual(header["chunk_count"], 2)
        self.assertAlmostEqual(rows[0][1], 2.0, places=5)

    def test_checksum_tamper(self):
        blob = bytearray(V.encode([[1.0, 2.0]], "m", 2))
        blob[-1] ^= 0xFF
        with self.assertRaises(V.VectorFileError):
            V.decode(bytes(blob))

    def test_bad_dim(self):
        with self.assertRaises(V.VectorFileError):
            V.encode([[1.0, 2.0]], "m", 3)

    def test_model_matches(self):
        h = {"embed_model_name": "m1", "dim": 3}
        self.assertEqual(V.model_matches(h, "m1"), (True, ""))
        ok, why = V.model_matches(h, "m2")
        self.assertFalse(ok)
        self.assertIn("rebuild", why)


# ---------------- chunk ----------------

class TestChunk(unittest.TestCase):
    def test_front_matter(self):
        meta, body = C.parse_front_matter(MD_A)
        self.assertEqual(meta["game"], "ark")
        self.assertEqual(meta["updated"], "2026-01-05")
        self.assertIn("理智", body)

    def test_no_front_matter(self):
        meta, body = C.parse_front_matter(MD_B)
        self.assertEqual(meta, {})
        self.assertTrue(body.startswith("# 密合"))

    def test_unclosed_front_matter_raises(self):
        with self.assertRaises(ValueError):
            C.parse_front_matter("---\ngame: ark\n没收尾")

    def test_chunk_ids_stable_and_hashed(self):
        a = C.chunk_document(MD_A, "ark", "src/x", "2026-01-05")
        b = C.chunk_document(MD_A, "ark", "src/x", "2026-01-05")
        self.assertEqual([c["id"] for c in a], [c["id"] for c in b])
        self.assertTrue(all(c["content_sha"] and c["embed_sha"] for c in a))

    def test_embed_sha_changes_with_title_but_content_maybe_not(self):
        recs1 = C.chunk_document("正文一样", "ark", "s1", "2026-01-01")
        recs2 = C.chunk_document("正文一样", "r1999", "s1", "2026-01-01")
        self.assertEqual(recs1[0]["content_sha"], recs2[0]["content_sha"])
        self.assertNotEqual(recs1[0]["embed_sha"], recs2[0]["embed_sha"])

    def test_bad_game_raises(self):
        with self.assertRaises(ValueError):
            C.chunk_document("x", "wow", "s", "2026-01-01")

    def test_bad_updated_raises(self):
        with self.assertRaises(ValueError):
            C.chunk_document("x", "ark", "s", "2026/1/1")

    def test_long_section_split(self):
        long = "# T\n" + ("\n\n".join("段落%d %s" % (i, "字" * 200) for i in range(10)))
        recs = C.chunk_document(long, "ark", "s", "2026-01-01")
        self.assertGreater(len(recs), 1)


# ---------------- store ----------------

class TestStore(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.mkdtemp()
        self.st = ST.Store(self.d)
        self.chunks = [
            {"id": "a", "game": "ark", "title": "t", "text": "x", "source": "s",
             "updated": "2026-01-01", "tombstone": False, "content_sha": "c1", "embed_sha": "e1"},
            {"id": "b", "game": "general", "title": "t", "text": "y", "source": "s",
             "updated": "2026-01-01", "tombstone": False, "content_sha": "c2", "embed_sha": "e2"},
        ]
        self.vecs = [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0]]

    def test_empty_read(self):
        self.assertEqual(self.st.read_current(), ([], [], {}))

    def test_write_then_read(self):
        n = self.st.write_generation(self.chunks, self.vecs, "m1", 3)
        self.assertEqual(n, 1)
        c, v, h = self.st.read_current()
        self.assertEqual(len(c), 2)
        self.assertEqual(h["embed_model_name"], "m1")

    def test_rollback_one_gen(self):
        self.st.write_generation(self.chunks, self.vecs, "m1", 3)
        ok, gen = self.st.rollback()
        self.assertFalse(ok)  # 只有一代，回退不了
        self.st.write_generation(self.chunks[:1], self.vecs[:1], "m1", 3)
        ok, gen = self.st.rollback()
        self.assertTrue(ok)
        c, _, _ = self.st.read_current()
        self.assertEqual(len(c), 2)  # 回到第一代

    def test_sweep_keeps_two(self):
        for i in range(4):
            self.st.write_generation(self.chunks, self.vecs, "m1", 3)
        gens = [n for n, _ in self.st._gen_dirs()]
        self.assertLessEqual(len(gens), 2)

    def test_purge_partial_gen(self):
        os.makedirs(os.path.join(self.d, "gen-000099"))  # 只有目录没文件 = 烂尾
        removed = self.st.sweep_partial()
        self.assertGreaterEqual(removed, 1)
        self.assertFalse(os.path.exists(os.path.join(self.d, "gen-000099")))


# ---------------- search pure ----------------

class TestSearch(unittest.TestCase):
    def test_scope(self):
        self.assertEqual(S.filter_scope("ark"), {"ark", "general"})
        self.assertEqual(S.filter_scope(None), {"general"})

    def test_scope_accepts_game_list(self):
        # P5 联合检索：域集合给列表 → 全域并 general；空列表回落 general(与 None 同义)
        self.assertEqual(S.filter_scope(["r1999", "ark"]), {"r1999", "ark", "general"})
        self.assertEqual(S.filter_scope(("ark",)), {"ark", "general"})
        self.assertEqual(S.filter_scope([]), {"general"})
        self.assertEqual(S.filter_scope(["", " ark "]), {"ark", "general"})

    def test_topk_multi_game_union(self):
        chunks = [
            {"game": "ark", "tombstone": False, "text": "a"},
            {"game": "general", "tombstone": False, "text": "g"},
            {"game": "r1999", "tombstone": False, "text": "r"},
            {"game": "other", "tombstone": False, "text": "o"},
        ]
        vecs = [[1.0, 0.0], [1.0, 0.0], [1.0, 0.0], [1.0, 0.0]]
        hits = S.topk(chunks, vecs, [1.0, 0.0], game=["ark", "r1999"], k=5, use_numpy=False)
        texts = sorted(c["text"] for c, _ in hits)
        self.assertEqual(texts, ["a", "g", "r"])  # 两域+general 并进，域外 other 挡住

    def test_topk_game_filter_and_order(self):
        chunks = [
            {"game": "ark", "tombstone": False, "text": "a"},
            {"game": "general", "tombstone": False, "text": "g"},
            {"game": "r1999", "tombstone": False, "text": "r"},
            {"game": "ark", "tombstone": True, "text": "dead"},
        ]
        vecs = [[1.0, 0.0], [1.0, 0.0], [0.0, 1.0], [1.0, 0.0]]
        hits = S.topk(chunks, vecs, [1.0, 0.0], game="ark", k=5, use_numpy=False)
        texts = [c["text"] for c, _ in hits]
        self.assertEqual(texts, ["a", "g"])  # r 不在范围，dead 被 tombstone 挡

    def test_numpy_matches_pure(self):
        chunks = [{"game": "general", "tombstone": False, "text": str(i)} for i in range(20)]
        import hashlib
        vecs = []
        for i in range(20):
            h = hashlib.sha256(str(i).encode()).digest()
            vecs.append([b / 255.0 for b in h[:8]])
        q = [b / 255.0 for b in hashlib.sha256(b"7").digest()[:8]]
        a = [(c["text"], round(s, 4)) for c, s in S.topk(chunks, vecs, q, k=5, use_numpy=False)]
        b = [(c["text"], round(s, 4)) for c, s in S.topk(chunks, vecs, q, k=5, use_numpy=True)]
        self.assertEqual([x[0] for x in a], [x[0] for x in b])


# ---------------- embed pure/url ----------------

class TestEmbed(unittest.TestCase):
    def test_url_normalize(self):
        self.assertEqual(E.embeddings_url("https://x/v1"), "https://x/v1/embeddings")
        self.assertEqual(E.embeddings_url("https://x/v1/embeddings"), "https://x/v1/embeddings")
        with self.assertRaises(E.EmbedError):
            E.embeddings_url("ftp://x")

    def test_batching_and_order(self):
        cfg = make_cfg(kbEmbedBatch=2)
        calls = {"n": 0}

        def transport(url, payload, key, timeout):
            calls["n"] += 1
            items = payload["input"]
            data = [{"index": i, "embedding": [float(len(s)), 1.0]} for i, s in enumerate(items)]
            return 200, __import__("json").dumps({"data": data})

        vecs = E.embed_texts(["a", "bb", "ccc", "dddd", "e"], cfg, transport=transport)
        self.assertEqual(len(vecs), 5)
        self.assertEqual(calls["n"], 3)  # ceil(5/2)
        self.assertEqual(vecs[2][0], 3.0)  # ccc 长度 3

    def test_no_key(self):
        cfg = CFG.Config({"runtime": {"kbEmbedModel": "m"}}, environ={})
        with self.assertRaises(E.EmbedError):
            E.embed_texts(["x"], cfg)

    def test_429_then_success(self):
        cfg = make_cfg()
        state = {"n": 0}

        def transport(url, payload, key, timeout):
            state["n"] += 1
            if state["n"] == 1:
                return 429, "rate"
            return 200, __import__("json").dumps({"data": [{"index": 0, "embedding": [1.0, 2.0]}]})

        v = E.embed_texts(["q"], cfg, transport=transport)
        self.assertEqual(v[0], [1.0, 2.0])
        self.assertEqual(state["n"], 2)


# ---------------- config ----------------

class TestConfig(unittest.TestCase):
    def test_base_from_vision(self):
        cfg = CFG.Config({"runtime": {"visionBaseUrl": "https://x/compatible-mode/v1/chat/completions"}}, environ={})
        self.assertEqual(cfg.base_url, "https://x/compatible-mode/v1")

    def test_explicit_base_wins(self):
        cfg = CFG.Config({"runtime": {"kbBaseUrl": "https://kb/v1", "visionBaseUrl": "https://v/v1"}}, environ={})
        self.assertEqual(cfg.base_url, "https://kb/v1")

    def test_key_fallback(self):
        cfg = CFG.Config({"runtime": {}}, environ={"DASHSCOPE_API_KEY": "dsk"})
        k, name = cfg.api_key()
        self.assertEqual((k, name), ("dsk", "DASHSCOPE_API_KEY"))

    def test_kb_key_preferred(self):
        cfg = CFG.Config({"runtime": {}}, environ={"KB_API_KEY": "kbk", "DASHSCOPE_API_KEY": "dsk"})
        self.assertEqual(cfg.api_key()[0], "kbk")


# ---------------- manager (端到端 + FakeEmbed) ----------------

class TestManager(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.mkdtemp()
        self.root = tempfile.mkdtemp()
        self.st = ST.Store(os.path.join(self.d, "kb"))
        self.cfg = make_cfg(kbEmbedModel="fake-model")
        self.md_dir = os.path.join(self.root, "corpus")
        os.makedirs(self.md_dir, exist_ok=True)
        with open(os.path.join(self.md_dir, "a.md"), "w", encoding="utf-8") as f:
            f.write(MD_A)
        with open(os.path.join(self.md_dir, "b.md"), "w", encoding="utf-8") as f:
            f.write(MD_B)
        self.fake = E.FakeEmbed(dim=16)

    def test_import_dir_and_search(self):
        r = M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        self.assertTrue(r["ok"], r)
        self.assertGreaterEqual(r["added"], 2)
        self.assertEqual(self.fake.calls, 1)  # 一次 batch embed
        # b.md 无 front matter → game=general；a.md front matter=ark。搜 ark 应含 a 且带 general
        hit = M.do_search(self.st, self.cfg, "理智是核心资源", game="ark", embed_fn=self.fake)
        self.assertTrue(hit["ok"])
        self.assertTrue(hit["hits"])
        self.assertIn(hit["hits"][0]["game"], ("ark", "general"))

    def test_reimport_reuses_vectors(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        calls0 = self.fake.calls
        r2 = M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        self.assertTrue(r2["ok"])
        self.assertEqual(r2["embedded_new"], 0)  # embed_sha 全命中缓存 → 不重烧
        self.assertEqual(self.fake.calls, calls0)

    def test_preflight_failure_writes_nothing(self):
        with open(os.path.join(self.md_dir, "bad.md"), "w", encoding="utf-8") as f:
            f.write("---\ngame: wow\nupdated: 2026-01-01\n---\n正文")
        r = M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        self.assertFalse(r["ok"])
        self.assertFalse(self.st.current_gen())  # 整批预检失败，一字节都没写

    def test_model_mismatch_rejects_search(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        cfg2 = make_cfg(kbEmbedModel="other-model")
        hit = M.do_search(self.st, cfg2, "理智", game="ark", embed_fn=self.fake)
        self.assertFalse(hit["ok"])
        self.assertIn("rebuild", hit["why"])

    def test_rerank_degrades_to_vector_order(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        cfg = make_cfg(kbEmbedModel="fake-model", kbRerankModel="some-rerank")
        # transport 让 rerank 失败(返回 500) → 应降级回向量序，ok 仍为 True

        def bad_transport(url, payload, key, timeout):
            return 500, "boom"

        hit = M.do_search(self.st, cfg, "理智", game="ark", embed_fn=self.fake, transport=bad_transport)
        self.assertTrue(hit["ok"])
        self.assertFalse(hit["rerank"])
        self.assertIn("rerank", hit["why"])

    def _ranked_transport(self, first_score, rest_score):
        def transport(url, payload, key, timeout):
            docs = payload["input"]["documents"]
            results = [{"index": i, "relevance_score": first_score if i == 0 else rest_score}
                       for i in range(len(docs))]
            return 200, json.dumps({"output": {"results": results}})
        return transport

    def test_rerank_floor_drops_weak_hits(self):
        # M4 决策题实测坑钉桩：库里没有的机制也会返回"最相似"块 —— rerank 生效时
        # 相关性低于 cfg.rerank_min(默认 0.30) 的命中必须裁掉，不能当资料喂给模型。
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        cfg = make_cfg(kbEmbedModel="fake-model", kbRerankModel="some-rerank")
        hit = M.do_search(self.st, cfg, "理智", game="ark", embed_fn=self.fake,
                          transport=self._ranked_transport(0.91, 0.05))
        self.assertTrue(hit["ok"])
        self.assertTrue(hit["rerank"])
        self.assertEqual(len(hit["hits"]), 1)
        self.assertIn("裁掉", hit["why"])

    def test_rerank_floor_all_weak_means_no_hits(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        cfg = make_cfg(kbEmbedModel="fake-model", kbRerankModel="some-rerank")
        hit = M.do_search(self.st, cfg, "理智", game="ark", embed_fn=self.fake,
                          transport=self._ranked_transport(0.02, 0.05))
        self.assertTrue(hit["ok"])
        self.assertEqual(hit["hits"], [])
        # 显式配 0 = 关裁剪（回退旧行为的后路）
        cfg0 = make_cfg(kbEmbedModel="fake-model", kbRerankModel="some-rerank", kbRerankMinScore="0")
        hit0 = M.do_search(self.st, cfg0, "理智", game="ark", embed_fn=self.fake,
                           transport=self._ranked_transport(0.02, 0.05))
        self.assertTrue(hit0["hits"])

    def test_rerank_candidate_pool_not_starved(self):
        # WIKI-2 ra-59 实测坑钉桩：do_search 曾只给 rerank 喂 max(top*2,top) 条，
        # 目标块向量序靠后但 rerank 排第一时被池子饿死。默认池 24，显式配 0 回旧行为。
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        seen = {}
        real_topk = S.topk

        def spy_topk(chunks, vectors, qv, game=None, k=6):
            seen["k"] = k
            return real_topk(chunks, vectors, qv, game=game, k=k)

        cfg = make_cfg(kbEmbedModel="fake-model", kbRerankModel="some-rerank")
        old = M.S.topk
        M.S.topk = spy_topk
        try:
            M.do_search(self.st, cfg, "理智", game="ark", top=3, embed_fn=self.fake,
                        transport=self._ranked_transport(0.9, 0.5))
            self.assertGreaterEqual(seen.get("k", 0), 24)
            cfg0 = make_cfg(kbEmbedModel="fake-model", kbRerankModel="some-rerank",
                            kbRerankCandidates="0")
            M.do_search(self.st, cfg0, "理智", game="ark", top=3, embed_fn=self.fake,
                        transport=self._ranked_transport(0.9, 0.5))
            self.assertEqual(seen.get("k"), 6)
        finally:
            M.S.topk = old

    def test_drop_needs_yes(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        r = M.drop_game(self.st, self.cfg, "ark")
        self.assertTrue(r["dry"])
        self.assertGreaterEqual(r["would_delete"], 1)
        r2 = M.drop_game(self.st, self.cfg, "ark", yes=True)
        self.assertTrue(r2["ok"])
        st = M.stats(self.st)
        self.assertNotIn("ark", st["per_game"])

    def test_compact_removes_tombstones(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)  # 同 source 替换→旧块 tombstone
        st1 = M.stats(self.st)
        self.assertGreaterEqual(st1["tombstones"], 1)
        r = M.compact(self.st, self.cfg)
        self.assertTrue(r["ok"])
        self.assertGreaterEqual(r["removed"], 1)
        st2 = M.stats(self.st)
        self.assertEqual(st2["tombstones"], 0)

    def test_rebuild_reembeds_all(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        before = self.fake.calls
        r = M.rebuild(self.st, self.cfg, embed_fn=self.fake)
        self.assertTrue(r["ok"])
        self.assertGreaterEqual(self.fake.calls, before + 1)  # rebuild 无条件重算

    def test_reindex_reuses_when_same_model(self):
        M.import_paths(self.st, self.cfg, self.root, self.md_dir, game="general", embed_fn=self.fake)
        calls = self.fake.calls
        r = M.reindex(self.st, self.cfg, embed_fn=self.fake)
        self.assertTrue(r["ok"])
        self.assertEqual(r["embedded"], 0)
        self.assertEqual(self.fake.calls, calls)  # 模型未换 + 全命中 → 不 embed


# ---------------- 真实 API 响应 golden 重放(2026-09-27 专属 MaaS 端点实测钉版) ----------------

_GOLDEN_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "golden", "api-responses.jsonl")


def _load_api_golden():
    rows = {}
    with open(_GOLDEN_PATH, "rb") as fh:
        for ln in fh.read().decode("utf-8").splitlines():
            if ln.strip():
                row = json.loads(ln)
                rows[row["sample"]] = row
    return rows


class TestApiGoldenReplay(unittest.TestCase):
    """离线：把 golden 里的真实响应喂回 embed/rerank 解析路径，形状一漂移就红。"""

    @classmethod
    def setUpClass(cls):
        if not os.path.isfile(_GOLDEN_PATH):
            raise unittest.SkipTest("golden/api-responses.jsonl 不在(本地钉版文件)")
        cls.rows = _load_api_golden()

    def _cfg(self):
        return CFG.Config({"runtime": {"kbEmbedModel": "text-embedding-v4",
                                       "kbRerankModel": "gte-rerank-v2",
                                       "kbBaseUrl": "https://example.invalid/compatible-mode/v1"}},
                          environ={"KB_API_KEY": "testkey"})

    def test_embed_golden_parses(self):
        row = self.rows["embeddings"]
        body = json.dumps(row["response"])

        def transport(url, payload, key, timeout):
            self.assertTrue(url.endswith("/embeddings"))
            self.assertEqual(payload["model"], "text-embedding-v4")
            self.assertEqual(payload["input"], row["request"]["input"])
            return 200, body

        vecs = E.embed_texts(row["request"]["input"], self._cfg(), transport=transport)
        self.assertEqual(len(vecs), 2)
        self.assertEqual(row["response"]["data"][0]["dim_total"], 1024)  # 钉版事实：v4 默认 1024 维
        for got, want in zip(vecs, row["response"]["data"]):
            # golden 只截了前 6 维，解析路径吃的就是这 6 个：逐维对得上即证明形状没漂
            self.assertEqual(len(got), len(want["embedding"]))
            self.assertEqual([round(v, 9) for v in got], [round(float(x), 9) for x in want["embedding"]])

    def test_rerank_golden_parses(self):
        row = self.rows["rerank"]
        body = json.dumps(row["response"])
        docs = [{"text": t} for t in row["request"]["input"]["documents"]]

        def transport(url, payload, key, timeout):
            self.assertIn("/api/v1/services/rerank/text-rerank/text-rerank", url)
            self.assertEqual(payload["model"], "gte-rerank-v2")
            return 200, body

        out = S.rerank(row["request"]["input"]["query"], docs, self._cfg(), transport=transport)
        want = [(r["index"], r["relevance_score"]) for r in row["response"]["output"]["results"]]
        self.assertEqual([i for i, _ in out], [i for i, _ in want])
        self.assertEqual(out, want)
        self.assertEqual(out, sorted(out, key=lambda t: -t[1]))  # 降序口径


if __name__ == "__main__":
    unittest.main(verbosity=2)
