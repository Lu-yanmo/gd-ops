"""gdagent 配置与路径：settings 只读取、key 只认环境变量（与 ask.py / lib\\agent.ps1 同规）。

- 项目根 = 本包上两级（tools/gdagent → tools → 根）；state/kb 全在根下。
- KB 的 API key 只从环境变量读：先 KB_API_KEY，回落 DASHSCOPE_API_KEY；
  绝不读 settings 里的明文键（visionApiKey 那种不往 KB 通道带）。
- 端点解析：runtime.kbBaseUrl 给了就用；没给则从 runtime.visionBaseUrl 推 base
  （去掉尾部 /chat/completions），再没有回落 DashScope 公共兼容端点。
  一切"猜"的端点都只是默认值，真接通以 kb probe 的实测为准（P0：不凭文档猜字段）。
"""
import json
import os
from pathlib import Path

DEFAULT_EMBED_MODEL = "text-embedding-v3"
DEFAULT_DASHSCOPE_BASE = "https://dashscope.aliyuncs.com/compatible-mode/v1"
ENV_KEYS = ("KB_API_KEY", "DASHSCOPE_API_KEY")

ROOT = Path(__file__).resolve().parents[2]


class Config:
    def __init__(self, settings=None, environ=None):
        settings = settings if isinstance(settings, dict) else {}
        self._env = environ if environ is not None else os.environ
        rt = settings.get("runtime") or {}
        self.embed_model = (rt.get("kbEmbedModel") or DEFAULT_EMBED_MODEL).strip()
        self.rerank_model = (rt.get("kbRerankModel") or "").strip()
        self.base_url = self._resolve_base(rt)
        self.embed_batch = int(rt.get("kbEmbedBatch") or 10)
        self.timeout_seconds = float(rt.get("kbTimeoutSeconds") or 60)
        # rerank 相关性下限：低于它的命中不算"有依据"（M4 决策题实测：无裁时
        # 库里不存在的系统也会返回最相似块当资料）。阈值按 2026-09-28 实测标定：
        # 正确命中最低 0.1395、库外问题 top1 最高 0.1327 —— 绝对分只能当"垃圾线"
        # 用(0.12)，语义错配的防线在教材("资料不相关等同 no_hits")，别指望拉高数值。
        # 显式配 0 = 关裁剪。
        raw_floor = rt.get("kbRerankMinScore")
        self.rerank_min = float(raw_floor) if raw_floor not in (None, "") else 0.12
        # rerank 候选池：向量粗排送给 rerank 的条数。之前硬编码 max(top*2,top)，
        # top=3 时只喂 6 条——2026-09-28 ra-59 实测：目标块 rerank 排第 1(0.2887)
        # 但向量序第 14，进不了 6 条池，管线把精排饿死。默认 24，显式配 0=回旧行为。
        raw_pool = rt.get("kbRerankCandidates")
        self.rerank_candidates = int(raw_pool) if raw_pool not in (None, "") else 24

    def _resolve_base(self, rt):
        base = (rt.get("kbBaseUrl") or "").strip()
        if not base:
            vis = (rt.get("visionBaseUrl") or "").strip()
            if vis:
                base = vis
        if not base:
            base = DEFAULT_DASHSCOPE_BASE
        return _strip_chat_suffix(base)

    def api_key(self):
        for name in ENV_KEYS:
            v = (self._env.get(name) or "").strip()
            if v:
                return v, name
        return "", ""

    def has_key(self):
        k, _ = self.api_key()
        return bool(k)


def _strip_chat_suffix(url):
    u = url.strip().rstrip("/")
    low = u.lower()
    if low.endswith("/chat/completions"):
        u = u[: -len("/chat/completions")]
    return u.rstrip("/")


def load(root=ROOT):
    path = Path(root) / "config" / "settings.json"
    settings = {}
    if path.is_file():
        with open(path, "rb") as fh:
            settings = json.loads(fh.read().decode("utf-8", "replace") or "{}")
    return Config(settings)
