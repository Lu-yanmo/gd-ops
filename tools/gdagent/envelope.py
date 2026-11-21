"""信封输出：stdout 只出 JSON，日志/进度一律 stderr（铁律见 kb-plan §0）。

PowerShell 薄转发只吃 stdout 那一行 JSON，任何印在 stdout 的人话都会破坏它 ——
所以本模块把这条约束集中强制：emit() 是唯一往 stdout 写结果的出口，log() 只走 stderr。
"""
import json
import sys

_ENVELOPE_FIELDS = ("ok", "why")


def emit(obj):
    """把结果对象作为唯一一行 JSON 打到 stdout。ensure_ascii=True 防中文在 GBK 码页终端变乱码。"""
    sys.stdout.write(json.dumps(obj, ensure_ascii=True, sort_keys=False) + "\n")
    sys.stdout.flush()


def ok(**fields):
    out = {"ok": True}
    out.update(fields)
    out.setdefault("why", "")
    return out


def fail(why, **fields):
    out = {"ok": False, "why": str(why)}
    out.update(fields)
    return out


def log(msg):
    sys.stderr.write(str(msg) + "\n")
    sys.stderr.flush()
