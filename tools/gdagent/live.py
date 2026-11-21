"""gdagent.live —— agent 的活层装配(M2)：把 engine.send_exchange 的注入缝接到真机件。

分工(与 engine.py 头注同口径)：
  * engine.py 管纯流程与语义(自检/对拍钉死)，本文件只负责"缝到真东西上"，不掺规则；
  * 模型往返走 model.py(进程内复用 ask.py，key 只走环境变量、只读不打印)；
  * 快照/关卡表走 PS 侧留下的活段出口(`tools\\agent.ps1 -Snapshot`，补刷判表自此问 C# 判定层)；
    探不到就明说什么缺(降级快照)，绝不让一轮对话因为快照卡死；
  * farmTargets 逐条解析不在这里重写 —— 走 C# 判定层 verb `parse-farm-target-spec`(批次 5 的真身)，
    信封契约 = docs\\ps-migration-batch0.md §2(--in/--out 文件，stdout 不参与判定)；
  * run-verb 就是起子进程跑 daily-clear.ps1 —— 与 Invoke-GdAgentVerb 同源：900 秒封顶、
    只留关键日志行([STEP|WARN|OK|ERROR|INFO]/结果/方案/计划/补刷)、末 18 行、剥掉行首 [xx] 标签。

解释器查找与 Get-GdAgentChildPwsh 的对应：PS 侧顺序是"装机 7 → 当前进程 → PSHOME"；
Python 侧没有"当前 PowerShell 进程"可借，等价顺序 = PATH 上的 pwsh → Program Files\\PowerShell\\*。
两边都只找 pwsh 7 —— C 轮(2026-10-06)退役 5.1 后，"PATH 上的 powershell → System32 5.1"两档已删；
本机 pwsh 是 Store 版这一事实不变(2026-09-26 在案)。
"""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from . import engine as EN
from . import model as MD

VERB_LINE = re.compile(r'\[(STEP|WARN|OK|ERROR|INFO)\]|结果|方案|计划|补刷')
VERB_LEAD = re.compile(r'^\[[^\]]*\]')


def _decode(b):
    """pwsh7 重定向是 UTF-8；5.1 按码页(GBK) —— 先 UTF-8 严判，失败退 cp936，永不因解码炸掉一轮。"""
    if not b:
        return ''
    for enc in ('utf-8', 'cp936'):
        try:
            return b.decode(enc).lstrip('\ufeff')
        except (UnicodeDecodeError, LookupError):
            continue
    return b.decode('utf-8', 'replace')


def find_pwsh():
    which = shutil.which('pwsh')
    if which:
        return which
    pf = os.environ.get('ProgramFiles') or r'C:\Program Files'
    base = Path(pf) / 'PowerShell'
    if base.is_dir():
        for exe in sorted(base.glob('*/pwsh.exe'), reverse=True):     # 高版本目录优先
            if exe.is_file():
                return str(exe)
    return None


def _run(cmd, timeout):
    cp = subprocess.run(cmd, capture_output=True, timeout=timeout)   # noqa: S603 - 路径全来自本机枚举
    return cp.returncode, _decode(cp.stdout), _decode(cp.stderr)


# ---------- PS 活段出口：一次调用同时带回快照与关卡表 ----------
def ps_probe(root):
    """跑 `tools\\agent.ps1 -Snapshot`，拿它印的那一行 JSON({'snapshot':…,'known':{…}})。
    任何不对都回 None(入口还没加这档 / pwsh 找不到 / 超时 / 输出不是 JSON) —— 调用方各有降级。"""
    exe = find_pwsh()
    entry = Path(root) / 'tools' / 'agent.ps1'
    if not exe or not entry.is_file():
        return None
    cmd = [exe, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(entry), '-Snapshot']
    try:
        _code, out, _err = _run(cmd, 180)
    except Exception:                                                # noqa: BLE001
        return None
    for line in out.splitlines():
        s = line.strip()
        if not s.startswith('{'):
            continue
        try:
            row = json.loads(s)
        except ValueError:
            continue
        if isinstance(row, dict) and 'snapshot' in row:
            return row
    return None


def degraded_snapshot(paths, settings):
    """快照助手没应答时的最低限度：配置段照读、判表明说没算成、journal 尾照拼(与 PS 的三段同形)。"""
    bits = []
    try:
        ark = (settings or {}).get('ark') or {}
        cfg = {
            'farmTargets': ark.get('farmTargets'),
            'defaultStage': EN.ps_str(ark.get('defaultStage')),
            'planByWeekday': ark.get('planByWeekday'),
            'farmAppend': ark.get('farmAppend'),
            'farmMaxTimesPerRound': ark.get('farmMaxTimesPerRound'),
        }
        bits.append('配置：' + EN.compact(cfg))
    except Exception:                                                # noqa: BLE001
        bits.append('配置：读不到')
    bits.append('补刷判表：快照助手没应答')
    try:
        if paths.journal.is_file():
            lines = [ln for ln in paths.journal.read_text(encoding='utf-8-sig').splitlines() if ln.strip()]
            tail = lines[-3:]
            if tail:
                bits.append('最近留底：' + ' / '.join(ln.replace('"ts":', '') for ln in tail))
    except Exception:                                                # noqa: BLE001
        pass
    txt = '\n'.join(bits)
    if len(txt) > 1800:
        txt = txt[:1800] + '…(截断)'
    return txt


# ---------- farmTargets 解析器：C# 判定层 verb 的薄适配(信封契约同 Invoke-GdCli) ----------
def ensure_cli_exe(root):
    exe = Path(root) / 'tools' / 'panel-cs' / 'panel-cs.exe'
    if exe.is_file():
        return str(exe)
    pw = find_pwsh()
    build = Path(root) / 'tools' / 'panel-cs' / 'build.ps1'
    if pw and build.is_file():                                       # Get-GdCliExe 的现场自愈同款
        try:
            _run([pw, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(build), '-Quiet'], 300)
        except Exception:                                            # noqa: BLE001
            pass
    if not exe.is_file():
        raise RuntimeError('C# 判定层编译失败：跑 tools\\panel-cs\\build.ps1 看具体报错(产物 %s 仍不存在)' % exe)
    return str(exe)


def make_spec_parser(root):
    """C# 判定层(panel-cs 及其 build.ps1)都不在树里 → None(engine 会给出'这棵树没有 C# 判定层'的原话)；
    在树里 → 每条起一次 panel-cs verb。Found:false = 旧语义的 $null(空条目)。
    ⚠ 转接刀·运行面刀⑬(2026-11-06)：在场性代理从 `lib\\ark-farm.ps1` 改指 verb 的真宿主 —— PS 侧真身出仓后，
       ark-farm.ps1 不在场不再是"验不了 farmTargets"的判据(那枚代理会随件一起变成假红源)。"""
    pc = Path(root) / 'tools' / 'panel-cs'
    if not (pc / 'panel-cs.exe').is_file() and not (pc / 'build.ps1').is_file():
        return None

    def spec_parser(raw):
        exe = ensure_cli_exe(root)
        tmp = tempfile.mkdtemp(prefix='gd-farm-spec-')
        in_path = os.path.join(tmp, 'in.json')
        out_path = os.path.join(tmp, 'out.json')
        try:
            with open(in_path, 'w', encoding='utf-8') as fh:
                fh.write(EN.compact({'raw': raw}))
            try:
                _run([exe, 'parse-farm-target-spec', '--in=%s' % in_path, '--out=%s' % out_path], 60)
            except Exception as exc:                                 # noqa: BLE001
                raise RuntimeError('farmTargets 解析层没跑成：%s' % exc)
            if not os.path.isfile(out_path):
                raise RuntimeError('farmTargets 解析层没吐出结果文件')
            env0 = EN.read_json(out_path)
            if not env0.get('ok'):
                raise RuntimeError(str(env0.get('error') or 'farmTargets 解析层报了个没名字的错'))
            data = env0.get('data') or {}
            if not data.get('Found'):
                return None
            spec = data.get('Spec')
            return spec if isinstance(spec, dict) else None
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    return spec_parser


# ---------- run-verb：daily-clear 子进程(Invoke-GdAgentVerb 同规) ----------
def make_verb_fn(root, known_stages, spec_parser):
    def verb_fn(verb_args, allowed=False):
        args = [EN.ps_str(a) for a in (verb_args or [])]
        chk = EN.check_verb_args(args, known_stages, spec_parser)    # 进门先重闸(PS 侧同规：执行层不信上游)
        if not chk['Ok']:
            return {'Ran': False, 'Exit': -1, 'Detail': chk['Why']}
        if chk['Mutating'] and not allowed:
            return {'Ran': False, 'Exit': -1, 'Detail': '这一步要真跑(会烧理智)，等玩家当面确认'}
        exe = find_pwsh()
        if not exe:
            return {'Ran': False, 'Exit': -1, 'Detail': '找不到 pwsh.exe(PowerShell 7)——PATH/Program Files 全落了，请先安装：winget install Microsoft.PowerShell'}
        cmd = [exe, '-NoProfile', '-ExecutionPolicy', 'Bypass',
               '-File', str(Path(root) / 'daily-clear.ps1')] + args
        try:
            cp = subprocess.run(cmd, capture_output=True, timeout=900)   # noqa: S603
            code, out, err = cp.returncode, _decode(cp.stdout), _decode(cp.stderr)
        except subprocess.TimeoutExpired as exc:
            code, out, err = -1, _decode(exc.output or b''), _decode(exc.stderr or b'')
        lines = [ln for ln in (out + '\n' + err).splitlines() if VERB_LINE.search(ln)][-18:]
        return {'Ran': True, 'Exit': code, 'Detail': '\n'.join(VERB_LEAD.sub('', ln) for ln in lines)}

    return verb_fn


# ---------- 装配一台能聊的 agent ----------
def pick_kb_games(rt):
    """P5 联合检索的域集合(纯函数，测试可钉)：优先 runtime.kbAgentGames(列表)，
    没配或全空回落单值 runtime.kbAgentGame —— 旧配置行为逐字不变。
    域永远由引擎注入(模型只能给 query/top)：合体上下文配 ["r1999","ark"] 后，
    r1999 语境问方舟机制也能命中(回执逐条标归属，教材 §KB 防线配套)。"""
    raw = rt.get('kbAgentGames')
    if isinstance(raw, (list, tuple)):
        named = []
        for g in raw:
            s = str(g).strip()
            if s and s not in named:
                named.append(s)
        if named:
            return named
    one = str(rt.get('kbAgentGame') or '').strip()
    return [one] if one else []


def kb_game_override(env):
    """P5 补刀：跑批的单游戏域隔离改走环境变量 GDAGENT_KB_GAME —— 设了就**优先于 settings 两键**
    (kb-search 域=[X]、progression 也查 X)。决策题跑批曾用字节级 regex 切 settings 的 kbAgentGame，
    P5 起列表键优先会静默盖掉单值(混合域跑分判据漂移)，且写 settings 本身有并行会话覆写竞态(在案)；
    env 注入零写盘、进程死了自然失效。未设/空白 → None(照走 settings，日常对话行为不变)。
    10-06 跨游戏训练轮扩：**吃逗号列表**('ark,r1999') —— 对比题必须让检索域同时含两游戏才谈得上比较；
    查表(progression/farm)照旧只认第一枚(查表按当前游戏收窄是 P5 定案，不随这条变)。"""
    raw = (env or {}).get('GDAGENT_KB_GAME')
    if raw is None:
        return None
    parts = []
    for p in str(raw).split(','):
        p = p.strip()
        if p and p not in parts:      # 去重保序：'ark,ark' 与 'ark' 同一个域集合
            parts.append(p)
    return parts or None


def make_kb_fn(root):
    """M3(kb-plan §4)：kb-search 的进程内检索钩子。
    game 由配置注入(runtime.kbAgentGames/kbAgentGame，env 的 GDAGENT_KB_GAME 优先——跑批隔离用)，
    模型只能给 query/top —— KB 是外部数据，检索域这种越权面一个字都不交给模型。
    每次现读 settings：embed 模型/端点改了立刻生效。"""
    from . import config as CFG
    from .kb import manager as MK
    from .kb import store as ST

    def kb_fn(query, top):
        rt = (MD.read_settings(root) or {}).get('runtime') or {}
        games = kb_game_override(os.environ) or pick_kb_games(rt)
        if not games:
            return {'ok': False, 'hits': [],
                    'why': '没配 runtime.kbAgentGame，知识库检索跳过(需要游戏上下文才能查)'}
        cfg = CFG.Config({'runtime': rt})
        store = ST.Store(Path(root) / 'state' / 'kb')
        store.sweep_partial()
        return MK.do_search(store, cfg, query,
                            game=(games[0] if len(games) == 1 else games), top=top)

    return kb_fn


def make_prog_fn(root):
    """第五动词 progression-lookup 的进程内查表钩子(M4 立项口径：数值走结构化表，不进向量库)。
    game 与 kb-search 同一个会话上下文键(runtime.kbAgentGame，env 的 GDAGENT_KB_GAME 优先)，
    模型只能给 char/line/stage。"""
    from . import progression as PG

    def prog_fn(char, line, stage):
        rt = (MD.read_settings(root) or {}).get('runtime') or {}
        ov = kb_game_override(os.environ)
        game = ov[0] if ov else str(rt.get('kbAgentGame') or '').strip()
        if not game:
            return {'ok': False, 'why': '没配 runtime.kbAgentGame，不知道查哪款游戏的养成表'}
        return PG.lookup(root, game, char, line, stage)

    return prog_fn


def make_farm_fn(root):
    """第六动词 farm-lookup 的进程内查表钩子(DROP 轮：刷本效率走结构化表 state/drops，推荐现算)。
    game 与 progression-lookup 同一个会话上下文键(runtime.kbAgentGame，env GDAGENT_KB_GAME 优先)，
    模型只能给 material/mode。"""
    from . import drops as DP

    def farm_fn(material, mode=''):
        rt = (MD.read_settings(root) or {}).get('runtime') or {}
        ov = kb_game_override(os.environ)
        game = ov[0] if ov else str(rt.get('kbAgentGame') or '').strip()
        if not game:
            return {'ok': False, 'why': '没配 runtime.kbAgentGame，不知道查哪款游戏的掉落表'}
        return DP.lookup(root, game, material, mode)

    return farm_fn


def build_context(root, settings, paths):
    """把 engine.send_exchange 需要的六个缝一次配齐；PS 探针只在开头打一次，
    开头就没通(入口不带 -Snapshot / 没 pwsh)就不再每轮白等 180 秒。"""
    probe0 = ps_probe(root)
    known = (probe0 or {}).get('known') or {}
    ps_alive = probe0 is not None
    spec_parser = make_spec_parser(root)

    def snapshot_fn():
        if not ps_alive:
            return degraded_snapshot(paths, settings)
        probe = ps_probe(root)
        if probe is None:
            return degraded_snapshot(paths, settings)
        return EN.ps_str(probe.get('snapshot')) or degraded_snapshot(paths, settings)

    def model_fn(msgs):
        return MD.chat(msgs, settings, audit_path=str(paths.agent_dir / 'calls.jsonl'))

    return {
        'snapshot_fn': snapshot_fn,
        'model_fn': model_fn,
        'verb_fn': make_verb_fn(root, known, spec_parser),
        'kb_fn': make_kb_fn(root),
        'prog_fn': make_prog_fn(root),
        'farm_fn': make_farm_fn(root),
        'known_stages': known,
        'spec_parser': spec_parser,
    }


def greeting(root, paths, settings):
    """Get-GdAgentGreeting 同词：教材在不在、key 配没配(永不打印值)、模型名。"""
    gb_txt = ('教材已就位(%s)' % paths.gamebook.name) if paths.gamebook.is_file() \
        else '警告：教材缺失，模型会照兜底话术拒办 —— 先补 docs\\agent-gamebook.md'
    key_txt = 'key 已配(不打印)' if MD.resolve_key(settings) \
        else 'key 没配 → 问不成模型：settings.runtime.visionApiKey 或环境变量 DASHSCOPE_API_KEY'
    return '需求管家 · 模型 %s · %s · %s' % (MD.resolve_model(settings), gb_txt, key_txt)


def _guard_console():
    """人用/机读通道共用的控制台守卫：GBK 码页控制台上 print 会炸(2026-09-28 实机撞过，
    模型回复里的 • 、† 这类字编不出)。编码本身跟着控制台不动(防乱码)，只把编不出的字符换成 ?
    —— 宁可一个问号，不许崩一轮对话。"""
    for _s in (sys.stdout, sys.stderr):
        try:
            _s.reconfigure(errors='replace')
        except Exception:                                    # noqa: BLE001 - 换不掉就维持原样
            pass


def run_agent(root, ask='', rollback=False, rollback_dry=False):
    """`python -m gdagent agent` 的总入口。返回进程退出码。
    回滚两条通道不经过模型(纯本地)；对话通道 -Ask 单发一句，不带就是 REPL。"""
    _guard_console()
    paths = EN.Paths(root)
    if rollback_dry:
        print(EN.rollback_settings(paths, dry=True))
        return 0
    if rollback:
        print(EN.rollback_settings(paths))
        return 0
    settings = MD.read_settings(root)
    if not paths.settings.is_file():
        print('找不到 config\\settings.json —— 先跑 setup.ps1 把工程配起来，agent 不猜路径。')
        return 1
    print(greeting(root, paths, settings), file=sys.stderr)
    ctx = build_context(root, settings, paths)
    history = []
    kb_state = {'seen': set()}          # 会话级检索去重(跨轮持有，防检索循环)
    if ask:
        EN.send_exchange(paths, ask, history, ctx['model_fn'], ctx['verb_fn'],
                         known_stages=ctx['known_stages'], spec_parser=ctx['spec_parser'],
                         snapshot_fn=ctx['snapshot_fn'], kb_fn=ctx['kb_fn'], kb_state=kb_state,
                         prog_fn=ctx['prog_fn'], farm_fn=ctx['farm_fn'])
        return 0
    print('说需求即可(例：明天开始刷特种芯片组，刷到 8 个就停)。exit / quit 退出。', file=sys.stderr)
    while True:
        try:
            line = input('你')
        except (EOFError, KeyboardInterrupt):
            break
        t = line.strip()
        if t in ('exit', 'quit', 'q'):
            break
        if not t:
            continue
        EN.send_exchange(paths, t, history, ctx['model_fn'], ctx['verb_fn'],
                         known_stages=ctx['known_stages'], spec_parser=ctx['spec_parser'],
                         snapshot_fn=ctx['snapshot_fn'], kb_fn=ctx['kb_fn'], kb_state=kb_state,
                         prog_fn=ctx['prog_fn'], farm_fn=ctx['farm_fn'])
    return 0


# ---------- 两击确认的机读口(面板上那颗对话小窗吃这两个) ----------
# 与 run_agent 的分别：那一条给人用、印的是人话；这两条 stdout 只有信封 dict(由 __main__ emit)，
# 而且**全程不调 input()** —— 子进程的口上没有键盘，"等玩家确认"这件事在 GUI 里就是一颗按钮，
# 不该在 Python 进程里以"读一行标准输入"的形状存在。
def _machine_paths(root):
    """两条机读口共用的前置：控制台守卫 + settings 在不在。返回 (paths, settings, 错误信封或 None)。"""
    _guard_console()
    paths = EN.Paths(root)
    if not paths.settings.is_file():
        return paths, None, {'ok': False, 'why': '找不到 config\\settings.json —— 先跑 setup.ps1，agent 不猜路径'}
    return paths, MD.read_settings(root), None


def plan_ask(root, ask):
    """第一击：问一句需求，跑完整回路但要真跑的那几步只列计划。返回信封 dict。
    非计划动作(改配置/预览/三张查表)按现行语义当场就办了 —— 回执会一并进 reply 的下游，
    面板把它们原样显示给人看，不藏。"""
    paths, settings, err = _machine_paths(root)
    if err:
        return err
    text = str(ask or '').strip()
    if not text:
        return {'ok': False, 'why': '没给 --ask(这一口一次问一句)'}
    print(greeting(root, paths, settings), file=sys.stderr)
    ctx = build_context(root, settings, paths)
    r = EN.plan_exchange(paths, text, [], ctx['model_fn'], ctx['verb_fn'],
                         known_stages=ctx['known_stages'], spec_parser=ctx['spec_parser'],
                         snapshot_fn=ctx['snapshot_fn'], kb_fn=ctx['kb_fn'],
                         prog_fn=ctx['prog_fn'], farm_fn=ctx['farm_fn'])
    if not r['Ok']:
        return {'ok': False, 'why': r['Why'], 'reply': r['Reply'], 'plan_id': ''}
    return {'ok': True, 'why': r['Why'], 'reply': r['Reply'], 'plan_id': r['Id'],
            'created': r['Created'],
            'items': [{'desc': p['Desc'], 'args': list(p['Args'])} for p in r['Plan']]}


def plan_execute(root, plan_id):
    """第二击：按代号取回那份计划，逐条重闸后真跑。计划里没有的一步都不多做。"""
    paths, settings, err = _machine_paths(root)
    if err:
        return err
    pid = str(plan_id or '').strip()
    if not pid:
        return {'ok': False, 'why': '没给 --plan(第二击要的是第一击回的那个代号)'}
    ld = EN.load_plan(paths, pid)
    if not ld['Ok']:
        return {'ok': False, 'why': ld['Why'], 'plan_id': pid}
    ctx = build_context(root, settings, paths)      # 重闸要 known_stages 与 farmTargets 解析器，仍走同一装配
    r = EN.run_plan(paths, ld['Plan'], ctx['verb_fn'],
                    known_stages=ctx['known_stages'], spec_parser=ctx['spec_parser'])
    return {'ok': r['Ok'], 'why': r['Why'], 'plan_id': pid, 'created': ld['Created'],
            'results': [{'desc': x['Desc'], 'args': list(x['Args']), 'status': x['Status'],
                         'exit': x.get('Exit'), 'detail': x.get('Detail', '')} for x in r['Results']]}
