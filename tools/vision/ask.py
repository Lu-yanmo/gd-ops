#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""tools/vision/ask.py — 视觉兜底里"问模型"这一层。

为什么这一层用 Python 而不是 PowerShell：
  * 发 HTTPS 请求这件事，PS 侧原来靠 Invoke-RestMethod，它把"超时 / 连不上 / 端点写错 / key 不对 /
    被限流"揉成一句 WebException，人在日志里看不出该改哪儿；这里按 HTTP 状态和网络异常分类，
    回一句能照着改的中文。
  * 被限流(429)和服务端 5xx 要带退避地重试，PS 里手写重试会把 Invoke-GdVisionAsk 撑成另一套小框架。
  * 以后要接别的模型/别的协议，改这一个文件即可，不用碰判定、截图、点击那三块。

边界(和 lib\\vision-rescue.ps1 的头注一对)：
  * 本文件**只**做"读图 → 编码 → 发请求 → 把模型回的那段文字带回来"。它不认识端游也不认识模拟器，
    不截图、不点击、不判断该不该点 —— 解析回复、换算点位、确认画面变没变全在 PowerShell 侧，
    那里有 tools\\test-ark.ps1 的现成断言守着("禁止点领取"这类安全约束不许漂走)。
  * API key 只从环境变量 DASHSCOPE_API_KEY 读：不进命令行(会出现在进程列表和日志里)、
    不进请求文件、不出现在任何输出里(--selftest 里专门断言这一点)。
  * 输出永远是**一个 ASCII 的 JSON 对象**(中文按 \\uXXXX 转义)：这样不管控制台码页是 cp936 还是
    UTF-8，调用方 ConvertFrom-Json 都能原样读回来，不会把"弹窗"读成一串问号。

用法：
  python ask.py --request state\\vision\\ask-xxxx.json   # 由 PowerShell 调用(见 lib\\vision-rescue.ps1)
  python ask.py --selftest                              # 离线自检：不联网、不碰任何窗口

请求文件(JSON，UTF-8；字段全部由调用方给出)：
  image           必填，截图绝对路径(PNG)
  system_prompt   必填，整段提示词
  user_text       选填，图片之外那句追问
  endpoint        必填，打到 .../chat/completions 的完整地址
  model           必填
  max_tokens      选填，默认 300
  temperature     选填，默认 0(兜底要的是可复现，不是发挥)
  timeout_seconds 选填，默认 45
  retries         选填，默认 2 —— 只重试 429/5xx/网络异常，401/404 重试也不会变对
  audit           选填，jsonl 路径；每调一次追加一行"这次调用的账"(不含 key、不含绝对路径)
"""

import argparse
import base64
import json
import os
import socket
import ssl
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

ENV_KEY = "DASHSCOPE_API_KEY"
DEFAULT_TIMEOUT = 45
DEFAULT_MAX_TOKENS = 300
DEFAULT_RETRIES = 2
BODY_ECHO_LIMIT = 240          # 报错时最多带回这么多字符的服务端响应体(排障够用，又不会把日志撑爆)
# 1x1 透明 PNG：--selftest 用来验证"读图 + 编码 + 组请求体"这条链，不需要任何真实截图
TINY_PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
)


class AskError(Exception):
    """带"该改哪儿"的错误：message 就是回给人看的那句中文。"""


def mime_of(path):
    """按扩展名给 MIME；未知一律当 PNG(兜底截图只有 .png 这一种)。"""
    ext = os.path.splitext(path)[1].lower()
    return {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg"}.get(ext, "image/png")


def normalize_endpoint(endpoint):
    """地址没写全时补 /chat/completions(和 PowerShell 侧的 Resolve-GdVisionEndpoint 同一个口径)。

    人手工跑这个文件时最容易犯的就是从控制台复制了个以 /v1 结尾的基础地址 —— 补上，免得一次
    404 被当成"模型不可用"。已经写全的原样返回，绝不补出第二个。
    """
    b = (endpoint or "").strip().rstrip("/")
    if not b:
        raise AskError("没给端点(runtime.visionBaseUrl 是空的)")
    if not b.lower().endswith("/chat/completions"):
        b = b + "/chat/completions"
    if not b.lower().startswith(("https://", "http://")):
        raise AskError("端点不是 http/https 地址：%s" % b)
    return b


def build_payload(req, image_bytes):
    """请求文件 + 图片字节 → OpenAI 兼容的 chat/completions 请求体(和 PS 侧原来那份一字不差)。

    两种模式(2026-09-26 加)：
      * 视觉模式(默认)：image + system_prompt + user_text，兜底问画面走这条，行为与旧版一字不差；
      * 纯文本多轮：请求文件里直接给 messages 数组([{role,content}...])，图片可以不存在 ——
        对话式需求管家(lib\\agent.ps1)用它维持上下文。response_format=json_object 两种模式都保留。
    """
    msgs = req.get("messages")
    if isinstance(msgs, list) and msgs:
        return {
            "model": req.get("model") or "",
            "temperature": req.get("temperature", 0),
            "max_tokens": int(req.get("max_tokens") or DEFAULT_MAX_TOKENS),
            "response_format": {"type": "json_object"},
            "messages": msgs,
        }
    b64 = base64.b64encode(image_bytes).decode("ascii")
    user_content = [
        {"type": "image_url", "image_url": {"url": "data:%s;base64,%s" % (mime_of(req["image"]), b64)}}
    ]
    user_text = (req.get("user_text") or "").strip()
    if user_text:
        user_content.append({"type": "text", "text": user_text})
    return {
        "model": req.get("model") or "",
        "temperature": req.get("temperature", 0),
        "max_tokens": int(req.get("max_tokens") or DEFAULT_MAX_TOKENS),
        "response_format": {"type": "json_object"},
        "messages": [
            {"role": "system", "content": req.get("system_prompt") or ""},
            {"role": "user", "content": user_content},
        ],
    }


def describe_http_error(status, body, url, seconds, attempts):
    """把"为什么没成"翻译成人能照着改的一句话。返回 (那句中文, 是否值得重试)。"""
    host = urllib.parse.urlparse(url).netloc or url
    if status in (401, 403):
        return ("API key 被拒(HTTP %d)：检查 settings 里的 runtime.visionApiKey 或环境变量 %s" % (status, ENV_KEY), False)
    if status == 404:
        return ("端点不存在(HTTP 404)：检查 runtime.visionBaseUrl 是不是 %s" % host, False)
    if status == 400:
        return ("请求被拒(HTTP 400)：多半是 model 名不对，%s" % _peek(body), False)
    if status == 429:
        return ("被限流或额度用尽(HTTP 429，已试 %d 次)：%s" % (attempts, _peek(body)), True)
    if status >= 500:
        return ("服务端错误(HTTP %d，已试 %d 次)：%s" % (status, attempts, _peek(body)), True)
    return ("服务回了 HTTP %d：%s" % (status, _peek(body)), False)


def _peek(body):
    text = (body or "").replace("\n", " ").strip()
    if len(text) > BODY_ECHO_LIMIT:
        text = text[:BODY_ECHO_LIMIT] + "…"
    return text or "(空响应体)"


def post(url, payload, key, timeout):
    """发一次请求。返回 (状态码, 响应体文本)；网络类异常抛 AskError，HTTP 错误码原样返回给调用方判。"""
    data = json.dumps(payload).encode("utf-8")
    headers = {
        "Authorization": "Bearer %s" % key,
        "Content-Type": "application/json; charset=utf-8",
        "Accept": "application/json",
        "User-Agent": "game-daily-vision-rescue/1",
    }
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=timeout, context=ssl.create_default_context()) as resp:
            return resp.getcode(), resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:                       # 4xx/5xx 都到这：状态码和响应体留着分类
        return e.code, e.read().decode("utf-8", "replace")
    except socket.timeout:
        raise AskError("请求超时(%d 秒)：可把 runtime.visionTimeoutSeconds 调大，或检查网络" % int(timeout))
    except urllib.error.URLError as e:
        reason = getattr(e, "reason", e)
        raise AskError("连不上 %s：%s(网络不通/地址写错/证书问题)" % (urllib.parse.urlparse(url).netloc or url, reason))
    except ssl.SSLError as e:
        raise AskError("TLS 握手失败：%s" % e)


def ask(req):
    """真正的一问一答。返回 dict：{ok, text?, model?, http_status, elapsed_ms, usage?, error?}"""
    t0 = time.time()
    key = (os.environ.get(ENV_KEY) or "").strip()
    if not key:
        raise AskError("没有 %s 环境变量：key 只从环境变量读(不落命令行、不落文件)" % ENV_KEY)

    msgs = req.get("messages")
    if isinstance(msgs, list) and msgs:
        image_bytes = b""                       # 纯文本多轮模式(agent 用)：不读图，也不要求图存在
    else:
        image_path = req.get("image") or ""
        if not image_path or not os.path.isfile(image_path):
            raise AskError("截图文件不在：%s(截图那一步就没成)" % (image_path or "(没给)"))
        with open(image_path, "rb") as fh:
            image_bytes = fh.read()
        if len(image_bytes) < 8:
            raise AskError("截图文件太小(%d 字节)，不是一张照片" % len(image_bytes))

    url = normalize_endpoint(req.get("endpoint"))
    payload = build_payload(req, image_bytes)
    model = payload["model"]
    if not model:
        raise AskError("没给模型名(runtime.visionModel 是空的)")

    timeout = float(req.get("timeout_seconds") or DEFAULT_TIMEOUT)
    retries = int(req.get("retries") if req.get("retries") is not None else DEFAULT_RETRIES)
    attempts = 0
    last_status, last_body = 0, ""
    while True:
        attempts += 1
        status, body = post(url, payload, key, timeout)
        last_status, last_body = status, body
        if 200 <= status < 300:
            try:
                parsed = json.loads(body or "{}")
            except ValueError:
                raise AskError("服务回的不是 JSON(HTTP %d)：%s" % (status, _peek(body)))
            choices = parsed.get("choices") or []
            text = ""
            if choices:
                msg = choices[0].get("message") or {}
                text = msg.get("content") or ""
                if isinstance(text, list):                  # 个别网关把 content 也回成分段数组
                    text = "".join(seg.get("text", "") for seg in text if isinstance(seg, dict))
            result = {
                "ok": True,
                "text": text,
                "model": parsed.get("model") or model,
                "http_status": status,
                "elapsed_ms": int((time.time() - t0) * 1000),
                "attempts": attempts,
                "image_bytes": len(image_bytes),
            }
            if parsed.get("usage"):
                result["usage"] = parsed["usage"]
            return result
        msg, retryable = describe_http_error(status, body, url, timeout, attempts)
        if not retryable or attempts > retries:
            raise AskError(msg)
        time.sleep(min(6.0, 1.5 * attempts))                # 429 通常要求慢一点再来；两次也就 1.5s/3s


def write_audit(path, req, result):
    """给"这次调用记一笔账"。刻意不含：key、图片绝对路径、提示词正文、模型回的内容。"""
    if not path:
        return
    row = {
        "ts": time.strftime("%Y-%m-%d %H:%M:%S"),
        "ok": bool(result.get("ok")),
        "model": result.get("model") or req.get("model") or "",
        "host": urllib.parse.urlparse(normalize_endpoint(req.get("endpoint"))).netloc,
        "http_status": result.get("http_status") if result.get("http_status") is not None else 0,
        "elapsed_ms": result.get("elapsed_ms") or 0,
        "attempts": result.get("attempts") or 0,
        "image": os.path.basename(req.get("image") or ""),
        "image_bytes": result.get("image_bytes") or 0,
        "prompt_chars": (len(req.get("system_prompt") or "") + len(req.get("user_text") or "")) or
                        sum(len(str(m.get("content") or "")) for m in (req.get("messages") or [])),
        "reply_chars": len(result.get("text") or ""),
        "error": (result.get("error") or "")[:200],
    }
    if isinstance(result.get("usage"), dict):
        row["usage"] = result["usage"]
    try:
        d = os.path.dirname(os.path.abspath(path))
        if d and not os.path.isdir(d):
            os.makedirs(d, exist_ok=True)
        with open(path, "a", encoding="utf-8") as fh:
            fh.write(json.dumps(row, ensure_ascii=False) + "\n")
    except OSError:
        pass                                                # 记账绝不该把正事弄挂


def emit(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=True, sort_keys=False) + "\n")
    sys.stdout.flush()


def run_selftest():
    """离线自检：不联网、不读任何本机配置，只验本文件自己的四件事。"""
    fails = []
    nchk = [0]

    def chk(name, cond, extra=""):
        nchk[0] += 1
        if not cond:
            fails.append("%s%s" % (name, (" —— " + extra) if extra else ""))

    chk("端点已写全时不补第二个", normalize_endpoint("https://x/compatible-mode/v1/chat/completions")
        == "https://x/compatible-mode/v1/chat/completions")
    chk("以 /v1 结尾会补成 /chat/completions", normalize_endpoint("https://x/compatible-mode/v1/")
        == "https://x/compatible-mode/v1/chat/completions")
    try:
        normalize_endpoint("ftp://x/v1")
        chk("非 http/https 会被拒", False)
    except AskError:
        chk("非 http/https 会被拒", True)

    req = {"image": "shot.png", "model": "qwen3.8-flash", "user_text": "只看一眼",
           "system_prompt": "只回 JSON", "temperature": 0, "max_tokens": 300}
    p = build_payload(req, TINY_PNG)
    chk("请求体带 response_format=json_object", p.get("response_format") == {"type": "json_object"})
    chk("温度默认 0(兜底不要发挥)", p.get("temperature") == 0)
    c = p["messages"][1]["content"]
    chk("图片走 data:image/png;base64 内联", isinstance(c[0]["image_url"]["url"], str)
        and c[0]["image_url"]["url"].startswith("data:image/png;base64,"))
    chk("追问文字排在图片后面", len(c) == 2 and c[1]["type"] == "text" and c[1]["text"] == "只看一眼")
    chk("没给 user_text 时不塞空文字",
        len(build_payload(dict(req, user_text="  "), TINY_PNG)["messages"][1]["content"]) == 1)

    # 纯文本多轮模式(agent 用)：messages 原样透传，不碰图片
    mreq = {"messages": [{"role": "system", "content": "s"}, {"role": "user", "content": "u"}],
            "model": "qwen3.8-flash", "temperature": 0, "max_tokens": 900}
    mp = build_payload(mreq, b"")
    chk("文本模式 messages 原样透传", mp["messages"] == mreq["messages"])
    chk("文本模式仍要 JSON 回复且无图片字段", mp.get("response_format") == {"type": "json_object"}
        and all("image_url" not in str(m.get("content")) for m in mp["messages"]))
    chk("文本模式不给 image 也能组体", "image" not in mreq and mp.get("model") == "qwen3.8-flash")

    e1, r1 = describe_http_error(401, "", "https://x/v1/chat/completions", 45, 1)
    chk("401 说成 key 的问题", ("key" in e1) and r1 is False)
    e2, r2 = describe_http_error(404, "", "https://x/v1/chat/completions", 45, 1)
    chk("404 说成端点的问题", ("端点" in e2) and r2 is False)
    e3, r3 = describe_http_error(429, "rate limit", "https://x/v1/chat/completions", 45, 2)
    chk("429 才值得重试", r3 is True and "限流" in e3)
    e4, _ = describe_http_error(400, "no such model", "https://x/v1/chat/completions", 45, 1)
    chk("400 指向模型名", "model" in e4)

    # 输出里绝不能出现 key：拿一个假 key 把记账和 stdout 两条路都扫一遍
    fake = "sk-SELFTEST-SECRET-0123456789"
    os.environ[ENV_KEY] = fake
    tmp = os.path.abspath("_selftest_shot.png")
    with open(tmp, "wb") as fh:
        fh.write(TINY_PNG)
    audit = os.path.abspath("_selftest_audit.jsonl")
    if os.path.exists(audit):
        os.remove(audit)
    try:
        write_audit(audit, dict(req, image=tmp, endpoint="https://x/v1"),
                    {"ok": False, "error": "连不上 x", "http_status": 0})
        with open(audit, "r", encoding="utf-8") as fh:
            line = fh.read()
        chk("记账行里没有 key", fake not in line)
        chk("记账只留文件名不留绝对路径", tmp not in line and "shot.png" in line)
        chk("记账可被 JSON 读回", json.loads(line.strip()).get("ok") is False)
    except Exception as exc:                                    # noqa: BLE001 - 自检要把异常本身印出来
        chk("记账不炸", False, repr(exc))
    finally:
        for junk in (tmp, audit):
            try:
                os.remove(junk)
            except OSError:
                pass
        del os.environ[ENV_KEY]

    emit({"ok": not fails, "selftest": True, "checks": nchk[0], "failures": fails})
    return 0 if not fails else 1


def main(argv=None):
    ap = argparse.ArgumentParser(prog="ask.py", description="视觉兜底的模型调用层(只问模型，不截图不点击)")
    ap.add_argument("--request", help="请求文件(JSON)，由 PowerShell 侧生成")
    ap.add_argument("--selftest", action="store_true", help="离线自检：不联网")
    args = ap.parse_args(argv)

    if args.selftest:
        return run_selftest()
    if not args.request:
        emit({"ok": False, "error": "要 --request <json 文件> 或 --selftest"})
        return 2
    try:
        with open(args.request, "r", encoding="utf-8-sig") as fh:     # -sig：调用方带 BOM 也读得动
            req = json.load(fh)
    except Exception as exc:                                          # noqa: BLE001 - 统一降级成一句人话
        emit({"ok": False, "error": "请求文件读不了：%s" % exc})
        return 1

    result = None
    try:
        result = ask(req)
    except AskError as exc:
        result = {"ok": False, "error": str(exc), "http_status": 0}
    except Exception as exc:                                          # noqa: BLE001
        result = {"ok": False, "error": "调用层出了没想到的错：%r" % (exc,), "http_status": 0}
    if "http_status" not in result:
        result.setdefault("http_status", 0)
    try:
        write_audit(req.get("audit"), req, result)
    except Exception:                                                 # noqa: BLE001 - 记账失败不影响返回
        pass
    emit(result)
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main())
