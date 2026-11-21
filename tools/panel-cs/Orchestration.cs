// tools/panel-cs/Orchestration.cs —— 批次 5：编排/排程层的"纯规则段"本体，
// 从 lib\ark-farm.ps1 / lib\ark-daily.ps1 / lib\r1999-daily.ps1 / lib\maa.ps1 / run-via-task.ps1 搬来。
// 批次 8 追加：两路 daily 的重试分流纯判定段 decide-daily-retry(见文件尾)。
//
// 单一事实源纪律：自本批起 PS 侧的 Parse-GdArkFarmTargetSpec / Parse-GdArkDropText / Resolve-ArkFarmPlan、
// Resolve-GdArkDecision 的意图构造段(-Stage/-Force/farm 合算)、Resolve-GdR1999Decision 的 -Plan/-Force 构造段、
// Invoke-MaaRun 的每行标志分类段、Get-GdTaskArgsWithGames 的剥参切词段 —— 规则只写 C# 这一份。
// 文案必须与 PS 原话逐字符一致：等值面原由 tools\probe-orch-golden.ps1 拿基线 commit(a2fdd67)全字面对拍，
// 该门已于 2026-10-26 退役(甲档第九刀)，场景搬进本目录 SelfTest 的 --orchtest；有差异回修这里，禁止改期望、禁止放宽比对(docs\ps-migration-batch0.md §7)。
//
// 切分口径(批次 5 的边界，全部有断言面或副作用为证)：
//   * 两路 Start-Gd*Daily 与两个监控循环(Invoke-MaaRun / Invoke-M9ARun)整体留 PS：
//     Start-Process / Start-Sleep 轮询 / 文件续读 / 视觉兜底 / adb 都是"活段"，无头拍不了；
//     且 test-ark／test-r1999(行号都不引；10-24 刀②／刀③ 把"重试跳过清单"那两格的桌子从两份 -daily 壳搬到本件的
//     DailyRetry 两支，取证见 docs/rust-core-plan.md §11.128 ③)是**文本断言**，钉的正是在这两支里的状态名字面(ark 三枚／r1999 五枚，一枚不许少)与 maa.ps1 的 `Status = 'unknown-format'` 字面 —— 断言一字不改是批次纪律。
//   * daily-clear.ps1 只是参数外壳；真编排那十段现在**只住核内**(gdcore --daily-round)——PS 原身 Invoke-GdDailyRound
//     已于 2026-10-20 乙批② 从 lib\modules.ps1 划掉，那份十段的基线从此只剩轮骨架门钉住的 git blob(§11.104／§11.105)；
//     install-task.ps1 主体是 Register-ScheduledTask 现场 cmdlet，纯段只剩任务名格式化，不值单开 verb。
//   * supervisor / Send-GdNotice 归批次 6。
//   * "无结论"照批次 2/3/4 协议编码成 Found:false，PS 壳翻回 $null。
//
// 数字读回 Int32/Decimal 混发(批次 0 钉死)——这里一律 Gd.Str/Int 与 Convert.*，不裸强转。
// 已知局限(留档，不"顺手修")：Resolve-ArkFarmPlan 的挑关排序在**两键全并列**时，旧 PS 侧依赖 hashtable
// 枚举序(pwsh7 按进程加盐，本就不可押)——golden 场景必须避开全并列输入，这不是新实现的漂移。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class Orchestration
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ==================== verb: parse-farm-target-spec(原 Parse-GdArkFarmTargetSpec) ====================
        // 一行目标清单 → {Key;Target;Stages;Raw;Bad}。吃 "D32钢=30"、"D32钢=30@CE-5"、"30135=30@CE-5,CE-4"
        // 三种串写法，也吃 json 对象 { item/target/stages }；解析不动的给 Bad 说明，调用方汇总着说，不静默丢。

        // PS 的 `-split '[,，、;；\s]+' | Where-Object { $_ }`：切完去空串
        static object[] SplitStages(string s)
        {
            string[] parts = Regex.Split(s, @"[,，、;；\s]+", RegexOptions.None, TimeSpan.FromSeconds(5));
            List<object> outA = new List<object>();
            foreach (string p in parts) if (p.Length > 0) outA.Add(p);
            return outA.ToArray();
        }

        internal static object FarmTargetSpec(Dictionary<string, object> inp)
        {
            object raw = Gd.Get(inp, "raw");
            if (raw == null) return Gd.Out("Found", false);
            if (raw is string)
            {
                string t = ((string)raw).Trim();
                if (t.Length == 0) return Gd.Out("Found", false);
                Match m = Regex.Match(t, @"^(?<k>.+?)\s*[=＝]\s*(?<n>\d+)(?:\s*@\s*(?<s>.+))?$", RegexOptions.None, TimeSpan.FromSeconds(5));
                if (m.Success)
                    return Gd.Out("Found", true, "Spec", Gd.Out(
                        "Key", m.Groups["k"].Value.Trim(),
                        "Target", int.Parse(m.Groups["n"].Value, Inv),
                        "Stages", m.Groups["s"].Success ? SplitStages(m.Groups["s"].Value) : new object[0],
                        "Raw", t,
                        "Bad", ""));
                return Gd.Out("Found", true, "Spec", Gd.Out(
                    "Key", "", "Target", 0, "Stages", new object[0], "Raw", t,
                    "Bad", "看不懂「" + t + "」，要写成 材料名=数量[@关卡1,关卡2]"));
            }
            Dictionary<string, object> obj = Gd.Map(raw);
            if (obj == null) return Gd.Out("Found", false);   // 既不是串也不是对象：旧 PS 走到这里也会在属性读上吃灰，壳侧只会喂这两种
            string k = Gd.Str(Gd.Get(obj, "item"));
            if (k.Length == 0) k = Gd.Str(Gd.Get(obj, "name"));
            int n = 0; int tmp;
            if (int.TryParse(Gd.Str(Gd.Get(obj, "target")), NumberStyles.Integer, Inv, out tmp)) n = tmp;
            if (n == 0 && int.TryParse(Gd.Str(Gd.Get(obj, "count")), NumberStyles.Integer, Inv, out tmp)) n = tmp;
            object[] s = Gd.List(Gd.Get(obj, "stages"));
            if (s.Length == 1 && s[0] is string) s = SplitStages((string)s[0]);
            if (k.Length == 0)
                return Gd.Out("Found", true, "Spec", Gd.Out(
                    "Key", "", "Target", 0, "Stages", new object[0], "Raw", CompressJson(raw),
                    "Bad", "条目里没有材料名/代号(item)"));
            if (n <= 0)
                return Gd.Out("Found", true, "Spec", Gd.Out(
                    "Key", k, "Target", 0, "Stages", s, "Raw", k,
                    "Bad", k + " 的目标数量没写或不是正整数"));
            return Gd.Out("Found", true, "Spec", Gd.Out(
                "Key", k, "Target", n, "Stages", s, "Raw", k + "=" + n, "Bad", ""));
        }

        // 对齐 ConvertTo-Json -Compress 的最小口径：键按枚举序、无空格、串按 JSON 基本转义(中文原样)。
        // 已知两代 PS 对 ' < > & 的 \u 转义不同 —— golden 场景避开这些字符，不在这里押平。
        static string CompressJson(object v)
        {
            StringBuilder sb = new StringBuilder();
            WriteJson(sb, v);
            return sb.ToString();
        }

        static void WriteJson(StringBuilder sb, object v)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is string) { Esc(sb, (string)v); return; }
            if (v is IDictionary<string, object>)
            {
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, object> kv in (IDictionary<string, object>)v)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    Esc(sb, kv.Key); sb.Append(':'); WriteJson(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            if (v is object[])
            {
                sb.Append('[');
                object[] a = (object[])v;
                for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); WriteJson(sb, a[i]); }
                sb.Append(']');
                return;
            }
            if (v is double || v is float || v is decimal || v is int || v is long)
            { sb.Append(Convert.ToString(v, Inv)); return; }
            Esc(sb, Gd.Str(v));
        }

        static void Esc(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') { sb.Append('\\').Append(c); }
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", Inv));
                else sb.Append(c);
            }
            sb.Append('"');
        }

        // ==================== verb: parse-ark-drop-text(原 Parse-GdArkDropText) ====================
        // gui.log 片段 → 掉落块数组 [{Stage; Items=[{Name;Delta}]}]。只认 (+N) 的增量记账；
        // "当前次数/理智/用时"这类同名不同物的行靠拒绝表挡掉。块头只按「<代号> 掉落统计」抓关卡代号。

        static readonly string[] DropRejects = new string[] { "当前次数", "理智", "剩余理智", "用时", "预计", "累计理智" };

        internal static object FarmDropText(Dictionary<string, object> inp)
        {
            string text = Gd.Str(Gd.Get(inp, "text"));
            List<object> blocks = new List<object>();
            if (text.Trim().Length == 0) return Gd.Out("Blocks", blocks.ToArray());
            Dictionary<string, object> cur = null;
            foreach (string rawLine in Regex.Split(text, @"\r?\n"))
            {
                string s = rawLine.Trim();
                if (s.Length == 0) continue;
                s = Regex.Replace(s, @"^\[[^\]]*\]\s*", "", RegexOptions.None, TimeSpan.FromSeconds(5));
                s = Regex.Replace(s, @"^<\d+>\s*", "", RegexOptions.None, TimeSpan.FromSeconds(5));
                Match head = Regex.Match(s, @"([A-Za-z0-9][A-Za-z0-9\-]*)\s*掉落统计", RegexOptions.None, TimeSpan.FromSeconds(5));
                if (head.Success)
                {
                    cur = Gd.Out("Stage", head.Groups[1].Value, "Items", new List<object>());
                    blocks.Add(cur);
                    continue;
                }
                if (cur == null) continue;
                MatchCollection pairs = Regex.Matches(s,
                    @"([^\s:：,，、;()（）+]+)\s*[::]\s*(\d+)(?:\s*[（(]\s*\+(\d+)\s*[）)])?", RegexOptions.None, TimeSpan.FromSeconds(5));
                if (pairs.Count == 0) { cur = null; continue; }   // 掉落块是连续的，出现不像配额的行就算块结束了
                List<object> items = (List<object>)cur["Items"];
                foreach (Match m in pairs)
                {
                    string nm = m.Groups[1].Value;
                    bool reject = false;
                    foreach (string r in DropRejects) if (nm == r) { reject = true; break; }
                    if (reject) continue;
                    int delta = 0;
                    if (m.Groups[3].Success) delta = int.Parse(m.Groups[3].Value, Inv);
                    items.Add(Gd.Out("Name", nm, "Delta", delta));
                }
            }
            return Gd.Out("Blocks", blocks.ToArray());
        }

        // ==================== verb: resolve-ark-farm-plan(原 Resolve-ArkFarmPlan) ====================
        // 纯判定：目标清单 + 账本 + 学习表 → 本轮补刷方案。一次只锁"还差最多"的一个目标；
        // 刷哪关：显式填的 > 学习表挑(每场平均掉最多的优先，并列取见得多的，最多 2 关) > 不刷并说为什么；
        // 次数 = 缺口(09-29 用户口径「先把目标刷满再刷别的」：按每场至少掉 1 的保守折算，不再拿平均掉落率打折；
        // append 档 MAA 侧是目标库存监停(DropCount=目标绝对值，凑够当场停整条 Fight 链)，TimesLimit 只是单轮护栏，
        // 保守只会多给护栏、不会多刷；封顶 MaxTimes 不变)。输出键序逐字照抄旧 pscustomobject。

        static Dictionary<string, object> FarmPlanNone(string why)
        {
            return Gd.Out("Active", false, "Reason", why, "ItemId", "", "ItemName", "",
                "Have", 0, "Target", 0, "Gap", 0, "Stages", new object[0], "Times", 0);
        }

        internal static object FarmPlan(Dictionary<string, object> inp)
        {
            object[] targets = Gd.List(Gd.Get(inp, "targets"));
            Dictionary<string, object> nameToId = AsMap(Gd.Get(inp, "nameToId"));
            Dictionary<string, object> idToName = AsMap(Gd.Get(inp, "idToName"));
            Dictionary<string, object> inventory = AsMap(Gd.Get(inp, "inventory"));
            Dictionary<string, object> origin = AsMap(Gd.Get(inp, "origin"));
            Dictionary<string, object> dropTable = AsMap(Gd.Get(inp, "dropTable"));
            Dictionary<string, object> knownStages = AsMap(Gd.Get(inp, "knownStages"));
            int maxTimes = Gd.Int(Gd.Get(inp, "maxTimes"), 12);
            if (maxTimes < 1) maxTimes = 1;
            if (targets.Length == 0) return FarmPlanNone("没配 ark.farmTargets → 材料补刷未启动");

            List<string> notes = new List<string>();
            Dictionary<string, object> best = null;
            foreach (object t0 in targets)
            {
                Dictionary<string, object> t = Gd.Map(t0);
                if (t == null) continue;   // 旧 PS 对 $null 条目会在 $t.Bad 上吃 null 继续走，壳保证不会喂进来 null
                string bad = Gd.Str(Gd.Get(t, "Bad"));
                if (bad.Length > 0) { notes.Add("跳过「" + Gd.Str(Gd.Get(t, "Raw")) + "」：" + bad); continue; }
                string id = ""; string nm = Gd.Str(Gd.Get(t, "Key"));
                string key = nm;
                if (Regex.IsMatch(key, @"^\d{3,8}$", RegexOptions.None, TimeSpan.FromSeconds(5)))
                {
                    id = key;
                    if (idToName.ContainsKey(id)) nm = Gd.Str(idToName[id]);
                }
                else if (nameToId.ContainsKey(key)) id = Gd.Str(nameToId[key]);
                if (id.Length == 0) { notes.Add("跳过「" + Gd.Str(Gd.Get(t, "Raw")) + "」：材料名在 MAA 材料表(item_index)里找不到"); continue; }

                int have = inventory.ContainsKey(id) ? Gd.Int(inventory[id], 0) : 0;
                int gap = Gd.Int(Gd.Get(t, "Target"), 0) - have;
                if (gap <= 0) { notes.Add(nm + " 已够(" + have + "/" + Gd.Str(Gd.Get(t, "Target")) + ")"); continue; }

                object[] stages = Gd.List(Gd.Get(t, "Stages"));
                if (stages.Length == 0)
                {
                    // 没填关卡 → 从学习表挑：平均每场掉得最多的优先，并列取见得多的；最多挑 2 关
                    List<Dictionary<string, object>> learned = new List<Dictionary<string, object>>();
                    Dictionary<string, object> byStage = AsMap(dropTable.ContainsKey(id) ? dropTable[id] : null);
                    if (byStage != null)
                        foreach (KeyValuePair<string, object> e in byStage)
                        {
                            int runs = Gd.Int(Gd.Get(e.Value, "runs"), 0);
                            int qty = Gd.Int(Gd.Get(e.Value, "qty"), 0);
                            if (runs > 0 && qty > 0)
                                learned.Add(Gd.Out("Stage", (object)e.Key, "Avg", (double)qty / runs, "Runs", runs));
                        }
                    stages = learned
                        .OrderByDescending(x => Convert.ToDouble(x["Avg"], Inv))
                        .ThenByDescending(x => Convert.ToInt64(x["Runs"], Inv))
                        .Take(2)
                        .Select(x => (object)Gd.Str(x["Stage"]))
                        .ToArray();
                    if (stages.Length == 0)
                    {
                        notes.Add(nm + " 还差 " + gap + "，但不知道该刷哪关(清单里没填、掉落账本也没教过) → 把它写成 "
                            + Gd.Str(Gd.Get(t, "Raw")) + "@CE-5 这种带关卡的形式");
                        continue;
                    }
                }
                List<object> badSt = new List<object>();
                if (knownStages.Count > 0)
                    foreach (object st in stages) if (!knownStages.ContainsKey(Gd.Str(st))) badSt.Add(st);
                List<object> stOk = new List<object>();
                foreach (object st in stages) if (!ContainsValue(badSt, st)) stOk.Add(st);
                if (stOk.Count == 0)
                {
                    notes.Add(nm + " 还差 " + gap + "，但关卡 " + JoinObjects(badSt.ToArray(), "/") + " 不被 MAA 关卡表认识 → 先更 MAA 资源或改 farmTargets");
                    continue;
                }

                // 每场平均掉落：挑关仍按它(掉得多的打头)，Reason 里如实报数；但次数不再用它折算(见头注)
                int tq = 0, tr = 0;
                if (dropTable.ContainsKey(id))
                {
                    Dictionary<string, object> byStage2 = AsMap(dropTable[id]);
                    foreach (object st in stOk)
                    {
                        object v = byStage2 == null ? null : (object)GetByKey(byStage2, Gd.Str(st));
                        if (v != null) { tq += Gd.Int(Gd.Get(v, "qty"), 0); tr += Gd.Int(Gd.Get(v, "runs"), 0); }
                    }
                }
                double avg = 1.0; string avgNote = "掉落率未知，按每场 1 估";
                if (tr > 0)
                {
                    avg = Math.Max((double)tq / tr, 0.1);
                    avgNote = "按每场掉 1 保守给次数、凑满目标为止(学习到的每场约 " + RoundStr(Math.Round(avg, 1)) + ")";
                }
                int times = gap;
                string capNote = "";
                if (times > maxTimes) { times = maxTimes; capNote = "(单轮封顶 " + maxTimes + "，剩下的明天接着刷)"; }

                Dictionary<string, object> cand = Gd.Out(
                    "Active", true, "ItemId", id, "ItemName", nm, "Have", have, "Target", Gd.Int(Gd.Get(t, "Target"), 0),
                    "Gap", gap, "Stages", stOk.ToArray(), "Times", times, "Avg", avg, "AvgNote", avgNote,
                    "CapNote", capNote, "BadStages", badSt.ToArray());
                if (best == null || gap > Gd.Int(best["Gap"], 0)) best = cand;
            }
            if (best == null)
            {
                string why = "本轮没有要补刷的材料";
                if (notes.Count > 0) why = "本轮没有要补刷的材料：" + string.Join("；", notes.ToArray());
                return FarmPlanNone(why);
            }
            string originNote = "";
            object orgV = origin.ContainsKey(Gd.Str(best["ItemId"])) ? origin[Gd.Str(best["ItemId"])] : null;
            if (Gd.Str(orgV) == "depot") originNote = "(现值来自库存缓存标定，是估算)";
            string badNote = "";
            object[] bestBad = Gd.List(best["BadStages"]);
            if (bestBad.Length > 0) badNote = "；已剔除不认识的关卡 " + JoinObjects(bestBad, "/");
            return Gd.Out(
                "Active", true,
                "ItemId", best["ItemId"], "ItemName", best["ItemName"],
                "Have", best["Have"], "Target", best["Target"], "Gap", best["Gap"],
                "Stages", best["Stages"], "Times", best["Times"],
                "Reason", "补刷 " + Gd.Str(best["ItemName"]) + "×" + Gd.Str(best["Times"]) + " @"
                    + JoinObjects(Gd.List(best["Stages"]), ",") + "：目标 " + Gd.Str(best["Target"])
                    + "，现有 " + Gd.Str(best["Have"]) + originNote + "，还差 " + Gd.Str(best["Gap"])
                    + "，" + Gd.Str(best["AvgNote"]) + Gd.Str(best["CapNote"]) + badNote,
                "Notes", notes.ToArray());
        }

        static Dictionary<string, object> AsMap(object v)
        {
            return v as Dictionary<string, object>;
        }

        static object GetByKey(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) ? v : null;
        }

        static bool ContainsValue(List<object> list, object v)
        {
            string s = Gd.Str(v);
            foreach (object x in list) if (Gd.Str(x) == s) return true;
            return false;
        }

        static string JoinObjects(object[] a, string sep)
        {
            string[] s = new string[a.Length];
            for (int i = 0; i < a.Length; i++) s[i] = Gd.Str(a[i]);
            return string.Join(sep, s);
        }

        // $([Math]::Round($avg,1)) 的串面：double 的 PS 串转(zh-CN 与不变文化输出一致)
        static string RoundStr(double d) { return d.ToString("R", Inv); }

        // ==================== verb: apply-ark-daily-intent(原 Resolve-GdArkDecision 的构造段) ====================
        // 命令行意图(-Stage 手造 / -Force 越自证结论 / farm 专用轮与需求优先合算) → 本轮方舟方案对象。
        // 需求优先语义(用户硬要求)：farm 关**去重排最前**、长草关在后；"方舟方案：…"日志行与 Farm 挂回留壳。

        internal static object ArkIntent(Dictionary<string, object> inp)
        {
            Dictionary<string, object> ark = AsMap(Gd.Get(inp, "ark"));
            if (ark == null) ark = new Dictionary<string, object>();
            string stage = Gd.Str(Gd.Get(inp, "stage"));
            bool force = Gd.Truthy(Gd.Get(inp, "force"));
            bool farm = Gd.Truthy(Gd.Get(inp, "farm"));
            object farmOpt = Gd.Get(inp, "farmOpt");
            List<string> warnings = new List<string>();

            if (stage.Length > 0)
                ark = Gd.Out("Mode", "manual", "Stage", stage, "StageHint", stage, "ClearSanity", true,
                    "Reason", "命令行指定刷 " + stage, "EventKey", null, "EventTip", null, "Source", "manual");
            if (force && Gd.Str(Gd.Get(ark, "Mode")) == "event-pending")
            {
                // 这句在 10-12 第 1 刀改过口："活动确认门"已退场，-Force 现在越的是"今天的自证结论"，
                // 越门这件事本身一模一样(event-pending → event、理智全刷活动关)，所以 Mode 名册一字未动。
                string hint = Gd.Str(Gd.Get(ark, "StageHint"));
                ark = Gd.Out("Mode", "event", "Stage", hint, "StageHint", hint, "ClearSanity", true,
                    "Reason", "(-Force) 不管今天的自证结论(进得去与否)，强行刷 " + hint,
                    "EventKey", Gd.Get(ark, "EventKey"), "EventTip", Gd.Get(ark, "EventTip"), "Source", Gd.Get(ark, "Source"));
                warnings.Add("-Force：本轮忽略活动图自证结论");
            }

            object[] farmStages = farmOpt == null ? new object[0] : Gd.List(Gd.Get(farmOpt, "Stages"));
            string farmReason = farmOpt == null ? "" : Gd.Str(Gd.Get(farmOpt, "Reason"));
            string farmMode = "";
            bool hasFarmFields = false;

            if (farm)
            {
                hasFarmFields = true;
                if (farmOpt != null)
                {
                    farmMode = "dedicated";
                    string joined = JoinObjects(farmStages, ",");
                    ark = Gd.Out("Mode", "farm", "Stage", joined, "StageHint", joined, "ClearSanity", true,
                        "Reason", "材料补刷专用轮 — " + farmReason,
                        "EventKey", Gd.Get(ark, "EventKey"), "EventTip", Gd.Get(ark, "EventTip"), "Source", Gd.Get(ark, "Source"));
                }
                else warnings.Add("-Farm 本轮没有可补的材料 → 按原方案跑");
            }
            else if (farmOpt != null)
            {
                hasFarmFields = true;
                farmMode = "append";
                // 需求优先档：farm 关排理智计划最前(去重)，长草关在后(也去重)；farm 关掉不进活动/manual
                List<object> merged = new List<object>();
                foreach (object st in farmStages) if (!ContainsValue(merged, st)) merged.Add(st);
                foreach (object st in SplitStages(Gd.Str(Gd.Get(ark, "Stage")))) if (!ContainsValue(merged, st)) merged.Add(st);
                ark = Overwrite(ark,
                    "Stage", JoinObjects(merged.ToArray(), ","),
                    "Reason", Gd.Str(Gd.Get(ark, "Reason")) + "；需求优先 " + farmReason);
            }

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Ark"] = ark;
            data["FarmMode"] = farmMode;
            data["HasFarmFields"] = hasFarmFields;
            data["Warnings"] = warnings.ToArray();
            return data;
        }

        // 在既有键序上覆盖两个键的值(Add-Member -Force 同语义：新键排在末尾，旧键原位覆盖)
        static Dictionary<string, object> Overwrite(Dictionary<string, object> src, params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>(src);
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // ==================== verb: apply-r1999-daily-intent(原 Resolve-GdR1999Decision 的构造段) ====================
        // -Force 跳确认门与 -Plan 手动指定的两段构造(Plan≠auto 无条件覆盖 Force 那一段的结果)，
        // 实例解析(r1999-instance)与"1999 方案：…"日志行留壳。

        internal static object R1999Intent(Dictionary<string, object> inp)
        {
            Dictionary<string, object> d = AsMap(Gd.Get(inp, "decision"));
            if (d == null) d = new Dictionary<string, object>();
            string plan = Gd.Str(Gd.Get(inp, "plan"));
            bool force = Gd.Truthy(Gd.Get(inp, "force"));
            List<string> warnings = new List<string>();

            if (force && Gd.Str(Gd.Get(d, "Mode")) == "event-pending")
            {
                d = Gd.Out("Mode", "event", "Instance", "event", "Version", Gd.Get(d, "Version"),
                    "VersionName", Gd.Get(d, "VersionName"), "EventKey", Gd.Get(d, "EventKey"),
                    "EventTip", Gd.Get(d, "EventTip"),
                    "Reason", "(-Force) 跳过 1999 活动确认门，直接用活动方案", "DataAge", Gd.Get(d, "DataAge"));
                warnings.Add("-Force：1999 本轮忽略活动确认门");
            }
            if (plan != "auto")
                d = Gd.Out("Mode", plan, "Instance", plan, "Version", "manual", "VersionName", "命令行指定",
                    "EventKey", null, "EventTip", null, "Reason", "命令行指定 " + plan, "DataAge", null);

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Decision"] = d;
            data["Warnings"] = warnings.ToArray();
            return data;
        }

        // ==================== verb: parse-maa-gui-lines(原 Invoke-MaaRun 的每行分类段) ====================
        // 一段新增 gui.log → 标志事件数组 + 计数。循环/兜底/完成判定留壳：这里只回答"这几行是什么"。
        // 五种标志与 ERR+benign 的判定顺序、清洗正则逐字照抄旧实现；marker 计数含"匹配上但 msg 为空"的行。

        internal static object MaaGuiLines(Dictionary<string, object> inp)
        {
            string text = Gd.Str(Gd.Get(inp, "text"));
            List<object> benignPatterns = new List<object>();
            foreach (object b in Gd.List(Gd.Get(inp, "benign"))) benignPatterns.Add(b);
            string[] lines = Regex.Split(text, @"\r?\n");
            List<object> events = new List<object>();
            int logChars = 0, markers = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0) continue;
                logChars += line.Length;
                if (Regex.IsMatch(line, "任务已全部完成", RegexOptions.None, TimeSpan.FromSeconds(5)))
                {
                    markers++;
                    string used = "";
                    if (i + 1 < lines.Length)
                    {
                        Match mu = Regex.Match(lines[i + 1], @"用时\s*([^)]+)", RegexOptions.None, TimeSpan.FromSeconds(5));
                        if (mu.Success) used = mu.Groups[1].Value.Trim();
                    }
                    events.Add(Gd.Out("Kind", "finished", "Used", used));
                }
                else
                {
                    Match mc = Regex.Match(line, @"Completed Task Chain:\s*([A-Za-z]+)", RegexOptions.None, TimeSpan.FromSeconds(5));
                    if (mc.Success)
                    {
                        markers++;
                        events.Add(Gd.Out("Kind", "chain", "Chain", mc.Groups[1].Value));
                    }
                    else if (Regex.IsMatch(line, @"无更新|已停滞", RegexOptions.None, TimeSpan.FromSeconds(5)))
                    {
                        markers++;
                        string warn = Regex.Replace(Regex.Replace(line, @"^.*?<\d+>\s*", "", RegexOptions.None, TimeSpan.FromSeconds(5)),
                            @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5)).Trim();
                        events.Add(Gd.Out("Kind", "stall", "Msg", warn));
                    }
                    else if (Regex.IsMatch(line, "任务已完成，但出现错误", RegexOptions.None, TimeSpan.FromSeconds(5)))
                    {
                        markers++;
                        events.Add(Gd.Out("Kind", "drained"));
                    }
                    else if (Regex.IsMatch(line, @"\[ERR\]|任务失败|Exception|连接失败", RegexOptions.None, TimeSpan.FromSeconds(5)))
                    {
                        markers++;
                        string msg = Regex.Replace(Regex.Replace(line, @"^.*?\]\s*", "", RegexOptions.None, TimeSpan.FromSeconds(5)),
                            @"^\d+\s*", "", RegexOptions.None, TimeSpan.FromSeconds(5));
                        string hit = null;
                        foreach (object pat in benignPatterns)
                            if (Regex.IsMatch(msg, Gd.Str(pat), RegexOptions.None, TimeSpan.FromSeconds(5))) { hit = Gd.Str(pat); break; }
                        if (msg.Length > 0 && hit == null) events.Add(Gd.Out("Kind", "error", "Msg", msg.Trim()));
                        else if (msg.Length > 0 && hit != null) events.Add(Gd.Out("Kind", "benign", "Msg", msg.Trim(), "Pattern", hit));
                    }
                }
            }
            return Gd.Out("LogChars", logChars, "Markers", markers, "Events", events.ToArray());
        }

        // ==================== verb: split-task-args(原 Get-GdTaskArgsWithGames 的剥参+切词段) ====================
        // 任务 Arguments 整串 → 参数数组：先剥原有 -Games/-Tag，再按引号切(引号内整体成一项)。
        // Count<3 的"不像话"拒绝与回落直跑留壳；-Tag manual -Games x 的追加也留壳(那是调用方的意图)。

        internal static object TaskArgs(Dictionary<string, object> inp)
        {
            string text = Gd.Str(Gd.Get(inp, "text"));
            text = Regex.Replace(text, @"\s+-Games\s+""?\w+""?", "", RegexOptions.None, TimeSpan.FromSeconds(5));
            text = Regex.Replace(text, @"\s+-Tag\s+""?\w+""?", "", RegexOptions.None, TimeSpan.FromSeconds(5));
            List<object> pieces = new List<object>();
            foreach (Match m in Regex.Matches(text, @"""([^""]*)""|(\S+)", RegexOptions.None, TimeSpan.FromSeconds(5)))
                pieces.Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
            return Gd.Out("Pieces", pieces.ToArray());
        }

        // ==================== verb: decide-daily-retry(批次 8：两路 daily 的重试分流纯判定段) ====================
        // 基线真身 = lib\ark-daily.ps1 行 141-148 与 lib\r1999-daily.ps1 行 90-106 的 if/elseif 分流
        // (钉 commit c498da0)。壳拿 {Retryable, Logs:[{Level,Text}]}：Retryable 为真才 Stop/Sleep/重跑(全留壳)，
        // Logs 逐条原样 Write-GdLog。文案必须与 PS 原话逐字符一致(panel-cs --orchtest 对拍面)：
        // 有差异回修这里，禁止改期望、禁止放宽比对(docs\ps-migration-batch0.md §7)。
        // 分流链逐字复刻 if/elseif 次序，不做"逻辑化简"：retryOnce=$false 时首支不成立会**落到**后面的
        // elseif —— 不重试也照样打错误日志，就是这个行为。ark 那句 not-started∧非管理员的独立 if 提示不在本 verb。
        // PS 的 -eq/-notin/-in 对串大小写不敏感 → 一律 OrdinalIgnoreCase。

        static bool EqS(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        internal static object DailyRetry(Dictionary<string, object> inp)
        {
            string game = Gd.Str(Gd.Get(inp, "game"));
            string status = Gd.Str(Gd.Get(inp, "status"));
            bool rescued = Gd.Truthy(Gd.Get(inp, "rescued"));
            bool retryOnce = Gd.Truthy(Gd.Get(inp, "retryOnce"));
            List<object> logs = new List<object>();
            bool retryable = false;

            if (EqS(game, "ark"))
            {
                // ark：不在跳过清单 {ok, not-started, unknown-format} 且允许重试 → 重试一次；否则只有 unknown-format 落 ERROR。
                bool inSkip = EqS(status, "ok") || EqS(status, "not-started") || EqS(status, "unknown-format");
                if (!inSkip && retryOnce)
                {
                    retryable = true;
                    logs.Add(Gd.Out("Level", "WARN", "Text", "方舟本轮未成功 → 收尾后重试一次"));
                }
                else if (EqS(status, "unknown-format"))
                {
                    logs.Add(Gd.Out("Level", "ERROR", "Text", "方舟日志标志一条都没认出来 → 重试同样看不懂，直接记入本轮告警(见下面的处理建议)"));
                }
            }
            else if (EqS(game, "r1999"))
            {
                // r1999：$retryable 表达式 = 不在 {ok,not-started,empty-queue,unknown-format,blocked} —— 或
                // 画面确实被兜清楚(Rescued)且状态在 {not-started,empty-queue,blocked}。
                bool baseRetry = !(EqS(status, "ok") || EqS(status, "not-started") || EqS(status, "empty-queue")
                                   || EqS(status, "unknown-format") || EqS(status, "blocked"));
                bool rv = baseRetry || (rescued && (EqS(status, "not-started") || EqS(status, "empty-queue") || EqS(status, "blocked")));
                if (rv && retryOnce)
                {
                    retryable = true;
                    logs.Add(Gd.Out("Level", "WARN", "Text", rescued
                        ? "1999 本轮未成功，但视觉兜底把挡路的界面点掉了 → 关闭 M9A 后重试一次"
                        : "1999 本轮未成功 → 关闭游戏后重试一次"));
                }
                else if (EqS(status, "unknown-format"))
                {
                    logs.Add(Gd.Out("Level", "ERROR", "Text", "1999 日志标志一条都没认出来 → 重试同样看不懂，直接记入本轮告警(见下面的处理建议)"));
                }
                else if (EqS(status, "blocked"))
                {
                    logs.Add(Gd.Out("Level", "ERROR", "Text", "1999 从头一个任务起就没进游戏 → 是客户端/资源要更新或模拟器状态不对，重试没有意义，直接记入本轮告警"));
                }
                else if (EqS(status, "not-started") || EqS(status, "empty-queue"))
                {
                    logs.Add(Gd.Out("Level", "WARN", "Text", "1999 队列没开始/空跑完成 → 重试没有意义，直接记入本轮告警"));
                }
            }
            else
            {
                throw new GdRuleException("decide-daily-retry：game 只认 ark/r1999，收到「" + game + "」");
            }
            return Gd.Out("Retryable", retryable, "Logs", logs.ToArray());
        }

        // ==================== verb: detect-maa-fight-skip(2026-09-28 新增：认 MAA 的跳关回执) ====================
        // 背景(09-28 06:00 实测)：我们写盘的 StagePlan 三关确实落文件(MAA 开机 .bak 里 TimesLimit/DropId
        // 都在)，但 MAA 加载时按认库把当日不可选的关**修剪**出计划(GetFightStage: from 只剩 1 关)，
        // 剩下的又不开放 → 整条 Fight 跳过，随后照常宣布「任务已全部完成」——判定层拿到的是假 ok。
        // 输入 gui.log 文本(整份或尾巴都行)：先自切"最后一次 GUI started 起"= 本次会话，纯字符串不读盘。
        // Skipped 第一档只押「关卡计划内没有当日开放的关卡」这一句(理智作战: … 任务跳过 有多种起因，比如理智已满，
        // 不能单独当认关证据，只作旁证计数)；Plan 取会话内最后一条 GetFightStage: from 的引号串数组。
        // 第二档(见循环后注释)：from [] 空计划 + Fight 任务跳过 也算认关。
        // 止损动作(剔 farm 关重试一次/认栽/当日标记)全在 ark-daily 壳层编排，这里只出结论。

        internal static object MaaFightSkip(Dictionary<string, object> inp)
        {
            string text = Gd.Str(Gd.Get(inp, "text"));
            string[] lines = Regex.Split(text, @"\r?\n");
            int startIdx = 0;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (Regex.IsMatch(lines[i], "MaaAssistantArknights GUI started", RegexOptions.None, TimeSpan.FromSeconds(5)))
                { startIdx = i; break; }
            }
            List<string> plan = new List<string>();
            List<string> evidence = new List<string>();
            bool skipped = false;
            bool sawPlan = false;
            int taskSkips = 0;
            for (int i = startIdx; i < lines.Length; i++)
            {
                string line = lines[i];
                Match mf = Regex.Match(line, @"GetFightStage:\s*from\s*\[([^\]]*)\]", RegexOptions.None, TimeSpan.FromSeconds(5));
                if (mf.Success)
                {
                    sawPlan = true;
                    plan = new List<string>();
                    foreach (Match mq in Regex.Matches(mf.Groups[1].Value, @"""([^""]*)""", RegexOptions.None, TimeSpan.FromSeconds(5)))
                        plan.Add(mq.Groups[1].Value);
                    continue;
                }
                if (Regex.IsMatch(line, "关卡计划内没有当日开放的关卡", RegexOptions.None, TimeSpan.FromSeconds(5)))
                {
                    skipped = true;
                    if (evidence.Count < 3) evidence.Add(CleanGuiLine(line));
                }
                else if (Regex.IsMatch(line, @"(理智作战|剩余理智)[^\r\n]*任务跳过", RegexOptions.None, TimeSpan.FromSeconds(5)))
                {
                    taskSkips++;
                    if (evidence.Count < 3) evidence.Add(CleanGuiLine(line));
                }
            }
            // 第二档认关：加载期修剪把计划修成**空列表**(GetFightStage: from [] 出现且只剩 0 关)再叠上
            // Fight 的「任务跳过」—— 剔掉 farm 关重试的那一轮就是这个形态，「没有当日开放」句未必会打。
            // 没见过 GetFightStage(理智已满等更早的跳过)不算：那条路径压根没走到建计划。
            if (!skipped && taskSkips > 0 && sawPlan && plan.Count == 0) skipped = true;
            return Gd.Out("Skipped", skipped, "Plan", plan.ToArray(),
                "TaskSkips", taskSkips, "Evidence", evidence.ToArray());
        }

        // 与 parse-maa-gui-lines 的 stall 清洗同款：剥掉 `<时间戳][级别][模块] <n> ` 前缀、压多空格。
        static string CleanGuiLine(string line)
        {
            string s = Regex.Replace(line, @"^.*?<\d+>\s*", "", RegexOptions.None, TimeSpan.FromSeconds(5));
            s = Regex.Replace(s, @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(5));
            return s.Trim();
        }

        // ==================== verb: observe-maa-stage-open(2026-10-05 新增：只读观测，攒"周几开哪本"的账) ====================
        // 背景(10-04/10-05 实锤)：MAA 对我们写盘的 StagePlan 连砍两刀 —— 第一刀在加载期按账号解锁进度修剪
        // (10-8 被剪)，第二刀在 GUI UpdateStageList 按星期修剪(周一 CE-5 被剪)；两刀剪空后内核走
        // 「回到上次作战」重放(asst.log 里 first:["LastOrCurBattleBegin"])，照烧理智但打的不是我们指定的本。
        // 修这个要靠"每星期几实际开哪些长草本"这张表，而这张表没人抄过 —— 本 verb 每轮从两份日志抄三对
        // 真值读数进观测账本(state\ark-stage-weekday-obs.json)，攒满一周后**由人**看账成案写 planByWeekday。
        // 护栏(用户批复 B 档)：只采集、只出读数，绝不在这里或账本里改任何配置。
        // 输入纯文本三件(asstText=asst.log 全 tail、guiText=gui.log 全 tail、date=壳侧注入当天 yyyy-MM-dd；
        // C# 侧不读盘不读钟，同 detect-maa-fight-skip 的纪律)。读数口径：
        //   Found   = 文本里有没有 `append_task Fight ` 行(没有=这一轮没递 Fight 任务，观测无意义)；
        //   Stage   = 最后一条该行的 "stage" 字段原样(空串正是"计划被剪空"的铁证，不粉饰)；
        //   LastBattle = 该行**之后**是否出现同时含 LastOrCurBattleBegin 与 "taskchain":"Fight" 的行(重放实锤)；
        //   Survivors/Selected = gui 最后一条 `GetFightStage: from [...], selected X` 的两半 —— 这是
        //     MAA 眼里"今天开着的关名单"，哪怕本轮没被烧到也照抄(它就是我们要的开放表旁证)。实测剪空形状是
        //     `from [""], selected `(空串占位不是关名，滤掉；selected 缺失或 null 统一成空串)；
        //   SawGuiPlan = 有没有见过 GetFightStage 行(理智更早跳过的轮会没有)。
        // 与 detect-maa-fight-skip 同族但语义相反：那条判"认栽"，这条只抄账；Skipped=false 的轮这里照样可能空名单。
        internal static object MaaStageObserve(Dictionary<string, object> inp)
        {
            string asstText = Gd.Str(Gd.Get(inp, "asstText"));
            string guiText = Gd.Str(Gd.Get(inp, "guiText"));
            string date = Gd.Str(Gd.Get(inp, "date"));
            bool found = false;
            string stage = "";
            bool lastBattle = false;
            string[] survivors = new string[0];
            string selected = "";
            bool sawGuiPlan = false;

            string[] asstLines = Regex.Split(asstText, @"\r?\n");
            int appendIdx = -1;
            for (int i = asstLines.Length - 1; i >= 0; i--)
            {
                if (asstLines[i].Contains("append_task Fight ")) { appendIdx = i; break; }
            }
            if (appendIdx >= 0)
            {
                found = true;
                Match ms = Regex.Match(asstLines[appendIdx], @"""stage""\s*:\s*""([^""]*)""", RegexOptions.None, TimeSpan.FromSeconds(5));
                if (ms.Success) stage = ms.Groups[1].Value;
                for (int i = appendIdx + 1; i < asstLines.Length; i++)
                {
                    if (asstLines[i].Contains("LastOrCurBattleBegin") && asstLines[i].Contains(@"""taskchain"":""Fight"""))
                    { lastBattle = true; break; }
                }
            }
            foreach (string line in Regex.Split(guiText, @"\r?\n"))
            {
                Match mg = Regex.Match(line, @"GetFightStage:\s*from\s*\[([^\]]*)\]", RegexOptions.None, TimeSpan.FromSeconds(5));
                if (!mg.Success) continue;
                sawGuiPlan = true;
                List<string> keep = new List<string>();
                foreach (Match mq in Regex.Matches(mg.Groups[1].Value, @"""([^""]*)""", RegexOptions.None, TimeSpan.FromSeconds(5)))
                    if (mq.Groups[1].Value.Length > 0) keep.Add(mq.Groups[1].Value);   // 实测剪空形状是 from [""], —— 空串占位不算存活关名
                survivors = keep.ToArray();
                Match msel = Regex.Match(line, @",\s*selected\s+(.+?)\s*$", RegexOptions.None, TimeSpan.FromSeconds(5));
                selected = msel.Success && msel.Groups[1].Value != "null" ? msel.Groups[1].Value : "";
            }
            return Gd.Out("Found", found, "Date", date, "Stage", stage, "LastBattle", lastBattle,
                "SawGuiPlan", sawGuiPlan, "Survivors", survivors, "Selected", selected);
        }

        // ==================== verb: decide-ark-event-probe(2026-10-12 第 1 刀：每轮自证活动图通没打通) ====================
        // 需求原话(用户)：「活动要打，需要打通活动」「直接用尝试用 maa 进入活动最后一关，如果通了就说明通关了」。
        // 于是每轮把活动**最后一关**当探针递下去，跑完从同一批现场读数里判"进得去没进得去"，
        // 结论落 state\ark-event-probe.json，下一轮由 Get-ArkEvent 喂进 decide-ark 的 probe 入参。
        // 判据支点(本文件上面 MaaStageObserve 那段两刀注释)：加载期那一刀按**账号解锁进度**修剪，
        // GUI 那一刀按**星期**修剪；活动关不受周轮换限制 ⇒ 它被剪时只剩一个解释：**没解锁到它**。
        // 实测锚(10-12 20:34 探针轮，YW-8)：进得去时 gui 会话内是 `GetFightStage: from ["YW-8"], selected YW-8`。
        // 输入四件(stage=本轮试打的那关、obs=observe-maa-stage-open 的原样回执、date/eventKey 由壳注入)：
        // C# 侧照样不读盘不读钟，比的就是"名单里有没有这个名字"。
        // 输出六件里 Valid 是**探针有效性**：理智更早跳过、没连上、名单压根没抄到 ⇒ Valid=false，
        // 壳侧据此**不落地**(留着昨天的结论也没意义，隔天本来就重判)，绝不当成"没打通"——
        // 把采集失败误判成未解锁，会白把一天的活动图换成长草图。
        internal static object ArkEventProbe(Dictionary<string, object> inp)
        {
            string date = Gd.Str(Gd.Get(inp, "date"));
            string evKey = Gd.Str(Gd.Get(inp, "eventKey"));
            string stage = Gd.Str(Gd.Get(inp, "stage"));
            Dictionary<string, object> obs = Gd.Map(Gd.Get(inp, "obs"));

            if (stage.Length == 0)
                return Gd.Out("Date", date, "EventKey", evKey, "Stage", stage, "Valid", false,
                    "Entered", false, "Evidence", "上一轮没有可当探针的活动关(判定给不出最后一关) → 这条结论不落地");
            if (obs == null)
                return Gd.Out("Date", date, "EventKey", evKey, "Stage", stage, "Valid", false,
                    "Entered", false, "Evidence", "本轮没有关卡开放观测(采集段没做起来) → 这条结论不落地，下轮照常再试");

            List<string> surv = new List<string>();
            foreach (object v in Gd.List(Gd.Get(obs, "Survivors")))
            {
                string s = Gd.Str(v);
                if (s.Length > 0) surv.Add(s);
            }
            bool sawPlan = Gd.Truthy(Gd.Get(obs, "SawGuiPlan"));
            string selected = Gd.Str(Gd.Get(obs, "Selected"));
            bool lastBattle = Gd.Truthy(Gd.Get(obs, "LastBattle"));
            string replay = lastBattle ? "；注意本轮内核走的是「回到上次作战」重放" : "";

            if (!sawPlan)
                return Gd.Out("Date", date, "EventKey", evKey, "Stage", stage, "Valid", false,
                    "Entered", false,
                    "Evidence", "本轮 MAA 没抄出关卡名单(GetFightStage 那行没出现，多半理智更早跳过或没连上) → 判不了，这条结论不落地");

            if (selected.Length > 0 && selected == stage)
                return Gd.Out("Date", date, "EventKey", evKey, "Stage", stage, "Valid", true, "Entered", true,
                    "Evidence", "MAA 的关卡名单里选中了 " + stage + "(名单共 " + surv.Count.ToString(CultureInfo.InvariantCulture) + " 关)" + replay);
            if (surv.Contains(stage))
                return Gd.Out("Date", date, "EventKey", evKey, "Stage", stage, "Valid", true, "Entered", true,
                    "Evidence", "MAA 的关卡名单含 " + stage + "(名单共 " + surv.Count.ToString(CultureInfo.InvariantCulture) + " 关，当前选中 " + (selected.Length > 0 ? selected : "无") + ")" + replay);
            if (surv.Count == 0)
                return Gd.Out("Date", date, "EventKey", evKey, "Stage", stage, "Valid", true, "Entered", false,
                    "Evidence", "MAA 本轮的关卡名单被剪空(from [\"\"]) → " + stage + " 进不去" + replay);
            return Gd.Out("Date", date, "EventKey", evKey, "Stage", stage, "Valid", true, "Entered", false,
                "Evidence", "MAA 名单里 " + surv.Count.ToString(CultureInfo.InvariantCulture) + " 关没有 " + stage
                          + "(名单：" + string.Join("、", surv.ToArray()) + "，当前选中 " + (selected.Length > 0 ? selected : "无") + ")" + replay);
        }
    }
}
