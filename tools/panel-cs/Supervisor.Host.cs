// tools/panel-cs/Supervisor.Host.cs —— 宿主批：Start/Stop-GdSupervisor 与心跳循环的 C# 直身(旧 PS 对面 lib\supervisor.ps1 + tools\supervise.ps1 已于 11-02 监督族刀③ 出仓，本件自此是唯一真身)。
//
// 与 PS 现形的形态差(都在这登记，回件同步)：
//   * 心跳住在一枚**分离的子进程**里，跑的是自己这枚 exe 的 supervise-loop。
//     宿主批第一版把它放在本进程一条后台线程上，而 CLI 的名册口径是"一次调用一个进程"——
//     调用方一退，那条 IsBackground 线程连同一个没人监督的读数一起消失，等于把监督静默接断。
//     照 Gate.Host.cs 头注「先把出口建出来再摘调用方」的先例补这一枚阻塞式自举回路；
//     调用方(lib\rust-round.ps1 的 round-supervisor-start 那一臂)本批**不改指**，仍在 PS 手里。
//   * 盘上形状回到 PS 旧版的"子 pid + 父 pid"：pid=子进程，owner=调用方给的 owner pid。
//     owner 没给或给了非正数 ⇒ 记 0，"owner 消失自退"那一臂不触发，只靠停拍标记与存活上限收工。
//   * Stop 全是进程语义(写停拍标记 → 等那枚 pid 消失 → 过宽限才强杀)；读到的 pid 恰是本进程时不强杀自己。
//   * 子进程自己从盘上读 settings.json(旧版子进程 dot 库重读同一份，同形)：调用方内存里改过的配置不往下传。
//   * supervise.ps1 缺失/没有可监督路线这两档：旧版是子进程起后 exit 3，这里 Start 侧回 Available=false、
//     loop 侧按 PS 的退出码面回 3(0=正常收工，3=起不来)。
//   * -Once 旁路(排查用)由 loop 侧的 --once 供上，Start 不认；owner 消失自退那一臂在 PS 旧版是子进程每 5 秒瞄一次，这里同尺。
//   * 落盘写手是 GdJson(单行 JSON、无 BOM)，PS 的 Write-GdJson/ConvertTo-Json 是缩进形态 —— 空白与 BOM 差异照 Round.Notice 的先例登记。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace GdPanel
{
    // csc 4.0(C# 5)表达不了 ValueTuple 语法 ⇒ 委托签名规定的 (bool Available, string Err) 用这个具名结果型承载，字段名逐字。
    public sealed class HostResult
    {
        public bool Available;
        public string Err;
    }

    public static class SupervisorHost
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static Dictionary<string, object> metaDoc;   // WriteBeat 每拍重刷的那份"现在"文档(pid/owner/started/games 定值)

        // 存活上限：旧版是 tools\supervise.ps1 的参数默认值，不进 runtime 开关面，这里同值同口径。
        // 公开给 Cli 的 --cap 兜底用：两处一份尺，免得改默认值时只改一半。
        public const int CapDefault = 480;

        // ==================== 公开面 ====================

        // 父侧(一次 CLI 调用)：算开关 → 停掉上一枚 → 起子进程 → 落一帧"现在"文档 → 立刻返回。
        // ownerPid 由调用方给(轮档那一臂就是跑整轮的那个进程)，子进程靠它做"owner 消失自退"那一臂。
        public static HostResult Start(Dictionary<string, object> S, string games, int ownerPid)
        {
            HostResult r = new HostResult();
            r.Available = false;
            r.Err = null;
            try
            {
                GdEnv.Init(null);
                if (S == null)
                {
                    List<string> missing;
                    S = GdSettings.Load(out missing, true, null);
                }
                Dictionary<string, object> set = SettingsOf(S);
                if (!Gd.Truthy(Gd.Get(set, "Enabled"))) return r;   // 开关关着：与旧版 return $false 同格，不算错

                // 上一枚还活着(旧轮硬退出没停干净)：先停掉它再起新的。外来 pid 强杀；恰是本进程时不动手。
                int selfPid = SelfPid();
                object prev = ReadSummary();
                int prevPid = prev == null ? 0 : Gd.Int(Gd.Get(prev, "pid"), 0);
                if (prev != null && prevPid > 0 && ProcessAlive(prevPid))
                {
                    GdLog.Line("上一枚监督进程(PID " + Gd.Str(Gd.Get(prev, "pid")) + ")还活着，先停掉它再起新的", "WARN");
                    if (prevPid != selfPid) KillQuiet(prevPid);
                }

                List<Route> routes = PickRoutes(S, games);
                if (routes.Count == 0)
                {
                    GdLog.Line("没有可监督的路线(Games=" + games + "，那几路没装——判据是助手库 lib\\maa.ps1／lib\\m9a.ps1 在不在；SupervisorEvidence 钩子已随 11-04 刀⑤ 出仓)", "ERROR");
                    return r;
                }
                int cap = CapDefault;

                Process child = SpawnLoop(games, ownerPid, cap);
                if (child == null)
                {
                    GdLog.Line("监督进程没起来(认不出自己这枚 exe 或 spawn 当场失败)(这一轮没人监督，主流程不受影响)", "WARN");
                    return r;
                }
                int childPid = child.Id;
                child.Dispose();
                try { GdJson.Write(StatePath(), NewMeta(childPid, ownerPid, games)); } catch { }
                QuietLine("监督 agent 上岗(PID " + childPid + "，每 " + Gd.Int(Gd.Get(set, "IntervalSec"), 60) + " 秒一拍，只在可疑时才问模型)", "INFO");
                r.Available = true;
                return r;
            }
            catch (Exception ex)
            {
                r.Err = ex.Message;
                r.Available = false;
                return r;
            }
        }

        // 子侧(退役前 supervise.ps1 心跳循环的阻塞式自举回路，那件 11-02 刀③ 出仓)：在自己的前台线程上跑到收工。
        // 不重问 Enabled —— 旧版子进程同样不查开关(开关只拦"要不要起")，已经起来了就拍到收工。
        // 返回退出码：0=正常收工(停拍标记 / owner 消失 / --once / 到达存活上限)，3=起不来(没配置 / 没有可监督的路线)。
        public static int Loop(int ownerPid, string games, bool once, int capMinutes)
        {
            GdEnv.Init(null);
            Dictionary<string, object> S;
            try
            {
                List<string> missing;
                S = GdSettings.Load(out missing, true, null);
            }
            catch (Exception ex)
            {
                GdLog.Line("配置读不到，监督不起：" + ex.Message, "ERROR");
                return 3;
            }
            Dictionary<string, object> set = SettingsOf(S);
            List<Route> routes = PickRoutes(S, games);
            if (routes.Count == 0)
            {
                GdLog.Line("没有可监督的路线(Games=" + games + "，那几路没装——判据是助手库 lib\\maa.ps1／lib\\m9a.ps1 在不在；SupervisorEvidence 钩子已随 11-04 刀⑤ 出仓)", "ERROR");
                return 3;
            }
            Session ss = new Session();
            ss.S = S;
            ss.Games = games;
            ss.Interval = Gd.Int(Gd.Get(set, "IntervalSec"), 60);
            ss.QuietSec = Gd.Int(Gd.Get(set, "QuietSec"), 1800);
            ss.MaxInt = Gd.Int(Gd.Get(set, "MaxInterventions"), 1);
            ss.Routes = routes;
            ss.OwnerPid = ownerPid;
            ss.Once = once;
            ss.CapMinutes = capMinutes;
            GdLog.Line("监督心跳开始(间隔 " + ss.Interval + "s，静止阈值 " + ss.QuietSec + "s，games=" + GameIds(routes) +
                       "，owner PID " + ownerPid + ")");
            GdJson.Write(StatePath(), NewMeta(SelfPid(), ownerPid, games));
            RunLoop(ss);
            // 自己退场前把 stopped 写回最终读数，面板念得出"几点下班"而不是"进程没了(崩?)"。
            // 死在循环外那一档走不到这里 —— 与旧版子进程在 while 里抛出后直接终止同形。
            MarkStopped();
            return 0;
        }

        public static HostResult Stop()
        {
            HostResult r = new HostResult();
            r.Available = false;
            r.Err = null;
            try
            {
                GdEnv.Init(null);
                object prev = ReadSummary();
                int prevPid = prev == null ? 0 : Gd.Int(Gd.Get(prev, "pid"), 0);
                if (prev == null || prevPid <= 0) return r;   // 与旧版 return $false 同格
                string stopFile = StopPath();
                try { File.WriteAllText(stopFile, NowText() + Environment.NewLine, new UTF8Encoding(false)); } catch { }

                int selfPid = SelfPid();
                if (prevPid != selfPid)
                {
                    DateTime deadline = DateTime.Now.AddSeconds(75);
                    while (DateTime.Now < deadline)
                    {
                        if (!ProcessAlive(prevPid)) break;
                        Thread.Sleep(1000);
                    }
                    // 宽限过了还没退(它卡在某一次采集里)才强杀；本进程除外，不许自杀。
                    if (ProcessAlive(prevPid) && prevPid != selfPid) { KillQuiet(prevPid); Thread.Sleep(300); }
                }
                try { if (File.Exists(stopFile)) File.Delete(stopFile); } catch { }
                MarkStopped();
                r.Available = true;
                return r;
            }
            catch (Exception ex)
            {
                r.Err = ex.Message;
                return r;
            }
        }

        // ==================== 起子进程的小件 ====================

        // 四枚开关一次算全(与 eval-supervisor-settings 同一把尺)，Start 与 Loop 各取自己要的那几格。
        static Dictionary<string, object> SettingsOf(Dictionary<string, object> S)
        {
            Dictionary<string, object> rt = Gd.Map(Gd.Get(S, "runtime"));
            return Gd.Map(Supervisor.Settings(Sub(
                "supervisor", Gd.Get(rt, "supervisor"),
                "intervalSeconds", Gd.Get(rt, "supervisorIntervalSeconds"),
                "quietMinutes", Gd.Get(rt, "supervisorQuietMinutes"),
                "maxInterventions", Gd.Get(rt, "supervisorMaxInterventions"))));
        }

        static Process SpawnLoop(string games, int ownerPid, int capMinutes)
        {
            string exe = SelfExePath();
            if (exe.Length == 0) return null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.Arguments = "supervise-loop --no-out --transcript --games=" + games +
                                " --owner=" + ownerPid.ToString(Inv) + " --cap=" + capMinutes.ToString(Inv);
                psi.WorkingDirectory = GdEnv.Root;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                return Process.Start(psi);
            }
            catch { return null; }
        }

        static string SelfExePath()
        {
            try { return Process.GetCurrentProcess().MainModule.FileName ?? ""; }
            catch { return ""; }
        }

        static int SelfPid()
        {
            try { return Process.GetCurrentProcess().Id; }
            catch { return 0; }
        }

        static string GameIds(List<Route> routes)
        {
            List<string> ids = new List<string>();
            foreach (Route r in routes) ids.Add(r.Id);
            return string.Join("+", ids.ToArray());
        }

        // ==================== 心跳循环(退役前 supervise.ps1 的本体，此处即唯一真身) ====================

        internal sealed class Route
        {
            public string Id;
            public bool CanRestart;
            public string VisionKind;    // "ark" / "mumu"
        }

        internal sealed class Session
        {
            public Dictionary<string, object> S;
            public string Games;
            public int Interval;
            public int QuietSec;
            public int MaxInt;
            public int OwnerPid;          // 调用方进程；<=0 时"owner 消失自退"那一臂不触发
            public bool Once;
            public int CapMinutes;
            public List<Route> Routes;
            public Dictionary<string, object[]> AsstOffset = new Dictionary<string, object[]>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, bool> TaskSeen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, bool> EverAlive = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, bool> Finished = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> Streaks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, DateTime> LastAsk = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, bool> Notified = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            public int Interventions;
            public List<object> Actions = new List<object>();
            public DateTime StartedAt = DateTime.Now;
        }

        static void RunLoop(Session ss)
        {
            // 循环体外没有兜底 catch：旧版子进程 ErrorActionPreference=Stop，死在循环外就是整枚退出( stopped 也不写回)，这里同形。
            Dictionary<string, object> S = ss.S;
            while (true)
            {
                string dailyLog = "";
                try
                {
                    Round.Lock lk = Round.LockAt(Path.Combine(GdEnv.State, "running.json"), false);
                    if (lk.Found) dailyLog = lk.Log;
                }
                catch { }

                List<object> perGame = new List<object>();
                foreach (Route m in ss.Routes)
                {
                    string id = m.Id;
                    string state = "busy"; string why = ""; string actionKind = "none"; string screenNote = "";
                    Dictionary<string, object> ev = null; Dictionary<string, object> screen = null;
                    try
                    {
                        Dictionary<string, object> spec = SpecOf(m, S);
                        ev = Collect(ss, m, spec, dailyLog);
                        if (Gd.Truthy(Gd.Get(ev, "AssistantAlive"))) ss.EverAlive[id] = true;
                        bool finished = Has(ss.Finished, id);
                        if (finished) state = "done";
                        else
                        {
                            Dictionary<string, object> st = Gd.Map(Supervisor.State(Sub(
                                "evidence", ev, "quietSec", ss.QuietSec, "everAlive", Has(ss.EverAlive, id))));
                            state = Gd.Str(st == null ? null : Gd.Get(st, "State"));
                            if (string.Equals(state, "done", StringComparison.OrdinalIgnoreCase)) { ss.Finished[id] = true; finished = true; }
                        }
                        int streak;
                        ss.Streaks.TryGetValue(id, out streak);
                        if (string.Equals(state, "quiet", StringComparison.OrdinalIgnoreCase)) streak = streak + 1; else streak = 0;
                        ss.Streaks[id] = streak;
                        // 复核只在"规则连续两拍说可疑"时发生，且同一路线两次问模型至少隔 5 分钟 —— 平时零 token。
                        if (string.Equals(state, "quiet", StringComparison.OrdinalIgnoreCase) && streak >= 2)
                        {
                            bool askNow = true;
                            DateTime la;
                            if (ss.LastAsk.TryGetValue(id, out la) && (DateTime.Now - la).TotalSeconds < 300) askNow = false;
                            if (askNow)
                            {
                                ss.LastAsk[id] = DateTime.Now;   // 标记在问之前落：问砸了也不许下一拍立刻再砸一次
                                screen = RequestScreen(S, m, id);
                            }
                        }
                        Dictionary<string, object> act = Gd.Map(Supervisor.Action(Sub(
                            "state", state,
                            "quietStreak", streak,
                            "screen", screen,
                            "canRestart", m.CanRestart,
                            "interventions", ss.Interventions,
                            "maxInterventions", ss.MaxInt,
                            "alreadyNotified", Has(ss.Notified, id + ":" + state))));
                        actionKind = Gd.Str(act == null ? null : Gd.Get(act, "Kind"));
                        why = Gd.Str(act == null ? null : Gd.Get(act, "Why"));
                        if (screen != null)
                            screenNote = Gd.Truthy(Gd.Get(screen, "Ok"))
                                ? (Gd.Truthy(Gd.Get(screen, "Blocking")) ? "blocking" : "clear")
                                : "ask-failed";
                        if (string.Equals(actionKind, "restart_assistant", StringComparison.OrdinalIgnoreCase))
                        {
                            bool ok = RestartAssistant(S, m);
                            ss.Interventions = ss.Interventions + 1;
                            ss.Actions.Add(Gd.Out("ts", DateTime.Now.ToString("HH:mm:ss", Inv), "game", id,
                                "kind", "restart_assistant", "ok", ok, "why", why));
                            ss.Streaks[id] = 0;
                            ss.LastAsk.Remove(id);
                            Round.Notice(Sub("title", "监督介入：" + id + "助手已重启", "text", why, "level", "WARN", "settings", S));
                        }
                        else if (string.Equals(actionKind, "notify", StringComparison.OrdinalIgnoreCase))
                        {
                            ss.Notified[id + ":" + state] = true;
                            ss.Actions.Add(Gd.Out("ts", DateTime.Now.ToString("HH:mm:ss", Inv), "game", id,
                                "kind", "notify", "ok", true, "why", why));
                            Round.Notice(Sub("title", "监督提醒：" + id + " 这一路不对劲", "text", state + " ： " + why, "level", "WARN", "settings", S));
                        }
                    }
                    catch (Exception ex)
                    {
                        // 采集这一拍没成：记 busy 太乐观、记 dead 太吓人，就记个带原因的 unknown，下一拍继续。
                        string w = ex.Message == null ? "" : ex.Message;
                        if (w.Length > 300) w = w.Substring(0, 300) + "…";
                        state = "unknown"; why = w;
                    }
                    bool aliveRow = ev != null && Gd.Truthy(Gd.Get(ev, "AssistantAlive"));
                    int ageRow = ev == null ? -1 : Gd.Int(Gd.Get(ev, "AsstAgeSec"), -1);
                    perGame.Add(Gd.Out(
                        "game", id,
                        "state", state,
                        "alive", aliveRow,
                        "asst_age", ageRow,
                        "done", Has(ss.Finished, id),
                        "screen", screenNote,
                        "action", actionKind,
                        "why", why));
                }

                try { WriteBeat(ss, perGame); }
                catch (Exception ex) { GdLog.Line("这一拍的记录没写进去：" + ex.Message, "WARN"); }

                if (ss.Once) { GdLog.Line("-Once：拍完这一拍就收工"); return; }
                if ((DateTime.Now - ss.StartedAt).TotalMinutes > ss.CapMinutes)
                {
                    GdLog.Line("到达存活上限 " + ss.CapMinutes + " 分钟，主动收工(主流程没走到收尾？去看它)", "WARN");
                    return;
                }
                // 睡眠切片：每 5 秒瞄一次停拍标记/owner，Stop 的宽限因此用不到一整拍。
                DateTime sleepUntil = DateTime.Now.AddSeconds(ss.Interval);
                while (DateTime.Now < sleepUntil)
                {
                    Thread.Sleep(5000);
                    string why2 = StopReason(ss.OwnerPid);
                    if (why2.Length > 0)
                    {
                        GdLog.Line(why2 == "stop" ? "收到停拍标记，监督收工" : "owner 进程消失了，监督下班");
                        return;
                    }
                }
            }
        }

        // Test-GdSupervisorStopRequested 的直身：'' = 继续，'stop' / 'owner' = 该下播。
        // owner <= 0(调用方没给)时那一臂不触发 —— Get-Process 查不到 pid 0 会当场抛，那是假"owner 消失"。
        static string StopReason(int ownerPid)
        {
            if (File.Exists(StopPath())) return "stop";
            if (ownerPid > 0 && !ProcessAlive(ownerPid)) return "owner";
            return "";
        }

        // ==================== 采集(Collect-GdSupervisorEvidence 的直身) ====================

        static Dictionary<string, object> SpecOf(Route m, Dictionary<string, object> S)
        {
            Dictionary<string, object> paths = Gd.Map(Gd.Get(S, "paths")) ?? new Dictionary<string, object>();
            if (m.Id == "ark")
            {
                string maaDir = Gd.Str(Gd.Get(paths, "maaDir"));
                string maaExe = Gd.Str(Gd.Get(paths, "maaExe"));
                return Sub(
                    "AssistantProc", FNOWE(maaExe),
                    "AssistantLog", GdEnv.JoinPath(maaDir, Gd.Str(Gd.Get(paths, "maaGuiLog"))),
                    "GameProc", FNOWE(Gd.Str(Gd.Get(paths, "arknightsExe"))),
                    "DonePattern", "任务已全部完成");
            }
            // M9A 一天一本 log-yyyyMMdd.log：AssistantLog 每次现算，跨天自动换本
            string m9aDir = Gd.Str(Gd.Get(paths, "m9aDir"));
            string logDir = GdEnv.JoinPath(m9aDir, Gd.Str(Gd.Get(paths, "m9aLogDir")));
            string daily = logDir.Length == 0 ? "" : Path.Combine(logDir, "log-" + DateTime.Now.ToString("yyyyMMdd", Inv) + ".log");
            return Sub(
                "AssistantProc", FNOWE(Gd.Str(Gd.Get(paths, "m9aExe"))),
                "AssistantLog", daily,
                "DonePattern", "启动任务完成|任务已全部完成",
                "ProgressPattern", "开始任务：|队列任务开始|用户操作：启动任务");
        }

        // 刀② 补尺开的三枚注入缝（--svtest 用；生产那一路一字未动，仍走下面这些默认臂）：
        //   * Collect 的进程表答法可由探针给 —— PS 侧那一路本来就是 function Test-GdProcessRunning 钉桩，同一形；
        //   * WriteBeat 的三枚路径可注入 —— 旧形硬编 GdEnv.State/Logs，自检要不碰现场盘只能把路径当入参收；
        //   * metaDoc 现值头可由 ResetMetaForTest 立 —— 记录两族格钉的是"拍落两个文件＋只留最近 30 拍"，得先有一份已知的头。
        // 缝只收路径与答法两枚入参，不把 Session/Route 的字段外露给自检件：内部形状一改就要改一次自检，那是把尺绑在墙上。
        internal delegate bool ProcAliveProbe(string name);

        static Dictionary<string, object> Collect(Session ss, Route m, Dictionary<string, object> spec, string dailyLogPath)
        {
            return Collect(ss, m, spec, dailyLogPath, NameRunning);
        }

        internal static Dictionary<string, object> Collect(Session ss, Route m, Dictionary<string, object> spec, string dailyLogPath, ProcAliveProbe alive)
        {
            string game = m.Id;
            string asstLog = Gd.Str(Gd.Get(spec, "AssistantLog"));
            double asstAge = 999999.0;
            string newText = "";
            if (asstLog.Length > 0 && File.Exists(asstLog))
            {
                try { asstAge = (DateTime.Now - File.GetLastWriteTime(asstLog)).TotalSeconds; } catch { }
                try
                {
                    // 偏移按"路径+字节数"成对存；首拍刻意不扫内容 —— 上一轮留在日志里的完成线不是这一轮的完成线。
                    object[] prevP;
                    ss.AsstOffset.TryGetValue(game, out prevP);
                    if (prevP != null && string.Equals(Gd.Str(prevP[0]), asstLog, StringComparison.OrdinalIgnoreCase))
                    {
                        object[] tail = GetTail(asstLog, Convert.ToInt64(prevP[1], Inv));
                        newText = (string)tail[0];
                        ss.AsstOffset[game] = new object[] { asstLog, tail[1] };
                    }
                    else
                    {
                        long len = 0;
                        try { len = new FileInfo(asstLog).Length; } catch { }
                        ss.AsstOffset[game] = new object[] { asstLog, len };
                    }
                }
                catch { }
            }
            double dailyAge = 0.0;
            if (dailyLogPath.Length > 0 && File.Exists(dailyLogPath))
            {
                try { dailyAge = (DateTime.Now - File.GetLastWriteTime(dailyLogPath)).TotalSeconds; } catch { }
            }
            bool done = false;
            string dp = Gd.Str(Gd.Get(spec, "DonePattern"));
            string pp = Gd.Str(Gd.Get(spec, "ProgressPattern"));
            if (newText.Length > 0 && dp.Length > 0)
            {
                if (pp.Length > 0)
                {
                    // 先吃进度线再认完成线，顺序不能反：同一拍里两类行可能一起涌进来
                    if (Regex.IsMatch(newText, pp, RegexOptions.IgnoreCase)) ss.TaskSeen[game] = true;
                    bool seen;
                    if (ss.TaskSeen.TryGetValue(game, out seen) && seen && Regex.IsMatch(newText, dp, RegexOptions.IgnoreCase)) done = true;
                }
                else if (Regex.IsMatch(newText, dp, RegexOptions.IgnoreCase)) done = true;
            }
            string gameProc = Gd.Str(Gd.Get(spec, "GameProc"));
            return Gd.Out(
                "AssistantAlive", alive(Gd.Str(Gd.Get(spec, "AssistantProc"))),
                "GameAlive", gameProc.Length > 0 && alive(gameProc),
                "AsstAgeSec", asstAge,
                "DailyAgeSec", dailyAge,
                "Done", done);
        }

        // Get-GdFileTail(common.ps1；⚠ 刀⑰ 笔(b) 该真身已出仓，坐标按历史引文读)的直身：偏移失配(日志轮转)回 0 重读，Offset 恒为读完时的文件尾。
        static object[] GetTail(string path, long offset)
        {
            if (!File.Exists(path)) return new object[] { "", (long)0 };
            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (offset > fs.Length) offset = 0;
                fs.Position = offset;
                string text = "";
                // 这里不套 using：内层 using 一Dispose 就把外层 fs 一起关了，读完再取 fs.Length 当场 ObjectDisposedException，
                // 而调用方那层 catch 把它咽掉 ⇒ 形状上"第二拍起读不到新行"，完成线永远认不出(11-02 刀② 补尺那两格第一次抓到)。
                StreamReader sr = new StreamReader(fs, Encoding.UTF8);
                text = sr.ReadToEnd();
                return new object[] { text, fs.Length };
            }
        }

        // ==================== 问画面(Request-GdSupervisorScreen 的直身) ====================
        // '这棵树上没有视觉兜底内核'/'这一路没声明兜底后端'两枚守卫是 Get-Command 探路时代的产物，
        // 编译直身里两枚后端恒在场，守卫不再可达(回件登记)。任何一步不对都回 Ok=false + 一句原因，绝不抛。

        static Dictionary<string, object> RequestScreen(Dictionary<string, object> S, Route m, string game)
        {
            Dictionary<string, object> rt = Gd.Map(Gd.Get(S, "runtime")) ?? new Dictionary<string, object>();
            Dictionary<string, object> paths = Gd.Map(Gd.Get(S, "paths")) ?? new Dictionary<string, object>();
            if (m.VisionKind == "ark")
            {
                if (!Round.FlagEnabled(Gd.Get(rt, "arkVisionRescue")))
                    return Gd.Out("Ok", false, "Why", "arkVisionRescue=false，视觉兜底已关闭");
                string gameProc = FNOWE(Gd.Str(Gd.Get(paths, "arknightsExe")));
                Dictionary<string, object> w = Gd.Map(Live.ArkFindWindow(Sub("procName", gameProc)));
                if (w == null || !Gd.Truthy(Gd.Get(w, "Found")))
                    return Gd.Out("Ok", false, "Why", "要看的窗口/虚拟机现在不在");
                return ShotAndAsk(S, m, game,
                    delegate(string shotPath) { return Live.WindowShot(Sub("path", shotPath, "procName", gameProc)); },
                    "");
            }
            if (!Round.FlagEnabled(Gd.Get(rt, "mumuVisionRescue")))
                return Gd.Out("Ok", false, "Why", "mumuVisionRescue=false，模拟器这一路的视觉兜底已关闭");
            Dictionary<string, object> ep0 = GateHost.MuMuEndpoint(S);
            string ep0a = Gd.Str(Gd.Get(ep0, "Adb")); string ep0s = Gd.Str(Gd.Get(ep0, "Serial"));
            if (ep0a.Length == 0 || ep0s.Length == 0 || !GateHost.MuMuVmAlive(ep0a, ep0s))
                return Gd.Out("Ok", false, "Why", "要看的窗口/虚拟机现在不在");
            // 端点每拍现查：虚拟机起没起、adb server 有没有被别的工具重启过，两次拍图之间真的会变。
            return ShotAndAsk(S, m, game,
                delegate(string shotPath)
                {
                    Dictionary<string, object> cur = GateHost.MuMuEndpoint(S);
                    string adb = Gd.Str(Gd.Get(cur, "Adb")); string serial = Gd.Str(Gd.Get(cur, "Serial"));
                    if (adb.Length == 0 || serial.Length == 0)
                        return Gd.Out("Ok", false, "Why", Gd.Str(Gd.Get(cur, "Why")));
                    return Live.MuMuShot(Sub("path", shotPath, "adb", adb, "serial", serial));
                },
                Live.MuMuScene);
        }

        // 返回型按 object 承：两枚后端的现出口(Live.WindowShot / Live.MuMuShot)交回来的就是 object，
        // 收口那一步本来就是 Gd.Map —— 声明成 Dictionary 等于在委托上多设一道它自己不会做的转换(csc 当场 CS0266)。
        delegate object ShotBuilder(string shotPath);

        static Dictionary<string, object> ShotAndAsk(Dictionary<string, object> S, Route m, string game, ShotBuilder shotBody, string scene)
        {
            string dir = Path.Combine(GdEnv.State, "rescue");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            string shotPath = Path.Combine(dir, "supervision-" + DateTime.Now.ToString("MMdd-HHmmss", Inv) + "-" + game + ".png");
            Dictionary<string, object> shot;
            try { shot = Gd.Map(shotBody(shotPath)); }
            catch (Exception ex) { return Gd.Out("Ok", false, "Why", "截图失败：" + ex.Message); }
            if (shot == null || !Gd.Truthy(Gd.Get(shot, "Ok")))
                return Gd.Out("Ok", false, "Why", "截图没成功：" + Gd.Str(Gd.Get(shot, "Why")));
            Dictionary<string, object> raw;
            try
            {
                raw = Gd.Map(Live.VisionAsk(Sub(
                    "imagePath", Gd.Str(Gd.Get(shot, "Path")),
                    "scene", scene,
                    "userText", "这是自动化正在跑的一路的当前画面。监督方注意到它很久没有进展，请判断画面上有没有挡住主流程的东西。按规则只回一个 JSON 对象。")));
            }
            catch (Exception ex) { return Gd.Out("Ok", false, "Why", "问模型失败：" + ex.Message); }
            if (raw == null || !Gd.Truthy(Gd.Get(raw, "Ok")))
                return Gd.Out("Ok", false, "Why", "问不成：" + Gd.Str(Gd.Get(raw, "Error")));
            Dictionary<string, object> v = Gd.Map(Gd.Get(raw, "Verdict"));
            if (v == null || !Gd.Truthy(Gd.Get(v, "Found")))
                return Gd.Out("Ok", false, "Why", "模型回了但解析不出 JSON");
            return Gd.Out("Ok", true, "Blocking", Gd.Truthy(Gd.Get(v, "Blocking")), "What", Gd.Str(Gd.Get(v, "What")));
        }

        // ==================== 重启(Restart-GdSupervisorAssistant + ark 那份 SupervisorRestart 钩子的直身；钩子本体已随 11-04 刀⑤ 出仓) ====================

        static bool RestartAssistant(Dictionary<string, object> S, Route m)
        {
            if (!m.CanRestart) return false;
            try
            {
                Dictionary<string, object> paths = Gd.Map(Gd.Get(S, "paths")) ?? new Dictionary<string, object>();
                string maaDir = Gd.Str(Gd.Get(paths, "maaDir"));
                Round.ProcTree(Sub("name", FNOWE(Gd.Str(Gd.Get(paths, "maaExe")))));
                ProcessStartInfo psi = new ProcessStartInfo(GdEnv.JoinPath(maaDir, Gd.Str(Gd.Get(paths, "maaExe"))));
                psi.WorkingDirectory = maaDir;
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }

        // ==================== 记录(Write/Read 的直身，键名行序照 PS 原文) ====================

        static void WriteBeat(Session ss, List<object> perGame)
        {
            WriteBeatTo(ss, perGame, StatePath(), BeatPath(), GdEnv.Logs);
        }

        internal static void WriteBeatTo(Session ss, List<object> perGame, string statePath, string beatPath, string logsDir)
        {
            Dictionary<string, object> beat = Gd.Out("ts", NowText(), "games", perGame.ToArray());
            try
            {
                File.AppendAllText(beatPath, GdJson.Serialize(beat) + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
            List<object> beats = new List<object>();
            object prev = ReadSummaryFrom(statePath);
            if (prev != null) foreach (object b in Gd.List(Gd.Get(prev, "beats"))) beats.Add(b);
            beats.Add(beat);
            if (beats.Count > 30) beats = new List<object>(beats.GetRange(beats.Count - 30, 30));
            Dictionary<string, object> doc = Gd.Out(
                "pid", Gd.Get(metaDoc, "pid"),
                "owner", Gd.Get(metaDoc, "owner"),
                "started", Gd.Get(metaDoc, "started"),
                "games", Gd.Get(metaDoc, "games"),
                "updated", Gd.Get(beat, "ts"),
                "beats", beats.ToArray(),
                "actions", ss.Actions.ToArray(),
                "stopped", "");
            metaDoc = doc;
            try { GdJson.Write(statePath, doc); } catch { }
            // 流水文件按天一个，留最近 7 天
            try
            {
                List<FileInfo> olds = new List<FileInfo>();
                foreach (string f in Directory.GetFiles(logsDir, "supervision-*.jsonl"))
                {
                    try { olds.Add(new FileInfo(f)); } catch { }
                }
                olds.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = 7; i < olds.Count; i++) { try { olds[i].Delete(); } catch { } }
            }
            catch { }
        }

        static object ReadSummary() { return ReadSummaryFrom(StatePath()); }

        internal static object ReadSummaryFrom(string statePath)
        {
            string p = statePath;
            if (!File.Exists(p)) return null;
            try
            {
                string txt = "";
                using (FileStream fs = File.Open(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                txt = sr.ReadToEnd();
                if (txt.Trim().Length == 0) return null;
                return GdJson.Deserialize(txt);
            }
            catch { return null; }
        }

        // 收工时把 stopped 写回最终读数(自退与 Stop 两条路同一份动作)：整读整写，已有键原位改值，键序不破。
        static void MarkStopped()
        {
            try
            {
                string p = StatePath();
                if (!File.Exists(p)) return;
                string txt = "";
                using (FileStream fs = File.Open(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                txt = sr.ReadToEnd();
                if (txt.Length == 0) return;
                Dictionary<string, object> doc = GdJson.Deserialize(txt) as Dictionary<string, object>;
                if (doc == null) return;
                doc["stopped"] = NowText();
                GdJson.Write(p, doc);
            }
            catch { }
        }

        // ==================== 路线名册(Get-GdModules -AvailableOnly + SupervisorEvidence 钩子的直身；钩子本体已随 11-04 刀⑤ 出仓，判据自此只剩助手库在不在) ====================
        // "在不在"的尺与 modules.ps1 同一口径：那一路的助手库文件在才算装了它；注册顺序=文件名序(ark 先)。

        static List<Route> PickRoutes(Dictionary<string, object> S, string games)
        {
            GdEnv.Init(null);
            List<string> want = new List<string>();
            foreach (string g in (games ?? "").Split('+'))
            {
                if (g.Length == 0) continue;
                if (string.Equals(g, "all", StringComparison.OrdinalIgnoreCase)) continue;
                want.Add(g);
            }
            List<Route> all = new List<Route>();
            if (File.Exists(Path.Combine(GdEnv.Root, @"lib\maa.ps1")))
                all.Add(NewRoute("ark", true, "ark"));
            if (File.Exists(Path.Combine(GdEnv.Root, @"lib\m9a.ps1")))
                all.Add(NewRoute("r1999", false, "mumu"));
            List<Route> picked = new List<Route>();
            foreach (Route r in all)
            {
                if (want.Count > 0 && !CiContains(want, r.Id)) continue;
                picked.Add(r);
            }
            return picked;
        }

        internal static Route NewRoute(string id, bool canRestart, string visionKind)
        {
            Route r = new Route();
            r.Id = id;
            r.CanRestart = canRestart;
            r.VisionKind = visionKind;
            return r;
        }

        static bool CiContains(List<string> list, string v)
        {
            foreach (string s in list) if (string.Equals(s, v, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ==================== 小件 ====================

        static Dictionary<string, object> NewMeta(int pid, int owner, string games)
        {
            metaDoc = Gd.Out(
                "pid", pid,
                "owner", owner,
                "started", NowText(),
                "games", games,
                "updated", "",
                "beats", new List<object>(),
                "actions", new List<object>(),
                "stopped", "");
            return metaDoc;
        }

        // 自检注入缝：记录两族格要一份已知的"现值头"才能钉 pid/拍数，生产那一路仍只在 Start/Loop 里立 NewMeta。
        internal static void ResetMetaForTest(int pid, int owner, string started, string games)
        {
            metaDoc = Gd.Out(
                "pid", pid,
                "owner", owner,
                "started", started,
                "games", games,
                "updated", "",
                "beats", new List<object>(),
                "actions", new List<object>(),
                "stopped", "");
        }

        static string StatePath() { GdEnv.Init(null); return Path.Combine(GdEnv.State, "supervision.json"); }
        static string StopPath() { GdEnv.Init(null); return Path.Combine(GdEnv.State, "supervisor.stop"); }
        static string BeatPath() { GdEnv.Init(null); return Path.Combine(GdEnv.Logs, "supervision-" + DateTime.Now.ToString("yyyyMMdd", Inv) + ".jsonl"); }
        static string NowText() { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv); }
        static string FNOWE(string rel) { if (string.IsNullOrEmpty(rel)) return ""; try { return Path.GetFileNameWithoutExtension(rel); } catch { return ""; } }

        static bool Has(Dictionary<string, bool> d, string k)
        {
            bool v;
            return d != null && d.TryGetValue(k, out v) && v;
        }

        static bool NameRunning(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                Process[] ps = Process.GetProcessesByName(name);
                foreach (Process p in ps) { try { p.Dispose(); } catch { } }
                return ps.Length > 0;
            }
            catch { return false; }
        }

        static bool ProcessAlive(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid)) { return !p.HasExited; }
            }
            catch { return false; }
        }

        static void KillQuiet(int pid)
        {
            try { using (Process p = Process.GetProcessById(pid)) p.Kill(); } catch { }
        }

        // Write-GdLog -Quiet 的直身：只落文件不上屏(上岗那句是 -Quiet 的，旧版逐字如此)。
        static void QuietLine(string msg, string level)
        {
            if (string.IsNullOrEmpty(GdLog.LogFile)) return;
            try
            {
                DateTime now = DateTime.Now;
                string line = "[" + now.ToString("HH:mm:ss", Inv) + "][" + level + "] " + msg;
                File.AppendAllText(GdLog.LogFile,
                    "[" + now.ToString("yyyy-MM-dd HH:mm:ss", Inv) + "]" + line + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }
        }

        static Dictionary<string, object> Sub(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }
    }
}
