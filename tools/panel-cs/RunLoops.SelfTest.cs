// tools/panel-cs/RunLoops.SelfTest.cs —— --looptest：批次 7/8/9(RunLoops.cs)的无头自检。
// 期望串逐字抄自 lib\maa.ps1 / lib\m9a.ps1 的判据段与回灌段(基线 c498da0 真身) —— 这里红了就是移植走样，
// 改 RunLoops.cs，不改这里的期望(§7 同一条纪律)。
// 批次纪律：不起进程、不读盘、不耗挂钟 —— 读数与 effect 回执全由场景直喂；
// 链步靠"上一步输出的 State 原样中继 + effectsResolved 追加回执"模拟壳的同 tick 迷你循环，
// 与 docs\ps-migration-live-caliber.md §4 的壳骨架一字不差。

using System;
using System.Collections.Generic;
using Dictionary = System.Collections.Generic.Dictionary<string, object>;

namespace GdPanel
{
    static class GdLoopSelfTest
    {
        sealed class Box
        {
            public int Total = 0;
            public int Fails = 0;
            public void Chk(string name, Func<bool> test)
            {
                Total++;
                bool ok = false;
                string detail = null;
                try { ok = test(); }
                catch (Exception ex) { ok = false; detail = ex.GetType().Name + ": " + ex.Message; }
                if (!ok)
                {
                    Fails++;
                    GdLog.Line("FAIL " + name + (detail != null ? "：" + detail : ""), "ERROR");
                }
            }
            public int Result(string tag)
            {
                if (Fails == 0) GdLog.Line(tag + " 全绿：" + Total + "/" + Total + " 项", "OK");
                else GdLog.Line(tag + " 有红项：" + Fails + "/" + Total, "ERROR");
                return Fails == 0 ? 0 : 1;
            }
        }

        static Dictionary<string, object> D(object v) { return v as Dictionary<string, object>; }

        static Dictionary<string, object> Inp(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // ---------- 读数构造器：默认值 = "一切正常、哪档都不该触发"，各用例只覆写自己要的键 ----------
        static Dictionary Now(double el, double q, object ago)
        {
            return Inp("elapsedSec", el, "quietSec", q, "grabAgoSec", ago);
        }

        static Dictionary Live(object[] kv)
        {
            Dictionary<string, object> d = Inp("maaRunning", true, "rescueAvailable", true, "grabAvailable", true);
            Merge(d, kv);
            return d;
        }

        static Dictionary Flags(object[] kv)
        {
            Dictionary<string, object> d = Inp(
                "seenGame", true, "markers", 3, "logChars", 812, "finished", false, "drained", false,
                "stallWarn", "", "completed", new Dictionary<string, object>(), "errors", new object[] { "e1" },
                "expectedChains", new object[] { "Fight", "Shop" }, "asstHint", "", "stuckTask", "",
                "stuckExec", 0, "chanClicks", 0);
            Merge(d, kv);
            return d;
        }

        static Dictionary Cfg(object[] kv)
        {
            Dictionary<string, object> d = Inp(
                "startTimeoutSec", 420, "timeoutSec", 1500, "stallSec", 120, "idleConfirmSec", 40,
                "startTimeoutMin", 7, "timeoutMin", 25, "abortOnMaaStallWarning", true,
                "guiLogPath", @"C:\maa\gui.log", "rescueMaxRaw", 2);
            Merge(d, kv);
            return d;
        }

        static void Merge(Dictionary<string, object> d, object[] kv)
        {
            if (kv == null) return;
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
        }

        static object Tick(Dictionary now, Dictionary live, Dictionary flags, Dictionary cfg, object st, object[] res)
        {
            return RunLoops.MaaTick(Inp("now", now, "live", live, "flags", flags, "cfg", cfg,
                "state", st, "effectsResolved", res ?? new object[0]));
        }

        // ---------- 出参取数 ----------
        static string Act(object r) { return Gd.Str(Gd.Get(D(r), "Action")); }
        static string Stat(object r) { return Gd.Str(Gd.Get(D(r), "Status")); }
        static string Det(object r) { return Gd.Str(Gd.Get(D(r), "Detail")); }
        static object[] Errs(object r) { return Gd.List(Gd.Get(D(r), "Errors")); }
        static Dictionary St(object r) { return D(Gd.Get(D(r), "State")); }
        static string Ph(object r) { return Gd.Str(Gd.Get(St(r), "phase")); }
        static int RL(object r) { return Gd.Int(Gd.Get(St(r), "rescueLeft"), -1); }
        static int NotesN(object r) { return Gd.List(Gd.Get(St(r), "rescueNotes")).Length; }
        static string NoteAt(object r, int i) { return Gd.Str(Gd.List(Gd.Get(St(r), "rescueNotes"))[i]); }
        static string GrabN(object r) { return Gd.Str(Gd.Get(St(r), "grabNote")); }
        static object[] Effs(object r) { return Gd.List(Gd.Get(D(r), "Effects")); }
        static Dictionary E(object r, int i) { return D(Effs(r)[i]); }
        static string EK(object r, int i) { return Gd.Str(Gd.Get(E(r, i), "Kind")); }
        static string ETag(object r, int i) { return Gd.Str(Gd.Get(E(r, i), "Tag")); }
        static string ECtx(object r, int i) { return Gd.Str(Gd.Get(E(r, i), "Context")); }
        static object[] Lgs(object r) { return Gd.List(Gd.Get(D(r), "Logs")); }
        static string LLv(object r, int i) { return Gd.Str(Gd.Get(D(Lgs(r)[i]), "Level")); }
        static string LTx(object r, int i) { return Gd.Str(Gd.Get(D(Lgs(r)[i]), "Text")); }
        static bool Dir(object r, string k) { return Gd.Truthy(Gd.Get(D(Gd.Get(D(r), "Directives")), k)); }

        // ---------- 回执构造器(= 壳执行 effect 后追加进 effectsResolved 的形状) ----------
        static Dictionary GrabRc(string detail) { return Inp("kind", "grab", "exception", false, "detail", detail); }
        static Dictionary GrabX(string msg) { return Inp("kind", "grab", "exception", true, "message", msg); }
        static Dictionary RescRc(bool tried, bool done, string detail)
        {
            return Inp("kind", "rescue", "exception", false, "tried", tried, "done", done, "detail", detail);
        }
        static Dictionary RescX(string msg) { return Inp("kind", "rescue", "exception", true, "message", msg); }
        static Dictionary RecRc() { return Inp("kind", "record"); }

        static readonly string StuckWarnFull =
            "端游没把点击吃进去(StartUp@GameStart 已重复 61 次、整轮 240 次点击)：输入桌面=Default(未锁屏)";
        static readonly string WhyStuck =
            "自动化在同一个子任务[StartUp@GameStart]上重复点了 61 次，一条链路都没完成";

        internal static int LoopTest()
        {
            Box b = new Box();

            // ---------- 首轮初始化与静默续判 ----------
            b.Chk("首 tick 空读数 → loop，无副作用", () =>
            {
                object r = Tick(Now(100, 10, null), Live(null), Flags(null), Cfg(null), null, null);
                return Act(r) == "loop" && Effs(r).Length == 0 && Lgs(r).Length == 0 && Ph(r) == "";
            });
            b.Chk("rescueMaxRaw 0→2 的初始化在 C#(旧代码循环外那句，效果同一)", () =>
            {
                object r = Tick(Now(100, 10, null), Live(null), Flags(null), Cfg(new object[] { "rescueMaxRaw", 0 }), null, null);
                return RL(r) == 2;
            });
            b.Chk("[int] 是银行家舍入：2.5→2 / 3.5→4 / 2.6→3", () =>
            {
                return RL(Tick(Now(100, 10, null), Live(null), Flags(null), Cfg(new object[] { "rescueMaxRaw", 2.5 }), null, null)) == 2
                    && RL(Tick(Now(100, 10, null), Live(null), Flags(null), Cfg(new object[] { "rescueMaxRaw", 3.5 }), null, null)) == 4
                    && RL(Tick(Now(100, 10, null), Live(null), Flags(null), Cfg(new object[] { "rescueMaxRaw", 2.6 }), null, null)) == 3;
            });

            // ---------- 档 1-3：finished / unknown-format / not-started ----------
            b.Chk("finished → tail(优先级第一，哪怕 markers=0 也该先收尾)", () =>
            {
                object r = Tick(Now(500, 10, null), Live(null), Flags(new object[] { "finished", true, "markers", 0 }), Cfg(null), null, null);
                return Act(r) == "tail";
            });
            b.Chk("unknown-format(日志在涨)整句与 Errors 回显", () =>
            {
                object r = Tick(Now(500, 10, null), Live(null), Flags(new object[] { "markers", 0 }), Cfg(null), null, null);
                return Act(r) == "return" && Stat(r) == "unknown-format"
                    && Det(r) == "MAA 日志在涨(新增 812 字符)但一条认识的标志都没有(可能 MAA 改了文案) → 这轮结果不可信，"
                        + "不要按它判定理智是否清完。先看 status.ps1 的 MAA 段，再对着 C:\\maa\\gui.log 更新 lib\\maa.ps1 的判定正则"
                    && Errs(r).Length == 1 && Gd.Str(Errs(r)[0]) == "e1";
            });
            b.Chk("unknown-format(没读到新增内容)走静默线也算", () =>
            {
                object r = Tick(Now(100, 130, null), Live(null), Flags(new object[] { "markers", 0, "logChars", 0 }), Cfg(null), null, null);
                return Stat(r) == "unknown-format"
                    && Det(r).StartsWith("MAA 本轮没有读到任何新增日志内容(可能 MAA 换了日志路径或文件名) →");
            });
            b.Chk("not-started：markers>0 挡住 unknown，整句带分钟数", () =>
            {
                object r = Tick(Now(500, 10, null), Live(null), Flags(new object[] { "seenGame", false }), Cfg(null), null, null);
                return Stat(r) == "not-started"
                    && Det(r) == "7 分钟内端游没被拉起、也没有任何链路完成 → 大概率是权限/自动登录/客户端路径问题(MAA 连端游需要管理员)";
            });
            b.Chk("unknown 排在 not-started 之前：markers=0 + seenGame=false 同时成立 → unknown-format(12 字符)", () =>
            {
                object r = Tick(Now(700, 10, null), Live(null),
                    Flags(new object[] { "markers", 0, "seenGame", false, "logChars", 12 }), Cfg(null), null, null);
                return Act(r) == "return" && Stat(r) == "unknown-format"
                    && Det(r).StartsWith("MAA 日志在涨(新增 12 字符)但一条认识的标志都没有(可能 MAA 改了文案) →");
            });
            b.Chk("seen 过游戏 + markers>0 + 超起始时限 → 不落 unknown/not-started，安静 loop", () =>
            {
                object r = Tick(Now(700, 10, null), Live(null), Flags(new object[] { "markers", 1 }), Cfg(null), null, null);
                return Act(r) == "loop" && Effs(r).Length == 0 && Lgs(r).Length == 0;
            });

            // ---------- 档 4：stuck 抢前台 → 兜底 → 落穿续判 ----------
            Dictionary stuckF = Flags(new object[] { "stuckTask", "StartUp@GameStart", "stuckExec", 61, "chanClicks", 240 });
            b.Chk("stuck 首次 → GrabNow 指令 + grab effect，挂起在 stuck-grab", () =>
            {
                object r = Tick(Now(120, 10, null), Live(null), stuckF, Cfg(null), null, null);
                return Act(r) == "loop" && Dir(r, "GrabNow") && Effs(r).Length == 1 && EK(r, 0) == "grab"
                    && Ph(r) == "stuck-grab" && RL(r) == 2;
            });
            b.Chk("grab 回执回来 → WARN 整句(带抢到的现场) + rescue stuck1，预算 2→1", () =>
            {
                object r1 = Tick(Now(120, 10, null), Live(null), stuckF, Cfg(null), null, null);
                object r2 = Tick(Now(120, 10, null), Live(null), stuckF, Cfg(null), Gd.Get(D(r1), "State"),
                    new object[] { GrabRc("输入桌面=Default(未锁屏)") });
                return Lgs(r2).Length == 1 && LLv(r2, 0) == "WARN" && LTx(r2, 0) == StuckWarnFull
                    && EK(r2, 0) == "rescue" && ETag(r2, 0) == "stuck1" && ECtx(r2, 0) == WhyStuck
                    && RL(r2) == 1 && Ph(r2) == "stuck-rescue";
            });
            b.Chk("抢前台抛异常 → grabNote『抢前台抛异常：…』+ 同拍 WARN 挂 rescue，后续 stalled 整句也带这句", () =>
            {
                Dictionary f = Flags(new object[] { "stuckTask", "X@Y", "stuckExec", 61, "chanClicks", 10 });
                object r1 = Tick(Now(120, 10, null), Live(null), f, Cfg(null), null, null);
                if (EK(r1, 0) != "grab") return false;
                object r2 = Tick(Now(120, 10, null), Live(null), f, Cfg(null),
                    Inp("phase", "stuck-grab", "rescueLeft", 2, "rescueNotes", new object[0], "grabNote", ""),
                    new object[] { GrabX("桌面被锁") });
                if (!(GrabN(r2) == "抢前台抛异常：桌面被锁" && Lgs(r2).Length == 1 && LLv(r2, 0) == "WARN"
                    && LTx(r2, 0) == "端游没把点击吃进去(X@Y 已重复 61 次、整轮 10 次点击)：抢前台抛异常：桌面被锁"
                    && EK(r2, 0) == "rescue" && ETag(r2, 0) == "stuck1" && Ph(r2) == "stuck-rescue" && RL(r2) == 1)) return false;
                object r3 = Tick(Now(120, 10, 61.0), Live(null), f, Cfg(null),
                    Inp("phase", "", "rescueLeft", 1, "rescueNotes", new object[] { "没动" }, "grabNote", "抢前台抛异常：桌面被锁"), null);
                return Stat(r3) == "stalled"
                    && Det(r3).Contains("抢前台时看到的点击环境：抢前台抛异常：桌面被锁。");
            });
            b.Chk("没有抢前台函数时也照样记起点：grabNote 定稿话术 + 同 tick 直接挂 rescue", () =>
            {
                object r = Tick(Now(120, 10, null), Live(new object[] { "grabAvailable", false }), stuckF, Cfg(null), null, null);
                return Dir(r, "GrabNow") && GrabN(r) == "这棵树上没有抢前台的函数，只能等停滞判定"
                    && LTx(r, 0) == "端游没把点击吃进去(StartUp@GameStart 已重复 61 次、整轮 240 次点击)：这棵树上没有抢前台的函数，只能等停滞判定"
                    && EK(r, 0) == "rescue" && Ph(r) == "stuck-rescue";
            });
            b.Chk("rescue 抛异常 → rc 三字段按旧 catch 原样 + record 的 Tag 错位到 stuck2", () =>
            {
                object r = Tick(Now(120, 10, null), Live(null), stuckF, Cfg(null),
                    Inp("phase", "stuck-rescue", "rescueLeft", 1, "rescueNotes", new object[0], "grabNote", "输入桌面=Default(未锁屏)"),
                    new object[] { GrabRc("输入桌面=Default(未锁屏)"), RescX("模型超时") });
                return EK(r, 0) == "record" && ETag(r, 0) == "stuck2"
                    && Gd.Str(Gd.Get(E(r, 0), "Detail")) == "兜底抛异常：模型超时"
                    && !Gd.Truthy(Gd.Get(E(r, 0), "Tried")) && !Gd.Truthy(Gd.Get(E(r, 0), "Done"))
                    && Ph(r) == "stuck-record" && NotesN(r) == 1;
            });
            b.Chk("record 回执回来 → 兜底结果 WARN + 落穿续判(后面没档了 → loop)", () =>
            {
                object s = Inp("phase", "stuck-record", "rescueLeft", 1, "rescueNotes", new object[] { "兜底抛异常：模型超时" }, "grabNote", "输入桌面=Default(未锁屏)");
                object[] res = new object[] { GrabRc("输入桌面=Default(未锁屏)"), RescX("模型超时"), RecRc() };
                object r = Tick(Now(120, 10, null), Live(null), stuckF, Cfg(null), s, res);
                return Act(r) == "loop" && Lgs(r).Length == 1 && LLv(r, 0) == "WARN"
                    && LTx(r, 0) == "视觉兜底结果：兜底抛异常：模型超时" && Ph(r) == "";
            });
            b.Chk("stuck 首轮兜完当 tick 落穿到 drained 档：tag 计数接着往下排(drain2)", () =>
            {
                object s = Inp("phase", "stuck-record", "rescueLeft", 1, "rescueNotes", new object[] { "画面都没动" }, "grabNote", "输入桌面=Default(未锁屏)");
                object[] res = new object[] { GrabRc("输入桌面=Default(未锁屏)"), RescRc(true, false, "画面都没动"), RecRc() };
                object r = Tick(Now(120, 10, null), Live(null),
                    Flags(new object[] { "stuckTask", "StartUp@GameStart", "stuckExec", 61, "chanClicks", 240, "drained", true }),
                    Cfg(null), s, res);
                return EK(r, 0) == "rescue" && ETag(r, 0) == "drain2" && Ph(r) == "drain-rescue" && RL(r) == 0
                    && LTx(r, 0) == "视觉兜底结果：画面都没动" && Lgs(r).Length == 1;
            });
            b.Chk("抢过 61 秒还在原地 → stalled 整句(grabNote 与兜底账都在)", () =>
            {
                object s = Inp("phase", "", "rescueLeft", 1, "rescueNotes", new object[] { "画面都没动" }, "grabNote", "输入桌面=Default(未锁屏)");
                object r = Tick(Now(120, 10, 61.0), Live(null), stuckF, Cfg(null), s, null);
                return Stat(r) == "stalled"
                    && Det(r) == "点击没被端游吃进去：StartUp@GameStart 已重复 61 次(整轮 240 次点击)且一条链路都没完成，"
                        + "抢前台后 60 秒仍在原地。抢前台时看到的点击环境：输入桌面=Default(未锁屏)。"
                        + "完成链路 0/2；视觉兜底试过 1 次，最后一次：画面都没动";
            });
            b.Chk("抢过 59 秒 → 还在观察窗内，本档不拦，后面没档 → loop", () =>
            {
                object s = Inp("phase", "", "rescueLeft", 1, "rescueNotes", new object[0], "grabNote", "x");
                object r = Tick(Now(120, 10, 59.0), Live(null), stuckF, Cfg(null), s, null);
                return Act(r) == "loop";
            });
            b.Chk("抢前台还在 60 秒观察窗内 + 队列已跑空 → 不落 stalled，落穿 drain 兜一次就早退", () =>
            {
                Dictionary f = Flags(new object[] { "stuckTask", "X@Y", "stuckExec", 61, "chanClicks", 10, "drained", true });
                object r1 = Tick(Now(100, 10, 30.0), Live(null), f, Cfg(null), null, null);
                if (!(EK(r1, 0) == "rescue" && ETag(r1, 0) == "drain1" && Ph(r1) == "drain-rescue" && RL(r1) == 1)) return false;
                object r2 = Tick(Now(100, 10, 30.0), Live(null), f, Cfg(null), St(r1),
                    new object[] { RescRc(true, false, "R-default") });
                object r3 = Tick(Now(100, 10, 30.0), Live(null), f, Cfg(null), St(r2),
                    new object[] { RescRc(true, false, "R-default"), RecRc() });
                return Act(r3) == "return" && Stat(r3) == "stalled"
                    && Det(r3) == "MAA 队列已跑空但只完成 0/2 条链路，它不会再自己动 → 不等静默判定，当场兜底过一次就直接早退(外层会重试一次)。完成链路 0/2";
            });

            // ---------- 档 5：drained 当场兜一次 → 无条件早退 ----------
            Dictionary drainedF = Flags(new object[] { "drained", true });
            b.Chk("drained → rescue drain1(Context 说清只完成几条)，预算扣一次", () =>
            {
                object r = Tick(Now(100, 10, null), Live(null), drainedF, Cfg(null), null, null);
                return EK(r, 0) == "rescue" && ETag(r, 0) == "drain1"
                    && ECtx(r, 0) == "MAA 自己说这一轮跑完了但报错，只完成 0/2 条链路，它不会再自己动"
                    && Ph(r) == "drain-rescue" && RL(r) == 1;
            });
            b.Chk("drain 兜完(没兜成也算) → 记完账就 stalled，整句带『外层会重试一次』", () =>
            {
                object s1 = Inp("phase", "drain-rescue", "rescueLeft", 1, "rescueNotes", new object[0], "grabNote", "");
                object r1 = Tick(Now(100, 10, null), Live(null), drainedF, Cfg(null), s1, new object[] { RescRc(true, false, "点了 3 个候选点位，画面都没动") });
                if (EK(r1, 0) != "record" || ETag(r1, 0) != "drain2") return false;
                object s2 = Gd.Get(D(r1), "State");
                object r2 = Tick(Now(100, 10, null), Live(null), drainedF, Cfg(null), s2,
                    new object[] { RescRc(true, false, "点了 3 个候选点位，画面都没动"), RecRc() });
                return Act(r2) == "return" && Stat(r2) == "stalled"
                    && LTx(r2, 0) == "视觉兜底结果：点了 3 个候选点位，画面都没动" && LLv(r2, 0) == "WARN"
                    && Det(r2) == "MAA 队列已跑空但只完成 0/2 条链路，它不会再自己动 → 不等静默判定，当场兜底过一次就直接早退(外层会重试一次)。"
                        + "完成链路 0/2";   // 基线口径：$detail 是 tick 开头快照，本 tick 刚落的这条兜底不进结论句(旧 live 门 drain-first 场景钉)
            });
            b.Chk("drained 但预算没了 → 不兜了也照样早退(detail 用历史账)", () =>
            {
                object s = Inp("phase", "", "rescueLeft", 0, "rescueNotes", new object[] { "上一次的结果" }, "grabNote", "");
                object r = Tick(Now(100, 10, null), Live(null), drainedF, Cfg(null), s, null);
                return Act(r) == "return" && Effs(r).Length == 0 && Stat(r) == "stalled"
                    && Det(r).Contains("当场兜底过一次就直接早退(外层会重试一次)。完成链路 0/2；视觉兜底试过 1 次，最后一次：上一次的结果");
            });
            b.Chk("drained 但链路已跑齐(allDone) → 这一档整个跳过，安静续等到确认线", () =>
            {
                Dictionary f = Flags(new object[] { "drained", true, "completed", Inp("Fight", 1, "Shop", 1) });
                object r = Tick(Now(100, 10, null), Live(null), f, Cfg(null), null, null);
                return Act(r) == "loop" && Effs(r).Length == 0;   // 静默还没过 idleConfirm，什么都不该发生
            });

            // ---------- 档 6-7：stallWarn 中止 / timeout ----------
            b.Chk("stallWarn+中止开关 → stalled，告警原文排在 Errors 最前", () =>
            {
                object r = Tick(Now(100, 10, null), Live(null), Flags(new object[] { "stallWarn", "MAA 自己报了停滞" }), Cfg(null), null, null);
                return Stat(r) == "stalled" && Det(r) == "MAA 自身停滞告警(已触发)。完成链路 0/2"
                    && Errs(r).Length == 2 && Gd.Str(Errs(r)[0]) == "MAA 自己报了停滞" && Gd.Str(Errs(r)[1]) == "e1";
            });
            b.Chk("stallWarn 但不中止 → 继续等各档", () =>
            {
                object r = Tick(Now(100, 10, null), Live(null), Flags(new object[] { "stallWarn", "w" }),
                    Cfg(new object[] { "abortOnMaaStallWarning", false }), null, null);
                return Act(r) == "loop";
            });
            b.Chk("timeout 整句用分钟原值", () =>
            {
                object r = Tick(Now(1600, 10, null), Live(null), Flags(null), Cfg(null), null, null);
                return Stat(r) == "timeout" && Det(r) == "超过 25 分钟未完成。完成链路 0/2";
            });

            // ---------- 档 8：静默 → 先兜一次，兜成就续命 ----------
            Dictionary quietF = Flags(null);
            b.Chk("静默超限有预算 → rescue stall1(tag 前缀就叫 stall，照抄)", () =>
            {
                object r = Tick(Now(100, 130, null), Live(null), quietF, Cfg(null), null, null);
                return EK(r, 0) == "rescue" && ETag(r, 0) == "stall1" && ECtx(r, 0) == "" && Ph(r) == "quiet-rescue";
            });
            b.Chk("兜成 → OK 日志 + 重置计时/清告警指令，本 tick 到为止(loop)", () =>
            {
                object s1 = Inp("phase", "quiet-rescue", "rescueLeft", 1, "rescueNotes", new object[0], "grabNote", "");
                object r1 = Tick(Now(100, 130, null), Live(null), quietF, Cfg(null), s1, new object[] { RescRc(true, true, "已按模型判断关掉活动弹窗") });
                if (ETag(r1, 0) != "stall2") return false;
                object r2 = Tick(Now(100, 130, null), Live(null), quietF, Cfg(null), Gd.Get(D(r1), "State"),
                    new object[] { RescRc(true, true, "已按模型判断关掉活动弹窗"), RecRc() });
                return Act(r2) == "loop" && LLv(r2, 0) == "OK"
                    && LTx(r2, 0) == "视觉兜底结果：已按模型判断关掉活动弹窗"
                    && Dir(r2, "ResetLastGrowth") && Dir(r2, "ClearStallWarn") && !Dir(r2, "GrabNow");
            });
            b.Chk("没兜成 → stalled，分钟数走 [int] 银行家舍入(150→2 / 210→4)", () =>
            {
                object r150 = Tick(Now(100, 150, null), Live(new object[] { "rescueAvailable", false }), quietF, Cfg(null), null, null);
                object r210 = Tick(Now(100, 210, null), Live(new object[] { "rescueAvailable", false }), quietF, Cfg(null), null, null);
                return Stat(r150) == "stalled" && Det(r150).StartsWith("MAA 日志 2 分钟无输出，判定卡住。")
                    && Det(r210) == "MAA 日志 4 分钟无输出，判定卡住。完成链路 0/2";
            });
            b.Chk("兜底『截图失败』跳票形态(Tried=false) → 与兜败同分支：record 带 false，WARN 原句，Detail 走快照", () =>
            {
                object r1 = Tick(Now(100, 150, null), Live(null), quietF, Cfg(null), null, null);
                if (ETag(r1, 0) != "stall1") return false;
                object r2 = Tick(Now(100, 150, null), Live(null), quietF, Cfg(null), St(r1),
                    new object[] { RescRc(false, false, "截图失败：PrintWindow 返回 0 字节") });
                if (!(EK(r2, 0) == "record" && ETag(r2, 0) == "stall2"
                    && !Gd.Truthy(Gd.Get(E(r2, 0), "Tried")) && !Gd.Truthy(Gd.Get(E(r2, 0), "Done"))
                    && Gd.Str(Gd.Get(E(r2, 0), "Detail")) == "截图失败：PrintWindow 返回 0 字节")) return false;
                object r3 = Tick(Now(100, 150, null), Live(null), quietF, Cfg(null), St(r2),
                    new object[] { RescRc(false, false, "截图失败：PrintWindow 返回 0 字节"), RecRc() });
                return Act(r3) == "return" && Stat(r3) == "stalled" && LLv(r3, 0) == "WARN"
                    && LTx(r3, 0) == "视觉兜底结果：截图失败：PrintWindow 返回 0 字节"
                    && Det(r3) == "MAA 日志 2 分钟无输出，判定卡住。完成链路 0/2";
            });

            // ---------- 档 9-10：进程没了 / 跑齐静默 ----------
            b.Chk("MAA 进程提前退出且没跑齐 → error", () =>
            {
                object r = Tick(Now(100, 10, null), Live(new object[] { "maaRunning", false }), Flags(null), Cfg(null), null, null);
                return Stat(r) == "error" && Det(r) == "MAA 进程提前退出。完成链路 0/2";
            });
            b.Chk("进程没了但已宣布完成/跑齐 → tail", () =>
            {
                object r1 = Tick(Now(100, 10, null), Live(new object[] { "maaRunning", false }),
                    Flags(new object[] { "finished", true }), Cfg(null), null, null);
                object r2 = Tick(Now(100, 10, null), Live(new object[] { "maaRunning", false }),
                    Flags(new object[] { "completed", Inp("Fight", 1, "Shop", 1) }), Cfg(null), null, null);
                return Act(r1) == "tail" && Act(r2) == "tail";
            });
            b.Chk("跑齐 + 静默过确认线 → tail；差一点 → loop", () =>
            {
                Dictionary f = Flags(new object[] { "completed", Inp("Fight", 1, "Shop", 1) });
                return Act(Tick(Now(100, 41, null), Live(null), f, Cfg(null), null, null)) == "tail"
                    && Act(Tick(Now(100, 39, null), Live(null), f, Cfg(null), null, null)) == "loop";
            });

            // ---------- $doneCount/$allDone/$detail 的 PS 等值 ----------
            b.Chk("同一条链路完成 2 次只按 1 计([Math]::Min) → allDone 仍成立", () =>
            {
                Dictionary f = Flags(new object[] { "completed", Inp("Fight", 2, "Shop", 1) });
                object r = Tick(Now(100, 10, null), Live(new object[] { "maaRunning", false }), f, Cfg(null), null, null);
                return Act(r) == "tail" && Det(r) == "";   // tail 无 Detail
            });
            b.Chk("hashtable 大小写不敏感：fIGHT/SHOP 也算跑齐", () =>
            {
                Dictionary f = Flags(new object[] { "completed", Inp("fIGHT", 1, "sHOP", 1) });
                object r = Tick(Now(100, 10, null), Live(new object[] { "maaRunning", false }), f, Cfg(null), null, null);
                return Act(r) == "tail";
            });
            b.Chk("detail 四段拼串：完成 X/Y：链表明(排序+逗号)；asst 线索；兜底账", () =>
            {
                Dictionary f = Flags(new object[] {
                    "completed", Inp("Shop", 1, "Recruit", 1),
                    "expectedChains", new object[] { "Fight", "Shop", "Recruit" },
                    "asstHint", "子任务 StartUp 重试 50/50", "stuckExec", 3 });
                object s = Inp("phase", "", "rescueLeft", 1, "rescueNotes", new object[] { "第1次\n第2次(换行原样留在账里)" }, "grabNote", "");
                object r = Tick(Now(100, 10, null), Live(new object[] { "maaRunning", false }), f, Cfg(null), s, null);
                return Stat(r) == "error"
                    && Det(r) == "MAA 进程提前退出。完成链路 2/3：Recruit, Shop；子任务 StartUp 重试 50/50；视觉兜底试过 1 次，最后一次：第1次\n第2次(换行原样留在账里)";
            });
            b.Chk("expectedChains 重复按大小写不敏感去重($expectedCount 的 hashtable 口径)", () =>
            {
                Dictionary f = Flags(new object[] { "expectedChains", new object[] { "Fight", "fight", "Shop" } });
                object r = Tick(Now(100, 10, null), Live(null), f, Cfg(null), null, null);
                return Det(r).EndsWith("(MAA 连端游需要管理员)") == false
                    && Act(Tick(Now(100, 10, null), Live(new object[] { "maaRunning", false }),
                        Flags(new object[] { "expectedChains", new object[] { "Fight", "fight", "Shop" }, "completed", Inp("Fight", 1, "Shop", 1) }),
                        Cfg(null), null, null)) == "tail";   // distinct=2 → 跑齐
            });
            b.Chk("errors 空也照样回显成空数组(信封键永远在)", () =>
            {
                object r = Tick(Now(1600, 10, null), Live(null), Flags(new object[] { "errors", new object[0] }), Cfg(null), null, null);
                return Stat(r) == "timeout" && Errs(r).Length == 0;
            });

            // ---------- 全链彩排：stuck 首次四步 + 落穿 quiet 三步(同 tick 七次重入) ----------
            b.Chk("一个 tick 里连跑两条兜底链：stuck1/stuck2 → drain2/drain3(计数一路接排)", () =>
            {
                Dictionary f = Flags(new object[] { "stuckTask", "StartUp@GameStart", "stuckExec", 61, "chanClicks", 240, "drained", true });
                Dictionary now = Now(120, 10, null);
                object r = Tick(now, Live(null), f, Cfg(null), null, null);
                object[] res = new object[0];
                // stuck: grab → rescue → record → (落穿)drain rescue
                res = Add(res, GrabRc("现场正常")); r = Tick(now, Live(null), f, Cfg(null), Gd.Get(D(r), "State"), res);
                if (ETag(r, 0) != "stuck1") return false;
                res = Add(res, RescRc(true, false, "弹窗没关掉")); r = Tick(now, Live(null), f, Cfg(null), Gd.Get(D(r), "State"), res);
                if (ETag(r, 0) != "stuck2") return false;
                res = Add(res, RecRc()); r = Tick(now, Live(null), f, Cfg(null), Gd.Get(D(r), "State"), res);
                if (ETag(r, 0) != "drain2" || Lgs(r).Length != 1) return false;   // 落穿：接着排 drain
                res = Add(res, RescRc(true, true, "活动页被点掉了")); r = Tick(now, Live(null), f, Cfg(null), Gd.Get(D(r), "State"), res);
                if (ETag(r, 0) != "drain3") return false;
                res = Add(res, RecRc()); r = Tick(now, Live(null), f, Cfg(null), Gd.Get(D(r), "State"), res);
                // drain 档兜成也照样早退(旧代码无条件)：LLv=OK。Detail 是 tick 开头快照 ——
                // 基线 305 定稿 $detail 串后 386 才 +=，本 tick 连落的两笔都不进当 tick 结论句(账在 Notes，下一 tick 才见)。
                return Act(r) == "return" && Stat(r) == "stalled" && LLv(r, 0) == "OK"
                    && Det(r) == "MAA 队列已跑空但只完成 0/2 条链路，它不会再自己动 → 不等静默判定，当场兜底过一次就直接早退(外层会重试一次)。完成链路 0/2"
                    && NotesN(r) == 2 && RL(r) == 0 && Ph(r) == "";
            });

            // ==================== 批次 8：decide-m9a-run-tick(m9a 一路反应段) ====================
            // 期望串逐字抄自 lib\m9a.ps1 Invoke-M9ARun 行 350-388 + $askScreen 闭包 325-337(基线 c498da0)。

            // ---------- 首拍档位 ----------
            b.Chk("m9a ok → OK 结论句(Round 同 PS ToEven：675 秒→11.2) + Duration + 直接 return", () =>
            {
                object r = MTick(675, true, true, "ok", "链路跑完", 8, null, null);
                return Act(r) == "return" && Stat(r) == "ok" && Det(r) == "链路跑完"
                    && Lgs(r).Length == 1 && LLv(r, 0) == "OK"
                    && LTx(r, 0) == "M9A 链路完成，共 8 个任务，用时 11.2 分钟"
                    && Math.Abs(Dur(r) - 675.0) < 1e-9 && Effs(r).Length == 0;
            });
            b.Chk("m9a ok 整分钟打整数形(660→『11 分钟』，PS double→串口径)", () =>
            {
                object r = MTick(660, true, true, "ok", "", 0, null, null);
                return LTx(r, 0) == "M9A 链路完成，共 0 个任务，用时 11 分钟";
            });
            b.Chk("m9a empty-queue → ERROR 日志是原句(不拼进程后缀)、Detail 也不拼", () =>
            {
                object r = MTick(30, true, false, "empty-queue", "队列一个任务都没有", 0, null, null);
                return Act(r) == "return" && Stat(r) == "empty-queue" && Det(r) == "队列一个任务都没有"
                    && LLv(r, 0) == "ERROR" && LTx(r, 0) == "队列一个任务都没有" && Effs(r).Length == 0;
            });
            b.Chk("m9a 非 ok 非 empty-queue → 进程后缀先拼再问画面(Context 里就能看到)", () =>
            {
                object r = MTick(30, true, true, "stalled", "M9A 十分钟没动静", 0, null, null);
                return EK(r, 0) == "ask-screen" && ETag(r, 0) == "m9a1" && EWhy(r, 0) == "停滞"
                    && ECtx(r, 0) == "M9A 十分钟没动静(游戏进程出现过)" && Ph(r) == "ask-rescue" && RL(r) == 1;
            });
            b.Chk("m9a unknown-format 不在问画面集合 → 拼后缀直接 return(状态名一律不改)", () =>
            {
                object r = MTick(30, false, true, "unknown-format", "标志一条没认出来", 0, null, null);
                return Act(r) == "return" && Stat(r) == "unknown-format"
                    && Det(r) == "标志一条没认出来(游戏进程从未出现)" && Effs(r).Length == 0;
            });
            b.Chk("m9a error 也不在问画面集合 → 拼后缀 return、预算不动(集合只认四档)", () =>
            {
                object r = MTick(30, true, true, "error", "进程提前退出", 0, null, null);
                return Act(r) == "return" && Stat(r) == "error" && Det(r) == "进程提前退出(游戏进程出现过)"
                    && Effs(r).Length == 0 && RL(r) == 2;
            });
            b.Chk("m9a empty-queue 在问画面集合里但**不拼后缀**：Why=判因、Context=原句、Tag m9a1", () =>
            {
                object r1 = MTick(30, true, true, "empty-queue", "空跑但值得问", 0, null, null);
                if (!(EK(r1, 0) == "ask-screen" && ETag(r1, 0) == "m9a1" && EWhy(r1, 0) == "判因"
                    && ECtx(r1, 0) == "空跑但值得问" && LLv(r1, 0) == "ERROR" && LTx(r1, 0) == "空跑但值得问")) return false;
                object r2 = MTickR(30, true, "empty-queue", "空跑但值得问", 0, St(r1), new object[] { AskRc(true, false, "画面上是桌面") });
                object r3 = MTickR(30, true, "empty-queue", "空跑但值得问", 0, St(r2),
                    new object[] { AskRc(true, false, "画面上是桌面"), RecRc() });
                return Stat(r3) == "empty-queue" && !Resc(r3) && LLv(r3, 0) == "WARN"
                    && LTx(r3, 0) == "视觉兜底(判因)：画面上是桌面"
                    && Det(r3) == "空跑但值得问；视觉兜底 1 次，最后一次：[判因] 画面上是桌面";
            });
            b.Chk("m9a 预算初始化 0→2、[int] 银行家舍入 2.5→2", () =>
            {
                return RL(MTickRescueMax(0)) == 2 && RL(MTickRescueMax(2.5)) == 2 && RL(MTickRescueMax(3.5)) == 4;
            });
            b.Chk("m9a 没预算/树上没函数 → 不问画面，兜底账为空也照样 return", () =>
            {
                object r1 = MTick(30, true, true, "blocked", "任务全失败", 0,
                    Inp("phase", "", "rescueLeft", 0, "rescueNotes", new object[0], "askDetail", ""), null);
                object r2 = MTick(30, true, false, "blocked", "任务全失败", 0, null, null);
                return Act(r1) == "return" && Effs(r1).Length == 0 && Det(r1) == "任务全失败(游戏进程出现过)"
                    && Act(r2) == "return" && Effs(r2).Length == 0;
            });

            // ---------- 三拍链：ask → record → 日志+去留 ----------
            b.Chk("m9a stalled 兜成：record Tag 与调用 Tag 同数(m9a1，没有 ark 的错位) → 本拍无日志", () =>
            {
                object r1 = MTick(30, true, true, "stalled", "十分钟没动静", 0, null, null);
                object r2 = MTickR(30, true, "stalled", "十分钟没动静", 0, St(r1), new object[] { AskRc(true, true, "更新弹窗被点掉了") });
                return EK(r2, 0) == "record" && ETag(r2, 0) == "m9a1"
                    && NoteAt(r2, 0) == "[停滞] 更新弹窗被点掉了" && Lgs(r2).Length == 0 && Ph(r2) == "ask-record";
            });
            b.Chk("m9a 记账回执回来 → 『视觉兜底(停滞)：…』OK 日志 + 撤回本轮结论(ResetLastLog+继续等待入账)", () =>
            {
                object r1 = MTick(30, true, true, "stalled", "十分钟没动静", 0, null, null);
                object r2 = MTickR(30, true, "stalled", "十分钟没动静", 0, St(r1), new object[] { AskRc(true, true, "更新弹窗被点掉了") });
                object r3 = MTickR(30, true, "stalled", "十分钟没动静", 0, St(r2),
                    new object[] { AskRc(true, true, "更新弹窗被点掉了"), RecRc() });
                return Act(r3) == "loop" && LLv(r3, 0) == "OK" && LTx(r3, 0) == "视觉兜底(停滞)：更新弹窗被点掉了"
                    && Dir(r3, "ResetLastLog") && NoteAt(r3, 1) == "[停滞已解除，继续等待]" && Ph(r3) == "" && RL(r3) == 1;
            });
            b.Chk("m9a blocked 兜成 → Rescued=true、状态名不改、Detail 带进程后缀+兜底账(给 r1999-daily 分流看)", () =>
            {
                object r1 = MTick(30, true, true, "blocked", "第一个任务就失败", 0, null, null);
                if (EWhy(r1, 0) != "判因") return false;
                object r2 = MTickR(30, true, "blocked", "第一个任务就失败", 0, St(r1), new object[] { AskRc(true, true, "资源更新界面被点掉了") });
                object r3 = MTickR(30, true, "blocked", "第一个任务就失败", 0, St(r2),
                    new object[] { AskRc(true, true, "资源更新界面被点掉了"), RecRc() });
                return Act(r3) == "return" && Stat(r3) == "blocked" && Resc(r3) && !Dir(r3, "ResetLastLog")
                    && LTx(r3, 0) == "视觉兜底(判因)：资源更新界面被点掉了" && LLv(r3, 0) == "OK"
                    && Det(r3) == "第一个任务就失败(游戏进程出现过)；视觉兜底 1 次，最后一次：[判因] 资源更新界面被点掉了";
            });
            b.Chk("m9a stalled 没兜成 → WARN + return(Rescued=false)，WARN 句与 ark 的『视觉兜底结果：』不是一套词", () =>
            {
                object r1 = MTick(30, false, true, "timeout", "跑过头了", 0, null, null);
                object r2 = MTickR(30, false, "timeout", "跑过头了", 0, St(r1), new object[] { AskRc(true, false, "画面没动") });
                object r3 = MTickR(30, false, "timeout", "跑过头了", 0, St(r2),
                    new object[] { AskRc(true, false, "画面没动"), RecRc() });
                return Act(r3) == "return" && Stat(r3) == "timeout" && !Resc(r3)
                    && LLv(r3, 0) == "WARN" && LTx(r3, 0) == "视觉兜底(停滞)：画面没动"
                    && Det(r3) == "跑过头了(游戏进程从未出现)；视觉兜底 1 次，最后一次：[停滞] 画面没动";
            });
            b.Chk("m9a 兜底抛异常 → rc 三字段按闭包 catch 合成 + 账目 [判因] 兜底抛异常：…", () =>
            {
                object r1 = MTick(30, true, true, "not-started", "一直没进游戏", 0, null, null);
                object r2 = MTickR(30, true, "not-started", "一直没进游戏", 0, St(r1), new object[] { AskX("视觉接口 500") });
                return EK(r2, 0) == "record" && !Gd.Truthy(Gd.Get(E(r2, 0), "Tried")) && !Gd.Truthy(Gd.Get(E(r2, 0), "Done"))
                    && Gd.Str(Gd.Get(E(r2, 0), "Detail")) == "兜底抛异常：视觉接口 500"
                    && NoteAt(r2, 0) == "[判因] 兜底抛异常：视觉接口 500";
            });
            b.Chk("m9a 续判拍 res.detail 回原串也不丢进程后缀(askDetail 随 state 中继，tick3 冒烟红回归钉)", () =>
            {
                object r1 = MTick(30, false, true, "blocked", "任务失败", 0, null, null);
                object r2 = MTickR(30, false, "blocked", "任务失败", 0, St(r1), new object[] { AskRc(true, false, "没东西可点") });
                object r3 = MTickR(30, false, "blocked", "任务失败", 0, St(r2),
                    new object[] { AskRc(true, false, "没东西可点"), RecRc() });
                return Det(r3) == "任务失败(游戏进程从未出现)；视觉兜底 1 次，最后一次：[判因] 没东西可点";
            });
            b.Chk("m9a 手写 state 没 askDetail 键 → 退回当拍 res.detail(真壳永远带，防御口径)", () =>
            {
                object s = Inp("phase", "ask-rescue", "rescueLeft", 1, "rescueNotes", new object[0]);
                object r = MTickR(30, true, "blocked", "原串", 0, s, new object[] { AskRc(true, false, "没动") });
                return ETag(r, 0) == "m9a1" && NoteAt(r, 0) == "[判因] 没动";
            });
            b.Chk("m9a 新一拍 phase 归零：旧 askDetail 不许串门，用当拍 res.detail + 历史账照样拼", () =>
            {
                object s = Inp("phase", "", "rescueLeft", 1,
                    "rescueNotes", new object[] { "[停滞] 上一轮的东西" }, "askDetail", "旧串(游戏进程出现过)");
                object r = MTickR2(30, false, false, "empty-queue", "又空了", 0, s, null, 2);   // 树上没函数 → 不问直接收
                return Det(r) == "又空了；视觉兜底 1 次，最后一次：[停滞] 上一轮的东西"
                    && LTx(r, 0) == "又空了"   // ERROR 原句(空队列分支在拼账之前)
                    && Det(r).Contains("旧串") == false;   // 旧 askDetail 不许串门
            });
            b.Chk("m9a ok 带历史兜底账 → 结论句不改、Detail 拼账(上一轮兜过这一轮成)", () =>
            {
                object s = Inp("phase", "", "rescueLeft", 0,
                    "rescueNotes", new object[] { "[停滞] 点掉了弹窗", "[停滞已解除，继续等待]" }, "askDetail", "");
                object r = MTickR(725, true, "ok", "链路跑完", 8, s, null);
                return Act(r) == "return" && Det(r) == "链路跑完；视觉兜底 2 次，最后一次：[停滞已解除，继续等待]"
                    && LTx(r, 0) == "M9A 链路完成，共 8 个任务，用时 12.1 分钟";
            });
            b.Chk("m9a 兜成续等后第二拍再问：Tag 排到 m9a3(两笔历史占号)、账 3 条拼进 Detail", () =>
            {
                object r1 = MTick(30, true, true, "stalled", "停在公招页", 0, null, null);
                object r2 = MTickR(30, true, "stalled", "停在公招页", 0, St(r1), new object[] { AskRc(true, true, "活动页点掉了") });
                object r3 = MTickR(30, true, "stalled", "停在公招页", 0, St(r2),
                    new object[] { AskRc(true, true, "活动页点掉了"), RecRc() });
                object r4 = MTickR(30, true, "stalled", "又停在同一页", 0, St(r3), null);
                if (!(EK(r4, 0) == "ask-screen" && ETag(r4, 0) == "m9a3" && EWhy(r4, 0) == "停滞"
                    && ECtx(r4, 0) == "又停在同一页(游戏进程出现过)" && RL(r4) == 0 && NotesN(r4) == 2)) return false;
                object r5 = MTickR(30, true, "stalled", "又停在同一页", 0, St(r4), new object[] { AskRc(true, false, "第二救没成") });
                object r6 = MTickR(30, true, "stalled", "又停在同一页", 0, St(r5),
                    new object[] { AskRc(true, false, "第二救没成"), RecRc() });
                return Act(r6) == "return" && Stat(r6) == "stalled" && !Resc(r6)
                    && Det(r6) == "又停在同一页(游戏进程出现过)；视觉兜底 3 次，最后一次：[停滞] 第二救没成";
            });
            b.Chk("m9a 预算 1 兜败耗尽 → 下一拍不问、只拼旧账(状态名与 Detail 后缀照旧)", () =>
            {
                object r1 = MTickR2(30, true, true, "stalled", "第一拍", 0, null, null, 1);
                object r2 = MTickR2(30, true, true, "stalled", "第一拍", 0, St(r1), new object[] { AskRc(true, false, "没兜动") }, 1);
                object r3 = MTickR2(30, true, true, "stalled", "第一拍", 0, St(r2),
                    new object[] { AskRc(true, false, "没兜动"), RecRc() }, 1);
                object r4 = MTickR2(30, true, true, "stalled", "第二拍", 0, St(r3), null, 1);
                return RL(r3) == 0 && Act(r3) == "return"
                    && Det(r3) == "第一拍(游戏进程出现过)；视觉兜底 1 次，最后一次：[停滞] 没兜动"
                    && Act(r4) == "return" && Effs(r4).Length == 0 && RL(r4) == 0
                    && Det(r4) == "第二拍(游戏进程出现过)；视觉兜底 1 次，最后一次：[停滞] 没兜动";
            });
            b.Chk("m9a 兜底『截图失败』跳票形态(Tried=false) → 与兜败同分支、记账原样带 false", () =>
            {
                object r1 = MTick(30, true, true, "stalled", "停在公招页", 0, null, null);
                object r2 = MTickR(30, true, "stalled", "停在公招页", 0, St(r1),
                    new object[] { AskRc(false, false, "截图失败：PrintWindow 返回 0 字节") });
                if (!(EK(r2, 0) == "record" && !Gd.Truthy(Gd.Get(E(r2, 0), "Tried"))
                    && !Gd.Truthy(Gd.Get(E(r2, 0), "Done")))) return false;
                object r3 = MTickR(30, true, "stalled", "停在公招页", 0, St(r2),
                    new object[] { AskRc(false, false, "截图失败：PrintWindow 返回 0 字节"), RecRc() });
                return Stat(r3) == "stalled" && !Resc(r3) && LLv(r3, 0) == "WARN"
                    && LTx(r3, 0) == "视觉兜底(停滞)：截图失败：PrintWindow 返回 0 字节"
                    && Det(r3) == "停在公招页(游戏进程出现过)；视觉兜底 1 次，最后一次：[停滞] 截图失败：PrintWindow 返回 0 字节";
            });

            // ==================== 批次 9：apply-maa-gui-events(ark 一路回灌段) ====================
            // 期望串逐字抄自 lib\maa.ps1 Invoke-MaaRun 行 236-278 的回灌 switch(基线 c498da0)。
            b.Chk("六档一批全走：日志序/级别/State 六键逐项对", () =>
            {
                object r = MGE(new object[] {
                    GEv(new object[] { "Kind", "finished", "Used", "00:07:32" }),
                    GEv(new object[] { "Kind", "chain", "Chain", "Fight" }),
                    GEv(new object[] { "Kind", "stall", "Msg", "任务处理似乎出现了停滞" }),
                    GEv(new object[] { "Kind", "drained" }),
                    GEv(new object[] { "Kind", "error", "Msg", "ERR something" }),
                    GEv(new object[] { "Kind", "benign", "Pattern", "Idle", "Msg", "idle err" }),
                }, GEState(new object[] { "errors", new object[] { "e1" } }));
                if (Lgs(r).Length != 6) return false;
                return LLv(r, 0) == "OK" && LTx(r, 0) == "MAA 宣布任务全部完成(内部计时 00:07:32)"
                    && LLv(r, 1) == "INFO" && LTx(r, 1) == "MAA 完成链路：Fight"
                    && LLv(r, 2) == "WARN" && LTx(r, 2) == "MAA 停滞告警：任务处理似乎出现了停滞"
                    && LLv(r, 3) == "WARN" && LTx(r, 3) == "MAA 宣布本轮已跑完但有错误(队列已空，它不会再自己动)"
                    && LLv(r, 4) == "WARN" && LTx(r, 4) == "MAA 异常线索：ERR something"
                    && LLv(r, 5) == "INFO" && LTx(r, 5) == "MAA 噪音(已按 benignMaaErrors 忽略)：idle err"
                    && Gd.Truthy(Gd.Get(GState(r), "finished")) && Gd.Truthy(Gd.Get(GState(r), "drained"))
                    && Gd.Str(Gd.Get(GState(r), "stallWarn")) == "任务处理似乎出现了停滞"
                    && Gd.List(Gd.Get(GState(r), "errors")).Length == 2
                    && Gd.Int(Gd.Get(D(Gd.Get(GState(r), "completed")), "Fight"), 0) == 1
                    && Gd.Str(Gd.List(Gd.Get(GState(r), "benignPatterns"))[0]) == "Idle";
            });
            b.Chk("finished 无内部计时 → 不拼括号；Used 有值才拼", () =>
            {
                object r = MGE(new object[] { GEv(new object[] { "Kind", "finished" }) }, GEState(null));
                return LTx(r, 0) == "MAA 宣布任务全部完成" && Gd.Truthy(Gd.Get(GState(r), "finished"));
            });
            b.Chk("chain 大小写不敏感累计：fIGHT 加到 Fight 头上，键保首写法、日志用当拍写法", () =>
            {
                object r = MGE(new object[] { GEv(new object[] { "Kind", "chain", "Chain", "fIGHT" }) },
                    GEState(new object[] { "completed", Inp("Fight", 2) }));
                Dictionary cm = D(Gd.Get(GState(r), "completed"));
                return LTx(r, 0) == "MAA 完成链路：fIGHT" && cm != null && cm.Count == 1
                    && Gd.Int(Gd.Get(cm, "Fight"), 0) == 3;
            });
            b.Chk("error 满 40 封顶：39 条时收进来，40 条时**无声**丢弃(不打日志)", () =>
            {
                object[] e39 = MakeErrors(39);
                object[] e40 = MakeErrors(40);
                object rA = MGE(new object[] { GEv(new object[] { "Kind", "error", "Msg", "m" }) }, GEState(new object[] { "errors", e39 }));
                object rB = MGE(new object[] { GEv(new object[] { "Kind", "error", "Msg", "m" }) }, GEState(new object[] { "errors", e40 }));
                return Gd.List(Gd.Get(GState(rA), "errors")).Length == 40 && Lgs(rA).Length == 1
                    && Gd.List(Gd.Get(GState(rB), "errors")).Length == 40 && Lgs(rB).Length == 0;
            });
            b.Chk("benign 同 Pattern 第二条不再打日志(HashSet 首见口径)", () =>
            {
                object r = MGE(new object[] {
                    GEv(new object[] { "Kind", "benign", "Pattern", "Idle", "Msg", "a" }),
                    GEv(new object[] { "Kind", "benign", "Pattern", "iDLE", "Msg", "b" }),
                }, GEState(null));
                return Lgs(r).Length == 1 && LTx(r, 0) == "MAA 噪音(已按 benignMaaErrors 忽略)：a"
                    && Gd.List(Gd.Get(GState(r), "benignPatterns")).Length == 1;
            });
            b.Chk("stall 逐条覆盖(后一条换掉前一条)、drained 幂等但每条都打日志(跨批状态中继)", () =>
            {
                object r1 = MGE(new object[] {
                    GEv(new object[] { "Kind", "drained" }), GEv(new object[] { "Kind", "drained" }),
                }, GEState(null));
                object r2 = MGE(new object[] { GEv(new object[] { "Kind", "stall", "Msg", "告警A" }) }, GState(r1));
                object r3 = MGE(new object[] { GEv(new object[] { "Kind", "stall", "Msg", "告警B" }) }, GState(r2));
                return Lgs(r1).Length == 2 && Gd.Truthy(Gd.Get(GState(r1), "drained"))
                    && LLv(r1, 0) == "WARN" && LTx(r1, 0) == "MAA 宣布本轮已跑完但有错误(队列已空，它不会再自己动)"
                    && Gd.Str(Gd.Get(GState(r2), "stallWarn")) == "告警A"
                    && LTx(r3, 0) == "MAA 停滞告警：告警B" && Gd.Str(Gd.Get(GState(r3), "stallWarn")) == "告警B";
            });
            b.Chk("空事件批 → 状态原样带回、零日志", () =>
            {
                object r = MGE(new object[0], GEState(new object[] { "finished", true, "stallWarn", "w" }));
                return Lgs(r).Length == 0 && Gd.Truthy(Gd.Get(GState(r), "finished"))
                    && Gd.Str(Gd.Get(GState(r), "stallWarn")) == "w";
            });

            // ==================== 批次 9：finish-maa-run(ark 一路挂钟收尾) ====================
            // 期望串逐字抄自 lib\maa.ps1 行 510-512(基线 c498da0)；durSec/chainText 由壳喂现场量。
            b.Chk("收尾整句：675 秒→11.2(ToEven)、Detail 拼排序好的链表、Duration 原样透传", () =>
            {
                object r = RunLoops.MaaFinish(Inp("durSec", 675.0, "chainText", "Fight, Recruit"));
                return Lgs(r).Length == 1 && LLv(r, 0) == "OK"
                    && LTx(r, 0) == "MAA 日常链路完成，用时 11.2 分钟"
                    && Stat(r) == "ok" && Det(r) == "完成链路：Fight, Recruit" && Math.Abs(Dur(r) - 675.0) < 1e-9;
            });
            b.Chk("整分钟打整数形(660→『11 分钟』)；链表空也不冒充有链路", () =>
            {
                object r = RunLoops.MaaFinish(Inp("durSec", 660.0, "chainText", ""));
                return LTx(r, 0) == "MAA 日常链路完成，用时 11 分钟" && Det(r) == "完成链路：";
            });

            // ==================== 批次 9：apply-m9a-events(m9a 一路状态机段) ====================
            // 期望串逐字抄自 lib\m9a.ps1 Register-M9AEvent 十三档(基线 c498da0)。
            b.Chk("task 首跳：进行中[N] 用 append 后的计数、chainStarted 落定、touch 生效", () =>
            {
                object r = M9E(new object[] { M9Ev("task", "启动游戏") }, M9State(null));
                return Lgs(r).Length == 1 && LLv(r, 0) == "INFO" && LTx(r, 0) == "M9A 进行中[1] 启动游戏"
                    && Gd.List(SV(r, "tasks")).Length == 1 && Gd.Truthy(SV(r, "chainStarted"))
                    && Gd.Int(SV(r, "eventCount"), -1) == 1 && Dir(r, "TouchLastLog");
            });
            b.Chk("tasks 键为 $null → @($null)+t 的 **[null,t]** 占位元素照落(计数从 2 起，旧行为)", () =>
            {
                object r = M9E(new object[] { M9Ev("task", "收取荒原") }, M9State(new object[] { "tasks", null }));
                return LTx(r, 0) == "M9A 进行中[2] 收取荒原" && Gd.List(SV(r, "tasks")).Length == 2;
            });
            b.Chk("queue-prepared：queueCount 落数、>0 才算开跑、日志吃原文 Text；0 不开 chainStarted", () =>
            {
                object a = M9E(new object[] { M9Ev("queue-prepared", "10") }, M9State(null));
                object z = M9E(new object[] { M9Ev("queue-prepared", "0") }, M9State(null));
                return LTx(a, 0) == "M9A 已提交任务队列：任务数量=10" && Gd.Int(SV(a, "queueCount"), -1) == 10
                    && Gd.Truthy(SV(a, "chainStarted"))
                    && Gd.Int(SV(z, "queueCount"), -1) == 0 && !Gd.Truthy(SV(z, "chainStarted"));
            });
            b.Chk("finish 分流三态：空账→connectPhase=done；有任务→chainDone；tasks=$null(@($null).Count=1)→也算 chainDone", () =>
            {
                object a = M9E(new object[] { M9Ev("finish", "") }, M9State(null));
                object b1 = M9E(new object[] { M9Ev("finish", "") }, M9State(new object[] { "tasks", new object[] { "启动游戏" } }));
                object c1 = M9E(new object[] { M9Ev("finish", "") }, M9State(new object[] { "tasks", null }));
                return Gd.Str(SV(a, "connectPhase")) == "done" && !Gd.Truthy(SV(a, "chainDone"))
                    && Gd.Truthy(SV(b1, "chainDone")) && Gd.Str(SV(b1, "connectPhase")) == "none"
                    && Gd.Truthy(SV(c1, "chainDone"));
            });
            // 刀㉓ 笔(b) 第三子批 第三刀(11-08) 补两枚「累计面」对面格——PS 侧 [t12] 留着的两枚量的是
            // ①connect-start→finish 两拍中继后 connectPhase 由 started 迁到 done（上面 765 那格 a 支是从 none
            //   直接喂 finish，没经过 started ⇒ 半同形，改错 started 这条迁移它不会红）；
            // ②queue-prepared 10 + 逐枚 task 事件累计出 tasks 账再 finish → chainDone（765 那格 b1 支是预置
            //   tasks 数组，没走累计面）。两枚都照 PS 的分拍形状喂，State 原样中继回壳（与 774/800/832 同款链路）。
            b.Chk("connect-start 后 finish：connectPhase 由 started 迁到 done、chainDone 仍假(两拍中继)", () =>
            {
                object a = M9E(new object[] { M9Ev("connect-start", "") }, M9State(null));
                if (Gd.Str(SV(a, "connectPhase")) != "started") return false;
                object f = M9E(new object[] { M9Ev("finish", "") }, GState(a));
                return Gd.Str(SV(f, "connectPhase")) == "done" && !Gd.Truthy(SV(f, "chainDone"))
                    && Gd.Int(SV(f, "eventCount"), -1) == 2;
            });
            b.Chk("queue-prepared 10 + 十枚 task 累计 + finish → chainDone（走累计面，不是预置账）", () =>
            {
                object q = M9E(new object[] { M9Ev("queue-prepared", "10") }, M9State(null));
                object[] ts = new object[10];
                for (int i = 0; i < 10; i++) ts[i] = M9Ev("task", "任务" + (i + 1));
                object t = M9E(ts, GState(q));
                object f = M9E(new object[] { M9Ev("finish", "") }, GState(t));
                return Gd.Int(SV(f, "queueCount"), -1) == 10 && Gd.List(SV(f, "tasks")).Length == 10
                    && Gd.Truthy(SV(f, "chainDone")) && Gd.Int(SV(f, "eventCount"), -1) == 12;
            });
            b.Chk("task-fail 累计：failedTasks 一条一报、failed 只认第一条、WARN 带累计数", () =>
            {
                object r1 = M9E(new object[] { M9Ev("task-fail", "启动游戏") }, M9State(null));
                if (LLv(r1, 0) != "WARN" || LTx(r1, 0) != "M9A 任务失败：启动游戏(累计 1 个)") return false;
                if (Gd.Str(SV(r1, "failed")) != "任务失败：启动游戏") return false;
                // 上一拍的 State 原样中继回壳再喂回来(与批 7/8 同款链路)
                object r2 = M9E(new object[] { M9Ev("task-fail", "收取荒原") }, GState(r1));
                return LTx(r2, 0) == "M9A 任务失败：收取荒原(累计 2 个)"
                    && Gd.List(SV(r2, "failedTasks")).Length == 2
                    && Gd.Str(SV(r2, "failed")) == "任务失败：启动游戏";
            });
            b.Chk("failedTasks 空数组按 $null -eq @() 塌缩 → 第一条直接成 [@t] 而不是 [..,t](累计 1 个)", () =>
            {
                object r = M9E(new object[] { M9Ev("task-fail", "使用兑换码") },
                    M9State(new object[] { "failedTasks", new object[0] }));
                return Gd.List(SV(r, "failedTasks")).Length == 1 && LTx(r, 0) == "M9A 任务失败：使用兑换码(累计 1 个)";
            });
            b.Chk("res-latest 三态：有差 WARN 整句 / -ne 不分大小写(V4.7.1=v4.7.1 不 WARN) / 无 current 不 WARN 但照记", () =>
            {
                object a = M9E(new object[] { M9Ev("res-latest", "v4.10.0") }, M9State(new object[] { "resCurrent", "v4.7.1" }));
                object b1 = M9E(new object[] { M9Ev("res-latest", "v4.7.1") }, M9State(new object[] { "resCurrent", "V4.7.1" }));
                object c1 = M9E(new object[] { M9Ev("res-latest", "v4.10.0") }, M9State(null));
                return Lgs(a).Length == 1 && LLv(a, 0) == "WARN"
                    && LTx(a, 0) == "M9A 资源/程序有新版：当前 v4.7.1 → 最新 v4.10.0(整包更新，命令行没有更新入口，需在 M9A 界面点『更新』)"
                    && Lgs(b1).Length == 0 && Lgs(c1).Length == 0 && Gd.Str(SV(c1, "resLatest")) == "v4.10.0";
            });
            b.Chk("connect-fail 只留第一条：第二拍再喂不改值、不再打 WARN", () =>
            {
                object r1 = M9E(new object[] { M9Ev("connect-fail", "初始化控制器失败：AdbSerial 为空") }, M9State(null));
                if (Lgs(r1).Length != 1 || LLv(r1, 0) != "WARN"
                    || LTx(r1, 0) != "M9A 连不上模拟器：初始化控制器失败：AdbSerial 为空") return false;
                object r2 = M9E(new object[] { M9Ev("connect-fail", "第二条") }, GState(r1));
                return Lgs(r2).Length == 0 && Gd.Str(SV(r2, "connectFail")) == "初始化控制器失败：AdbSerial 为空";
            });
            b.Chk("不认识的 Kind：只加 eventCount、**不** touch lastLog；noise 反过来只 touch 不加账", () =>
            {
                object a = M9E(new object[] { M9Ev("weird", "x") }, M9State(null));
                object n = M9E(new object[] { M9Ev("noise", "") }, M9State(null));
                return Gd.Int(SV(a, "eventCount"), -1) == 1 && !Dir(a, "TouchLastLog") && Lgs(a).Length == 0
                    && Dir(n, "TouchLastLog") && Lgs(n).Length == 0;
            });
            b.Chk("纯状态六档(chain-start/done/ui-start/connect-start/res-current/fail)：改对键、touch 全亮、零日志", () =>
            {
                object r = M9E(new object[] {
                    M9Ev("chain-start", ""), M9Ev("chain-done", ""), M9Ev("ui-start", ""),
                    M9Ev("connect-start", ""), M9Ev("res-current", "v4.7.1"), M9Ev("fail", "任务运行失败"),
                }, M9State(null));
                return Lgs(r).Length == 0 && Dir(r, "TouchLastLog")
                    && Gd.Truthy(SV(r, "chainStarted")) && Gd.Truthy(SV(r, "chainDone"))
                    && Gd.Truthy(SV(r, "startRequested")) && Gd.Str(SV(r, "connectPhase")) == "started"
                    && Gd.Str(SV(r, "resCurrent")) == "v4.7.1" && Gd.Str(SV(r, "failed")) == "任务运行失败"
                    && Gd.Int(SV(r, "eventCount"), -1) == 6;
            });
            b.Chk("fail 档不覆盖已有 failed(-not $State.failed 口径)", () =>
            {
                object r = M9E(new object[] { M9Ev("fail", "后来的") }, M9State(new object[] { "failed", "先到的" }));
                return Gd.Str(SV(r, "failed")) == "先到的";
            });
            b.Chk("eventCount 跨批累加：上拍 State 原样中继回来再 +2", () =>
            {
                object r1 = M9E(new object[] { M9Ev("task", "启动游戏") }, M9State(null));
                object r2 = M9E(new object[] { M9Ev("noise", ""), M9Ev("chain-done", "") }, GState(r1));
                return Gd.Int(SV(r2, "eventCount"), -1) == 3
                    && Gd.List(SV(r2, "tasks")).Length == 1 && Gd.Truthy(SV(r2, "chainDone"));
            });
            b.Chk("m9a 空事件批 → 状态原样带回、零日志、不 touch lastLog", () =>
            {
                object r = M9E(new object[0], M9State(new object[] { "eventCount", 3, "chainStarted", true }));
                return Lgs(r).Length == 0 && !Dir(r, "TouchLastLog")
                    && Gd.Int(SV(r, "eventCount"), -1) == 3 && Gd.Truthy(SV(r, "chainStarted"));
            });
            b.Chk("queue-prepared 负数 → queueCount 照落 -2、不算开跑、日志吃原句", () =>
            {
                object r = M9E(new object[] { M9Ev("queue-prepared", "-2") }, M9State(null));
                return LTx(r, 0) == "M9A 已提交任务队列：任务数量=-2" && Gd.Int(SV(r, "queueCount"), -9) == -2
                    && !Gd.Truthy(SV(r, "chainStarted"));
            });
            b.Chk("未变化的可空键原样回填(failed/queueCount/resCurrent 没喂就是 null，不被并成空串)", () =>
            {
                object r = M9E(new object[] { M9Ev("noise", "") }, M9State(null));
                Dictionary s = GState(r);
                return s.ContainsKey("failed") && s["failed"] == null
                    && s["queueCount"] == null && s["resCurrent"] == null && s["connectFail"] == null
                    && s["failedTasks"] == null && Gd.Str(s["connectPhase"]) == "none";
            });

            return b.Result("looptest");
        }

        // ---------- m9a 一路的输入构造器(键面与壳的迷你循环一字不差) ----------
        static object MTick(double el, bool seen, bool rescue, string status, string detail, int tasks, object st, object[] rx)
        {
            return MTickR2(el, seen, rescue, status, detail, tasks, st, rx, 2);
        }
        static object MTickR(double el, bool seen, string status, string detail, int tasks, object st, object[] rx)
        {
            return MTickR2(el, seen, true, status, detail, tasks, st, rx, 2);
        }
        static object MTickR2(double el, bool seen, bool rescue, string status, string detail, int tasks, object st, object[] rx, int maxRaw)
        {
            return RunLoops.M9aTick(Inp(
                "now", Inp("elapsedSec", el),
                "live", Inp("seenGame", seen, "rescueAvailable", rescue),
                "res", Inp("status", status, "detail", detail, "tasksCount", tasks),
                "cfg", Inp("rescueMaxRaw", maxRaw),
                "state", st, "effectsResolved", rx ?? new object[0]));
        }
        static object MTickRescueMax(object maxRaw)
        {
            return RunLoops.M9aTick(Inp(
                "now", Inp("elapsedSec", 100.0),
                "live", Inp("seenGame", true, "rescueAvailable", true),
                "res", Inp("status", "unknown-format", "detail", "", "tasksCount", 0),
                "cfg", Inp("rescueMaxRaw", maxRaw),
                "state", null, "effectsResolved", new object[0]));
        }
        static Dictionary AskRc(bool tried, bool done, string detail)
        {
            return Inp("kind", "ask-screen", "exception", false, "tried", tried, "done", done, "detail", detail);
        }
        static Dictionary AskX(string msg) { return Inp("kind", "ask-screen", "exception", true, "message", msg); }
        static string EWhy(object r, int i) { return Gd.Str(Gd.Get(E(r, i), "Why")); }
        static bool Resc(object r) { return Gd.Truthy(Gd.Get(D(r), "Rescued")); }
        static double Dur(object r)
        {
            object v = Gd.Get(D(r), "Duration");
            return v == null ? -1.0 : Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        static object[] Add(object[] head, object tailItem)
        {
            object[] outA = new object[head.Length + 1];
            Array.Copy(head, outA, head.Length);
            outA[head.Length] = tailItem;
            return outA;
        }

        // ---------- 批次 9 的输入构造器(键面与 maa.ps1/m9a.ps1 转发壳喂的一字不差) ----------
        static Dictionary GEState(object[] kv)
        {
            Dictionary<string, object> d = Inp(
                "completed", new Dictionary<string, object>(), "errors", new object[0],
                "benignPatterns", new object[0], "finished", false, "drained", false, "stallWarn", "");
            Merge(d, kv);
            return d;
        }
        static Dictionary GEv(object[] kv)
        {
            Dictionary<string, object> d = Inp("Kind", "", "Chain", "", "Msg", "", "Used", "", "Pattern", "");
            Merge(d, kv);
            return d;
        }
        static object MGE(object[] evs, Dictionary st)
        {
            return RunLoops.MaaGuiEvents(Inp("events", evs, "state", st));
        }
        static Dictionary GState(object r) { return D(Gd.Get(D(r), "State")); }
        static object[] MakeErrors(int n)
        {
            object[] a = new object[n];
            for (int i = 0; i < n; i++) a[i] = "e" + i;
            return a;
        }

        static Dictionary M9State(object[] kv)
        {
            Dictionary<string, object> d = Inp(
                "tasks", new object[0], "chainStarted", false, "chainDone", false, "connectPhase", "none",
                "failed", null, "queueCount", null, "startRequested", false, "eventCount", 0,
                "failedTasks", null, "resCurrent", null, "resLatest", null, "connectFail", null);
            Merge(d, kv);
            return d;
        }
        static Dictionary M9Ev(string kind, string text) { return Inp("Kind", kind, "Text", text); }
        static object M9E(object[] evs, Dictionary st)
        {
            return RunLoops.M9aEvents(Inp("events", evs, "state", st));
        }
        static object SV(object r, string k) { return Gd.Get(GState(r), k); }
    }
}
