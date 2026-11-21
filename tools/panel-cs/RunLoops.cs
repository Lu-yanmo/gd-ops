// tools/panel-cs/RunLoops.cs —— 批次 7/8/9：活段监控循环的"判据段+回灌段"本体
// (ark 一路 decide-maa-run-tick + m9a 一路 decide-m9a-run-tick + 批次 9 的事件回灌三件)。
// 口径全文见 docs\ps-migration-live-caliber.md(剧本注入回放 + 同 tick effect 迷你循环)。
// 基线真身 = lib\maa.ps1 Invoke-MaaRun 行 296-433 的判据链(钉 commit c498da0)。
// 文案必须与 PS 原话逐字符一致：panel-cs --looptest 拿基线做全字面对拍(原 live 门 10-26 摘件)，
// 有差异回修这里，禁止改期望、禁止放宽比对(docs\ps-migration-batch0.md §7)。
// 改判据/话术改本文件并跑 panel-cs --looptest，不要往 lib\maa.ps1 的壳里加规则。
//
// 一次 tick 的纯判据：读数快照(now/live/flags/cfg) + 四键小状态 → 动作 + 新状态。
// C# 绝不碰挂钟/盘/进程/模型 —— elapsedSec/quietSec/grabAgoSec 由壳算好当数喂进来；
// 副作用(抢前台/视觉兜底/救火记账)全以 effect 挂回壳执行，回执(effectsResolved)带回来在
// **同一 tick** 里续判(旧代码兜完当场决定 continue/return，隔一轮再判就不是逐字不变)。
// 判据顺序与 maa.ps1 逐档对应：
//   1 finished→tail  2 unknown-format  3 not-started  4 stuck(抢前台→兜底→落穿续判 / 60 秒仍原地→stalled)
//   5 drained(兜一次→无条件早退)  6 stallWarn 中止  7 timeout  8 quiet(兜成→重置计时续判；否则 stalled)
//   9 MAA 进程没了(收尾或 error)  10 allDone+静默→收尾；都没有→loop。
// tail(收尾)整段留壳：旧代码出循环后**重新取一次挂钟**算 $dur、Round($dur/60,1)、打 OK 日志 —— 挂钟量，拍不了也不该拍。
//
// 挂起点编码在 state.phase："" / stuck-grab / stuck-rescue / stuck-record /
// drain-rescue / drain-record / quiet-rescue / quiet-record。tag 前缀与 phase 不是一套词：
// 静默档的兜底调用在旧代码里就叫 "stall$N"，照抄。
// RecordTag 比调用 Tag 大 1 是旧代码的错位(调用取 Count+1、记账在 += 之后再取 Count+1，疑似笔误)：
// 按纪律原样复刻不顺手修，要修另开一批。
// Detail 是 tick 开头快照(旧代码 return 拼的是镜像段算好的 $detail 串，档内兜底新落的那条不进
// 当 tick 结论句)：旧 live 门的 drain-first 场景钉着(10-26 已搬进 --looptest)，实现见 Execute 的 tickDetail。
//
// PS 等值细节：hashtable 大小写不敏感 → completed/expectedChains 一律 OrdinalIgnoreCase；
// [int]$x 是银行家舍入 → 一律 Gd.Int(Convert.ToInt32 同口径)；[int]($quiet/60) 同理走 ToEven。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GdPanel
{
    static class RunLoops
    {
        // ==================== verb: decide-maa-run-tick ====================
        internal static object MaaTick(Dictionary<string, object> inp)
        {
            return new MaaTickRun(inp).Execute();
        }

        // 一次 tick 判定的全部输入/输出累加。写成小类只为把七八个桶收拢，没有任何外部状态。
        sealed class MaaTickRun
        {
            // —— 读数(now / live / flags / cfg)，全是壳采好的标量 ——
            double elapsed, quiet;
            bool hasGrabAgo; double grabAgo;
            bool finished, seenGame, drained, maaRunning, rescueAvail, grabAvail, abort;
            int markers, logChars, stuckExec, chanClicks;
            string stallWarn, asstHint, stuckTask, guiLogPath;
            Dictionary<string, object> completed;   // OrdinalIgnoreCase(PS hashtable 口径)
            List<string> chainKeys;                 // expectedChains 去重(保首个写法，对齐 $expectedCount)
            object[] errors;
            double startTimeout, stall, timeout, idleConfirm;
            object startTimeoutMinRaw, timeoutMinRaw, rescueMaxRaw;

            // —— C# 持有的四键小状态(壳 opaque 中继) ——
            string phase, grabNote;
            int rescueLeft;
            List<string> notes;
            // Detail 快照：旧代码每 tick 开头(基线 298-305 镜像段)算好 $detail，档内兜底新落的
            // $rc.Detail **不重算** —— drain/quiet 首兜当 tick 早退的结论句里没有"视觉兜底试过 N 次"。
            // 旧对拍门(10-26 摘件)的 drain-first 场景抓出的口径，已搬进 --looptest：快照在此，逐档复用。
            string tickDetail;

            // —— 本 tick 已执行 effect 的回执(旧→新追加) ——
            List<object> resolved;

            // —— 输出累加 ——
            List<object> effects = new List<object>();
            List<object> logs = new List<object>();
            bool grabNow, resetLastGrowth, clearStallWarn;
            string action = "loop";
            bool ret; string retStatus, retDetail; object[] retErrors;

            // 兜底回执合成出的 rc(与旧代码 catch 分支同款三字段)
            sealed class Rc { public bool Tried; public bool Done; public string Detail; }

            internal MaaTickRun(Dictionary<string, object> inp)
            {
                Dictionary<string, object> now = As(Gd.Get(inp, "now"));
                Dictionary<string, object> live = As(Gd.Get(inp, "live"));
                Dictionary<string, object> flags = As(Gd.Get(inp, "flags"));
                Dictionary<string, object> cfg = As(Gd.Get(inp, "cfg"));

                elapsed = Dbl(Gd.Get(now, "elapsedSec"));
                quiet = Dbl(Gd.Get(now, "quietSec"));
                object ga = Gd.Get(now, "grabAgoSec");
                // 对齐 $null -eq $chanGrabAt：没抢过时值是 null —— ConvertTo-Json 会把它落成 JSON null
                // 或直接省键，两种都得判成"还没抢"(键在但为 null 不算，批次 6 的裸开关教训反向用)。
                hasGrabAgo = ga != null && !(ga is string && ((string)ga).Length == 0);
                grabAgo = Dbl(ga);

                finished = Gd.Truthy(Gd.Get(flags, "finished"));
                seenGame = Gd.Truthy(Gd.Get(flags, "seenGame"));
                drained = Gd.Truthy(Gd.Get(flags, "drained"));
                maaRunning = Gd.Truthy(Gd.Get(live, "maaRunning"));
                rescueAvail = Gd.Truthy(Gd.Get(live, "rescueAvailable"));
                grabAvail = Gd.Truthy(Gd.Get(live, "grabAvailable"));
                markers = Gd.Int(Gd.Get(flags, "markers"), 0);
                logChars = Gd.Int(Gd.Get(flags, "logChars"), 0);
                stuckExec = Gd.Int(Gd.Get(flags, "stuckExec"), 0);
                chanClicks = Gd.Int(Gd.Get(flags, "chanClicks"), 0);
                stallWarn = Gd.Str(Gd.Get(flags, "stallWarn"));
                asstHint = Gd.Str(Gd.Get(flags, "asstHint"));
                stuckTask = Gd.Str(Gd.Get(flags, "stuckTask"));
                errors = Gd.List(Gd.Get(flags, "errors"));

                completed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, object> cm = Gd.Map(Gd.Get(flags, "completed"));
                if (cm != null) foreach (KeyValuePair<string, object> kv in cm) completed[kv.Key] = kv.Value;

                chainKeys = new List<string>();
                HashSet<string> seenChains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (object c in Gd.List(Gd.Get(flags, "expectedChains")))
                {
                    string s = Gd.Str(c);
                    if (seenChains.Add(s)) chainKeys.Add(s);
                }

                startTimeout = Dbl(Gd.Get(cfg, "startTimeoutSec"));
                timeout = Dbl(Gd.Get(cfg, "timeoutSec"));
                stall = Dbl(Gd.Get(cfg, "stallSec"));
                idleConfirm = Dbl(Gd.Get(cfg, "idleConfirmSec"));
                startTimeoutMinRaw = Gd.Get(cfg, "startTimeoutMin");
                timeoutMinRaw = Gd.Get(cfg, "timeoutMin");
                abort = Gd.Truthy(Gd.Get(cfg, "abortOnMaaStallWarning"));
                guiLogPath = Gd.Str(Gd.Get(cfg, "guiLogPath"));
                rescueMaxRaw = Gd.Get(cfg, "rescueMaxRaw");

                resolved = new List<object>(Gd.List(Gd.Get(inp, "effectsResolved")));

                Dictionary<string, object> st = Gd.Map(Gd.Get(inp, "state"));
                if (st == null)
                {
                    // $rescueLeft = [int]$S.runtime.visionMaxRescue; if ($rescueLeft -le 0) { $rescueLeft = 2 }
                    // 的初始化就发生在首轮(旧代码在循环外做一次，效果同一 tick 内不会重来)。
                    phase = ""; grabNote = ""; notes = new List<string>(); tickDetail = "";
                    rescueLeft = Gd.Int(rescueMaxRaw, 0);
                    if (rescueLeft <= 0) rescueLeft = 2;
                }
                else
                {
                    phase = Gd.Str(Gd.Get(st, "phase"));
                    grabNote = Gd.Str(Gd.Get(st, "grabNote"));
                    tickDetail = Gd.Str(Gd.Get(st, "tickDetail"));
                    rescueLeft = Gd.Int(Gd.Get(st, "rescueLeft"), 0);
                    notes = new List<string>();
                    foreach (object n in Gd.List(Gd.Get(st, "rescueNotes"))) notes.Add(Gd.Str(n));
                }
            }

            internal object Execute()
            {
                if (phase.Length > 0)
                {
                    // 真壳永远原样回传我们出的 state(含 tickDetail)；手写续判 state(--looptest/对拍门)可能没带 ——
                    // 此刻 notes 还是 state 带来的历史账、本 tick 无新落，补算与真快照同值。
                    if (tickDetail.Length == 0) tickDetail = DetailText();
                    Resume();
                }
                else
                {
                    // 新 tick：先按旧代码镜像段的口径把 $detail 快照定下(此时 notes 还是上 tick 的账)，
                    // 之后档内兜底落多少条都不回头改它 —— 挂进 state 供同 tick 重入续用。
                    tickDetail = DetailText();
                    Ladder(1);
                }
                return Emit();
            }

            // ---------- 挂起点续判：本 tick 内壳执行完 effect 带着回执重入 ----------
            void Resume()
            {
                if (phase == "stuck-grab")
                {
                    Dictionary<string, object> r = Last("grab");
                    if (r != null && Gd.Truthy(Gd.Get(r, "exception")))
                        grabNote = "抢前台抛异常：" + Gd.Str(Gd.Get(r, "message"));
                    else if (r != null) grabNote = Gd.Str(Gd.Get(r, "detail"));
                    LogStuckWarn();
                    phase = "";
                    if (StartRescue("stuck", WhyStuck())) return;
                    Ladder(5);   // 旧代码 if/elseif 之后不 return：stuck 首轮兜完照旧落穿跑后面的档
                    return;
                }
                if (phase == "stuck-rescue")
                {
                    Rc rc = TakeRescue();
                    notes.Add(rc.Detail);   // $rescues += $rc.Detail —— 记账的 Count+1 在这之后，错位由此来
                    AddRecord("stuck", rc);
                    phase = "stuck-record";
                    return;
                }
                if (phase == "stuck-record")
                {
                    LogRescueResult();
                    phase = "";
                    Ladder(5);
                    return;
                }
                if (phase == "drain-rescue")
                {
                    Rc rc = TakeRescue();
                    notes.Add(rc.Detail);
                    AddRecord("drain", rc);
                    phase = "drain-record";
                    return;
                }
                if (phase == "drain-record")
                {
                    LogRescueResult();
                    phase = "";
                    Ret("stalled", DrainSentence() + tickDetail, errors);   // 兜过兜成都立刻早退；Detail 用 tick 开头快照(基线 305 行为：本 tick 新落的兜底不进结论句)
                    return;
                }
                if (phase == "quiet-rescue")
                {
                    Rc rc = TakeRescue();
                    notes.Add(rc.Detail);
                    AddRecord("stall", rc);   // 静默档的调用 tag 在旧代码里就叫 stall，照抄
                    phase = "quiet-record";
                    return;
                }
                if (phase == "quiet-record")
                {
                    LogRescueResult();
                    phase = "";
                    if (LastRescueDone())
                    {
                        // 挡路的东西拿掉了，停滞计时从头算，给 MAA 机会继续；也不让它下轮被停滞告警提前判死。
                        resetLastGrowth = true; clearStallWarn = true;
                        return;   // Action=loop(旧代码是 continue，跳过本档后面两查，等价)
                    }
                    Ret("stalled", "MAA 日志 " + IntStr(Math.Round(quiet / 60.0, MidpointRounding.ToEven))
                        + " 分钟无输出，判定卡住。" + tickDetail, errors);   // 快照口径：本 tick 新落的那条不进结论句(基线 422 在 += 之后拼的仍是 305 的串)
                    return;
                }
                Ladder(1);   // 不认识的挂起点：按全新 tick 判(壳中继不该走到这)
            }

            // ---------- 判据梯 ----------
            void Ladder(int from)
            {
                if (from <= 1 && finished) { action = "tail"; return; }

                if (from <= 2 && markers == 0 && (elapsed > startTimeout || quiet > stall))
                {
                    string why = logChars == 0
                        ? "本轮没有读到任何新增日志内容(可能 MAA 换了日志路径或文件名)"
                        : "日志在涨(新增 " + logChars + " 字符)但一条认识的标志都没有(可能 MAA 改了文案)";
                    Ret("unknown-format",
                        "MAA " + why + " → 这轮结果不可信，不要按它判定理智是否清完。先看 status.ps1 的 MAA 段，再对着 "
                        + guiLogPath + " 更新 lib\\maa.ps1 的判定正则", errors);
                    return;
                }
                if (from <= 3 && !seenGame && completed.Count == 0 && elapsed > startTimeout)
                {
                    Ret("not-started",
                        IntStr(Gd.Int(startTimeoutMinRaw, 0))
                        + " 分钟内端游没被拉起、也没有任何链路完成 → 大概率是权限/自动登录/客户端路径问题(MAA 连端游需要管理员)",
                        errors);
                    return;
                }
                if (from <= 4 && stuckExec >= 60 && completed.Count == 0 && !finished)
                {
                    if (!hasGrabAgo)
                    {
                        // 4a 首次：旧代码第一件事就是 $chanGrabAt = Get-Date —— 抢不抢得动都要给这一轮记上起点。
                        grabNow = true;
                        if (grabAvail)
                        {
                            effects.Add(Gd.Out("Kind", "grab", "Tag", "", "Context", ""));
                            phase = "stuck-grab";
                            return;
                        }
                        grabNote = "这棵树上没有抢前台的函数，只能等停滞判定";
                        LogStuckWarn();
                        if (StartRescue("stuck", WhyStuck())) return;
                    }
                    else if (grabAgo > 60)
                    {
                        Ret("stalled",
                            "点击没被端游吃进去：" + stuckTask + " 已重复 " + stuckExec + " 次(整轮 " + chanClicks
                            + " 次点击)且一条链路都没完成，抢前台后 60 秒仍在原地。抢前台时看到的点击环境：" + grabNote
                            + "。" + tickDetail, errors);
                        return;
                    }
                }
                if (from <= 5 && drained && !allDone)
                {
                    if (StartRescue("drain",
                            "MAA 自己说这一轮跑完了但报错，只完成 " + DoneCount() + "/" + chainKeys.Count + " 条链路，它不会再自己动"))
                        return;
                    Ret("stalled", DrainSentence() + tickDetail, errors);
                    return;
                }
                if (from <= 6 && stallWarn.Length > 0 && abort)
                {
                    List<object> errs = new List<object>();
                    errs.Add(stallWarn);
                    errs.AddRange(errors);
                    Ret("stalled", "MAA 自身停滞告警(已触发)。" + tickDetail, errs.ToArray());
                    return;
                }
                if (from <= 7 && elapsed > timeout)
                {
                    Ret("timeout",
                        "超过 " + IntStr(Gd.Int(timeoutMinRaw, 0)) + " 分钟未完成。" + tickDetail, errors);
                    return;
                }
                if (from <= 8 && quiet > stall)
                {
                    if (StartRescue("stall", "")) return;
                    Ret("stalled", "MAA 日志 " + IntStr(Math.Round(quiet / 60.0, MidpointRounding.ToEven))
                        + " 分钟无输出，判定卡住。" + tickDetail, errors);
                    return;
                }
                if (from <= 9 && !maaRunning)
                {
                    if (finished || allDone) { action = "tail"; return; }
                    Ret("error", "MAA 进程提前退出。" + tickDetail, errors);
                    return;
                }
                if (from <= 10 && allDone && quiet > idleConfirm) { action = "tail"; return; }
                action = "loop";
            }

            // ---------- 兜底链的公共件 ----------
            bool StartRescue(string tagPrefix, string context)
            {
                if (rescueLeft > 0 && rescueAvail)
                {
                    rescueLeft--;
                    effects.Add(Gd.Out("Kind", "rescue", "Tag", tagPrefix + (notes.Count + 1), "Context", context));
                    phase = tagPrefix == "stuck" ? "stuck-rescue" : (tagPrefix == "drain" ? "drain-rescue" : "quiet-rescue");
                    return true;
                }
                return false;
            }

            Rc TakeRescue()
            {
                Rc rc = new Rc();
                Dictionary<string, object> r = Last("rescue");
                if (r != null && Gd.Truthy(Gd.Get(r, "exception")))
                {
                    // 旧代码 catch：Tried/$Done 都给 $false(哪怕确实动过手)—— 照抄。
                    rc.Tried = false; rc.Done = false;
                    rc.Detail = "兜底抛异常：" + Gd.Str(Gd.Get(r, "message"));
                }
                else if (r != null)
                {
                    rc.Tried = Gd.Truthy(Gd.Get(r, "tried"));
                    rc.Done = Gd.Truthy(Gd.Get(r, "done"));
                    rc.Detail = Gd.Str(Gd.Get(r, "detail"));
                }
                return rc;
            }

            void AddRecord(string tagPrefix, Rc rc)
            {
                // RecordTag 比调用 Tag 大 1：$rescues 已 += 过，再取 Count+1 —— 旧代码原样(疑似笔误，不顺手修)。
                effects.Add(Gd.Out(
                    "Kind", "record", "Tag", tagPrefix + (notes.Count + 1),
                    "Tried", rc.Tried, "Done", rc.Done, "Detail", rc.Detail,
                    "Context", ""));
                phase = tagPrefix == "stuck" ? "stuck-record" : (tagPrefix == "drain" ? "drain-record" : "quiet-record");
            }

            void LogRescueResult()
            {
                string last = notes.Count > 0 ? notes[notes.Count - 1] : "";
                logs.Add(Gd.Out("Level", LastRescueDone() ? "OK" : "WARN", "Text", "视觉兜底结果：" + last));
            }

            void LogStuckWarn()
            {
                logs.Add(Gd.Out("Level", "WARN", "Text",
                    "端游没把点击吃进去(" + stuckTask + " 已重复 " + stuckExec + " 次、整轮 " + chanClicks + " 次点击)：" + grabNote));
            }

            string WhyStuck()
            {
                return "自动化在同一个子任务[" + stuckTask + "]上重复点了 " + stuckExec + " 次，一条链路都没完成";
            }

            string DrainSentence()
            {
                return "MAA 队列已跑空但只完成 " + DoneCount() + "/" + chainKeys.Count
                    + " 条链路，它不会再自己动 → 不等静默判定，当场兜底过一次就直接早退(外层会重试一次)。";
            }

            // ---------- $detail 拼串(四段，与旧代码逐字一致) ----------
            bool allDone { get { return DoneCount() >= chainKeys.Count && chainKeys.Count > 0; } }

            string DetailText()
            {
                string d = "完成链路 " + DoneCount() + "/" + chainKeys.Count;
                string chainText = string.Join(", ", completed.Keys.OrderBy(k => k, StringComparer.Ordinal));
                if (chainText.Length > 0) d += "：" + chainText;
                if (asstHint.Length > 0) d += "；" + asstHint;
                if (notes.Count > 0) d += "；视觉兜底试过 " + notes.Count + " 次，最后一次：" + notes[notes.Count - 1];
                return d;
            }

            int DoneCount()
            {
                // $doneCount += [Math]::Min([int]$completed[$k], [int]$expectedCount[$k]) —— 缺失键 [int]$null=0。
                int sum = 0;
                foreach (string k in chainKeys)
                {
                    object v;
                    int have = completed.TryGetValue(k, out v) ? Gd.Int(v, 0) : 0;
                    sum += Math.Min(have, 1);   // expectedCount 每条恒 1(旧代码逐字)
                }
                return sum;
            }

            // ---------- 回执工具：链每步只追加一条回执，按 kind 从尾部找自己那一条 ----------
            Dictionary<string, object> Last(string kind)
            {
                for (int i = resolved.Count - 1; i >= 0; i--)
                {
                    Dictionary<string, object> r = Gd.Map(resolved[i]);
                    if (r != null && Gd.Str(Gd.Get(r, "kind")) == kind) return r;
                }
                return null;
            }

            bool LastRescueDone()
            {
                Dictionary<string, object> r = Last("rescue");
                return r != null && !Gd.Truthy(Gd.Get(r, "exception")) && Gd.Truthy(Gd.Get(r, "done"));
            }

            // ---------- 出参 ----------
            void Ret(string status, string detail, object[] errs)
            {
                action = "return"; ret = true; retStatus = status; retDetail = detail; retErrors = errs;
            }

            object Emit()
            {
                Dictionary<string, object> d = Gd.Out(
                    "Action", action,
                    "Effects", effects.ToArray(),
                    "Directives", Gd.Out(
                        "GrabNow", grabNow, "ResetLastGrowth", resetLastGrowth, "ClearStallWarn", clearStallWarn),
                    "Logs", logs.ToArray(),
                    "State", Gd.Out(
                        "phase", phase, "rescueLeft", rescueLeft,
                        "rescueNotes", notes.ToArray(), "grabNote", grabNote,
                        "tickDetail", tickDetail));
                if (ret)
                {
                    d["Status"] = retStatus;
                    d["Detail"] = retDetail;
                    d["Errors"] = retErrors ?? new object[0];
                }
                return d;
            }

            // ---------- 输入小工具 ----------
            static Dictionary<string, object> As(object v)
            {
                Dictionary<string, object> d = Gd.Map(v);
                return d ?? new Dictionary<string, object>();
            }

            static double Dbl(object v)
            {
                if (v == null) return 0.0;
                try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return 0.0; }
            }

            static string IntStr(long l) { return l.ToString(CultureInfo.InvariantCulture); }
            static string IntStr(double d) { return ((long)d).ToString(CultureInfo.InvariantCulture); }
        }

        // ==================== verb: decide-m9a-run-tick(批次 8，m9a 一路) ====================
        // 基线真身 = lib\m9a.ps1 Invoke-M9ARun 行 350-388 的反应段(含 $askScreen 闭包 L325-337 的纯判定部分，
        // 钉 commit c498da0)。Resolve-M9AStatus 出结论的那一拍才调；while/sleep、读日志、adb 查进程、
        // 真兜底链与记账全在核(尺住在 Rust 第 19/30 节；PS 壳那两枚 Invoke-GdVisionRescue/Add-GdRescueRecord 已于第二十刀刀③ 出仓)。口径与 ark 一路同一份剧本
        // (docs\ps-migration-live-caliber.md)，对拍门 = panel-cs --looptest(原 live 门 10-26 摘件)。
        // 与 ark 一路的三处话术差别(都是旧代码原样，不许"顺手统一")：
        //   * 记账 Tag 与调用 Tag **同数**(m9a 的记账取 += 之后的 Notes.Count，没有 ark 那个错位一格)；
        //   * 兜底日志是 "视觉兜底(停滞|判因)：<detail>"，不是 ark 的 "视觉兜底结果："；
        //   * Notes 条目带 "[Why] " 前缀，收尾拼串 "；视觉兜底 N 次，最后一次：…"(ark 是 "试过 N 次")。
        // 状态名一律不改(daily-clear 的收尾与通知按状态名分流)；Rescued 只给 r1999-daily 的重试分流看。
        // 挂起点 state.phase："" / ask-rescue(等回执) / ask-record(等记账回执后落日志+定去留)。
        internal static object M9aTick(Dictionary<string, object> inp)
        {
            return new M9aTickRun(inp).Execute();
        }

        sealed class M9aTickRun
        {
            // —— 读数(壳采好的标量；C# 不碰挂钟/进程/模型) ——
            double elapsed;
            bool seenGame, rescueAvail;
            string status, detail;
            int tasksCount;
            object rescueMaxRaw;

            // —— C# 持有的小状态(壳 opaque 中继) ——
            string phase;
            int rescueLeft;
            List<string> notes;
            // 注意：首拍拼好的 "(游戏进程出现过)" 后缀要随 state.askDetail 中继续命 —— 旧代码 $res.Detail 是
            // 同一对象跨步在改，而续判拍壳喂回的 res.detail 是原串，不中继收尾拼串就会丢这一截(tick3 冒烟抓出)。
            // 取用只发生在 phase=ask-* 的挂起续判期；phase 归零后的新结论拍照用当拍 res.detail。见构造函数。

            // —— 本 tick 已执行 effect 的回执(旧→新追加) ——
            List<object> resolved;

            // —— 输出累加 ——
            List<object> effects = new List<object>();
            List<object> logs = new List<object>();
            bool resetLastLog;
            string action = "loop";
            bool ret; string retStatus, retDetail;
            bool rescued;
            bool hasDuration; double duration;

            // 兜底回执合成出的 rc(与旧代码闭包 catch 分支同款三字段)
            sealed class Rc { public bool Tried; public bool Done; public string Detail; }

            internal M9aTickRun(Dictionary<string, object> inp)
            {
                Dictionary<string, object> now = As(Gd.Get(inp, "now"));
                Dictionary<string, object> live = As(Gd.Get(inp, "live"));
                Dictionary<string, object> res = As(Gd.Get(inp, "res"));
                Dictionary<string, object> cfg = As(Gd.Get(inp, "cfg"));

                elapsed = Dbl(Gd.Get(now, "elapsedSec"));
                seenGame = Gd.Truthy(Gd.Get(live, "seenGame"));
                rescueAvail = Gd.Truthy(Gd.Get(live, "rescueAvailable"));
                status = Gd.Str(Gd.Get(res, "status"));
                detail = Gd.Str(Gd.Get(res, "detail"));
                tasksCount = Gd.Int(Gd.Get(res, "tasksCount"), 0);
                rescueMaxRaw = Gd.Get(cfg, "rescueMaxRaw");

                resolved = new List<object>(Gd.List(Gd.Get(inp, "effectsResolved")));

                Dictionary<string, object> st = Gd.Map(Gd.Get(inp, "state"));
                if (st == null)
                {
                    // $rescue = @{Left=[int]$S.runtime.visionMaxRescue;…}; if ($Left -le 0) { 2 } —— 首轮初始化留到这。
                    phase = ""; notes = new List<string>();
                    rescueLeft = Gd.Int(rescueMaxRaw, 0);
                    if (rescueLeft <= 0) rescueLeft = 2;
                }
                else
                {
                    phase = Gd.Str(Gd.Get(st, "phase"));
                    rescueLeft = Gd.Int(Gd.Get(st, "rescueLeft"), 0);
                    notes = new List<string>();
                    foreach (object n in Gd.List(Gd.Get(st, "rescueNotes"))) notes.Add(Gd.Str(n));
                    // 挂起续判期：工作 Detail 用 state 中继的那份(含首拍拼好的进程后缀)；键不在(手写 state)则退回当拍值。
                    if ((phase == "ask-rescue" || phase == "ask-record") && Gd.Get(st, "askDetail") != null)
                        detail = Gd.Str(Gd.Get(st, "askDetail"));
                }
            }

            internal object Execute()
            {
                if (phase == "ask-rescue") ResumeAsk();
                else if (phase == "ask-record") ResumeRecord();
                else Ladder();
                return Emit();
            }

            // ---------- 反应段首判(基线 350-368 + 闭包门槛 327-329) ----------
            void Ladder()
            {
                if (Eq(status, "ok"))
                {
                    // 挂钟量 elapsed 由壳喂进来；Round(x,1) 与 PS [math]::Round 同为 ToEven(批 8① rprobe 实测)。
                    logs.Add(Gd.Out("Level", "OK", "Text",
                        "M9A 链路完成，共 " + tasksCount + " 个任务，用时 " + MinStr(Math.Round(elapsed / 60.0, 1)) + " 分钟"));
                    hasDuration = true; duration = elapsed;
                    Finish();
                    return;
                }
                if (Eq(status, "empty-queue")) logs.Add(Gd.Out("Level", "ERROR", "Text", detail));
                else detail += seenGame ? "(游戏进程出现过)" : "(游戏进程从未出现)";
                if (InAskSet() && rescueLeft > 0 && rescueAvail)
                {
                    // 闭包门槛：Get-Command 没有 → $null；Left<=0 → $null；都等价于"不问、直接收尾"。
                    rescueLeft--;
                    effects.Add(Gd.Out("Kind", "ask-screen", "Tag", "m9a" + (notes.Count + 1),
                        "Why", WhyKind(), "Context", detail));
                    phase = "ask-rescue";
                    return;   // Action=loop：等回执同 tick 重入续判
                }
                Finish();
            }

            // ---------- 挂起点续判(壳执行完 effect 带着回执当拍重入) ----------
            void ResumeAsk()
            {
                Rc rc = TakeAsk();
                notes.Add("[" + WhyKind() + "] " + rc.Detail);
                // 记账 Tag 取 += 之后的 Count —— 与调用 Tag(notes.Count+1)数值相同：m9a 旧代码本就没有 ark 那个错位。
                effects.Add(Gd.Out("Kind", "record", "Tag", "m9a" + notes.Count,
                    "Tried", rc.Tried, "Done", rc.Done, "Detail", rc.Detail, "Context", ""));
                phase = "ask-record";
            }

            void ResumeRecord()
            {
                Rc rc = TakeAsk();
                logs.Add(Gd.Out("Level", rc.Done ? "OK" : "WARN", "Text", "视觉兜底(" + WhyKind() + ")：" + rc.Detail));
                phase = "";
                if (rc.Done)
                {
                    if (Alive())
                    {
                        // 挡路的东西拿掉了：停滞计时从头算，给 M9A 机会自己往下走 —— 本轮结论撤回(旧代码 continue)。
                        resetLastLog = true;
                        notes.Add("[停滞已解除，继续等待]");
                        return;   // Action=loop
                    }
                    // 进程都没了，点掉弹窗救不了这一轮，但外层那一次重试有了意义 —— 标记给 r1999-daily 的分流看。
                    rescued = true;
                }
                Finish();
            }

            // ---------- 收尾(基线 384-387：Notes 非空则拼进 Detail，状态一律不改) ----------
            void Finish()
            {
                if (notes.Count > 0)
                    detail += "；视觉兜底 " + notes.Count + " 次，最后一次：" + notes[notes.Count - 1];
                action = "return"; ret = true; retStatus = status; retDetail = detail;
            }

            // ---------- 状态集合与话术(PS -eq/-in 大小写不敏感 → OrdinalIgnoreCase) ----------
            static bool Eq(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
            bool Alive() { return Eq(status, "stalled") || Eq(status, "timeout"); }
            string WhyKind() { return Alive() ? "停滞" : "判因"; }
            bool InAskSet()
            {
                return Alive() || Eq(status, "blocked") || Eq(status, "not-started") || Eq(status, "empty-queue");
            }

            // ---------- 回执工具(与 ark 一路同款：链每步只追加一条，按 kind 从尾找) ----------
            Rc TakeAsk()
            {
                Rc rc = new Rc();
                Dictionary<string, object> r = Last("ask-screen");
                if (r != null && Gd.Truthy(Gd.Get(r, "exception")))
                {
                    // 旧代码闭包 catch：Tried/Done 都给 $false(哪怕确实动过手)—— 照抄。
                    rc.Tried = false; rc.Done = false;
                    rc.Detail = "兜底抛异常：" + Gd.Str(Gd.Get(r, "message"));
                }
                else if (r != null)
                {
                    rc.Tried = Gd.Truthy(Gd.Get(r, "tried"));
                    rc.Done = Gd.Truthy(Gd.Get(r, "done"));
                    rc.Detail = Gd.Str(Gd.Get(r, "detail"));
                }
                return rc;
            }

            Dictionary<string, object> Last(string kind)
            {
                for (int i = resolved.Count - 1; i >= 0; i--)
                {
                    Dictionary<string, object> r = Gd.Map(resolved[i]);
                    if (r != null && Gd.Str(Gd.Get(r, "kind")) == kind) return r;
                }
                return null;
            }

            // ---------- 出参 ----------
            object Emit()
            {
                Dictionary<string, object> d = Gd.Out(
                    "Action", action,
                    "Effects", effects.ToArray(),
                    "Directives", Gd.Out("ResetLastLog", resetLastLog),
                    "Logs", logs.ToArray(),
                    "State", Gd.Out("phase", phase, "rescueLeft", rescueLeft,
                        "rescueNotes", notes.ToArray(), "askDetail", detail));
                if (ret)
                {
                    d["Status"] = retStatus;
                    d["Detail"] = retDetail;
                    d["Rescued"] = rescued;
                    if (hasDuration) d["Duration"] = duration;
                }
                return d;
            }

            // ---------- 输入小工具(与 MaaTickRun 同款口径，各管各的) ----------
            static Dictionary<string, object> As(object v)
            {
                Dictionary<string, object> d = Gd.Map(v);
                return d ?? new Dictionary<string, object>();
            }

            static double Dbl(object v)
            {
                if (v == null) return 0.0;
                try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return 0.0; }
            }

            // PS "$([math]::Round($elapsed/60,1))"：double→串走 G15 最简形(2.0 打成 "2"、1.9 打成 "1.9")。
            static string MinStr(double d) { return d.ToString(CultureInfo.InvariantCulture); }
        }

        // ==================== verb: apply-maa-gui-events(批次 9，ark 一路回灌段) ====================
        // 基线真身 = lib\maa.ps1 Invoke-MaaRun 行 236-278 的 foreach($ev) 回灌 switch(钉 commit c498da0)。
        // 六档语义与旧代码逐字一致；$markers/$logChars 的 += 是纯数字透传，留在壳。
        // 状态中继：壳把 completed/benignSeen 摊平成 {completed:映射, benignPatterns:列表} 喂进来，
        // 回执后按同款形状重建(hashtable 大小写不敏感 → 这里一律 OrdinalIgnoreCase，键保首个写法)。
        // 话术细节：drained 档的 WARN 句里"任务已完成，但出现错误"这句匹配规则在 parse-maa-gui-lines
        // (批 5 已搬)，这里只负责认下 $drained；error 档满 40 条后**无声丢弃**(不打日志)。
        internal static object MaaGuiEvents(Dictionary<string, object> inp)
        {
            Dictionary<string, object> st = Obj(Gd.Get(inp, "state"));
            object[] evs = Gd.List(Gd.Get(inp, "events"));

            Dictionary<string, object> completed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object> cm = Gd.Map(Gd.Get(st, "completed"));
            if (cm != null) foreach (KeyValuePair<string, object> kv in cm) completed[kv.Key] = kv.Value;
            List<object> errors = new List<object>(Gd.List(Gd.Get(st, "errors")));
            HashSet<string> benign = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> benignList = new List<string>();
            foreach (object p in Gd.List(Gd.Get(st, "benignPatterns")))
                if (benign.Add(Gd.Str(p))) benignList.Add(Gd.Str(p));
            bool finished = Gd.Truthy(Gd.Get(st, "finished"));
            bool drained = Gd.Truthy(Gd.Get(st, "drained"));
            string stallWarn = Gd.Str(Gd.Get(st, "stallWarn"));
            List<object> logs = new List<object>();

            foreach (object o in evs)
            {
                Dictionary<string, object> ev = Obj(o);
                string kind = Gd.Str(Gd.Get(ev, "Kind"));
                if (Same(kind, "finished"))
                {
                    finished = true;
                    object used = Gd.Get(ev, "Used");
                    string text = "MAA 宣布任务全部完成";
                    if (Gd.Truthy(used)) text += "(内部计时 " + Gd.Str(used) + ")";
                    logs.Add(Gd.Out("Level", "OK", "Text", text));
                }
                else if (Same(kind, "chain"))
                {
                    // $completed[$chain]++：缺键从 $null 起跳得 1；键已存在(不同大小写)时 .NET 字典保首写法，与 PS 同形。
                    string chain = Gd.Str(Gd.Get(ev, "Chain"));
                    object old; completed.TryGetValue(chain, out old);
                    completed[chain] = Gd.Int(old, 0) + 1;
                    logs.Add(Gd.Out("Level", "INFO", "Text", "MAA 完成链路：" + chain));
                }
                else if (Same(kind, "stall"))
                {
                    stallWarn = Gd.Str(Gd.Get(ev, "Msg"));
                    logs.Add(Gd.Out("Level", "WARN", "Text", "MAA 停滞告警：" + stallWarn));
                }
                else if (Same(kind, "drained"))
                {
                    drained = true;
                    logs.Add(Gd.Out("Level", "WARN", "Text", "MAA 宣布本轮已跑完但有错误(队列已空，它不会再自己动)"));
                }
                else if (Same(kind, "error"))
                {
                    if (errors.Count < 40)
                    {
                        string msg = Gd.Str(Gd.Get(ev, "Msg"));
                        errors.Add(msg);
                        logs.Add(Gd.Out("Level", "WARN", "Text", "MAA 异常线索：" + msg));
                    }
                }
                else if (Same(kind, "benign"))
                {
                    // 去重看 Pattern、打日志用 Msg —— 旧代码就是这么分开的。
                    string pat = Gd.Str(Gd.Get(ev, "Pattern"));
                    if (benign.Add(pat))
                    {
                        benignList.Add(pat);
                        logs.Add(Gd.Out("Level", "INFO", "Text", "MAA 噪音(已按 benignMaaErrors 忽略)：" + Gd.Str(Gd.Get(ev, "Msg"))));
                    }
                }
            }

            Dictionary<string, object> outer = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> kv in completed) outer[kv.Key] = kv.Value;
            return Gd.Out(
                "State", Gd.Out(
                    "completed", outer,
                    "errors", errors.ToArray(),
                    "benignPatterns", benignList.ToArray(),
                    "finished", finished, "drained", drained, "stallWarn", stallWarn),
                "Logs", logs.ToArray());
        }

        // ==================== verb: finish-maa-run(批次 9，ark 一路挂钟收尾) ====================
        // 基线真身 = lib\maa.ps1 Invoke-MaaRun 行 510-512(钉 c498da0)：$dur 与 $(($completed.Keys | Sort-Object) -join ', ')
        // 都是挂钟/排序量，由壳算好喂进来(durSec=TotalSeconds，chainText=已排序拼接串)；
        // 这里只复刻纯拼串：Round(dur/60,1) 与 [math]::Round 同为 ToEven，double→串走 G15 最简形(12.0→"12")。
        internal static object MaaFinish(Dictionary<string, object> inp)
        {
            double dur = Num(Gd.Get(inp, "durSec"));
            string chainText = Gd.Str(Gd.Get(inp, "chainText"));
            string min = Math.Round(dur / 60.0, 1).ToString(CultureInfo.InvariantCulture);
            return Gd.Out(
                "Logs", new object[] { Gd.Out("Level", "OK", "Text", "MAA 日常链路完成，用时 " + min + " 分钟") },
                "Status", "ok",
                "Detail", "完成链路：" + chainText,
                "Duration", dur);
        }

        // ==================== verb: apply-m9a-events(批次 9，m9a 一路状态机段) ====================
        // 基线真身 = lib\m9a.ps1 Register-M9AEvent 行 162-231 的十三档 switch(钉 commit c498da0)。
        // $State.lastLog = Get-Date 是挂钟量：C# 只出 Directives.TouchLastLog，置时刻留在壳。
        // eventCount++ 在 switch 之前 —— 不认识的 Kind 也计数(但**不** touch lastLog)，unknown-format 判据吃它。
        // 三个在案语义坑(逐条按 PS 行为复刻，不许"顺手修")：
        //   1) tasks 键为 $null 时 @($State.tasks)+Text 得到 **[null,Text]**(Count=2) —— 占位 null 照落；
        //   2) failedTasks 走的是 $null -eq 比较：空数组与 $null 同真 → 两条分支都直接给 [@t](user 记忆 powershell-empty-array-collapse)；
        //   3) res-latest 的 -ne 是字符串**不区分大小写**比较 → OrdinalIgnoreCase。
        // 未变化的状态值一律原样回填(保 $null 与键写法)，别让壳把缺键读成空串。
        internal static object M9aEvents(Dictionary<string, object> inp)
        {
            Dictionary<string, object> st = Obj(Gd.Get(inp, "state"));
            object[] evs = Gd.List(Gd.Get(inp, "events"));

            int eventCount = (int)Gd.Int(Gd.Get(st, "eventCount"), 0);
            object chainStarted = Gd.Get(st, "chainStarted");
            object chainDone = Gd.Get(st, "chainDone");
            object connectPhase = Gd.Get(st, "connectPhase");
            object failed = Gd.Get(st, "failed");
            object queueCount = Gd.Get(st, "queueCount");
            object startRequested = Gd.Get(st, "startRequested");
            object resCurrentRaw = Gd.Get(st, "resCurrent");
            object resLatestRaw = Gd.Get(st, "resLatest");
            object connectFailRaw = Gd.Get(st, "connectFail");

            List<object> tasks = new List<object>();
            object tasksRaw = Gd.Get(st, "tasks");
            bool tasksNull = tasksRaw == null;
            if (!tasksNull) tasks.AddRange(Gd.List(tasksRaw));
            List<object> failedTasks = new List<object>();
            object ftRaw = Gd.Get(st, "failedTasks");
            bool ftNull = ftRaw == null;
            if (!ftNull) failedTasks.AddRange(Gd.List(ftRaw));

            bool touch = false;
            List<object> logs = new List<object>();

            foreach (object o in evs)
            {
                Dictionary<string, object> ev = Obj(o);
                eventCount++;   // 旧代码：先加计数，再进 switch(不认识的 Kind 也算"读进来了")
                string kind = Gd.Str(Gd.Get(ev, "Kind"));
                string text = Gd.Str(Gd.Get(ev, "Text"));
                if (Same(kind, "task"))
                {
                    chainStarted = true;
                    if (tasksNull) { tasks.Add(null); tasksNull = false; }   // @($null)+t 的占位元素，照落
                    tasks.Add(text);
                    logs.Add(Gd.Out("Level", "INFO", "Text", "M9A 进行中[" + tasks.Count + "] " + text));
                }
                else if (Same(kind, "chain-start")) chainStarted = true;
                else if (Same(kind, "chain-done")) chainDone = true;
                else if (Same(kind, "queue-prepared"))
                {
                    // [int]$Event.Text：坏值当场抛(旧行为)；日志吃的是原文 Text 不是转出来的数。
                    int q = Convert.ToInt32(text, CultureInfo.InvariantCulture);
                    queueCount = q;
                    if (q > 0) chainStarted = true;
                    logs.Add(Gd.Out("Level", "INFO", "Text", "M9A 已提交任务队列：任务数量=" + text));
                }
                else if (Same(kind, "ui-start")) startRequested = true;
                else if (Same(kind, "connect-start")) connectPhase = "started";
                else if (Same(kind, "finish"))
                {
                    // tasks 键为 $null 时 @($null).Count 是 1 —— 老代码真把这当成"看到过任务"，照抄。
                    int tc = tasksNull ? 1 : tasks.Count;
                    if (Gd.Truthy(chainStarted) || tc > 0) chainDone = true;
                    else connectPhase = "done";
                }
                else if (Same(kind, "fail")) { if (!Gd.Truthy(failed)) failed = text; }
                else if (Same(kind, "task-fail"))
                {
                    if (ftNull || failedTasks.Count == 0) { failedTasks.Clear(); failedTasks.Add(text); ftNull = false; }
                    else failedTasks.Add(text);
                    logs.Add(Gd.Out("Level", "WARN", "Text", "M9A 任务失败：" + text + "(累计 " + failedTasks.Count + " 个)"));
                    if (!Gd.Truthy(failed)) failed = "任务失败：" + text;
                }
                else if (Same(kind, "res-current")) resCurrentRaw = text;
                else if (Same(kind, "res-latest"))
                {
                    resLatestRaw = text;
                    string cur = Gd.Str(resCurrentRaw), latest = text;
                    if (Gd.Truthy(resCurrentRaw) && !Same(latest, cur))
                        logs.Add(Gd.Out("Level", "WARN", "Text",
                            "M9A 资源/程序有新版：当前 " + cur + " → 最新 " + latest + "(整包更新，命令行没有更新入口，需在 M9A 界面点『更新』)"));
                }
                else if (Same(kind, "connect-fail"))
                {
                    if (!Gd.Truthy(connectFailRaw))
                    {
                        connectFailRaw = text;
                        logs.Add(Gd.Out("Level", "WARN", "Text", "M9A 连不上模拟器：" + text));
                    }
                }
                else if (Same(kind, "noise")) { /* 只 touch lastLog，下面统一 */ }
                else continue;   // 不认识的 Kind：eventCount 已加，其余一概不碰
                touch = true;
            }

            return Gd.Out(
                "Directives", Gd.Out("TouchLastLog", touch),
                "Logs", logs.ToArray(),
                "State", Gd.Out(
                    // tasks/failedTasks 没被本批碰过且原值是 null → 回填 null 而不是 []：
                    // 旧代码 @($null).Count=1 的语义要靠"键仍是 $null"跨批中继才丢不干净(finish 档吃它)。
                    "tasks", tasksNull ? (object)null : tasks.ToArray(),
                    "chainStarted", chainStarted ?? (object)false,
                    "chainDone", chainDone ?? (object)false,
                    "connectPhase", connectPhase ?? (object)"none",
                    "failed", failed,
                    "queueCount", queueCount,
                    "startRequested", startRequested ?? (object)false,
                    "eventCount", eventCount,
                    "failedTasks", ftNull ? (object)null : failedTasks.ToArray(),
                    "resCurrent", resCurrentRaw,
                    "resLatest", resLatestRaw,
                    "connectFail", connectFailRaw));
        }

        // ---------- 批次 9 公共小件(与批 7/8 各类内私货同口径，函数级 verbs 共用这一份) ----------
        static Dictionary<string, object> Obj(object v)
        {
            Dictionary<string, object> d = Gd.Map(v);
            return d ?? new Dictionary<string, object>();
        }

        static double Num(object v)
        {
            if (v == null) return 0.0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return 0.0; }
        }

        static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }
}
