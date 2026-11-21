# -*- coding: utf-8 -*-
"""kb_notices.py —— 明日方舟官网公告抓取 → kb-corpus md 语料（10-05「让agent会读公告」）。

链路（全部实测于 10-05，详见文件尾注释）：
  1) GET https://ak.hypergryph.com/news —— 页面是 Next.js RSC，公告名册内嵌在
     self.__next_f.push 段里（initialData.LATEST.list，约 96 条，字段
     cid/tab/title/author/displayTime/brief）。
  2) 逐条 GET https://ak.hypergryph.com/news/{cid} —— 正文直接 SSR 在 HTML 里，
     剥 script/style 再剥标签即可拿到干净文本，尾部导航用固定锚截掉。
  3) 每条产一个 front matter md 到 state/kb-corpus/ark/notice-<cid>.md，
     source 写成 web:ak.hypergryph.com/news/<cid> 官网公告（import 侧按 source
     整批替换，同 cid 重复抓取幂等）。

实测坑（勿改口径）：
  - curl 直连与走本机代理都只回 5 字节 "/news"（TLS 指纹反爬）；Python urllib +
    浏览器 UA 直连 HTTP 200 才拿得动整页。
  - __next_f 段必须逐段 json.loads('"' + seg + '"') 解码；用 latin-1 或
    unicode_escape 会把中文吃掉。

用法：
  python tools\\kb_notices.py [--top 12] [--out <目录>] [--quiet]
产完 md 后自行入库（脚本末尾会打印这条命令）：
  python tools\\kb.py import state\\kb-corpus\\ark --game ark
"""
import argparse
import html as _htmlmod
import json
import os
import re
import sys
import time
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NEWS_LIST_URL = "https://ak.hypergryph.com/news"
NEWS_DETAIL_URL = "https://ak.hypergryph.com/news/%s"
# 浏览器 UA：官网对非浏览器 UA 直接反爬（见文件头实测坑）
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0 Safari/537.36")
# 详情页尾部固定导航锚（正文在这些行之前结束）
NOISE_ANCHORS = ("INFO", "返回列表", "GO BACK", "返回首页", "HOMEPAGE")
_DATE_LINE = re.compile(r"^\d{4} // \d{2} / \d{2}$")


def fetch(url, timeout=30):
    req = urllib.request.Request(url, headers={
        "User-Agent": UA,
        "Accept": "text/html,application/xhtml+xml",
        "Accept-Language": "zh-CN,zh;q=0.9",
    })
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read().decode("utf-8", "replace")


def decode_next_f(html_text):
    """__next_f 推送段 → 拼接解码后的 blob 字符串。坏段跳过不整体弃。"""
    segs = re.findall(r'self\.__next_f\.push\(\[1,\s*"(.*?)"\]\)', html_text, re.S)
    parts = []
    for s in segs:
        try:
            parts.append(json.loads('"' + s + '"'))
        except ValueError:
            continue
    return "".join(parts)


def _scan_json_array(text, open_idx):
    """从 text[open_idx]=='[' 起做括号配对（认字符串与转义），返回子串或 None。"""
    depth = 0
    in_str = False
    esc = False
    for i in range(open_idx, len(text)):
        ch = text[i]
        if in_str:
            if esc:
                esc = False
            elif ch == "\\":
                esc = True
            elif ch == '"':
                in_str = False
            continue
        if ch == '"':
            in_str = True
        elif ch == "[":
            depth += 1
        elif ch == "]":
            depth -= 1
            if depth == 0:
                return text[open_idx:i + 1]
            if depth < 0:
                return None
    return None


def extract_notice_items(html_text):
    """列表页 HTML → 公告名册 list[dict]（只保留有 cid+title+displayTime 的条目）。"""
    blob = decode_next_f(html_text)
    anchor = '"LATEST":{"list":'
    i = blob.find(anchor)
    if i < 0:
        return []
    open_idx = blob.find("[", i + len(anchor))
    if open_idx < 0:
        return []
    raw = _scan_json_array(blob, open_idx)
    if raw is None:
        return []
    try:
        arr = json.loads(raw)
    except ValueError:
        return []
    items = []
    for it in arr:
        if not isinstance(it, dict):
            continue
        cid = str(it.get("cid") or "")
        title = str(it.get("title") or "").strip()
        ts = it.get("displayTime")
        if not cid or not title or not isinstance(ts, (int, float)):
            continue
        items.append({
            "cid": cid,
            "title": title,
            "author": str(it.get("author") or "").strip(),
            "displayTime": int(ts),
            "brief": str(it.get("brief") or "").strip(),
            "tab": str(it.get("tab") or ""),
        })
    return items


def notice_body_text(html_text):
    """详情页 HTML → 干净正文纯文本。剥 script/style → 块级标签转换行 → 剥残余标签；
    头部切到 `YYYY // MM / DD` 日期行之后，尾部切到固定导航锚之前。找不到锚就保守全留。"""
    t = re.sub(r"<(script|style)\b.*?</\1>", " ", html_text, flags=re.S | re.I)
    t = re.sub(r"<(?:br|/p|/div|/h[1-6]|/li|/tr)\s*/?>", "\n", t, flags=re.I)
    t = re.sub(r"<[^>]+>", " ", t)
    t = _htmlmod.unescape(t)
    lines = [ln.replace("\u00a0", " ").strip() for ln in t.split("\n")]
    lines = [ln for ln in lines if ln]
    start = 0
    for i, ln in enumerate(lines[:12]):
        if _DATE_LINE.match(ln):
            start = i + 1
            break
    end = len(lines)
    for i in range(start, len(lines)):
        if lines[i] in NOISE_ANCHORS:
            end = i
            break
    body = "\n".join(lines[start:end]).strip()
    return body


def build_notice_md(item, body):
    """条目+正文 → kb-corpus md（front matter 契约见 kb/chunk.py parse_front_matter）。"""
    day = time.strftime("%Y-%m-%d", time.localtime(item["displayTime"]))
    url = NEWS_DETAIL_URL % item["cid"]
    title = item["title"]
    parts = [
        "---",
        "game: ark",
        "updated: %s" % day,
        "title: %s" % title,
        "source: web:%s 官网公告" % url,
        "---",
        "",
        "# %s" % title,
        "",
        "发布日期 %s，作者 %s。来源 %s。" % (day, item["author"] or "官网", url),
        "",
    ]
    if body:
        parts.append(body)
    else:
        # 详情页正文抓取失败时退化为列表页 brief，仍可检索；标记如实写
        parts.append("（正文未取到，以下仅为官网列表摘要）\n\n%s" % item["brief"])
    parts.append("")
    return "\n".join(parts)


def run(top, out_dir, sleep_s, quiet):
    def say(msg):
        if not quiet:
            sys.stdout.write(msg + "\n")

    page = fetch(NEWS_LIST_URL)
    items = extract_notice_items(page)
    if not items:
        say("列表解析为空：页面形状可能变了（__next_f 或 initialData.LATEST 锚不在），未写任何文件")
        return 2
    items.sort(key=lambda it: it["displayTime"], reverse=True)
    picked = items[:top]
    say("名册共 %d 条，取最新 %d 条抓详情" % (len(items), len(picked)))
    os.makedirs(out_dir, exist_ok=True)
    wrote = unchanged = failed = 0
    for it in picked:
        path = os.path.join(out_dir, "notice-%s.md" % it["cid"])
        body = ""
        try:
            body = notice_body_text(fetch(NEWS_DETAIL_URL % it["cid"]))
        except Exception as e:
            say("  cid=%s 详情抓取失败（%r），正文退化为 brief" % (it["cid"], e))
        md = build_notice_md(it, body)
        old = None
        if os.path.exists(path):
            try:
                with open(path, "r", encoding="utf-8") as f:
                    old = f.read()
            except OSError:
                old = None
        if old == md:
            unchanged += 1
        else:
            tmp = path + ".tmp"
            with open(tmp, "w", encoding="utf-8", newline="\n") as f:
                f.write(md)
            os.replace(tmp, path)
            wrote += 1
            say("  写入 notice-%s.md（%s）" % (it["cid"], it["title"]))
        if not body:
            failed += 1
        time.sleep(sleep_s)
    say("完成：新写 %d、未变 %d、其中无正文退化 %d" % (wrote, unchanged, failed))
    rel = os.path.relpath(out_dir, ROOT)
    say("入库命令：python tools\\kb.py import %s --game ark" % rel)
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description="明日方舟官网公告 → kb-corpus md")
    ap.add_argument("--top", type=int, default=12, help="抓最新 N 条详情（默认 12）")
    ap.add_argument("--out", default=os.path.join(ROOT, "state", "kb-corpus", "ark"),
                    help="md 输出目录（默认 state\\kb-corpus\\ark）")
    ap.add_argument("--sleep", type=float, default=0.6, help="详情逐条间隔秒（默认 0.6）")
    ap.add_argument("--quiet", action="store_true", help="只打错误与摘要")
    args = ap.parse_args(argv)
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    return run(args.top, args.out, args.sleep, args.quiet)


if __name__ == "__main__":
    sys.exit(main())
