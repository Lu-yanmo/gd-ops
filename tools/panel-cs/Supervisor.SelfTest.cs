// tools/panel-cs/Supervisor.SelfTest.cs —— --svtest：批次 6(Supervisor.cs)的无头自检。
// 期望串逐字抄自 lib\supervisor.ps1 的旧实现(四句 Why、现值头、拍行、"读不到/没问/none/原因"话术，
// 与 tools\test-modules.ps1 [sv] 块同一批口径) —— 这里红了就是移植走样，改 Supervisor.cs，
// 不改这里的期望(§7 同一条纪律)。
// 批次纪律：不起进程、不读现场盘、不写文件 —— 证据/状态/画面/文档全部由场景直接喂进来。
// 宿主批(HostGroup)同一条纪律，只多一项：它按 Cli.SelfTest 的先例读**同目录的 .cs 源码**当文本锚，
// 且两枚 supervisor 出口都走"序列号已由 conn 供上"或"路径守卫先返回"那两支，全程不起 adb、不起 MuMuManager。
// 已知局限(与 Supervisor.cs 头注同一条)：beats/games 缺失形状旧侧 @($null) 会多出一个空元素，
// 真实 supervision.json 恒为数组，场景全部用数组形状。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace GdPanel
{
    static class GdSvSelfTest
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

        static object Ev(object done, object alive, object asstAge, object dailyAge)
        {
            return Inp("Done", done, "AssistantAlive", alive, "AsstAgeSec", asstAge, "DailyAgeSec", dailyAge);
        }

        static object Scr(object ok, object blocking, object what, object why)
        {
            return Inp("Ok", ok, "Blocking", blocking, "What", what, "Why", why);
        }

        static string KindOf(object r) { return Gd.Str(Gd.Get(D(r), "Kind")); }
        static string WhyOf(object r) { return Gd.Str(Gd.Get(D(r), "Why")); }
        static string StateOf(object r) { return Gd.Str(Gd.Get(D(r), "State")); }

        static List<string> LinesOf(object r)
        {
            List<string> outL = new List<string>();
            foreach (object x in Gd.List(Gd.Get(D(r), "Lines"))) outL.Add(Gd.Str(x));
            return outL;
        }
        static bool HasLine(List<string> ls, string frag)
        {
            foreach (string s in ls) if (s.Contains(frag)) return true;
            return false;
        }
        static int LineCountWith(List<string> ls, string frag)
        {
            int n = 0;
            foreach (string s in ls) if (s.Contains(frag)) n++;
            return n;
        }

        public static int SvTest()
        {
            Box b = new Box();
            SettingsGroup(b);
            StateGroup(b);
            ActionGroup(b);
            DetailGroup(b);
            CollectGroup(b);
            BeatGroup(b);
            HostGroup(b);
            return b.Result("--svtest");
        }

        // ---------- eval-supervisor-settings：没显式 false 就是开 + 三档钳制 ----------
        static void SettingsGroup(Box b)
        {
            object d = Supervisor.Settings(Inp());
            b.Chk("缺省：开/60s/30分/额度 1", () =>
                (bool)Gd.Get(D(d), "Enabled") && Gd.Int(Gd.Get(D(d), "IntervalSec"), -1) == 60 &&
                Gd.Int(Gd.Get(D(d), "QuietSec"), -1) == 1800 && Gd.Int(Gd.Get(D(d), "MaxInterventions"), -1) == 1);

            d = Supervisor.Settings(Inp("supervisor", false));
            b.Chk("supervisor=false 才关", () => !(bool)Gd.Get(D(d), "Enabled"));
            d = Supervisor.Settings(Inp("supervisor", true));
            b.Chk("supervisor=true 是开", () => (bool)Gd.Get(D(d), "Enabled"));
            d = Supervisor.Settings(Inp("supervisor", 0));
            b.Chk("数值 0 按 PS 等值(x -ne $false)算关", () => !(bool)Gd.Get(D(d), "Enabled"));
            d = Supervisor.Settings(Inp("supervisor", "False"));
            b.Chk("字符串 'False' 是关", () => !(bool)Gd.Get(D(d), "Enabled"));
            d = Supervisor.Settings(Inp("supervisor", "false"));
            b.Chk("字符串 'false' 大小写不敏感也是关", () => !(bool)Gd.Get(D(d), "Enabled"));
            d = Supervisor.Settings(Inp("supervisor", "yes"));
            b.Chk("其余字符串按 PS 等值算开(只认 \"False\")", () => (bool)Gd.Get(D(d), "Enabled"));

            d = Supervisor.Settings(Inp("intervalSeconds", 5));
            b.Chk("间隔<15s 归 60(拍太快没意义)", () => Gd.Int(Gd.Get(D(d), "IntervalSec"), -1) == 60);
            d = Supervisor.Settings(Inp("intervalSeconds", 90));
            b.Chk("间隔 90s 原样", () => Gd.Int(Gd.Get(D(d), "IntervalSec"), -1) == 90);
            d = Supervisor.Settings(Inp("intervalSeconds", 15));
            b.Chk("间隔踩线 15s 不钳(<15 才钳)", () => Gd.Int(Gd.Get(D(d), "IntervalSec"), -1) == 15);
            d = Supervisor.Settings(Inp("intervalSeconds", 0));
            b.Chk("间隔 0 是假值 → 走缺省 60", () => Gd.Int(Gd.Get(D(d), "IntervalSec"), -1) == 60);

            d = Supervisor.Settings(Inp("quietMinutes", 1));
            b.Chk("静止阈值下限 5 分钟(=300 秒)", () => Gd.Int(Gd.Get(D(d), "QuietSec"), -1) == 300);
            d = Supervisor.Settings(Inp("quietMinutes", 45));
            b.Chk("静止阈值 45 分 → 2700 秒", () => Gd.Int(Gd.Get(D(d), "QuietSec"), -1) == 2700);

            d = Supervisor.Settings(Inp("maxInterventions", 0));
            b.Chk("额度可为 0(纯报告模式，0 不是 null)", () => Gd.Int(Gd.Get(D(d), "MaxInterventions"), -1) == 0);
            d = Supervisor.Settings(Inp("maxInterventions", -3));
            b.Chk("负额度钳 0", () => Gd.Int(Gd.Get(D(d), "MaxInterventions"), -1) == 0);
            d = Supervisor.Settings(Inp("maxInterventions", 2));
            b.Chk("额度 2 原样", () => Gd.Int(Gd.Get(D(d), "MaxInterventions"), -1) == 2);
        }

        // ---------- parse-supervisor-state：done/dead/quiet/busy，判据顺序是认真的 ----------
        static void StateGroup(Box b)
        {
            b.Chk("完成线压死一切：进程没了也判 done", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(true, false, 9999, 9999), "everAlive", true))) == "done");
            b.Chk("曾活着+现在没了+没完成 = dead", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, false, 10, 10), "everAlive", true))) == "dead");
            b.Chk("助手从没活着过 ≠ dead(开轮太早)", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, false, 9999, 10), "everAlive", false))) == "busy");
            b.Chk("两本日志都超阈值才 quiet", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, true, 9999, 9999), "quietSec", 1800))) == "quiet");
            b.Chk("只有助手日志老 = 不算静止", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, true, 9999, 10), "quietSec", 1800))) == "busy");
            b.Chk("只有主日志老 = 不算静止", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, true, 10, 9999), "quietSec", 1800))) == "busy");
            b.Chk("刚好踩在阈值上不算静止(严格大于)", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, true, 1800, 1800), "quietSec", 1800))) == "busy");
            b.Chk("ages 缺失按 0 收 → busy", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, true, null, null)))) == "busy");
            b.Chk("Decimal 年龄(信封里小数)也参与比较", () =>
                StateOf(Supervisor.State(Inp("evidence", Ev(false, true, 1800.5m, 1801.25m), "quietSec", 1800))) == "quiet");
        }

        // ---------- resolve-supervisor-action：决策表 + 四句 Why 逐字 ----------
        static void ActionGroup(Box b)
        {
            b.Chk("busy 不动", () => KindOf(Supervisor.Action(Inp("state", "busy"))) == "none");
            b.Chk("done 不动", () => KindOf(Supervisor.Action(Inp("state", "done"))) == "none");
            b.Chk("DONE 大写也不动(PS -eq 大小写不敏感)", () => KindOf(Supervisor.Action(Inp("state", "DONE"))) == "none");
            object r = Supervisor.Action(Inp("state", "dead"));
            b.Chk("dead 通知一次，话术逐字", () =>
                KindOf(r) == "notify" && WhyOf(r) == "助手进程不见了，而这一轮还没宣布完成");
            b.Chk("dead 通知过就闭嘴", () =>
                KindOf(Supervisor.Action(Inp("state", "dead", "alreadyNotified", true))) == "none");
            b.Chk("静止一拍不动手(可能是抖动)", () =>
                KindOf(Supervisor.Action(Inp("state", "quiet", "quietStreak", 1, "screen", Scr(true, false, "x", ""), "canRestart", true))) == "none");
            r = Supervisor.Action(Inp("state", "quiet", "quietStreak", 2, "screen", Scr(true, true, "活动弹窗", ""), "canRestart", true));
            b.Chk("blocking 交给主流程兜底，话术逐字", () =>
                KindOf(r) == "none" && WhyOf(r) == "画面被「活动弹窗」挡着，交给主流程兜底");
            r = Supervisor.Action(Inp("state", "quiet", "quietStreak", 2, "screen", Scr(true, false, "日常界面", ""), "canRestart", true,
                "interventions", 0, "maxInterventions", 1));
            b.Chk("静止+画面干净+许重启 = restart_assistant，话术逐字", () =>
                KindOf(r) == "restart_assistant" && WhyOf(r) == "连续静止且画面没有挡路的东西(日常界面)，判定层没醒，重启助手");
            r = Supervisor.Action(Inp("state", "quiet", "quietStreak", 2, "screen", Scr(true, false, "日常界面", ""), "canRestart", true,
                "interventions", 1, "maxInterventions", 1));
            b.Chk("额度用完退回 notify，话术逐字", () =>
                KindOf(r) == "notify" && WhyOf(r) == "连续静止、画面没有弹窗，但这一路不许自动重启(或额度用完)");
            b.Chk("这一路不许重启 → 只 notify", () =>
                KindOf(Supervisor.Action(Inp("state", "quiet", "quietStreak", 2, "screen", Scr(true, false, "日常界面", ""), "canRestart", false))) == "notify");
            r = Supervisor.Action(Inp("state", "quiet", "quietStreak", 2, "screen", Scr(false, false, "", "截图失败")));
            b.Chk("看不清画面只 notify，话术逐字带上下文", () =>
                KindOf(r) == "notify" && WhyOf(r) == "连续静止，且看不清画面(截图失败)");
            r = Supervisor.Action(Inp("state", "quiet", "quietStreak", 2, "screen", Scr(false, false, "", "")));
            b.Chk("没上下文就不缀括号", () => KindOf(r) == "notify" && WhyOf(r) == "连续静止，且看不清画面");
            b.Chk("notify 说过不再重复轰炸", () =>
                KindOf(Supervisor.Action(Inp("state", "quiet", "quietStreak", 3, "screen", Scr(false, false, "", ""), "alreadyNotified", true))) == "none");
            b.Chk("未知状态按旧实现 fall-through 走 quiet 流程(一拍仍不动)", () =>
                KindOf(Supervisor.Action(Inp("state", "whatever", "quietStreak", 1))) == "none");
        }

        // ---------- compose-supervisor-detail：假文档钉整段话术 ----------
        static void DetailGroup(Box b)
        {
            b.Chk("喂 null(从没监督过)返回空数组而不是抛", () => LinesOf(Supervisor.Detail(Inp())).Count == 0);

            object beat = Inp("ts", "2026-09-26 13:05:00", "games", new object[] {
                Inp("game", "t1", "state", "quiet", "alive", true, "asst_age", 1810, "done", false,
                    "screen", "clear", "action", "notify", "why", "连续静止，额度用完"),
                Inp("game", "t2", "state", "dead", "alive", false, "asst_age", -1, "done", true,
                    "screen", null, "action", "none", "why", "") });
            object doc = Inp("pid", 4321, "owner", 1, "started", "2026-09-26 13:00:00", "games", "t1+t2",
                "updated", "2026-09-26 13:05:00", "stopped", "", "beats", new object[] { beat }, "actions", new object[0]);
            List<string> ls = LinesOf(Supervisor.Detail(Inp("sv", doc, "date", "20260926")));
            b.Chk("现值头把 PID/路线/上岗/最后一拍念全", () =>
                ls.Count > 0 && ls[0] == "现值：PID 4321 · 盯的路线 t1+t2 · 上岗 2026-09-26 13:00:00 · 最后一拍 2026-09-26 13:05:00");
            b.Chk("没收工不许出现「收工」二字", () => !string.Join(" ", ls.ToArray()).Contains("收工"));
            b.Chk("流水行走注入的日期戳", () => HasLine(ls, @"流水：logs\supervision-20260926.jsonl(一行一拍，留 7 天)"));
            b.Chk("一拍两路 → 展开成两行(谁都没被合并)", () => LineCountWith(ls, "  13:05:00 ") == 2);
            b.Chk("asst_age=-1 说成\"读不到\"，不是\"-1秒\"", () =>
                HasLine(ls, "t2=dead 助手日志静默 读不到") && !HasLine(ls, "-1秒"));
            b.Chk("画面没问过说\"没问\"", () => HasLine(ls, "画面 没问"));
            b.Chk("数值 age 后缀\"秒\"", () => HasLine(ls, "助手日志静默 1810秒"));
            b.Chk("没动作说 none，有动作原样念", () => HasLine(ls, "动作 none") && HasLine(ls, "动作 notify"));
            b.Chk("why 为空的那一路不缀\"原因 \"", () => LineCountWith(ls, "原因 ") == 1);
            b.Chk("没介入过 → 明说没有", () => HasLine(ls, "这一轮没有介入过(它唯一的干预是重启助手，重启过会记在这里)。"));
            b.Chk("整段输出不含 '|'(面板协议分隔符)", () => !string.Join("\n", ls.ToArray()).Contains("|"));

            object docStopped = Inp("pid", 4321, "owner", 1, "started", "2026-09-26 13:00:00", "games", "t1+t2",
                "updated", "2026-09-26 13:05:00", "stopped", "2026-09-26 13:20:00", "beats", new object[0],
                "actions", new object[] { Inp("ts", "13:06:00", "game", "t1", "kind", "restart_assistant", "ok", false, "why", "重启没成") });
            ls = LinesOf(Supervisor.Detail(Inp("sv", docStopped, "date", "20260926")));
            b.Chk("收工时间真收工时出现在头行", () => ls[0].EndsWith(" · 收工 2026-09-26 13:20:00"));
            b.Chk("拍被挤掉仍报\"还没落下第一拍\"，且不吞介入记录", () =>
                HasLine(ls, "这一枚监督进程还没落下第一拍(起拍间隔默认 60 秒)。") &&
                HasLine(ls, "介入记录(1 次)：") && HasLine(ls, "  13:06:00 t1 restart_assistant 没成 — 重启没成"));

            object b1 = Inp("ts", "2026-09-26 13:01:00", "games", new object[] { Inp("game", "t1", "state", "busy", "alive", true, "asst_age", 5, "done", false, "screen", "", "action", "none", "why", "") });
            object b2 = Inp("ts", "2026-09-26 13:02:00", "games", new object[] { Inp("game", "t1", "state", "busy", "alive", true, "asst_age", 5, "done", false, "screen", "", "action", "none", "why", "") });
            object b3 = Inp("ts", "2026-09-26 13:03:00", "games", new object[] { Inp("game", "t1", "state", "quiet", "alive", true, "asst_age", 30, "done", false, "screen", "", "action", "none", "why", "") });
            ls = LinesOf(Supervisor.Detail(Inp("sv", Inp("pid", 1, "games", "t1", "started", "s", "updated", "u",
                "beats", new object[] { b1, b2, b3 }, "actions", new object[0]), "maxBeats", 2, "date", "20260926")));
            b.Chk("MaxBeats=2 → 只念最后两拍，头行说\"最近 2 拍\"", () =>
                HasLine(ls, "最近 2 拍(字段：状态 / 助手日志静默 / 进程 / 完成线 / 画面 / 动作)：") &&
                LineCountWith(ls, "  13:0") == 2 && HasLine(ls, "13:02:00") && HasLine(ls, "13:03:00") && !HasLine(ls, "13:01:00"));
            ls = LinesOf(Supervisor.Detail(Inp("sv", Inp("pid", 1, "games", "t1", "started", "s", "updated", "u",
                "beats", new object[] { b1 }, "actions", new object[0]), "maxBeats", 0, "date", "20260926")));
            b.Chk("MaxBeats=0 钳到 1 拍", () => HasLine(ls, "最近 1 拍"));
            b.Chk("screen 空串也走\"没问\"", () => HasLine(ls, "画面 没问"));
        }

        static int ListCount(object container, string key)
        {
            int n = 0;
            foreach (object x in Gd.List(Gd.Get(D(container), key))) n++;
            return n;
        }

        static object ItemAt(object container, string key, int idx)
        {
            int n = 0;
            foreach (object x in Gd.List(Gd.Get(D(container), key))) { if (n == idx) return x; n++; }
            return null;
        }

        // ---------- 采集真演（刀② 补尺 11-02：从 tools\test-modules.ps1 [sv] 段那六枚搬进来） ----------
        // 这两组是本件头注那条「不写文件」纪律的第一次破例，破得有限：只写自己的临时目录（gd-svtest-*-<pid>，
        // 跑完 finally 整棵删掉），进程表一律由注入的探针答（PS 侧同形＝function Test-GdProcessRunning 钉桩），
        // 所以既不起进程，也不读现场盘上的任何真路径。
        // 为什么必须搬而不能留在 PS 侧：这些格钉的是「首拍只对齐不扫旧内容／只留最近 30 拍／流水一行一拍」，
        // 而刀③ 摘的正是这套语义的 PS 真身 —— 尺留在会被拆掉的那面墙上，摘完就没人管这一格了。
        static void CollectGroup(Box b)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gd-svtest-collect-" + System.Diagnostics.Process.GetCurrentProcess().Id);
            try { Directory.CreateDirectory(tmp); } catch { }
            UTF8Encoding noBom = new UTF8Encoding(false);
            try
            {
                SupervisorHost.Session ss = new SupervisorHost.Session();
                SupervisorHost.Route r1 = SupervisorHost.NewRoute("t1", true, "");
                SupervisorHost.Route r2 = SupervisorHost.NewRoute("t2", true, "");
                SupervisorHost.ProcAliveProbe alive = delegate(string n) { return true; };

                string logA = Path.Combine(tmp, "a.log");
                File.WriteAllText(logA, "上一轮留下的 任务已全部完成 旧行\n", noBom);
                Dictionary<string, object> specA = Inp("AssistantProc", "假助手", "AssistantLog", logA, "DonePattern", "任务已全部完成");
                object ev = SupervisorHost.Collect(ss, r1, specA, "", alive);
                b.Chk("探针答活着时回件 AssistantAlive 为真(注入缝自己被钉住，不是缝开了没人走)",
                    () => Gd.Truthy(Gd.Get(D(ev), "AssistantAlive")));
                b.Chk("首拍只对末尾，不把旧完成线当这一轮的", () => !Gd.Truthy(Gd.Get(D(ev), "Done")));
                File.AppendAllText(logA, "这一轮 任务已全部完成\n", noBom);
                ev = SupervisorHost.Collect(ss, r1, specA, "", alive);
                b.Chk("拍与拍之间新增的行才算数", () => Gd.Truthy(Gd.Get(D(ev), "Done")));

                string logB = Path.Combine(tmp, "b.log");
                File.WriteAllText(logB, "x\n", noBom);
                Dictionary<string, object> specB = Inp("AssistantProc", "假助手", "AssistantLog", logB,
                    "DonePattern", "启动任务完成", "ProgressPattern", "开始任务：");
                SupervisorHost.Collect(ss, r2, specB, "", alive);          // 首拍：只对齐偏移
                File.AppendAllText(logB, "启动任务完成\n", noBom);
                ev = SupervisorHost.Collect(ss, r2, specB, "", alive);
                b.Chk("没见过进度线，「启动任务完成」单独出现不算完成", () => !Gd.Truthy(Gd.Get(D(ev), "Done")));
                File.AppendAllText(logB, "开始任务：\n启动任务完成\n", noBom);
                ev = SupervisorHost.Collect(ss, r2, specB, "", alive);
                b.Chk("进度线补上后，完成线算数", () => Gd.Truthy(Gd.Get(D(ev), "Done")));

                string logC = Path.Combine(tmp, "c.log");
                File.WriteAllText(logC, "任务已全部完成\n", noBom);
                Dictionary<string, object> specC = Inp("AssistantProc", "假助手", "AssistantLog", logC, "DonePattern", "任务已全部完成");
                ev = SupervisorHost.Collect(ss, r2, specC, "", alive);     // 同一路游戏换日志路径＝跨天换本
                b.Chk("日志换路径(跨天)后首拍同样不扫旧内容", () => !Gd.Truthy(Gd.Get(D(ev), "Done")));
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }

        // ---------- 记录（同上搬入；路径三枚全由缝注入，落盘读数只出现在临时目录里） ----------
        static void BeatGroup(Box b)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "gd-svtest-beat-" + System.Diagnostics.Process.GetCurrentProcess().Id);
            try { Directory.CreateDirectory(tmp); } catch { }
            try
            {
                string statePath = Path.Combine(tmp, "supervision.json");
                string beatPath = Path.Combine(tmp, "supervision-20260926.jsonl");
                SupervisorHost.Session ss = new SupervisorHost.Session();
                SupervisorHost.ResetMetaForTest(4242, 1, "2026-09-26 00:00:00", "all");
                List<object> perGame = new List<object>();
                perGame.Add(Inp("game", "t1", "state", "busy", "alive", true, "asst_age", 5, "done", false,
                    "screen", "", "action", "none", "why", ""));
                SupervisorHost.WriteBeatTo(ss, perGame, statePath, beatPath, tmp);
                object doc = SupervisorHost.ReadSummaryFrom(statePath);
                b.Chk("一拍落两个文件(jsonl 流水 + json 现值)", () =>
                    File.Exists(beatPath) && doc != null && Gd.Int(Gd.Get(D(doc), "pid"), -1) == 4242);
                object beat0 = ItemAt(doc, "beats", 0);
                b.Chk("beat 内容原样读回", () =>
                    Gd.Str(Gd.Get(D(ItemAt(beat0, "games", 0)), "game")) == "t1");
                b.Chk("现值头三枚由注入缝供数到底(pid/owner/games 都没被写手改掉)", () =>
                    Gd.Int(Gd.Get(D(doc), "owner"), -1) == 1 && Gd.Str(Gd.Get(D(doc), "games")) == "all");
                // 这一格最初写的是 ListCount(doc,"actions")==0，实测是枚恒真格：Gd.List(null) 返回空数组(Decisions.cs:71)，
                // 于是"写手把 actions 写成 null"和"写成 []"给出同一个读数。改成咬盘上那串字面量。
                b.Chk("这一轮没介入过时 actions 落成空数组而不是 null/缺键(盘上字面量)", () =>
                    File.ReadAllText(statePath).Contains("\"actions\":[]") && ListCount(doc, "actions") == 0);
                for (int i = 0; i < 32; i++) SupervisorHost.WriteBeatTo(ss, perGame, statePath, beatPath, tmp);
                doc = SupervisorHost.ReadSummaryFrom(statePath);
                // PS 侧那枚写的是 -le 30(上限口径)，这里按实现钉死 ==30：改成 31 拍仍绿＝尺没牙。
                b.Chk("json 里只留最近 30 拍(它是\"此刻\"，流水在 jsonl)", () => ListCount(doc, "beats") == 30);
                b.Chk("jsonl 一行一拍、不截断(33 拍)", () => File.ReadAllLines(beatPath).Length == 33);
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }

        // ---------- 宿主批：Gate.Host.cs 的端点算术 + 两枚 supervisor verb 的回件键名契约 ----------
        // 这一组不碰现场盘：adb/Manager 两枚路径一律指到 panel-cs.exe 自己那一枚"确实存在的文件"上，
        // 且每格都走"序列号已由 conn 供上"或"路径守卫先返回"那两支 ⇒ 全程不起任何进程、不写任何文件。
        // 钉的是形状与口径(拼接、两形入参、优先级、落空话术)，判据来源＝lib\vision-mumu.ps1 的 Get-GdMuMuAdbEndpoint 真身。
        static void HostGroup(Box b)
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string self = Path.Combine(dir, "panel-cs.exe");       // 只当"在位的文件"用，绝不起它
            string gone = Path.Combine(dir, "gd-no-such-adb-zzz.exe");

            Dictionary<string, object> S = Inp(
                "paths", Inp("mumuDir", dir, "mumuManager", "MuMuManager.exe", "mumuAdb", "panel-cs.exe"),
                "mumu", Inp("vmIndex", "0", "vmNameHint", "gd-hostgroup"));

            object e = GateHost.MuMuEndpoint(S, Inp("adb", self, "serial", "127.0.0.1:16384"));
            b.Chk("conn 供齐 adb+serial 时原样采用，不兜第二源", () =>
                Gd.Str(Gd.Get(D(e), "Adb")) == self && Gd.Str(Gd.Get(D(e), "Serial")) == "127.0.0.1:16384" &&
                Gd.Str(Gd.Get(D(e), "Why")).Length == 0);

            e = GateHost.MuMuEndpoint(S, Inp("serial", "emulator-5554"));
            b.Chk("conn 只给 serial 时 adb 由 paths.mumuDir+mumuAdb 拼出(字符串形)", () =>
                Gd.Str(Gd.Get(D(e), "Adb")).EndsWith("panel-cs.exe", StringComparison.Ordinal) &&
                Gd.Str(Gd.Get(D(e), "Serial")) == "emulator-5554");

            Dictionary<string, object> SList = Inp(
                "paths", Inp("mumuDir", dir, "mumuManager", "MuMuManager.exe",
                             "mumuAdb", new object[] { "panel-cs.exe", "nope.exe" }),
                "mumu", Inp("vmIndex", "0"));
            object eList = GateHost.MuMuEndpoint(SList, Inp("serial", "emulator-5554"));
            b.Chk("mumuAdb 写成清单时取第一枚，与字符串形同读数(照 Join-GdPath 的 @(...) 口径)", () =>
                Gd.Str(Gd.Get(D(eList), "Adb")) == Gd.Str(Gd.Get(D(e), "Adb")));

            // 相对名那一路必须挑一枚"哪儿都不在"的文件名：换成 panel-cs.exe 时，
            // 自检的工作目录恰好是 tools\panel-cs 的话 File.Exists 会变真，这一格就成了看目录脸色的假红/假绿。
            e = GateHost.MuMuEndpoint(Inp("paths", Inp("mumuAdb", "gd-no-such-rel-zzz.exe")), Inp("serial", "x:1"));
            b.Chk("mumuDir 空：拼出来还是相对名、文件不在 → 报 setup.ps1 那句而不是丢一句内部报错", () =>
                Gd.Str(Gd.Get(D(e), "Adb")).Length == 0 &&
                Gd.Str(Gd.Get(D(e), "Why")).Contains("setup.ps1") &&
                Gd.Str(Gd.Get(D(e), "Serial")).Length == 0);

            e = GateHost.MuMuEndpoint(S, Inp("adb", gone, "serial", "127.0.0.1:16384"));
            b.Chk("conn 给的 adb 文件不在：路径守卫排在序列号之前，同一句 setup.ps1 话术", () =>
                Gd.Str(Gd.Get(D(e), "Adb")).Length == 0 && Gd.Str(Gd.Get(D(e), "Why")).Contains("setup.ps1"));

            b.Chk("探活两枚空参数直接回假(不起进程)", () =>
                !GateHost.MuMuVmAlive("", "127.0.0.1:16384") && !GateHost.MuMuVmAlive(self, "") &&
                !GateHost.MuMuVmAlive(gone, "127.0.0.1:16384"));

            // 刀⑮ 笔(c)(2026-11-06)：出口面现拍 —— verb 真挂上分发链，不是名册上的虚名。
            // 夹具形：settings 与 conn 都由 body 供上 ⇒ 全程不读真 settings.json、不起任何进程(上面那条纪律一样成立)；
            // 回执三键 {Adb,Serial,Why} 的键名即契约——PS 侧两本入口(1999-event-audit／vision-ask)自此按这个名字读。
            string vdir = Path.Combine(Path.GetTempPath(), "gd-mumu-verb-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(vdir);
            try
            {
                string vi = Path.Combine(vdir, "endpoint-in.json");
                string vo = Path.Combine(vdir, "endpoint-out.json");
                GdJson.Write(vi, Inp("settings", S, "conn", Inp("adb", self, "serial", "127.0.0.1:16384")));
                int vcode = GdCli.Run("resolve-mumu-adb-endpoint", new string[] { "--in=" + vi, "--out=" + vo });
                Dictionary<string, object> venv = GdJson.ReadEnvelope(vo);
                Dictionary<string, object> vdata = D(Gd.Get(venv, "data"));
                b.Chk("CLI 端到端：resolve-mumu-adb-endpoint 真挂上分发链，信封 verb/ok 自证＋conn 供齐两源原样采用三键", () =>
                    vcode == 0 && Gd.Truthy(Gd.Get(venv, "ok")) &&
                    Gd.Str(Gd.Get(venv, "verb")) == "resolve-mumu-adb-endpoint" && vdata != null &&
                    Gd.Str(Gd.Get(vdata, "Adb")) == self &&
                    Gd.Str(Gd.Get(vdata, "Serial")) == "127.0.0.1:16384" &&
                    Gd.Str(Gd.Get(vdata, "Why")).Length == 0);
            }
            finally { try { Directory.Delete(vdir, true); } catch { } }

            // 跨语言契约：lib\rust-round.ps1 那两臂把回件读成 [pscustomobject]@{Available;Err}，
            // 键名一改，PS 侧读到的是 $null —— Available 恒假(监督静默没上岗)且不报错，正是最难查的那种坏。
            string cli = File.ReadAllText(Path.Combine(dir, "Cli.cs"), Encoding.UTF8);
            int kv = 0, at = 0;
            while ((at = cli.IndexOf("\"Available\", hr.Available, \"Err\"", at + 1, StringComparison.Ordinal)) >= 0) { kv++; }
            b.Chk("两枚 supervisor verb 的回件都按 Available/Err 两枚键交出(与 PS 臂的键名同形)", () => kv == 2);

            string host = File.ReadAllText(Path.Combine(dir, "Supervisor.Host.cs"), Encoding.UTF8);
            int meta = host.IndexOf("metaDoc = Gd.Out(", StringComparison.Ordinal);
            string[] keys = new string[] { "\"pid\"", "\"owner\"", "\"started\"", "\"games\"", "\"updated\"", "\"beats\"", "\"actions\"", "\"stopped\"" };
            b.Chk("心跳文档八枚键齐且顺序对(Supervisor.Detail 与 Get-GdSupervisorState 认的就是这套名)", () =>
            {
                // 只在 metaDoc 那一句里扫：整文件扫会让改名后的旧键名在文件后段(读回件那一处)再出现一次，
                // 于是"把 owner 改掉"这种真伤形态反而照旧绿 —— mutation 实测交回过这一形。
                if (meta < 0) return false;
                int end = host.IndexOf(';', meta);
                if (end < 0) return false;
                string stmt = host.Substring(meta, end - meta);
                int cur = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    int nx = stmt.IndexOf(keys[i], cur, StringComparison.Ordinal);
                    if (nx < 0) return false;
                    cur = nx;
                }
                return true;
            });

            // ---- 宿主批第二刀(阻塞式自举回路)的五组锚 ----
            // 为什么这几组只读源码不跑现场：--svtest 的纪律是"不起进程、不写盘"，而这一刀的产物正是一枚子进程；
            // 能静态钉死的是**两端同名**(分派链里的 verb 与 Start 拼进 argv 的那一枚)与"心跳不再住在本进程线程上"——
            // 这两条形变了，接进轮档那一臂就是静默接断监督，当场查不出来。
            b.Chk("出口两端同名：Cli.cs 分派链有 supervise-loop，且 SpawnLoop 的 argv 首枚就是它", () =>
                cli.IndexOf("verb == \"supervise-loop\"", StringComparison.Ordinal) >= 0 &&
                host.IndexOf("psi.Arguments = \"supervise-loop ", StringComparison.Ordinal) >= 0);

            b.Chk("心跳不住在本进程的线程上(new Thread／自家停拍标记都不许在场，子进程是隐藏起的)", () =>
                Occ(host, "new Thread(") == 0 && Occ(host, "static Thread") == 0 &&
                Occ(host, "stopReq") == 0 && Occ(host, "psi.CreateNoWindow = true") == 1);

            // 盘上 pid/owner 两格是面板与 Stop 唯一的读数来源：
            //   Start 侧 pid=子进程(不是自己)，Loop 侧 pid=自己 —— 两形的 owner 都必须是调用方给的那枚，不许写成 self。
            b.Chk("上岗文档两侧各一次 NewMeta：父侧记子 pid、子侧记自己 pid，owner 一律用调用方给的那枚", () =>
                Occ(host, "NewMeta(childPid, ownerPid, games)") == 1 &&
                Occ(host, "NewMeta(SelfPid(), ownerPid, games)") == 1 &&
                Occ(host, "if (ownerPid > 0 && !ProcessAlive(ownerPid)) return \"owner\";") == 1);

            b.Chk("收工四臂话术逐字在位(停拍标记／owner 消失／-Once／存活上限，一条都不许少)", () =>
                host.IndexOf("-Once：拍完这一拍就收工", StringComparison.Ordinal) >= 0 &&
                host.IndexOf("到达存活上限 ", StringComparison.Ordinal) >= 0 &&
                host.IndexOf("收到停拍标记，监督收工", StringComparison.Ordinal) >= 0 &&
                host.IndexOf("owner 进程消失了，监督下班", StringComparison.Ordinal) >= 0);

            // 宽限与强杀三处都要带"不是自己"这一判：漏一处就是面板点一下"停止"把整轮带走。
            b.Chk("强杀只对外来 pid：Start 与 Stop 里 prevPid != selfPid 三处守卫齐", () =>
                Occ(host, "prevPid != selfPid") == 3);

            // --cap 兜底与 Start 用的是同一把尺：写成两处字面量，改默认值时必漏一处。
            b.Chk("存活上限只有一把尺：Cli 的 --cap 兜底读 SupervisorHost.CapDefault", () =>
                Occ(host, "public const int CapDefault") == 1 &&
                cli.IndexOf("SupervisorHost.CapDefault", StringComparison.Ordinal) >= 0 &&
                Occ(cli, "480") == 0);

            // ---- 刀③ 摘件笔的第一格：面板那一行不再问 PS 兜底壳在不在盘上 ----
            // 为什么钉"不在场"而不是钉别处：这一格量的是「摘件这件事不会把给人看的那一行一起摘掉」。
            // 原来 SupervisorRow 第一行钉的是 lib\supervisor.ps1 在不在盘上(抄已删的 panel.ps1 那句 $haveSupervisor)，
            // 监督面迁到本 assembly 后那枚 .ps1 只是兜底壳 —— 壳一摘，那行就静默消失(绿着变空，比红难发现)。
            // 针里带上 Combine(c.Root, 与 @" 两形，注释里那句"lib\supervisor.ps1"不会被误当活锚命中。
            string sum = File.ReadAllText(Path.Combine(dir, "Panel.Summary.cs"), Encoding.UTF8);
            b.Chk("面板的监督行不再问 PS 兜底壳在不在(在场闸门已摘，且没有别的 File.Exists 影子闸门)", () =>
                Occ(sum, "Combine(c.Root, @\"lib\\supervisor") == 0);
            // 闸门摘了，行还得能产生：头一句必须是现读心跳，占不占位只由 state\supervision.json 说了算。
            // 为什么不写成整文件的 Occ 计数：`Dictionary<string, object> sv = ReadSupervisor();` 这一形在上一段
            // (详情那一处)逐字出现过两次，整文件计数==1 会当场红成假伤 —— 改按"定义行往后那一截"区间量，
            // 顺带把 File.Exists 也只在区间内判空，摘闸门与留来源钉在同一截里。
            int srow = sum.IndexOf("static GdSummaryRow SupervisorRow(Ctx c)", StringComparison.Ordinal);
            string shead = srow < 0 ? "" : sum.Substring(srow, Math.Min(260, sum.Length - srow));
            b.Chk("SupervisorRow 仍在位且定义后头一句就是现读心跳(这一截里不许再有 File.Exists 型在场闸门)", () =>
                srow >= 0 &&
                shead.IndexOf("= ReadSupervisor();", StringComparison.Ordinal) >= 0 &&
                Occ(shead, "File.Exists") == 0);

            // ---- 刀③ 摘件笔的第二格：纯洁性那把尺从 PS 侧搬钉到本层自己 ----
            // 旧格住在 tools\test-modules.ps1 的 [sv] 门内，逐枚扫 lib\supervisor.ps1 与 tools\supervise.ps1 的代码面
            // (Get-GdCodeOnlyText 剥注释那把尺)。两件出仓后那格没有靶子，而 Supervisor.cs 头注「m3 纯洁性…这里也不许出现
            // 具体游戏名」是一句活的承诺 —— 承诺不许只留在注释里，所以尺改钉 Supervisor.cs 自己，剥注释同口径。
            // 为什么只钉本件不钉 Supervisor.Host.cs：宿主批按清单里的 id 认路线，"ark"/"r1999" 字面量本来就在位，那是另一张脸
            // 的账；在这一格里把它一起算成"也纯洁"就是偷偷放宽判据(反过来把它挂成一格红的又是另一批的活，不属本刀)。
            string svsrc = File.ReadAllText(Path.Combine(dir, "Supervisor.cs"), Encoding.UTF8);
            b.Chk("监督判定层的代码面没有游戏名/助手名字面量(纯洁性那把尺已随 PS 两件搬钉到本层)", () =>
            {
                string code = StripLineComments(svsrc);
                return Occ(code, "ark") == 0 && Occ(code, "r1999") == 0 && Occ(code, "Maa") == 0 &&
                       Occ(code, "MAA") == 0 && Occ(code, "MuMu") == 0 && Occ(code, "端游") == 0;
            });

            // ---- 刀⑧(2026-11-04) 两把闸臂进核：Gate.Host.cs 那两枚直身的锚 ----
            // 这一组同样不起进程：闸直身里"起 pwsh"那一步只能靠 noSpawn 绕开，所以钉的是
            // 三格读数的形状(挑行取末条／joined 拼全部／Code 透传)、两枚臂的常量没串台、合流与内层上限各只一把尺，
            // 以及回执键名与核内答手折进 journal 的那三枚小写字面对得平。
            // 正向锚一律**剥注释后**扫：本件头注与函数注释里写着两本工具的名字与那句挑行正则，
            // 不剥就会重演"把调用行整个换回旧壳、套件照样全绿"那笔账(test-r1999 的在册教训)。
            string gateSrc = File.ReadAllText(Path.Combine(dir, "Gate.Host.cs"), Encoding.UTF8);
            string gate = StripLineComments(gateSrc);
            string verbsSrc = StripLineComments(File.ReadAllText(Path.Combine(dir, "Cli.Verbs.cs"), Encoding.UTF8));
            string roundSrc = StripLineComments(File.ReadAllText(Path.Combine(dir, "Round.cs"), Encoding.UTF8));

            // 靶缩到函数体：两枚臂只差"脚本名＋正则"两枚常量，整文件扫两枚字面都在场 ⇒ 把两条臂对调也照旧绿。
            // 区间取"本枚签名 → 下一枚签名"，切不到就交回假(空串里不含针)，宁红不假绿。
            string updB = GateBody(gate, "internal static object M9aUpdgate", "internal static object EventAudit");
            string audB = GateBody(gate, "internal static object EventAudit", "static object GateRun");
            b.Chk("两枚臂的脚本名与挑行正则各归各的函数体(对调两枚常量当场红)", () =>
                updB.Contains(@"tools\m9a-update.ps1") && updB.Contains("已是最新|更新完成") &&
                !updB.Contains(@"tools\1999-event-audit.ps1") &&
                audB.Contains(@"tools\1999-event-audit.ps1") && audB.Contains("审计结论|无事发生|维持 event-pending") &&
                !audB.Contains(@"tools\m9a-update.ps1"));

            // 两端同名(与 supervise-loop 那格同形)：分派链与名册两处都得有，少一处就是"verb 存在但没人能调"。
            b.Chk("出口两端同名：Cli.cs 分派链与 Cli.Verbs.cs 名册都认这两枚闸 verb", () =>
                cli.IndexOf("verb == \"run-m9a-update\"", StringComparison.Ordinal) >= 0 &&
                cli.IndexOf("verb == \"run-1999-event-audit\"", StringComparison.Ordinal) >= 0 &&
                verbsSrc.IndexOf("\"run-m9a-update\"", StringComparison.Ordinal) >= 0 &&
                verbsSrc.IndexOf("\"run-1999-event-audit\"", StringComparison.Ordinal) >= 0);

            // 内层上限只有一把尺(照 CapDefault 那格的形制)：把秒数抄进第二处，改默认值时必漏一处。
            b.Chk("内层上限只有一把尺：GateRunBoundSeconds 定义一次，除定义外不许出现裸秒数", () =>
                Occ(gate, "const int GateRunBoundSeconds") == 1 && Occ(gate, "2400") == 1);

            // 合流那一步是这一批的行为差本体：PS 臂是 2>&1 一桶，两桶各扫会把 joined 的行序换成"全部 stdout 后接全部 stderr"。
            b.Chk("闸那一发按合流收(merge=true 只此一处)，其余几枚老 RunProc 调用仍走两桶", () =>
                Occ(gate, "GateRunBoundSeconds, true)") == 1 && Occ(gate, "merge ? so : new StringBuilder()") == 1);

            // 回执键名：C# 侧交 PascalCase，核内答手 jv_get_ci 折成 PS 臂当年的 code/line/joined 三枚小写。
            // 这一格量的是"三枚都在且只一次且按这个序"——多一枚少一枚换一枚，journal 两侧就对不平。
            int kAt = gate.IndexOf("\"Code\",", StringComparison.Ordinal);
            int kStart = kAt < 0 ? -1 : gate.LastIndexOf("Gd.Out(", kAt, StringComparison.Ordinal);
            int kEnd = kAt < 0 ? -1 : gate.IndexOf(';', kAt);
            b.Chk("闸回执只交 Code/Line/Joined 三枚键且顺序对(与核内答手折的小写三枚同集合)", () =>
            {
                if (kStart < 0 || kEnd <= kStart) return false;
                string stmt = gate.Substring(kStart, kEnd - kStart);
                if (Occ(stmt, "\"Code\"") != 1 || Occ(stmt, "\"Line\"") != 1 || Occ(stmt, "\"Joined\"") != 1) return false;
                int iCode = stmt.IndexOf("\"Code\"", StringComparison.Ordinal);
                int iLine = stmt.IndexOf("\"Line\"", StringComparison.Ordinal);
                int iJoi = stmt.IndexOf("\"Joined\"", StringComparison.Ordinal);
                return iCode < iLine && iLine < iJoi;
            });

            // 引擎候选序按用途分档：提权面(Round.cs 的 Elevate)仍旧 false，闸面 true。
            // 为什么两形都要钉：把 false 一起放开 = 悄悄改掉"商店版 runas 提不了"那条真身判据；
            // 把 true 收回 false = 本机一枚引擎都找不到(实测 PSHOME 不继承、Program Files 没有 7)，两枚闸恒抛＝回归。
            b.Chk("商店版命中按用途分档：提权面仍传 false，-File 直跑面传 true(两形各一次)", () =>
                Occ(roundSrc, "LocalPwsh(OptStr(c, \"pshome\", null), false)") == 1 &&
                Occ(roundSrc, "FindOnPath(\"pwsh.exe\", false)") == 1 &&
                Occ(gate, "Round.LocalPwsh(Round.OptStr(c, \"pshome\", null), true)") == 1 &&
                Occ(gate, "Round.FindOnPath(\"pwsh.exe\", true)") == 1);

            // ---- 三格功能面(全走 noSpawn 夹具，不起进程、不碰盘) ----
            object g1 = GateHost.M9aUpdgate(Inp(
                "noSpawn", true, "fakeCode", 3,
                "fakeLines", new object[] { "开头一句", "已是最新", "中间一句", "更新完成 v4.12.0" }));
            b.Chk("noSpawn 夹具下三格齐：Code 透传假码、Line 取最后一条命中、Joined 按 ' / ' 拼全部行", () =>
                Gd.Lng(Gd.Get(D(g1), "Code")) == 3 &&
                Gd.Str(Gd.Get(D(g1), "Line")) == "更新完成 v4.12.0" &&
                Gd.Str(Gd.Get(D(g1), "Joined")) == "开头一句 / 已是最新 / 中间一句 / 更新完成 v4.12.0");

            object g2 = GateHost.M9aUpdgate(Inp("noSpawn", true, "fakeCode", 0, "fakeLines", new object[] { "啥也没中" }));
            b.Chk("挑不中时 Line 回空串而不是 null(链体那句 INFO 拼出来是空，与 PS 臂 [string]$null 同形)", () =>
                Gd.Get(D(g2), "Line") is string && Gd.Str(Gd.Get(D(g2), "Line")).Length == 0 &&
                Gd.Str(Gd.Get(D(g2), "Joined")) == "啥也没中");

            object g3 = GateHost.EventAudit(Inp(
                "noSpawn", true, "fakeCode", 0,
                "fakeLines", new object[] { "维持 event-pending", "审计结论 yes" }));
            b.Chk("审计臂吃自己那条正则(同一份行集里两条都命中时各挑自己那条的末行)", () =>
                Gd.Str(Gd.Get(D(g3), "Line")) == "审计结论 yes" &&
                Gd.Str(Gd.Get(D(g1), "Line")) != Gd.Str(Gd.Get(D(g3), "Line")));

            // 末位幻影空元素：读手逐行 AppendLine，尾部必然多一枚空元素；不剥 ⇒ joined 末尾多个 ' / '、
            // 与 PS 臂 $gOut 的枚数不同轴。中间与真正末尾的空行必须保留(那是另一种坏)。
            b.Chk("合流文本拆行：只剥末位那一枚幻影空元素，中间的与真正末尾的空行照旧保留", () =>
                GateHost.SplitLines("a\r\nb\r\n").Count == 2 &&
                GateHost.SplitLines("a\r\n\r\n").Count == 2 && GateHost.SplitLines("a\r\n\r\n")[1].Length == 0 &&
                GateHost.SplitLines("a\r\nb").Count == 2 && GateHost.SplitLines("").Count == 0);
        }

        // 刀⑧：取"本枚签名 → 下一枚签名"那一截源码(锚缩进函数体用)。切不到起点交空串，让那一格当场红。
        static string GateBody(string src, string from, string until)
        {
            int a = src.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = src.IndexOf(until, a, StringComparison.Ordinal);
            return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
        }

        // 剥注释只处理 // 两形(整行与行尾)：Supervisor.cs 里没有 /* */ 块注释，也没有跨行字符串，
        // 所以这一把尺的盲区是"字符串字面量里出现 //"——那会把后面真代码切掉，只会造成假绿不会造成假红；
        // 纯洁性这一格的正对照由 mutation 现拍(临时往代码面塞一枚 "ark" 字面量，必红)。
        static string StripLineComments(string src)
        {
            var sb = new System.Text.StringBuilder();
            foreach (string raw in src.Split('\n'))
            {
                int cut = raw.IndexOf("//", StringComparison.Ordinal);
                sb.Append(cut < 0 ? raw : raw.Substring(0, cut)).Append('\n');
            }
            return sb.ToString();
        }

        static int Occ(string hay, string ndl)
        {
            int n = 0, at = 0;
            while ((at = hay.IndexOf(ndl, at + 1, StringComparison.Ordinal)) >= 0) n++;
            return n;
        }
    }
}
