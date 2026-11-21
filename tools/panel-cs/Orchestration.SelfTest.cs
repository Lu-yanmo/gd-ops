// tools/panel-cs/Orchestration.SelfTest.cs —— --orchtest：批次 5(Orchestration.cs)的无头自检。
// 期望串逐字抄自 lib\ark-farm.ps1 / lib\ark-daily.ps1 / lib\r1999-daily.ps1 / lib\maa.ps1 / run-via-task.ps1
// 的旧实现(tools\test-ark.ps1 的 farm 段口径同一批) —— 这里红了就是移植走样，改 Orchestration.cs，
// 不改这里的期望(§7 同一条纪律)。
// 批次纪律：不起进程、不读盘、不写文件 —— 目标清单/账本/日志片段/任务参数串全部由场景直接喂进来。
// 已知局限(与 Orchestration.cs 头注同一条)：挑关排序在两键全并列时旧侧本就不可押，场景全部避开全并列。

using System;
using System.Collections.Generic;

namespace GdPanel
{
    static class GdOrchSelfTest
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

        static Dictionary<string, object> Obj(params object[] kv) { return Inp(kv); }

        static bool Bool(object v) { return Gd.Truthy(v); }
        static int IntOf(object v)
        {
            try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); } catch { return int.MinValue; }
        }

        public static int OrchTest()
        {
            Box b = new Box();
            SpecGroup(b);
            DropGroup(b);
            PlanGroup(b);
            ArkIntentGroup(b);
            R1999Group(b);
            MaaGuiGroup(b);
            TaskArgsGroup(b);
            RetryGroup(b);
            SkipGroup(b);
            ObserveGroup(b);
            ProbeGroup(b);
            return b.Result("--orchtest");
        }

        // ---------- 目标清单解析(原 Parse-GdArkFarmTargetSpec) ----------
        static void SpecGroup(Box b)
        {
            Dictionary<string, object> p1 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "D32钢=30@CE-5")), "Spec"));
            b.Chk("名=数@关 拆得开", () => Bool(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "D32钢=30@CE-5")), "Found"))
                && (string)p1["Key"] == "D32钢" && IntOf(p1["Target"]) == 30
                && Gd.List(p1["Stages"]).Length == 1 && Gd.Str(p1["Bad"]) == "");
            Dictionary<string, object> p2 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "30135=12@CE-5,CE-4")), "Spec"));
            b.Chk("代号+多关(逗号/顿号分隔)拆得开", () => (string)p2["Key"] == "30135" && IntOf(p2["Target"]) == 12
                && Gd.List(p2["Stages"]).Length == 2);
            Dictionary<string, object> p2b = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "30135=12@CE-5、CE-4；LS-5")), "Spec"));
            b.Chk("顿号/分号/空白都是关卡分隔", () => Gd.List(p2b["Stages"]).Length == 3);
            Dictionary<string, object> p3 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw",
                Obj("item", "D32钢", "target", 20, "stages", new object[] { "CE-5" }))), "Spec"));
            b.Chk("json 对象写法也吃", () => (string)p3["Key"] == "D32钢" && IntOf(p3["Target"]) == 20 && Gd.Str(p3["Bad"]) == "");
            Dictionary<string, object> p3b = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw",
                Obj("item", "D32钢", "target", 20, "stages", new object[] { "CE-5，CE-4" }))), "Spec"));
            b.Chk("stages 只给一条串时按分隔符再切(旧 $s.Count -eq 1 分支)", () => Gd.List(p3b["Stages"]).Length == 2);
            Dictionary<string, object> p3c = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw",
                Obj("name", "或铝石", "count", 7))), "Spec"));
            b.Chk("name/count 别名也吃，没关卡就是空数组", () => (string)p3c["Key"] == "或铝石" && IntOf(p3c["Target"]) == 7
                && Gd.List(p3c["Stages"]).Length == 0);
            Dictionary<string, object> p4 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "想刷点火腿")), "Spec"));
            b.Chk("看不懂的行给 Bad 说明而不是丢掉", () => Gd.Str(p4["Bad"]).Contains("材料名=数量") && (string)p4["Raw"] == "想刷点火腿");
            Dictionary<string, object> p5 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", Obj("item", "D32钢"))), "Spec"));
            b.Chk("对象缺数量也给 Bad(Raw 回显键名)", () => Gd.Str(p5["Bad"]).Contains("目标数量") && (string)p5["Raw"] == "D32钢");
            Dictionary<string, object> p6 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", Obj("target", 5))), "Spec"));
            b.Chk("对象连 item 都没有 → Bad 里点名(item)，Raw 回压缩 JSON", () =>
                (string)p6["Bad"] == "条目里没有材料名/代号(item)" && (string)p6["Raw"] == "{\"target\":5}");
            b.Chk("raw 缺失/空串 → Found:false(旧实现返回 $null)", () =>
                !Bool(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", null)), "Found")) &&
                !Bool(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "   ")), "Found")));
            Dictionary<string, object> p7 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "D32钢＝30 @ CE-5")), "Spec"));
            b.Chk("全角等号与 @ 前后空格都认(旧正则 [=＝] 与 \\s*@\\s*)", () =>
                IntOf(p7["Target"]) == 30 && Gd.List(p7["Stages"]).Length == 1);
            // 甲档第九刀：probe-orch-golden 退役时搬来的 spec/nostage 档(值取门最后一次全绿的新侧)。
            Dictionary<string, object> p8 = D(Gd.Get(Orchestration.FarmTargetSpec(Inp("raw", "D32钢=30")), "Spec"));
            b.Chk("不写 @关卡 → Stages 空数组(旧正则无 @ 分支)", () =>
                (string)p8["Key"] == "D32钢" && IntOf(p8["Target"]) == 30 && Gd.List(p8["Stages"]).Length == 0);
        }

        // ---------- 掉落文本解析(原 Parse-GdArkDropText) ----------
        static void DropGroup(Box b)
        {
            string sample = "[08:50:26] CE-5 掉落统计:\nD32钢 : 2 (+2)   或铝石 : 1 (+1)   当前次数 : 3\n"
                + "[08:52:10] LS-5 掉落统计:\n采购凭证 : 30 (30)";
            object[] blks = Gd.List(Gd.Get(Orchestration.FarmDropText(Inp("text", sample)), "Blocks"));
            b.Chk("两个块、关卡代号抓得到", () => blks.Length == 2
                && (string)Gd.Get(blks[0], "Stage") == "CE-5" && (string)Gd.Get(blks[1], "Stage") == "LS-5");
            object[] items0 = Gd.List(Gd.Get(blks[0], "Items"));
            b.Chk("时间戳前缀不影响识别；「当前次数」被拒绝表挡掉", () => items0.Length == 2
                && (string)Gd.Get(items0[0], "Name") == "D32钢" && IntOf(Gd.Get(items0[0], "Delta")) == 2);
            object[] items1 = Gd.List(Gd.Get(blks[1], "Items"));
            b.Chk("只按 (+N) 记增量，\"(30)\"这种总数不当增量", () =>
                items1.Length == 1 && IntOf(Gd.Get(items1[0], "Delta")) == 0);
            b.Chk("空文本给空数组不抛", () =>
                Gd.List(Gd.Get(Orchestration.FarmDropText(Inp("text", "")), "Blocks")).Length == 0);
            object[] b2 = Gd.List(Gd.Get(Orchestration.FarmDropText(Inp("text", "<12> PR-B-2 掉落统计:术师芯片组 : 2 (+2)")), "Blocks"));
            b.Chk("行首 <n> 前缀剥掉后块头照样认", () => b2.Length == 1 && (string)Gd.Get(b2[0], "Stage") == "PR-B-2");
            object[] b3 = Gd.List(Gd.Get(Orchestration.FarmDropText(Inp(
                "text", "CE-4 掉落统计:\nD32钢 : 1 (+1)\n这是一句普通说明\n30135 : 9 (+9)")), "Blocks"));
            b.Chk("出现不像配额的行就算块结束(后面不再入账)", () => b3.Length == 1 && Gd.List(Gd.Get(b3[0], "Items")).Length == 1);
            // 甲档第九刀：probe-orch-golden 退役时搬来的 drop/nohead|nostage 两档(值取门最后一次全绿的新侧)。
            object[] b4 = Gd.List(Gd.Get(Orchestration.FarmDropText(Inp("text", "掉落统计:")), "Blocks"));
            b.Chk("块头没有关卡名 → 整段不给块(旧正则要求名字+掉落统计)", () => b4.Length == 0);
            object[] b5 = Gd.List(Gd.Get(Orchestration.FarmDropText(Inp("text", "CE5X 掉落统计:")), "Blocks"));
            b.Chk("关卡名不要求连字符(CE5X 照收)；空块 Items 为空", () =>
                b5.Length == 1 && (string)Gd.Get(b5[0], "Stage") == "CE5X" && Gd.List(Gd.Get(b5[0], "Items")).Length == 0);
        }

        // ---------- 补刷判表(原 Resolve-ArkFarmPlan) ----------
        static readonly Dictionary<string, object> N2I = Obj("D32钢", "30135", "或铝石", "30115");
        static readonly Dictionary<string, object> I2N = Obj("30135", "D32钢", "30115", "或铝石");
        static readonly Dictionary<string, object> Known = Obj("CE-5", true, "CE-4", true);
        static readonly Dictionary<string, object> DT = Obj("30135", Obj(
            "CE-5", Obj("runs", 5, "qty", 12),
            "CE-4", Obj("runs", 3, "qty", 3)));

        static Dictionary<string, object> Spec(string key, int target, params object[] stages)
        {
            return Obj("Key", key, "Target", target, "Stages", stages, "Raw", key + "=" + target, "Bad", "");
        }

        static void PlanGroup(Box b)
        {
            Dictionary<string, object> r0 = D(Orchestration.FarmPlan(Inp("targets", new object[0])));
            b.Chk("空清单不炸，理由说\"没配 farmTargets\"", () =>
                !Bool(r0["Active"]) && Gd.Str(r0["Reason"]).Contains("farmTargets"));

            Dictionary<string, object> inp1 = Obj("targets", new object[] { Spec("D32钢", 30, "CE-5") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj("30135", 18),
                "origin", Obj(), "dropTable", DT, "knownStages", Known, "maxTimes", 12);
            Dictionary<string, object> rf = D(Orchestration.FarmPlan(inp1));
            b.Chk("缺口=目标-现有；显式填关优先；次数保守折算=缺口(每场掉 1 保底，凑满目标为止)", () =>
                Bool(rf["Active"]) && IntOf(rf["Gap"]) == 12 && (string)Gd.List(rf["Stages"])[0] == "CE-5"
                && IntOf(rf["Times"]) == 12);
            b.Chk("Reason 说清账目(逐字口径：还差 12 / 每场约 2.4 照报但只进括号)", () => {
                string s = Gd.Str(rf["Reason"]);
                return s.Contains("还差 12") && s.Contains("每场约 2.4") && s.StartsWith("补刷 D32钢×12 @CE-5：目标 30，现有 18");
            });

            Dictionary<string, object> inpCap = Obj("targets", new object[] { Spec("D32钢", 99, "CE-5") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj(),
                "origin", Obj(), "dropTable", DT, "knownStages", Known, "maxTimes", 4);
            Dictionary<string, object> rc = D(Orchestration.FarmPlan(inpCap));
            b.Chk("单轮封顶生效并在 Reason 里说\"明天接着刷\"", () =>
                IntOf(rc["Times"]) == 4 && Gd.Str(rc["Reason"]).Contains("(单轮封顶 4，剩下的明天接着刷)"));

            Dictionary<string, object> inpG = Obj("targets", new object[] { Spec("D32钢", 10, "CE-5") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj("30135", 12),
                "origin", Obj(), "dropTable", DT, "knownStages", Known, "maxTimes", 12);
            Dictionary<string, object> rg = D(Orchestration.FarmPlan(inpG));
            b.Chk("已够的目标不刷，理由里带\"已够(12/10)\"", () =>
                !Bool(rg["Active"]) && Gd.Str(rg["Reason"]).Contains("D32钢 已够(12/10)"));

            Dictionary<string, object> inpO = Obj("targets", new object[] { Spec("30135", 30, "CE-9") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj(),
                "origin", Obj(), "dropTable", Obj(), "knownStages", Known, "maxTimes", 12);
            Dictionary<string, object> ro = D(Orchestration.FarmPlan(inpO));
            b.Chk("只填了不认识的关 → 不刷并说为什么", () =>
                !Bool(ro["Active"]) && Gd.Str(ro["Reason"]).Contains("不被 MAA 关卡表认识"));

            Dictionary<string, object> inpU = Obj("targets", new object[] { Spec("点火腿", 5) },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj(),
                "origin", Obj(), "dropTable", Obj(), "knownStages", Obj(), "maxTimes", 12);
            Dictionary<string, object> ru = D(Orchestration.FarmPlan(inpU));
            b.Chk("材料表查不到的名字跳过并说明(不静默)", () =>
                !Bool(ru["Active"]) && Gd.Str(ru["Reason"]).Contains("item_index"));

            Dictionary<string, object> inpN = Obj("targets", new object[] { Spec("D32钢", 5) },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj(),
                "origin", Obj(), "dropTable", Obj(), "knownStages", Obj(), "maxTimes", 12);
            Dictionary<string, object> rn = D(Orchestration.FarmPlan(inpN));
            b.Chk("没学习数据又没填关 → 提示写成带 @关卡 的形式", () =>
                !Bool(rn["Active"]) && Gd.Str(rn["Reason"]).Contains("@CE-5 这种带关卡的形式"));

            Dictionary<string, object> inpE = Obj("targets", new object[] { Spec("D32钢", 8, "CE-5") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj("30135", 4),
                "origin", Obj("30135", "depot"), "dropTable", Obj(), "knownStages", Obj(), "maxTimes", 12);
            Dictionary<string, object> re = D(Orchestration.FarmPlan(inpE));
            b.Chk("depot 标定的现值明说\"是估算\"；无学习数据按每场 1 估", () => {
                string s = Gd.Str(re["Reason"]);
                return s.Contains("估算") && s.Contains("按每场 1 估") && IntOf(re["Times"]) == 4;
            });

            Dictionary<string, object> inpB = Obj("targets", new object[] { Spec("D32钢", 20, "CE-5"), Spec("或铝石", 100, "CE-4") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj("30135", 18, "30115", 90),
                "origin", Obj(), "dropTable", Obj(), "knownStages", Obj(), "maxTimes", 12);
            Dictionary<string, object> rb = D(Orchestration.FarmPlan(inpB));
            b.Chk("多目标一次只锁缺口最大的那个(或铝石差10)", () =>
                (string)rb["ItemName"] == "或铝石" && IntOf(rb["Gap"]) == 10);

            // 学习表挑关：CE-5 平均 2.4 > CE-4 平均 1，两关都进(最多2)，合计均值 15/8=1.875 → 串面 1.9
            // (1.875 不是 1 位小数的中点[那得是 1.85]，ToEven 不生效 —— 实测 [math]::Round(15/8,1) 两代 PS 都给 1.9)
            Dictionary<string, object> inpL = Obj("targets", new object[] { Spec("D32钢", 30) },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj("30135", 18),
                "origin", Obj(), "dropTable", DT, "knownStages", Known, "maxTimes", 12);
            Dictionary<string, object> rl = D(Orchestration.FarmPlan(inpL));
            b.Chk("没填关 → 学习表挑关(平均掉得多的打头，最多 2 关)；次数仍=缺口 12", () => {
                object[] st = Gd.List(rl["Stages"]);
                return st.Length == 2 && (string)st[0] == "CE-5" && (string)st[1] == "CE-4" && IntOf(rl["Times"]) == 12;
            });
            b.Chk("合计均值的串面 1.9(1.875 不是中点，ToEven 不生效，实测两代 PS 都给 1.9)", () =>
                Gd.Str(rl["Reason"]).Contains("每场约 1.9"));

            Dictionary<string, object> inpCode = Obj("targets", new object[] { Spec("30115", 20, "CE-4") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj(),
                "origin", Obj(), "dropTable", Obj(), "knownStages", Obj(), "maxTimes", 12);
            Dictionary<string, object> rcode = D(Orchestration.FarmPlan(inpCode));
            b.Chk("键是代号时 ItemName 反查回材料名(Reason 用名不用号)，无学习数据按 1 估并封顶", () =>
                (string)rcode["ItemName"] == "或铝石" && Gd.Str(rcode["Reason"]).StartsWith("补刷 或铝石×12 @CE-4：目标 20，现有 0，还差 20，掉落率未知，按每场 1 估"));

            Dictionary<string, object> inpMix = Obj("targets", new object[] {
                Obj("Key", "点火腿", "Target", 0, "Stages", new object[0], "Raw", "想刷点火腿", "Bad", "看不懂「想刷点火腿」，要写成 材料名=数量[@关卡1,关卡2]"),
                Spec("D32钢", 20, "CE-5") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj("30135", 18),
                "origin", Obj(), "dropTable", Obj(), "knownStages", Obj(), "maxTimes", 12);
            Dictionary<string, object> rmix = D(Orchestration.FarmPlan(inpMix));
            b.Chk("坏行不进方案但 Notes 里留痕(汇总着说，不静默丢)", () =>
                Bool(rmix["Active"]) && Gd.List(rmix["Notes"]).Length == 1
                && Gd.Str(Gd.List(rmix["Notes"])[0]).StartsWith("跳过「想刷点火腿」"));

            Dictionary<string, object> inpZero = Obj("targets", new object[] { Spec("D32钢", 3, "CE-5") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj(),
                "origin", Obj(), "dropTable", Obj(), "knownStages", Obj(), "maxTimes", 0);
            Dictionary<string, object> rz = D(Orchestration.FarmPlan(inpZero));
            b.Chk("MaxTimes<1 兜成 1(旧 [int] 兜底)，封顶句跟着说 1", () =>
                IntOf(rz["Times"]) == 1 && Gd.Str(rz["Reason"]).Contains("单轮封顶 1"));

            // 甲档第九刀：probe-orch-golden 退役时搬来的 plan/mixst 档(值取门最后一次全绿的新侧)。
            Dictionary<string, object> inpMixSt = Obj("targets", new object[] { Spec("D32钢", 12, "CE-5", "CE-9") },
                "nameToId", N2I, "idToName", I2N, "inventory", Obj("30135", 18),
                "origin", Obj(), "dropTable", DT, "knownStages", Known, "maxTimes", 12);
            Dictionary<string, object> rmixst = D(Orchestration.FarmPlan(inpMixSt));
            b.Chk("已知关+未知关混填、现量已够 → 仍只判已够(不因未知关多话)", () =>
                !Bool(rmixst["Active"]) && Gd.Str(rmixst["Reason"]).Contains("D32钢 已够(18/12)"));
        }

        // ---------- 方舟本轮意图(原 Resolve-GdArkDecision 构造段) ----------
        static readonly Dictionary<string, object> IdleArk = Obj("Mode", "idle", "Stage", "CE-5,LS-5", "StageHint", "CE-5,LS-5",
            "ClearSanity", true, "Reason", "长草", "EventKey", null, "EventTip", null, "Source", "weekday");
        static readonly Dictionary<string, object> PendArk = Obj("Mode", "event-pending", "Stage", null, "StageHint", "CT-1",
            "ClearSanity", false, "Reason", "等新活动确认", "EventKey", "act1", "EventTip", "sidestory", "Source", "event");

        static void ArkIntentGroup(Box b)
        {
            Dictionary<string, object> m = D(Gd.Get(Orchestration.ArkIntent(Inp("ark", PendArk, "stage", "OF-1", "force", false, "farm", false)), "Ark"));
            b.Chk("-Stage 手造 manual(逐字：命令行指定刷 OF-1；Source=manual)", () =>
                (string)m["Mode"] == "manual" && (string)m["Stage"] == "OF-1" && (string)m["StageHint"] == "OF-1"
                && (string)m["Reason"] == "命令行指定刷 OF-1" && (string)m["Source"] == "manual" && Bool(m["ClearSanity"]));
            b.Chk("manual 档 EventKey/EventTip 是给过的空值(不是残留活动键)", () =>
                m["EventKey"] == null && m["EventTip"] == null);

            Dictionary<string, object> r = D(Orchestration.ArkIntent(Inp("ark", PendArk, "stage", "", "force", true, "farm", false)));
            Dictionary<string, object> f = D(r["Ark"]);
            b.Chk("-Force 越自证结论 → event 档取 StageHint，Reason 带(-Force)原话", () =>
                (string)f["Mode"] == "event" && (string)f["Stage"] == "CT-1"
                && (string)f["Reason"] == "(-Force) 不管今天的自证结论(进得去与否)，强行刷 CT-1"
                && (string)f["EventKey"] == "act1" && (string)f["EventTip"] == "sidestory" && (string)f["Source"] == "event");
            b.Chk("-Force 的 WARN 文案原样交给壳去打", () =>
                Gd.List(r["Warnings"]).Length == 1 && Gd.Str(Gd.List(r["Warnings"])[0]) == "-Force：本轮忽略活动图自证结论");

            Dictionary<string, object> fn = D(Orchestration.ArkIntent(Inp("ark", IdleArk, "stage", "", "force", true, "farm", false)));
            b.Chk("-Force 对非 event-pending 不动手(空 Warn，ark 原样)", () =>
                Gd.List(fn["Warnings"]).Length == 0 && (string)D(fn["Ark"])["Mode"] == "idle"
                && !Bool(fn["HasFarmFields"]));

            Dictionary<string, object> fd = D(Orchestration.ArkIntent(Inp("ark", IdleArk, "stage", "", "force", false, "farm", true,
                "farmOpt", Obj("Stages", new object[] { "PR-D-2", "CE-5" }, "Reason", "补刷 特种芯片组×8 @PR-D-2"))));
            Dictionary<string, object> dd = D(fd["Ark"]);
            b.Chk("专用轮 Mode=farm、Stage 只挂 farm 关(逗号拼)", () =>
                (string)dd["Mode"] == "farm" && (string)dd["Stage"] == "PR-D-2,CE-5"
                && (string)dd["Reason"] == "材料补刷专用轮 — 补刷 特种芯片组×8 @PR-D-2");
            b.Chk("专用轮键抄自原 ark(EventKey/EventTip/Source)", () =>
                dd["EventKey"] == null && (string)dd["Source"] == "weekday" && (string)fd["FarmMode"] == "dedicated"
                && Bool(fd["HasFarmFields"]));

            Dictionary<string, object> fa = D(Orchestration.ArkIntent(Inp("ark", Obj("Mode", "idle", "Stage", "CE-5,LS-5,CE-5",
                "StageHint", "CE-5,LS-5", "ClearSanity", true, "Reason", "长草", "EventKey", null, "EventTip", null, "Source", "weekday"),
                "stage", "", "force", false, "farm", false,
                "farmOpt", Obj("Stages", new object[] { "PR-D-2", "CE-5" }, "Reason", "补刷 特种芯片组×8 @PR-D-2"))));
            Dictionary<string, object> da = D(fa["Ark"]);
            b.Chk("需求优先档：farm 关排最前且去重(CE-5 不重复出现)", () =>
                (string)da["Stage"] == "PR-D-2,CE-5,LS-5" && (string)fa["FarmMode"] == "append");
            b.Chk("append 的 Reason 用「；需求优先 」拼接(逐字)", () =>
                (string)da["Reason"] == "长草；需求优先 补刷 特种芯片组×8 @PR-D-2");
            b.Chk("append 档不动 Mode/StageHint/ClearSanity(只改 Stage/Reason)", () =>
                (string)da["Mode"] == "idle" && (string)da["StageHint"] == "CE-5,LS-5" && Bool(da["ClearSanity"]));

            Dictionary<string, object> fl = D(Orchestration.ArkIntent(Inp("ark", IdleArk, "stage", "", "force", false, "farm", true)));
            b.Chk("-Farm 但本轮没有可补 → 原方案 + FarmMode 空 + WARN 文案", () =>
                (string)fl["FarmMode"] == "" && Bool(fl["HasFarmFields"])
                && Gd.Str(Gd.List(fl["Warnings"])[0]) == "-Farm 本轮没有可补的材料 → 按原方案跑"
                && (string)D(fl["Ark"])["Mode"] == "idle");

            // 甲档第九刀：probe-orch-golden 退役时搬来的 ark/plain|manual-farm|force-farm 三档。
            // 后两档走壳里那套**两步序**(先 manual/force、再拿变换后的 ark 过 farm 档)，值取门最后一次全绿的新侧。
            Dictionary<string, object> pl = D(ArkTwoStep(IdleArk, "", false, false, null));
            b.Chk("两步序·两步都不触发 → 原 ark 原样、零 Warn、无 farm 字段", () =>
                (string)D(pl["Ark"])["Mode"] == "idle" && (string)D(pl["Ark"])["Stage"] == "CE-5,LS-5"
                && (string)D(pl["Ark"])["Reason"] == "长草" && Gd.List(pl["Warnings"]).Length == 0
                && !Bool(pl["HasFarmFields"]));

            Dictionary<string, object> mf = D(ArkTwoStep(IdleArk, "OF-1", false, false,
                Obj("Stages", new object[] { "PR-D-2" }, "Reason", "补刷 D32钢×5 @CE-5")));
            b.Chk("两步序·manual→append：farm 关排最前、StageHint 保 OF-1、Reason 用「；需求优先 」拼", () => {
                Dictionary<string, object> da2 = D(mf["Ark"]);
                return (string)da2["Mode"] == "manual" && (string)da2["Stage"] == "PR-D-2,OF-1"
                    && (string)da2["StageHint"] == "OF-1" && Bool(da2["ClearSanity"])
                    && (string)da2["Reason"] == "命令行指定刷 OF-1；需求优先 补刷 D32钢×5 @CE-5"
                    && da2["EventKey"] == null && da2["EventTip"] == null && (string)da2["Source"] == "manual"
                    && (string)mf["FarmMode"] == "append" && Bool(mf["HasFarmFields"])
                    && Gd.List(mf["Warnings"]).Length == 0;
            });

            Dictionary<string, object> ff = D(ArkTwoStep(PendArk, "", true, false,
                Obj("Stages", new object[] { "PR-D-2" }, "Reason", "补刷 D32钢×5 @CE-5")));
            b.Chk("两步序·force→append：先越门(event/CT-1)再并 farm 关，WARN 只剩 -Force 那句", () => {
                Dictionary<string, object> da3 = D(ff["Ark"]);
                return (string)da3["Mode"] == "event" && (string)da3["Stage"] == "PR-D-2,CT-1"
                    && (string)da3["StageHint"] == "CT-1" && Bool(da3["ClearSanity"])
                    && (string)da3["Reason"] == "(-Force) 不管今天的自证结论(进得去与否)，强行刷 CT-1；需求优先 补刷 D32钢×5 @CE-5"
                    && (string)da3["EventKey"] == "act1" && (string)da3["EventTip"] == "sidestory"
                    && (string)da3["Source"] == "event" && (string)ff["FarmMode"] == "append"
                    && Gd.List(ff["Warnings"]).Length == 1
                    && Gd.Str(Gd.List(ff["Warnings"])[0]) == "-Force：本轮忽略活动图自证结论";
            });
        }

        // 壳里 Resolve-GdArkDecision 的两步序(先 manual/force、再拿变换后的 ark 过 farm 档)：
        // C# 的两个 verb 各管一步，这里按壳的顺序串起来，Warnings 两段依序合计。
        static object ArkTwoStep(Dictionary<string, object> ark0, string stage, bool force, bool farm, object farmOpt)
        {
            Dictionary<string, object> r1 = D(Orchestration.ArkIntent(Inp("ark", ark0, "stage", stage, "force", force,
                "farm", false, "farmOpt", null)));
            Dictionary<string, object> r2 = D(Orchestration.ArkIntent(Inp("ark", r1["Ark"], "stage", "", "force", false,
                "farm", farm, "farmOpt", farmOpt)));
            List<object> warnings = new List<object>();
            foreach (object w in Gd.List(r1["Warnings"])) warnings.Add(w);
            foreach (object w in Gd.List(r2["Warnings"])) warnings.Add(w);
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Ark"] = r2["Ark"];
            data["FarmMode"] = r2["FarmMode"];
            data["HasFarmFields"] = r2["HasFarmFields"];
            data["Warnings"] = warnings.ToArray();
            return data;
        }

        // ---------- 1999 本轮意图(原 Resolve-GdR1999Decision 构造段) ----------
        static readonly Dictionary<string, object> PendR = Obj("Mode", "event-pending", "Instance", "idle", "Version", "v4.7.1",
            "VersionName", "测试版本", "EventKey", "act123", "EventTip", "活动", "Reason", "待确认", "DataAge", null);

        static void R1999Group(Box b)
        {
            Dictionary<string, object> r = D(Orchestration.R1999Intent(Inp("decision", PendR, "plan", "auto", "force", true)));
            Dictionary<string, object> d = D(r["Decision"]);
            b.Chk("-Force → event/event，Reason 与 WARN 逐字", () =>
                (string)d["Mode"] == "event" && (string)d["Instance"] == "event"
                && (string)d["Reason"] == "(-Force) 跳过 1999 活动确认门，直接用活动方案"
                && (string)d["Version"] == "v4.7.1" && (string)d["VersionName"] == "测试版本"
                && (string)d["EventKey"] == "act123" && d["DataAge"] == null
                && Gd.Str(Gd.List(r["Warnings"])[0]) == "-Force：1999 本轮忽略活动确认门");

            Dictionary<string, object> r2 = D(Orchestration.R1999Intent(Inp("decision", PendR, "plan", "idle", "force", true)));
            Dictionary<string, object> d2 = D(r2["Decision"]);
            b.Chk("-Plan≠auto 无条件覆盖(连 Force 的结果也覆盖)：Version=manual/VersionName=命令行指定", () =>
                (string)d2["Mode"] == "idle" && (string)d2["Instance"] == "idle" && (string)d2["Version"] == "manual"
                && (string)d2["VersionName"] == "命令行指定" && (string)d2["Reason"] == "命令行指定 idle"
                && d2["EventKey"] == null && d2["DataAge"] == null
                // 甲档第九刀补：r19/both 档的 WARN 在覆盖之后仍原样留着(门最后全绿的新侧读数)。
                && Gd.List(r2["Warnings"]).Length == 1
                && Gd.Str(Gd.List(r2["Warnings"])[0]) == "-Force：1999 本轮忽略活动确认门");

            Dictionary<string, object> r3 = D(Orchestration.R1999Intent(Inp("decision", PendR, "plan", "auto", "force", false)));
            b.Chk("两样都不给 → decision 原样、零 Warn", () =>
                Gd.List(r3["Warnings"]).Length == 0 && (string)D(r3["Decision"])["Mode"] == "event-pending");
        }

        // ---------- gui.log 行分类(原 Invoke-MaaRun 每行段) ----------
        static void MaaGuiGroup(Box b)
        {
            string text = "[12:00:00] Completed Task Chain: Recruit\n<12> 任务已全部完成\n<12> 用时 3 分钟 (03:12)\n"
                + "<13> 无更新   请检查客户端   <14>\n<15> 任务已完成，但出现错误\n<16> [ERR] 900 连接失败 something\n"
                + "<17> 任务失败：作战\n<18> 一条普通日志\n";
            Dictionary<string, object> res = D(Orchestration.MaaGuiLines(Inp("text", text, "benign", new object[] { @"连接失败 something$" })));
            object[] evs = Gd.List(res["Events"]);
            b.Chk("五种标志各归各类(顺序按行)", () => {
                string[] kinds = new string[evs.Length];
                for (int i = 0; i < evs.Length; i++) kinds[i] = Gd.Str(Gd.Get(evs[i], "Kind"));
                return string.Join(",", kinds) == "chain,finished,stall,drained,benign,error";
            });
            b.Chk("marker 计数=认识的行数(普通行不算)", () => IntOf(res["Markers"]) == 6);
            b.Chk("logChars 只加非空白行的长度", () => {
                int want = 0;
                foreach (string l in text.Split('\n')) if (l.Trim().Length > 0) want += l.TrimEnd('\r').Length;
                return IntOf(res["LogChars"]) == want;
            });
            b.Chk("完成行的下一行抠「用时」并裁尾(旧 $Matches[1].Trim())", () =>
                (string)Gd.Get(evs[1], "Used") == "3 分钟 (03:12");
            b.Chk("停滞行剥 <n> 前缀并把连续空白压成一个空格", () =>
                (string)Gd.Get(evs[2], "Msg") == "无更新 请检查客户端 <14>");
            b.Chk("benign 命中带出所用模式串(壳按它去重)", () =>
                (string)Gd.Get(evs[4], "Pattern") == @"连接失败 something$" && (string)Gd.Get(evs[4], "Msg") == "连接失败 something");
            b.Chk("「任务失败：<中文名>」算异常线索(行首 <n> 前缀照旧留下——旧清洗只剥到第一个 ] )", () =>
                (string)Gd.Get(evs[5], "Msg") == "<17> 任务失败：作战");

            Dictionary<string, object> r2 = D(Orchestration.MaaGuiLines(Inp("text", "[ERR]\n", "benign", new object[0])));
            b.Chk("清洗后 msg 为空：marker 照记、事件不发(旧分支语义)", () =>
                IntOf(r2["Markers"]) == 1 && Gd.List(r2["Events"]).Length == 0);
            Dictionary<string, object> r3 = D(Orchestration.MaaGuiLines(Inp("text", "", "benign", new object[0])));
            b.Chk("空片段全零不抛", () => IntOf(r3["Markers"]) == 0 && IntOf(r3["LogChars"]) == 0);
            Dictionary<string, object> r4 = D(Orchestration.MaaGuiLines(Inp("text", "Completed Task Chain: Recruit5", "benign", new object[0])));
            b.Chk("链路名只吃字母([A-Za-z]+)，尾巴数字不当链名", () => {
                object[] e4 = Gd.List(r4["Events"]);
                return e4.Length == 1 && (string)Gd.Get(e4[0], "Chain") == "Recruit";
            });

            // 甲档第九刀：probe-orch-golden 退役时搬来的 gui/benign-dup|chain-twice 两档。
            // 去重与计数本来在壳的 benignSeen/completed 账上，这里钉分类层**按行各发一条**。
            Dictionary<string, object> rd = D(Orchestration.MaaGuiLines(Inp("text",
                "<1> 连接失败 something\n<2> 连接失败 something", "benign", new object[] { @"连接失败 something$" })));
            b.Chk("同一噪音行两次 → 两条 benign 事件(去重是壳的账，不是分类层)", () => {
                object[] ed = Gd.List(rd["Events"]);
                return ed.Length == 2 && (string)Gd.Get(ed[0], "Kind") == "benign"
                    && (string)Gd.Get(ed[1], "Kind") == "benign"
                    && (string)Gd.Get(ed[1], "Pattern") == @"连接失败 something$";
            });
            Dictionary<string, object> rt = D(Orchestration.MaaGuiLines(Inp("text",
                "Completed Task Chain: Mainline\nCompleted Task Chain: Mainline\nCompleted Task Chain: Recruit5",
                "benign", new object[0])));
            b.Chk("同一链路两次 → 三条 chain 事件按行发", () => {
                object[] et = Gd.List(rt["Events"]);
                return et.Length == 3 && (string)Gd.Get(et[0], "Chain") == "Mainline"
                    && (string)Gd.Get(et[1], "Chain") == "Mainline" && (string)Gd.Get(et[2], "Chain") == "Recruit";
            });
        }

        // ---------- 任务参数切词(原 Get-GdTaskArgsWithGames) ----------
        static void TaskArgsGroup(Box b)
        {
            Dictionary<string, object> r = D(Orchestration.TaskArgs(Inp("text",
                "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"C:\\a b\\daily-clear.ps1\" -Tag auto -Games ark")));
            object[] p = Gd.List(r["Pieces"]);
            b.Chk("剥掉原有 -Games/-Tag，带空格引号路径整体成一项", () => {
                string[] s = new string[p.Length];
                for (int i = 0; i < p.Length; i++) s[i] = Gd.Str(p[i]);
                return string.Join("|", s) == "-NoProfile|-WindowStyle|Hidden|-ExecutionPolicy|Bypass|-File|C:\\a b\\daily-clear.ps1";
            });
            Dictionary<string, object> r2 = D(Orchestration.TaskArgs(Inp("text",
                "-NoProfile -File \"x.ps1\" -Games \"ark\" -Tag \"manual\"")));
            b.Chk("值带引号的 -Games/-Tag 也剥得掉(旧 \\\"? 可选引号)", () =>
                Gd.List(r2["Pieces"]).Length == 3 && Gd.Str(Gd.List(r2["Pieces"])[2]) == "x.ps1");
            Dictionary<string, object> r3 = D(Orchestration.TaskArgs(Inp("text", "-File x")));
            b.Chk("没有可剥的就原样切(Count 守卫归壳管)", () => Gd.List(r3["Pieces"]).Length == 2);
            // 甲档第九刀：probe-orch-golden 退役时搬来的 ta/bare 档(值取门最后一次全绿的新侧)。
            Dictionary<string, object> r4 = D(Orchestration.TaskArgs(Inp("text", "pwsh -File daily-clear.ps1")));
            b.Chk("裸命令行三段原样切(pwsh/-File/脚本名)", () => {
                object[] q = Gd.List(r4["Pieces"]);
                return q.Length == 3 && (string)q[0] == "pwsh" && (string)q[1] == "-File" && (string)q[2] == "daily-clear.ps1";
            });
        }

        // ---------- 批次 8：decide-daily-retry(两路 daily 的重试分流) ----------
        // 期望串逐字抄自 lib\ark-daily.ps1:141-148 与 lib\r1999-daily.ps1:90-106(基线 c498da0)。
        static void RetryGroup(Box b)
        {
            object arkStalled = Retry("ark", "stalled", false, true);
            object arkUfNoRetry = Retry("ark", "unknown-format", false, false);
            object arkUfRetry = Retry("ark", "unknown-format", false, true);
            object arkOk = Retry("ark", "ok", false, true);
            object arkNs = Retry("ark", "not-started", false, true);
            object n99Stalled = Retry("r1999", "stalled", false, true);
            object n99BlockedRescued = Retry("r1999", "blocked", true, true);
            object n99BlockedPlain = Retry("r1999", "blocked", false, true);
            object n99NsRescued = Retry("r1999", "not-started", true, true);
            object n99NsPlain = Retry("r1999", "not-started", false, false);
            object n99Uf = Retry("r1999", "unknown-format", false, true);
            object n99Empty = Retry("r1999", "empty-queue", false, true);
            object n99Timeout = Retry("r1999", "timeout", false, true);
            object arkErrorNoRetry = Retry("ark", "error", false, false);
            object n99NsPlainTrue = Retry("r1999", "not-started", false, true);
            object n99EmptyRescued = Retry("r1999", "empty-queue", true, true);
            object n99TimeoutNoRetry = Retry("r1999", "timeout", false, false);
            object n99BlockedRescuedNoRetry = Retry("r1999", "blocked", true, false);

            b.Chk("ark stalled+retryOnce → 重试一次(WARN『收尾后重试一次』)", () =>
                Rry(arkStalled) && Rlg(arkStalled).Length == 1 && Rlv(arkStalled, 0) == "WARN"
                    && Rtx(arkStalled, 0) == "方舟本轮未成功 → 收尾后重试一次");
            b.Chk("ark unknown-format 就算 retryOnce 也不重试；不重试也照样落 elseif 打 ERROR(链逐字，不化简)", () =>
                !Rry(arkUfNoRetry) && !Rry(arkUfRetry)
                    && Rtx(arkUfNoRetry, 0) == "方舟日志标志一条都没认出来 → 重试同样看不懂，直接记入本轮告警(见下面的处理建议)"
                    && Rlv(arkUfNoRetry, 0) == "ERROR" && Rtx(arkUfRetry, 0) == Rtx(arkUfNoRetry, 0));
            b.Chk("ark ok/not-started → 既不重试也不吭声", () =>
                !Rry(arkOk) && !Rry(arkNs) && Rlg(arkOk).Length == 0 && Rlg(arkNs).Length == 0);
            b.Chk("r1999 stalled → 兜没兜都 WARN『关闭游戏后重试一次』", () =>
                Rry(n99Stalled) && Rtx(n99Stalled, 0) == "1999 本轮未成功 → 关闭游戏后重试一次");
            b.Chk("r1999 blocked+Rescued → 翻案重试，话术换『视觉兜底把挡路的界面点掉了』那句", () =>
                Rry(n99BlockedRescued) && Rlv(n99BlockedRescued, 0) == "WARN"
                    && Rtx(n99BlockedRescued, 0) == "1999 本轮未成功，但视觉兜底把挡路的界面点掉了 → 关闭 M9A 后重试一次");
            b.Chk("r1999 blocked 没兜清 → 不重试但落 elseif blocked → ERROR『从头一个任务起就没进游戏』", () =>
                !Rry(n99BlockedPlain) && Rlv(n99BlockedPlain, 0) == "ERROR"
                    && Rtx(n99BlockedPlain, 0) == "1999 从头一个任务起就没进游戏 → 是客户端/资源要更新或模拟器状态不对，重试没有意义，直接记入本轮告警");
            b.Chk("r1999 not-started+Rescued 也翻案；不带 Rescued → WARN『队列没开始/空跑完成』(retryOnce 开/关同落)", () =>
                Rry(n99NsRescued) && !Rry(n99NsPlain) && !Rry(n99NsPlainTrue)
                    && Rtx(n99NsPlain, 0) == "1999 队列没开始/空跑完成 → 重试没有意义，直接记入本轮告警" && Rlv(n99NsPlain, 0) == "WARN"
                    && Rtx(n99NsPlainTrue, 0) == Rtx(n99NsPlain, 0));
            b.Chk("r1999 unknown-format 永远不重试(Rescued 也不行——不在翻案清单)且 ERROR 整句", () =>
            {
                object ufRescued = Retry("r1999", "unknown-format", true, true);
                return !Rry(n99Uf) && !Rry(ufRescued) && Rlv(n99Uf, 0) == "ERROR"
                    && Rtx(n99Uf, 0) == "1999 日志标志一条都没认出来 → 重试同样看不懂，直接记入本轮告警(见下面的处理建议)";
            });
            b.Chk("r1999 empty-queue 无 Rescued → 落 WARN 队列句；timeout 无 Rescued 也走重试", () =>
                !Rry(n99Empty) && Rtx(n99Empty, 0) == "1999 队列没开始/空跑完成 → 重试没有意义，直接记入本轮告警"
                    && Rry(n99Timeout) && Rtx(n99Timeout, 0) == "1999 本轮未成功 → 关闭游戏后重试一次");
            b.Chk("ark error + retryOnce=false → 不重试也不告警(elseif 只认 unknown-format)", () =>
                !Rry(arkErrorNoRetry) && Rlg(arkErrorNoRetry).Length == 0);
            b.Chk("r1999 empty-queue + Rescued → 翻案重试，话术换『兜清楚了』那句", () =>
                Rry(n99EmptyRescued) && Rlv(n99EmptyRescued, 0) == "WARN"
                    && Rtx(n99EmptyRescued, 0) == "1999 本轮未成功，但视觉兜底把挡路的界面点掉了 → 关闭 M9A 后重试一次");
            b.Chk("r1999 timeout + retryOnce=false → 不重试且静默(timeout 不在 elseif 清单)", () =>
                !Rry(n99TimeoutNoRetry) && Rlg(n99TimeoutNoRetry).Length == 0);
            b.Chk("r1999 blocked+Rescued 但 retryOnce=false → 落 elseif blocked 的 ERROR(翻案条件被开关挡住)", () =>
                !Rry(n99BlockedRescuedNoRetry) && Rlv(n99BlockedRescuedNoRetry, 0) == "ERROR"
                    && Rtx(n99BlockedRescuedNoRetry, 0) == "1999 从头一个任务起就没进游戏 → 是客户端/资源要更新或模拟器状态不对，重试没有意义，直接记入本轮告警");
            b.Chk("game 不认识 → GdRuleException 原话(退出码 2 归壳，规则说不干)", () =>
            {
                try { Retry("mumu", "stalled", false, true); return false; }
                catch (GdRuleException ex) { return ex.Message == "decide-daily-retry：game 只认 ark/r1999，收到「mumu」"; }
            });
        }

        static object Retry(string game, string status, bool rescued, bool retryOnce)
        {
            return Orchestration.DailyRetry(Inp("game", game, "status", status, "rescued", rescued, "retryOnce", retryOnce));
        }
        static bool Rry(object r) { return Gd.Truthy(Gd.Get(D(r), "Retryable")); }
        static object[] Rlg(object r) { return Gd.List(Gd.Get(D(r), "Logs")); }
        static string Rlv(object r, int i) { return Gd.Str(Gd.Get(D(Rlg(r)[i]), "Level")); }
        static string Rtx(object r, int i) { return Gd.Str(Gd.Get(D(Rlg(r)[i]), "Text")); }

        // ---------- 2026-09-28 新增：detect-maa-fight-skip(认 MAA 的跳关回执) ----------
        // 场景行逐字抄自本机 MAA 安装目录的 debug\gui.log 06:00:42~06:00:57 段(09-28 真跑实录)。
        static void SkipGroup(Box b)
        {
            string start = "[2026-09-28 06:00:42.007][INF][Bootstrapper]           <2> MaaAssistantArknights GUI started";
            string gf1 = "[2026-09-28 06:00:57.347][INF][FightSettingsUserControlModel] <2> GetFightStage: from [\"PR-D-2\"], selected null";
            string noOpen = "[2026-09-28 06:00:57.347][INF][TaskQueueViewModel]     <2> 关卡计划内没有当日开放的关卡";
            string skipLine = "[2026-09-28 06:00:57.348][INF][TaskQueueViewModel]     <2> 理智作战: 剩余理智 任务跳过";

            object real = Skip(start + "\n" + gf1 + "\n" + noOpen + "\n" + skipLine);
            b.Chk("09-28 实录会话：Skipped=true、认库实况 Plan=[PR-D-2]、任务跳过计 1", () =>
                Sk(real) && Pl(real).Length == 1 && Gd.Str(Pl(real)[0]) == "PR-D-2" && Ts(real) == 1);
            b.Chk("证据行剥掉时间戳/模块前缀，剩原话", () =>
                Ev(real).Length >= 1 && Gd.Str(Ev(real)[0]) == "关卡计划内没有当日开放的关卡");

            object normal = Skip(start + "\n"
                + "[2026-09-28 06:00:57.347][INF][FightSettingsUserControlModel] <2> GetFightStage: from [\"PR-D-2\", \"CE-5\", \"LS-5\"], selected PR-D-2\n"
                + "[2026-09-28 06:10:59.000][INF][TaskQueueViewModel]     <2> 任务已全部完成！");
            b.Chk("正常轮(有开放关)不误认：Skipped=false、Plan 三关原样读出", () =>
                !Sk(normal) && Pl(normal).Length == 3 && Gd.Str(Pl(normal)[2]) == "LS-5");

            object fullSan = Skip(start + "\n" + skipLine);
            b.Chk("只有『理智作战: … 任务跳过』没有『没有当日开放』句 → 不算认关(理智已满等其它起因)，只计数", () =>
                !Sk(fullSan) && Ts(fullSan) == 1);

            object twoSessions = Skip(noOpen + "\n" + start + "\n" + gf1 + "\n" + noOpen + "\n" + skipLine + "\n" + start + "\n"
                + "[2026-09-28 18:01:00.000][INF][TaskQueueViewModel]     <2> 任务已全部完成！");
            b.Chk("多会话文本只押最后一次 GUI started 之后(上一轮的跳关不背今晚的锅)", () =>
                !Sk(twoSessions) && Ts(twoSessions) == 0 && Pl(twoSessions).Length == 0);

            object lastPlan = Skip(start + "\n" + gf1 + "\n"
                + "[2026-09-28 06:00:58.000][INF][FightSettingsUserControlModel] <2> GetFightStage: from [\"CE-5\"], selected CE-5\n" + noOpen);
            b.Chk("会话里多条 GetFightStage → Plan 取最后一条", () =>
                Sk(lastPlan) && Pl(lastPlan).Length == 1 && Gd.Str(Pl(lastPlan)[0]) == "CE-5");

            // 剔掉 farm 关重试那一轮的形态：长草关被 MAA 加载期整个修剪掉 → from [] 再任务跳过。
            object emptyPlan = Skip(start + "\n"
                + "[2026-09-28 18:00:57.347][INF][FightSettingsUserControlModel] <2> GetFightStage: from [], selected null\n" + skipLine);
            b.Chk("第二档：from [] 空计划 + Fight 任务跳过 → 也算认关", () =>
                Sk(emptyPlan) && Pl(emptyPlan).Length == 0 && Ts(emptyPlan) == 1);
            object emptyNoSkip = Skip(start + "\n"
                + "[2026-09-28 18:00:57.347][INF][FightSettingsUserControlModel] <2> GetFightStage: from [], selected null\n");
            b.Chk("只有 from [] 没有任务跳过句 → 不算(没证据说这一轮真被跳)", () =>
                !Sk(emptyNoSkip) && Pl(emptyNoSkip).Length == 0 && Ts(emptyNoSkip) == 0);
        }

        static object Skip(string text) { return Orchestration.MaaFightSkip(Inp("text", text)); }
        static bool Sk(object r) { return Gd.Truthy(Gd.Get(D(r), "Skipped")); }
        static object[] Pl(object r) { return Gd.List(Gd.Get(D(r), "Plan")); }
        static int Ts(object r) { return Convert.ToInt32(Gd.Get(D(r), "TaskSkips"), System.Globalization.CultureInfo.InvariantCulture); }
        static object[] Ev(object r) { return Gd.List(Gd.Get(D(r), "Evidence")); }

        // ---------- 2026-10-05 新增：observe-maa-stage-open(只读观测，攒每周几开哪本的账) ----------
        // 夹具行逐字抄自本机 debug\asst.log 与 gui.log 的 10-05 两班实录；asst 行里 penguin_id 是
        // 机专属值已整字段摘除(读数只押 stage/重放两格，摘它不伤判据)。
        static void ObserveGroup(Box b)
        {
            string apEmpty = "[2026-10-05 06:00:55.354][INF][Px24300][Tx10913] asst::Assistant::append_task Fight {\"stage\":\"\",\"medicine\":0,\"times\":20,\"series\":0,\"server\":\"CN\",\"drops\":{\"4001\":999999}}";
            string replay = "[2026-10-05 06:01:20.100][TRC][Px24300][Tx10913] asst::PackageTask::run | run subtask 1 / 4 {\"class\":\"asst::ProcessTask\",\"first\":[\"LastOrCurBattleBegin\"],\"subtask\":\"ProcessTask\",\"taskchain\":\"Fight\",\"taskid\":4}";
            string guiEmpty = "[2026-10-05 06:00:55.350][INF][FightSettingsUserControlModel] <2> GetFightStage: from [\"\"], selected ";
            string apNamed = "[2026-10-05 06:00:55.354][INF][Px24300][Tx10913] asst::Assistant::append_task Fight {\"stage\":\"LS-5\",\"medicine\":0,\"times\":20,\"series\":0,\"server\":\"CN\"}";
            string guiOpen = "[2026-10-05 06:00:55.350][INF][FightSettingsUserControlModel] <2> GetFightStage: from [\"LS-5\", \"SK-5\"], selected LS-5";

            object trimmed = Obs(apEmpty + "\n" + replay, guiEmpty, "2026-10-05");
            b.Chk("10-05 两刀剪空铁证形状：Found=true、Stage 空串原样、重放实锤、名单滤掉空串占位", () =>
                Of(trimmed) && Og(trimmed) == "" && Ol(trimmed) && Oo(trimmed)
                && Ov(trimmed).Length == 0 && Oc(trimmed) == "" && Oq(trimmed) == "2026-10-05");

            object normal = Obs(apNamed + "\n" + "[x] 无关行", guiOpen, "2026-10-06");
            b.Chk("正常轮：Stage 非空、无重放行不算 LastBattle、Survivors 两关、Selected 抠出", () =>
                Of(normal) && Og(normal) == "LS-5" && !Ol(normal)
                && Ov(normal).Length == 2 && Gd.Str(Ov(normal)[1]) == "SK-5" && Oc(normal) == "LS-5");

            object noFight = Obs("[2026-10-05][INF] 只有别的路径行", guiOpen, "2026-10-05");
            b.Chk("asst 里没有 append_task Fight 行 → Found=false(gui 读数照抄不误)：这条观测进账本前会被壳跳过", () =>
                !Of(noFight) && Oo(noFight) && Ov(noFight).Length == 2);

            object prevShift = Obs(replay + "\n" + apNamed, "", "2026-10-05");
            b.Chk("上一班遗留的重放行排在 append 之前 → 不算本轮走过回到上次作战(只朝后看)", () =>
                Of(prevShift) && !Ol(prevShift));

            object lastGui = Obs(apNamed, guiOpen + "\n"
                + "[2026-10-05 18:01:03.300][INF][FightSettingsUserControlModel] <2> GetFightStage: from [\"CE-5\"], selected null", "2026-10-05");
            b.Chk("多条 GetFightStage 取最后一条；selected 的 null 形态与缺失形态同样归空串", () =>
                Ov(lastGui).Length == 1 && Gd.Str(Ov(lastGui)[0]) == "CE-5" && Oc(lastGui) == "");

            object emptyAll = Obs("", "", "");
            b.Chk("两份文本都空 → 全零读数不抛(壳侧空 tail 是高频路径)", () =>
                !Of(emptyAll) && !Ol(emptyAll) && !Oo(emptyAll) && Ov(emptyAll).Length == 0);
        }

        // ---------- 2026-10-12 第 1 刀新增：decide-ark-event-probe(每轮自证活动图进得去没) ----------
        // 夹具就是上面 observe 组那几种现场形状，逐字同款；探针轮(10-12 20:34)的真锚是
        // `GetFightStage: from ["YW-8"], selected YW-8` ⇒ 进得去那一格照它抄。
        static void ProbeGroup(Box b)
        {
            Dictionary<string, object> ovYes = Gd.Out("Found", true, "Date", "2026-10-12", "Stage", "YW-8",
                "LastBattle", false, "SawGuiPlan", true, "Survivors", new object[] { "YW-8" }, "Selected", "YW-8");
            Dictionary<string, object> ovInList = Gd.Out("Found", true, "Date", "2026-10-12", "Stage", "CE-5",
                "LastBattle", false, "SawGuiPlan", true, "Survivors", new object[] { "LS-5", "YW-8" }, "Selected", "LS-5");
            Dictionary<string, object> ovTrimmed = Gd.Out("Found", true, "Date", "2026-10-12", "Stage", "CE-5",
                "LastBattle", false, "SawGuiPlan", true, "Survivors", new object[] { "LS-5", "SK-5" }, "Selected", "LS-5");
            Dictionary<string, object> ovEmpty = Gd.Out("Found", true, "Date", "2026-10-12", "Stage", "",
                "LastBattle", true, "SawGuiPlan", true, "Survivors", new object[0], "Selected", "");
            Dictionary<string, object> ovNoPlan = Gd.Out("Found", false, "Date", "2026-10-12", "Stage", "YW-8",
                "LastBattle", false, "SawGuiPlan", false, "Survivors", new object[0], "Selected", "");

            b.Chk("探针·进得去(名单里就是它且被选中)：Valid+Entered，证据带名单条数", () =>
            {
                Dictionary<string, object> r = Gd.Map(Orchestration.ArkEventProbe(
                    Inp("date", "2026-10-12", "eventKey", "zztest-event", "stage", "YW-8", "obs", ovYes)));
                return (bool)r["Valid"] && (bool)r["Entered"] && (string)r["Stage"] == "YW-8"
                    && (string)r["Date"] == "2026-10-12" && (string)r["EventKey"] == "zztest-event"
                    && (string)r["Evidence"] == "MAA 的关卡名单里选中了 YW-8(名单共 1 关)";
            });
            b.Chk("探针·名单含它但当前选中的是别的关 → 照样算进得去", () =>
            {
                Dictionary<string, object> r = Gd.Map(Orchestration.ArkEventProbe(
                    Inp("date", "2026-10-12", "eventKey", "e", "stage", "YW-8", "obs", ovInList)));
                return (bool)r["Valid"] && (bool)r["Entered"]
                    && ((string)r["Evidence"]).Contains("名单含 YW-8") && ((string)r["Evidence"]).Contains("当前选中 LS-5");
            });
            b.Chk("探针·名单里没有它 → 判未解锁(Entered=false 但 Valid=true，这条要落地)", () =>
            {
                Dictionary<string, object> r = Gd.Map(Orchestration.ArkEventProbe(
                    Inp("date", "2026-10-12", "eventKey", "e", "stage", "YW-8", "obs", ovTrimmed)));
                return (bool)r["Valid"] && !(bool)r["Entered"]
                    && (string)r["Evidence"] == "MAA 名单里 2 关没有 YW-8(名单：LS-5、SK-5，当前选中 LS-5)";
            });
            b.Chk("探针·名单被剪空(from [\"\"]) → 进不去，重放行只补一句注解", () =>
            {
                Dictionary<string, object> r = Gd.Map(Orchestration.ArkEventProbe(
                    Inp("date", "2026-10-12", "eventKey", "e", "stage", "YW-8", "obs", ovEmpty)));
                return (bool)r["Valid"] && !(bool)r["Entered"]
                    && ((string)r["Evidence"]).Contains("被剪空")
                    && ((string)r["Evidence"]).EndsWith("；注意本轮内核走的是「回到上次作战」重放");
            });
            b.Chk("探针·名单压根没抄到(SawGuiPlan=false) → Valid=false 不落地，绝不当成没打通", () =>
            {
                Dictionary<string, object> r = Gd.Map(Orchestration.ArkEventProbe(
                    Inp("date", "2026-10-12", "eventKey", "e", "stage", "YW-8", "obs", ovNoPlan)));
                return !(bool)r["Valid"] && !(bool)r["Entered"] && ((string)r["Evidence"]).Contains("判不了");
            });
            b.Chk("探针·obs 缺或目标关空 → 一律 Valid=false 不抛", () =>
            {
                Dictionary<string, object> a = Gd.Map(Orchestration.ArkEventProbe(
                    Inp("date", "2026-10-12", "eventKey", "e", "stage", "YW-8")));
                Dictionary<string, object> c = Gd.Map(Orchestration.ArkEventProbe(
                    Inp("date", "2026-10-12", "eventKey", "e", "stage", "", "obs", ovYes)));
                return !(bool)a["Valid"] && !(bool)a["Entered"] && !(bool)c["Valid"]
                    && ((string)c["Evidence"]).Contains("没有可当探针的活动关");
            });
        }

        static object Obs(string asst, string gui, string date) { return Orchestration.MaaStageObserve(Inp("asstText", asst, "guiText", gui, "date", date)); }        static bool Of(object r) { return Gd.Truthy(Gd.Get(D(r), "Found")); }
        static string Og(object r) { return Gd.Str(Gd.Get(D(r), "Stage")); }
        static bool Ol(object r) { return Gd.Truthy(Gd.Get(D(r), "LastBattle")); }
        static bool Oo(object r) { return Gd.Truthy(Gd.Get(D(r), "SawGuiPlan")); }
        static object[] Ov(object r) { return Gd.List(Gd.Get(D(r), "Survivors")); }
        static string Oc(object r) { return Gd.Str(Gd.Get(D(r), "Selected")); }
        static string Oq(object r) { return Gd.Str(Gd.Get(D(r), "Date")); }
    }
}
