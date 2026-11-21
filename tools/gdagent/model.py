"""gdagent.model —— agent 的模型往返(M2：吸收 tools\\vision\\ask.py)。

为什么是"复用"不是"重写"：ask.py 的端点补全/错误分类/退避重试/审计记账被视觉兜底和
agent 两条路共用，重写一份 = 制造第二个口径(全家反复合过的规：一份规则一个家)。
本文件只做三件事：
  1) 把 ask.py 按路径 import 进来(spec_from_file_location，它不在任何包里的事实不变)；
  2) 按 lib\\agent.ps1 的三条优先级把参数配齐 ——
     key：环境变量 DASHSCOPE_API_KEY 优先，回落 settings.runtime.visionApiKey(只读不打印)；
     模型：agentModel → visionModel → qwen3.8-flash；超时：agentTimeoutSeconds → visionTimeoutSeconds → 90；
  3) 进程内直调 ask()：不再起子进程、不再落请求文件(PS 侧那套是给"不能 import"的引擎用的绕路)。
key 注入方式：临时写进本进程 environ 再调 ask()(它只认环境变量)——调用完立刻还原，
不落命令行、不落请求文件、不进日志。
"""
import importlib.util
import json
import os
from pathlib import Path

ASK_PATH = Path(__file__).resolve().parents[1] / 'vision' / 'ask.py'

DEFAULT_CHAT_MODEL = 'qwen3.8-flash'
DEFAULT_BASE_URL = 'https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions'
MAX_TOKENS = 1200
TEMPERATURE = 0

_ask_mod = None


def _load_ask():
    global _ask_mod
    if _ask_mod is None:
        if not ASK_PATH.is_file():
            raise RuntimeError('这棵树没有 tools\\vision\\ask.py')
        spec = importlib.util.spec_from_file_location('gdagent_ask', str(ASK_PATH))
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        _ask_mod = mod
    return _ask_mod


def resolve_key(settings, environ=None):
    """Get-GdVisionApiKey 同规：env DASHSCOPE_API_KEY 优先，回落 settings 的 visionApiKey。只回串，永不打印。"""
    env = environ if environ is not None else os.environ
    k = (env.get('DASHSCOPE_API_KEY') or '').strip()
    if not k:
        rt = (settings or {}).get('runtime') or {}
        k = str(rt.get('visionApiKey') or '').strip()
    return k


def resolve_model(settings):
    rt = (settings or {}).get('runtime') or {}
    return str(rt.get('agentModel') or rt.get('visionModel') or DEFAULT_CHAT_MODEL)


def resolve_timeout(settings):
    rt = (settings or {}).get('runtime') or {}
    for key in ('agentTimeoutSeconds', 'visionTimeoutSeconds'):
        v = rt.get(key)
        if v:
            try:
                return float(v)
            except (TypeError, ValueError):
                pass
    return 90.0


def resolve_endpoint(settings):
    """端点解析交给 ask.normalize_endpoint 补全 —— 传 base 或全 URL 都可以；settings 没给用默认。"""
    rt = (settings or {}).get('runtime') or {}
    base = str(rt.get('visionBaseUrl') or '').strip()
    return base or DEFAULT_BASE_URL


def chat(messages, settings, environ=None, audit_path=None):
    """一次文本多轮问答。返回 {'Ok':bool,'Text'|'Error':str}，任何不对都不抛 ——
    对话循环里要的是"回一句人话"，与 Invoke-GdAgentModel 同契约。
    成败都记一笔账(和 ask.py main() 的顺序一致：先出结果，再写审计，记账失败不影响返回)。"""
    key = resolve_key(settings, environ)
    if not key:
        return {'Ok': False, 'Error': '没有 API key(runtime.visionApiKey 或环境变量 DASHSCOPE_API_KEY)'}
    sec = resolve_timeout(settings)
    req = {
        'messages': list(messages),
        'endpoint': resolve_endpoint(settings),
        'model': resolve_model(settings),
        'temperature': TEMPERATURE,
        'max_tokens': MAX_TOKENS,
        'timeout_seconds': sec,
    }
    if audit_path:
        req['audit'] = str(audit_path)
    try:
        ask_mod = _load_ask()
    except Exception as exc:                                 # noqa: BLE001
        return {'Ok': False, 'Error': '模型调用层起不来：%s' % exc}
    saved = os.environ.get('DASHSCOPE_API_KEY')
    os.environ['DASHSCOPE_API_KEY'] = key
    try:
        result = ask_mod.ask(req)
    except ask_mod.AskError as exc:
        result = {'ok': False, 'error': str(exc), 'http_status': 0}
    except Exception as exc:                                 # noqa: BLE001
        result = {'ok': False, 'error': '调用层出了没想到的错：%r' % (exc,), 'http_status': 0}
    finally:
        if saved is None:
            os.environ.pop('DASHSCOPE_API_KEY', None)
        else:
            os.environ['DASHSCOPE_API_KEY'] = saved
    try:
        if audit_path:
            ask_mod.write_audit(str(audit_path), req, result)
    except Exception:                                        # noqa: BLE001 - 记账不影响返回
        pass
    if result.get('ok'):
        return {'Ok': True, 'Text': result.get('text', '')}
    return {'Ok': False, 'Error': str(result.get('error') or '模型没回内容')}


def read_settings(root):
    path = Path(root) / 'config' / 'settings.json'
    if not path.is_file():
        return {}
    with open(str(path), 'rb') as fh:
        return json.loads(fh.read().decode('utf-8', 'replace') or '{}')
