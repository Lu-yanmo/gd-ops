"""embed HTTP 客户端：OpenAI 兼容 POST {base}/embeddings，batch + 退避重试（对齐 tools/vision/ask.py）。

- key 只从环境变量（config.api_key()），不落命令行/文件。
- 依赖 stdlib urllib：不引第三方包，有 Python 的机器就能跑（本机虽有 requests，仍不引，
  少一个环境假设）。transport() 可注入，自检喂假响应、不联网。
- 一切网络/额度失败都归成 EmbedError(中文那句)，由 manager 决定是降级还是中止整批。
"""
import json
import socket
import ssl
import time
import urllib.error
import urllib.parse
import urllib.request

from .chunk import embed_text  # noqa: F401  (re-export 便捷)

DEFAULT_BATCH = 10
_RETRIES = 2
_BODY_ECHO = 240


class EmbedError(Exception):
    pass


def embeddings_url(base):
    b = (base or "").strip().rstrip("/")
    if not b:
        raise EmbedError("没给 embed 端点(config kbBaseUrl/visionBaseUrl 都是空的)")
    if not b.lower().startswith(("https://", "http://")):
        raise EmbedError("端点不是 http/https 地址：%s" % b)
    if not b.lower().endswith("/embeddings"):
        b = b + "/embeddings"
    return b


def _peek(text):
    t = (text or "").replace("\n", " ").strip()
    return (t[:_BODY_ECHO] + "…") if len(t) > _BODY_ECHO else (t or "(空响应体)")


def _classify(status, body, attempts):
    host_hint = ""
    if status in (401, 403):
        return ("embed API key 被拒(HTTP %d)：检查环境变量 KB_API_KEY/DASHSCOPE_API_KEY%s" % (status, host_hint), False)
    if status == 404:
        return ("embed 端点不存在(HTTP 404)：核对 config kbBaseUrl", False)
    if status == 400:
        return ("embed 请求被拒(HTTP 400)，多半 model 名不对：%s" % _peek(body), False)
    if status == 429:
        return ("embed 被限流/额度用尽(HTTP 429，已试 %d 次)：%s" % (attempts, _peek(body)), True)
    if status >= 500:
        return ("embed 服务端错误(HTTP %d，已试 %d 次)：%s" % (status, attempts, _peek(body)), True)
    return ("embed 服务回了 HTTP %d：%s" % (status, _peek(body)), False)


def default_transport(url, payload, key, timeout):
    data = json.dumps(payload).encode("utf-8")
    headers = {
        "Authorization": "Bearer %s" % key,
        "Content-Type": "application/json; charset=utf-8",
        "Accept": "application/json",
        "User-Agent": "game-daily-gdagent/1",
    }
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=ssl.create_default_context()) as resp:
            return resp.getcode(), resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")
    except socket.timeout:
        raise EmbedError("embed 请求超时(%d 秒)：可调 config kbTimeoutSeconds 或查网络" % int(timeout))
    except urllib.error.URLError as e:
        raise EmbedError("连不上 %s：%s" % (urllib.parse.urlparse(url).netloc or url, getattr(e, "reason", e)))
    except ssl.SSLError as e:
        raise EmbedError("embed TLS 握手失败：%s" % e)


def embed_texts(texts, cfg, transport=None):
    """一批文本 → list[list[float]]（顺序对齐）。自动按 batch 切分、串行请求。"""
    if not texts:
        return []
    transport = transport or default_transport
    key, env_name = cfg.api_key()
    if not key:
        raise EmbedError("没有 embed key：请设环境变量 KB_API_KEY 或 DASHSCOPE_API_KEY")
    url = embeddings_url(cfg.base_url)
    out = []
    bs = max(1, int(getattr(cfg, "embed_batch", DEFAULT_BATCH)))
    for start in range(0, len(texts), bs):
        batch = texts[start:start + bs]
        out.extend(_embed_batch(batch, url, key, cfg, transport))
    return out


def _embed_batch(batch, url, key, cfg, transport):
    payload = {"model": cfg.embed_model, "input": batch, "encoding_format": "float"}
    attempts = 0
    while True:
        attempts += 1
        status, body = transport(url, payload, key, cfg.timeout_seconds)
        if 200 <= status < 300:
            try:
                parsed = json.loads(body or "{}")
            except ValueError:
                raise EmbedError("embed 回的不是 JSON(HTTP %d)：%s" % (status, _peek(body)))
            data = parsed.get("data")
            if not isinstance(data, list) or len(data) != len(batch):
                raise EmbedError("embed 响应 data 形状不对(期望 %d 条)：%s" % (len(batch), _peek(body)))
            data = sorted(data, key=lambda d: d.get("index", 0))
            vecs = [list(map(float, d.get("embedding") or [])) for d in data]
            if any(len(v) == 0 for v in vecs):
                raise EmbedError("embed 返回了空向量")
            return vecs
        msg, retryable = _classify(status, body, attempts)
        if not retryable or attempts > _RETRIES:
            raise EmbedError(msg)
        time.sleep(min(6.0, 1.5 * attempts))


class FakeEmbed:
    """自检/离线用的确定性 embed：文本哈希 → 固定维度单位向量。可当 transport 之外的 embed_fn。"""

    def __init__(self, dim=8):
        self.dim = dim
        self.calls = 0

    def __call__(self, texts, cfg=None):
        import hashlib
        self.calls += 1
        out = []
        for t in texts:
            h = hashlib.sha256(t.encode("utf-8")).digest()
            vec = [b / 255.0 for b in h[: self.dim]]
            s = sum(v * v for v in vec) ** 0.5 or 1.0
            out.append([v / s for v in vec])
        return out
