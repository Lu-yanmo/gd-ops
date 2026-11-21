// tools/panel-cs/Decisions.SelfTest.cs —— --dectest：批次 1 判定层的无头自检。
// 用例镜像 tools\test-ark.ps1 [t7] / test-r1999 的判定场景，但全部喂临时树 + 内存假数据：
// 不读真配置、不碰 state\ 里的确认门(那套 Save/Restore 隔离是 PS 壳侧的事，C# 侧本来就无状态)。
// 断言里的中文串是从 lib\ark-decisions.ps1 / lib\r1999-decisions.ps1 原话抄来的 ——
// 这里红了就是 C# 移植走样，改 Decisions.cs，不改这里的期望串。
// ⚠ 10-26 甲档第八刀：屏保那一族 14 档自 tools\probe-decisions-golden.ps1 退休前最后一次全绿读数逐字搬来这里
//    (old 侧钉 e269d13 裁出的旧真身、new 侧走现树壳 → C# verb，两侧 14 档逐字符全等)；场景名与门里逐条同名。
//    那 14 条同样是"改 C# 才动这里"，不要改期望去迁就实现。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GdPanel
{
    static class GdDecisionsSelfTest
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

        // ---------- 假数据小工具 ----------
        static long Ms(string wall) { return Gd.Utc8ToMs(wall).Value; }

        static Dictionary<string, object> Stage(string value, string drop)
        {
            return Gd.Out("Value", value, "Drop", drop);
        }

        static Dictionary<string, object> Activity(string tip, string start, string end)
        {
            return Gd.Out("Tip", tip, "UtcStartTime", start, "UtcExpireTime", end);
        }

        // 一个"分组项"：{ Activity = …, Stages = […] }，与 MAA 缓存同构
        static Dictionary<string, object> EventObj(string tip, string start, string end, params Dictionary<string, object>[] stages)
        {
            return Gd.Out("Activity", Activity(tip, start, end), "Stages", stages);
        }

        static Dictionary<string, object> ArkIn(object data, long now, string source,
            Dictionary<string, object> ack, Dictionary<string, object> shopDone, int dow)
        {
            return ArkIn(data, now, source, ack, shopDone, dow, null);
        }

        // 第 7 枚入参 probe = 上一轮"拿活动最后一关当探针试打"的现场结论(state\ark-event-probe.json 同构)。
        // ack / verbAck 判定层已不读(10-12 第 1 刀)；⚠ 10-30 B-3②-d 刀① 起 verbAck／verbShopReset 两枚键从 body 摘掉，壳侧不传、判定层连读取一并摘 ⇒ 夹具跟着不再喂(喂着反而不像真输入)。decide-r1999 段末尾那格「防回归」是唯一例外，它故意手构一份带这两枚键的 body，钉"就算壳侧误传也不进文案"。
        static Dictionary<string, object> ArkIn(object data, long now, string source,
            Dictionary<string, object> ack, Dictionary<string, object> shopDone, int dow,
            Dictionary<string, object> probe)
        {
            if (ack == null) ack = new Dictionary<string, object>();
            if (shopDone == null) shopDone = new Dictionary<string, object>();
            Dictionary<string, object> ark = Gd.Out(
                "defaultStage", "CE-5,LS-5",
                "planByWeekday", Gd.Out("3", "LS-6"));
            return Gd.Out(
                "ark", ark, "dow", dow, "now", now, "data", data, "source", source,
                "ack", ack, "shopDone", shopDone,
                "probe", probe);
        }

        // 探针结论的现场形状（判定层只认 Date / EventKey / Entered / Evidence 四格，Stage 是留给日志看的）
        static Dictionary<string, object> ProbeToday(string key, bool entered, string evidence)
        {
            return Gd.Out("Date", "2026-09-23", "EventKey", key, "Stage", "AI-8",
                "Entered", entered, "Evidence", evidence);
        }

        static Dictionary<string, object> SideStory(object groups)
        {
            return Gd.Out("Official", Gd.Out("sideStory", groups));
        }

        // 屏保 14 档逐字投影的助手(10-26 甲档第八刀，原 probe-decisions-golden 的 saver 一族全量搬入)：
        // SaverCfg 复刻门里 SaverCfg 的四个键序；SaverWho / SaverVerdict 复刻门里"直接取值"与 SaverStr 的固定序投影。
        static object SaverCfg(string saver, object product, object active, object timeout)
        {
            return Gd.Out("Saver", saver, "Product", product, "Active", active, "TimeoutSec", timeout);
        }
        static string SaverWho(object cfg)
        {
            return Gd.Str(Gd.Get(Gd.Map(Decisions.ScreensaverWhoV(Gd.Out("cfg", cfg))), "Text"));
        }
        static string SaverVerdict(object cfg, int floorSec)
        {
            Dictionary<string, object> v = Gd.Map(Decisions.ScreensaverVerdict(Gd.Out("cfg", cfg, "floorSec", floorSec)));
            return "K=" + Gd.Str(v["Kind"]) + "|R=" + (((bool)v["Risk"]) ? "True" : "False")
                + "|T=" + Gd.Str(v["Text"]) + "|D=" + Gd.Str(v["Detail"]);
        }

        // ============================ --dectest ============================
        public static int DecTest()
        {
            GdCli.TryAttachConsole();
            Box b = new Box();
            string tmp = Path.Combine(Path.GetTempPath(),
                "gd-dec-" + Process.GetCurrentProcess().Id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-"
                + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(tmp);
            try
            {
                GdEnv.InitForTest(tmp);

                // 固定的时间线：所有期望串里的 "MM-dd HH:mm" 都从这几个墙上时刻来
                long start = Ms("2026-09-20 04:00:00");
                long end = Ms("2026-09-26 18:00:00");
                long now = Ms("2026-09-23 12:00:00");

                // ---------- Gd 底座语义 ----------
                b.Chk("Truthy：null/空串/0/空集合假，其余真", delegate
                {
                    Dictionary<string, object> empty = new Dictionary<string, object>();
                    return !Gd.Truthy(null) && !Gd.Truthy("") && !Gd.Truthy(0) && !Gd.Truthy(0L)
                        && !Gd.Truthy(empty) && !Gd.Truthy(new object[0])
                        && Gd.Truthy(true) && Gd.Truthy("x") && Gd.Truthy(3) && Gd.Truthy(new object[] { 1 });
                });
                b.Chk("Utc8ToMs：/ 换 -、多空格压缩、坏串 null", delegate
                {
                    return Gd.Utc8ToMs("2026/09/20 04:00:00") == start
                        && Gd.Utc8ToMs("2026-09-20   04:00:00") == start
                        && Gd.Utc8ToMs("2026-09-26") == Ms("2026-09-26 00:00:00")
                        && Gd.Utc8ToMs("") == null && Gd.Utc8ToMs(null) == null && Gd.Utc8ToMs("乱写的") == null;
                });
                b.Chk("Fmt：0/假值 → '?'，UTC+8 展示", delegate
                {
                    return Gd.Fmt(start) == "09-20 04:00" && Gd.Fmt(0) == "?" && Gd.Fmt(null) == "?"
                        && Gd.Fmt(start, "yyyy-MM-dd") == "2026-09-20";
                });
                b.Chk("Str：null→\"\"，bool→True/False(PS 口径)", delegate
                {
                    return Gd.Str(null) == "" && Gd.Str(true) == "True" && Gd.Str(false) == "False" && Gd.Str(12) == "12";
                });
                b.Chk("Out 保序(键顺序即对拍协议)", delegate
                {
                    List<string> keys = new List<string>(Gd.Out("b", 1, "a", 2, "c", 3).Keys);
                    return keys[0] == "b" && keys[1] == "a" && keys[2] == "c";
                });

                // ---------- decide-ark：长草 / 每轮自证 / 领空 / 提示语 ----------
                b.Chk("ark 无活动 → 长草计划(planByWeekday 没有的 dow 回落 defaultStage)", delegate
                {
                    Dictionary<string, object> a5 = Gd.Map(Decisions.Ark(ArkIn(SideStory(Gd.Out()), now, "假数据", null, null, 5)));
                    Dictionary<string, object> a1 = Gd.Map(Decisions.Ark(ArkIn(SideStory(Gd.Out()), now, "假数据", null, null, 1)));
                    return (string)a5["Mode"] == "idle" && (string)a5["Stage"] == "CE-5,LS-5" && (bool)a5["ClearSanity"]
                        && (string)a5["Reason"] == "无可刷活动关卡，按长草计划刷 CE-5,LS-5(数据源：假数据)"
                        && (string)a1["Stage"] == "CE-5,LS-5";   // dow=1 表里没有 → defaultStage
                });
                b.Chk("ark 周六命中 planByWeekday['3'] 之外的键就用默认(此路只测 3)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(Gd.Out()), now, "假数据", null, null, 3)));
                    return (string)r["Stage"] == "LS-6"
                        && (string)r["Reason"] == "无可刷活动关卡，按长草计划刷 LS-6(数据源：假数据)";
                });
                b.Chk("ark source 不给 → '无数据'", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(Gd.Out()), now, "", null, null, 5)));
                    return ((string)r["Reason"]).EndsWith("(数据源：无数据)");
                });
                Dictionary<string, object> oneGrp = Gd.Out("zztest-event",
                    EventObj("测试活动", "2026-09-20 04:00:00", "2026-09-26 18:00:00",
                        Stage("AI-8", "10001"), Stage("CE-5", "3004")));
                b.Chk("ark 没探过(probe 缺) → event：本轮即以最后一关为探针，理智照刷", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5)));
                    return (string)r["Mode"] == "event" && (bool)r["ClearSanity"] && (string)r["Stage"] == "AI-8"
                        && (string)r["Reason"] == "活动[测试活动](09-20 04:00 ~ 09-26 18:00) 本轮即以最后一关 AI-8 为探针：进得去就按活动图刷，被剪则当日改刷长草"
                        && (string)r["EventKey"] == "zztest-event" && !(bool)r["ShopDone"];
                });
                b.Chk("ark 当日自证进得去 → event：文案写'今天已自证'、理智全刷代币关", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(
                        ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5, ProbeToday("zztest-event", true, ""))));
                    return (string)r["Mode"] == "event" && (bool)r["ClearSanity"] && (string)r["Stage"] == "AI-8"
                        && (string)r["StageHint"] == "AI-8"
                        && (string)r["Reason"] == "活动[测试活动](09-20 04:00 ~ 09-26 18:00) 今天已自证进得去 AI-8 → 理智全刷 AI-8";
                });
                b.Chk("ark 当日试打被剪 → event-pending：改刷长草但理智照烧、提示语指 -Force", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(
                        ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5, ProbeToday("zztest-event", false, "名单里没有 AI-8"))));
                    return (string)r["Mode"] == "event-pending" && (bool)r["ClearSanity"]
                        && (string)r["Stage"] == "CE-5,LS-5" && (string)r["StageHint"] == "AI-8"
                        && (string)r["Reason"] == "活动[测试活动](sideStory/zztest-event) 今天试打过 AI-8，被 MAA 剪掉 → 判为你还没打通到这关，当日剩余轮改刷长草计划 CE-5,LS-5(理智照烧，不再空试；你打通后下一轮自动切回活动图，要现在就强刷：daily-clear.ps1 -Force)。证据：名单里没有 AI-8"
                        && (string)r["EventKey"] == "zztest-event" && !(bool)r["ShopDone"];
                });
                b.Chk("ark 被剪但现场没抓到证据串 → 文案不带'。证据：'尾巴", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(
                        ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5, ProbeToday("zztest-event", false, ""))));
                    return (string)r["Mode"] == "event-pending" && !((string)r["Reason"]).Contains("。证据：");
                });
                b.Chk("ark 昨天的结论不作数(日期不等) → 重新拿最后一关当探针", delegate
                {
                    Dictionary<string, object> p = ProbeToday("zztest-event", false, "");
                    p["Date"] = "2026-09-22";
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5, p)));
                    return (string)r["Mode"] == "event" && (string)r["Stage"] == "AI-8"
                        && ((string)r["Reason"]).Contains("本轮即以最后一关 AI-8 为探针");
                });
                b.Chk("ark 结论属于别的活动(代号不等) → 不吃这条", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(
                        ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5, ProbeToday("zztest-event2", false, ""))));
                    return (string)r["Mode"] == "event" && (string)r["Stage"] == "AI-8";
                });
                b.Chk("ark 唯一活动已领空 → 回落长草 + ShopDone(10-30 B-3②-d 刀① 起这句不再带恢复用的 verb 名)", delegate
                {
                    Dictionary<string, object> ack = Gd.Out("zztest-event", true);
                    Dictionary<string, object> sd = Gd.Out("zztest-event", true);
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(oneGrp), now, "假数据", ack, sd, 5)));
                    return (string)r["Mode"] == "idle" && (bool)r["ShopDone"] && (bool)r["ClearSanity"]
                        && (string)r["EventKey"] == "zztest-event"   // 领空后仍能报出活动 id(供撤销)
                        && (string)r["Reason"] == "活动[测试活动]商店已标记领空 → 剩余理智改刷长草计划 CE-5,LS-5(数据源：假数据)";
                });
                b.Chk("ark 两活动：A 领空 + 当日 B 被剪 → pending 且带'另有 1 个…已跳过'尾巴", delegate
                {
                    Dictionary<string, object> grp = Gd.Out(
                        "zztest-event", EventObj("测试活动A", "2026-09-20 04:00:00", "2026-09-26 18:00:00", Stage("AI-8", "10001")),
                        "zztest-event2", EventObj("测试活动B", "2026-09-20 04:00:00", "2026-09-29 04:00:00", Stage("LS-5", "3004")));
                    Dictionary<string, object> sd = Gd.Out("zztest-event", true);
                    Dictionary<string, object> p = ProbeToday("zztest-event2", false, "");
                    p["Stage"] = "LS-5";
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", null, sd, 5, p)));
                    return (string)r["Mode"] == "event-pending" && (bool)r["ClearSanity"]
                        && (string)r["EventKey"] == "zztest-event2" && (string)r["Stage"] == "CE-5,LS-5"
                        && (string)r["Reason"] == "活动[测试活动B](sideStory/zztest-event2) 今天试打过 LS-5，被 MAA 剪掉 → 判为你还没打通到这关，当日剩余轮改刷长草计划 CE-5,LS-5(理智照烧，不再空试；你打通后下一轮自动切回活动图，要现在就强刷：daily-clear.ps1 -Force)；另有 1 个活动商店已领空，已跳过";
                });
                b.Chk("ark 两活动：A 领空 + B 未探过 → 自动改刷 B 并带跳过说明", delegate
                {
                    Dictionary<string, object> grp = Gd.Out(
                        "zztest-event", EventObj("测试活动A", "2026-09-20 04:00:00", "2026-09-26 18:00:00", Stage("AI-8", "10001")),
                        "zztest-event2", EventObj("测试活动B", "2026-09-20 04:00:00", "2026-09-29 04:00:00", Stage("LS-5", "3004")));
                    Dictionary<string, object> sd = Gd.Out("zztest-event", true);
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", null, sd, 5)));
                    return (string)r["Mode"] == "event" && (string)r["EventKey"] == "zztest-event2" && (string)r["Stage"] == "LS-5"
                        && ((string)r["Reason"]).EndsWith("；另有 1 个活动商店已领空，已跳过");
                });
                b.Chk("ark 两活动：A 领空 + 当日 B 进得去 → 刷 B，文案写'今天已自证'", delegate
                {
                    Dictionary<string, object> grp = Gd.Out(
                        "zztest-event", EventObj("测试活动A", "2026-09-20 04:00:00", "2026-09-26 18:00:00", Stage("AI-8", "10001")),
                        "zztest-event2", EventObj("测试活动B", "2026-09-20 04:00:00", "2026-09-29 04:00:00", Stage("LS-5", "3004")));
                    Dictionary<string, object> sd = Gd.Out("zztest-event", true);
                    Dictionary<string, object> p = ProbeToday("zztest-event2", true, "");
                    p["Stage"] = "LS-5";
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", null, sd, 5, p)));
                    return (string)r["Mode"] == "event" && (string)r["Stage"] == "LS-5" && (bool)r["ClearSanity"]
                        && (string)r["Reason"] == "活动[测试活动B](09-20 04:00 ~ 09-29 04:00) 今天已自证进得去 LS-5 → 理智全刷 LS-5；另有 1 个活动商店已领空，已跳过";
                });
                // ---------- 刀㉑ 笔(b)-2：test-ark 的 t7 段那 19 枚，逐枚对面补齐(其余十四枚由上面既有格承接) ----------
                // 分工口径照在册：判定层语义(Mode／Stage／StageHint／Reason 话术)归 --dectest；
                // 「领空表给不给」那一折(IgnoreShopDone／shopDoneStopsEvent)的真身在 compose-decision-inputs⇒归 --roundtest 新节 34)。
                b.Chk("ark 探针档把活动关卡留在 StageHint(收尾采集照它点名)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5)));
                    return (string)r["Mode"] == "event" && (string)r["StageHint"] == "AI-8";
                });
                b.Chk("ark 探针档提示语讲清「为探针」，且整段不出现 Ack 参数名(反向钉：出现即红)", delegate
                {
                    string s = (string)Gd.Map(Decisions.Ark(ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5)))["Reason"];
                    return s.Contains("为探针") && !s.Contains("Ack");
                });
                b.Chk("ark 被剪臂提示语三句齐(被 MAA 剪掉／不空试／下一轮自动切回)且不出现 AckArk(反向钉)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(
                        ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5, ProbeToday("zztest-event", false, "名单里没有 AI-8"))));
                    string s = (string)r["Reason"];
                    return s.Contains("被 MAA 剪掉") && s.Contains("下一轮自动切回") && !s.Contains("AckArk");
                });
                b.Chk("ark 领空回落提示语说「剩余理智改刷长草计划」且不出现 daily-clear(10-30 B-3②-d 刀① 摘 body 两枚键的反向钉)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(
                        ArkIn(SideStory(oneGrp), now, "假数据", null, Gd.Out("zztest-event", true), 5)));
                    string s = (string)r["Reason"];
                    return s.Contains("剩余理智改刷长草计划") && !s.Contains("daily-clear");
                });
                b.Chk("ark 两活动都领空 → 长草计划且 ShopDone 为真，EventKey 报先结束那枚(供撤销)", delegate
                {
                    Dictionary<string, object> grp = Gd.Out(
                        "zztest-event", EventObj("测试活动A", "2026-09-20 04:00:00", "2026-09-26 18:00:00", Stage("AI-8", "10001")),
                        "zztest-event2", EventObj("测试活动B", "2026-09-20 04:00:00", "2026-09-29 04:00:00", Stage("LS-5", "3004")));
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", null,
                        Gd.Out("zztest-event", true, "zztest-event2", true), 5)));
                    return (string)r["Mode"] == "idle" && (bool)r["ShopDone"] && (bool)r["ClearSanity"]
                        && (string)r["Stage"] == "CE-5,LS-5" && (string)r["EventKey"] == "zztest-event"
                        && (string)r["Reason"] == "活动[测试活动A、测试活动B]商店已标记领空 → 剩余理智改刷长草计划 CE-5,LS-5(数据源：假数据)";
                });

                b.Chk("ark 多活动按结束先后排队(先刷快结束的)", delegate
                {
                    Dictionary<string, object> grp = Gd.Out(
                        "late", EventObj("晚结束", "2026-09-20 04:00:00", "2026-09-28 04:00:00", Stage("LS-6", "10002")),
                        "early", EventObj("早结束", "2026-09-20 04:00:00", "2026-09-25 04:00:00", Stage("AI-8", "10001")));
                    Dictionary<string, object> ack = Gd.Out("late", true, "early", true);
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", ack, null, 5)));
                    return (string)r["EventKey"] == "early" && (string)r["Stage"] == "AI-8";
                });
                b.Chk("ark 代币关挑选：Drop 非纯数字的跳过；全非数字取第一个", delegate
                {
                    Dictionary<string, object> grp = Gd.Out(
                        "e1", EventObj("挑关", "2026-09-20 04:00:00", "2026-09-26 18:00:00",
                            Stage("NT-1", "task_flag"), Stage("AI-8", "10003"), Stage("CE-5", "10004")),
                        "e2", EventObj("全坏", "2026-09-20 04:00:00", "2026-09-25 18:00:00",
                            Stage("LS-5", "task_flag"), Stage("SK-2", "")));
                    Dictionary<string, object> r1 = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", Gd.Out("e1", true, "e2", true), null, 5)));
                    // e2 结束更早 → 排序在前；其 Stages 无数字 Drop → 取第一个 LS-5
                    Dictionary<string, object> r2 = Gd.Map(Decisions.Ark(ArkIn(SideStory(Gd.Out("e1",
                        EventObj("挑关", "2026-09-20 04:00:00", "2026-09-26 18:00:00",
                            Stage("NT-1", "task_flag"), Stage("AI-8", "10003"), Stage("CE-5", "10004")))),
                        now, "假数据", Gd.Out("e1", true), null, 5)));
                    return (string)r1["EventKey"] == "e2" && (string)r1["Stage"] == "LS-5"
                        && (string)r2["Stage"] == "AI-8";   // 有数字 Drop → 跳过 NT-1 取 AI-8
                });
                b.Chk("ark 分组值是数组(item0…)也能吃；无 Activity 包装也能吃", delegate
                {
                    Dictionary<string, object> bare = Gd.Out(
                        "Tip", "裸活动", "UtcStartTime", "2026-09-20 04:00:00", "UtcExpireTime", "2026-09-26 18:00:00",
                        "Stages", new object[] { Stage("AI-8", "10001") });
                    Dictionary<string, object> data = Gd.Out("Official", Gd.Out("sideStory", new object[] { bare }));
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(data, now, "假数据", Gd.Out("item0", true), null, 5)));
                    return (string)r["Mode"] == "event" && (string)r["EventKey"] == "item0" && (string)r["Stage"] == "AI-8";
                });
                b.Chk("ark 时间窗外的活动不参与(结束在昨天/开始在未来)", delegate
                {
                    Dictionary<string, object> grp = Gd.Out(
                        "past", EventObj("已结束", "2026-09-10 04:00:00", "2026-09-15 04:00:00", Stage("AI-8", "10001")),
                        "future", EventObj("未开始", "2026-09-28 04:00:00", "2026-09-30 04:00:00", Stage("LS-6", "10002")));
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", Gd.Out("past", true, "future", true), null, 5)));
                    return (string)r["Mode"] == "idle";
                });
                b.Chk("ark 数据整个缺(data=null) → 长草计划，不炸", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(null, now, "假数据", null, null, 5)));
                    return (string)r["Mode"] == "idle" && (string)r["Stage"] == "CE-5,LS-5" && (bool)r["ClearSanity"]
                        && (string)r["Reason"] == "无可刷活动关卡，按长草计划刷 CE-5,LS-5(数据源：假数据)"
                        && r["EventKey"] == null && r["EventTip"] == null;
                });
                b.Chk("ark 起止时间是空串 → 当没填跳过(不按 0 算窗口内)", delegate
                {
                    Dictionary<string, object> grp = Gd.Out("e1", EventObj("空时间", "", "", Stage("AI-8", "10001")));
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", null, null, 5)));
                    return (string)r["Mode"] == "idle" && r["EventKey"] == null
                        && (string)r["Reason"] == "无可刷活动关卡，按长草计划刷 CE-5,LS-5(数据源：假数据)";
                });
                b.Chk("ark 起止时间是 1970 零值(ms=0) → 与没填同判跳过(原话 -not $start：0 也算没填)", delegate
                {
                    // 1970-01-01 08:00:00 按 UTC+8 折成 ms=0；起点/终点各喂一枚，两枚都该被 Truthy 判假跳过。
                    Dictionary<string, object> grp = Gd.Out(
                        "zero-start", EventObj("零值起点", "1970-01-01 08:00:00", "2026-09-26 18:00:00", Stage("AI-8", "10001")),
                        "zero-end", EventObj("零值终点", "2026-09-20 04:00:00", "1970-01-01 08:00:00", Stage("LS-5", "10002")));
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", null, null, 5)));
                    return (string)r["Mode"] == "idle" && r["EventKey"] == null;
                });
                b.Chk("ark 空 Stages 的活动不算可刷", delegate
                {
                    Dictionary<string, object> grp = Gd.Out("e", EventObj("没关", "2026-09-20 04:00:00", "2026-09-26 18:00:00"));
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(SideStory(grp), now, "假数据", null, null, 5)));
                    return (string)r["Mode"] == "idle" && (string)r["EventKey"] == null;
                });
                b.Chk("ark 材料加倍期 → 长草 Reason 追加提示", delegate
                {
                    // 真 MAA 数据里 resourceCollection 直接带 Tip/UtcStartTime/UtcExpireTime(它不是"分组→项"两层结构)
                    Dictionary<string, object> data = Gd.Out("Official", Gd.Out(
                        "sideStory", new Dictionary<string, object>(),
                        "resourceCollection", Activity("材料加倍", "2026-09-22 04:00:00", "2026-09-24 04:00:00")));
                    Dictionary<string, object> r = Gd.Map(Decisions.Ark(ArkIn(data, now, "假数据", null, null, 5)));
                    return (string)r["Mode"] == "idle"
                        && (string)r["Reason"] == "无可刷活动关卡，按长草计划刷 CE-5,LS-5(数据源：假数据)；正逢[材料加倍]材料加倍期(至 09-24 04:00)，刷材料本效率翻倍";
                });

                // ---------- decide-r1999 ----------
                Func<Dictionary<string, object>, Dictionary<string, object>, Dictionary<string, object>, bool, Dictionary<string, object>> rIn =
                    delegate(Dictionary<string, object> data, Dictionary<string, object> ack, Dictionary<string, object> sd, bool requiresAck)
                    {
                        return Gd.Out(
                            "re1999", Gd.Out("mainStoryRequiresAck", requiresAck),
                            "now", now, "data", data, "ack", ack ?? new Dictionary<string, object>(),
                            "shopDone", sd ?? new Dictionary<string, object>());
                    };
                Func<long, long, string, object, Dictionary<string, object>> ver =
                    delegate(long vs, long ve, string name, object act)
                    {
                        return Gd.Out("start_time", vs, "end_time", ve, "version_name", name, "activity", act);
                    };
                long vStart = Ms("2026-09-15 04:00:00"), vEnd = Ms("2026-09-29 04:00:00");
                Dictionary<string, object> combatMain = Gd.Out("start_time", start, "end_time", end, "event_type", "MainStory");
                Dictionary<string, object> dataMain = Gd.Out("1.9", ver(vStart, vEnd, "版本名", Gd.Out("combat", combatMain)));
                b.Chk("r1999 不在版本窗口 → 长草 + 数据过期提醒", delegate
                {
                    Dictionary<string, object> past = Gd.Out("1.8", ver(vStart - 86400000L * 20, Ms("2026-09-20 04:00:00"), "旧版本", Gd.Out()));
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(past, null, null, true)));
                    return (string)r["Mode"] == "idle" && (string)r["Instance"] == "idle"
                        && (string)r["Reason"] == "不在任何已记录版本的窗口内 → 长草；活动数据只记录到 2026-09-20，可能未更新";
                });
                b.Chk("r1999 主线进行中未确认 → pending(Instance=idle)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(dataMain, null, null, true)));
                    return (string)r["Mode"] == "event-pending" && (string)r["Instance"] == "idle"
                        && (string)r["EventKey"] == "1.9-combat" && (string)r["EventTip"] == "版本名"
                        && (string)r["Reason"] == "版本 1.9[版本名]主线活动进行中(至 09-26 18:00)，未确认手动通关 → 本轮跑长草方案";
                });
                b.Chk("r1999 确认后(或 requireAck=false) → event(Instance=event)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(dataMain, Gd.Out("1.9-combat", true), null, true)));
                    Dictionary<string, object> r2 = Gd.Map(Decisions.R1999(rIn(dataMain, null, null, false)));
                    return (string)r["Mode"] == "event" && (string)r["Instance"] == "event"
                        && (string)r["Reason"] == "版本 1.9[版本名]活动进行中(09-20 04:00 ~ 09-26 18:00)"
                        && (string)r2["Mode"] == "event";
                });
                b.Chk("r1999 非主线活动不受确认门管", delegate
                {
                    Dictionary<string, object> d = Gd.Out("1.9", ver(vStart, vEnd, "版本名", Gd.Out(
                        "combat", Gd.Out("start_time", start, "end_time", end, "event_type", "Activity"))));
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(d, null, null, true)));
                    return (string)r["Mode"] == "event";
                });
                b.Chk("r1999 商店领空 → idle 但保留 EventKey/EventTip", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(dataMain, Gd.Out("1.9-combat", true), Gd.Out("1.9-combat", true), true)));
                    return (string)r["Mode"] == "idle" && (string)r["Instance"] == "idle" && (bool)r["ShopDone"] && (string)r["EventKey"] == "1.9-combat"
                        && (string)r["Reason"] == "版本 1.9[版本名]活动商店已标记领空 → 本轮跑长草方案";
                });
                b.Chk("r1999 防回归：body 里误带 verbAck／verbShopReset 两枚键也不许拼进文案(10-30 B-3②-d 刀① 摘键后)", delegate
                {
                    // 手构一份"旧形状"的 body：两枚键仍在、值是自造的测试名。判定层现在一个字都不读它们 ⇒ Reason 必须与不喂键那一路逐字相等。
                    Dictionary<string, object> p = Gd.Map(Decisions.R1999(Gd.Out("re1999", Gd.Out("mainStoryRequiresAck", true), "now", now, "data", dataMain, "ack", new Dictionary<string, object>(), "shopDone", new Dictionary<string, object>(), "verbAck", "--ack-测试", "verbShopReset", "--reset-测试")));
                    Dictionary<string, object> s = Gd.Map(Decisions.R1999(Gd.Out("re1999", Gd.Out("mainStoryRequiresAck", true), "now", now, "data", dataMain, "ack", new Dictionary<string, object>(), "shopDone", Gd.Out("1.9-combat", true), "verbAck", "--ack-测试", "verbShopReset", "--reset-测试")));
                    return (string)p["Mode"] == "event-pending" && (string)p["Reason"] == "版本 1.9[版本名]主线活动进行中(至 09-26 18:00)，未确认手动通关 → 本轮跑长草方案"
                        && (string)s["Mode"] == "idle" && (bool)s["ShopDone"] && (string)s["Reason"] == "版本 1.9[版本名]活动商店已标记领空 → 本轮跑长草方案";
                });
                b.Chk("r1999 复刻窗口 → rerun", delegate
                {
                    Dictionary<string, object> d = Gd.Out("1.9", ver(vStart, vEnd, "版本名", Gd.Out(
                        "re-release", Gd.Out("start_time", Ms("2026-09-21 00:00:00"), "end_time", Ms("2026-09-25 00:00:00")))));
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(d, null, null, true)));
                    return (string)r["Mode"] == "rerun" && (string)r["Instance"] == "rerun" && (string)r["EventTip"] == "版本名 复刻"
                        && (string)r["Reason"] == "版本 1.9 复刻进行中(09-21 00:00 ~ 09-25 00:00)";
                });
                b.Chk("r1999 版本窗口内无活动 → 长草(带版本窗口说明)", delegate
                {
                    Dictionary<string, object> d = Gd.Out("1.9", ver(vStart, vEnd, "版本名", Gd.Out()));
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(d, null, null, true)));
                    return (string)r["Mode"] == "idle"
                        && (string)r["Reason"] == "版本 1.9[版本名]当前无进行中活动(版本窗口 09-15 04:00 ~ 09-29 04:00)→ 长草";
                });
                b.Chk("r1999 空数据也不炸", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999(rIn(new Dictionary<string, object>(), null, null, true)));
                    return (string)r["Mode"] == "idle" && ((string)r["Reason"]).StartsWith("不在任何已记录版本的窗口内 → 长草");
                });

                // ---------- check-stages ----------
                b.Chk("check-stages：混合分隔符拆分+去重前空格、理智合计、未知关卡", delegate
                {
                    Dictionary<string, object> table = Gd.Out("CE-5", 6, "LS-5", 6, "SR-8", 10);
                    Dictionary<string, object> r = Gd.Map(Decisions.CheckStages(Gd.Out("table", table, "stageText", "CE-5, LS-5、SR-8；CE-5  ")));
                    object[] list = (object[])r["Stages"];
                    return list.Length == 4 && (int)r["Cost"] == 28 && ((object[])r["Bad"]).Length == 0
                        && (string)r["Note"] == "共 4 关，单圈理智 28";
                });
                b.Chk("check-stages：未知关卡点名；空表说拿不到", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.CheckStages(Gd.Out("table", Gd.Out("CE-5", 6), "stageText", "CE-5,XX-9")));
                    object[] bad = (object[])r["Bad"];
                    Dictionary<string, object> r2 = Gd.Map(Decisions.CheckStages(Gd.Out("table", new Dictionary<string, object>(), "stageText", "CE-5")));
                    return bad.Length == 1 && (string)bad[0] == "XX-9" && (string)r["Note"] == "未知关卡：XX-9"
                        && (int)r["Cost"] == 6
                        && (string)r2["Note"] == "拿不到 MAA 关卡表，未校验";
                });

                b.Chk("check-stages：单关也报「共 1 关」(计数原样，不按复数口径)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.CheckStages(Gd.Out("table", Gd.Out("CE-5", 6), "stageText", "CE-5")));
                    return ((object[])r["Stages"]).Length == 1 && (int)r["Cost"] == 6
                        && (string)r["Note"] == "共 1 关，单圈理智 6";
                });
                b.Chk("check-stages：空串/纯分隔符 → 0 关 0 理智(关卡表在场就不算「拿不到」)", delegate
                {
                    Dictionary<string, object> r = Gd.Map(Decisions.CheckStages(Gd.Out("table", Gd.Out("CE-5", 6), "stageText", "")));
                    Dictionary<string, object> r2 = Gd.Map(Decisions.CheckStages(Gd.Out("table", Gd.Out("CE-5", 6), "stageText", " ,、； ")));
                    return ((object[])r["Stages"]).Length == 0 && (int)r["Cost"] == 0
                        && (string)r["Note"] == "共 0 关，单圈理智 0"
                        && ((object[])r2["Stages"]).Length == 0 && (string)r2["Note"] == "共 0 关，单圈理智 0";
                });

                // ---------- r1999-instance ----------
                b.Chk("r1999-instance：按实例名找文件、Tasks 取 <||> 末段、找不到抛原话", delegate
                {
                    string dir = Path.Combine(tmp, "instances");
                    Directory.CreateDirectory(dir);
                    GdJson.Write(Path.Combine(dir, "a1.json"), Gd.Out(
                        "InstanceName", "别的", "CurrentTasks", new object[] { "x<||>y<||>无关任务" }));
                    GdJson.Write(Path.Combine(dir, "b2.json"), Gd.Out(
                        "InstanceName", "日常", "CurrentTasks", new object[] { "root<||>每日<||>清体力" }));
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999Instance(Gd.Out("dir", dir, "want", "日常", "planKey", "event")));
                    bool ok = (string)r["Id"] == "b2" && (string)r["Name"] == "日常" && ((object[])r["Tasks"]).Length == 1
                        && (string)((object[])r["Tasks"])[0] == "清体力";
                    string msg = null;
                    try { Decisions.R1999Instance(Gd.Out("dir", dir, "want", "没有这个", "planKey", "idle")); }
                    catch (GdRuleException ex) { msg = ex.Message; }
                    return ok && msg == "M9A 中找不到名为[没有这个]的实例(计划 idle)";
                });
                b.Chk("r1999-instance/长草：planKey=idle 找到实例 → 计划键不参与取数，任务逐个取末段", delegate
                {
                    string dir = Path.Combine(tmp, "instances2");
                    Directory.CreateDirectory(dir);
                    GdJson.Write(Path.Combine(dir, "c3.json"), Gd.Out(
                        "InstanceName", "双计划", "CurrentTasks", new object[] {
                            "idle<||>每日<||>清体力", "event<||>活动", "event<||>活动<||>刷关" }));
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999Instance(Gd.Out("dir", dir, "want", "双计划", "planKey", "idle")));
                    object[] tasks = (object[])r["Tasks"];
                    return (string)r["Id"] == "c3" && (string)r["Name"] == "双计划"
                        && tasks.Length == 3 && (string)tasks[0] == "清体力" && (string)tasks[1] == "活动" && (string)tasks[2] == "刷关";
                });
                b.Chk("r1999-instance/活动：planKey=event 同一文件同形(多计划任务混排也只取末段)", delegate
                {
                    string dir = Path.Combine(tmp, "instances3");
                    Directory.CreateDirectory(dir);
                    GdJson.Write(Path.Combine(dir, "d4.json"), Gd.Out(
                        "InstanceName", "双计划", "CurrentTasks", new object[] {
                            "idle<||>每日<||>清体力", "event<||>活动", "event<||>活动<||>刷关" }));
                    Dictionary<string, object> r = Gd.Map(Decisions.R1999Instance(Gd.Out("dir", dir, "want", "双计划", "planKey", "event")));
                    object[] tasks = (object[])r["Tasks"];
                    return (string)r["Id"] == "d4" && tasks.Length == 3 && (string)tasks[2] == "刷关";
                });

                // ---------- 屏保两档(批次 10) ----------
                b.Chk("屏保 who：Wallpaper Engine 代理名 → 统一归因；产品名优先；空产品回退 .scr 文件名；null → 空串", delegate
                {
                    string w1 = Gd.Str(Gd.Get(Gd.Map(Decisions.ScreensaverWhoV(Gd.Out("cfg", Gd.Out(
                        "Saver", @"C:\Windows\System32\wallpaper64.exe_proxy.scr", "Product", "WallpaperEngine loldark")))), "Text"));
                    string w2 = Gd.Str(Gd.Get(Gd.Map(Decisions.ScreensaverWhoV(Gd.Out("cfg", Gd.Out(
                        "Saver", @"C:\a\b.scr", "Product", "别的家")))), "Text"));
                    string w3 = Gd.Str(Gd.Get(Gd.Map(Decisions.ScreensaverWhoV(Gd.Out("cfg", Gd.Out(
                        "Saver", @"C:\Windows\System32\mystars.scr", "Product", "")))), "Text"));
                    string w4 = Gd.Str(Gd.Get(Gd.Map(Decisions.ScreensaverWhoV(Gd.Out("cfg", (object)null))), "Text"));
                    return w1 == "Wallpaper Engine" && w2 == "别的家" && w3 == "mystars" && w4 == "";
                });
                b.Chk("屏保 verdict：四档顺序 unknown→none→armed→off，只有 armed 报 Risk", delegate
                {
                    Dictionary<string, object> u = Gd.Map(Decisions.ScreensaverVerdict(Gd.Out("cfg", (object)null)));
                    Dictionary<string, object> n = Gd.Map(Decisions.ScreensaverVerdict(Gd.Out("cfg", Gd.Out(
                        "Saver", "", "Product", "", "Active", "1", "TimeoutSec", 0))));
                    Dictionary<string, object> a = Gd.Map(Decisions.ScreensaverVerdict(Gd.Out("cfg", Gd.Out(
                        "Saver", @"C:\Windows\System32\mystars.scr", "Product", "", "Active", "1", "TimeoutSec", 600))));
                    Dictionary<string, object> o = Gd.Map(Decisions.ScreensaverVerdict(Gd.Out("cfg", Gd.Out(
                        "Saver", @"C:\Windows\System32\mystars.scr", "Product", "", "Active", "0", "TimeoutSec", 600))));
                    // test-lib [t1g] 同款硬约束：短话不带箭头；armed 长话点名 unarm 脚本；off 长话说"不是终态"。
                    string at = (string)a["Text"], ot = (string)o["Text"];
                    return (string)u["Kind"] == "unknown" && !(bool)u["Risk"]
                        && (string)n["Kind"] == "none" && !(bool)n["Risk"]
                        && (string)a["Kind"] == "armed" && (bool)a["Risk"]
                        && (string)o["Kind"] == "off" && !(bool)o["Risk"]
                        && at.IndexOf('→') < 0 && ot.IndexOf('→') < 0 && at.Length <= 30 && ot.Length <= 30
                        && ((string)a["Detail"]).Contains("unarm-screensaver")
                        && ((string)o["Detail"]).Contains("不是终态");
                });
                b.Chk("屏保 verdict：floorSec 钳位、字符串读数走 [int] 语义、键大小写宽容(Ci)", delegate
                {
                    // TimeoutSec=60 但 floor=300 → 展示 300 秒；600>300 不钳。小写键+字符串值=壳 JSON 回来的真实形状。
                    Dictionary<string, object> c1 = Gd.Map(Decisions.ScreensaverVerdict(Gd.Out(
                        "cfg", Gd.Out("saver", @"D:\x\y.scr", "product", "", "active", "1", "timeoutsec", "60"),
                        "floorSec", 300)));
                    Dictionary<string, object> c2 = Gd.Map(Decisions.ScreensaverVerdict(Gd.Out(
                        "cfg", Gd.Out("saver", @"D:\x\y.scr", "product", "", "active", "1", "timeoutsec", "600"),
                        "floorSec", 300)));
                    return (string)c1["Text"] == "屏保：y/300秒 仍武装" && (string)c2["Text"] == "屏保：y/600秒 仍武装";
                });

                // ---------- 屏保 14 档逐字投影(10-26 甲档第八刀：tools\probe-decisions-golden.ps1 退休前的 saver 全量) ----------
                // 期望串现取自那枚门最后一次全绿读数：old 侧钉 e269d13 真身裁出的两枚函数，new 侧走现树壳 → C# verb，14 档逐字符全等。
                // 场景名与门里逐条同名 ⇒ 那枚退休时它的 14 格去处在表里可逐条对上；改 C# 而这句话不跟，这里就红(§7 同一条纪律)。
                b.Chk("saver/who-WE代理", delegate
                { return SaverWho(SaverCfg(@"C:\Windows\System32\wallpaper32_proxy.scr", "Wallpaper Engine Screensaver", "1", 600)) == "Wallpaper Engine"; });
                b.Chk("saver/who-产品名直取", delegate
                { return SaverWho(SaverCfg(@"C:\a\b.scr", "别的家", "1", 0)) == "别的家"; });
                b.Chk("saver/who-回退文件名", delegate
                { return SaverWho(SaverCfg(@"C:\Windows\System32\mystars.scr", "", "1", 600)) == "mystars"; });
                b.Chk("saver/who-空Cfg", delegate
                { return SaverWho(null) == ""; });
                b.Chk("saver/who-只有Saver", delegate
                { return SaverWho(Gd.Out("Saver", @"D:\x\y.scr")) == "y"; });
                b.Chk("saver/verdict-unknown", delegate
                { return SaverVerdict(null, 0) == @"K=unknown|R=False|T=屏保：注册表读不到，判断不了|D=屏保：读不到 HKCU\Control Panel\Desktop，判断不了武装没有(不代表没事)"; });
                b.Chk("saver/verdict-none", delegate
                { return SaverVerdict(SaverCfg("", "", "1", 0), 0) == @"K=none|R=False|T=屏保：没挂 .scr(不会接管会话)|D=屏保：系统里没有指定屏幕保护程序 → 到点不会有任何东西盖住端游窗口，06:00 那一路安全"; });
                b.Chk("saver/verdict-armed", delegate
                { return SaverVerdict(SaverCfg(@"C:\Windows\System32\wallpaper32_proxy.scr", "Wallpaper Engine Screensaver", "1", 600), 0) == @"K=armed|R=True|T=屏保：Wallpaper Engine/600秒 仍武装|D=屏保：Wallpaper Engine/600秒 仍武装 → 到点会接管输入桌面，方舟那一路起跑前置判定会直接早退 not-started；解法：tools\unarm-screensaver.ps1 -Apply(或 设置→个性化→锁屏界面→屏幕保护程序→选「(无)」)"; });
                b.Chk("saver/verdict-off", delegate
                { return SaverVerdict(SaverCfg(@"C:\Windows\System32\mystars.scr", "", "0", 600), 0) == @"K=off|R=False|T=屏保：mystars/600秒 已解除|D=屏保：mystars/600秒，武装位是 0 → 到点不触发(日常""只关不复""就停在这档)。注意它不是终态：mystars 的设置里仍勾着屏保时，改一次设置就可能被重新武装，体检看到这行变成""仍武装""就是这个原因"; });
                b.Chk("saver/verdict-floor", delegate
                { return SaverVerdict(SaverCfg(@"C:\x\y.scr", "", "1", 60), 300) == @"K=armed|R=True|T=屏保：y/300秒 仍武装|D=屏保：y/300秒 仍武装 → 到点会接管输入桌面，方舟那一路起跑前置判定会直接早退 not-started；解法：tools\unarm-screensaver.ps1 -Apply(或 设置→个性化→锁屏界面→屏幕保护程序→选「(无)」)"; });
                b.Chk("saver/verdict-floor不钳", delegate
                { return SaverVerdict(SaverCfg(@"C:\x\y.scr", "", "1", 600), 300) == @"K=armed|R=True|T=屏保：y/600秒 仍武装|D=屏保：y/600秒 仍武装 → 到点会接管输入桌面，方舟那一路起跑前置判定会直接早退 not-started；解法：tools\unarm-screensaver.ps1 -Apply(或 设置→个性化→锁屏界面→屏幕保护程序→选「(无)」)"; });
                b.Chk("saver/verdict-字符串读数", delegate
                { return SaverVerdict(SaverCfg(@"C:\x\y.scr", "", "1", "900"), 0) == @"K=armed|R=True|T=屏保：y/900秒 仍武装|D=屏保：y/900秒 仍武装 → 到点会接管输入桌面，方舟那一路起跑前置判定会直接早退 not-started；解法：tools\unarm-screensaver.ps1 -Apply(或 设置→个性化→锁屏界面→屏幕保护程序→选「(无)」)"; });
                b.Chk("saver/verdict-缺键", delegate
                { return SaverVerdict(Gd.Out("Saver", @"C:\x\y.scr", "Active", "1"), 0) == @"K=armed|R=True|T=屏保：y/0秒 仍武装|D=屏保：y/0秒 仍武装 → 到点会接管输入桌面，方舟那一路起跑前置判定会直接早退 not-started；解法：tools\unarm-screensaver.ps1 -Apply(或 设置→个性化→锁屏界面→屏幕保护程序→选「(无)」)"; });
                b.Chk("saver/verdict-off无产品", delegate
                { return SaverVerdict(SaverCfg(@"C:\x\y.scr", "", "0", "600"), 0) == @"K=off|R=False|T=屏保：y/600秒 已解除|D=屏保：y/600秒，武装位是 0 → 到点不触发(日常""只关不复""就停在这档)。注意它不是终态：y 的设置里仍勾着屏保时，改一次设置就可能被重新武装，体检看到这行变成""仍武装""就是这个原因"; });

                // ---------- verb 接线(信封/退出码) ----------
                b.Chk("verb decide-ark：--in/--out 回环，data 即判定对象(未探过 → event 探针档)", delegate
                {
                    string ip = Path.Combine(tmp, "in1.json"), op = Path.Combine(tmp, "out1.json");
                    GdJson.Write(ip, ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5));
                    int code = GdCli.Run("decide-ark", new string[] { "--in=" + ip, "--out=" + op });
                    Dictionary<string, object> env = Gd.Map(GdJson.Read(op));
                    return code == 0 && (bool)env["ok"] && (int)env["exit"] == 0 && (int)env["proto"] == 1
                        && (string)Gd.Map(env["data"])["Mode"] == "event"
                        && (string)Gd.Map(env["data"])["Stage"] == "AI-8";
                });
                b.Chk("verb decide-ark：probe 走真 JSON 往返也能挡门(当日被剪 → event-pending)", delegate
                {
                    string ip = Path.Combine(tmp, "in1b.json"), op = Path.Combine(tmp, "out1b.json");
                    GdJson.Write(ip, ArkIn(SideStory(oneGrp), now, "假数据", null, null, 5,
                        ProbeToday("zztest-event", false, "名单里没有 AI-8")));
                    int code = GdCli.Run("decide-ark", new string[] { "--in=" + ip, "--out=" + op });
                    Dictionary<string, object> d = Gd.Map(Gd.Map(GdJson.Read(op))["data"]);
                    return code == 0 && (string)d["Mode"] == "event-pending" && (bool)d["ClearSanity"]
                        && (string)d["Stage"] == "CE-5,LS-5" && ((string)d["Reason"]).Contains("被 MAA 剪掉");
                });
                b.Chk("verb 缺 --in → 1；顶层非对象 → 1；未知参数 → 1", delegate
                {
                    string op = Path.Combine(tmp, "out2.json");
                    if (GdCli.Run("decide-ark", new string[] { "--out=" + op }) != 1) return false;
                    string bad = Path.Combine(tmp, "in2.json");
                    File.WriteAllText(bad, "[1,2]", new UTF8Encoding(false));
                    if (GdCli.Run("check-stages", new string[] { "--in=" + bad, "--out=" + op }) != 1) return false;
                    return GdCli.Run("decide-r1999", new string[] { "--nope", "--out=" + op }) == 1;
                });
                b.Chk("verb r1999-instance 找不到 → 退出码 2 + error 是原话", delegate
                {
                    string ip = Path.Combine(tmp, "in3.json"), op = Path.Combine(tmp, "out3.json");
                    string dir = Path.Combine(tmp, "instances");
                    Directory.CreateDirectory(dir);
                    GdJson.Write(ip, Gd.Out("dir", dir, "want", "不存在", "planKey", "event"));
                    int code = GdCli.Run("r1999-instance", new string[] { "--in=" + ip, "--out=" + op });
                    Dictionary<string, object> env = Gd.Map(GdJson.Read(op));
                    return code == 2 && !(bool)env["ok"] && (int)env["exit"] == 2
                        && (string)env["error"] == "M9A 中找不到名为[不存在]的实例(计划 event)";
                });
            }
            catch (Exception ex)
            {
                b.Chk("dectest 整体没炸", delegate { throw ex; });
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { }
            }
            return b.Result("dectest");
        }
    }
}
