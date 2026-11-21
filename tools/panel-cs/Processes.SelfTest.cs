// tools/panel-cs/Processes.SelfTest.cs —— --proctest：批次 3(Processes.cs)的无头自检。
// 期望串逐字抄自 lib\maa.ps1 / lib\m9a.ps1 / lib\mumu.ps1 与旧树 lib\detect.ps1(11-06 刀⑮(a) 整件出仓)的旧实现 ——
// 临时生成器(gd-proc-gen)在 HEAD 旧实现上跑同一套场景抓出 TSV，这里的期望从那份 TSV 解码一层搬来；
// 这里红了就是移植走样，改 Processes.cs，不改这里的期望(docs\ps-migration-batch0.md §7 同一条纪律)。
//
// 批次纪律：不起了进程、不碰真注册表、不写任何文件 —— MAA 配置/日志片段/状态包全部由场景直接喂进来。
// 顺带钉死两处旧实现原话里的大小写：farm 两档的 changes 是 PowerShell 显示的 True/False，
// plain 档清旗子是代码里写死的小写 false —— 两者都必须在 C# 侧原样存活。

using System;
using System.Collections.Generic;
using System.Globalization;

namespace GdPanel
{
    static class GdProcSelfTest
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

        // ---------- 小工具 ----------
        static Dictionary<string, object> D(object v) { return v as Dictionary<string, object>; }
        static string J(object v, string sep)
        {
            List<string> bits = new List<string>();
            foreach (object o in Gd.List(v)) bits.Add(Gd.Str(o));
            return string.Join(sep, bits.ToArray());
        }

        // ============================ --proctest ============================
        public static int ProcTest()
        {
            Box b = new Box();
            AsstGroup(b);
            MaaGroup(b);
            ArgsGroup(b);
            LineGroup(b);
            ResGroup(b);
            E2eGroup(b);
            MuMuGroup(b);
            AdbGroup(b);
            RelaunchGroup(b);
            return b.Result("--proctest");
        }

        // ---------- asst.log 读数/线索 ----------
        static void AsstGroup(Box b)
        {
            string dead = AsstLines(
                @"{""cur_task"":""StartUp@GameStart"",""details"":{""cur_retry"":0,""retry_times"":50}}",
                @"SubTaskStart {""details"":{""exec_times"":269,""task"":""GameStart""}}",
                @"Win32Controller click: (643, 666)",
                @"Win32Controller click: (647, 670)",
                @"Win32Controller click: (638, 663)");
            string swipe = AsstLines(
                @"{""cur_task"":""InfrastTimePhase"",""details"":{""cur_retry"":0,""retry_times"":5}}",
                @"SubTaskStart {""details"":{""exec_times"":1,""action"":""Swipe"",""task"":""SwipeToTheLeft""}}",
                @"asst::Win32Controller::inject_input_event | enter",
                @"asst::Win32Controller::inject_input_event | leave, 14 ms",
                @"asst::Win32Controller::inject_input_event | enter");
            string excerpt = AsstLines(
                @"[2026-09-24 18:23:26.192][INF][Px29472][Tx36949] {""class"":""asst::ProcessTask"",""cur_task"":""StartUp@GameStart"",""details"":{""cur_retry"":0,""retry_times"":50,""to_be_recognized"":[""StartUp@GameStart""]},""taskchain"":""StartUp"",""taskid"":0}",
                @"[2026-09-24 18:23:26.320][INF][Px29472][Tx36949] Assistant::append_callback | SubTaskStart {""class"":""asst::ProcessTask"",""details"":{""action"":""ClickSelf"",""algorithm"":""MatchTemplate"",""exec_times"":316,""max_times"":2147483647,""result"":{""rect"":[611,652,58,58],""score"":0.991275,""template"":""GameStart.png""},""task"":""GameStart""},""subtask"":""ProcessTask"",""taskchain"":""StartUp"",""taskid"":0}",
                @"[2026-09-24 18:23:26.320][TRC][Px29472][Tx36949] Win32Controller click: (629, 701)");
            string justOne = AsstLines(
                @"{""cur_task"":""RecruitBegin"",""details"":{""cur_retry"":0,""retry_times"":50}}",
                @"SubTaskStart {""details"":{""exec_times"":1,""task"":""RecruitBegin""}}",
                @"Win32Controller click: (400, 300)");

            b.Chk("asst/dead", delegate { return AsstStateStr(dead) == "StartUp@GameStart|269|3|0"; });
            b.Chk("asst/swipe", delegate { return AsstStateStr(swipe) == "InfrastTimePhase|1|0|2"; });
            b.Chk("asst/excerpt", delegate { return AsstStateStr(excerpt) == "StartUp@GameStart|316|1|0"; });
            b.Chk("asst/empty", delegate { return AsstStateStr("") == "|0|0|0"; });
            b.Chk("asst/hint-dead", delegate
            {
                return AsstHintStr(dead) == "MAA 内核其实还在跑，卡在子任务 StartUp@GameStart，已重复 269 次(最后点到 638,663)，同一处反复点击而界面不动 → 端游没把这些点击吃进去：要么真有个 MAA 不认识的窗口挡着，要么这一轮窗口不在前台/会话被锁(有人在机器前时，同样的发消息点击是点得动的)";
            });
            b.Chk("asst/hint-justOne", delegate { return AsstHintStr(justOne) == "MAA 内核其实还在跑，最后停在子任务 RecruitBegin"; });
            b.Chk("asst/hint-excerpt", delegate
            {
                return AsstHintStr(excerpt) == "MAA 内核其实还在跑，卡在子任务 StartUp@GameStart，已重复 316 次(最后点到 629,701)，同一处反复点击而界面不动 → 端游没把这些点击吃进去：要么真有个 MAA 不认识的窗口挡着，要么这一轮窗口不在前台/会话被锁(有人在机器前时，同样的发消息点击是点得动的)";
            });
            b.Chk("asst/hint-none", delegate { return AsstHintStr("[INF] asst::Controller::connect 123") == ""; });
            b.Chk("asst/hint-empty", delegate { return AsstHintStr("") == ""; });
        }
        static string AsstLines(params string[] ls) { return string.Join("\n", ls); }
        static string AsstStateStr(string text)
        {
            Dictionary<string, object> d = D(Processes.AsstState(Gd.Map(Gd.Out("text", text))));
            return Gd.Str(Gd.Get(d, "Task")) + "|" + Gd.Str(Gd.Get(d, "Exec")) + "|"
                 + Gd.Str(Gd.Get(d, "Clicks")) + "|" + Gd.Str(Gd.Get(d, "Inject"));
        }
        static string AsstHintStr(string text)
        {
            return Gd.Str(Gd.Get(D(Processes.AsstHint(Gd.Map(Gd.Out("text", text)))), "Hint"));
        }

        // ---------- MAA 队列规划(Set-MaaDailyConfig 的规则段) ----------
        const string CfgJson =
            @"{""Configurations"":{""Default"":{
 ""Gui"":{""StartUpSettings"":{""RunDirectly"":""yes-manual""},""RuntimeSettings"":{""StartGame"":true}},
 ""TaskQueue"":[
  {""TaskType"":""fight"",""Name"":""00:00 刷战斗"",""IsEnable"":true,""StagePlan"":[""OLD""],""UseMedicine"":true},
  {""TaskType"":""Recruit"",""Name"":""获得干员"",""IsEnable"":true},
  {""TaskType"":""StartUp"",""Name"":""启动"",""IsEnable"":false},
  {""TaskType"":""Fight"",""Name"":""剩余理智"",""IsEnable"":false}
 ]}}}";
        const string CfgJsonApply =
            @"{""Configurations"":{""Default"":{
 ""Gui"":{""StartUpSettings"":{""RunDirectly"":""yes-manual""},""RuntimeSettings"":{""StartGame"":true}},
 ""TaskQueue"":[
  {""TaskType"":""fight"",""Name"":""00:00 刷战斗"",""IsEnable"":true,""StagePlan"":[""OLD""],""UseMedicine"":true,""UseStone"":false,""UseExpiringMedicine"":false,
   ""EnableTargetDrop"":false,""DropId"":"""",""DropCount"":0,""IsInventoryTarget"":false,""EnableTimesLimit"":false,""TimesLimit"":0},
  {""TaskType"":""Recruit"",""Name"":""获得干员"",""IsEnable"":true},
  {""TaskType"":""StartUp"",""Name"":""启动"",""IsEnable"":false},
  {""TaskType"":""Fight"",""Name"":""剩余理智"",""IsEnable"":false,""StagePlan"":[],""UseMedicine"":false,""UseStone"":false,""UseExpiringMedicine"":false,
   ""EnableTargetDrop"":false,""DropId"":"""",""DropCount"":0,""IsInventoryTarget"":false,""EnableTimesLimit"":false,""TimesLimit"":0}
 ]}}}";

        static Dictionary<string, object> Plan(string cfgJson, string stage, string mode, bool clearSanity, string farmMode)
        {
            return D(Processes.PlanMaaQueue(Gd.Map(Gd.Out(
                "config", GdJson.Deserialize(cfgJson),
                "wantFinish", "剩余理智", "alsoEnableMainFight", true, "launchArkFromHelper", true,
                "keepOn", new string[] { "StartUp", "Miner" }, "turnOff", new string[] { "Recruit" },
                "table", Gd.Map(Gd.Out("CE-5", 6, "LS-5", 15, "SK-5", 30, "CA-5", 30)),
                "mode", mode, "clearSanity", clearSanity, "stage", stage,
                "farmActive", farmMode.Length > 0, "farmMode", farmMode,
                "farmItemId", "3003", "farmGap", 12, "farmTarget", 99, "farmTimes", 5))));
        }
        static string PlanStr(Dictionary<string, object> d)
        {
            return "Changes=" + J(Gd.Get(d, "Changes"), "\n")
                 + "\nStage=" + Gd.Str(Gd.Get(d, "Stage"))
                 + "\nChains=" + J(Gd.Get(d, "ExpectedChains"), ",");
        }
        static string FarmLines(string name, string tag, string dropCount, string invTarget)
        {
            return FarmLines(name, tag, "3003", dropCount, "5", invTarget);
        }
        static string FarmLines(string name, string tag, string dropId, string dropCount, string timesLimit, string invTarget)
        {
            return "\n" + name + " → EnableTargetDrop = True(" + tag + ")"
                 + "\n" + name + " → DropId = " + dropId + "(" + tag + ")"
                 + "\n" + name + " → DropCount = " + dropCount + "(" + tag + ")"
                 + "\n" + name + " → IsInventoryTarget = " + invTarget + "(" + tag + ")"
                 + "\n" + name + " → EnableTimesLimit = True(" + tag + ")"
                 + "\n" + name + " → TimesLimit = " + timesLimit + "(" + tag + ")"
                 + "\n" + name + " → UseMedicine = False(" + tag + ")"
                 + "\n" + name + " → UseStone = False(" + tag + ")"
                 + "\n" + name + " → UseExpiringMedicine = False(" + tag + ")";
        }

        static void MaaGroup(Box b)
        {
            const string Head = "Changes=StartUpSettings.RunDirectly = true(启动后自动开始)\nRuntimeSettings.StartGame = false(端游改由日常脚本先拉起来，MAA 没存端游路径)";
            const string Mid = "\n获得干员 → IsEnable=False\n启动 → IsEnable=True\n剩余理智 → IsEnable=True";

            b.Chk("maa/append", delegate
            {
                string want = Head
                    + "\n00:00 刷战斗 → StagePlan = CE-5,LS-5" + FarmLines("00:00 刷战斗", "材料补刷·目标库存", "99", "True")
                    + Mid
                    + "\n剩余理智 → StagePlan = CE-5,LS-5" + FarmLines("剩余理智", "材料补刷·目标库存", "99", "True")
                    + "\nStage=CE-5,LS-5\nChains=fight,StartUp,Fight";
                return PlanStr(Plan(CfgJson, "CE-5,LS-5", "event", true, "append")) == want;
            });
            b.Chk("maa/dedicated", delegate
            {
                string want = Head
                    + "\n00:00 刷战斗 → StagePlan = CE-5" + FarmLines("00:00 刷战斗", "材料补刷专用轮", "12", "False")
                    + Mid
                    + "\n剩余理智 → StagePlan = CE-5" + FarmLines("剩余理智", "材料补刷专用轮", "12", "False")
                    + "\nStage=CE-5\nChains=fight,StartUp,Fight";
                return PlanStr(Plan(CfgJson, "CE-5", "event", true, "dedicated")) == want;
            });
            b.Chk("maa/plain无farm", delegate
            {
                string want = Head
                    + "\n00:00 刷战斗 → StagePlan = CE-5,SK-5" + FarmLines("00:00 刷战斗", "默认档护甲", "4001", "999999", "20", "False")
                    + Mid
                    + "\n剩余理智 → StagePlan = CE-5,SK-5" + FarmLines("剩余理智", "默认档护甲", "4001", "999999", "20", "False")
                    + "\nStage=CE-5,SK-5\nChains=fight,StartUp,Fight";
                return PlanStr(Plan(CfgJson, "CE-5,SK-5", "event", true, "")) == want;
            });
            b.Chk("maa/长草WARN", delegate
            {
                Dictionary<string, object> d = Plan(CfgJson, "CE-5,NOPE-9", "event", true, "");
                List<string> ls = new List<string>();
                foreach (object lo in Gd.List(Gd.Get(d, "Logs")))
                {
                    Dictionary<string, object> l = D(lo);
                    ls.Add(Gd.Str(Gd.Get(l, "Level")) + " " + Gd.Str(Gd.Get(l, "Text")));
                }
                return string.Join("\n", ls.ToArray()) == "WARN 活动关卡 NOPE-9 还不在 MAA 本地关卡表(资源可能没更到)，仍按接口结果写入\nINFO 理智计划 CE-5,NOPE-9(未知关卡：NOPE-9)";
            });
            b.Chk("maa/idle拒", delegate
            {
                try { Plan(CfgJson, "NOT-A-STAGE", "idle", true, ""); return false; }
                catch (GdRuleException ex)
                {
                    return ex.Message == "长草计划里的关卡代号不被 MAA 识别：NOT-A-STAGE → 请改 settings.json 的 ark 计划";
                }
            });
            b.Chk("maa/空计划拒", delegate
            {
                try { Plan(CfgJson, " , ；", "event", true, ""); return false; }
                catch (GdRuleException ex) { return ex.Message == "理智计划为空：检查 settings.ark.defaultStage / planByWeekday"; }
            });
            b.Chk("maa/空队列拒", delegate
            {
                try { Plan(@"{""Configurations"":{""Default"":{""TaskQueue"":[]}}}", "CE-5", "event", true, ""); return false; }
                catch (GdRuleException ex) { return ex.Message == "MAA 配置里的 TaskQueue 为空或结构不认识(预期为数组)"; }
            });
            b.Chk("maa/缺Default拒", delegate
            {
                try { Plan("{}", "CE-5", "event", true, ""); return false; }
                catch (GdRuleException ex) { return ex.Message == "MAA 配置里找不到 Configurations.Default，请检查 MAA 版本/实例名"; }
            });
            b.Chk("maa/键缺失算一项", delegate   // TaskQueue 键整个缺失 = @($null) 长度 1，不是"为空"
            {
                Dictionary<string, object> d = Plan(@"{""Configurations"":{""Default"":{}}}", "CE-5", "event", true, "");
                return Gd.Str(Gd.Get(d, "Stage")) == "CE-5";
            });
            // apply：NewConfig 树(壳写盘的就是它)逐键核对，串口径照抄生成器里那行 "$($_.Name):$($_.IsEnable):…"
            b.Chk("maa/apply落盘", delegate
            {
                Dictionary<string, object> d = Plan(CfgJsonApply, "CE-5,LS-5", "event", true, "append");
                Dictionary<string, object> cfg = D(Gd.Get(d, "NewConfig"));
                Dictionary<string, object> def = D(Gd.Get(cfg, "Configurations"));
                def = D(Gd.Get(def, "Default"));
                string run = Gd.Str(Gd.Get(D(Gd.Get(D(Gd.Get(def, "Gui")), "StartUpSettings")), "RunDirectly"));
                string start = Gd.Str(Gd.Get(D(Gd.Get(D(Gd.Get(def, "Gui")), "RuntimeSettings")), "StartGame"));
                List<string> qs = new List<string>();
                foreach (object qo in Gd.List(Gd.Get(def, "TaskQueue")))
                {
                    Dictionary<string, object> qi = D(qo);
                    qs.Add(Gd.Str(Gd.Get(qi, "Name")) + ":" + Gd.Str(Gd.Get(qi, "IsEnable")) + ":" + J(Gd.Get(qi, "StagePlan"), "+")
                         + ":DM=" + Gd.Str(Gd.Get(qi, "UseMedicine")) + ":DC=" + Gd.Str(Gd.Get(qi, "DropCount"))
                         + ":IT=" + Gd.Str(Gd.Get(qi, "IsInventoryTarget")) + ":TL=" + Gd.Str(Gd.Get(qi, "TimesLimit")));
                }
                string q = string.Join("|", qs.ToArray());
                return run == "True" && start == "False"
                    && q == "00:00 刷战斗:True:CE-5+LS-5:DM=False:DC=99:IT=True:TL=5|获得干员:False::DM=:DC=:IT=:TL=|启动:True::DM=:DC=:IT=:TL=|剩余理智:True:CE-5+LS-5:DM=False:DC=99:IT=True:TL=5";
            });
            // Intent：壳的回读核对靠它逐项比 IsEnable，键序=队列原序、值是真 bool
            b.Chk("maa/intent", delegate
            {
                Dictionary<string, object> d = Plan(CfgJson, "CE-5,LS-5", "event", true, "append");
                List<string> ias = new List<string>();
                foreach (object io in Gd.List(Gd.Get(d, "Intent")))
                {
                    Dictionary<string, object> ie = D(io);
                    ias.Add(Gd.Str(Gd.Get(ie, "Name")) + "=" + Gd.Str(Gd.Get(ie, "Enable")));
                }
                return string.Join(";", ias.ToArray()) + ";" == "00:00 刷战斗=True;获得干员=False;启动=True;剩余理智=True;";
            });
        }

        // ---------- M9A 启动参数 ----------
        static void ArgsGroup(Box b)
        {
            b.Chk("args/id", delegate { return ArgsStr("id", true, true) == "--instance,deadbeef,--autostart,--quit-after-run,--force-start"; });
            b.Chk("args/name", delegate { return ArgsStr("name", true, true) == "--instance,日常-长草,--autostart,--quit-after-run,--force-start"; });
            b.Chk("args/noforce", delegate { return ArgsStr("id", true, false) == "--instance,deadbeef,--autostart,--quit-after-run"; });
            b.Chk("args/nocli", delegate { return ArgsStr("id", false, true) == ""; });
        }
        static string ArgsStr(string mode, bool cliArgs, bool forceStart)
        {
            Dictionary<string, object> d = D(Processes.M9aLaunchArgs(Gd.Map(Gd.Out(
                "cliArgs", cliArgs, "mode", mode, "id", "deadbeef", "name", "日常-长草", "forceStart", forceStart))));
            return J(Gd.Get(d, "Args"), ",");
        }

        // ---------- M9A 单行解析(整批 verb 喂单行) ----------
        const string PLong = @"[2026-08-30 12:18:45.984][INF] [cfg=Default][inst=日常-活动/cafebabe][src=Worker][op=ExecuteTaskQueue] >>> 任务 启动任务开始（异步）。";

        static void LineGroup(Box b)
        {
            b.Chk("line/conn-start", delegate { return Line1(PLong, "") == "connect-start:"; });
            b.Chk("line/chain-start", delegate { return Line1(PLong.Replace("启动任务开始", "队列任务开始"), "") == "chain-start:"; });
            b.Chk("line/chain-done", delegate { return Line1(PLong.Replace("启动任务开始（异步）。", "任务已全部完成！"), "") == "chain-done:"; });
            b.Chk("line/finish", delegate { return Line1(PLong.Replace("启动任务开始", "启动任务完成"), "") == "finish:"; });
            b.Chk("line/task", delegate { return Line1(@"[..][cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 开始任务：收取荒原", "") == "task:收取荒原"; });
            b.Chk("line/filter-out", delegate { return Line1(PLong, "deadbeef") == "NULL"; });
            b.Chk("line/filter-in", delegate { return Line1(PLong, "cafebabe") == "connect-start:"; });
            b.Chk("line/no-inst", delegate { return Line1(@"[..][cfg=Default] 任务已全部完成！", "zzz") == "chain-done:"; });
            b.Chk("line/fail", delegate { return Line1(@"[..][inst=x/deadbeef][src=Worker][op=StopTask] 停止前状态：FAILED", "") == "fail:停止前状态：FAILED"; });
            // 反例格（刀㉓ 笔(b) 第三子批 第一刀）：SUCCEEDED 是正常收尾行，一条 fail 事件都不许产出。
            // 旧形只在 PS 侧量（把整轮日志喂进去再问 $sI.failed）——核侧「停止前状态：」这个词只认 FAILED
            // 这一半，从来没人从核侧答过一次；这格把这半条语义在核侧钉死，将来有人放宽那条正则当场红。
            b.Chk("line/succeeded", delegate { return Line1(@"[..][inst=x/deadbeef][src=Worker][op=StopTask] 停止前状态：SUCCEEDED", "") == "NULL"; });
            b.Chk("line/noise", delegate { return Line1(@"[..][inst=x/deadbeef][src=Agent][op=StdErr] 如前面有提示新资源版本还请更新", "") == "noise:"; });
            b.Chk("line/null", delegate { return Line1(@"[..][cfg=Default] [内存管理]释放了11.97 MB（21.1%)", "") == "NULL"; });
            // 对面格（刀㉓ 笔(b) 第三子批 第六刀）：本格原在 PS 侧 test-r1999 的 [t14] 段（「新版文案不认识时
            // Read-GdM9ALine 返回 $null」）。游戏新版本若改口说「任务链已全部执行」，解析器要么显式立格认它，
            // 要么按不认识算——不许硬凑一个事件。打红它的唯一改法是放宽 Processes.cs 的 chain-done 那一行，
            // 把这句也认成整条链路完成，那正是本格钉住的东西。
            b.Chk("line/unknown-2", delegate { return Line1(@"[2026-xx][INF] [inst=日常-长草/deadbeef] 任务链已全部执行 SUCCESS", "deadbeef") == "NULL"; });
            b.Chk("line/ui-start", delegate
            {
                return Line1(@"[2026-09-23 17:30:40.048][INF] [cfg=Default][inst=日常-长草/deadbeef][src=UI][op=StartTask] 用户操作：启动任务 | controller=Adb, resource=官服, device=<none>, selectedTasks=10", "") == "ui-start:";
            });
            b.Chk("line/queue", delegate { return Line1(@"[..][inst=日常-长草/deadbeef][src=Worker][op=ExecuteTaskQueue] 准备执行任务队列：任务数量=10", "") == "queue-prepared:10"; });
            b.Chk("line/task-fail", delegate { return Line1(@"[..][op=Worker] 任务失败：常规作战", "") == "task-fail:常规作战"; });
            b.Chk("line/res-c", delegate { return Line1("当前资源版本：v4.7.1", "") == "res-current:v4.7.1"; });
            b.Chk("line/res-l", delegate { return Line1("最新资源版本：v4.10.0", "") == "res-latest:v4.10.0"; });
            b.Chk("line/conn-fail", delegate { return Line1(@"[cfg=Default][op=Boot] >>> 任务 初始化控制器失败 | xxx", "") == "connect-fail:初始化控制器失败 | xxx"; });
            b.Chk("line/conn-fail2", delegate { return Line1(@"[Record] 连接失败", "") == "connect-fail:连接失败"; });
            b.Chk("lines/bulk", delegate
            {
                string text = PLong + "\n无关行 [内存管理]释放了11.97 MB（21.1%)\n当前资源版本：v4.7.1\n最新资源版本：v4.10.0\n\n任务失败：常规作战";
                Dictionary<string, object> d = D(Processes.M9aLines(Gd.Map(Gd.Out("text", text, "instanceId", "deadbeef"))));
                List<string> evs = new List<string>();
                foreach (object eo in Gd.List(Gd.Get(d, "Events")))
                {
                    Dictionary<string, object> e = D(eo);
                    evs.Add(Gd.Str(Gd.Get(e, "Kind")) + ":" + Gd.Str(Gd.Get(e, "Text")));
                }
                return string.Join(",", evs.ToArray()) == "res-current:v4.7.1,res-latest:v4.10.0,task-fail:常规作战";
            });
        }
        static string Line1(string text, string instanceId)
        {
            Dictionary<string, object> d = D(Processes.M9aLines(Gd.Map(Gd.Out("text", text, "instanceId", instanceId))));
            object[] evs = Gd.List(Gd.Get(d, "Events"));
            if (evs.Length == 0) return "NULL";
            Dictionary<string, object> e = D(evs[0]);
            return Gd.Str(Gd.Get(e, "Kind")) + ":" + Gd.Str(Gd.Get(e, "Text"));
        }

        // ---------- M9A 状态结论 ----------
        static void ResGroup(Box b)
        {
            b.Chk("res/blocked", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("启动游戏", "日常"), "failedTasks", S("启动游戏"),
                               "queueCount", 10, "eventCount", 5, "resCurrent", "v4.7.1", "resLatest", "v4.10.0",
                               "connectFail", "AdbSerial 为空"), 300, 0, true, "log-x.log")
                    == "blocked|" + J2(S("启动游戏", "日常")) + "|" + J2(S("启动游戏"))
                     + "|M9A 有 1 个任务报『任务失败』(第一个任务[启动游戏]就失败，后面 1 个都是在没进游戏的情况下空转)。先看模拟器：客户端多半停在更新/下载界面或已退回桌面，不是判定库的问题；M9A 报资源/程序落后(当前 v4.7.1 → 最新 v4.10.0)——这是整包更新，命令行没有更新入口，得在 M9A 界面点『更新』；M9A 连不上模拟器(AdbSerial 为空)——多半是 MuMu 虚拟机没起来或 adb 设备指纹变了；失败任务：启动游戏；日志：log-x.log";
            });
            b.Chk("res/全失败", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("A", "B"), "failedTasks", S("A", "B"), "eventCount", 4),
                              300, 0, true, "log-a.log")
                    == "blocked|" + J2(S("A", "B")) + "|" + J2(S("A", "B"))
                     + "|M9A 有 2 个任务报『任务失败』(第一个任务[A]就失败，后面 1 个都是在没进游戏的情况下空转)。先看模拟器：客户端多半停在更新/下载界面或已退回桌面，不是判定库的问题；失败任务：A、B；日志：log-a.log";
            });
            b.Chk("res/ok", delegate
            {
                return ResStr(St("chainStarted", true, "chainDone", true, "tasks", S("任务1", "任务2"), "eventCount", 4), 40, 0, false, "")
                    == "ok|" + J2(S("任务1", "任务2")) + "||实例[日常-长草] 完成 2 个任务";
            });
            b.Chk("res/ok-quit", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("任务1", "任务2", "任务3"), "queueCount", 3, "eventCount", 5), 430, 0, true, "")
                    == "ok|" + J2(S("任务1", "任务2", "任务3")) + "||实例[日常-长草] 完成 3 个任务/队列声明 3 个(M9A 已按 --quit-after-run 退出，未见完成行)";
            });
            b.Chk("res/ok带尾警", delegate
            {
                return ResStr(St("chainStarted", true, "chainDone", true, "tasks", S("任务1"), "failed", "收尾炸了", "eventCount", 3), 40, 0, false, "")
                    == "ok|" + J2(S("任务1")) + "||实例[日常-长草] 完成 1 个任务(收尾阶段有告警：收尾炸了)";
            });
            // 中间任务单独失败（实测「使用兑换码」没码可换就这样）→ 整轮不许判红，但失败清单必须进告警。
            // 这格是 res/ok带尾警 的另一半：那格量的是 failed（收尾告警），这格量的是 failedTasks（任务级告警），
            // 走的是 Processes.cs 里两条不同的拼接分支；PS 侧旧格只在 t17 现场喂事件后问一句 Status，
            // 整句告警话术「其中 N 个任务报过『任务失败』」核侧此前没人答过。
            b.Chk("res/ok-中间失败带告警", delegate
            {
                return ResStr(St("chainStarted", true, "chainDone", true, "tasks", S("启动游戏", "使用兑换码"),
                                 "failedTasks", S("使用兑换码"), "queueCount", 10, "eventCount", 5), 300, 0, true, "")
                    == "ok|" + J2(S("启动游戏", "使用兑换码")) + "||实例[日常-长草] 完成 2 个任务/队列声明 10 个(其中 1 个任务报过『任务失败』：使用兑换码)";
            });
            b.Chk("res/empty-queue-声明", delegate
            {
                return ResStr(St("chainDone", true, "queueCount", 10, "eventCount", 2), 5, 0, false, "")
                    == "empty-queue|||M9A 连接完成后直接报『任务已全部完成』，但一条『开始任务：』都没有(用时 0.1 分钟) → M9A 自己报的任务数量=10(声明了却没执行，通常是界面没进游戏)；实例[日常-长草]";
            });
            b.Chk("res/empty-queue-连接", delegate
            {
                return ResStr(St("chainDone", true, "connectFail", "控制器初始化失败", "queueCount", 10, "eventCount", 3), 90, 0, false, "")
                    == "empty-queue|||M9A 连接完成后直接报『任务已全部完成』，但一条『开始任务：』都没有(用时 1.5 分钟) → M9A 连模拟器这一步就失败了(声明了 10 个任务，一个都没开始)；M9A 连不上模拟器(控制器初始化失败)——多半是 MuMu 虚拟机没起来或 adb 设备指纹变了；实例[日常-长草]";
            });
            // 负向锚格（09-25 19:29 那轮定下的口径）：「声明了 10 个却一个没开始」不许说成"队列为空"——
            // 上面那格的整句里恰好没写这个词，但整句期望不等于负向锚（有人改话术时正好把它加回来，整句照样对不上才红；
            // 而这格专门盯这个词）。判据是核侧 allQueuedDone/qc 分流那条：qc 在场就走"连模拟器这一步就失败"分支。
            b.Chk("res/empty-queue-不说队列为空", delegate
            {
                return ResStr(St("chainDone", true, "connectFail", "初始化控制器失败：AdbSerial 为空",
                                 "queueCount", 10, "eventCount", 3), 30, 0, false, "log-20260925.log")
                    .Contains("队列为空") == false;
            });
            b.Chk("res/empty-queue-下发", delegate
            {
                return ResStr(St("chainDone", true, "startRequested", true, "eventCount", 2), 60, 0, false, "")
                    == "empty-queue|||M9A 连接完成后直接报『任务已全部完成』，但一条『开始任务：』都没有(用时 1 分钟) → 开始命令下发了但没有任何任务(任务列表可能全被取消勾选)；实例[日常-长草]";
            });
            b.Chk("res/empty-queue-没收到", delegate
            {
                return ResStr(St("chainDone", true, "eventCount", 1), 60, 0, false, "")
                    == "empty-queue|||M9A 连接完成后直接报『任务已全部完成』，但一条『开始任务：』都没有(用时 1 分钟) → M9A 从未收到\"用户操作：启动任务\" → 命令行 --autostart 没生效(注意：短参数 -a 实测无效，必须用长参数)；实例[日常-长草]";
            });
            // 刀㉓ 笔(b) 第三子批 第三刀(11-08)：M9A 自报「0 个任务」那一档——PS 侧旧格 [t12]「任务数量=0 时
            // empty-queue 会引用 M9A 自己报的数」量的是 Processes.cs:529 的 qc 分流，而上面五枚 empty-queue 格
            // 喂的全是 queueCount=10 或根本没喂 qc ⇒ 0 值整句核侧此前没人答过。这格必须有才谈得上摘 PS 那一格：
            // 0 是 falsy，谁把 :529 写成「qc 真值才走这臂」，这格当场红而其余五格照绿（旧四枚缺口格同款口径）。
            b.Chk("res/empty-queue-零任务引用自报数", delegate
            {
                return ResStr(St("chainDone", true, "queueCount", 0, "eventCount", 2), 5, 0, false, "")
                    == "empty-queue|||M9A 连接完成后直接报『任务已全部完成』，但一条『开始任务：』都没有(用时 0.1 分钟) → M9A 自己报的任务数量=0(声明了却没执行，通常是界面没进游戏)；实例[日常-长草]";
            });
            b.Chk("res/unknowfmt-退", delegate
            {
                return ResStr(St("eventCount", 0, "connectPhase", "done"), 100, 0, true, "log-z.log")
                    == "unknown-format|||M9A 已退出，但日志里一条认识的标志都没解析出来(实例[日常-长草] id=deadbeef，日志：log-z.log) → 多半是 M9A 更新后改了日志文案，或实例重新导入后 id 变了(行内 [inst=名字/id] 过滤把整批行排除了)，也可能是 M9A 压根没往这个文件里写(进程卡在启动/日志目录改了)。这轮结果不可信，别按它判定体力是否清完：先跑 tools\\probe-m9a-history.ps1 看实例 id、再用 tools\\replay-m9a-log.ps1 对着这份日志复现，然后更新 lib\\m9a.ps1 的 Read-GdM9ALine";
            });
            b.Chk("res/unknowfmt-活", delegate
            {
                return ResStr(St("eventCount", 0), 700, 0, false, "log-z.log")
                    == "unknown-format|||启动已 11.7 分钟，日志里一条认识的标志都没解析出来(实例[日常-长草] id=deadbeef，日志：log-z.log) → 多半是 M9A 更新后改了日志文案，或实例重新导入后 id 变了(行内 [inst=名字/id] 过滤把整批行排除了)，也可能是 M9A 压根没往这个文件里写(进程卡在启动/日志目录改了)。这轮结果不可信，别按它判定体力是否清完：先跑 tools\\probe-m9a-history.ps1 看实例 id、再用 tools\\replay-m9a-log.ps1 对着这份日志复现，然后更新 lib\\m9a.ps1 的 Read-GdM9ALine";
            });
            // 负向锚（刀㉓ 笔(b) 第三子批 第七刀）：本格原在 PS 侧 test-r1999 的 [t14] 段。
            // 「整句里恰好没写」不是负向锚（在册判语）——这里显式钉住：unknown-format 的详情不许声称
            // --autostart 没生效；那句只属于 not-started／empty-queue 两家的诊断。把 unknownDetail 掺进
            // 这个词（或把本档改去复用别家的诊断串），本格当场红，而整句格也会一起红——异值同句，照登。
            b.Chk("res/unknowfmt-不谎称autostart", delegate
            {
                string s = ResStr(St("eventCount", 0), 700, 0, false, "log-test.log");
                return s.StartsWith("unknown-format|||") && s.Contains("--autostart 没生效") == false;
            });
            b.Chk("res/error-失败", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("常规作战"), "failed", "任务运行失败！", "eventCount", 2), 300, 0, true, "")
                    == "error|" + J2(S("常规作战")) + "||M9A 已退出，链路失败：任务运行失败！(已到[常规作战])";
            });
            b.Chk("res/error-半程", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("任务1"), "queueCount", 10, "eventCount", 3), 430, 0, true, "")
                    == "error|" + J2(S("任务1")) + "||M9A 进程提前退出(已到[任务1]，队列声明 10 个)";
            });
            b.Chk("res/notstart-退", delegate
            {
                return ResStr(St("connectPhase", "done", "eventCount", 2), 30, 0, true, "log-t.log")
                    == "not-started|||M9A 连接后就退出了，一个任务都没跑(只完成了连接(队列任务从未开始)) → 命令行 --autostart 没生效(短参数 -a 实测无效)，或资源/程序有新版本要更新；日志：log-t.log";
            });
            b.Chk("res/notstart-活", delegate
            {
                return ResStr(St("connectPhase", "done", "eventCount", 2), 700, 0, false, "log-y.log")
                    == "not-started|||10 分钟内没跑任何任务(只完成了连接(队列任务从未开始))。M9A 连上了模拟器却没开始任务队列 → 命令行 --autostart 没生效(短参数 -a 实测无效)，或资源/程序有新版本要更新(日志里有\"还请更新\"这类提示)，或界面被弹窗挡住，日志：log-y.log";
            });
            b.Chk("res/notstart-没连上", delegate
            {
                return ResStr(St("eventCount", 2, "connectPhase", "started"), 700, 0, false, "log-y.log")
                    == "not-started|||10 分钟内没跑任何任务(正在连接模拟器)。M9A 连模拟器这一步都没走完，日志：log-y.log";
            });
            b.Chk("res/timeout", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("任务1"), "eventCount", 3), 7300, 0, false, "")
                    == "timeout|" + J2(S("任务1")) + "||超过 120 分钟未跑完(已到[任务1])";
            });
            b.Chk("res/stalled23", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("任务1"), "eventCount", 4), 700, 1380, false, "")
                    == "stalled|" + J2(S("任务1")) + "||M9A 日志 23 分钟无输出(已到[任务1])，可能卡在某个界面";
            });
            b.Chk("res/stalled99", delegate
            {
                return ResStr(St("chainStarted", true, "tasks", S("任务1"), "eventCount", 4), 5200, 5940, false, "")
                    == "stalled|" + J2(S("任务1")) + "||M9A 日志 99 分钟无输出(已到[任务1])，可能卡在某个界面";
            });
            b.Chk("res/零任务只有失败行", delegate
            {
                string s = ResStr(St("tasks", S(), "failedTasks", S("启动游戏"), "connectPhase", "none",
                                     "eventCount", 1), 60, 0, true, "log-t.log");
                // 整句期望＋负向锚两面：一条 task-fail 却没一条"开始任务"时，既不许套 blocked 的
                // "开始过的 0 个任务全部识别失败"（那是 allStartedFailed 前提里的 tasks.Length>0 在挡），
                // 也不许把这轮说成跑过任务——落点只有 not-started 这一家。
                return s == "not-started|||M9A 连接后就退出了，一个任务都没跑(尚未开始任何任务) → 命令行 --autostart 没生效(短参数 -a 实测无效)，或资源/程序有新版本要更新；日志：log-t.log"
                    && s.Contains("0 个任务全部") == false;
            });
            b.Chk("res/还在跑", delegate { return ResStr(St("eventCount", 1, "chainStarted", true), 60, 0, false, "") == "NULL"; });
            b.Chk("res/null-安静", delegate { return ResStr(St("chainStarted", true, "tasks", S("任务1"), "eventCount", 4), 700, 0, false, "") == "NULL"; });
        }
        static string[] S(params string[] v) { return v; }
        static string J2(string[] a) { return string.Join(",", a); }
        // St 只负责把"键,值,键,值…"拼成状态包(对应旧 $State hashtable)；结论调用在 ResStr 里做，
        // 场景参数(elapsed/quiet/gone/log)与实例信息、三个超时分钟数一次递进 resolve-m9a-status 的本体。
        static Dictionary<string, object> St(params object[] kv) { return Gd.Map(Gd.Out(kv)); }
        static string ResStr(Dictionary<string, object> state, double elapsed, double quiet, bool gone, string log)
        {
            Dictionary<string, object> d = D(Processes.M9aStatus(Gd.Map(Gd.Out(
                "state", state, "elapsed", elapsed, "quietSeconds", quiet, "processGone", gone, "logName", log,
                "instanceName", "日常-长草", "instanceId", "deadbeef",
                "startTimeoutMinutes", 10, "timeoutMinutes", 120, "stallTimeoutMinutes", 22))));
            if (!Gd.Truthy(Gd.Get(d, "Found"))) return "NULL";   // 还在跑 = Found:false，壳里翻译回 $null
            return Gd.Str(Gd.Get(d, "Status")) + "|" + Jv(Gd.Get(d, "Tasks")) + "|"
                 + Jv(Gd.Get(d, "FailedTasks")) + "|" + Gd.Str(Gd.Get(d, "Detail"));
        }
        static string Jv(object v)
        {
            List<string> bits = new List<string>();
            foreach (object o in Gd.List(v)) bits.Add(Gd.Str(o));
            return string.Join(",", bits.ToArray());
        }

        // ---------- M9A 真日志端到端(解析→累计→结论) ----------
        // 刀㉓ 笔(b) 第三子批 第八刀(11-08)：本组三格顶替 test-r1999 [t13] 四枚＋[t17] 三枚＋[t17] 那枚
        // 缺键累计格。旧留格理由写的是「拆开各段核侧各自绿不等于组合面有钉」——本组正面把那根链串起来：
        // 真日志行 → Processes.M9aLines 解析 → RunLoops.M9aEvents 逐拍累计(State 原样中继，与壳的 foreach 同形)
        // → Processes.M9aStatus 出结论。喂的是 09-23／09-25 两轮真日志原文(实例 id 用 deadbeef，与 PS 侧一致)。
        // 判据一个字没改：队列=10、SUCCEEDED 不算失败、finish 落 chainDone、两枚任务失败累计、版本差读到、
        // blocked 结论；缺键那格量的是「键根本不在(不是空数组)」这一形。
        static void E2eGroup(Box b)
        {
            string[] raw0923 = new string[]
            {
                "[2026-09-23 17:36:22.736][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Worker][op=ExecuteTaskQueue] 准备执行任务队列：任务数量=10",
                "[2026-09-23 17:36:22.770][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Worker][op=ExecuteTaskQueue] >>> 任务 启动任务开始（异步）。",
                "[2026-09-23 17:36:25.126][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 开始任务：启动游戏",
                "[2026-09-23 17:43:18.707][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 开始任务：使用兑换码",
                "[2026-09-23 17:43:33.122][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 开始任务：关闭游戏",
                "[2026-09-23 17:43:33.977][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Worker][op=ExecuteTaskQueue] >>> 任务 启动任务完成（异步）。",
                "[2026-09-23 17:43:33.978][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Worker][op=StopTask] 停止前状态：SUCCEEDED",
                "[2026-09-23 17:43:34.286][INF] [cfg=Default] 应用已关闭"
            };
            string[] raw0925 = new string[]
            {
                "[2026-09-25 19:24:21.177][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] [Record] 当前资源版本：v4.7.1",
                "[2026-09-25 19:24:21.185][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] [Record] 最新资源版本：v4.10.0",
                "[2026-09-25 19:24:23.165][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Worker][op=ExecuteTaskQueue] 准备执行任务队列：任务数量=10",
                "[2026-09-25 19:24:25.126][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 开始任务：启动游戏",
                "[2026-09-25 19:26:27.316][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 任务失败：启动游戏",
                "[2026-09-25 19:26:45.511][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 开始任务：收取荒原",
                "[2026-09-25 19:26:49.543][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Monitor][op=MonitorLog] 任务失败：收取荒原",
                "[2026-09-25 19:28:50.180][INF] [cfg=Default][inst=日常-长草/deadbeef][src=Worker][op=StopTask] 停止前状态：NOT_STARTED"
            };
            b.Chk("e2e/0923-命令行成功轮：队列=10、SUCCEEDED 不算失败、finish 落 chainDone、结论=ok", delegate
            {
                Dictionary<string, object> st = FreshState();
                foreach (object eo in EventsOf(raw0923)) st = Evoke(D(eo), st);
                bool acc = Gd.Int(Gd.Get(st, "queueCount"), -1) == 10 && Gd.Get(st, "failed") == null
                    && Gd.Truthy(Gd.Get(st, "chainDone"));
                Dictionary<string, object> d = StatusOf(st, 445, "log-20260923.log");
                return acc && Gd.Truthy(Gd.Get(d, "Found")) && Gd.Str(Gd.Get(d, "Status")) == "ok";
            });
            b.Chk("e2e/0925-更新包卡住轮：两枚任务失败累计(同名同序)、版本差读到、结论=blocked", delegate
            {
                Dictionary<string, object> st = FreshState();
                foreach (object eo in EventsOf(raw0925)) st = Evoke(D(eo), st);
                List<string> ft = new List<string>();
                foreach (object o in Gd.List(Gd.Get(st, "failedTasks"))) ft.Add(Gd.Str(o));
                bool acc = string.Join(",", ft.ToArray()) == "启动游戏,收取荒原"
                    && Gd.Str(Gd.Get(st, "resCurrent")) == "v4.7.1" && Gd.Str(Gd.Get(st, "resLatest")) == "v4.10.0";
                Dictionary<string, object> d = StatusOf(st, 280, "log-20260925.log");
                return acc && Gd.Truthy(Gd.Get(d, "Found")) && Gd.Str(Gd.Get(d, "Status")) == "blocked";
            });
            b.Chk("e2e/legacy-状态包没有 failedTasks 键(键根本不在，不是空数组)：task-fail 按累计 1 个算", delegate
            {
                Dictionary<string, object> st = St("tasks", new object[0], "chainStarted", false, "chainDone", false,
                    "connectPhase", "none", "failed", null, "queueCount", null, "startRequested", false, "eventCount", 0);
                Dictionary<string, object> r = D(RunLoops.M9aEvents(Gd.Map(Gd.Out("state", st, "events",
                    new object[] { Gd.Out("Kind", "task-fail", "Text", "启动游戏") }))));
                object[] ft = Gd.List(Gd.Get(D(Gd.Get(r, "State")), "failedTasks"));
                object[] logs = Gd.List(Gd.Get(r, "Logs"));
                return ft.Length == 1 && Gd.Str(Gd.Get(D(logs[0]), "Text")) == "M9A 任务失败：启动游戏(累计 1 个)";
            });
        }
        static Dictionary<string, object> FreshState()
        {
            return St("tasks", new object[0], "chainStarted", false, "chainDone", false, "connectPhase", "none",
                "failed", null, "queueCount", null, "startRequested", false, "eventCount", 0,
                "failedTasks", new object[0], "resCurrent", null, "resLatest", null, "connectFail", null);
        }
        static object[] EventsOf(string[] lines)
        {
            Dictionary<string, object> d = D(Processes.M9aLines(Gd.Map(Gd.Out(
                "text", string.Join("\n", lines), "instanceId", "deadbeef"))));
            return Gd.List(Gd.Get(d, "Events"));
        }
        static Dictionary<string, object> Evoke(Dictionary<string, object> ev, Dictionary<string, object> st)
        {
            return D(Gd.Get(RunLoops.M9aEvents(Gd.Map(Gd.Out("state", st, "events", new object[] { ev }))), "State"));
        }
        static Dictionary<string, object> StatusOf(Dictionary<string, object> st, double elapsed, string log)
        {
            return D(Processes.M9aStatus(Gd.Map(Gd.Out(
                "state", st, "elapsed", elapsed, "quietSeconds", 0.0, "processGone", true, "logName", log,
                "instanceName", "日常-长草", "instanceId", "deadbeef",
                "startTimeoutMinutes", 10, "timeoutMinutes", 120, "stallTimeoutMinutes", 22))));
        }

        // ==================== MuMu / adb ====================
        static void MuMuGroup(Box box)
        {
            Box b = box;
            b.Chk("mumu/state-ok", delegate
            {
                Dictionary<string, object> d = D(Processes.MumuState(Gd.Map(Gd.Out(
                    "code", 0, "out", @"{""is_android_started"":true,""is_process_started"":false,""name"":""MuMu安卓设备""}", "err", ""))));
                return Gd.Str(Gd.Get(d, "Available")) == "True" && Gd.Str(Gd.Get(d, "AndroidStarted")) == "True"
                    && Gd.Str(Gd.Get(d, "ProcessStarted")) == "False" && Gd.Str(Gd.Get(d, "Name")) == "MuMu安卓设备"
                    && Gd.Str(Gd.Get(d, "Raw")).StartsWith("{\"is_android_started");
            });
            b.Chk("mumu/state-非零退出", delegate
            {
                Dictionary<string, object> d = D(Processes.MumuState(Gd.Map(Gd.Out("code", 1, "out", "o不是一半", "err", "坏"))));
                return Gd.Str(Gd.Get(d, "Available")) == "False" && Gd.Str(Gd.Get(d, "Raw")) == "o不是一半坏";
            });
            b.Chk("mumu/state-坏JSON", delegate
            {
                Dictionary<string, object> d = D(Processes.MumuState(Gd.Map(Gd.Out("code", 0, "out", "{不是json", "err", ""))));
                return Gd.Str(Gd.Get(d, "Available")) == "False" && Gd.Str(Gd.Get(d, "Raw")) == "{不是json";
            });
            // 12 刀从已退役的 process 等值门(场景 mumu/state-3空输出)抄来：退出码 0 但 stdout 空 = 管理器没答话，
            // 与"退出码非零"同档(都别去 JSON 解析)；Raw 只拼 stdout+stderr(这里两者都空)。
            b.Chk("mumu/state-空输出(码0但没答话)", delegate
            {
                Dictionary<string, object> d = D(Processes.MumuState(Gd.Map(Gd.Out("code", 0, "out", "", "err", ""))));
                return Gd.Str(Gd.Get(d, "Available")) == "False" && Gd.Str(Gd.Get(d, "AndroidStarted")) == "False"
                    && Gd.Str(Gd.Get(d, "ProcessStarted")) == "False" && Gd.Str(Gd.Get(d, "Name")) == ""
                    && Gd.Str(Gd.Get(d, "Raw")) == "";
            });
            // 上格(门原样)与"坏 JSON"那格在 err 为空时同形 —— 这一格拿 stderr 当分水岭：
            // 码 0 但 stdout 空必须走"没答话"那条(把 stderr 也拼进 Raw)，不是"解析失败"那条(只留 stdout)。
            b.Chk("mumu/state-空输出但 stderr 有话说 → Raw 带上 stderr", delegate
            {
                Dictionary<string, object> d = D(Processes.MumuState(Gd.Map(Gd.Out("code", 0, "out", "", "err", "manager busy"))));
                return Gd.Str(Gd.Get(d, "Available")) == "False" && Gd.Str(Gd.Get(d, "Raw")) == "manager busy";
            });
            // 从 mumu/state-4没开机 抄来：两个布尔各判各的 —— 开机位 false、进程位 true 必须原样分家，
            // 不能用一个糊成一个(旧壳就是分别出 AndroidStarted/ProcessStarted 两格，调用方按需挑)。
            b.Chk("mumu/state-没开机(两个布尔各判各的)", delegate
            {
                Dictionary<string, object> d = D(Processes.MumuState(Gd.Map(Gd.Out("code", 0,
                    "out", @"{""is_android_started"":false,""is_process_started"":true,""name"":""other-vm""}", "err", ""))));
                return Gd.Str(Gd.Get(d, "Available")) == "True" && Gd.Str(Gd.Get(d, "AndroidStarted")) == "False"
                    && Gd.Str(Gd.Get(d, "ProcessStarted")) == "True" && Gd.Str(Gd.Get(d, "Name")) == "other-vm";
            });
            b.Chk("mumu/vms-两项", delegate
            {
                Dictionary<string, object> d = D(Processes.MumuVms(Gd.Map(Gd.Out("raw", @"{""0"":{""name"":""MuMu安卓设备"",""index"":0},""1"":{""name"":""第二台""}}"))));
                List<string> vs = new List<string>();
                foreach (object vo in Gd.List(Gd.Get(d, "Vms")))
                {
                    Dictionary<string, object> v = D(vo);
                    vs.Add(Gd.Str(Gd.Get(v, "Index")) + "=" + Gd.Str(Gd.Get(v, "Name")));
                }
                return string.Join(",", vs.ToArray()) == "0=MuMu安卓设备,1=第二台";
            });
            b.Chk("mumu/vms-坏形状全空", delegate
            {
                foreach (string raw in new string[] { "", "   ", "not json", "[1,2]", "null" })
                {
                    Dictionary<string, object> d = D(Processes.MumuVms(Gd.Map(Gd.Out("raw", raw))));
                    if (Gd.List(Gd.Get(d, "Vms")).Length != 0) return false;
                }
                return true;
            });
        }

        static void AdbGroup(Box b)
        {
            b.Chk("adb/devices-命中", delegate
            {
                Dictionary<string, object> d = D(Processes.AdbDevices(Gd.Map(Gd.Out(
                    "out", "List of devices attached\n127.0.0.1:7555\tdevice\n", "serial", "127.0.0.1:7555"))));
                return Gd.Str(Gd.Get(d, "Ok")) == "True";
            });
            b.Chk("adb/devices-没命中", delegate
            {
                Dictionary<string, object> d = D(Processes.AdbDevices(Gd.Map(Gd.Out(
                    "out", "List of devices attached\n127.0.0.1:161 device\n", "serial", "127.0.0.1:7555"))));
                return Gd.Str(Gd.Get(d, "Ok")) == "False";
            });
            b.Chk("adb/pid", delegate
            {
                Dictionary<string, object> d = D(Processes.AdbPidof(Gd.Map(Gd.Out("out", "  12345  \n"))));
                return Gd.Str(Gd.Get(d, "Found")) == "True" && Gd.Str(Gd.Get(d, "Pid")) == "12345";
            });
            b.Chk("adb/pid-none", delegate
            {
                Dictionary<string, object> d = D(Processes.AdbPidof(Gd.Map(Gd.Out("out", "no pid"))));
                return Gd.Str(Gd.Get(d, "Found")) == "False";
            });
            // 12 刀从已退役的 process 等值门(场景 adb/pid-multi)抄来：pidof 偶尔吐多枚(空格分隔)，
            // 旧实现先 \D 全剥再 [int]，拼起来就是 111222 —— 这条是"先剥再转"的口径，不是"取第一个"。
            b.Chk("adb/pid-多枚连写(先剥非数字再转)", delegate
            {
                Dictionary<string, object> d = D(Processes.AdbPidof(Gd.Map(Gd.Out("out", "111 222"))));
                return Gd.Str(Gd.Get(d, "Found")) == "True" && Gd.Str(Gd.Get(d, "Pid")) == "111222";
            });
            // 从 adb/pid-noise 抄来：输出里混着文字("pid noise 777x")也只留数字，777 就是 pid。
            b.Chk("adb/pid-混文字(只留数字)", delegate
            {
                Dictionary<string, object> d = D(Processes.AdbPidof(Gd.Map(Gd.Out("out", "pid noise 777x"))));
                return Gd.Str(Gd.Get(d, "Found")) == "True" && Gd.Str(Gd.Get(d, "Pid")) == "777";
            });
            b.Chk("adb/pid-超界不炸", delegate   // 旧实现在这里会 [int] 抛转换错；C# 侧明说 Found:false(注释在 Processes.cs)
            {
                Dictionary<string, object> d = D(Processes.AdbPidof(Gd.Map(Gd.Out("out", "99999999999999"))));
                return Gd.Str(Gd.Get(d, "Found")) == "False";
            });
        }

        // ---------- 提权重启拼参(批次 10，镜像 test-ark [t8]/[t9] 的六条形状) ----------
        static string RelaunchStr(Dictionary<string, object> inp)
        {
            List<string> parts = new List<string>();
            foreach (object o in Gd.List(Gd.Get(D(Processes.RelaunchArgs(inp)), "Args"))) parts.Add(Gd.Str(o));
            return string.Join(" ", parts.ToArray());
        }

        static Dictionary<string, object> Entry(string k, string t, object v)
        {
            return Gd.Out("K", k, "T", t, "V", v);
        }

        static void RelaunchGroup(Box b)
        {
            b.Chk("relaunch/骨架+force 原样+串值带引号", delegate
            {
                string s = RelaunchStr(Gd.Out(
                    "script", @"C:\games\ark-daily.ps1",
                    "omit", new object[] { "NoElevate" },
                    "force", new object[] { "-NoElevate" },
                    "entries", new object[] { Entry("Games", "str", "ark") }));
                return s == @"-NoProfile -ExecutionPolicy Bypass -File ""C:\games\ark-daily.ps1"" -NoElevate -Games ""ark""";
            });
            b.Chk("relaunch/omit 不分大小写、DryRun 永远不进、T=null 跳过", delegate
            {
                string s = RelaunchStr(Gd.Out(
                    "script", "x.ps1",
                    "omit", new object[] { "gAmEs" },              // -in 语义：大小写不敏感地跳掉 -Games
                    "force", new object[0],
                    "entries", new object[] {
                        Entry("Games", "str", "ark"),              // 被 omit 跳
                        Entry("DryRun", "flag", true),             // 保底永远跳
                        Entry("Mode", "null", null),               // 旧代码 -$null 分支
                        Entry("Tag", "str", "ok") }));
                return s == @"-NoProfile -ExecutionPolicy Bypass -File ""x.ps1"" -Tag ""ok""";
            });
            b.Chk("relaunch/flag 真值才加、假值整个不留", delegate
            {
                string on = RelaunchStr(Gd.Out("script", "x.ps1", "omit", new object[0], "force", new object[0],
                    "entries", new object[] { Entry("SkipCleanup", "flag", true) }));
                string off = RelaunchStr(Gd.Out("script", "x.ps1", "omit", new object[0], "force", new object[0],
                    "entries", new object[] { Entry("SkipCleanup", "flag", false) }));
                return on.EndsWith("-SkipCleanup") && off == @"-NoProfile -ExecutionPolicy Bypass -File ""x.ps1""";
            });
            b.Chk("relaunch/串值里的 \" 逐字复刻 -replace 成 \\\"", delegate
            {
                string s = RelaunchStr(Gd.Out("script", "x.ps1", "omit", new object[0], "force", new object[0],
                    "entries", new object[] { Entry("Note", "str", "a\"b") }));
                return s.EndsWith(@"-Note ""a\""b""");
            });
            b.Chk("relaunch/条目顺序=壳喂进来的 $Bound 枚举序", delegate
            {
                string s = RelaunchStr(Gd.Out("script", "x.ps1", "omit", new object[0], "force", new object[0],
                    "entries", new object[] { Entry("B", "str", "1"), Entry("A", "str", "2"), Entry("C", "str", "3") }));
                return s == @"-NoProfile -ExecutionPolicy Bypass -File ""x.ps1"" -B ""1"" -A ""2"" -C ""3""";
            });
        }
    }
}
