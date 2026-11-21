// tools/panel-cs/Supervisor.cs —— 批次 6：监督 agent 的纯判定段本体，从 lib\supervisor.ps1 搬来。
//
// 单一事实源纪律：自本批起 PS 侧的 Get-GdSupervisorEnabled/IntervalSec/QuietSec/MaxInterventions、
// Get-GdSupervisorState、Get-GdSupervisorAction、Get-GdSupervisorDetailLines —— 规则与话术只写 C# 这一份。
// 话术必须与 PS 原话逐字符一致：等值面原由 tools\rustcore\probe-sv-rust-golden.ps1 拿基线 commit(7b07650) 全字面对拍，
// 该门已于 2026-10-26 退役(甲档第九刀)，48 档搬进 Supervisor.SelfTest.cs 的 --svtest；有差异回修这里，禁止改期望、禁止放宽比对(docs\ps-migration-batch0.md §7)。
//
// 切分口径(批次 6 的边界)：
//   ⚠ 11-02 监督族刀③ 更正：下面三行是批次 6 当时的边界，此后两刀把它推翻了(留字只为认出"当初打算把活留在 PS")。
//   * 有副作用的那批(Collect/RequestScreen/Restart 与 Write/Read、Start/Stop、三个 Path 函数)自此全住本 assembly(Supervisor.Host.cs)，
//     lib\supervisor.ps1 与 tools\supervise.ps1 双双出仓 —— 那批"活"不再"留 PS 壳"；Send-GdNotice 于本批摘除(lib\common.ps1 侧零活调用点，verb 已承接)。
//   * test-modules 的 [sv] 块自此只留门外的静态锚，行为级断言随刀② 搬进 --svtest ——
//     尤其 -Evidence 这个名字不许缩短(旧注释：-Ev 撞 advanced function 公共参数别名的实测坑)。
//
// m3 纯洁性(11-02 监督族刀③ 起由本 assembly 的 --svtest 钉 Supervisor.cs 自己的代码面)：这里不许出现具体游戏名/助手名字面量(PS 壳两件已出仓)。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class Supervisor
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // PS 等值口径：`$x -ne $false` 不是 C# 的 x != false —— bool 看值、数字转成 0/1、
        // 字符串把 $false 换成 "False" 做大小写不敏感比较('False' 字符串在 PS 里就是关)。
        // 其余类型(数组/对象)现实中不出现在 supervisor 键上，退化成真值判断，注释在此不硬编规则。
        static bool NeFalse(object v)
        {
            if (v == null) return true;
            if (v is bool) return (bool)v;
            if (v is string) return !string.Equals((string)v, "False", StringComparison.OrdinalIgnoreCase);
            try { return Convert.ToDouble(v, Inv) != 0.0; } catch { return Gd.Truthy(v); }
        }

        // ==================== verb: eval-supervisor-settings(原四件开关取值) ====================
        // 输入是四个**原始 JSON 值**(缺键=null)，一次算全：Enabled=「没显式 false 就是开」；
        // IntervalSec <15 归 60(拍太快费日志)；QuietSec=分钟×60、下限 5 分钟；MaxInterventions 缺省 1、负数钳 0。
        // 已知局限：[int] 转换失败在旧 PS 里是当场 throw，这里按 0 收(随后走各自的钳制)——非法值不进对拍场景。

        internal static object Settings(Dictionary<string, object> inp)
        {
            bool enabled = Gd.Get(inp, "supervisor") == null || NeFalse(Gd.Get(inp, "supervisor"));

            int v = 0;
            object rawI = Gd.Get(inp, "intervalSeconds");
            if (Gd.Truthy(rawI)) v = Gd.Int(rawI, 0);
            if (v < 15) v = 60;

            int m = 30;
            object rawQ = Gd.Get(inp, "quietMinutes");
            if (Gd.Truthy(rawQ)) m = Gd.Int(rawQ, 0);
            if (m < 5) m = 5;

            int max;
            object rawM = Gd.Get(inp, "maxInterventions");
            if (rawM == null) max = 1;
            else { max = Gd.Int(rawM, 0); if (max < 0) max = 0; }

            return Gd.Out("Enabled", enabled, "IntervalSec", v, "QuietSec", m * 60, "MaxInterventions", max);
        }

        // ==================== verb: parse-supervisor-state(原 Get-GdSupervisorState) ====================
        // 规则心跳，零 token：done/dead/quiet/busy 四态。判据顺序是认真的(注释随旧实现搬来)：
        // * done 排最前 —— 完成线看到了，进程在不在都不重要(MAA 跑完可能自己退)。
        // * dead 只在"曾经见过助手活着、现在没了"时才判 —— 一轮开跑时助手还没被主流程拉起来，
        //   上来就判 dead 是假警报。
        // * quiet 要求**两本日志都**安静**严格大于**阈值 —— 单条任务链十分钟不写日志是已知常态，
        //   只看一本会天天误报；刚好踩在阈值上不算静止。
        // asst_age/daily_age 缺失按 [double]$null=0 收。

        internal static object State(Dictionary<string, object> inp)
        {
            object ev = Gd.Get(inp, "evidence");
            if (Gd.Truthy(Gd.Get(ev, "Done"))) return Gd.Out("State", "done");
            bool alive = Gd.Truthy(Gd.Get(ev, "AssistantAlive"));
            if (Gd.Truthy(Gd.Get(inp, "everAlive")) && !alive) return Gd.Out("State", "dead");
            int quiet = Gd.Int(Gd.Get(inp, "quietSec"), 1800);
            double a = ToD(Gd.Get(ev, "AsstAgeSec"));
            double d = ToD(Gd.Get(ev, "DailyAgeSec"));
            if (a > quiet && d > quiet) return Gd.Out("State", "quiet");
            return Gd.Out("State", "busy");
        }

        static double ToD(object v)
        {
            if (v == null) return 0.0;
            try { return Convert.ToDouble(v, Inv); } catch { return 0.0; }
        }

        // ==================== verb: resolve-supervisor-action(原 Get-GdSupervisorAction 决策表) ====================
        // 干预决策表：quiet 要**连续两拍**才算数(一拍可能是日志轮转/时间抖动)；
        // 画面 blocking=true → 不干预，那是主流程兜底的活场；blocking=false(没弹窗却全线静止)
        // 且这一路允许重启且还有额度 → restart_assistant；其余异常一律 notify，
        // 且每种状态一轮只通知一次(调用方记 alreadyNotified)。
        // done/busy 之外的 state 一律按 quiet 流程走(照抄旧实现的 fall-through)；字符串比较按
        // PS -eq 的大小写不敏感口径。四句 Why 是话术本体，逐字禁改(golden 拍全文)。

        internal static object Action(Dictionary<string, object> inp)
        {
            string state = Gd.Str(Gd.Get(inp, "state"));
            if (string.Equals(state, "done", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "busy", StringComparison.OrdinalIgnoreCase))
                return Gd.Out("Kind", "none", "Why", "");
            bool notified = Gd.Truthy(Gd.Get(inp, "alreadyNotified"));
            if (string.Equals(state, "dead", StringComparison.OrdinalIgnoreCase))
            {
                if (notified) return Gd.Out("Kind", "none", "Why", "");
                return Gd.Out("Kind", "notify", "Why", "助手进程不见了，而这一轮还没宣布完成");
            }
            if (Gd.Int(Gd.Get(inp, "quietStreak"), 0) < 2) return Gd.Out("Kind", "none", "Why", "");
            object scr = Gd.Get(inp, "screen");
            if (scr != null && Gd.Truthy(Gd.Get(scr, "Ok")))
            {
                if (Gd.Truthy(Gd.Get(scr, "Blocking")))
                    return Gd.Out("Kind", "none", "Why", "画面被「" + Gd.Str(Gd.Get(scr, "What")) + "」挡着，交给主流程兜底");
                if (Gd.Truthy(Gd.Get(inp, "canRestart")) &&
                    Gd.Int(Gd.Get(inp, "interventions"), 0) < Gd.Int(Gd.Get(inp, "maxInterventions"), 1))
                    return Gd.Out("Kind", "restart_assistant", "Why",
                        "连续静止且画面没有挡路的东西(" + Gd.Str(Gd.Get(scr, "What")) + ")，判定层没醒，重启助手");
                if (notified) return Gd.Out("Kind", "none", "Why", "");
                return Gd.Out("Kind", "notify", "Why", "连续静止、画面没有弹窗，但这一路不许自动重启(或额度用完)");
            }
            if (notified) return Gd.Out("Kind", "none", "Why", "");
            string why = "连续静止，且看不清画面";
            if (scr != null && Gd.Truthy(Gd.Get(scr, "Why"))) why += "(" + Gd.Str(Gd.Get(scr, "Why")) + ")";
            return Gd.Out("Kind", "notify", "Why", why);
        }

        // ==================== verb: compose-supervisor-detail(原 Get-GdSupervisorDetailLines) ====================
        // 把 supervision.json 里**已经算好**的东西念成给人看的展开文本。纯函数、一个字都不判：
        // state/action/screen/why 都是采集与判据那两层写进去的值，这里只按固定字段取值、按固定说法拼句子。
        // 日期戳(Get-Date 现场)由壳以 date 参数注入；行数刻意收敛(最近 MaxBeats 拍×每路一行+现值头+介入记录)。
        // 纪律：输出行不许含 '|'(面板协议分隔符，test-modules 钉着)；空串字段说"没问/none"，
        // asst_age=-1 说"读不到"；beats 被 30 拍上限挤掉时不许顺手 return 吞掉介入记录(旧实现注释原话)。
        // 已知局限：旧侧 beats/games 缺失时 @($null) 会多出一个空元素，这里按空集合收 —— 真实
        // supervision.json 的 beats/games 恒为数组(Write-GdSupervisorBeat 落盘形状)，场景不含该形状。

        internal static object Detail(Dictionary<string, object> inp)
        {
            var lines = new List<object>();
            object sv = Gd.Get(inp, "sv");
            if (sv == null) return Gd.Out("Lines", lines.ToArray());

            string stopped = Gd.Truthy(Gd.Get(sv, "stopped")) ? " · 收工 " + Gd.Str(Gd.Get(sv, "stopped")) : "";
            lines.Add("现值：PID " + Gd.Str(Gd.Get(sv, "pid")) + " · 盯的路线 " + Gd.Str(Gd.Get(sv, "games")) +
                      " · 上岗 " + Gd.Str(Gd.Get(sv, "started")) + " · 最后一拍 " + Gd.Str(Gd.Get(sv, "updated")) + stopped);
            lines.Add("流水：logs\\supervision-" + Gd.Str(Gd.Get(inp, "date")) + ".jsonl(一行一拍，留 7 天)");

            object[] beats = Gd.List(Gd.Get(sv, "beats"));
            if (beats.Length == 0)
            {
                // 只说"还没落下第一拍"，不许就此结束：拍被挤掉、介入记录还在。
                lines.Add("这一枚监督进程还没落下第一拍(起拍间隔默认 60 秒)。");
            }
            else
            {
                int mb = Gd.Int(Gd.Get(inp, "maxBeats"), 6);
                if (mb < 1) mb = 1;
                int shown = Math.Min(mb, beats.Length);
                lines.Add("最近 " + shown + " 拍(字段：状态 / 助手日志静默 / 进程 / 完成线 / 画面 / 动作)：");
                for (int i = beats.Length - shown; i < beats.Length; i++)
                {
                    object b = beats[i];
                    string ts = Gd.Str(Gd.Get(b, "ts"));
                    if (ts.Length >= 19) ts = ts.Substring(11, 8);
                    foreach (object g in Gd.List(Gd.Get(b, "games")))
                    {
                        string age = Gd.Str(Gd.Get(g, "asst_age"));
                        age = (age == "-1") ? "读不到" : age + "秒";
                        lines.Add("  " + ts + " " + Gd.Str(Gd.Get(g, "game")) + "=" + Gd.Str(Gd.Get(g, "state")) +
                                  " 助手日志静默 " + age +
                                  " 进程 " + (Gd.Truthy(Gd.Get(g, "alive")) ? "在" : "没") +
                                  " 完成线 " + (Gd.Truthy(Gd.Get(g, "done")) ? "见过" : "没有") +
                                  " 画面 " + (Gd.Truthy(Gd.Get(g, "screen")) ? Gd.Str(Gd.Get(g, "screen")) : "没问") +
                                  " 动作 " + (Gd.Truthy(Gd.Get(g, "action")) ? Gd.Str(Gd.Get(g, "action")) : "none") +
                                  (Gd.Truthy(Gd.Get(g, "why")) ? " 原因 " + Gd.Str(Gd.Get(g, "why")) : ""));
                    }
                }
            }

            object[] acts = Gd.List(Gd.Get(sv, "actions"));
            if (acts.Length > 0)
            {
                lines.Add("介入记录(" + acts.Length + " 次)：");
                foreach (object a in acts)
                    lines.Add("  " + Gd.Str(Gd.Get(a, "ts")) + " " + Gd.Str(Gd.Get(a, "game")) + " " + Gd.Str(Gd.Get(a, "kind")) +
                              " " + (Gd.Truthy(Gd.Get(a, "ok")) ? "成" : "没成") + " — " + Gd.Str(Gd.Get(a, "why")));
            }
            else
            {
                lines.Add("这一轮没有介入过(它唯一的干预是重启助手，重启过会记在这里)。");
            }
            return Gd.Out("Lines", lines.ToArray());
        }
    }
}
