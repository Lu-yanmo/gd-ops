// tools/panel-cs/Decisions.cs —— 批次 1：「今天刷什么」的判定本体，从 lib\ark-decisions.ps1 / lib\r1999-decisions.ps1 逐条搬来。
//
// 单一事实源纪律：自本批起 PS 侧的 Resolve-ArkPlan / Resolve-R1999Plan / Test-GdStages / Resolve-R1999Instance
// 全部改成薄转发，规则只写 C# 这一份。文案必须与 PS 原话逐字符一致 ——
// 判定面由本目录 Decisions.SelfTest.cs 逐项做**取值断言**(随 build.ps1 -Verify 的 --dectest 跑)，有差异回修这里，禁止放宽对拍。⚠ 10-26 甲档第六刀起 tools\probe-decisions-golden.ps1 不再拿 git 里的旧实现做全字面对拍——那 45 档点的是 HEAD 里两份 PS 件的逐字副本(与现树同一份代码、没有对面)；第八刀把最后留下的屏保 14 档逐字搬进本目录(场景与旧门同名)并整件退役，那座门自此不存在(取证见 docs\rust-core-plan.md §11.141／§11.144)。
//
// 输入形状(转发壳组装、--in 文件送来)：各判定所需的一切都是**值**，不含任何现场副作用 ——
//   decide-ark:            { ark:{…settings块…}, dow, now, data, source, ack:{…}, shopDone:{…} }
//   decide-r1999:          { re1999:{…}, now, data, ack:{…}, shopDone:{…} }
//   check-stages:          { table:{关卡:理智}, stageText }
//   r1999-instance:        { dir, want, planKey }
// 输出进信封 data，键名与声明顺序照抄原 pscustomobject(顺序过得了对拍的 ConvertTo-Json 比对)。
// 数字读回是 Int32/Decimal 混发(批次 0 钉死)——这里一律 Convert.ToXxx，不裸强转。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    // 判定层的"该拒"：不是代码错也不是环境缺，是规则说不干 —— 退出码 2，error 是给用户看的原话。
    public sealed class GdRuleException : Exception
    {
        public GdRuleException(string msg) : base(msg) { }
    }

    static class Gd
    {
        // PS 真值口径：$null/空串/0/空集合为假，其余为真(判定层大量依赖，别按 C# 直觉来)。
        public static bool Truthy(object v)
        {
            if (v == null) return false;
            if (v is bool) return (bool)v;
            if (v is string) return ((string)v).Length > 0;
            if (v is IDictionary<string, object>) return ((IDictionary<string, object>)v).Count > 0;
            if (v is object[]) return ((object[])v).Length > 0;
            if (v is System.Collections.IList) return ((System.Collections.IList)v).Count > 0;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture) != 0.0; } catch { return true; }
        }

        public static Dictionary<string, object> Map(object v)
        {
            return v as Dictionary<string, object>;
        }

        // 对齐 Get-GdMapValue：null 映射、缺键、非对象 → null。
        public static object Get(object map, string key)
        {
            Dictionary<string, object> d = Map(map);
            if (d == null) return null;
            object v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        // 批次 10：PS 的属性访问($Cfg.Saver)本来自带大小写宽容，信封字典却是大小写敏感的(Gd.Map 原样透传) ——
        // 吃"调用方手构对象"的 verb 先过这道视图，别让旧代码里合法的写法在新壳判红(键冲突时保首个，与 PS Hashtable 同向)。
        public static Dictionary<string, object> Ci(object v)
        {
            Dictionary<string, object> d = Map(v);
            if (d == null) return null;
            Dictionary<string, object> ci = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> kv in d) ci[kv.Key] = kv.Value;
            return ci;
        }

        public static object[] List(object v)
        {
            if (v == null) return new object[0];
            object[] a = v as object[];
            if (a != null) return a;
            // string 也实现 ICollection<char>、JSS 字典实现 ICollection<KeyValuePair> —— 都不许被摊开：
            // PS 侧 @('单串') 和 @(ConvertFrom-Json 的单对象) 都是"一个元素"(批次 2 对拍 queue/D 抓到)。
            if (v is string) return new object[] { v };
            if (v is IDictionary<string, object>) return new object[] { v };
            System.Collections.ICollection c = v as System.Collections.ICollection;
            if (c != null)
            {
                object[] outA = new object[c.Count];
                c.CopyTo(outA, 0);
                return outA;
            }
            return new object[] { v };
        }

        // 对齐 [string] 强转：null → ""；数字按不变文化(PS 的 CurrentCulture 是 zh-CN，输出同为 ASCII 数字)。
        public static string Str(object v)
        {
            if (v == null) return "";
            if (v is string) return (string)v;
            if (v is bool) return (bool)v ? "True" : "False";   // PS 的 [string]$true 就是 True
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static long Lng(object v)
        {
            if (v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        public static int Int(object v, int def)
        {
            if (v == null) return def;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        // 压缩空白、/ 换 -、四种精确格式再兜底 TryParse；失败/空 → null。
        // ⚠ 2026-11-03 转接刀改口：原句「对齐 Convert-GdUtc8ToMs」——那枚 PS 真身已出仓，这一份是唯一实现，出口＝verb convert-utc8-to-ms。
        public static long? Utc8ToMs(object textObj)
        {
            string text = Str(textObj);
            if (text.Length == 0) return null;
            string s = Regex.Replace(text, @"\s+", " ").Replace('/', '-').Trim();
            string[] fmts = new string[] { "yyyy-MM-dd HH:mm:ss", "yyyy-M-d H:mm:ss", "yyyy-MM-dd", "yyyy-M-d" };
            DateTime dt;
            bool ok = DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
            if (!ok) ok = DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt);
            if (!ok) return null;
            DateTime asUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return new DateTimeOffset(asUtc).ToUnixTimeMilliseconds() - 8 * 3600 * 1000;
        }

        // 假值(含 0)一律 '?'。
        // ⚠ 2026-11-03 转接刀改口：原句「对齐 Format-GdUnixMs」——那枚 PS 真身已出仓，这一份是唯一实现，出口＝verb format-unix-ms。
        public static string Fmt(object msObj) { return Fmt(msObj, "MM-dd HH:mm"); }

        public static string Fmt(object msObj, string pattern)
        {
            if (!Truthy(msObj)) return "?";
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(Lng(msObj))
                    .ToOffset(TimeSpan.FromHours(8)).ToString(pattern, CultureInfo.InvariantCulture);
            }
            catch { return "?"; }
        }

        public static Dictionary<string, object> Out(params object[] kv)
        {
            // 按声明顺序建输出对象(对拍比的是 JSON 文本，顺序即协议)。
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }
    }

    static class Decisions
    {
        // ==================== decide-ark：原 lib\ark-decisions.ps1 的 Resolve-ArkPlan ====================
        public static object Ark(Dictionary<string, object> inp)
        {
            object arkBlock = Gd.Get(inp, "ark");
            long now = Gd.Lng(Gd.Get(inp, "now"));
            object data = Gd.Get(inp, "data");
            string source = Gd.Str(Gd.Get(inp, "source"));
            if (source.Length == 0) source = "无数据";
            // ack / verbAck 两枚入参自 10-12 第 1 刀起不再读(-AckArk 退场)；⚠ 10-30 B-3②-d 刀① 起壳侧连传都不传了
            // (body 摘掉 verbAck／verbShopReset 两枚键)，于是这里连"不碰即可"都省了 —— 少一行读取就少一个假文案。
            Dictionary<string, object> shopDone = Gd.Map(Gd.Get(inp, "shopDone")) ?? new Dictionary<string, object>();
            int dow = Gd.Int(Gd.Get(inp, "dow"), 0);

            string idlePlan = Gd.Str(Gd.Get(Gd.Get(arkBlock, "planByWeekday"), dow.ToString(CultureInfo.InvariantCulture)));
            if (idlePlan.Length == 0) idlePlan = Gd.Str(Gd.Get(arkBlock, "defaultStage"));

            List<Dictionary<string, object>> fightable = new List<Dictionary<string, object>>();
            object server = Gd.Get(data, "Official");
            if (server != null)
            {
                foreach (KeyValuePair<string, object> grp in new List<KeyValuePair<string, object>>(Gd.Map(server) ?? new Dictionary<string, object>()))
                {
                    object gv = grp.Value;
                    if (gv == null) continue;
                    List<KeyValuePair<string, object>> entries = new List<KeyValuePair<string, object>>();
                    object[] gvArr = gv as object[];
                    if (gvArr != null)
                    {
                        for (int i = 0; i < gvArr.Length; i++) entries.Add(new KeyValuePair<string, object>("item" + i.ToString(CultureInfo.InvariantCulture), gvArr[i]));
                    }
                    else
                    {
                        Dictionary<string, object> gvMap = Gd.Map(gv);
                        if (gvMap != null)
                            foreach (KeyValuePair<string, object> item in gvMap) entries.Add(item);
                    }
                    foreach (KeyValuePair<string, object> e in entries)
                    {
                        object v = e.Value;
                        if (v == null) continue;
                        object act = Gd.Get(v, "Activity");
                        if (act == null) act = v;
                        long? start = Gd.Utc8ToMs(Gd.Get(act, "UtcStartTime"));
                        long? end = Gd.Utc8ToMs(Gd.Get(act, "UtcExpireTime"));
                        if (!Gd.Truthy(start) || !Gd.Truthy(end)) continue;   // 原话 `-not $start`：0 也算没填
                        if (now < start.Value || now > end.Value) continue;
                        List<Dictionary<string, object>> stages = new List<Dictionary<string, object>>();
                        foreach (object st in Gd.List(Gd.Get(v, "Stages")))
                        {
                            object val = Gd.Get(st, "Value");
                            if (Gd.Truthy(val)) stages.Add(Gd.Out("Value", Gd.Str(val), "Drop", Gd.Str(Gd.Get(st, "Drop"))));
                        }
                        fightable.Add(Gd.Out(
                            "Group", grp.Key, "Id", Gd.Str(e.Key), "Tip", Gd.Str(Gd.Get(act, "Tip")),
                            "Start", start.Value, "End", end.Value, "Stages", stages.ToArray()));
                    }
                }
            }
            // 原话：Where Stages.Count>0 | Sort-Object End —— Count 用 .NET 的数组长度即可，排序稳定按 End。
            List<Dictionary<string, object>> runnable0 = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> f in fightable)
                if (Gd.List(f["Stages"]).Length > 0) runnable0.Add(f);
            runnable0.Sort(delegate(Dictionary<string, object> a, Dictionary<string, object> b)
            {
                return Gd.Lng(a["End"]).CompareTo(Gd.Lng(b["End"]));
            });
            fightable = runnable0;

            // 「资源收集」= 材料加倍期(没有活动代币，长草计划本身最优)，只作为提示。
            string rcNote = "";
            object rc = Gd.Get(server, "resourceCollection");
            if (rc != null)
            {
                long? rs = Gd.Utc8ToMs(Gd.Get(rc, "UtcStartTime"));
                long? re = Gd.Utc8ToMs(Gd.Get(rc, "UtcExpireTime"));
                if (Gd.Truthy(rs) && Gd.Truthy(re) && now >= rs.Value && now <= re.Value)
                    rcNote = "；正逢[" + Gd.Str(Gd.Get(rc, "Tip")) + "]材料加倍期(至 " + Gd.Fmt(re) + ")，刷材料本效率翻倍";
            }

            Func<string, string, string, bool, Dictionary<string, object>> idle = delegate(string reason, string key, string tip, bool done)
            {
                return Gd.Out(
                    "Mode", "idle", "Stage", idlePlan, "StageHint", null, "ClearSanity", true,
                    "Reason", reason + "(数据源：" + source + ")" + rcNote,
                    "EventKey", key, "EventTip", tip, "Source", source, "ShopDone", done);
            };

            if (fightable.Count == 0)
                return idle("无可刷活动关卡，按长草计划刷 " + idlePlan, null, null, false);

            List<Dictionary<string, object>> emptied = new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> runnable = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> f in fightable)
                (shopDone.ContainsKey(Gd.Str(f["Id"])) ? emptied : runnable).Add(f);

            if (runnable.Count == 0)
            {
                List<string> tips = new List<string>();
                foreach (Dictionary<string, object> e in emptied) tips.Add(Gd.Str(e["Tip"]));
                return idle("活动[" + string.Join("、", tips.ToArray()) + "]商店已标记领空 → 剩余理智改刷长草计划 " + idlePlan,
                            Gd.Str(emptied[0]["Id"]), Gd.Str(emptied[0]["Tip"]), true);
            }

            Dictionary<string, object> ev = runnable[0];
            string stage = "";
            foreach (object st in Gd.List(ev["Stages"]))
            {
                string drop = Gd.Str(Gd.Get(st, "Drop"));
                if (Regex.IsMatch(drop, @"^\d{3,6}$")) { stage = Gd.Str(Gd.Get(st, "Value")); break; }
            }
            if (stage.Length == 0) stage = Gd.Str(Gd.Get(Gd.List(ev["Stages"])[0], "Value"));

            string evKey = Gd.Str(ev["Id"]);
            string evTip = Gd.Str(ev["Tip"]);

            // ===== 每轮自证(2026-10-12 第 1 刀，判据由用户定档「进得去最后一关＝已通关」) =====
            // 上一轮"把活动最后一关递下去试打"的结论由现场侧读 MAA 两份日志得出(lib\ark-daily.ps1 收尾段
            // → state\ark-event-probe.json)，判定层照旧不读盘不读钟，只比"日期＋活动代号＋一个布尔"。
            // 名册一字未动(仍 idle / event / event-pending 三档、回执键集不变)⇒ 两枚契约号都不升；
            // 变的是 event-pending 的**进入条件与 ClearSanity**：旧="没敲 -AckArk 就整轮不烧理智"，
            // 新="今天试过、那关被 MAA 剪掉了 ⇒ 判为你还没解锁它 ⇒ 当日剩余轮改刷长草套餐(理智照烧、不再空试活动)"。
            // 为什么"被剪＝未解锁"这句敢写在判据里(Orchestration.cs 顶部那段两刀注释)：加载期那一刀按账号
            // 解锁进度修剪、GUI 那一刀按星期修剪；活动关不受周轮换限制，所以它被剪时只剩一个解释。
            // 实测锚(10-12 20:34 探针轮)：进得去时 gui 会话内是 `GetFightStage: from ["YW-8"], selected YW-8`、
            // asst 侧 Fight 链 first:["StageBegin"] 而非 LastOrCurBattleBegin(重放旧关=探针无效，另判)。
            // acked 那一路从此不再挡门，只留作"这台机器以前确认过"的历史痕迹读着不报错(见注脚)。
            Dictionary<string, object> probe = Gd.Map(Gd.Get(inp, "probe"));
            bool probedToday = probe != null
                && Gd.Str(Gd.Get(probe, "Date")) == Gd.Fmt(now, "yyyy-MM-dd")
                && Gd.Str(Gd.Get(probe, "EventKey")) == evKey;
            bool entered = probedToday && Gd.Truthy(Gd.Get(probe, "Entered"));

            if (probedToday && !entered)
            {
                string evi = Gd.Str(Gd.Get(probe, "Evidence"));
                string tail2 = emptied.Count > 0
                    ? "；另有 " + emptied.Count.ToString(CultureInfo.InvariantCulture) + " 个活动商店已领空，已跳过" : "";
                return Gd.Out(
                    "Mode", "event-pending", "Stage", idlePlan, "StageHint", stage, "ClearSanity", true,
                    "Reason", "活动[" + evTip + "](" + Gd.Str(ev["Group"]) + "/" + evKey + ") 今天试打过 " + stage
                              + "，被 MAA 剪掉 → 判为你还没打通到这关，当日剩余轮改刷长草计划 " + idlePlan
                              + "(理智照烧，不再空试；你打通后下一轮自动切回活动图，要现在就强刷：daily-clear.ps1 -Force)" + tail2
                              + (evi.Length > 0 ? "。证据：" + evi : ""),
                    "EventKey", evKey, "EventTip", evTip, "Source", source, "ShopDone", false);
            }

            string skipNote = emptied.Count > 0 ? "；另有 " + emptied.Count.ToString(CultureInfo.InvariantCulture) + " 个活动商店已领空，已跳过" : "";
            return Gd.Out(
                "Mode", "event", "Stage", stage, "StageHint", stage, "ClearSanity", true,
                "Reason", "活动[" + evTip + "](" + Gd.Fmt(ev["Start"]) + " ~ " + Gd.Fmt(ev["End"]) + ") "
                          + (entered ? "今天已自证进得去 " + stage + " → 理智全刷 " + stage
                                     : "本轮即以最后一关 " + stage + " 为探针：进得去就按活动图刷，被剪则当日改刷长草")
                          + skipNote,
                "EventKey", evKey, "EventTip", evTip, "Source", source, "ShopDone", false);
        }

        // ==================== decide-r1999：原 lib\r1999-decisions.ps1 的 Resolve-R1999Plan ====================
        public static object R1999(Dictionary<string, object> inp)
        {
            object re = Gd.Get(inp, "re1999");
            long now = Gd.Lng(Gd.Get(inp, "now"));
            object data = Gd.Get(inp, "data");
            Dictionary<string, object> ack = Gd.Map(Gd.Get(inp, "ack")) ?? new Dictionary<string, object>();
            Dictionary<string, object> shopDone = Gd.Map(Gd.Get(inp, "shopDone")) ?? new Dictionary<string, object>();

            Dictionary<string, object> versions = Gd.Map(data) ?? new Dictionary<string, object>();
            KeyValuePair<string, object> cur = new KeyValuePair<string, object>();
            bool hasCur = false;
            foreach (KeyValuePair<string, object> p in versions)
            {
                if (Gd.Lng(Gd.Get(p.Value, "start_time")) <= now && now <= Gd.Lng(Gd.Get(p.Value, "end_time"))) { cur = p; hasCur = true; break; }
            }

            Dictionary<string, object> result = Gd.Out(
                "Mode", "idle", "Version", null, "VersionName", null, "EventKey", null, "EventTip", null,
                "Reason", "", "Instance", "idle", "DataAge", null, "ShopDone", false);

            if (!hasCur)
            {
                long latest = 0;
                foreach (KeyValuePair<string, object> p in versions) { if (Gd.Lng(Gd.Get(p.Value, "end_time")) > latest) latest = Gd.Lng(Gd.Get(p.Value, "end_time")); }
                string note = latest < now ? "；活动数据只记录到 " + Gd.Fmt(latest, "yyyy-MM-dd") + "，可能未更新" : "";
                result["Reason"] = "不在任何已记录版本的窗口内 → 长草" + note;
                return result;
            }

            result["Version"] = cur.Key;
            result["VersionName"] = Gd.Str(Gd.Get(cur.Value, "version_name"));
            string vName = (string)result["VersionName"];
            object activity = Gd.Get(cur.Value, "activity");
            object combat = Gd.Get(activity, "combat");
            object rerun = Gd.Get(activity, "re-release");

            if (combat != null && Gd.Lng(Gd.Get(combat, "start_time")) <= now && now <= Gd.Lng(Gd.Get(combat, "end_time")))
            {
                bool isMain = Regex.IsMatch(Gd.Str(Gd.Get(combat, "event_type")), "MainStory");
                string key = cur.Key + "-combat";
                result["EventKey"] = key;
                result["EventTip"] = vName;
                if (shopDone.ContainsKey(key))
                {
                    result["ShopDone"] = true;
                    result["Reason"] = "版本 " + cur.Key + "[" + vName + "]活动商店已标记领空 → 本轮跑长草方案";
                    return result;
                }
                if (isMain && Gd.Truthy(Gd.Get(re, "mainStoryRequiresAck")) && !ack.ContainsKey(key))
                {
                    result["Mode"] = "event-pending";
                    result["Instance"] = "idle";
                    result["Reason"] = "版本 " + cur.Key + "[" + vName + "]主线活动进行中(至 " + Gd.Fmt(Gd.Get(combat, "end_time"))
                                       + ")，未确认手动通关 → 本轮跑长草方案";
                }
                else
                {
                    result["Mode"] = "event";
                    result["Instance"] = "event";
                    result["Reason"] = "版本 " + cur.Key + "[" + vName + "]活动进行中(" + Gd.Fmt(Gd.Get(combat, "start_time"))
                                       + " ~ " + Gd.Fmt(Gd.Get(combat, "end_time")) + ")";
                }
                return result;
            }
            if (rerun != null && Gd.Lng(Gd.Get(rerun, "start_time")) <= now && now <= Gd.Lng(Gd.Get(rerun, "end_time")))
            {
                result["Mode"] = "rerun";
                result["Instance"] = "rerun";
                result["EventTip"] = vName + " 复刻";
                result["Reason"] = "版本 " + cur.Key + " 复刻进行中(" + Gd.Fmt(Gd.Get(rerun, "start_time")) + " ~ " + Gd.Fmt(Gd.Get(rerun, "end_time")) + ")";
                return result;
            }
            result["Reason"] = "版本 " + cur.Key + "[" + vName + "]当前无进行中活动(版本窗口 " + Gd.Fmt(Gd.Get(cur.Value, "start_time"))
                               + " ~ " + Gd.Fmt(Gd.Get(cur.Value, "end_time")) + ")→ 长草";
            return result;
        }

        // ==================== check-stages：原 Test-GdStages 的判定部分(关卡表由 PS 侧读好喂进来) ====================
        public static object CheckStages(Dictionary<string, object> inp)
        {
            Dictionary<string, object> table = Gd.Map(Gd.Get(inp, "table")) ?? new Dictionary<string, object>();
            string stageText = Gd.Str(Gd.Get(inp, "stageText"));
            List<string> list = new List<string>();
            foreach (string s in Regex.Split(stageText, @"[,，、;；\s]+"))
                if (s.Length > 0) list.Add(s);
            List<string> bad = new List<string>();
            int cost = 0;
            foreach (string s in list)
            {
                if (!table.ContainsKey(s)) bad.Add(s);
                else cost += Gd.Int(table[s], 0);
            }
            string note;
            if (table.Count == 0) note = "拿不到 MAA 关卡表，未校验";
            else if (bad.Count > 0) note = "未知关卡：" + string.Join(", ", bad.ToArray());
            else note = "共 " + list.Count.ToString(CultureInfo.InvariantCulture) + " 关，单圈理智 " + cost.ToString(CultureInfo.InvariantCulture);
            return Gd.Out("Stages", list.ToArray(), "Bad", bad.ToArray(), "Cost", cost, "Note", note);
        }

        // ==================== r1999-instance：原 Resolve-R1999Instance 的扫描部分(两道前置检查留在 PS 壳) ====================
        public static object R1999Instance(Dictionary<string, object> inp)
        {
            string dir = Gd.Str(Gd.Get(inp, "dir"));
            string want = Gd.Str(Gd.Get(inp, "want"));
            string planKey = Gd.Str(Gd.Get(inp, "planKey"));
            string[] files = Directory.GetFiles(dir, "*.json");
            Array.Sort(files, StringComparer.Ordinal);   // Get-ChildItem 默认按名序，这边对齐它
            foreach (string f in files)
            {
                object cfg = GdJson.Read(f);
                if (Gd.Str(Gd.Get(cfg, "InstanceName")) != want) continue;
                List<string> tasks = new List<string>();
                foreach (object t in Gd.List(Gd.Get(cfg, "CurrentTasks")))
                {
                    string[] seg = Gd.Str(t).Split(new string[] { "<||>" }, StringSplitOptions.None);
                    tasks.Add(seg[seg.Length - 1]);
                }
                return Gd.Out(
                    "Id", Path.GetFileNameWithoutExtension(f),
                    "Name", want,
                    "Path", f,
                    "Tasks", tasks.ToArray());
            }
            throw new GdRuleException("M9A 中找不到名为[" + want + "]的实例(计划 " + planKey + ")");
        }

        // ==================== resolve-screensaver-who / resolve-screensaver-verdict(批次 10，原 lib\common.ps1 屏保两档) ====================
        // 基线真身 = 原 common.ps1 Get-GdScreensaverWho / Get-GdScreensaverVerdict(钉 commit e269d13；
        // 转接刀·屏保稳态族 2026-11-03 已把那两枚壳连同 PS 侧真身一起摘掉，本处自此是唯一事实源)。
        // 注册表读数(HKCU\Control Panel\Desktop)也不再由 PS 侧喂：现由 read-screensaver-config(Round.cs::ScreensaverConfig)
        // 现答；Found:false 那一折归调用方(status.ps1／ark-daily／unarm-screensaver／test-lib 各自现折成 cfg=null)。
        // 四档判据顺序是认真的(test-lib [t1g] 逐档钉着)：unknown(读不到→不吓唬人也不说没事) → none(没挂 .scr) →
        // armed(挂且武装，唯一 Risk=true) → off(挂但解除 —— 不是终态，长话要把"会被重新武装"说明白)。
        // 文案与 test-lib 的三条硬约束：短话不带箭头且 ≤30 字装得进卡片；armed 长话必须点名 unarm-screensaver；
        // off 长话必须带"不是终态"。C# 侧一改这些串，test-lib/golden 双红，改期望是禁手。

        // Who 的 C# 真身：两档 verb 共用(旧 verdict 第一行就是调它)。$Cfg 用 Gd.Ci 宽容键大小写(旧代码是 PS 属性访问)。
        static string ScreensaverWho(object cfgRaw)
        {
            Dictionary<string, object> cfg = Gd.Ci(cfgRaw);
            if (cfg == null) return "";                       // -not $Cfg：$null/非对象都算"没有"
            string saver = Gd.Str(Gd.Get(cfg, "Saver"));
            if (saver.Length == 0) return "";                 // -not $Cfg.Saver
            string prod = Gd.Str(Gd.Get(cfg, "Product"));
            if (System.Text.RegularExpressions.Regex.IsMatch(prod, @"wallpaper\s*engine",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return "Wallpaper Engine";
            if (prod.Length > 0) return prod;
            try { return Path.GetFileNameWithoutExtension(saver); }   // 面板那行要给人名，不印 8.3 短名
            catch { return saver; }
        }

        // ==================== verb: convert-utc8-to-ms ／ format-unix-ms ====================
        // 这两枚出口一格规则都不写：只是把上面的 Gd.Utc8ToMs／Gd.Fmt 露成信封，给 PS 侧那两枚同规则真身出仓用。
        // null 原样出信封(PS 侧 ConvertFrom-Json 收到 $null，与旧 PS 实现的返回形状同形)；
        // pattern 不给就走 Fmt 的单参版(MM-dd HH:mm)，"假值(含 0)一律 '?'"那条兜底也留在 Fmt 里，不在这儿重抄一遍。
        public static object Utc8ToMsV(Dictionary<string, object> inp)
        {
            return Gd.Out("Ms", Gd.Utc8ToMs(Gd.Get(inp, "text")));
        }

        public static object UnixMsFmtV(Dictionary<string, object> inp)
        {
            object ms = Gd.Get(inp, "ms");
            string pattern = Gd.Str(Gd.Get(inp, "pattern"));
            return Gd.Out("Text", pattern.Length == 0 ? Gd.Fmt(ms) : Gd.Fmt(ms, pattern));
        }

        public static object ScreensaverWhoV(Dictionary<string, object> inp)
        {
            return Gd.Out("Text", ScreensaverWho(Gd.Get(inp, "cfg")));
        }

        public static object ScreensaverVerdict(Dictionary<string, object> inp)
        {
            object cfgRaw = Gd.Get(inp, "cfg");
            string who = ScreensaverWho(cfgRaw);   // 旧代码第一行就调它(unknown 档也算)，顺序照旧
            Dictionary<string, object> cfg = Gd.Ci(cfgRaw);
            if (cfg == null)
                return Gd.Out(
                    "Kind", "unknown", "Risk", false,
                    "Text", "屏保：注册表读不到，判断不了",
                    "Detail", "屏保：读不到 HKCU\\Control Panel\\Desktop，判断不了武装没有(不代表没事)");
            string saver = Gd.Str(Gd.Get(cfg, "Saver"));
            if (saver.Length == 0)
                return Gd.Out(
                    "Kind", "none", "Risk", false,
                    "Text", "屏保：没挂 .scr(不会接管会话)",
                    "Detail", "屏保：系统里没有指定屏幕保护程序 → 到点不会有任何东西盖住端游窗口，06:00 那一路安全");
            int secs = Gd.Int(Gd.Get(cfg, "TimeoutSec"), 0);
            int floorSec = Gd.Int(Gd.Get(inp, "floorSec"), 0);
            if (floorSec > 0 && secs < floorSec) secs = floorSec;   // [int] 强转后才是数值语义，字符串值走 Gd.Int 同旧
            string whoSecs = (who.Length > 0 ? who : "某个 .scr") + "/" + secs + "秒";
            string shortT = "屏保：" + whoSecs;
            if (Gd.Str(Gd.Get(cfg, "Active")) == "1")
                return Gd.Out(
                    "Kind", "armed", "Risk", true,
                    "Text", shortT + " 仍武装",
                    "Detail", "屏保：" + whoSecs + " 仍武装 → 到点会接管输入桌面，方舟那一路起跑前置判定会直接早退 not-started；" +
                              "解法：tools\\unarm-screensaver.ps1 -Apply(或 设置→个性化→锁屏界面→屏幕保护程序→选「(无)」)");
            return Gd.Out(
                "Kind", "off", "Risk", false,
                "Text", shortT + " 已解除",
                "Detail", "屏保：" + whoSecs + "，武装位是 0 → 到点不触发(日常\"只关不复\"就停在这档)。" +
                          "注意它不是终态：" + (who.Length > 0 ? who : "挂屏保的那个程序") +
                          " 的设置里仍勾着屏保时，改一次设置就可能被重新武装，体检看到这行变成\"仍武装\"就是这个原因");
        }
    }
}
