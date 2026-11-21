"""python -m gdagent <verb> ... —— KB/agent 命令行入口。KB 动词 stdout 只出 JSON 信封，日志走 stderr。

KB 动词：import / search / stats / drop / compact / rebuild / reindex / rollback / probe
agent 动词(M2)：agent = 和需求管家对话(人用终端，输出是人话)；agent-eval = 对拍批处理口
(cases JSONL → 投影结果 JSONL，tools\\probe-agent-golden.ps1 的双跑 Python 侧)。
两击确认(M6 GUI 轮)：agent-plan --ask "…" = 第一击(要真跑的步骤只列计划、落盘、回代号)；
agent-run --plan <代号> = 第二击(按代号取回计划，逐条重新过闸后真跑)。这两条 stdout 只有信封 JSON。
"""
import argparse
import sys

from . import config as CFG
from . import envelope as ENV
from . import engine as EN
from .kb import chunk as CK
from .kb import manager as M
from .kb import store as ST
from .kb import embed as E


def build_parser():
    p = argparse.ArgumentParser(prog="gdagent", description="game-daily 游戏知识库 (M1)")
    sub = p.add_subparsers(dest="verb", required=True)

    s = sub.add_parser("import", help="导入 md 文件或目录")
    s.add_argument("target")
    s.add_argument("--game", choices=list(CK.GAMES))
    s.add_argument("--source", dest="source_prefix")

    s = sub.add_parser("search")
    s.add_argument("query")
    s.add_argument("--game")
    s.add_argument("--top", type=int, default=6)
    s.add_argument("--rerank", choices=["on", "off"], default="on")

    sub.add_parser("stats")

    s = sub.add_parser("drop")
    s.add_argument("--game", required=True)
    s.add_argument("--yes", action="store_true")

    sub.add_parser("compact")
    sub.add_parser("rebuild")
    sub.add_parser("reindex")
    sub.add_parser("rollback")
    sub.add_parser("probe")

    s = sub.add_parser("agent", help="和需求管家对话(人用终端：输出是人话，不是信封 JSON)")
    s.add_argument("--ask", default="", help="单发一句需求，办完就退")
    s.add_argument("--rollback", action="store_true", help="按 journal 逆序恢复(不经过模型)")
    s.add_argument("--rollback-dry", dest="rollback_dry", action="store_true", help="只看攒了几项变更")

    s = sub.add_parser("agent-plan", help="两击确认·第一击：问一句，要真跑的步骤只列计划不执行(stdout 出信封)")
    s.add_argument("--ask", required=True, help="一句需求原文")
    s = sub.add_parser("agent-run", help="两击确认·第二击：按第一击回的代号把那份计划真跑掉(stdout 出信封)")
    s.add_argument("--plan", required=True, help="计划代号(形如 20260929-121530-ab12)")

    s = sub.add_parser("agent-eval", help="对拍批处理口：cases JSONL → 投影结果 JSONL")
    s.add_argument("--cases", required=True)
    s.add_argument("--out", required=True)
    s.add_argument("--fake-farm-spec", dest="fake_farm_spec", action="store_true",
                   help="farmTargets 用假解析器(与 PS 探针同名件逐字一致；真规则归 panel-cs --orchtest)")
    return p


def main(argv=None):
    args = build_parser().parse_args(argv)

    # agent 两动词不走 KB 的 store/信封通道：对话要的是人话，对拍要的是文件进文件出。
    if args.verb == "agent":
        from . import live
        return live.run_agent(CFG.ROOT, ask=args.ask, rollback=args.rollback,
                              rollback_dry=args.rollback_dry)
    # 两击确认的两条机读口：stdout 只有信封 JSON，人话一律 stderr(铁律同 KB 动词)。
    if args.verb == "agent-plan":
        from . import live
        res = live.plan_ask(CFG.ROOT, args.ask)
        ENV.emit(res)
        return 0 if res.get("ok") else 1
    if args.verb == "agent-run":
        from . import live
        res = live.plan_execute(CFG.ROOT, args.plan)
        ENV.emit(res)
        return 0 if res.get("ok") else 1
    if args.verb == "agent-eval":
        sp = EN.fake_spec_parser if args.fake_farm_spec else None
        n = EN.run_eval_cases(args.cases, args.out, sp)
        ENV.emit(ENV.ok(cases=n, out=str(args.out)))
        return 0

    cfg = CFG.load()
    store = ST.Store(CFG.ROOT / "state" / "kb")
    store.sweep_partial()

    if args.verb == "import":
        res = M.import_paths(store, cfg, CFG.ROOT, args.target, game=args.game, source_prefix=args.source_prefix)
    elif args.verb == "search":
        res = M.do_search(store, cfg, args.query, game=args.game, top=args.top, rerank_on=(args.rerank == "on"))
    elif args.verb == "stats":
        res = dict(ok=True, why="", **M.stats(store))
    elif args.verb == "drop":
        res = M.drop_game(store, cfg, args.game, yes=args.yes)
    elif args.verb == "compact":
        res = M.compact(store, cfg)
    elif args.verb == "rebuild":
        res = M.rebuild(store, cfg)
    elif args.verb == "reindex":
        res = M.reindex(store, cfg)
    elif args.verb == "rollback":
        okv, info = store.rollback()
        res = ({"ok": True, "why": "", "rolled_back_to": info} if okv
               else {"ok": False, "why": info})
    elif args.verb == "probe":
        res = _probe(cfg)
    else:
        res = ENV.fail("未知动词：%s" % args.verb)

    ENV.emit(res)
    return 0 if res.get("ok") else 1


def _probe(cfg):
    if not cfg.has_key():
        return ENV.fail("无 embed key(环境变量 KB_API_KEY/DASHSCOPE_API_KEY)，probe 需真实 key；跳过不算失败",
                        skipped=True)
    try:
        vec = E.embed_texts(["知识库连通性探针"], cfg)[0]
        return ENV.ok(model=cfg.embed_model, dim=len(vec), endpoint=cfg.base_url,
                      why="embed 连通，可用此响应钉 golden")
    except E.EmbedError as e:
        return ENV.fail("embed 探测失败：%s" % e)


if __name__ == "__main__":
    sys.exit(main())
