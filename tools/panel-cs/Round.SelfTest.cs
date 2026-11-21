// tools/panel-cs/Round.SelfTest.cs —— --roundtest：B-3②-a(Round.cs)轮骨架现场七枚 + ②-b1(Round.Data.cs)
// 判定段/门表段数据面六枚 + ②-b2(Chain.cs)链体丙档直答件十枚 + R9-a 收编那四枚(Round.cs 的屏保两枚与
// history 落盘一枚、Round.Data.cs 的活动图自证一枚)的纯规则段自检。
//
// 体裁照 --livetest(§11.50 ②-1 与 Live.SelfTest.cs 头注那条纪律)：**只钉话术与算术，不钉现场**。
//   * 不真弹 UAC(noSpawn)、不真打 webhook(fakeWebhookErr)、不起气泡(noSend 或 fakeToastErr)、
//     不真杀进程(noKill)、不读真计划任务(fake 档)；
//   * ②-b1 那六枚同一条规矩：**不真打 MAA 活动接口**(apiJson/apiErr 两枚夹具)、
//     **缓存与活动数据读盘**要么给 cacheJson/dataJson 夹具、要么把路径指到临时树上的那份文件；
//     写盘只写临时树的 state\，收尾整目录删掉；
//   * ②-b2 那十枚同一条规矩：**不真起子进程**(MuMu/端游/MAA/M9A 全由 nativeRuns / windows / running 三本夹具代答)、
//     **不真发输入**(needs-click 那一档只做登记，见下面够不着清单)、**配置写盘只写临时树的 chain\ 与 maa\ 两格**
//     (MAA config.json 与 M9A appsettings.json 各一份，前置内容按序由 Wf 复位)；
//     外置缓存(stageTableCache/itemMapsCache 两把进程内小抄)一律由入参喂 table / itemIndex，**下毒那一格排在族首**；
//   * R9-a 那四枚同一条规矩：**不真关屏保** —— `disable-screensaver-for-round` **不喂 active 的那一路整条不自证**
//     (那一档是 SPI 现读，读到"开着"就真发 SPI_SETSCREENSAVEACTIVE 把**用户现场**的屏保关掉，自检不许改现场)，
//     读数全由 active/setOk 两枚夹具代答；**写盘只写临时树**的 stateDir(history.csv 与 ark-event-probe.json 各一份)；
//     注册表那一半**只读**(HKCU\Control Panel\Desktop)，所以 config 这一枚敢留一格现读正例、外加一格"指到开不了的键"
//     的确定负例；CSV 那把舍入尺与字节形状(BOM 有无、行尾 CRLF)按 §11.67 的实测读数钉；
//   * 不要求这台机器装了 pwsh7、有管理员、有某个任务、有某个进程 —— 现场量一律夹具注入(旋钮身份见 Round.cs 头注)；
//   * 临时树里**故意不建 config\settings.json**：只要有一格漏喂 settings 夹具，SettingsNow()/SafeSettings()
//     就会去读那棵空树 ⇒ 当场红(那里还有一格专门钉这条守门，②-b1 的 read-ark-activity 同样有一格)。
//     唯一的例外是回落名册那一格，它喂 settings 而不靠现读；
//   * 写盘的几枚(state\last-notice.json、state\running.json、②-b1 的三本门表)全打在 InitForTest 的临时树上。
//
// 期望串逐字抄自 lib\common.ps1 的旧实现(Start-GdElevatedSelf / Send-GdNotice / Start-GdRunLock /
// Stop-GdRunLock / Get-GdTaskState / Stop-GdProcessTree / Get-GdElevateBatchLine)、lib\modules.ps1 的
// Invoke-GdDailyRound(通知标题那两句 —— 那枚十段真身已于 2026-10-20 乙批② 划掉，本件这几句是**抄定即冻结**的
// 转写体，不随工作树改口，原文只剩轮骨架门钉住的那份 blob)，以及 ②-b1 的六份真身(Get-ArkEvent / Get-R1999Event /
// Set-GdArkShopState / Set-GdR1999Ack / Set-GdR1999ShopState / Get-Gd{Ack,ShopDone}Map)、
// ②-b2 的十份真身(Invoke-GdTakeover / Stop-MaaAndGame / Stop-M9A(已于 11-06 转接刀·运行面刀⑫ 笔(a) 从 lib\m9a.ps1 删除，去向见 docs\rust-core-plan.md §11.252) / Get-GdMaa{FightSkip,StageObservation}／其中 Invoke-GdTakeover 与 Get-GdMaa{FightSkip,StageObservation} 三枚已于 11-05 转接刀·运行面刀⑨ 从 lib\common.ps1／lib\maa.ps1 删除，去向见 docs\rust-core-plan.md §11.241 /
// Start-GdArkClient＋Wait-GdArkLauncherReady(这两枚与上一行的 Stop-MaaAndGame 已于 11-03 转接刀·方舟拉起族从 lib\maa.ps1 删除，去向见 docs\rust-core-plan.md §11.217) / Start-GdMuMu / Set-MaaDailyConfig / Set-M9AInstance(已于 11-05 转接刀·运行面刀⑨ 从 lib\m9a.ps1 删除，去向见同一本 §11.241) /
// Update-GdArkFarmLedger —— 逐条出处见 Chain.cs 头注的两段双源登记)、
// R9-a 那四枚的对应真身(Get-GdScreensaverConfig(common.ps1 旧 359-378；该真身已于转接刀·屏保稳态族从 lib\common.ps1 删除，去向见 docs\rust-core-plan.md §11.215) / Enter-GdNoSleep 的 keepScreenOn 半段
// (该真身整枚已于丙档落码刀①从 lib\common.ps1 删除，去向见 docs\rust-core-plan.md §11.116) / Add-GdHistory(common.ps1 旧 712-740；该 PS 真身已于 11-07 刀⑰ 笔(c)-1 整枚出仓，全树零活调用，去向见 docs\rust-core-plan.md §11.274) / Update-GdArkEventProbe(ark-decisions.ps1:158-189；该真身已于 11-06 转接刀·运行面刀⑯ 从 lib\ark-decisions.ps1 删除，去向见 docs\rust-core-plan.md §11.262)
// —— 逐条出处见 Round.cs 与 Round.Data.cs 各自那段的头注)——
// 生产接线(②-d)之前它们仍是事实源(Enter-GdNoSleep 那半段、屏保 config 那枚与②-b2 的拉起族三枚除外——PS 侧原身已分别于丙档落码刀①、转接刀·屏保稳态族与转接刀·方舟拉起族删除，那几格没有原身可对)，这里红了就是移植走样，
// 改 Round.cs / Round.Data.cs / Chain.cs，不改这里的期望(批次 1 起同一条纪律)。
//
// 专门钉住的原样怪癖：① `-ne $false` 由**左侧类型**决定怎么折 $false ⇒ 字符串 "0" 算**开着**
// (FlagEnabled 第一版把数字 TryParse 摊到字符串上就判反了，这一格就是那次的守门)；
// ② Get-GdElevateBatchLine 的 '{0} {1}' 在空 ArgList 时留一个**尾随空格**，不 trim；
// ③ Stop-GdProcessTree 名曰树、实为按进程名，返回的是**匹配到的进程数**(Killed 与 Matched 同值就是这一格)；
// ④ activityCacheMaxHours 缺键＝0 ⇒ 缓存永远算过期，且那句 WARN 里插的是**原值**(缺键时是**空**，不是 0)；
// ⑤ 领空话术里的 dow 用原始 0-6(判定层用"周日=7")；⑥ 1999 确认话术那个箭头前没有空格；
// ⑦ 门表是**不分大小写**的表(PS 的 @{})；⑧ [math]::Round 与 Math.Round 同用银行家舍入(1.25→1.2)。
//   ②-b2 那十枚的原样怪癖另立六笔(Chain.cs 的第 7~12 笔：maxChecks 当 maxClicks 用、$clicks++ 在 if 之前、
//   拼参数不加引号、命中不睡超时多睡一轮、两份进程内缓存只在现场读那一路建、总结句带路径不进话术)，
//   本文件引用时照 Chain.cs 的编号说"第 N 笔"，不与上面①~⑧混号。
//
// 够不着的几样如实登记，不假装测过：
//   * 提权两支的真身(本机 pwsh runas / cmd 载体)与"读不到退出码"那一抛 —— 要等 ②-b 的 UAC 实弹；
//   * webhook 真 POST、气泡真弹、Schedule.Service COM 真读、Get-Process 真匹配 —— 夹具档一律绕开；
//   * 判活两把尺(unhandledMeansAlive)的分岔只在"读不到进程细节"那一档(提权轮/完整性级别不同)，
//     自检造不出那种进程，只钉"能读到时两把尺同值"；面板那侧的活体读回由 --exttest 五格随行自证。
//   * **真打 MAA 活动接口那一条 catch**(Invoke-WebRequest 的 Exception.Message 长什么样) —— 夹具只钉了话术的壳；
//   * **真 settings/真 state 盘**(临时树上的三本门表不等于玩家机器上那三本) —— 要等 ②-d 转接后的实跑；
//   * PS 那侧**还在按老路读写**这两张表(转接前双写不算，这里的"落盘"只证明形状与文件名对得上)。
//   ②-b2 那十枚另登记以下几样够不着，同样不假装测过：
//   * **needs-click 那一档的真点击**(PostMessage 打到真窗口)与它点完那 30 秒睡眠 —— 自检不许碰输入注入，
//     也不许给既有 verb 加 dry-run 键(§11.50 ②-1 那条判据)，相位序列只喂到 waiting／clear 两档；
//   * **真起端游进程、真等它出窗口**、以及经 start-ark-client 走完整路的`端游窗口就绪`＋暖身＋启动器更新页
//     (名册是静态夹具：命中就早退、不命中就撞超时，所以 LauncherReady 那五档改成**直调**自证)；
//   * **asst.log 尾 16MB 那把尺的本体**(自检喂的是小文件，只钉"取尾"这条路径被走到、形状对)；
//   * **takeover 那轮之后硬编码的 4 秒睡眠本体**(自检喂 sleepSeconds=0 免挂钟，钉的是它前面的名册与话术)；
//   * **`MAA 配置回读不一致`／`M9A 实例切换回读失败`那两抛**(要的是"写进去又读回别的"，夹具造不出)；
//   * **`认不出这一档相位`那一句**(模型回的东西不在映射表里 —— 自检把 launcherAsks 钉在四档内，那档走不到)。
// R9-a 那四枚另登记以下几样够不着，同样不假装测过：
//   * **真 SPI 两发**(SPI_GETSCREENSAVEACTIVE 的现读、以及读到"开着"之后真发 SPI_SETSCREENSAVEACTIVE 关掉它)
//     —— 后者会改**用户现场**，自检故意不许走(夹具 active/setOk 两枚把这一路整条代答)；要等现场那一轮的真实读数；
//   * **真注册表里那本屏保账**(自检只留了一格"读得到时形状自洽"＋一格"指到开不了的键 ⇒ Found=false"，
//     不钉本机装了哪枚 .scr)；
//   * **FileVersionInfo 真读到产品名**(假路径只能证明"读不到回空串不抛"那一半)；
//   * **真 UAC 那一发**(收编后的 `round-elevate` 走 `--relaunch-entry` 把入口路径递进核内、再由这一枚真起子进程；
//     自检与门都喂 noSpawn，两支载体的真退出码仍是 unobserved)；
//   * **真 history.csv 的存量续写**(临时树里那份不是玩家机器上那本；"表头只在文件不存在时写"这一条
//     只证明了形状，没证明与存量字节兼容 —— 那要看现场那一轮之后 history.csv 有没有多出乱码行)。
// 本套件里唯一一次起子进程是 DeadPid() 那一发 `cmd /d /c exit`(瞬退、无窗)：残留那一格没有别的取法，
// 与 --exttest 的同名助手同一先例。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GdPanel
{
    static class RoundSelfTest
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

        static Dictionary<string, object> In(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        static Dictionary<string, object> Row(object o) { return o as Dictionary<string, object>; }
        static string S(object v) { return v == null ? "(null)" : Gd.Str(v); }
        static bool B(object v) { return v != null && Gd.Truthy(v); }
        static int I(object v) { return Gd.Int(v, int.MinValue); }
        static object Key(object data, string k) { return Gd.Get(Row(data), k); }
        static int Len(object v) { return Gd.List(v).Length; }
        // 门表回的是**字典**，Len() 对字典会算"一个元素"(Gd.List 那条防摊开的守卫)，比格数必须用这把尺：
        // 不是字典回 -1(形状错要红得明显)，是字典回格数(对齐 PS 的 $map.Count)。
        static int Cnt(object v)
        {
            Dictionary<string, object> d = v as Dictionary<string, object>;
            return d == null ? -1 : d.Count;
        }

        // ②-b1 数据面那六枚的回执里混着"小数(小时/天数)"与"话术数组"，这两把尺只为读它们而存在：
        //   Dbl = 取数(缺键回 NaN，好让"该有数却没了"红得明显)；Ln = 取 Lines 里第 i 句(越界回 null)。
        static double Dbl(object v)
        {
            if (v == null) return double.NaN;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return double.NaN; }
        }
        static string Ln(object v, int i)
        {
            object[] a = Gd.List(v);
            return i < a.Length ? S(a[i]) : null;
        }
        // 抛出去的那句原话要逐字比(不只看"抛没抛")：Assert-GdGameId 的文案就是磁盘侧的用户可见话术。
        static object ThrownMsg(Func<object> run)
        {
            try { run(); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        // 哨兵串：只许住在夹具里，**绝不许出现在回执里**(泄漏面自己先过一道)。
        const string KeySentinel = "SENTINEL-VISION-KEY";
        const string HookSentinel = "https://sentinel.webhook.invalid/notify";

        // 拿一个已经退出的 PID(残留那一格没有别的取法；与 Panel.SelfTest 的 DeadPid 同一先例)
        static int DeadPid()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                psi.Arguments = "/d /c exit";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi)) { p.WaitForExit(); return p.Id; }
            }
            catch { return 0; }
        }

        static bool Thrown<T>(Func<object> run) where T : Exception
        {
            try { run(); return false; }
            catch (Exception ex) { return ex is T; }
        }

        // ==================== 刀⑪(build-round-ctx)的夹具两本＋一把键序尺 ====================
        // 声明序即协议(journal 的 init 与 ctx 做全等校验)，所以比键必须按序比，不能只比格数。
        static string KeysOf(object v)
        {
            Dictionary<string, object> d = v as Dictionary<string, object>;
            if (d == null) return "(not-dict)";
            StringBuilder sb = new StringBuilder();
            foreach (string k in d.Keys) { if (sb.Length > 0) sb.Append(","); sb.Append(k); }
            return sb.ToString();
        }

        // CtxSettings：只放 BuildCtx **逐枚点名**要的那几枚 paths/runtime/re1999。三件事各钉一处：
        //   * visionApiKey 故意住在 runtime 整块里 —— 它是"整块 settings 绝不进 ctx"那条硬约束的哨兵靶；
        //   * stallTimeoutMinutes 给串形 "8"、abortOnMaaStallWarning 给串形 "false" —— 钉 [int]／[bool]
        //     两枚强转在 PS 口径下怎么折(settings 里这两形都真实存在，不是臆造的夹具)；
        //   * paths.mumuDir／mumuAdb 给假路径 —— MuMuPaths 是纯路径算术(不起进程、不读实例配置)，
        //     adb 回落档那格要的只是"算术有没有走到"，不是本机装没装 MuMu(只读自检不许要求现场状态)。
        static Dictionary<string, object> CtxSettings(object cliArgs)
        {
            Dictionary<string, object> rt = In(
                "arkStartTimeoutMinutes", 10, "arkTimeoutMinutes", 45, "stallTimeoutMinutes", "8",
                "idleConfirmSeconds", 90, "abortOnMaaStallWarning", "false", "visionMaxRescue", 3,
                "benignMaaErrors", new object[] { "warn line", @"C:\a b.exe" },
                "m9aInstanceArg", "--instance", "m9aForceStart", true,
                "r1999StartTimeoutMinutes", 12, "r1999TimeoutMinutes", 60,
                "visionApiKey", KeySentinel);
            if (cliArgs != null) rt["m9aCliArgs"] = cliArgs;
            return In(
                "paths", In("maaDir", @"T:\maa", "maaExe", @"T:\maa\MAA.exe", "maaConfig", @"T:\maa\config.json",
                            "maaGuiLog", @"T:\maa\gui.log", "maaAsstLog", @"T:\maa\asst.log",
                            "arknightsExe", @"T:\ark\Arknights.exe",
                            "m9aDir", @"T:\m9a", "m9aExe", "m9a.exe", "m9aLogDir", @"T:\m9a\logs",
                            "mumuDir", @"T:\mumu", "mumuAdb", @"nx_main\adb.exe"),
                "runtime", rt,
                "re1999", In("package", "com.example.m9a"));
        }

        // CtxBody：壳给的那几枚旋钮(与 lib\rust-round.ps1 当年喂 Get-GdRoundCtx 的入参面同形)＋夹具 settings。
        // 生产侧 body 里**没有** settings 这枚(现读 config\settings.json)；gates 故意混三形：一枚空项、
        // 一枚大写键(真身手构的 [pscustomobject] 就是大写)、一枚小写键(过 JSON 之后的形)。
        static Dictionary<string, object> CtxBody(Dictionary<string, object> s)
        {
            return In("settings", s,
                "ids", new object[] { "ark", "r1999" },
                "gates", new object[] { null,
                    In("Id", "farm", "Verb", "Invoke-GdArkFarm"),
                    In("id", "shop", "verb", "Invoke-GdArkShopReset") },
                "opt", In("chip", 3, "note", "ctx-cell"),
                "force", false, "dryRun", false, "skipCleanup", false, "noElevate", true,
                "nameField", "Id", "noticeOwner", "owner", "tag", "selftest",
                "logPath", @"T:\logs\run-ctx.log", "ownerPid", 4242L);
        }

        // ==================== ②-b2(Chain.cs 链体直答件)的夹具助手 ====================
        // 三族，各自钉一件事：
        //   * Nat —— nativeRuns 的一项(**大写** Code/Out/Err)；Chain 递给 parse-mumu-state 的那三格才是
        //     小写 code/out/err，两本账不许拧(Gd.Get 对夹具是大小写敏感的，写错就是静默读到 null)。
        //   * Wf／WLog —— 临时树里落一份文件(日志按行给，读侧按 \r?\n 切，这里统一 \n)；空内容那一档
        //     要的是"路径就绪但没字节"，与"路径不存在"必须分成两格。
        //   * AskOne —— 启动器那一路的一枚"问模型"夹具，档位与 --livetest 的 LaunchKnobs 同形，
        //     但 scriptPath 必须指到**本棵临时树**里的 ask.py(LiveSelfTest.AskKnobs 钉死 livetest 那棵树，不借)。
        static Dictionary<string, object> Nat(int code, string outText, string errText)
        {
            return In("Code", code, "Out", outText, "Err", errText);
        }

        static string Wf(string dir, string leaf, string text)
        {
            try { Directory.CreateDirectory(dir); } catch { }
            string p = Path.Combine(dir, leaf);
            File.WriteAllText(p, text, new UTF8Encoding(false));
            return p;
        }

        static string WLog(string dir, string leaf, params string[] rows)
        {
            return Wf(dir, leaf, string.Join("\n", rows));
        }

        static Dictionary<string, object> AskOne(string askDir, string fakeOut)
        {
            return In("handle", 987654321L, "path", Path.Combine(askDir, "launcher.png"),
                      "shot", LiveSelfTest.FixShot(),
                      "hasKey", true, "noSpawn", true, "model", "m-fake", "timeoutSeconds", 45,
                      "pythonExe", @"C:\fake\python.exe", "scriptPath", Path.Combine(askDir, "ask.py"),
                      "askDir", askDir, "fakeCode", 0, "fakeOut", fakeOut, "fakeErr", "");
        }

        // MuMuManager `info -v` 的两档回话(真身那三格 JSON 键是小写蛇形，这是**对方**的形状，照抄)：
        const string MuMuOnJson = @"{""name"":""MuMu安卓设备"",""is_android_started"":true,""is_process_started"":true}";
        const string MuMuOffJson = @"{""name"":""MuMu安卓设备"",""is_android_started"":false,""is_process_started"":true}";

        // MAA 配置树与 appsettings 的最小形状：Gd.Get 对喂进来的夹具**大小写敏感**，键名逐字照两家自己的盘。
        static Dictionary<string, object> Queue(string name, string type, object isEnable, string stagePlan)
        {
            return In("Name", name, "TaskType", type, "IsEnable", isEnable,
                      "StagePlan", new object[] { stagePlan });
        }

        static Dictionary<string, object> MaaTree(params object[] queues)
        {
            Dictionary<string, object> def = In(
                "Gui", In("StartUpSettings", In("RunDirectly", true)),
                "TaskQueue", queues);
            return In("Configurations", In("Default", def));
        }

        // settings 夹具：Chain 那一路只按名取 runtime 的三枚开关(takeOverAssistant／quitAssistantAfter／
        // closeArknightsAfter)与 ark 的三格(finishAllSanityWith／keepQueuesEnabled／disableQueues、窗口标题式)。
        // 漏了这一格就会去读临时树上**故意不建**的 config\settings.json ⇒ 当场红，这就是"漏喂就红"的守门。
        // 四块一律给齐(ark/mumu/paths 给空表)：空表 ≠ 没这一格，Gd.Get 到空表里取键仍旧是 null。
        static Dictionary<string, object> St(params object[] runtimeKv)
        {
            return In("runtime", In(runtimeKv), "ark", In(), "mumu", In(), "paths", In());
        }

        static Dictionary<string, object> StArk(Dictionary<string, object> ark, params object[] runtimeKv)
        {
            return In("runtime", In(runtimeKv), "ark", ark, "mumu", In(), "paths", In());
        }

        // Get-MaaPaths 的五格：全指到临时树的 maaDir 下；日志两格由**顶层**夹具 guiLog/asstLog 再顶一次
        // (这两格正是 self-check 把"读日志尾"钉住的把手，真跑一侧不许送)。
        static Dictionary<string, object> MaaPaths(string dir)
        {
            return In("maaDir", dir, "maaExe", "maa.exe", "maaConfig", "config.json",
                      "maaGuiLog", "gui.log", "maaAsstLog", "asst.log");
        }

        // 材料表夹具最小的一枚(id → {name})：两张表的现算全在 Chain.BuildItemMaps，这里只给形状。
        static Dictionary<string, object> ItemIdx()
        {
            return In("30031", In("name", "先锋封装芯片"));
        }

        // 材料表夹具的多枚版(转接刀·运行面刀⑬ 补格 2026-11-06)：一枚夹具带三件事——
        //   ① 30031 与 39007 **同名** ⇒ 钉 BuildItemMaps 的"首现胜"(NameToId[先锋封装芯片] 只认 30031)；
        //   ② 或铝石 ⇒ 给"同一个块里第二个材料也入账"那格用；
        //   ③ AlloyPlate 大小写混排 ⇒ 给"查表不分大小写"那格用(日志里故意写全小写 alloyplate)。
        // ⚠ "首现胜"这一格吃的是 Dictionary 的枚举序：In() 只增不删，插入序＝枚举序(.NET 实现细节，
        //   但这份夹具就靠它)。真要换形制得先把 BuildItemMaps 改成吃有序输入，别在这里假装有两把尺。
        static Dictionary<string, object> ItemIdxMany()
        {
            return In("30031", In("name", "先锋封装芯片"), "39007", In("name", "先锋封装芯片"),
                      "30115", In("name", "或铝石"), "30116", In("name", "AlloyPlate"));
        }

        // ==================== R9-a 收编那四枚的取数助手 ====================
        // 这一族钉的是**磁盘形状**与**回执键名/键序**，所以读回来必须从字节读：
        //   * ReadRaw / StartsBom —— 实测 pwsh 7 的 `Set-Content/Add-Content -Encoding utf8` 写出来是**无 BOM**
        //     (5.1 才写 EF BB BF)，用带编码猜测的读法会把"BOM 有没有"读成"内容对不对"，那一格就恒绿了；
        //   * CrlfOnly —— 行尾只许 CRLF；**裸 LF 与裸 CR 分开算**(裸 CR 是 detail 折行那条规则的一部分，
        //     由 CsvRows 留在行内容里，不许混进行尾这条尺)；
        //   * CsvRows —— 按 \r\n 切，末尾那段空串是最后一行的行尾留下的，剥掉。
        static string ReadRaw(string path)
        {
            return Encoding.UTF8.GetString(File.ReadAllBytes(path));
        }

        static bool StartsBom(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            return b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
        }

        static bool CrlfOnly(string raw)
        {
            return !raw.Replace("\r\n", string.Empty).Contains("\n");
        }

        static string[] CsvRows(string path)
        {
            string raw = ReadRaw(path);
            // ⚠ 必须 **Ordinal**：`StartsWith("\uFEFF")` 默认按当前区域性比，而 U+FEFF 在 NLS 里是**可忽略字符**
            //   (零权重)⇒ 一枚**没有 BOM** 的文件也会被判"以 BOM 开头"、跟着 Substring(1) 把首字符吃掉
            //   (实测：表头 `"time…` 变成 `time…`，53 字符变 52，那一格恒假)。字节级那一尺交给 StartsBom。
            if (raw.StartsWith("\uFEFF", StringComparison.Ordinal)) raw = raw.Substring(1);
            List<string> rows = new List<string>(raw.Split(new string[] { "\r\n" }, StringSplitOptions.None));
            if (rows.Count > 0 && rows[rows.Count - 1].Length == 0) rows.RemoveAt(rows.Count - 1);
            return rows.ToArray();
        }

        // 盘上那份 MAA 配置：Fight 那一档留着"没被改过"的旧值(IsEnable=false、没有 DropId 那九格)，
        // 好让 apply 真写盘那一格有活干；"采集"故意开着，用来钉 ExpectedChains 的去重与顺序。
        const string MaaCfgText =
            "{\"Configurations\":{\"Default\":{\"Gui\":{\"StartUpSettings\":{\"RunDirectly\":true}},"
          + "\"TaskQueue\":["
          + "{\"Name\":\"战斗\",\"TaskType\":\"Fight\",\"IsEnable\":false,\"StagePlan\":[]},"
          + "{\"Name\":\"采集\",\"TaskType\":\"Collection\",\"IsEnable\":true,\"StagePlan\":[]}]}}}";

        // M9A 的 appsettings：**点号字面键**是 M9A 自己的形状(Gd.Get 不拆点号，正好当字面键取)。
        const string M9aTodoText = "{\"Instances.LastActive\":\"old-id\","
          + "\"Instances.LastActiveName\":\"旧实例\",\"NoAutoStart\":\"True\"}";
        const string M9aDoneText = "{\"Instances.LastActive\":\"new-id\","
          + "\"Instances.LastActiveName\":\"新实例\",\"NoAutoStart\":\"False\"}";

        public static int RoundTest()
        {
            Box b = new Box();
            Console.WriteLine("---- panel-cs --roundtest(B-3②-a 轮骨架现场七枚 + ②-b1 判定段/门表段数据面六枚 + ②-b2 链体丙档直答件十枚 + R9-a 收编那四枚的纯规则段：不弹 UAC/不打 webhook/不起气泡/不真杀进程/不读真任务/不打活动接口/不真起子进程/不发输入/不真关屏保) ----");
            int mine = Process.GetCurrentProcess().Id;
            string tmp = Path.Combine(Path.GetTempPath(), "gd-roundtest-" + mine);
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            try { Directory.CreateDirectory(tmp); } catch (Exception ex)
            {
                GdLog.Line("--roundtest 连临时树都建不起来(后面写盘那几格必红)：" + ex.Message, "ERROR");
            }
            // ②-b2 链体那一路的落点，全在这棵临时树里(收尾整目录删)：
            //   * chainDir —— 日志夹具与 MuMuManager 那一枚"只要存在就行"的空文件；
            //   * maaDir —— MAA 的目录形状(config.json 真身、gui/asst 日志由顶层夹具另指)；
            //   * ledgerDir 的三个子目录 —— 每格账本独占一份 stateDir，格与格之间不许互相看见；
            //   * arkExe —— 只喂给路径闸的那一枚空文件(名字固定，好和 windows 夹具的 Name 对齐)。
            string chainDir = Path.Combine(tmp, "chain");
            string maaDir = Path.Combine(tmp, "maa");
            string ledgerDir = Path.Combine(tmp, "ledger");
            string maaCfgFile = Path.Combine(maaDir, "config.json");
            string m9aFile = Path.Combine(chainDir, "appsettings.json");
            string mumuExe = Path.Combine(chainDir, "MuMuManager.exe");
            string arkExe = Path.Combine(tmp, "ark", "Arknights.exe");
            string askDir = Path.Combine(tmp, "ask");
            // 一份"paths 填齐了"的夹具，给 stop／apply／ledger 那几族共用：
            // maaExe 定进程名(maa)、arknightsExe 定端游名与 closeArknightsAfter 的那道闸、m9aExe 定 M9A 名。
            Dictionary<string, object> allPaths = MaaPaths(maaDir);
            allPaths["arknightsExe"] = arkExe;
            allPaths["m9aExe"] = "m9a.exe";
            // 临时树里只造"回落名册"那一格要的 lib\maa.ps1；**故意不造 config\settings.json**。
            try
            {
                Directory.CreateDirectory(Path.Combine(tmp, "lib"));
                File.WriteAllText(Path.Combine(tmp, "lib", "maa.ps1"), "#");
            }
            catch { }
            // ②-b2 的链体那一路再补三样，全落在这棵临时树上、随收尾整目录删：
            //   * ask\ask.py —— VisionAsk 的第二道前置只认 File.Exists(不执行它，noSpawn 那一档不起 Python)；
            //   * ark\Arknights.exe —— start-ark-client 的路径闸也只认 File.Exists，空文件足够，
            //     文件名固定这一枚是为了让 BaseName 和 windows 夹具里的 "Name" 对得上；
            //   * 启动器那一档的截图落点(ask\ 目录本身)—— WindowShot 被 shot 夹具顶掉，但 path 的
            //     父目录要能落，后面 ledger 那几格的 state\ 子目录同理。
            try
            {
                Directory.CreateDirectory(Path.Combine(tmp, "ask"));
                File.WriteAllText(Path.Combine(tmp, "ask", "ask.py"), "#");
                Directory.CreateDirectory(Path.Combine(tmp, "ark"));
                File.WriteAllText(Path.Combine(tmp, "ark", "Arknights.exe"), "");
                Directory.CreateDirectory(chainDir);
                Directory.CreateDirectory(maaDir);
                File.WriteAllText(mumuExe, "");
                File.WriteAllText(maaCfgFile, MaaCfgText, new UTF8Encoding(false));
                File.WriteAllText(m9aFile, M9aTodoText, new UTF8Encoding(false));
            }
            catch { }
            GdEnv.InitForTest(tmp);
            string state = GdEnv.State;
            string lockFile = Path.Combine(state, "running.json");
            string noticeFile = Path.Combine(state, "last-notice.json");
            // ②-b1 数据面那五本账的落点：全在临时树的 state\ 上，收尾随整目录一起删。
            string ackR1999 = Path.Combine(state, "r1999-event-ack.json");
            string shopArk = Path.Combine(state, "ark-shop-done.json");
            string shopR1999 = Path.Combine(state, "r1999-shop-done.json");
            string probeFile = Path.Combine(state, "ark-event-probe.json");
            int dead = DeadPid();

            // ==================== 1) FlagEnabled：PS 的 `-ne $false` 语义 ====================
            b.Chk("缺键/null 算开着(清单没写 flag 的旧写法合法)", delegate { return Round.FlagEnabled(null); });
            b.Chk("空串算开着(\"\" 与 \"False\" 不等)", delegate { return Round.FlagEnabled(""); });
            b.Chk("真布尔 $false 才算关", delegate { return !Round.FlagEnabled(false) && Round.FlagEnabled(true); });
            b.Chk("字符串 false/FALSE(不分大小写)算关", delegate
            {
                return !Round.FlagEnabled("false") && !Round.FlagEnabled("FALSE");
            });
            b.Chk("原样怪癖：字符串 \"0\" 算**开着**(左侧是串时 $false 折成 \"False\"，不比数值)", delegate
            {
                return Round.FlagEnabled("0");
            });
            b.Chk("数字 0 / 0.0 / 0D 算关(左侧是数时 $false 折成 0)", delegate
            {
                return !Round.FlagEnabled(0) && !Round.FlagEnabled(0.0) && !Round.FlagEnabled(0m);
            });
            b.Chk("数字 1/2 与字符串 true 都算开着", delegate
            {
                return Round.FlagEnabled(1) && Round.FlagEnabled(2) && Round.FlagEnabled("true");
            });
            b.Chk("认不出的形状(数组)按开着收(旧路不会因此拦下任何一轮)", delegate
            {
                return Round.FlagEnabled(new object[] { "x" });
            });

            // ==================== 2) ParseLock：运行锁读侧唯一真身 ====================
            b.Chk("null／没有 pid 的标记 = 压根没标记(PS 那侧回 $null 那一格)", delegate
            {
                return !Round.ParseLock(null).Found
                    && !Round.ParseLock("{}").Found
                    && !Round.ParseLock(@"{""log"":""x""}").Found;
            });
            b.Chk("pid/started/games/tag 四格读回", delegate
            {
                Round.Lock lk = Round.ParseLock(@"{""pid"":4321,""started"":""2026-10-13 06:00:00"",""games"":""ark + r1999"",""tag"":""auto""}");
                return lk.Found && lk.Pid == 4321 && lk.Started == "2026-10-13 06:00:00"
                       && lk.Games == "ark + r1999" && lk.Tag == "auto";
            });
            b.Chk("键名不分大小写(ConvertFrom-Json 之后旧 -match 仍算同一枚)", delegate
            {
                Round.Lock lk = Round.ParseLock(@"{""PID"":7,""STARTED"":""s"",""GAMES"":""ark"",""TAG"":""t"",""LOG"":""l""}");
                return lk.Found && lk.Pid == 7 && lk.Started == "s" && lk.Games == "ark" && lk.Tag == "t" && lk.Log == "l";
            });
            b.Chk("log 的反斜杠是**双重转义还原**(还原后要能直接 File.Exists，不是 JSON 解析)", delegate
            {
                Round.Lock lk = Round.ParseLock(@"{""pid"":1,""log"":""C:\\logs\\run-20261013.log""}");
                return lk.Log == @"C:\logs\run-20261013.log";
            });
            b.Chk("pid 溢出(int.Parse 抛)判成没标记而不是把读侧带崩", delegate
            {
                return !Round.ParseLock(@"{""pid"":99999999999999,""games"":""ark""}").Found;
            });

            // ==================== 3) LockAt：判活 + 两把尺 ====================
            b.Chk("路径为空/文件不在 → 没标记(不抛)", delegate
            {
                return !Round.LockAt("", true).Found
                    && !Round.LockAt(Path.Combine(tmp, "ghost.json"), true).Found
                    && !Round.LockAt(null, false).Found;
            });
            b.Chk("活 PID(自己)判在跑：两把尺同值", delegate
            {
                string p = Path.Combine(tmp, "alive.json");
                File.WriteAllText(p, "{\"pid\":" + mine + ",\"games\":\"ark\"}", new UTF8Encoding(false));
                Round.Lock psRuler = Round.LockAt(p, false);
                Round.Lock panelRuler = Round.LockAt(p, true);
                return psRuler.Found && psRuler.Alive && panelRuler.Found && panelRuler.Alive && psRuler.Pid == mine;
            });
            if (dead > 0)
            {
                b.Chk("已退出的 PID 判残留=**没在跑**(否则一次硬杀把面板永久挡在门外)；两把尺这一格同值", delegate
                {
                    string p = Path.Combine(tmp, "dead.json");
                    File.WriteAllText(p, "{\"pid\":" + dead + ",\"games\":\"ark\"}", new UTF8Encoding(false));
                    return Round.LockAt(p, false).Found && !Round.LockAt(p, false).Alive
                        && Round.LockAt(p, true).Found && !Round.LockAt(p, true).Alive;
                });
            }
            else Console.WriteLine("  SKIP 拿不到一个已退出的 PID，残留那一格没测到(与 --exttest 同一条)");
            b.Chk("标记位置是个目录(读不动) → 判没标记不抛", delegate
            {
                string dir = Path.Combine(tmp, "weird-dir.json");
                Directory.CreateDirectory(dir);
                return !Round.LockAt(dir, true).Found;
            });
            Console.WriteLine("  登记：两把尺的分岔只在\"读不到进程细节\"那一档(提权轮/完整性级别不同)，自检造不出那种进程 ⇒ unobserved");

            // ==================== 4) OwnsText："只删自己登记的那一份" ====================
            b.Chk("{\"PID\":123} 也算自己那份(-match 不分大小写，K2 实测)", delegate
            {
                return Round.OwnsText(@"{""PID"":123,""games"":""ark""}", 123);
            });
            b.Chk("pid 123 不许认领 {\"pid\":1234}(尾随 [\\s,}] 那一格)", delegate
            {
                return !Round.OwnsText(@"{""pid"":1234,""games"":""ark""}", 123);
            });
            b.Chk("pid 123 也不许认领 {\"pid\":12}", delegate { return !Round.OwnsText(@"{""pid"":12}", 123); });
            b.Chk("自己那份：逗号收尾与右括号收尾都算", delegate
            {
                return Round.OwnsText(@"{""pid"":123,""tag"":""""}", 123) && Round.OwnsText(@"{""pid"":123}", 123);
            });
            b.Chk("null／没有 pid 的文本 = 不是自己的", delegate
            {
                return !Round.OwnsText(null, 123) && !Round.OwnsText("{}", 123);
            });

            // ==================== 5) resolve-round-context：逐枚点名取键 ====================
            // 名册现扫：回落那一格靠的就是临时树里只有 lib\maa.ps1 这一路库。
            b.Chk("点名的 runtime 键才回，缺的那枚回 null 但**键在场**", delegate
            {
                object d = Round.Context(In(
                    "settings", In("runtime", In("pythonExe", @"C:\fake\python.exe", "visionApiKey", KeySentinel)),
                    "runtimeKeys", new object[] { "pythonExe", "notThere" },
                    "isAdmin", false, "now", 0L));
                Dictionary<string, object> rt = Row(Key(d, "Runtime"));
                return rt != null && rt.Count == 2
                    && S(rt["pythonExe"]) == @"C:\fake\python.exe"
                    && rt.ContainsKey("notThere") && rt["notThere"] == null;
            });
            b.Chk("整块 runtime 绝不回给核：哨兵 key 在回执里搜不到(序列化后自证)", delegate
            {
                object d = Round.Context(In(
                    "settings", In("runtime", In("visionApiKey", KeySentinel, "pythonExe", @"C:\fake\python.exe")),
                    "runtimeKeys", new object[] { "pythonExe" },
                    "isAdmin", false, "now", 0L));
                return !GdJson.Serialize(d).Contains(KeySentinel);
            });
            b.Chk("动态 flag 在这一侧算完：显式 false 那枚落成 false，没点的落 true", delegate
            {
                object d = Round.Context(In(
                    "settings", In("runtime", In("arkRequiresElevation", false, "r1999RequiresElevation", "0")),
                    "elevFlags", new object[] { "arkRequiresElevation", "r1999RequiresElevation", "notThereFlag" },
                    "isAdmin", true, "now", 0L));
                Dictionary<string, object> el = Row(Key(d, "Elevation"));
                return el != null && el.Count == 3
                    && B(el["arkRequiresElevation"]) == false
                    && B(el["r1999RequiresElevation"])          // 原样怪癖：字符串 "0" 算开着
                    && B(el["notThereFlag"]);
            });
            b.Chk("IsAdmin 用夹具，不现读这台机器", delegate
            {
                object d = Round.Context(In("isAdmin", false, "now", 0L, "paths", In()));
                return Key(d, "IsAdmin") is bool && !(bool)Key(d, "IsAdmin");
            });
            b.Chk("在位性三档：在位→Ready／空串→Missing／填了但不在→Absent 带\"键 → 路径\"", delegate
            {
                string real = Path.Combine(tmp, "lib", "maa.ps1");
                string ghost = Path.Combine(tmp, "no-such-dir");
                object d = Round.Context(In(
                    "paths", In("maaDir", real, "m9aDir", "", "mumuDir", ghost),
                    "pathKeys", new object[] { "maaDir", "m9aDir", "mumuDir" },
                    "isAdmin", false, "now", 0L));
                Dictionary<string, object> ready = Row(Key(d, "Ready"));
                Dictionary<string, object> paths = Row(Key(d, "Paths"));
                object[] missing = Gd.List(Key(d, "Missing"));
                object[] absent = Gd.List(Key(d, "Absent"));
                return ready != null && (bool)ready["maaDir"] && !(bool)ready["m9aDir"] && !(bool)ready["mumuDir"]
                    && paths != null && paths.ContainsKey("m9aDir") && paths["m9aDir"] == null   // 空串那枚回 null
                    && missing.Length == 1 && S(missing[0]) == "m9aDir"
                    && absent.Length == 1 && S(absent[0]) == "mumuDir → " + ghost;
            });
            b.Chk("RequiredPathKeys 原样回点名那几枚(不新建第三棵名册)", delegate
            {
                object d = Round.Context(In("pathKeys", new object[] { "maaDir", "m9aDir" }, "isAdmin", false, "now", 0L, "paths", In()));
                object[] keys = Gd.List(Key(d, "RequiredPathKeys"));
                return keys.Length == 2 && S(keys[0]) == "maaDir" && S(keys[1]) == "m9aDir";
            });
            b.Chk("没点名 pathKeys → 回落这棵树真装了哪几路(临时树只有 lib\\maa.ps1)", delegate
            {
                object d = Round.Context(In("paths", In("maaDir", Path.Combine(tmp, "lib", "maa.ps1"), "m9aDir", ""),
                                            "isAdmin", false, "now", 0L));
                object[] keys = Gd.List(Key(d, "RequiredPathKeys"));
                return keys.Length == 1 && S(keys[0]) == "maaDir";
            });
            b.Chk("now 直通：NowMs 是喂进去的那枚，NowText/Dow 与它自洽(不现读挂钟)", delegate
            {
                long ms = 1760000000000L;
                object d = Round.Context(In("isAdmin", false, "now", ms, "paths", In()));
                long back = Convert.ToInt64(Key(d, "NowMs"), CultureInfo.InvariantCulture);
                DateTime local = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
                return back == ms
                    && S(Key(d, "NowText")) == local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    && I(Key(d, "Dow")) == (int)local.DayOfWeek;
            });
            b.Chk("没喂 now 才现读挂钟：NowMs 落在现在±5 分钟，且与 NowText 同一时刻", delegate
            {
                object d = Round.Context(In("isAdmin", false, "paths", In()));
                long back = Convert.ToInt64(Key(d, "NowMs"), CultureInfo.InvariantCulture);
                long nowBack = Convert.ToInt64(
                    new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds(), CultureInfo.InvariantCulture);
                bool near = Math.Abs(nowBack - back) < 300000L;
                DateTime parsed;
                bool shape = DateTime.TryParseExact(S(Key(d, "NowText")), "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
                return near && shape && Math.Abs((parsed - DateTimeOffset.FromUnixTimeMilliseconds(back).LocalDateTime).TotalSeconds) < 2.0;
            });
            b.Chk("守门：漏喂 settings 又要点 paths → 去读临时树那份(不在)当场抛，不静默给空 ctx", delegate
            {
                return Thrown<FileNotFoundException>(delegate
                {
                    return Round.Context(In("pathKeys", new object[] { "maaDir" }, "isAdmin", false, "now", 0L));
                });
            });

            // ==================== 6) NoticeTitle / BatchLine(两枚内部纯件) ====================
            b.Chk("标题两句照 modules.ps1：全成\"…日常已完成\"、否则\"…日常需要人工看一眼\"", delegate
            {
                return Round.NoticeTitle("方舟", true) == "方舟日常已完成"
                    && Round.NoticeTitle("方舟", false) == "方舟日常需要人工看一眼";
            });
            b.Chk("owner 为 null 也只丢 owner，后半句还在(${NoticeOwner} 那笔怪癖只有一个来源)", delegate
            {
                return Round.NoticeTitle(null, true) == "日常已完成";
            });
            b.Chk("BatchLine：不带空格不加引号，参数用一个空格拼", delegate
            {
                return Round.BatchLine(@"C:\f\pwsh.exe", new List<string> { "-File", "x.ps1" }) == @"C:\f\pwsh.exe -File x.ps1";
            });
            b.Chk("BatchLine：带空格整串加引号(Program Files 那一路)；参数**不另加引号**(旧路就没加)", delegate
            {
                return Round.BatchLine(@"C:\Program Files\PowerShell\7\pwsh.exe", new List<string> { "-File", @"C:\a b.ps1" })
                    == "\"C:\\Program Files\\PowerShell\\7\\pwsh.exe\" -File C:\\a b.ps1";
            });
            b.Chk("原样怪癖：ArgList 为空时留一个尾随空格('{0} {1}' 不 trim)", delegate
            {
                return Round.BatchLine("pwsh.exe", new List<string>()) == "pwsh.exe ";
            });

            // ==================== 7) start-elevated-round(noSpawn 档：一支都不真起) ====================
            b.Chk("noSpawn＋给了本机 pwsh → 载体 pwsh、Code=fakeCode、Lines 空", delegate
            {
                object d = Round.Elevate(In("noSpawn", true, "localPwsh", @"C:\f\pwsh.exe",
                                            "batchPwsh", @"C:\f\pwsh.exe", "fakeCode", 7,
                                            "argList", new object[] { "-File", @"C:\r\daily-clear.ps1" }));
                return I(Key(d, "Code")) == 7 && S(Key(d, "Carrier")) == "pwsh"
                    && Len(Key(d, "Lines")) == 0
                    && S(Key(d, "BatchLine")) == @"C:\f\pwsh.exe -File C:\r\daily-clear.ps1";
            });
            b.Chk("本机 pwsh 不在场(商店版被跳过就是这一格) → 载体落 cmd", delegate
            {
                object d = Round.Elevate(In("noSpawn", true, "localPwsh", "", "batchPwsh", "pwsh.exe",
                                            "argList", new object[] { "-File", "x.ps1" }, "fakeCode", 0));
                return S(Key(d, "Carrier")) == "cmd" && S(Key(d, "BatchLine")) == "pwsh.exe -File x.ps1";
            });
            b.Chk("给了 argList 就用它：**空数组也算给**(此时不许再要 script)", delegate
            {
                object d = Round.Elevate(In("noSpawn", true, "localPwsh", @"C:\f\pwsh.exe",
                                            "batchPwsh", @"C:\f\pwsh.exe", "argList", new object[0]));
                return S(Key(d, "BatchLine")) == @"C:\f\pwsh.exe " && I(Key(d, "Code")) == 0;
            });
            b.Chk("没给 argList → 回退 build-relaunch-args 同装配拼参(-Force 那几枚排在 -File 之后)", delegate
            {
                object d = Round.Elevate(In("noSpawn", true, "localPwsh", @"C:\f\pwsh.exe",
                                            "batchPwsh", @"C:\f\pwsh.exe",
                                            "script", @"C:\r\daily-clear.ps1",
                                            "force", new object[] { "-NoElevate" },
                                            "entries", new object[] { In("K", "Ids", "T", "string[]", "V", "ark") }));
                return S(Key(d, "BatchLine"))
                    == @"C:\f\pwsh.exe -NoProfile -ExecutionPolicy Bypass -File ""C:\r\daily-clear.ps1"" -NoElevate -Ids ""ark""";
            });
            b.Chk("既没 argList 也没 script → 前置话术(退出码 2 那一档)，不猜空参数就提权", delegate
            {
                return Thrown<GdRuleException>(delegate { return Round.Elevate(In("noSpawn", true)); });
            });
            b.Chk("batchPwsh 的默认值不许落宿主自己的 exe：要么落 pwsh，要么照旧那句抛(两种现场都算过)", delegate
            {
                // 这一格钉的是 CLI 冒烟抓到的一处移植走样：真身的第一级 (Get-Process -Id $PID).Path 在 PS 宿主里
                // 取到的是"跑脚本的解释器"，照搬到 panel-cs.exe 里就变成面板自己的绝对路径，
                // 批处理行会拼成 "panel-cs.exe -File x.ps1" —— 面板不认 -File，提权起来是一发空打。
                // 不许要求机器现场：这台有 pwsh 就断言落点里没宿主 exe；一个都没有就断言抛的是原话那句。
                try
                {
                    object d = Round.Elevate(In("noSpawn", true, "argList", new object[] { "-File", "x.ps1" }));
                    string line = S(Key(d, "BatchLine"));
                    string selfName = "panel-cs.exe";
                    try { selfName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName); } catch { }
                    return line.IndexOf("panel-cs", StringComparison.OrdinalIgnoreCase) < 0
                        && line.IndexOf(selfName, StringComparison.OrdinalIgnoreCase) < 0
                        && line.IndexOf("pwsh", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch (GdRuleException ex)
                {
                    return ex.Message != null && ex.Message.Contains("找不到 pwsh.exe");
                }
            });

            // ==================== 8) send-round-notice(全打临时树；noSend 挡气泡挡 POST) ====================
            b.Chk("默认档：owner 缺=每日、allOk 缺=全成 ⇒ 标题/等级走 OK 那支", delegate
            {
                object d = Round.Notice(In("noSend", true, "nowText", "2026-10-13 20:00:00"));
                return S(Key(d, "Title")) == "每日日常已完成" && S(Key(d, "Level")) == "OK"
                    && S(Key(d, "Time")) == "2026-10-13 20:00:00"
                    && S(Key(d, "FileKey")) == @"state\last-notice.json" && B(Key(d, "File"));
            });
            b.Chk("allOk=false 的标题与等级(需要人工看一眼 / WARN)", delegate
            {
                object d = Round.Notice(In("noSend", true, "owner", "方舟", "allOk", false, "text", "t"));
                return S(Key(d, "Title")) == "方舟日常需要人工看一眼" && S(Key(d, "Level")) == "WARN";
            });
            b.Chk("落盘是无条件的第一件事：两枚失败夹具都喂上，last-notice.json 仍写得出来且读得回", delegate
            {
                if (File.Exists(noticeFile)) File.Delete(noticeFile);
                object d = Round.Notice(In("noSend", false, "owner", "方舟", "allOk", true,
                                           "text", "方舟：fight\n详情", "nowText", "2026-10-13 20:00:01",
                                           "settings", In("notify", In("webhook", HookSentinel)),
                                           "fakeWebhookErr", "boom-hook", "fakeToastErr", "boom-toast"));
                Dictionary<string, object> back = Row(GdJson.Read(noticeFile));
                return S(Key(d, "File")) == "True" && back != null
                    && S(back["title"]) == "方舟日常已完成" && S(back["level"]) == "OK"
                    && S(back["text"]) == "方舟：fight\n详情" && S(back["time"]) == "2026-10-13 20:00:01";
            });
            b.Chk("落盘形状：我们这侧是单行(旧路 ConvertTo-Json 缩进版，差已在文件头登记)", delegate
            {
                string raw = File.ReadAllText(noticeFile);
                return raw.IndexOf('\n') < 0 && raw.StartsWith("{");
            });
            b.Chk("两把 try 分开：webhook 与气泡同时失败 → 两句都在，各判 failed(不许合并)", delegate
            {
                object d = Round.Notice(In("noSend", false, "text", "t", "nowText", "2026-10-13 20:00:02",
                                           "settings", In("notify", In("webhook", HookSentinel, "toast", true)),
                                           "fakeWebhookErr", "boom-hook", "fakeToastErr", "boom-toast"));
                object[] lines = Gd.List(Key(d, "Lines"));
                return S(Key(d, "Webhook")) == "failed" && S(Key(d, "Toast")) == "failed"
                    && lines.Length == 2
                    && S(lines[0]) == "webhook 推送失败：boom-hook"
                    && S(lines[1]) == "桌面通知发送失败(不影响日常本身)：boom-toast";
            });
            b.Chk("noSend 只把\"要打\"变成 skipped，失败夹具在它面前不参与(自检绝不联网)", delegate
            {
                object d = Round.Notice(In("noSend", true, "text", "t",
                                           "settings", In("notify", In("webhook", HookSentinel, "toast", true)),
                                           "fakeWebhookErr", "boom-hook", "fakeToastErr", "boom-toast"));
                return S(Key(d, "Webhook")) == "skipped" && S(Key(d, "Toast")) == "skipped"
                    && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("没配 webhook = none(与 skipped 分得开：一个是没这回事，一个是有但不打)", delegate
            {
                object d = Round.Notice(In("noSend", false, "text", "t",
                                           "settings", In("notify", In("webhook", "", "toast", false))));
                return S(Key(d, "Webhook")) == "none" && S(Key(d, "Toast")) == "off";
            });
            b.Chk("toast 明确关掉 = off，noSend 也越不过它(真身 -not $settings -or toast 那一格)", delegate
            {
                object d = Round.Notice(In("noSend", true, "text", "t",
                                           "settings", In("notify", In("webhook", HookSentinel, "toast", false))));
                return S(Key(d, "Toast")) == "off" && S(Key(d, "Webhook")) == "skipped";
            });
            b.Chk("settings 读不到(临时树里没有那份)时按真身默认走：toast 视为开 ⇒ noSend 下 skipped", delegate
            {
                object d = Round.Notice(In("noSend", true, "text", "t"));
                return S(Key(d, "Toast")) == "skipped" && S(Key(d, "Webhook")) == "none";
            });
            b.Chk("安全边界：回执里搜不到 webhook URL(只有异常原话进 Lines)", delegate
            {
                object d = Round.Notice(In("noSend", false, "text", "t",
                                           "settings", In("notify", In("webhook", HookSentinel, "toast", true)),
                                           "fakeWebhookErr", "boom-hook", "fakeToastErr", "boom-toast"));
                return !GdJson.Serialize(d).Contains(HookSentinel);
            });

            // ==================== 9) start-run-lock / stop-run-lock(同一枚 running.json) ====================
            b.Chk("start-run-lock 必须显式给 pid：一次性 verb 把自己的 PID 登记成活锁=面板撞车保护失真", delegate
            {
                return Thrown<GdRuleException>(delegate { return Round.LockStart(In("games", "ark")); });
            });
            b.Chk("干净起跑：Wrote、键值原样、默认 games=all／log=当前日志(自检不落盘故空串)", delegate
            {
                if (File.Exists(lockFile)) File.Delete(lockFile);
                object d = Round.LockStart(In("pid", mine, "started", "2026-10-13 06:00:00", "games", "ark + r1999",
                                              "tag", "auto", "log", @"C:\logs\x.log"));
                Round.Lock lk = Round.LockAt(lockFile, false);
                return B(Key(d, "Wrote")) && !B(Key(d, "PrevAlive")) && Len(Key(d, "Lines")) == 0
                    && lk.Found && lk.Pid == mine && lk.Started == "2026-10-13 06:00:00"
                    && lk.Games == "ark + r1999" && lk.Tag == "auto" && lk.Log == @"C:\logs\x.log";
            });
            b.Chk("落盘单行、UTF-8 无 BOM(面板与 PS 都按原文正则读这一枚)", delegate
            {
                byte[] raw = File.ReadAllBytes(lockFile);
                string text = File.ReadAllText(lockFile);
                bool noBom = !(raw.Length > 2 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF);
                return noBom && text.IndexOf('\n') < 0 && text.StartsWith("{") && !Round.OwnsText(text, 999999);
            });
            b.Chk("已有一轮活着：**只说不拦**——那句 WARN 原话逐字、且照样登记自己(旧路不硬拦的理由是无人值守)", delegate
            {
                File.WriteAllText(lockFile, "{\"pid\":" + mine + ",\"started\":\"2026-10-13 05:00:00\",\"games\":\"ark\",\"tag\":\"auto\",\"log\":\"\"}",
                                  new UTF8Encoding(false));
                int other = dead > 0 ? dead : 999999;
                object d = Round.LockStart(In("pid", other, "started", "2026-10-13 06:00:00", "games", "all"));
                object[] lines = Gd.List(Key(d, "Lines"));
                bool wrote = B(Key(d, "Wrote")) && B(Key(d, "PrevAlive"));
                return wrote && lines.Length == 1
                    && S(lines[0]) == "已有一轮在跑(PID " + mine + "，2026-10-13 05:00:00 起，games=ark) —— 本轮照跑，两轮会抢同一个助手/端游"
                    && Round.LockAt(lockFile, false).Pid == other;      // 照跑=照覆盖
            });
            b.Chk("上一轮是残留(已退出)：不吭声、登记自己", delegate
            {
                if (dead > 0)
                {
                    File.WriteAllText(lockFile, "{\"pid\":" + dead + ",\"started\":\"2026-09-25 19:11:35\",\"games\":\"ark\",\"tag\":\"\",\"log\":\"\"}",
                                      new UTF8Encoding(false));
                    object d = Round.LockStart(In("pid", mine, "started", "2026-10-13 06:00:00", "games", "all"));
                    return B(Key(d, "Wrote")) && !B(Key(d, "PrevAlive")) && Len(Key(d, "Lines")) == 0;
                }
                return true;
            });
            b.Chk("stop-run-lock 也必须显式给 pid(撤锁只撤自己那一份)", delegate
            {
                return Thrown<GdRuleException>(delegate { return Round.LockStop(In()); });
            });
            b.Chk("撤自己那份：删得掉；键名大小写不同也算自己那份", delegate
            {
                File.WriteAllText(lockFile, "{\"PID\":" + mine + ",\"games\":\"ark\"}", new UTF8Encoding(false));
                object d = Round.LockStop(In("pid", mine));
                return B(Key(d, "Had")) && B(Key(d, "Removed")) && !File.Exists(lockFile);
            });
            b.Chk("别的轮已经覆盖了它 → Had 但**不删**(那等于把别人的撞车保护拆了)", delegate
            {
                File.WriteAllText(lockFile, "{\"pid\":" + mine + ",\"games\":\"ark\"}", new UTF8Encoding(false));
                object d = Round.LockStop(In("pid", 4321));
                return B(Key(d, "Had")) && !B(Key(d, "Removed")) && File.Exists(lockFile);
            });
            b.Chk("没有标记：Had=false/Removed=false，整枚 catch{} 不抛(收尾这一路绝不能被撤锁带崩)", delegate
            {
                try { if (File.Exists(lockFile)) File.Delete(lockFile); } catch { }
                object d = Round.LockStop(In("pid", mine));
                return !B(Key(d, "Had")) && !B(Key(d, "Removed"));
            });

            // ==================== 10) read-task-state(fake 档：一次 COM 都不碰) ====================
            b.Chk("read-task-state 必须给任务名(任务名属现场量)", delegate
            {
                return Thrown<GdRuleException>(delegate { return Round.TaskState(In("fake", In("state", "Ready"))); });
            });
            b.Chk("fake 档五格直通：Found/State/Last/Result/Next", delegate
            {
                object d = Round.TaskState(In("name", "GameDaily-Clear", "fake",
                    In("state", "Ready", "last", "2026-10-13 06:00:01", "result", 0, "next", "2026-10-14 06:00:00")));
                return B(Key(d, "Found")) && S(Key(d, "State")) == "Ready"
                    && S(Key(d, "Last")) == "2026-10-13 06:00:01" && I(Key(d, "Result")) == 0
                    && S(Key(d, "Next")) == "2026-10-14 06:00:00";
            });
            b.Chk("任务不在场 = Found:false(旧路回 $null 那一格)", delegate
            {
                object d = Round.TaskState(In("name", "Ghost-Task", "fake", In("found", false)));
                return !B(Key(d, "Found")) && S(Key(d, "State")) == "";
            });
            b.Chk("Result 读不出= null 而不是 0(旧路 try{[int]}catch{$null}：0 是\"成功\"的意思，别混)", delegate
            {
                object d = Round.TaskState(In("name", "T", "fake", In("state", "Unknown", "result", null)));
                return Key(d, "Result") == null;
            });
            b.Chk("TaskStateName 五档与 PS 的 TaskState 同一套序号", delegate
            {
                return Round.TaskStateName(0) == "Unknown" && Round.TaskStateName(1) == "Disabled"
                    && Round.TaskStateName(2) == "Queued" && Round.TaskStateName(3) == "Ready"
                    && Round.TaskStateName(4) == "Running";
            });
            b.Chk("越界的数原样回数字(PS 对枚举越界也回数字，这里不编新词)", delegate
            {
                return Round.TaskStateName(6) == "6" && Round.TaskStateName(-1) == "-1";
            });

            // ==================== 11) stop-process-tree(noKill 档：一枚进程都不碰) ====================
            b.Chk("stop-process-tree 必须给进程名(按进程名匹配，不给就是空打)", delegate
            {
                return Thrown<GdRuleException>(delegate { return Round.ProcTree(In("noKill", true)); });
            });
            b.Chk("noKill＋喂名册：Matched=Killed=匹配数(原样怪癖：返回的是\"匹配到的进程数\")", delegate
            {
                object d = Round.ProcTree(In("name", "pwsh", "noKill", true, "fakeProcs", new object[] { 1, 2, 3 }));
                return I(Key(d, "Matched")) == 3 && I(Key(d, "Killed")) == 3 && B(Key(d, "Skipped")) && !B(Key(d, "Force"));
            });
            b.Chk("Force 只是透传的那枚旗标，noKill 之下不改变计数", delegate
            {
                object d = Round.ProcTree(In("name", "pwsh", "noKill", true, "force", true,
                                             "fakeProcs", new object[] { 7 }));
                return I(Key(d, "Matched")) == 1 && I(Key(d, "Killed")) == 1 && B(Key(d, "Force"));
            });
            b.Chk("名册为空 = 0 枚(与真身\"$procs.Count -eq 0 → return 0\"同格)", delegate
            {
                object d = Round.ProcTree(In("name", "ghost-proc", "noKill", true, "fakeProcs", new object[0]));
                return I(Key(d, "Matched")) == 0 && I(Key(d, "Killed")) == 0;
            });

            // ==================== 12) CLI 接线：那七枚真的挂上了分发链(②-a 批) ====================
            b.Chk("resolve-round-context 走 --in/--out：退出码 0、信封 verb 对、data 里有 Runtime", delegate
            {
                string inPath = Path.Combine(tmp, "ctx-in.json");
                string outPath = Path.Combine(tmp, "ctx-out.json");
                GdJson.Write(inPath, In("settings", In("runtime", In("pythonExe", @"C:\fake\python.exe")),
                                        "runtimeKeys", new object[] { "pythonExe" },
                                        "isAdmin", false, "now", 0));
                int code = GdCli.Run("resolve-round-context", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                Dictionary<string, object> data = Row(Gd.Get(env, "data"));
                return code == 0 && (bool)Gd.Get(env, "ok") && S(Gd.Get(env, "verb")) == "resolve-round-context"
                    && data != null && data.ContainsKey("Runtime") && data.ContainsKey("Elevation")
                    && data.ContainsKey("RequiredPathKeys") && data.ContainsKey("NowText");
            });
            b.Chk("start-run-lock 缺 pid 走 CLI：退出码 2(前置/规则那一档)＋原话进 error", delegate
            {
                string inPath = Path.Combine(tmp, "lock-in.json");
                string outPath = Path.Combine(tmp, "lock-out.json");
                GdJson.Write(inPath, In("games", "ark"));
                int code = GdCli.Run("start-run-lock", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                return code == 2 && !Gd.Truthy(Gd.Get(env, "ok"))
                    && S(Gd.Get(env, "error")).Contains("必须显式给 pid");
            });
            b.Chk("send-round-notice 走 CLI：那份 last-notice.json 落在**临时树**的 state\\ 上(真跑才会落进真 state)", delegate
            {
                if (File.Exists(noticeFile)) File.Delete(noticeFile);
                string inPath = Path.Combine(tmp, "notice-in.json");
                string outPath = Path.Combine(tmp, "notice-out.json");
                GdJson.Write(inPath, In("owner", "方舟", "allOk", true, "text", "t", "noSend", true));
                int code = GdCli.Run("send-round-notice", new string[] { "--in=" + inPath, "--out=" + outPath });
                if (code != 0 || !File.Exists(noticeFile)) return false;
                return Path.GetFullPath(noticeFile).StartsWith(Path.GetFullPath(tmp), StringComparison.OrdinalIgnoreCase)
                    && S(Gd.Get(Row(GdJson.Read(noticeFile)), "title")) == "方舟日常已完成";
            });

            // ==================== 13) read-ark-activity：三档顺序 + 那两句 WARN(网络与读盘全走夹具) ====================
            b.Chk("接口夹具命中：Data=夹具、Source='MAA 活动接口'、CacheAgeHours 留空、一句 WARN 都不吭", delegate
            {
                object d = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityApiUrl", "http://fake.invalid/act")),
                    "apiUrl", "http://fake.invalid/act",
                    "apiJson", In("events", new object[] { "CE-5" })));
                return S(Key(d, "Source")) == "MAA 活动接口" && B(Key(d, "ApiUrlGiven")) && !B(Key(d, "Offline"))
                    && !B(Key(d, "CacheStale")) && Key(d, "CacheAgeHours") == null && Len(Key(d, "Lines")) == 0
                    && Key(d, "Data") != null;
            });
            b.Chk("offline 档：apiUrl 在场也不打接口，直接落本地缓存(source 里带'几小时前')", delegate
            {
                object d = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityApiUrl", "http://fake.invalid/act", "activityCacheMaxHours", 24)),
                    "offline", true, "cacheJson", In("a", 1), "cacheAgeHours", 3.0));
                return B(Key(d, "Offline")) && S(Key(d, "Source")) == "MAA 本地缓存(3 小时前)"
                    && Dbl(Key(d, "CacheAgeHours")) == 3.0 && !B(Key(d, "CacheStale")) && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("原样怪癖：缓存小时数 Round(...,1) 与 PS 的 [math]::Round 同用**银行家舍入**(1.25→1.2，不是 1.3)", delegate
            {
                object d = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityCacheMaxHours", 24)),
                    "cacheJson", In("a", 1), "cacheAgeHours", 1.25));
                return S(Key(d, "Source")) == "MAA 本地缓存(1.2 小时前)" && Dbl(Key(d, "CacheAgeHours")) == 1.2;
            });
            b.Chk("apiErr 夹具：接口那句 WARN 原话逐字，然后照真身落到缓存档", delegate
            {
                object d = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityApiUrl", "http://fake.invalid/act", "activityCacheMaxHours", 24)),
                    "apiErr", "模拟断流", "cacheJson", In("a", 1), "cacheAgeHours", 2.0));
                return Ln(Key(d, "Lines"), 0) == "活动接口拉取失败(模拟断流)，改用本地缓存" && Len(Key(d, "Lines")) == 1
                    && S(Key(d, "Source")) == "MAA 本地缓存(2 小时前)";
            });
            b.Chk("接口与缓存两头落空 → '既拉不到…按长草期处理' 那句、Source='无数据'、Data 为 null", delegate
            {
                object d = RoundData.ArkActivity(In("settings", In("ark", In())));
                return S(Key(d, "Source")) == "无数据" && Key(d, "Data") == null && !B(Key(d, "ApiUrlGiven"))
                    && Len(Key(d, "Lines")) == 1 && Ln(Key(d, "Lines"), 0) == "既拉不到活动接口、也没有本地缓存 → 按长草期处理";
            });
            b.Chk("原样怪癖：activityCacheMaxHours 缺键＝0 ⇒ 缓存永远算过期，且那句 WARN 里插的是**原值**(空)，不是 0", delegate
            {
                object d = RoundData.ArkActivity(In(
                    "settings", In("ark", In()),                    // 整棵树没配这一项(真身 [int]$null = 0)
                    "cacheJson", In("a", 1), "cacheAgeHours", 1.25));
                return B(Key(d, "CacheStale")) && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "活动缓存超过  小时未刷新，结论可能滞后(MAA 启动后会自行更新)";
            });
            b.Chk("配了 24 且没过 → 不吭；配了 1 且过了 → 那句里带上'1'", delegate
            {
                object ok = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityCacheMaxHours", 24)), "cacheJson", In("a", 1), "cacheAgeHours", 5.0));
                object bad = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityCacheMaxHours", 1)), "cacheJson", In("a", 1), "cacheAgeHours", 5.0));
                return !B(Key(ok, "CacheStale")) && Len(Key(ok, "Lines")) == 0
                    && B(Key(bad, "CacheStale"))
                    && Ln(Key(bad, "Lines"), 0) == "活动缓存超过 1 小时未刷新，结论可能滞后(MAA 启动后会自行更新)";
            });
            b.Chk("原样怪癖：'拉到东西了没有'按 PS 的 -not $data 判 —— 空对象算**有**、空数组算**没有**", delegate
            {
                object emptyObj = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityApiUrl", "http://fake.invalid/act")),
                    "apiUrl", "http://fake.invalid/act", "apiJson", In(), "offline", false));
                object emptyArr = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityApiUrl", "http://fake.invalid/act")),
                    "apiUrl", "http://fake.invalid/act", "apiJson", new object[0]));
                return S(Key(emptyObj, "Source")) == "MAA 活动接口" && S(Key(emptyArr, "Source")) == "无数据";
            });
            b.Chk("真读盘那半段：缓存文件落在**临时树**上，按 mtime 现算小时数(刚写的 ⇒ 0 小时前、没过 24 的线)", delegate
            {
                string cache = Path.Combine(tmp, "act-cache.json");
                GdJson.Write(cache, In("events", new object[] { "CE-5" }));
                object d = RoundData.ArkActivity(In(
                    "settings", In("ark", In("activityCacheMaxHours", 24)),
                    "paths", In("maaDir", tmp, "maaActivityCache", "act-cache.json")));
                return S(Key(d, "Source")) == "MAA 本地缓存(0 小时前)" && Key(d, "Data") != null
                    && !B(Key(d, "CacheStale")) && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("守门：漏喂 settings 又没喂 paths → 去读临时树那份(不在)当场抛，不静默当'没活动'", delegate
            {
                return Thrown<FileNotFoundException>(delegate { return RoundData.ArkActivity(In("offline", true)); });
            });

            // ==================== 14) read-r1999-activity：读盘 + DataAge(天) ====================
            b.Chk("path 给了却不存在：Ready=false、Why='缺少 <路径>'、Reason 是那句'…，按长草处理'", delegate
            {
                string miss = Path.Combine(tmp, "没有这个文件.json");
                object d = RoundData.R1999Activity(In("path", miss));
                return !B(Key(d, "Ready")) && Key(d, "Data") == null && Key(d, "DataAgeDays") == null
                    && S(Key(d, "Why")) == "缺少 " + miss
                    && S(Key(d, "Reason")) == "缺少 " + miss + "，按长草处理";
            });
            b.Chk("m9aDir 没填 → 路径是空串，Why 换成那句人话(真身注释明写'缺少 ，按长草处理'玩家看不懂)", delegate
            {
                object d = RoundData.R1999Activity(In("settings", In("paths", In("m9aDir", "", "m9aActivityData", @"activity\cn.json"))));
                return !B(Key(d, "Ready")) && S(Key(d, "Path")) == ""
                    && S(Key(d, "Why")) == "M9A 目录还没填(先跑 setup.ps1)"
                    && S(Key(d, "Reason")) == "M9A 目录还没填(先跑 setup.ps1)，按长草处理";
            });
            b.Chk("真读盘：临时树上那份回读得到、DataAge 现算(mtime 刚写 ⇒ 0 天)且不吭'缺少'", delegate
            {
                string p = Path.Combine(tmp, "m9a-cn.json");
                GdJson.Write(p, In("version", 12, "events", new object[] { "ev1" }));
                object d = RoundData.R1999Activity(In("path", p));
                return B(Key(d, "Ready")) && S(Key(d, "Why")) == "" && Key(d, "Reason") == null
                    && Dbl(Key(d, "DataAgeDays")) < 0.01
                    && Gd.Map(Key(d, "Data")) != null && I(Gd.Get(Key(d, "Data"), "version")) == 12;
            });
            b.Chk("paths 拼接那半段：m9aDir+m9aActivityData 现拼(给了 path 就直答，不读 settings)", delegate
            {
                object joined = RoundData.R1999Activity(In(
                    "settings", In("paths", In("m9aDir", tmp, "m9aActivityData", "ghost.json"))));
                return S(Key(joined, "Path")) == Path.Combine(tmp, "ghost.json");
            });
            b.Chk("dataAgeDays 夹具原样带上回(那句'(活动数据 N 天前)'的**追加**动作属于判定，本 verb 不做——见头注双源第④处)", delegate
            {
                string p = Path.Combine(tmp, "m9a-cn.json");
                object d = RoundData.R1999Activity(In("path", p, "dataJson", In("version", 1), "dataAgeDays", 1.25));
                return B(Key(d, "Ready")) && Dbl(Key(d, "DataAgeDays")) == 1.2
                    && S(Key(d, "Reason")) == "(null)";
            });

            // ==================== 15) read-decision-state：五本 state 读 + 那把 flag 尺的读数 ====================
            b.Chk("空树三本：Ack/ShopDone 是空表(不是 null)、Probe 没探过=null、门默认开着", delegate
            {
                object d = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In())));
                // 门表回的是**字典**不是数组：Len() 走 Gd.List，它对字典回"一个元素"(那条防摊开的守卫)，
                // 所以比格数必须用 Cnt(对齐 PS 的 $map.Count)，否则空表会被数成一。
                return S(Key(d, "Game")) == "ark" && Cnt(Key(d, "Ack")) == 0 && Cnt(Key(d, "ShopDone")) == 0
                    && Key(d, "Probe") == null && B(Key(d, "ShopDoneGateOpen")) && Len(Key(d, "Want")) == 3;
            });
            b.Chk("want 逐枚点名：只要 ack 时 ShopDone/Probe 都不去碰(省两次读盘，也不替调用方多取)", delegate
            {
                object d = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In()), "want", new object[] { "ack" }));
                return Key(d, "Ack") != null && Key(d, "ShopDone") == null && Key(d, "Probe") == null
                    && Len(Key(d, "Want")) == 1;
            });
            b.Chk("盘上那本 shop-done 表回读得到，且键**不分大小写**(PS 的 @{} 就是这把尺，区分大小写会留孤儿格)", delegate
            {
                GdJson.Write(shopArk, In("done", In("CE-5", "2026-10-13 06:05")));
                object d = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In())));
                Dictionary<string, object> map = Row(Key(d, "ShopDone"));
                return map != null && map.ContainsKey("ce-5") && map.Count == 1 && S(map["CE-5"]) == "2026-10-13 06:05";
            });
            b.Chk("自证账本五键原样回读(Date/EventKey/Stage/Entered/Evidence)——判定层 Decisions.cs 就吃这五键", delegate
            {
                GdJson.Write(probeFile, In("Date", "10-13 06:12", "EventKey", "CE-5", "Stage", "CE-5/4",
                                           "Entered", true, "Evidence", "作战完成"));
                object d = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In()), "want", new object[] { "probe" }));
                Dictionary<string, object> pb = Row(Key(d, "Probe"));
                return pb != null && S(pb["Date"]) == "10-13 06:12" && S(pb["EventKey"]) == "CE-5"
                    && S(pb["Stage"]) == "CE-5/4" && B(pb["Entered"]) && S(pb["Evidence"]) == "作战完成";
            });
            b.Chk("EventKey 是空串 = '这一轮还没探'→ null(不能把空账本喂成一条结论)", delegate
            {
                GdJson.Write(probeFile, In("Date", "10-13 06:12", "EventKey", "", "Stage", "CE-5/4", "Entered", false));
                object d = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In()), "want", new object[] { "probe" }));
                return Key(d, "Probe") == null;
            });
            b.Chk("账本读坏了 → 照样 null(真身 Get-GdArkEventProbe 那一层有 try：坏账本不该把整轮带走)", delegate
            {
                File.WriteAllText(probeFile, "{这不是合法的 JSON", new UTF8Encoding(false));
                object d = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In()), "want", new object[] { "probe" }));
                return Key(d, "Probe") == null;
            });
            File.Delete(probeFile);
            b.Chk("r1999 读的是 r1999 那本盘：ack 表分游戏各走各的文件", delegate
            {
                GdJson.Write(ackR1999, In("acked", In("mainline-3", "2026-10-12 07:00")));
                object r = RoundData.DecisionState(In("game", "r1999", "settings", In("re1999", In()),
                                                      "want", new object[] { "ack" }));
                object a = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In()),
                                                      "want", new object[] { "ack" }));
                return S(Key(r, "Game")) == "r1999" && Cnt(Key(r, "Ack")) == 1 && Cnt(Key(a, "Ack")) == 0;
            });
            b.Chk("块名不许按 id 拼：id 是 r1999、settings 块叫 re1999(Get-GdSettingsBlock 的回落分支)", delegate
            {
                object open = RoundData.DecisionState(In("game", "r1999", "settings", In("re1999", In("shopDoneStopsEvent", false)),
                                                         "want", new object[] { "ack" }));
                object named = RoundData.DecisionState(In("game", "r1999", "settings", In("re1999", In("shopDoneStopsEvent", true)),
                                                           "settingsBlock", "re1999", "want", new object[] { "ack" }));
                return !B(Key(open, "ShopDoneGateOpen")) && B(Key(named, "ShopDoneGateOpen"));
            });
            b.Chk("那把 flag 尺复用 C# 侧唯一真身(Round.FlagEnabled)：字符串 \"0\" 算开着、显式 false 才算关", delegate
            {
                object zero = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In("shopDoneStopsEvent", "0")),
                                                         "want", new object[] { "ack" }));
                object off = RoundData.DecisionState(In("game", "ark", "settings", In("ark", In("shopDoneStopsEvent", false)),
                                                        "want", new object[] { "ack" }));
                return B(Key(zero, "ShopDoneGateOpen")) && !B(Key(off, "ShopDoneGateOpen"));
            });
            b.Chk("认不得的游戏 id 当场挡(它会被拼进状态文件名)，原话逐字对 Assert-GdGameId", delegate
            {
                return Thrown<GdRuleException>(delegate
                {
                    return RoundData.DecisionState(In("game", "__no_such__", "settings", In("ark", In())));
                }) && S(ThrownMsg(delegate
                {
                    return RoundData.DecisionState(In("game", "__no_such__", "settings", In("ark", In())));
                })) == "不认识的游戏 id：'__no_such__'(这棵树上认得：ark / r1999)";
            });
            b.Chk("名册可注入：将来模块清单多一款游戏由调用方点名 gameIds，不在这里硬编码第三路", delegate
            {
                object d = RoundData.DecisionState(In("game", "third", "gameIds", new object[] { "ark", "r1999", "third" },
                                                      "settings", In("third", In()), "want", new object[] { "ack" }));
                return S(Key(d, "Game")) == "third" && Cnt(Key(d, "Ack")) == 0;
            });

            // ==================== 15b) compose-decision-inputs：判定面进核的组值半段(1ev.①) ====================
            b.Chk("六键齐摆：Dow 落在 1..7(周日=7 的归一区间)、Now 是毫秒、Ack/ShopDone 是空表(不是 null)、Probe 没探过=null(先把 r1999 那对盘账清空再读)", delegate
            {
                string ackF = Path.Combine(state, "r1999-event-ack.json");
                string shopF = Path.Combine(state, "r1999-shop-done.json");
                if (File.Exists(ackF)) File.Delete(ackF);
                if (File.Exists(shopF)) File.Delete(shopF);
                object d = RoundData.ComposeDecisionInputs(In("game", "r1999", "settings", In("re1999", In())));
                return S(Key(d, "Game")) == "r1999" && I(Key(d, "Dow")) >= 1 && I(Key(d, "Dow")) <= 7
                    && Dbl(Key(d, "Now")) > 0 && Cnt(Key(d, "Ack")) == 0 && Cnt(Key(d, "ShopDone")) == 0
                    && Key(d, "Block") != null && Cnt(Key(d, "Paths")) >= 0 && Key(d, "Probe") == null;
            });
            b.Chk("领空折在这里**折好再交**：shopDoneStopsEvent=false ⇒ ShopDone 给空表(盘上有账也不交)；缺省开 ⇒ 交盘上读数且键不分大小写——judge 臂不用再懂这把尺", delegate
            {
                GdJson.Write(shopR1999, In("done", In("CE-5", "2026-10-13 06:05")));
                object off = RoundData.ComposeDecisionInputs(In("game", "r1999",
                    "settings", In("re1999", In("shopDoneStopsEvent", false))));
                object on = RoundData.ComposeDecisionInputs(In("game", "r1999", "settings", In("re1999", In())));
                Dictionary<string, object> map = Row(Key(on, "ShopDone"));
                return Cnt(Key(off, "ShopDone")) == 0 && Cnt(Key(on, "ShopDone")) == 1
                    && map != null && map.ContainsKey("ce-5") && S(map["CE-5"]) == "2026-10-13 06:05";
            });
            b.Chk("game 归一成写法名(ARK→ark)与认不得的当场挡——和 read-decision-state 共用 AssertGame 一处真身", delegate
            {
                object d = RoundData.ComposeDecisionInputs(In("game", "ARK", "settings", In("ark", In())));
                return S(Key(d, "Game")) == "ark" && Thrown<GdRuleException>(delegate
                {
                    return RoundData.ComposeDecisionInputs(In("game", "__no_such__", "settings", In("ark", In())));
                });
            });

            // ==================== 16) set-ark-shop-state：四分支话术 + 真落盘(只落临时树) ====================
            b.Chk("没有进行中的活动(空 key)：只 WARN 那一句、Action=none、**盘上一个字都不写**", delegate
            {
                if (File.Exists(shopArk)) File.Delete(shopArk);
                object d = RoundData.ArkShopState(In("eventKey", "", "reason", "活动接口和缓存都拉不到"));
                return S(Key(d, "Action")) == "none" && !B(Key(d, "Written")) && I(Key(d, "Count")) == 0
                    && Ln(Key(d, "Lines"), 0) == "方舟当前没有进行中的活动可标记：活动接口和缓存都拉不到"
                    && !File.Exists(shopArk);
            });
            b.Chk("标记：Action=mark、时间戳用喂进来的那个、盘上 done 表里就是这一格、回执 Count=1", delegate
            {
                if (File.Exists(shopArk)) File.Delete(shopArk);
                object d = RoundData.ArkShopState(In("eventKey", "CE-5", "eventTip", "限时活动", "done", true,
                                                     "nowText", "2026-10-13 07:00", "idlePlan", "CE-5",
                                                     "settings", In("ark", In())));
                if (S(Key(d, "Action")) != "mark" || !B(Key(d, "Written")) || I(Key(d, "Count")) != 1) return false;
                if (Ln(Key(d, "Lines"), 0) != "已标记方舟活动 CE-5[限时活动] 商店领空 → 之后剩余理智只刷 CE-5") return false;
                Dictionary<string, object> obj = Row(GdJson.Read(shopArk));
                Dictionary<string, object> done = Row(Gd.Get(obj, "done"));
                return done != null && S(done["CE-5"]) == "2026-10-13 07:00";
            });
            b.Chk("原样怪癖：回落那句人话里的 dow 用**原始 0-6**(周日读 planByWeekday.\"0\")，不是判定层那把'周日=7'", delegate
            {
                object sun = RoundData.ArkShopState(In("eventKey", "K", "eventTip", "t", "done", true, "nowText", "T",
                                                       "dow", 0, "settings", In("ark", In("planByWeekday", In("1", "CE-5"),
                                                                                          "defaultStage", "1-7"))));
                object mon = RoundData.ArkShopState(In("eventKey", "K", "eventTip", "t", "done", true, "nowText", "T",
                                                       "dow", 1, "settings", In("ark", In("planByWeekday", In("1", "CE-5"),
                                                                                          "defaultStage", "1-7"))));
                return Ln(Key(sun, "Lines"), 0).EndsWith("只刷 1-7") && Ln(Key(mon, "Lines"), 0).EndsWith("只刷 CE-5");
            });
            b.Chk("整表写回不丢别的格：连标两枚 ⇒ 盘上两格都在、Count=2(真身是取表→盖一格→整表写回)", delegate
            {
                // 从空表起：Count 是**整表**格数，前一格(CE-5/K)留下的账会把它抬上去，那不是在验"写回丢不丢"。
                if (File.Exists(shopArk)) File.Delete(shopArk);
                RoundData.ArkShopState(In("eventKey", "K1", "eventTip", "t", "done", true, "nowText", "07:00",
                                          "idlePlan", "P", "settings", In("ark", In())));
                object d = RoundData.ArkShopState(In("eventKey", "K2", "eventTip", "t", "done", true, "nowText", "07:05",
                                                     "idlePlan", "P", "settings", In("ark", In())));
                Dictionary<string, object> done = Row(Gd.Get(Row(GdJson.Read(shopArk)), "done"));
                return I(Key(d, "Count")) == 2 && done != null && done.Count == 2
                    && S(done["K1"]) == "07:00" && S(done["K2"]) == "07:05";
            });
            b.Chk("撤销：Action=reset、话术'…恢复自动刷活动代币'、盘上那一格真没了(别的格留下)", delegate
            {
                object d = RoundData.ArkShopState(In("eventKey", "K1", "done", false, "settings", In("ark", In())));
                Dictionary<string, object> done = Row(Gd.Get(Row(GdJson.Read(shopArk)), "done"));
                return S(Key(d, "Action")) == "reset" && B(Key(d, "Written")) && I(Key(d, "Count")) == 1
                    && Ln(Key(d, "Lines"), 0) == "已撤销 K1 的领空标记 → 恢复自动刷活动代币"
                    && done != null && !done.ContainsKey("K1") && done.ContainsKey("K2");
            });
            b.Chk("撤销落空(本来就没有)：Action=miss、Written=false、只 WARN 一句、**不建文件**(真身那句 return 在 Remove 之前)", delegate
            {
                File.Delete(shopArk);
                object d = RoundData.ArkShopState(In("eventKey", "K9", "done", false, "settings", In("ark", In())));
                return S(Key(d, "Action")) == "miss" && !B(Key(d, "Written")) && I(Key(d, "Count")) == 0
                    && Ln(Key(d, "Lines"), 0) == "K9 本来就没有领空标记" && !File.Exists(shopArk);
            });

            // ==================== 17) set-r1999-ack：只有'标记'一档(这道门没有撤销口) ====================
            b.Chk("没有待确认活动：只 WARN 那句、不写盘", delegate
            {
                if (File.Exists(ackR1999)) File.Delete(ackR1999);
                object d = RoundData.R1999Ack(In("eventKey", "", "reason", "这一版没有主线活动"));
                return S(Key(d, "Action")) == "none" && !B(Key(d, "Written"))
                    && Ln(Key(d, "Lines"), 0) == "1999 当前没有待确认活动：这一版没有主线活动" && !File.Exists(ackR1999);
            });
            b.Chk("原样怪癖：确认话术里那个箭头前**没有空格**(真身原文如此，ark 那两句却有空格，不许统一)", delegate
            {
                if (File.Exists(ackR1999)) File.Delete(ackR1999);
                object d = RoundData.R1999Ack(In("eventKey", "mainline-3", "eventTip", "第三章", "nowText", "2026-10-13 07:10"));
                if (Ln(Key(d, "Lines"), 0) != "已确认 1999 活动 mainline-3[第三章]→ 日常将使用活动方案") return false;
                Dictionary<string, object> acked = Row(Gd.Get(Row(GdJson.Read(ackR1999)), "acked"));
                return acked != null && S(acked["mainline-3"]) == "2026-10-13 07:10";
            });
            b.Chk("重复确认同一格：Count 不增(整表写回是**覆盖**不是追加)", delegate
            {
                object d = RoundData.R1999Ack(In("eventKey", "mainline-3", "eventTip", "第三章", "nowText", "07:20"));
                Dictionary<string, object> acked = Row(Gd.Get(Row(GdJson.Read(ackR1999)), "acked"));
                return I(Key(d, "Count")) == 1 && acked.Count == 1 && S(acked["mainline-3"]) == "07:20";
            });

            // ==================== 18) set-r1999-shop-state：与 ark 那枚同构，差两句人话 + 回落值是实例名 ====================
            b.Chk("标记：回落值从 re1999.planToInstance.idle **现读**(不是关卡名)，落的是 r1999 那本盘", delegate
            {
                if (File.Exists(shopR1999)) File.Delete(shopR1999);
                object d = RoundData.R1999ShopState(In("eventKey", "ev1", "eventTip", "活动一", "done", true,
                                                       "nowText", "07:30",
                                                       "settings", In("re1999", In("planToInstance", In("idle", "长草实例")))));
                if (Ln(Key(d, "Lines"), 0) != "已标记 1999 活动 ev1[活动一] 商店领空 → 之后跑[长草实例]实例") return false;
                Dictionary<string, object> done = Row(Gd.Get(Row(GdJson.Read(shopR1999)), "done"));
                return done != null && S(done["ev1"]) == "07:30" && !File.Exists(shopArk);
            });
            b.Chk("idleInstance 直答优先于现读(转接期壳里已经算好了就别再读一遍 settings)", delegate
            {
                object d = RoundData.R1999ShopState(In("eventKey", "ev2", "eventTip", "t", "done", true,
                                                       "nowText", "07:31", "idleInstance", "指定实例"));
                return Ln(Key(d, "Lines"), 0).EndsWith("跑[指定实例]实例");
            });
            b.Chk("撤销那两句与 ark 同构但**结尾那句不一样**('恢复活动方案' vs '恢复自动刷活动代币')", delegate
            {
                object r = RoundData.R1999ShopState(In("eventKey", "ev2", "done", false));
                object m = RoundData.R1999ShopState(In("eventKey", "ev9", "done", false));
                return S(Key(r, "Action")) == "reset" && Ln(Key(r, "Lines"), 0) == "已撤销 ev2 的领空标记 → 恢复活动方案"
                    && S(Key(m, "Action")) == "miss" && Ln(Key(m, "Lines"), 0) == "ev9 本来就没有领空标记"
                    && !File.Exists(shopArk);
            });
            // 刀㉓ 笔(a)(2026-11-08)：上面那格只钉了两句话术与两枚 Action，**盘上那一半**没钉。
            // test-r1999 [t7] 的「撤销标记生效」「重复撤销判 miss」两枚行为钉吃的正是盘上那一半(摘完再判一次会回到 event)，
            // 摘它们之前先把这半搬到本节(纪律＝摘而不搬不许)。ev1 由 18) 首格写入、ev2 由上一格摘掉，故此处 done 应恰好空表。
            b.Chk("撤销真从盘上摘掉＋重复撤销真不写盘(盘上口径，不是只看回执 Action)", delegate
            {
                object r = RoundData.R1999ShopState(In("eventKey", "ev1", "done", false));
                object m = RoundData.R1999ShopState(In("eventKey", "ev9", "done", false));
                Dictionary<string, object> done = Row(Gd.Get(Row(GdJson.Read(shopR1999)), "done"));
                return S(Key(r, "Action")) == "reset" && S(Key(m, "Action")) == "miss"
                    && done != null && done.Count == 0 && File.Exists(shopR1999);
            });

            // ==================== 19) CLI 接线：这六枚真的挂上了分发链(名册两处同批的自证) ====================
            b.Chk("read-decision-state 走 --in/--out：退出码 0、信封 verb 对、data 里五键齐", delegate
            {
                string inPath = Path.Combine(tmp, "ds-in.json");
                string outPath = Path.Combine(tmp, "ds-out.json");
                GdJson.Write(inPath, In("game", "ark", "settings", In("ark", In())));
                int code = GdCli.Run("read-decision-state", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                Dictionary<string, object> data = Row(Gd.Get(env, "data"));
                return code == 0 && Gd.Truthy(Gd.Get(env, "ok")) && S(Gd.Get(env, "verb")) == "read-decision-state"
                    && data != null && data.ContainsKey("Ack") && data.ContainsKey("ShopDone")
                    && data.ContainsKey("ShopDoneGateOpen") && data.ContainsKey("Probe") && data.ContainsKey("Game");
            });
            b.Chk("set-ark-shop-state 走 CLI：那份表落在**临时树**的 state\\ 上(真跑才会落进真 state)", delegate
            {
                if (File.Exists(shopArk)) File.Delete(shopArk);
                string inPath = Path.Combine(tmp, "shop-in.json");
                string outPath = Path.Combine(tmp, "shop-out.json");
                GdJson.Write(inPath, In("eventKey", "CLI-1", "eventTip", "t", "done", true,
                                         "nowText", "08:00", "idlePlan", "P"));
                int code = GdCli.Run("set-ark-shop-state", new string[] { "--in=" + inPath, "--out=" + outPath });
                if (code != 0 || !File.Exists(shopArk)) return false;
                return Path.GetFullPath(shopArk).StartsWith(Path.GetFullPath(tmp), StringComparison.OrdinalIgnoreCase)
                    && S(Gd.Get(Row(Gd.Get(Row(GdJson.Read(shopArk)), "done")), "CLI-1")) == "08:00";
            });
            b.Chk("read-ark-activity 走 CLI 且漏喂 settings：退出码 2(前置那一档)＋原话进 error，不静默回'无数据'", delegate
            {
                string inPath = Path.Combine(tmp, "act-in.json");
                string outPath = Path.Combine(tmp, "act-out.json");
                GdJson.Write(inPath, In("offline", true));
                int code = GdCli.Run("read-ark-activity", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                return code == 2 && !Gd.Truthy(Gd.Get(env, "ok"))
                    && S(Gd.Get(env, "error")).Contains("还没有 config\\settings.json");
            });
            b.Chk("认不得的游戏 id 走 CLI：退出码 2＋规则原话(GdRuleException 与前置同档)", delegate
            {
                string inPath = Path.Combine(tmp, "bad-in.json");
                string outPath = Path.Combine(tmp, "bad-out.json");
                GdJson.Write(inPath, In("game", "__no_such__", "settings", In("ark", In())));
                int code = GdCli.Run("read-decision-state", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                return code == 2 && S(Gd.Get(env, "error")).Contains("不认识的游戏 id：'__no_such__'");
            });
            b.Chk("名册第二处(usage 长串)随行自证：未知子命令那句报错里必须同时数到本批六枚", delegate
            {
                string outPath = Path.Combine(tmp, "usage-out.json");
                int code = GdCli.Run("no-such-verb", new string[] { "--out=" + outPath });
                string err = S(Gd.Get(GdJson.ReadEnvelope(outPath), "error"));
                return code == 1 && err.Contains("read-ark-activity") && err.Contains("read-r1999-activity")
                    && err.Contains("read-decision-state") && err.Contains("set-ark-shop-state")
                    && err.Contains("set-r1999-ack") && err.Contains("set-r1999-shop-state");
            });

            // ==================== 20) takeover-processes：接管关闭那把闸(真值判，缺键=关) ====================
            b.Chk("没开接管(takeOverAssistant 缺键)且助手在跑：当场抛原话，一枚都不接管", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.Takeover(In("names", new object[] { "maa" }, "label", "MAA",
                        "running", new object[] { "maa" }, "sleepSeconds", 0, "settings", St()));
                });
                return S(msg) == "MAA(maa) 正在运行；要么手动关掉再跑，要么把 settings.runtime.takeOverAssistant 设为 true";
            });
            b.Chk("开了接管：一句 WARN、Stopped 只记真在跑那枚(空名与不在跑都 continue)、label 缺省「助手」、回执四键", delegate
            {
                object d = Chain.Takeover(In("names", new object[] { "", "maa", "ark" },
                    "running", new object[] { "maa" }, "sleepSeconds", 0,
                    "settings", St("takeOverAssistant", true), "noKill", true,
                    "fakeProcs", new object[] { 11 }));
                return B(Key(d, "Done")) && S(Key(d, "Label")) == "助手" && Cnt(d) == 4
                    && Len(Key(d, "Stopped")) == 1 && S(Gd.List(Key(d, "Stopped"))[0]) == "maa"
                    && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "助手 正在运行 → 先关掉再改配置(避免它退出时把配置写回旧值)";
            });
            b.Chk("原样怪癖第 1 笔：明写 false 与缺键**同档**(都当场拒绝)，与 Round.FlagEnabled 那把 -ne $false 反着走", delegate
            {
                object off = ThrownMsg(delegate
                {
                    return Chain.Takeover(In("names", new object[] { "m9a" }, "label", "M9A",
                        "running", new object[] { "m9a" }, "sleepSeconds", 0,
                        "settings", St("takeOverAssistant", false)));
                });
                object miss = ThrownMsg(delegate
                {
                    return Chain.Takeover(In("names", new object[] { "m9a" }, "label", "M9A",
                        "running", new object[] { "m9a" }, "sleepSeconds", 0, "settings", St()));
                });
                return S(off) == "M9A(m9a) 正在运行；要么手动关掉再跑，要么把 settings.runtime.takeOverAssistant 设为 true"
                    && S(off) == S(miss);
            });
            b.Chk("running 给了空名册：一枚都不算在跑、零句、Stopped 空(自检不许现查真进程)", delegate
            {
                object d = Chain.Takeover(In("names", new object[] { "maa", "ark" }, "running", new object[0],
                    "sleepSeconds", 0, "settings", St("takeOverAssistant", true)));
                return Len(Key(d, "Stopped")) == 0 && Len(Key(d, "Lines")) == 0 && B(Key(d, "Done"));
            });

            // ==================== 21) stop-maa-and-game：两档分开的开关，只有真关掉才吭那一句 ====================
            b.Chk("两枚都开 ⇒ 两句各带进程数(MaaClosed 与 ArkClosed 同值＝同一份 fakeProcs，原样怪癖第 2 笔)", delegate
            {
                object d = Chain.StopMaaAndGame(In("settings", St("quitAssistantAfter", true, "closeArknightsAfter", true),
                    "paths", allPaths, "noKill", true, "fakeProcs", new object[] { 1, 2 }));
                return I(Key(d, "MaaClosed")) == 2 && I(Key(d, "ArkClosed")) == 2 && Cnt(d) == 4
                    && Len(Key(d, "Lines")) == 2
                    && Ln(Key(d, "Lines"), 0) == "已关闭 MAA(2 个进程)"
                    && Ln(Key(d, "Lines"), 1) == "已关闭明日方舟端游(2 个进程)";
            });
            b.Chk("只开 quitAssistantAfter ⇒ 端游那档一枚不数、一句不吭", delegate
            {
                object d = Chain.StopMaaAndGame(In("settings", St("quitAssistantAfter", true),
                    "paths", allPaths, "noKill", true, "fakeProcs", new object[] { 5, 6, 7 }));
                return I(Key(d, "MaaClosed")) == 3 && I(Key(d, "ArkClosed")) == 0
                    && Len(Key(d, "Lines")) == 1 && Ln(Key(d, "Lines"), 0) == "已关闭 MAA(3 个进程)";
            });
            b.Chk("两枚都缺键(真值判＝关)：0/0、零句(与 Round.FlagEnabled 那族缺键算开正好相反)", delegate
            {
                object d = Chain.StopMaaAndGame(In("settings", St(), "paths", allPaths,
                    "noKill", true, "fakeProcs", new object[] { 1, 2 }));
                return I(Key(d, "MaaClosed")) == 0 && I(Key(d, "ArkClosed")) == 0 && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("原样怪癖第 6 笔：closeArknightsAfter 开着但 paths.arknightsExe 没填 ⇒ 空名不算在跑、不抛、只有 MAA 那一句", delegate
            {
                object d = Chain.StopMaaAndGame(In("settings", St("quitAssistantAfter", true, "closeArknightsAfter", true),
                    "paths", In("maaDir", maaDir, "maaExe", "maa.exe"), "noKill", true,
                    "fakeProcs", new object[] { 1 }));
                return I(Key(d, "MaaClosed")) == 1 && I(Key(d, "ArkClosed")) == 0
                    && Len(Key(d, "Lines")) == 1 && Ln(Key(d, "Lines"), 0) == "已关闭 MAA(1 个进程)";
            });

            // ==================== 22) stop-m9a：只有一档，话术是 M9A 不是 MAA(两侧不许统一) ====================
            b.Chk("quitAssistantAfter 开 ⇒ 「已关闭 M9A(1 个进程)」，M9aClosed 一格", delegate
            {
                object d = Chain.StopM9A(In("settings", St("quitAssistantAfter", true), "paths", allPaths,
                    "noKill", true, "fakeProcs", new object[] { 3 }));
                return I(Key(d, "M9aClosed")) == 1 && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "已关闭 M9A(1 个进程)" && Cnt(d) == 3;
            });
            b.Chk("缺键 ⇒ 0、零句(m9a 那一路只有这一枚开关，没有端游档)", delegate
            {
                object d = Chain.StopM9A(In("settings", St(), "paths", allPaths,
                    "noKill", true, "fakeProcs", new object[] { 3 }));
                return I(Key(d, "M9aClosed")) == 0 && Len(Key(d, "Lines")) == 0;
            });

            // ==================== 23) read-maa-fight-skip：读日志尾那半，判据一枚不在这里重写 ====================
            b.Chk("gui.log 不在 ⇒ Found=false＋Reason=gui-log-missing(答手据此发 Null，与\"读到但没认栽\"是两回事)", delegate
            {
                object d = Chain.ReadFightSkip(In("paths", allPaths, "guiLog", Path.Combine(chainDir, "ghost-gui.log")));
                return !B(Key(d, "Found")) && S(Key(d, "Reason")) == "gui-log-missing" && Cnt(d) == 2;
            });
            b.Chk("gui.log 在但没字节 ⇒ Reason=gui-log-empty(路径就绪与读得出内容是两档，别糊成一格)", delegate
            {
                string p = Wf(chainDir, "gui-empty.log", "");
                object d = Chain.ReadFightSkip(In("paths", allPaths, "guiLog", p));
                return !B(Key(d, "Found")) && S(Key(d, "Reason")) == "gui-log-empty";
            });
            b.Chk("正常档：会话内两句各计一次，Plan 取最后一条 from 的引号串，Evidence 逐字清洗过前缀", delegate
            {
                string p = WLog(chainDir, "gui-skip-1.log",
                    "[INF]<1> MaaAssistantArknights GUI started",
                    "[INF]<2> GetFightStage: from [\"CE-5\",\"CE-6\"], selected CE-5",
                    "[INF]<3> 关卡计划内没有当日开放的关卡",
                    "[INF]<4> 剩余理智作战 任务跳过");
                object d = Chain.ReadFightSkip(In("paths", allPaths, "guiLog", p));
                object[] plan = Gd.List(Key(d, "Plan"));
                object[] evi = Gd.List(Key(d, "Evidence"));
                return B(Key(d, "Found")) && B(Key(d, "Skipped")) && I(Key(d, "TaskSkips")) == 1
                    && plan.Length == 2 && S(plan[0]) == "CE-5" && evi.Length == 2
                    && S(evi[0]) == "关卡计划内没有当日开放的关卡" && S(evi[1]) == "剩余理智作战 任务跳过";
            });
            b.Chk("那两句在最后一次 GUI started **之前** ⇒ 一概不算(会话自切是判据的支点，不是装饰)", delegate
            {
                string p = WLog(chainDir, "gui-skip-2.log",
                    "[INF]<1> 关卡计划内没有当日开放的关卡",
                    "[INF]<2> 剩余理智作战 任务跳过",
                    "[INF]<3> MaaAssistantArknights GUI started");
                object d = Chain.ReadFightSkip(In("paths", allPaths, "guiLog", p));
                return B(Key(d, "Found")) && !B(Key(d, "Skipped")) && I(Key(d, "TaskSkips")) == 0
                    && Len(Key(d, "Evidence")) == 0 && Len(Key(d, "Plan")) == 0;
            });
            b.Chk("第二档(加载期修剪)：from [] 空计划 ＋ 任务跳过 ⇒ 也算认栽，Plan 仍原样是空表", delegate
            {
                string p = WLog(chainDir, "gui-skip-3.log",
                    "[INF]<1> MaaAssistantArknights GUI started",
                    "[INF]<2> GetFightStage: from [], selected ",
                    "[INF]<3> 理智作战 任务跳过");
                object d = Chain.ReadFightSkip(In("paths", allPaths, "guiLog", p));
                return B(Key(d, "Skipped")) && Len(Key(d, "Plan")) == 0 && I(Key(d, "TaskSkips")) == 1
                    && Ln(Key(d, "Evidence"), 0) == "理智作战 任务跳过";
            });

            // ==================== 24) read-maa-stage-observation：只抄账，不改任何配置 ====================
            b.Chk("两本日志都没读到 ⇒ LogsRead=false＋Reason=no-logs(与\"读到了但没递 Fight\"分开)", delegate
            {
                object d = Chain.ReadStageObservation(In("paths", allPaths,
                    "guiLog", Path.Combine(chainDir, "ghost-gui.log"),
                    "asstLog", Path.Combine(chainDir, "ghost-asst.log")));
                return !B(Key(d, "Found")) && !B(Key(d, "LogsRead")) && S(Key(d, "Reason")) == "no-logs" && Cnt(d) == 3;
            });
            b.Chk("成功档七键：asst 尾窗给 stage 与重放行，gui 的 from/selected 原样抄，日期由夹具注入", delegate
            {
                string asst = WLog(chainDir, "asst-obs-1.log",
                    @"append_task Fight {""stage"":""CE-5""}",
                    @"[INF]<9> first:[""LastOrCurBattleBegin""] ""taskchain"":""Fight""");
                string gui = WLog(chainDir, "gui-obs-1.log",
                    "[INF]<2> GetFightStage: from [\"CE-5\",\"CE-6\"], selected 6");
                object d = Chain.ReadStageObservation(In("paths", allPaths, "asstLog", asst, "guiLog", gui,
                    "nowText", "2026-10-14"));
                return B(Key(d, "Found")) && B(Key(d, "LogsRead")) && S(Key(d, "Date")) == "2026-10-14"
                    && S(Key(d, "Stage")) == "CE-5" && B(Key(d, "LastBattle")) && B(Key(d, "SawGuiPlan"))
                    && Len(Key(d, "Survivors")) == 2 && S(Key(d, "Selected")) == "6" && Cnt(d) == 8;
            });
            b.Chk("剪空形状原样抄：from [\"\"] 的空串占位不算存活关名，selected 后面没东西就是空串(不粉饰成上一关)", delegate
            {
                string asst = WLog(chainDir, "asst-obs-2.log", @"append_task Fight {""stage"":""""}");
                string gui = WLog(chainDir, "gui-obs-2.log", "[INF]<2> GetFightStage: from [\"\"], selected ");
                object d = Chain.ReadStageObservation(In("paths", allPaths, "asstLog", asst, "guiLog", gui,
                    "nowText", "2026-10-14"));
                return B(Key(d, "Found")) && S(Key(d, "Stage")).Length == 0
                    && !B(Key(d, "LastBattle")) && Len(Key(d, "Survivors")) == 0
                    && S(Key(d, "Selected")).Length == 0 && B(Key(d, "SawGuiPlan"));
            });

            // ==================== 25) start-ark-client：路径闸／窗口名册／dry-run／等窗口超时 ====================
            b.Chk("路径闸在前：exe 不在就当场抛原话(头尾两句逐字，中间那格是收到什么就打什么)", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.StartArkClient(In("arknightsExe", Path.Combine(chainDir, "ghost.exe"),
                        "settings", St(), "paths", allPaths));
                });
                string txt = S(msg);
                return txt.StartsWith("端游路径不对：") && txt.EndsWith("(改 settings.paths.arknightsExe)");
            });
            b.Chk("dryRun **绕不过**路径闸(真身那句 throw 就排在 $DryRun 分支之前)：CLI 那一路实测 rc=2，这一格把实测转成门", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.StartArkClient(In("arknightsExe", Path.Combine(chainDir, "ghost.exe"),
                        "dryRun", true, "settings", St(), "paths", allPaths, "windows", new object[0]));
                });
                return S(msg).StartsWith("端游路径不对：") && S(msg).EndsWith("(改 settings.paths.arknightsExe)");
            });
            b.Chk("窗口名册命中 ⇒ 恰一句「端游窗口已存在：[Name#Id] Title」并早退(不 sleep、不问模型、不起进程)", delegate
            {
                object d = Chain.StartArkClient(In("arknightsExe", arkExe, "settings", St(), "paths", allPaths,
                    "windows", new object[] { In("Name", "Arknights", "Id", 4321L, "Title", "明日方舟") }));
                Dictionary<string, object> w = Row(Key(d, "Window"));
                return B(Key(d, "Started")) && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "端游窗口已存在：[Arknights#4321] 明日方舟"
                    && w != null && S(w["Name"]) == "Arknights" && I(w["Id"]) == 4321 && S(w["Title"]) == "明日方舟";
            });
            b.Chk("不在跑且没窗口 ＋ dryRun ⇒ 两句(启动端游／[dry-run] 跳过实际启动)、Started=false、Window=null(真起那一句记 unobserved)", delegate
            {
                object d = Chain.StartArkClient(In("arknightsExe", arkExe, "dryRun", true,
                    "settings", St(), "paths", allPaths, "windows", new object[0], "running", new object[0]));
                return !B(Key(d, "Started")) && Key(d, "Window") == null && Len(Key(d, "Lines")) == 2
                    && Ln(Key(d, "Lines"), 0) == "启动端游：" + arkExe
                    && Ln(Key(d, "Lines"), 1) == "[dry-run] 跳过实际启动";
            });
            b.Chk("进程在跑但窗口没出 ⇒ 三句(WARN／等待超时／等了 N 秒…)，原样怪癖第 10 笔那轮多睡不在话术里", delegate
            {
                object d = Chain.StartArkClient(In("arknightsExe", arkExe, "settings", St(), "paths", allPaths,
                    "windows", new object[0], "running", new object[] { "Arknights" },
                    "windowTimeoutSeconds", 1, "pollSeconds", 1));
                return !B(Key(d, "Started")) && Len(Key(d, "Lines")) == 3
                    && Ln(Key(d, "Lines"), 0) == "端游进程在跑但还没有窗口(Arknights)，等它出登录界面"
                    && Ln(Key(d, "Lines"), 1) == "等待超时(1s)：端游窗口"
                    && Ln(Key(d, "Lines"), 2) == "等了 1 秒还没看到端游窗口(要登录/在更新/路径不对?)";
            });
            b.Chk("标题中但进程名不中 ⇒ 仍算没窗口(名册先按名过滤)；名的大小写不分、标题走 -match(不分大小写)", delegate
            {
                object a = Chain.StartArkClient(In("arknightsExe", arkExe, "dryRun", true, "settings", St(),
                    "paths", allPaths, "windows", new object[] { In("Name", "other", "Id", 1L, "Title", "明日方舟") },
                    "running", new object[0]));
                object b2 = Chain.StartArkClient(In("arknightsExe", arkExe, "settings", St(), "paths", allPaths,
                    "windows", new object[] { In("Name", "ARKNIGHTS", "Id", 9L, "Title", "Arknights") }));
                return Len(Key(a, "Lines")) == 2 && !B(Key(a, "Started"))
                    && B(Key(b2, "Started")) && Ln(Key(b2, "Lines"), 0) == "端游窗口已存在：[ARKNIGHTS#9] Arknights";
            });

            // ==================== 26) LauncherReady(直调)：相位五档里的四档 ＋ 开关／dry-run／超时 ====================
            // 经 start-ark-client 走不到这里(名册是静态夹具，命中就早退、不命中就撞超时)，故 Chain 把可见性放到 internal。
            b.Chk("arkAutoUpdateClick 明写关掉 ⇒ 直接放行、零句(顶层夹具那一枚；缺顶层时回落 settings.runtime 同一档)", delegate
            {
                List<object> off = new List<object>();
                bool r1 = Chain.LauncherReady(In("arkAutoUpdateClick", false), off);
                List<object> off2 = new List<object>();
                bool r2 = Chain.LauncherReady(In("settings", St("arkAutoUpdateClick", false), "dryRun", true), off2);
                return r1 && r2 && off.Count == 0 && off2.Count == 0;
            });
            b.Chk("dry-run ⇒ 那一句「[dry-run] 跳过启动器更新页检查」并放行(问模型一次都不问)", delegate
            {
                List<object> lines = new List<object>();
                bool got = Chain.LauncherReady(In("settings", St(), "dryRun", true), lines);
                return got && lines.Count == 1 && S(lines[0]) == "[dry-run] 跳过启动器更新页检查";
            });
            b.Chk("相位序列 waiting→clear：两句逐字、返 true(launcherAsks 按**问模型次数**取，clicks 自己那把尺没动)", delegate
            {
                List<object> lines = new List<object>();
                bool got = Chain.LauncherReady(In("settings", St(), "launcherAsks", new object[] {
                        AskOne(askDir, LiveSelfTest.LWaitJson), AskOne(askDir, LiveSelfTest.LClearJson) },
                        "waitSeconds", 60, "sleepSeconds", 0), lines);
                return got && lines.Count == 2
                    && S(lines[0]) == "启动器检查：这一页还不能进游戏(模型说：正在下载资源 47%)，没有该点的地方，继续等"
                    && S(lines[1]) == "启动器检查：已经不在更新页了(模型说：已经在游戏主界面)，可以开始了";
            });
            b.Chk("模型回话是空文本 ⇒ uncertain 那一句、放行(判不出=交回原路，这一枚不许把日常链拦死)", delegate
            {
                List<object> lines = new List<object>();
                bool got = Chain.LauncherReady(In("settings", St(), "launcherAsks", new object[] { AskOne(askDir, LiveSelfTest.LBlankJson) },
                        "waitSeconds", 60, "sleepSeconds", 0), lines);
                return got && lines.Count == 1
                    && S(lines[0]) == "启动器检查：模型回话里抠不出 JSON，按原样放行";
            });
            b.Chk("到点还没进游戏 ⇒ 问了 1 次、点过 0 次那两句 ＋ 返 false(止损不是新闸门)", delegate
            {
                List<object> lines = new List<object>();
                bool got = Chain.LauncherReady(In("settings", St(), "launcherAsks", new object[] { AskOne(askDir, LiveSelfTest.LWaitJson) },
                        "waitSeconds", 1, "sleepSeconds", 1), lines);
                return !got && lines.Count == 2
                    && S(lines[1]) == "启动器检查：等了 1 秒(问模型 1 次、点过 0 次)还没进游戏，这一轮到此为止";
            });
            b.Chk("这一棵树上没有 ask.py ⇒ 问不动那一句原话进话术、放行(第二道前置只认 File.Exists，不执行它)", delegate
            {
                Dictionary<string, object> knob = AskOne(askDir, LiveSelfTest.LClearJson);
                knob["scriptPath"] = Path.Combine(askDir, "ghost.py");
                List<object> lines = new List<object>();
                bool got = Chain.LauncherReady(In("settings", St(), "launcherAsks", new object[] { knob },
                        "waitSeconds", 60, "sleepSeconds", 0), lines);
                return got && lines.Count == 1
                    && S(lines[0]) == @"启动器检查：问模型没成功(缺少 tools\vision\ask.py(问模型这一层的实现)，这一棵树上问不了模型)，分不清是不是更新页，按原样放行";
            });
            b.Chk("窗口没了(no-window) ⇒ 那一句 ＋ 返 false：检查没有对象，等下去也不会自己长回来", delegate
            {
                List<object> lines = new List<object>();
                bool got = Chain.LauncherReady(In("settings", St(), "procName", "gd-no-such-ark-proc", "waitSeconds", 60), lines);
                return !got && lines.Count == 1
                    && S(lines[0]) == "启动器检查：端游窗口没找到(可能已经关了)，这一轮不再检查更新页";
            });

            // ==================== 27) start-mumu：info → launch → 轮询到 AndroidStarted(整枚直答) ====================
            b.Chk("MuMu 目录没填 ⇒ 不把空串递给 RunNative，而是那句专门的话再抛(链体读到就是这一档)", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.StartMuMu(In("vmIndex", "0", "settings", St(), "paths", allPaths));
                });
                return S(msg) == "MuMuManager 无法读取虚拟机信息(0)：MuMu 目录还没填(先跑 setup.ps1)";
            });
            b.Chk("dryRun 同样绕不过「目录没填」那道可读性闸(真身 throw 排在 $DryRun 分支之前)：CLI 实测 rc=2，这一格把实测转成门", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.StartMuMu(In("vmIndex", "0", "dryRun", true, "settings", St(), "paths", allPaths));
                });
                return S(msg) == "MuMuManager 无法读取虚拟机信息(0)：MuMu 目录还没填(先跑 setup.ps1)";
            });
            b.Chk("已在运行 ⇒ 一句「已在运行」并早退，Name 取对方回话里的 name(小写蛇形，是对侧的形状)", delegate
            {
                Chain.ResetNativeCursor();
                object d = Chain.StartMuMu(In("vmIndex", "0", "manager", mumuExe, "settings", St(), "paths", allPaths,
                    "nativeRuns", new object[] { Nat(0, MuMuOnJson, "") }));
                return B(Key(d, "Started")) && S(Key(d, "Name")) == "MuMu安卓设备" && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "MuMu 虚拟机 0(MuMu安卓设备)已在运行"
                    && Chain.NativeCallsUsed == 1 && Cnt(d) == 4;
            });
            b.Chk("没在跑 ＋ dryRun ⇒ 两句(启动 MuMu 虚拟机／[dry-run] 跳过实际启动)，launch 那一发不打", delegate
            {
                Chain.ResetNativeCursor();
                object d = Chain.StartMuMu(In("vmIndex", "0", "manager", mumuExe, "dryRun", true,
                    "settings", St(), "paths", allPaths, "nativeRuns", new object[] { Nat(0, MuMuOffJson, "") }));
                return !B(Key(d, "Started")) && Len(Key(d, "Lines")) == 2
                    && Ln(Key(d, "Lines"), 0) == "启动 MuMu 虚拟机 0(MuMu安卓设备)"
                    && Ln(Key(d, "Lines"), 1) == "[dry-run] 跳过实际启动" && Chain.NativeCallsUsed == 1;
            });
            b.Chk("off→launch→on 三步：夹具按序消费、命中那一档不睡，NativeCallsUsed 恰 3(原样怪癖第 10 笔不在话术里)", delegate
            {
                Chain.ResetNativeCursor();
                object d = Chain.StartMuMu(In("vmIndex", "0", "manager", mumuExe, "settings", St(), "paths", allPaths,
                    "bootTimeoutSeconds", 5, "pollSeconds", 1, "nativeRuns", new object[] {
                        Nat(0, MuMuOffJson, ""), Nat(0, "", ""), Nat(0, MuMuOnJson, "") }));
                return B(Key(d, "Started")) && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "启动 MuMu 虚拟机 0(MuMu安卓设备)"
                    && Chain.NativeCallsUsed == 3;
            });
            b.Chk("settings 里没填 mumuBootTimeoutSeconds ⇒ 递给等待的是 0(真身 [int]$null 就是 0)，当场判超时、不粉饰成默认值", delegate
            {
                Chain.ResetNativeCursor();
                object d = Chain.StartMuMu(In("vmIndex", "0", "manager", mumuExe, "settings", St(), "paths", allPaths,
                    "pollSeconds", 1, "nativeRuns", new object[] { Nat(0, MuMuOffJson, "") }));
                return !B(Key(d, "Started")) && Len(Key(d, "Lines")) == 2
                    && Ln(Key(d, "Lines"), 1) == "等待超时(0s)：MuMu 安卓系统启动";
            });
            b.Chk("launch 返回码非 0 ⇒ 那句是 Err 在前 Out 在后(真身 -join 就这个顺序)，再叠等待超时、Started=false", delegate
            {
                Chain.ResetNativeCursor();
                object d = Chain.StartMuMu(In("vmIndex", "0", "manager", mumuExe, "settings", St(), "paths", allPaths,
                    "bootTimeoutSeconds", 1, "pollSeconds", 1, "nativeRuns", new object[] {
                        Nat(0, MuMuOffJson, ""), Nat(2, "启动失败", "坏") }));
                return !B(Key(d, "Started")) && Len(Key(d, "Lines")) == 3
                    && Ln(Key(d, "Lines"), 1) == "launch 返回码 2：坏启动失败"
                    && Ln(Key(d, "Lines"), 2) == "等待超时(1s)：MuMu 安卓系统启动";
            });

            // ==================== 28) apply-maa-daily-config：现场检查＋递给 plan-maa-queue＋备份写盘回读 ====================
            // ⚠ 无表那一档必须排在本族**第一格**：StageTableNow 的现场读一旦建了空表就进程内缓存到死(原样怪癖第 11 笔)，
            //   后面几格一律显式喂 table 夹具，才不会被这一格的缓存顶掉。
            b.Chk("关卡表建不起来(现场读空) ⇒ 三句按序：无表 WARN→活动档仍写但 WARN→理智计划；预览不落一个字节", delegate
            {
                object d = Chain.ApplyMaaDailyConfig(In("settings", St(), "paths", allPaths,
                    "running", new object[0],
                    "config", MaaTree(Queue("战斗", "Fight", false, null)),
                    "decision", In("Mode", "activity", "ClearSanity", false, "Stage", "CE-5")));
                return Len(Key(d, "Lines")) == 3
                    && Ln(Key(d, "Lines"), 0) == "未能建立 MAA 关卡表，理智计划不会被校验"
                    && Ln(Key(d, "Lines"), 1) == "活动关卡 CE-5 还不在 MAA 本地关卡表(资源可能没更到)，仍按接口结果写入"
                    && Ln(Key(d, "Lines"), 2) == "理智计划 CE-5(拿不到 MAA 关卡表，未校验)"
                    && Ln(Key(d, "Changes"), 0) == "(无需改动)" && Len(Key(d, "ExpectedChains")) == 0
                    && !B(Key(d, "Applied")) && Cnt(d) == 7 && S(Key(d, "Path")) == maaCfgFile;
            });
            b.Chk("MAA 在跑且 apply=true ⇒ 当场抛「请先关闭」，不读盘不写盘(改配置会被它退出时盖回去)", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.ApplyMaaDailyConfig(In("settings", St(), "paths", allPaths,
                        "running", new object[] { "maa" }, "apply", true));
                });
                return S(msg) == "MAA 正在运行，请先关闭再改配置";
            });
            b.Chk("MAA 在跑但只是预览 ⇒ 那句 WARN 排在理智计划之前，Applied=false", delegate
            {
                object d = Chain.ApplyMaaDailyConfig(In("settings", St(), "paths", allPaths,
                    "running", new object[] { "maa" }, "table", In("CE-5", 6),
                    "config", MaaTree(Queue("战斗", "Fight", false, null)),
                    "decision", In("Mode", "idle", "ClearSanity", false, "Stage", "CE-5")));
                return Len(Key(d, "Lines")) == 2
                    && Ln(Key(d, "Lines"), 0) == "MAA 正在运行：预览不受影响，真正跑时会先接管关闭(残留的高权限 MAA 需要由提权那一轮来收)"
                    && Ln(Key(d, "Lines"), 1) == "理智计划 CE-5(共 1 关，单圈理智 6)" && !B(Key(d, "Applied"));
            });
            b.Chk("配置不在 ⇒ 抛「MAA 配置不存在：」带全路径(闸在读盘之前，别去 parse 一个不存在的树)", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.ApplyMaaDailyConfig(In("settings", St(),
                        "paths", In("maaDir", maaDir, "maaExe", "maa.exe", "maaConfig", "ghost.json"),
                        "running", new object[0]));
                });
                return S(msg).StartsWith("MAA 配置不存在：") && S(msg).EndsWith("ghost.json");
            });
            b.Chk("真写盘：IsEnable＋StagePlan＋默认档护甲九项 = 恰 11 处改动，回读三项落地、末句是校验通过(备份：…)", delegate
            {
                Wf(maaDir, "config.json", MaaCfgText);
                object d = Chain.ApplyMaaDailyConfig(In("settings", StArk(In("finishAllSanityWith", "战斗")),
                    "paths", allPaths, "running", new object[0], "apply", true,
                    "table", In("CE-5", 6),
                    "decision", In("Mode", "idle", "ClearSanity", true, "Stage", "CE-5", "FarmMode", "")));
                object[] ch = Gd.List(Key(d, "Changes"));
                if (ch.Length != 11 || Len(Key(d, "ExpectedChains")) != 2
                    || S(Gd.List(Key(d, "ExpectedChains"))[0]) != "Fight") return false;
                if (S(ch[0]) != "战斗 → IsEnable=True" || S(ch[1]) != "战斗 → StagePlan = CE-5") return false;
                if (S(ch[2]) != "战斗 → EnableTargetDrop = True(默认档护甲)"
                    || S(ch[3]) != "战斗 → DropId = 4001(默认档护甲)"
                    || S(ch[4]) != "战斗 → DropCount = 999999(默认档护甲)"
                    || S(ch[5]) != "战斗 → IsInventoryTarget = False(默认档护甲)"
                    || S(ch[9]) != "战斗 → UseStone = False(默认档护甲)") return false;
                if (!Ln(Key(d, "Lines"), 1).StartsWith("MAA 配置已更新并校验通过(备份：")) return false;
                object[] q = Gd.List(Gd.Get(Row(Gd.Get(Gd.Get(GdJson.Read(maaCfgFile), "Configurations"), "Default")), "TaskQueue"));
                if (q.Length != 2) return false;
                Dictionary<string, object> fight = Row(q[0]);
                return B(fight["IsEnable"]) && S(Gd.List(fight["StagePlan"])[0]) == "CE-5"
                    && S(fight["DropId"]) == "4001" && I(fight["TimesLimit"]) == 20 && B(Key(d, "Applied"));
            });
            b.Chk("没有 Fight 队列 ⇒ 一处都不改(\"(无需改动)\")、期望链空表，理智计划那一句照旧打", delegate
            {
                object d = Chain.ApplyMaaDailyConfig(In("settings", St(), "paths", allPaths,
                    "running", new object[0], "table", In("CE-5", 6),
                    "config", MaaTree(Queue("启动", "StartUp", false, null)),
                    "decision", In("Mode", "idle", "ClearSanity", false, "Stage", "CE-5")));
                return Len(Key(d, "Lines")) == 1 && Ln(Key(d, "Lines"), 0) == "理智计划 CE-5(共 1 关，单圈理智 6)"
                    && Len(Key(d, "Changes")) == 1 && Ln(Key(d, "Changes"), 0) == "(无需改动)"
                    && Len(Key(d, "ExpectedChains")) == 0 && S(Key(d, "Stage")) == "CE-5";
            });

            // 2026-11-08 刀㉓ 笔(b) 第三子批 第十一刀：test-ark [t3] 那九枚 apply-maa-daily-config 行为格(与 [t2] 三枚
            //   check-stages 拆分/合计/未知格)换家到本节——下面五枚逐枚对平 PS 旧判据：keepOn/turnOff 按任务类型、预览
            //   一字节不落盘(SHA-256 前后同)、event-pending 不烧理智、event 档照刷活动关、坏关卡代号整句抛出。
            b.Chk("日常链路都在启用集、停用链路一枚都不进：keepQueuesEnabled 全在 ExpectedChains、disableQueues 按任务类型拦下", delegate
            {
                object d = Chain.ApplyMaaDailyConfig(In("settings", StArk(In("finishAllSanityWith", "战斗",
                    "keepQueuesEnabled", new object[] { "StartUp", "Award" },
                    "disableQueues", new object[] { "Roguelike" })),
                    "paths", allPaths, "running", new object[0], "table", In("CE-5", 6, "SR-8", 21),
                    "config", MaaTree(Queue("战斗", "Fight", true, "CE-5"), Queue("开始唤醒", "StartUp", true, null),
                        Queue("领取奖励", "Award", false, null), Queue("自动肉鸽", "Roguelike", true, null)),
                    "decision", In("Mode", "idle", "ClearSanity", true, "Stage", "CE-5")));
                object[] ch = Gd.List(Key(d, "ExpectedChains"));
                if (ch.Length != 3 || S(ch[0]) != "Fight" || S(ch[1]) != "StartUp" || S(ch[2]) != "Award") return false;
                foreach (object o in ch) if (S(o) == "Roguelike") return false;
                bool award = false, rogue = false;
                foreach (object o in Gd.List(Key(d, "Changes")))
                {
                    string sc = S(o);
                    if (sc == "领取奖励 → IsEnable=True") award = true;
                    if (sc == "自动肉鸽 → IsEnable=False") rogue = true;
                }
                return award && rogue;
            });
            b.Chk("预览一字节不落盘：config.json 的 SHA-256 前后相同，且 Changes 非空(算出了改动、只是没写)", delegate
            {
                Wf(maaDir, "config.json", MaaCfgText);
                string before = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(maaCfgFile)));
                object d = Chain.ApplyMaaDailyConfig(In("settings", StArk(In("finishAllSanityWith", "战斗")),
                    "paths", allPaths, "running", new object[0], "table", In("CE-5", 6),
                    "decision", In("Mode", "idle", "ClearSanity", true, "Stage", "CE-5")));
                string after = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(maaCfgFile)));
                return before == after && !B(Key(d, "Applied")) && Len(Key(d, "Changes")) > 0;
            });
            b.Chk("不烧理智的形状(event-pending + ClearSanity=false)：Fight 被停、ExpectedChains 为空、StagePlan 一个字都不写", delegate
            {
                object d = Chain.ApplyMaaDailyConfig(In("settings", StArk(In("finishAllSanityWith", "战斗")),
                    "paths", allPaths, "running", new object[0], "table", In("SR-8", 21),
                    "config", MaaTree(Queue("战斗", "Fight", true, "CE-5")),
                    "decision", In("Mode", "event-pending", "ClearSanity", false, "Stage", "SR-8")));
                bool stop = false, stagePlan = false;
                foreach (object o in Gd.List(Key(d, "Changes")))
                {
                    string sc = S(o);
                    if (sc == "战斗 → IsEnable=False") stop = true;
                    if (sc.Contains("StagePlan")) stagePlan = true;
                }
                return stop && !stagePlan && Len(Key(d, "ExpectedChains")) == 0;
            });
            b.Chk("event 档 + ClearSanity=true：Fight 进启用集且 StagePlan = SR-8(活动关照刷)", delegate
            {
                object d = Chain.ApplyMaaDailyConfig(In("settings", StArk(In("finishAllSanityWith", "战斗")),
                    "paths", allPaths, "running", new object[0], "table", In("SR-8", 21),
                    "config", MaaTree(Queue("战斗", "Fight", false, null)),
                    "decision", In("Mode", "event", "ClearSanity", true, "Stage", "SR-8")));
                object[] ch = Gd.List(Key(d, "ExpectedChains"));
                bool enable = false, stagePlan = false;
                foreach (object o in Gd.List(Key(d, "Changes")))
                {
                    string sc = S(o);
                    if (sc == "战斗 → IsEnable=True") enable = true;
                    if (sc == "战斗 → StagePlan = SR-8") stagePlan = true;
                }
                return ch.Length == 1 && S(ch[0]) == "Fight" && enable && stagePlan;
            });
            b.Chk("长草档坏关卡代号 ⇒ 整句抛出「长草计划里的关卡代号不被 MAA 识别：NOT-A-STAGE → 请改 settings.json 的 ark 计划」", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.ApplyMaaDailyConfig(In("settings", St(), "paths", allPaths,
                        "running", new object[0], "table", In("CE-5", 6),
                        "config", MaaTree(Queue("战斗", "Fight", false, null)),
                        "decision", In("Mode", "idle", "ClearSanity", true, "Stage", "NOT-A-STAGE")));
                });
                return S(msg) == "长草计划里的关卡代号不被 MAA 识别：NOT-A-STAGE → 请改 settings.json 的 ark 计划";
            });

            // ==================== 29) apply-m9a-instance：点号字面键那两格 ＋ 三句原话 ====================
            b.Chk("M9A 在跑且 m9aCliArgs 缺键(null⇒开) ⇒ 那句 WARN ＋ Changes 只一格「交给命令行 -i 转发」", delegate
            {
                object d = Chain.ApplyM9AInstance(In("settings", St(), "paths", allPaths,
                    "running", new object[] { "m9a" }, "appSettings", m9aFile,
                    "instance", In("Id", "new-id", "Name", "新实例")));
                return Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "M9A 正在运行：改实例这一步跳过，启动时由命令行 -i/-f 接管(会转发给正在跑的实例)"
                    && Ln(Key(d, "Changes"), 0) == "(M9A 在跑，交给命令行 -i 转发)" && !B(Key(d, "Applied"));
            });
            b.Chk("原样怪癖三族里的 M9ACliOn：明写 false ⇒ 在跑就是硬闸(与 takeover 那族同判，与 arkAutoUpdateClick 那族反判)", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.ApplyM9AInstance(In("settings", St("m9aCliArgs", false), "paths", allPaths,
                        "running", new object[] { "m9a" }, "appSettings", m9aFile,
                        "instance", In("Id", "new-id", "Name", "新实例"), "m9aCliArgs", false));
                });
                return S(msg) == "M9A 正在运行，请先关闭再改实例";
            });
            b.Chk("appsettings 不在 ⇒ 抛「M9A appsettings 不存在：」＋那格路径(闸在读盘之前)", delegate
            {
                object msg = ThrownMsg(delegate
                {
                    return Chain.ApplyM9AInstance(In("settings", St(), "paths", allPaths,
                        "running", new object[0], "appSettings", Path.Combine(chainDir, "ghost-appsettings.json"),
                        "instance", In("Id", "x", "Name", "y")));
                });
                return S(msg).StartsWith("M9A appsettings 不存在：") && S(msg).EndsWith("ghost-appsettings.json");
            });
            b.Chk("真切换：两处改动逐字(旧 id→新 id(名)、NoAutoStart True→False)＋末句「已切到[新实例](备份：」＋盘上三键", delegate
            {
                Wf(chainDir, "appsettings.json", M9aTodoText);
                object d = Chain.ApplyM9AInstance(In("settings", St(), "paths", allPaths,
                    "running", new object[0], "appSettings", m9aFile, "apply", true,
                    "instance", In("Id", "new-id", "Name", "新实例")));
                Dictionary<string, object> back = Row(GdJson.Read(m9aFile));
                return Len(Key(d, "Changes")) == 2
                    && Ln(Key(d, "Changes"), 0) == "Instances.LastActive: old-id → new-id(新实例)"
                    && Ln(Key(d, "Changes"), 1) == "NoAutoStart: True → False(启动即自动开始)"
                    && Ln(Key(d, "Lines"), 0).StartsWith("M9A 实例已切到[新实例](备份：")
                    && S(back["Instances.LastActive"]) == "new-id" && S(back["Instances.LastActiveName"]) == "新实例"
                    && S(back["NoAutoStart"]) == "False" && B(Key(d, "Applied"));
            });
            b.Chk("已经是那一枚 ⇒ \"(已是 新实例，无需改动)\"、一句都不吭、apply 那一档开着也不落笔(盘上原文一字未变)", delegate
            {
                Wf(chainDir, "appsettings.json", M9aDoneText);
                object d = Chain.ApplyM9AInstance(In("settings", St(), "paths", allPaths,
                    "running", new object[0], "appSettings", m9aFile, "apply", true,
                    "instance", In("Id", "new-id", "Name", "新实例")));
                return Len(Key(d, "Lines")) == 0 && Len(Key(d, "Changes")) == 1
                    && Ln(Key(d, "Changes"), 0) == "(已是 新实例，无需改动)"
                    && B(Key(d, "Applied"))                        // Applied 回声的是"这一档要落笔"，不是"落了几笔"
                    && File.ReadAllText(m9aFile) == M9aDoneText;   // 没进写盘那条路：原文一个字节都没换
            });

            // ==================== 30) update-ark-farm-ledger：offset 续读＋两本账＋一句总结 ====================
            // 每格独占一份 stateDir(格与格之间不许互相看见)，材料表一律显式喂 itemIndex
            // (现场读那一路会进程内缓存，临时树上没有 item_index.json ⇒ 缓存一份空表就是给后面的格下毒)。
            b.Chk("gui.log 不 ready ⇒ 全零＋Summary 空串(链体那句 bare string 要的就是空，不是报错)", delegate
            {
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(),
                    "guiLog", Path.Combine(chainDir, "ghost-gui.log"),
                    "stateDir", Path.Combine(ledgerDir, "s0")));
                return B(Key(d, "Done")) && S(Key(d, "Summary")).Length == 0 && I(Key(d, "Blocks")) == 0
                    && I(Key(d, "Plus")) == 0 && I(Key(d, "Unknown")) == 0 && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("首跑没缓存＋一场掉落 ⇒ 那两句用「；」串起来，账本落在**给定**的 stateDir 上", delegate
            {
                string s = Path.Combine(ledgerDir, "s1");
                string gui = WLog(chainDir, "gui-ledger-1.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(),
                    "guiLog", gui, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                return S(Key(d, "Summary")) == "没有库存缓存，初值按 0 起账(会先多刷一轮才收敛)；记了 1 场掉落、入账 +2"
                    && I(Key(d, "Blocks")) == 1 && I(Key(d, "Plus")) == 2
                    && inv != null && I(Gd.Get(Row(inv["30031"]), "count")) == 2;
            });
            b.Chk("offset 续读：第二场只算新追加的那一块，账从 2 加到 5(整份重算就是重复入账)", delegate
            {
                string s = Path.Combine(ledgerDir, "s2");
                string gui = WLog(chainDir, "gui-ledger-2.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                File.AppendAllText(gui, "\n[INF]<13> CE-5 掉落统计\n[INF]<14> 先锋封装芯片: 5 (+3)", new UTF8Encoding(false));
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                Dictionary<string, object> dt = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-droptable.json"))), "30031"));
                return S(Key(d, "Summary")) == "记了 1 场掉落、入账 +3" && I(Key(d, "Plus")) == 3
                    && I(Gd.Get(Row(inv["30031"]), "count")) == 5
                    && dt != null && I(Gd.Get(Row(dt["CE-5"]), "runs")) == 2
                    && I(Gd.Get(Row(dt["CE-5"]), "qty")) == 5;
            });
            b.Chk("首跑有 DepotData 缓存 ⇒ 按它标定初值(计数是**字符串**那一格照抄)，再加本场 +2 ⇒ 9", delegate
            {
                string s = Path.Combine(ledgerDir, "s3");
                string depot = Wf(chainDir, "DepotData.json", @"{""data"":{""30031"":""7""}}");
                string gui = WLog(chainDir, "gui-ledger-3.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(),
                    "guiLog", gui, "stateDir", s, "depotData", depot));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                return S(Key(d, "Summary")) == "已按 MAA 库存缓存标定初值(1 项，估算)；记了 1 场掉落、入账 +2"
                    && I(Gd.Get(Row(inv["30031"]), "count")) == 9;
            });
            b.Chk("材料表认不出的名字 ⇒ 入账 +0、那一句带前 3 个名字、另落一本 ark-farm-drops.json(不并入账本)", delegate
            {
                string s = Path.Combine(ledgerDir, "s4");
                string gui = WLog(chainDir, "gui-ledger-4.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 神秘材料: 1 (+1)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(),
                    "guiLog", gui, "stateDir", s));
                object[] drops = Gd.List(GdJson.Read(Path.Combine(s, "ark-farm-drops.json")));
                return S(Key(d, "Summary")) == "没有库存缓存，初值按 0 起账(会先多刷一轮才收敛)；记了 1 场掉落、入账 +0；1 个名字没在材料表认出来(神秘材料)"
                    && I(Key(d, "Plus")) == 0 && I(Key(d, "Unknown")) == 1
                    && drops.Length == 1 && S(drops[0]) == "神秘材料";
            });
            b.Chk("原样怪癖第 12 笔：offset.path 是本机绝对路径 ⇒ 只落盘，不进 Summary／不进 Lines(泄漏面先过自检这一道)", delegate
            {
                string s = Path.Combine(ledgerDir, "s5");
                string gui = WLog(chainDir, "gui-ledger-5.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(),
                    "guiLog", gui, "stateDir", s));
                string sum = S(Key(d, "Summary"));
                foreach (object o in Gd.List(Key(d, "Lines"))) if (S(o).Contains(tmp)) return false;
                Dictionary<string, object> off = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "offset"));
                return !sum.Contains(tmp) && sum.Length > 0 && off != null && S(off["path"]) == gui;
            });

            // ---- 转接刀·运行面刀⑬ 补格(2026-11-06)：test-ark 的 [farm] 段将被摘，摘之前先把对面补齐 ----
            // 那一段里"打 ark-farm.ps1 三枚真身(Get-GdArkItemMaps:53／Read-GdArkFarmLedger:93／
            // Update-GdArkFarmLedger:125)"的语义，上面 30 段只钉到"追加半块"与"未知名字"两档，
            // 剩下这些档对面**零格**：幂等、offset 数值、跨路径起读、同块多件、拒绝表与总数形、
            // 坏账本降级、stateDir 落点、材料表三规则。删了就是净丢覆盖，所以先补后删(在册刀序)。
            b.Chk("刀⑬ 补1(幂等)：同一本日志连跑两遍 ⇒ 第二遍空话、账一位不回扫", delegate
            {
                string s = Path.Combine(ledgerDir, "s6");
                string gui = WLog(chainDir, "gui-ledger-6.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                object first = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                object again = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                return S(Key(first, "Summary")).Length > 0 && S(Key(again, "Summary")).Length == 0
                    && B(Key(again, "Done")) && I(Key(again, "Blocks")) == 0 && I(Key(again, "Plus")) == 0
                    && inv != null && I(Gd.Get(Row(inv["30031"]), "count")) == 2;
            });
            b.Chk("刀⑬ 补2(offset 数值)：存的偏移＝那本日志的真实字节数(不是行数、不是上一拍的旧长度)", delegate
            {
                string s = Path.Combine(ledgerDir, "s7");
                string gui = WLog(chainDir, "gui-ledger-7.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)",
                    "[INF]<13> CE-4 掉落统计", "[INF]<14> 先锋封装芯片: 3 (+1)");
                Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                Dictionary<string, object> off = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "offset"));
                return off != null && S(off["path"]) == gui && I(off["offset"]) == new FileInfo(gui).Length
                    && new FileInfo(gui).Length > 0;
            });
            b.Chk("刀⑬ 补3(跨路径起读)：账本里的 offset.path 与这次那本不同 ⇒ 整本从 0 读(偏移不跨文件复用)", delegate
            {
                string s = Path.Combine(ledgerDir, "s8");
                string guiA = WLog(chainDir, "gui-ledger-8a.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", guiA, "stateDir", s));
                // 第二本必须**比第一本长**(第一本 64 字节、这一本 147 字节)：否则"错用旧偏移"会被
                // 下面那句 len>=savedOffset 的守卫自动兜住，这一格就没有牙了。
                string guiB = WLog(chainDir, "gui-ledger-8b.log",
                    "[INF]<21> CE-5 掉落统计", "[INF]<22> 先锋封装芯片: 2 (+2)",
                    "[INF]<23> 这一行没有配额形状只用来把这本日志写得比上一本长");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", guiB, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                Dictionary<string, object> off = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "offset"));
                return new FileInfo(guiB).Length > new FileInfo(guiA).Length
                    && I(Key(d, "Blocks")) == 1 && I(Key(d, "Plus")) == 2
                    && inv != null && I(Gd.Get(Row(inv["30031"]), "count")) == 4
                    && off != null && S(off["path"]) == guiB;
            });
            b.Chk("刀⑬ 补4(同块多件)：同一行两个材料都入账(只记第一个就是漏账)", delegate
            {
                string s = Path.Combine(ledgerDir, "s9");
                string gui = WLog(chainDir, "gui-ledger-9.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)   或铝石: 1 (+1)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdxMany(), "guiLog", gui, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                return I(Key(d, "Blocks")) == 1 && I(Key(d, "Plus")) == 3 && I(Key(d, "Unknown")) == 0
                    && inv != null && I(Gd.Get(Row(inv["30031"]), "count")) == 2
                    && I(Gd.Get(Row(inv["30115"]), "count")) == 1;
            });
            b.Chk("刀⑬ 补5(拒绝表＋总数形)：「当前次数」被拒绝表挡住不入未识别账；「: 30 (30)」只按 (+N) 入账，但学习表仍记一场", delegate
            {
                string s = Path.Combine(ledgerDir, "s10");
                string gui = WLog(chainDir, "gui-ledger-10.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)   当前次数 : 3",
                    "[INF]<13> LS-5 掉落统计", "[INF]<14> 先锋封装芯片: 30 (30)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                Dictionary<string, object> dt = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-droptable.json"))), "30031"));
                return I(Key(d, "Blocks")) == 2 && I(Key(d, "Plus")) == 2 && I(Key(d, "Unknown")) == 0
                    && inv != null && I(Gd.Get(Row(inv["30031"]), "count")) == 2
                    && dt != null && I(Gd.Get(Row(dt["CE-5"]), "runs")) == 1 && I(Gd.Get(Row(dt["CE-5"]), "qty")) == 2
                    && I(Gd.Get(Row(dt["LS-5"]), "runs")) == 1 && I(Gd.Get(Row(dt["LS-5"]), "qty")) == 0;
            });
            b.Chk("刀⑬ 补6(坏账本降级)：两本账都读不动 ⇒ 那两句 WARN 原话在场、按空账继续记(不抛、不整段罢工)", delegate
            {
                string s = Path.Combine(ledgerDir, "s11");
                Wf(s, "ark-inventory.json", "{\"byId\": ###");
                Wf(s, "ark-droptable.json", "{\"30031\": ###");
                string gui = WLog(chainDir, "gui-ledger-11.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                string l0 = Ln(Key(d, "Lines"), 0);
                string l1 = Ln(Key(d, "Lines"), 1);
                return Len(Key(d, "Lines")) == 2 && l0 != null && l0.Contains("ark-inventory.json 读不了")
                    && l0.Contains("按空账处理") && l1 != null && l1.Contains("ark-droptable.json 读不了")
                    && l1.Contains("学习关系按空处理")
                    && S(Key(d, "Summary")) == "记了 1 场掉落、入账 +2"
                    && inv != null && I(Gd.Get(Row(inv["30031"]), "count")) == 2;
            });
            b.Chk("刀⑬ 补7(stateDir 落点)：同一本日志换一份 stateDir ⇒ 账按目录分家，各写各的、不共一本", delegate
            {
                string gui = WLog(chainDir, "gui-ledger-12.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                string sx = Path.Combine(ledgerDir, "s12x");
                string sy = Path.Combine(ledgerDir, "s12y");
                Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", sx));
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", sy));
                Dictionary<string, object> ix = Row(Gd.Get(Row(GdJson.Read(Path.Combine(sx, "ark-inventory.json"))), "byId"));
                Dictionary<string, object> iy = Row(Gd.Get(Row(GdJson.Read(Path.Combine(sy, "ark-inventory.json"))), "byId"));
                return S(Key(d, "Summary")) == "没有库存缓存，初值按 0 起账(会先多刷一轮才收敛)；记了 1 场掉落、入账 +2"
                    && ix != null && iy != null && I(Gd.Get(Row(ix["30031"]), "count")) == 2
                    && I(Gd.Get(Row(iy["30031"]), "count")) == 2;
            });
            b.Chk("刀⑬ 补8(材料表三规则)：重名取首现(30031 先入表 ⇒ 39007 不接账)、查表不分大小写(alloyplate 认得 AlloyPlate)", delegate
            {
                string s = Path.Combine(ledgerDir, "s13");
                string gui = WLog(chainDir, "gui-ledger-13.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)   alloyplate: 1 (+1)");
                object d = Chain.UpdateFarmLedger(In("paths", allPaths, "itemIndex", ItemIdxMany(), "guiLog", gui, "stateDir", s));
                Dictionary<string, object> inv = Row(Gd.Get(Row(GdJson.Read(Path.Combine(s, "ark-inventory.json"))), "byId"));
                return I(Key(d, "Plus")) == 3 && I(Key(d, "Unknown")) == 0
                    && inv != null && inv.ContainsKey("30031") && !inv.ContainsKey("39007") && inv.ContainsKey("30116")
                    && I(Gd.Get(Row(inv["30031"]), "count")) == 2 && I(Gd.Get(Row(inv["30116"]), "count")) == 1;
            });

            // ==================== 30b) resolve-ark-farm-decision：三前置＋取数半段直身(判定面进核的 farm 通路) ====================
            // 每格独占一份 stateDir(f0..f5)，材料表与关卡表一律显式喂(itemIndex/table 两枚夹具)——
            // 现场读那两路会进程内缓存，别让任何一格把缓存下毒。
            b.Chk("三前置·farmAppend 显式 false ⇒ 一句不吭直接 Farm=null(PS 的 -eq $false 形：缺省可不是 false)", delegate
            {
                object d = Chain.ResolveArkFarmDecision(In("settings", StArk(In("farmAppend", false)),
                    "decision", In("Mode", "idle"), "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", Path.Combine(ledgerDir, "f0")));
                return Key(d, "Farm") == null && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("三前置·decision 非 idle(活动轮已有主张) ⇒ Farm=null 一句不吭——补刷只在长草轮前置", delegate
            {
                object d = Chain.ResolveArkFarmDecision(In("settings", StArk(In()),
                    "decision", In("Mode", "Annihilation"), "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", Path.Combine(ledgerDir, "f1")));
                return Key(d, "Farm") == null && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("三前置·今日已认栽(MAA 判关卡全关的 mark 落着今天) ⇒ 那句 INFO 原话＋Farm=null", delegate
            {
                string s = Path.Combine(ledgerDir, "f2");
                GdJson.Write(Path.Combine(s, "ark-fight-skip-mark.json"), In("Date", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                object d = Chain.ResolveArkFarmDecision(In("settings", StArk(In()),
                    "decision", In("Mode", "idle"), "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", s));
                return Key(d, "Farm") == null && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "补刷前置今日已认栽(MAA 判关卡全关) → 本轮不前置补刷关，按长草计划走";
            });
            b.Chk("真跑通：账本起底 0＋学习表 CE-5 每场 2 ⇒ Farm 活、Times=缺口 5、关从学习表挑、Reason 逐字报数", delegate
            {
                string s = Path.Combine(ledgerDir, "f3");
                GdJson.Write(Path.Combine(s, "ark-inventory.json"), In("byId", In("30031", In("count", 0, "origin", ""))));
                GdJson.Write(Path.Combine(s, "ark-droptable.json"), In("30031", In("CE-5", In("runs", 10, "qty", 20))));
                object d = Chain.ResolveArkFarmDecision(In("settings",
                    StArk(In("farmTargets", new object[] { "先锋封装芯片=5" }, "farmMaxTimesPerRound", 12)),
                    "decision", In("Mode", "idle"), "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", s));
                Dictionary<string, object> fp = Row(Key(d, "Farm"));
                return fp != null && B(fp["Active"]) && S(fp["ItemId"]) == "30031" && S(fp["ItemName"]) == "先锋封装芯片"
                    && I(fp["Gap"]) == 5 && I(fp["Times"]) == 5 && Len(fp["Stages"]) == 1
                    && S(Gd.List(fp["Stages"])[0]) == "CE-5"
                    && S(fp["Reason"]) == "补刷 先锋封装芯片×5 @CE-5：目标 5，现有 0，还差 5，按每场掉 1 保守给次数、凑满目标为止(学习到的每场约 2)";
            });
            b.Chk("原样怪癖(PS :315)：farmMaxTimesPerRound 缺键 ⇒ TryParse 把 out 清 0 ⇒ FarmPlan 里 <1 钳 1 ⇒ Times=1、那格封顶话术在场——别在直身里救回 12", delegate
            {
                string s = Path.Combine(ledgerDir, "f4");
                GdJson.Write(Path.Combine(s, "ark-inventory.json"), In("byId", In("30031", In("count", 0, "origin", ""))));
                GdJson.Write(Path.Combine(s, "ark-droptable.json"), In("30031", In("CE-5", In("runs", 10, "qty", 20))));
                object d = Chain.ResolveArkFarmDecision(In("settings",
                    StArk(In("farmTargets", new object[] { "先锋封装芯片=5" })),
                    "decision", In("Mode", "idle"), "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", s));
                Dictionary<string, object> fp = Row(Key(d, "Farm"));
                return fp != null && B(fp["Active"]) && I(fp["Times"]) == 1
                    && S(fp["Reason"]).EndsWith("(单轮封顶 1，剩下的明天接着刷)");
            });
            b.Chk("depot 纠偏：缓存比账本新才采信 ⇒ 账本 9 被实时读数 7 纠偏、那句 INFO 逐字；纠偏后已够 ⇒ 不再出 Farm", delegate
            {
                string s = Path.Combine(ledgerDir, "f5");
                string maa2 = Path.Combine(tmp, "maa2");
                string inv = Path.Combine(s, "ark-inventory.json");
                GdJson.Write(inv, In("byId", In("30031", In("count", 9, "origin", ""))));
                string depot = Wf(Path.Combine(maa2, "data"), "DepotData.json", @"{""data"":{""30031"":""7""}}");
                File.SetLastWriteTime(inv, DateTime.Now.AddMinutes(-2));
                File.SetLastWriteTime(depot, DateTime.Now);
                object d = Chain.ResolveArkFarmDecision(In("settings",
                    StArk(In("farmTargets", new object[] { "30031=3" })),
                    "decision", In("Mode", "idle"), "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", s, "paths", In("maaDir", maa2)));
                return Key(d, "Farm") == null && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "补刷纠偏：库存缓存比账本新，已按实时读数纠偏：id=30031 现值 9→7";
            });
            b.Chk("dedicated 绕过三前置直进判定且 !Active 那句 WARN 原话(PS :322)——decision 空对象也走得到(专用轮不等 idle)", delegate
            {
                object d = Chain.ResolveArkFarmDecision(In("settings", StArk(In()),
                    "decision", In(), "dedicated", true, "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", Path.Combine(ledgerDir, "f6")));
                return Key(d, "Farm") == null && Len(Key(d, "Lines")) == 1
                    && Ln(Key(d, "Lines"), 0) == "专用材料轮没有可刷的目标：没配 ark.farmTargets → 材料补刷未启动";
            });
            // ---- 刀⑬ 补格(承上)：决策通道这一路对面缺的两档 ----
            // f7 钉"首跑 Resolve 先于任何记账"那条(旧 PS 侧靠 test-ark 的「文件不在就是空账」那格)；
            // f8 钉 IdToName 反查轴(目标写代号时话术用名不用号)——旧那格打在 Parse 层，
            //     这里打在**决策通道的回执**上，摘件后才有对面。
            b.Chk("刀⑬ 补9(空账起底)：两本账文件都不在 ⇒ 不抛、按空账起(Have=0)，且不塞一句 WARN 进 Lines", delegate
            {
                string s = Path.Combine(ledgerDir, "f7");
                object d = Chain.ResolveArkFarmDecision(In("settings",
                    StArk(In("farmTargets", new object[] { "先锋封装芯片=5@CE-5" }, "farmMaxTimesPerRound", 12)),
                    "decision", In("Mode", "idle"), "itemIndex", ItemIdx(), "table", In("CE-5", 36),
                    "stateDir", s));
                Dictionary<string, object> fp = Row(Key(d, "Farm"));
                return fp != null && B(fp["Active"]) && I(fp["Have"]) == 0 && I(fp["Gap"]) == 5
                    && I(fp["Times"]) == 5 && Len(Key(d, "Lines")) == 0;
            });
            b.Chk("刀⑬ 补10(反查轴)：目标写成代号 ⇒ ItemName 从 IdToName 反查回来，Reason 用名不用号(重名夹具下仍落 30031)", delegate
            {
                string s = Path.Combine(ledgerDir, "f8");
                GdJson.Write(Path.Combine(s, "ark-inventory.json"), In("byId", In("30031", In("count", 0, "origin", ""))));
                GdJson.Write(Path.Combine(s, "ark-droptable.json"), In("30031", In("CE-5", In("runs", 10, "qty", 20))));
                object d = Chain.ResolveArkFarmDecision(In("settings",
                    StArk(In("farmTargets", new object[] { "30031=5" }, "farmMaxTimesPerRound", 12)),
                    "decision", In("Mode", "idle"), "itemIndex", ItemIdxMany(), "table", In("CE-5", 36),
                    "stateDir", s));
                Dictionary<string, object> fp = Row(Key(d, "Farm"));
                return fp != null && B(fp["Active"]) && S(fp["ItemId"]) == "30031"
                    && S(fp["ItemName"]) == "先锋封装芯片" && I(fp["Times"]) == 5
                    && S(fp["Reason"]).StartsWith("补刷 先锋封装芯片×5 @CE-5：目标 5，现有 0，还差 5");
            });

            // ==================== 31) CLI 接线：这十枚真的挂上了分发链(名册两处同批的自证) ====================
            b.Chk("takeover-processes 走 --in/--out：退出码 0、四键齐、那一句 WARN 在信封的 data 里", delegate
            {
                string inPath = Path.Combine(tmp, "tk-in.json");
                string outPath = Path.Combine(tmp, "tk-out.json");
                GdJson.Write(inPath, In("names", new object[] { "maa" }, "running", new object[] { "maa" },
                    "settings", St("takeOverAssistant", true), "sleepSeconds", 0, "noKill", true,
                    "fakeProcs", new object[] { 11 }));
                int code = GdCli.Run("takeover-processes", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                Dictionary<string, object> data = Row(Gd.Get(env, "data"));
                return code == 0 && Gd.Truthy(Gd.Get(env, "ok")) && S(Gd.Get(env, "verb")) == "takeover-processes"
                    && data != null && data.Count == 4 && Len(data["Stopped"]) == 1
                    && Ln(data["Lines"], 0) == "助手 正在运行 → 先关掉再改配置(避免它退出时把配置写回旧值)";
            });
            b.Chk("read-maa-fight-skip 走 CLI：读不到日志不是错误，退出码 0＋Found=false 那一档照摆", delegate
            {
                string inPath = Path.Combine(tmp, "fs-in.json");
                string outPath = Path.Combine(tmp, "fs-out.json");
                GdJson.Write(inPath, In("paths", allPaths, "guiLog", Path.Combine(chainDir, "ghost-gui.log")));
                int code = GdCli.Run("read-maa-fight-skip", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                Dictionary<string, object> data = Row(Gd.Get(env, "data"));
                return code == 0 && data != null && !Gd.Truthy(data["Found"])
                    && S(data["Reason"]) == "gui-log-missing";
            });
            b.Chk("apply-maa-daily-config 漏喂 settings：退出码 2＋「还没有 config\\settings.json」原话(临时树故意不建那一枚)", delegate
            {
                string inPath = Path.Combine(tmp, "mc-in.json");
                string outPath = Path.Combine(tmp, "mc-out.json");
                GdJson.Write(inPath, In("decision", In("Mode", "idle", "ClearSanity", false, "Stage", "CE-5")));
                int code = GdCli.Run("apply-maa-daily-config", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                return code == 2 && !Gd.Truthy(Gd.Get(env, "ok"))
                    && S(Gd.Get(env, "error")).Contains("还没有 config\\settings.json");
            });
            b.Chk("名册第二处(usage 长串)随行自证：未知子命令那句报错里必须同时数到本批十枚", delegate
            {
                string outPath = Path.Combine(tmp, "usage2-out.json");
                int code = GdCli.Run("no-such-verb", new string[] { "--out=" + outPath });
                string err = S(Gd.Get(GdJson.ReadEnvelope(outPath), "error"));
                return code == 1 && err.Contains("takeover-processes") && err.Contains("stop-maa-and-game")
                    && err.Contains("stop-m9a") && err.Contains("read-maa-fight-skip")
                    && err.Contains("read-maa-stage-observation") && err.Contains("start-ark-client")
                    && err.Contains("start-mumu") && err.Contains("apply-maa-daily-config")
                    && err.Contains("apply-m9a-instance") && err.Contains("update-ark-farm-ledger");
            });

            // ==================== 31b) farm 两枚走 CLI：刀⑬ 改口后**真消费者**那条路由的本体 ====================
            // 段 30/30b 打的是**进程内直身**(Chain.UpdateFarmLedger／Chain.ResolveArkFarmDecision)，
            // 而 lib\ark-module.ps1 的 StatusPlan 行与 lib\agent.ps1 的快照打的是**子进程信封**
            // (Invoke-GdCli -Verb ...)。两副眼镜缺一副就会出现"直身全绿、verb 却没接上"的假绿——
            // 摘 test-ark 那一段之前，先把这条路由自己钉住(回执的键名与话术逐字＝直身那一副的复刻)。
            b.Chk("update-ark-farm-ledger 走 --in/--out：退出码 0、六键齐、Summary 那句与直身同字", delegate
            {
                string inPath = Path.Combine(tmp, "fl-in.json");
                string outPath = Path.Combine(tmp, "fl-out.json");
                string s = Path.Combine(ledgerDir, "cli-fl");
                string gui = WLog(chainDir, "gui-cli-fl.log",
                    "[INF]<11> CE-5 掉落统计", "[INF]<12> 先锋封装芯片: 2 (+2)");
                GdJson.Write(inPath, In("paths", allPaths, "itemIndex", ItemIdx(), "guiLog", gui, "stateDir", s));
                int code = GdCli.Run("update-ark-farm-ledger", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                Dictionary<string, object> data = Row(Gd.Get(env, "data"));
                return code == 0 && Gd.Truthy(Gd.Get(env, "ok"))
                    && S(Gd.Get(env, "verb")) == "update-ark-farm-ledger"
                    && data != null && data.Count == 6 && B(data["Done"])
                    && S(data["Summary"]) == "没有库存缓存，初值按 0 起账(会先多刷一轮才收敛)；记了 1 场掉落、入账 +2"
                    && I(data["Blocks"]) == 1 && I(data["Plus"]) == 2 && I(data["Unknown"]) == 0
                    && new FileInfo(Path.Combine(s, "ark-inventory.json")).Length > 0;
            });
            b.Chk("resolve-ark-farm-decision 走 --in/--out：dedicated 那一路的 WARN 原话从信封回来(ark-module/agent 拼的就是这一句)", delegate
            {
                string inPath = Path.Combine(tmp, "fd-in.json");
                string outPath = Path.Combine(tmp, "fd-out.json");
                GdJson.Write(inPath, In("settings", StArk(In()), "decision", In(), "dedicated", true,
                    "itemIndex", ItemIdx(), "table", In("CE-5", 36), "stateDir", Path.Combine(ledgerDir, "cli-fd")));
                int code = GdCli.Run("resolve-ark-farm-decision", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                Dictionary<string, object> data = Row(Gd.Get(env, "data"));
                return code == 0 && Gd.Truthy(Gd.Get(env, "ok")) && data != null
                    && Gd.Get(data, "Farm") == null
                    && Ln(data["Lines"], 0) == "专用材料轮没有可刷的目标：没配 ark.farmTargets → 材料补刷未启动";
            });

            // ==================== 32) R9-a 收编那四枚：换的是"谁来答"，不是"答成什么样" ====================
            // 四枚 verb(Round.cs 的 ScreensaverConfig/ScreensaverOff/RoundHistory ＋ Round.Data.cs 的
            // ArkEventProbeUpdate)是核内 `round_answer` 四臂的**取数面**：核内只挑回执里的几枚、键名与键序逐字照
            // 当年转接层写进 res 的那枚(所以这一族钉的是"形状＋磁盘字节形状"，不钉判定 —— 判定仍住段 8 那四档
            // 话术与 `resolve-screensaver-verdict`；这里重钉一遍就是抄第二份判据，§11.64 ④ 那条切分)。
            // ⚠ **现场安全那条**(比"只读自检不许要求机器现场状态"更硬)：`disable-screensaver-for-round`
            //   **不喂 active 的那一路绝不自证** —— 那一档是 SPI 现读，读到"屏保开着"就会真发
            //   SPI_SETSCREENSAVEACTIVE **把用户的屏保关掉**。自检不许改现场 ⇒ 那一路整条留给现场实跑，
            //   连同真 UAC(`start-elevated-round` 不喂 noSpawn)一起登记进下面"够不着清单"。
            //   注册表那一半是**只读**(HKCU\Control Panel\Desktop)，所以 config 这一枚敢留一格现读正例。
            // 每格各钉一副眼镜，别当"报 0 就算验过"(§11.66 ⑩／⑫ 那条)：夹具给了≠眼镜接上了。
            string svDir = Path.Combine(tmp, "sv-off");
            string histDir = Path.Combine(tmp, "hist");
            string roundDir = Path.Combine(tmp, "hist-round");
            string flatDir = Path.Combine(tmp, "hist-flat");
            string clockDir = Path.Combine(tmp, "hist-clock");
            string probeDir = Path.Combine(tmp, "probe");
            string ghostDir = Path.Combine(tmp, "no-such-state-dir");
            try
            {
                Directory.CreateDirectory(svDir);
                Directory.CreateDirectory(histDir);
                Directory.CreateDirectory(roundDir);
                Directory.CreateDirectory(flatDir);
                Directory.CreateDirectory(clockDir);
                Directory.CreateDirectory(probeDir);
            }
            catch { }

            // ---------- read-screensaver-config(原 Get-GdScreensaverConfig；common.ps1 旧 359-378 是历史引文，别当活锚读) ----------
            b.Chk("config：reg 夹具给齐 ⇒ 五枚同序 Found/Saver/Product/Active/TimeoutSec、TimeoutSec 折成整数", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverConfig(In("reg", In(
                    "Saver", "SFAKE.scr", "Active", "1", "TimeoutSec", "600", "Product", "FakeProduct"))));
                List<string> keys = new List<string>(d.Keys);
                return keys.Count == 5 && keys[0] == "Found" && keys[1] == "Saver" && keys[2] == "Product"
                    && keys[3] == "Active" && keys[4] == "TimeoutSec"
                    && d["Found"] is bool && B(d["Found"]) && S(d["Saver"]) == "SFAKE.scr"
                    && S(d["Product"]) == "FakeProduct" && S(d["Active"]) == "1" && I(d["TimeoutSec"]) == 600;
            });
            b.Chk("config：reg 写了 null ⇒ Found=false、四枚全空/0 —— 这一档**绝不等于\"没装屏保\"**(真身那句 return $null 同形)", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverConfig(In("reg", null)));
                return d.Count == 5 && !B(d["Found"]) && S(d["Saver"]) == "" && S(d["Product"]) == ""
                    && S(d["Active"]) == "" && I(d["TimeoutSec"]) == 0;
            });
            b.Chk("config：TimeoutSec 坏串与空串都算 0 不抛；Saver 有、Product 缺 ⇒ 真去读 FileVersionInfo，读不到回空串(归因不在这一枚)", delegate
            {
                Dictionary<string, object> bad = Row(Round.ScreensaverConfig(In("reg", In("Saver", "SFAKE.scr", "TimeoutSec", "abc"))));
                Dictionary<string, object> blank = Row(Round.ScreensaverConfig(In("reg", In("Saver", "SFAKE.scr", "TimeoutSec", ""))));
                Dictionary<string, object> live = Row(Round.ScreensaverConfig(In("reg", In("Saver", @"C:\fake\gd-selftest-none.scr"))));
                return I(bad["TimeoutSec"]) == 0 && B(bad["Found"]) && S(bad["Active"]) == "" && S(bad["Product"]) == ""
                    && I(blank["TimeoutSec"]) == 0
                    && B(live["Found"]) && S(live["Saver"]) == @"C:\fake\gd-selftest-none.scr" && S(live["Product"]) == "";
            });
            b.Chk("config：不喂 reg ⇒ 真走 OpenSubKey 那一路(只读注册表)，这里只钉形状自洽、**不钉本机值**", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverConfig(In()));
                List<string> keys = new List<string>(d.Keys);
                if (keys.Count != 5 || keys[0] != "Found" || !(d["Found"] is bool)) return false;
                if (B(d["Found"])) return I(d["TimeoutSec"]) >= 0 && S(d["Saver"]) != "(null)" && S(d["Active"]) != "(null)";
                return S(d["Saver"]) == "" && S(d["Product"]) == "" && S(d["Active"]) == "" && I(d["TimeoutSec"]) == 0;
            });
            b.Chk("config：regPath 指到一枚开不了的键 ⇒ 现读那一路的确定负例(Found=false、四枚空/0)，与\"读到稳态\"分成两档", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverConfig(In("regPath", @"Control Panel\Gd-SelfTest-No-Such-Key")));
                return d.Count == 5 && !B(d["Found"]) && S(d["Saver"]) == "" && S(d["Product"]) == ""
                    && S(d["Active"]) == "" && I(d["TimeoutSec"]) == 0;
            });

            // ---------- disable-screensaver-for-round(Enter-GdNoSleep 的 keepScreenOn 半段，该真身已删·丙档落码刀①) ----------
            b.Chk("关屏保：active=true ⇒ 三枚同序 Read/WasActive/Disabled=true、true、true(键序＝转接层当年写进 res 那枚)", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverOff(In("active", true)));
                List<string> keys = new List<string>(d.Keys);
                return keys.Count == 3 && keys[0] == "Read" && keys[1] == "WasActive" && keys[2] == "Disabled"
                    && B(d["Read"]) && B(d["WasActive"]) && B(d["Disabled"]);
            });
            b.Chk("关屏保：active=true＋setOk=false ⇒ Disabled=false(SPI 关不动那一档；段 8 那句\"没关掉\"的话术靠它，判据不在这里)", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverOff(In("active", true, "setOk", false)));
                return B(d["Read"]) && B(d["WasActive"]) && !B(d["Disabled"]);
            });
            b.Chk("关屏保：active=false＋setOk=true ⇒ WasActive=false、Disabled=false —— 负对照：把 Set 挪到判据之前就会答成 true", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverOff(In("active", false, "setOk", true)));
                return B(d["Read"]) && !B(d["WasActive"]) && !B(d["Disabled"]);
            });
            b.Chk("关屏保：active 写了 null ⇒ Read=false、三枚全 false、不抛(SPI 读不到那一档；与\"读到没开\"是两个读数)", delegate
            {
                Dictionary<string, object> d = Row(Round.ScreensaverOff(In("active", null)));
                return d.Count == 3 && !B(d["Read"]) && !B(d["WasActive"]) && !B(d["Disabled"]);
            });
            b.Chk("关屏保：stateDir 里那本 screensaver.restore 被清掉(旧机制的账)，且回执不受它影响", delegate
            {
                string restore = Path.Combine(svDir, "screensaver.restore");
                File.WriteAllText(restore, "{\"Keep\":1}");
                Dictionary<string, object> d = Row(Round.ScreensaverOff(In("active", true, "stateDir", svDir)));
                return !File.Exists(restore) && B(d["Read"]) && B(d["Disabled"]);
            });
            b.Chk("关屏保：清旧机制排在 SPI **之前** —— active=false 那一档也要把 restore 账本删掉(挪到早退之后就是旧机制残留)", delegate
            {
                string restore2 = Path.Combine(svDir, "screensaver.restore");
                File.WriteAllText(restore2, "{\"Keep\":1}");
                Dictionary<string, object> d = Row(Round.ScreensaverOff(In("active", false, "stateDir", svDir)));
                return !File.Exists(restore2) && B(d["Read"]) && !B(d["WasActive"]) && !B(d["Disabled"]);
            });

            // ---------- append-round-history(原 PS 侧 Add-GdHistory，common.ps1 旧 712-740；该真身已于 11-07 刀⑰ 笔(c)-1 整枚出仓，本族格自此只钉核侧一份) ----------
            string histFile = Path.Combine(histDir, "history.csv");
            b.Chk("history 首建：无 BOM＋表头逐字＋CRLF 行尾、六字段带引号，回执只有 Written 一枚(核内那一臂据此折成裸 true)", delegate
            {
                if (File.Exists(histFile)) File.Delete(histFile);
                Dictionary<string, object> d = Row(Round.RoundHistory(In("stateDir", histDir,
                    "nowText", "2026-10-17 06:00:00", "id", "ark", "mode", "daily", "status", "ok",
                    "durationSec", 120.0, "detail", "首行")));
                string[] rows = CsvRows(histFile);
                string raw = ReadRaw(histFile);
                return d.Count == 1 && d.ContainsKey("Written") && B(d["Written"])
                    && !StartsBom(histFile) && raw.EndsWith("\r\n", StringComparison.Ordinal) && CrlfOnly(raw)
                    && rows.Length == 2
                    && rows[0] == "\"time\",\"game\",\"mode\",\"status\",\"duration_sec\",\"detail\""
                    && rows[1] == "\"2026-10-17 06:00:00\",\"ark\",\"daily\",\"ok\",\"120\",\"首行\"";
            });
            b.Chk("history 续写：表头不重复、共 3 行，第二行的时间用的是**第二发**喂的 nowText(真身每写一行现取一次 Get-Date)", delegate
            {
                Round.RoundHistory(In("stateDir", histDir, "nowText", "2026-10-17 08:30:00", "id", "r1999",
                    "mode", "event", "status", "partial", "durationSec", 7.0, "detail", "次行"));
                string[] rows = CsvRows(histFile);
                return rows.Length == 3
                    && rows[1].StartsWith("\"2026-10-17 06:00:00\",", StringComparison.Ordinal)
                    && rows[2] == "\"2026-10-17 08:30:00\",\"r1999\",\"event\",\"partial\",\"7\",\"次行\"";
            });
            b.Chk("history 那把舍入尺：1.5→2、2.5→2([math]::Round 的中点取偶)、-0.0→\"0\"(存量按 5.1 那支写的)、缺 durationSec→空字段", delegate
            {
                Round.RoundHistory(In("stateDir", roundDir, "nowText", "T", "id", "ark", "status", "ok", "durationSec", 1.5));
                Round.RoundHistory(In("stateDir", roundDir, "nowText", "T", "id", "ark", "status", "ok", "durationSec", 2.5));
                Round.RoundHistory(In("stateDir", roundDir, "nowText", "T", "id", "ark", "status", "ok", "durationSec", -0.0));
                Round.RoundHistory(In("stateDir", roundDir, "nowText", "T", "id", "ark", "status", "ok"));
                string[] rows = CsvRows(Path.Combine(roundDir, "history.csv"));
                return rows.Length == 5
                    && rows[1].EndsWith("\"ok\",\"2\",\"\"", StringComparison.Ordinal)
                    && rows[2].EndsWith("\"ok\",\"2\",\"\"", StringComparison.Ordinal)
                    && rows[3].EndsWith("\"ok\",\"0\",\"\"", StringComparison.Ordinal)
                    && rows[4].EndsWith("\"ok\",\"\",\"\"", StringComparison.Ordinal);
            });
            b.Chk("history 的 detail：CRLF/LF 折成 \" | \"、**单独的 CR 不折**(实测两引擎同形)、字段里的引号翻倍", delegate
            {
                Round.RoundHistory(In("stateDir", flatDir, "nowText", "T", "id", "ark", "status", "ok",
                    "detail", "a\r\nb\nc\rd\"e"));
                string[] rows = CsvRows(Path.Combine(flatDir, "history.csv"));
                return rows.Length == 2 && rows[1] == "\"T\",\"ark\",\"\",\"ok\",\"\",\"a | b | c\rd\"\"e\"";
            });
            b.Chk("history 不喂 nowText ⇒ verb 现读一次钟(核内那一臂**故意不喂**：ctx 的 NowText 是起轮快照，喂了就把每行盖成 06:00:00)", delegate
            {
                Round.RoundHistory(In("stateDir", clockDir, "id", "ark", "status", "ok"));
                string[] rows = CsvRows(Path.Combine(clockDir, "history.csv"));
                if (rows.Length != 2) return false;
                string t = rows[1].Split(',')[0].Trim('\"');
                DateTime parsed;
                if (!DateTime.TryParseExact(t, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out parsed)) return false;
                return t.Length == 19 && rows[1].Contains("\"ark\"") && rows[1].EndsWith("\"\",\"\"", StringComparison.Ordinal);
            });
            b.Chk("history 的 stateDir 不存在 ⇒ 当场拦、**不建目录**：走 CLI 退出码 1(通用 catch 那一档，不是前置的 2)，账没写到别处", delegate
            {
                string inPath = Path.Combine(tmp, "hist-bad-in.json");
                string outPath = Path.Combine(tmp, "hist-bad-out.json");
                GdJson.Write(inPath, In("stateDir", ghostDir, "id", "ark", "status", "ok"));
                int code = GdCli.Run("append-round-history", new string[] { "--in=" + inPath, "--out=" + outPath });
                Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                return code == 1 && !Gd.Truthy(Gd.Get(env, "ok"))
                    && S(Gd.Get(env, "error")).Contains("DirectoryNotFoundException")
                    && !Directory.Exists(ghostDir) && !File.Exists(Path.Combine(ghostDir, "history.csv"));
            });

            // ---------- update-ark-event-probe(Update-GdArkEventProbe，ark-decisions.ps1:158-189；PS 原身已于 11-06 转接刀·运行面刀⑯ 出仓，本枚为唯一真身) ----------
            // 回执三键(Written/NoticeLevel/NoticeText)；两枚前置闸与三档"不落地"都不写盘，只有 Valid 那几档写。
            // 档位必须逐字(WARN/OK/WARN)：核内段 9 用 fx_log 转成一行账外日志，`DirectOut.lines` 只会按 INFO 落，
            // 降档就是改话术的面(§11.67 那条收编理由)，所以这里把**档位与文字**并成一格比。
            string probeLeaf = "ark-event-probe.json";
            b.Chk("自证：decision 整枚不给 ⇒ 三枚同序 Written=false＋两枚空串，一枚盘都不落(真身那两句 return $null)", delegate
            {
                string d0 = Path.Combine(probeDir, "no-decision");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d0, "date", "2026-10-17")));
                List<string> keys = new List<string>(r.Keys);
                return keys.Count == 3 && keys[0] == "Written" && keys[1] == "NoticeLevel" && keys[2] == "NoticeText"
                    && !B(r["Written"]) && S(r["NoticeLevel"]) == "" && S(r["NoticeText"]) == ""
                    && !File.Exists(Path.Combine(d0, probeLeaf));
            });
            b.Chk("自证：EventKey 空(活动没开)——第一枚前置闸，与第二枚分开各钉一格", delegate
            {
                string d1 = Path.Combine(probeDir, "no-eventkey");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d1, "date", "2026-10-17",
                    "decision", In("EventKey", "", "StageHint", "YW-8"), "obs", In("SawGuiPlan", true))));
                return !B(r["Written"]) && S(r["NoticeLevel"]) == "" && S(r["NoticeText"]) == ""
                    && !File.Exists(Path.Combine(d1, probeLeaf));
            });
            b.Chk("自证：StageHint 空(判定给不出最后一关)——第二枚前置闸，obs 齐也不许采", delegate
            {
                string d2 = Path.Combine(probeDir, "no-stagehint");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d2, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", ""),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { "YW-8" }, "Selected", "YW-8"))));
                return !B(r["Written"]) && S(r["NoticeLevel"]) == "" && S(r["NoticeText"]) == ""
                    && !File.Exists(Path.Combine(d2, probeLeaf));
            });
            b.Chk("自证：obs 写了 null ⇒ WARN＋那句\"没有关卡开放观测\"、不落盘(采集段没做起来 ≠ 没打通)", delegate
            {
                string d3 = Path.Combine(probeDir, "obs-null");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d3, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"), "obs", null)));
                return !B(r["Written"]) && S(r["NoticeLevel"]) == "WARN"
                    && S(r["NoticeText"]) == "活动图自证：本轮没有关卡开放观测(采集段没做起来) → 这条结论不落地，下轮照常再试"
                    && !File.Exists(Path.Combine(d3, probeLeaf));
            });
            b.Chk("自证：SawGuiPlan=false ⇒ WARN＋那句\"没抄出关卡名单\"、不落盘(理智早跳或没连上，都不算未解锁)", delegate
            {
                string d4 = Path.Combine(probeDir, "no-plan");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d4, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", false, "Survivors", new object[] { "YW-8" }, "Selected", "YW-8"))));
                return !B(r["Written"]) && S(r["NoticeLevel"]) == "WARN"
                    && S(r["NoticeText"]) == "活动图自证：本轮 MAA 没抄出关卡名单(GetFightStage 那行没出现，多半理智更早跳过或没连上) → 判不了，这条结论不落地"
                    && !File.Exists(Path.Combine(d4, probeLeaf));
            });
            b.Chk("自证：Selected==探针关 ⇒ Written=true＋档位 OK、落盘五枚(Date/EventKey/Stage/Entered/Evidence)读回逐字", delegate
            {
                string d5 = Path.Combine(probeDir, "hit");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d5, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { "CE-5", "YW-8" },
                              "Selected", "YW-8", "LastBattle", false))));
                if (B(r["Written"]) != true || S(r["NoticeLevel"]) != "OK") return false;
                string ev = "MAA 的关卡名单里选中了 YW-8(名单共 2 关)";
                if (S(r["NoticeText"]) != "活动图自证：YW-8 进得去 → 下一轮照常按活动图刷(" + ev + ")") return false;
                Dictionary<string, object> disk = Row(GdJson.Read(Path.Combine(d5, probeLeaf)));
                if (disk == null) return false;
                List<string> keys = new List<string>(disk.Keys);
                return keys.Count == 5 && keys[0] == "Date" && keys[1] == "EventKey" && keys[2] == "Stage"
                    && keys[3] == "Entered" && keys[4] == "Evidence"
                    && S(Gd.Get(disk, "Date")) == "2026-10-17" && S(Gd.Get(disk, "EventKey")) == "fcyx"
                    && S(Gd.Get(disk, "Stage")) == "YW-8" && B(Gd.Get(disk, "Entered"))
                    && S(Gd.Get(disk, "Evidence")) == ev;
            });
            b.Chk("自证：名单含但未选中 ⇒ 仍算进得去(OK)，Evidence 走\"名单含…当前选中…\"那一句", delegate
            {
                string d6 = Path.Combine(probeDir, "in-list");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d6, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { "CE-5", "YW-8" },
                              "Selected", "CE-5", "LastBattle", false))));
                string ev = "MAA 的关卡名单含 YW-8(名单共 2 关，当前选中 CE-5)";
                return B(r["Written"]) && S(r["NoticeLevel"]) == "OK"
                    && S(r["NoticeText"]) == "活动图自证：YW-8 进得去 → 下一轮照常按活动图刷(" + ev + ")"
                    && S(Gd.Get(Row(GdJson.Read(Path.Combine(d6, probeLeaf))), "Evidence")) == ev;
            });
            b.Chk("自证：名单被剪空 ⇒ Written=true(这条结论落地)、档位 WARN、Entered=false 落盘", delegate
            {
                string d7 = Path.Combine(probeDir, "empty-list");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d7, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { }, "Selected", "", "LastBattle", false))));
                string ev = "MAA 本轮的关卡名单被剪空(from [\"\"]) → YW-8 进不去";
                Dictionary<string, object> disk = Row(GdJson.Read(Path.Combine(d7, probeLeaf)));
                return B(r["Written"]) && S(r["NoticeLevel"]) == "WARN"
                    && S(r["NoticeText"]) == "活动图自证：YW-8 进不去 → 判为还没打通，当日剩余轮改刷长草(" + ev + ")"
                    && disk != null && !B(Gd.Get(disk, "Entered")) && S(Gd.Get(disk, "Evidence")) == ev;
            });
            b.Chk("自证：名单非空但没这一关 ⇒ WARN，Evidence 里那串名单按\"、\"拼、并带上当前选中", delegate
            {
                string d8 = Path.Combine(probeDir, "miss");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d8, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { "CE-5", "1-7" },
                              "Selected", "CE-5", "LastBattle", false))));
                string ev = "MAA 名单里 2 关没有 YW-8(名单：CE-5、1-7，当前选中 CE-5)";
                return B(r["Written"]) && S(r["NoticeLevel"]) == "WARN"
                    && S(r["NoticeText"]) == "活动图自证：YW-8 进不去 → 判为还没打通，当日剩余轮改刷长草(" + ev + ")";
            });
            b.Chk("自证：LastBattle=true ⇒ Evidence 末尾那句\"回到上次作战重放\"跟着走(档位不变，只多这半句)", delegate
            {
                string d9 = Path.Combine(probeDir, "replay");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", d9, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { "YW-8" },
                              "Selected", "YW-8", "LastBattle", true))));
                string ev = "MAA 的关卡名单里选中了 YW-8(名单共 1 关)；注意本轮内核走的是「回到上次作战」重放";
                return B(r["Written"]) && S(r["NoticeLevel"]) == "OK"
                    && S(r["NoticeText"]) == "活动图自证：YW-8 进得去 → 下一轮照常按活动图刷(" + ev + ")";
            });
            b.Chk("自证：date 不喂 ⇒ verb 现读 yyyy-MM-dd(核内那一臂只喂 decision/obs/stateDir，日期不在核的手里)", delegate
            {
                string da = Path.Combine(probeDir, "now-date");
                Dictionary<string, object> r = Row(RoundData.ArkEventProbeUpdate(In("stateDir", da,
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { "YW-8" }, "Selected", "YW-8"))));
                string disk = S(Gd.Get(Row(GdJson.Read(Path.Combine(da, probeLeaf))), "Date"));
                DateTime parsed;
                return B(r["Written"]) && disk.Length == 10
                    && DateTime.TryParseExact(disk, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                             DateTimeStyles.None, out parsed);
            });

            // ---------- CLI 接线：这四枚真的挂上了分发链(名册两处同批的自证) ----------
            b.Chk("config 与关屏保走 --in/--out：退出码 0、信封 verb 对、回执键数分别是 5 与 3(核内四臂就按这个挑数)", delegate
            {
                string cIn = Path.Combine(tmp, "sv-cfg-in.json");
                string cOut = Path.Combine(tmp, "sv-cfg-out.json");
                string oIn = Path.Combine(tmp, "sv-off-in.json");
                string oOut = Path.Combine(tmp, "sv-off-out.json");
                GdJson.Write(cIn, In("reg", In("Saver", "SFAKE.scr", "Active", "1", "TimeoutSec", "300", "Product", "FakeProduct")));
                GdJson.Write(oIn, In("active", false, "stateDir", svDir));
                int c1 = GdCli.Run("read-screensaver-config", new string[] { "--in=" + cIn, "--out=" + cOut });
                Dictionary<string, object> e1 = GdJson.ReadEnvelope(cOut);
                Dictionary<string, object> d1 = Row(Gd.Get(e1, "data"));
                int c2 = GdCli.Run("disable-screensaver-for-round", new string[] { "--in=" + oIn, "--out=" + oOut });
                Dictionary<string, object> e2 = GdJson.ReadEnvelope(oOut);
                Dictionary<string, object> d2 = Row(Gd.Get(e2, "data"));
                return c1 == 0 && c2 == 0 && Gd.Truthy(Gd.Get(e1, "ok")) && Gd.Truthy(Gd.Get(e2, "ok"))
                    && S(Gd.Get(e1, "verb")) == "read-screensaver-config"
                    && S(Gd.Get(e2, "verb")) == "disable-screensaver-for-round"
                    && d1 != null && d1.Count == 5 && I(d1["TimeoutSec"]) == 300
                    && d2 != null && d2.Count == 3 && B(d2["Read"]) && !B(d2["WasActive"]);
            });
            b.Chk("history 与自证走 --in/--out：退出码 0、data 形状(1 枚 / 3 枚)、JSON 往返后那把舍入尺仍旧答 2", delegate
            {
                string hIn = Path.Combine(tmp, "hist-cli-in.json");
                string hOut = Path.Combine(tmp, "hist-cli-out.json");
                string pIn = Path.Combine(tmp, "probe-cli-in.json");
                string pOut = Path.Combine(tmp, "probe-cli-out.json");
                string cliHist = Path.Combine(tmp, "hist-cli");
                string cliProbe = Path.Combine(tmp, "probe-cli");
                Directory.CreateDirectory(cliHist);
                GdJson.Write(hIn, In("stateDir", cliHist, "nowText", "2026-10-17 09:00:00", "id", "ark",
                    "mode", "daily", "status", "ok", "durationSec", 1.5, "detail", "cli"));
                GdJson.Write(pIn, In("stateDir", cliProbe, "date", "2026-10-17",
                    "decision", In("EventKey", "fcyx", "StageHint", "YW-8"),
                    "obs", In("SawGuiPlan", true, "Survivors", new object[] { "YW-8" }, "Selected", "YW-8")));
                int c1 = GdCli.Run("append-round-history", new string[] { "--in=" + hIn, "--out=" + hOut });
                Dictionary<string, object> e1 = GdJson.ReadEnvelope(hOut);
                Dictionary<string, object> d1 = Row(Gd.Get(e1, "data"));
                int c2 = GdCli.Run("update-ark-event-probe", new string[] { "--in=" + pIn, "--out=" + pOut });
                Dictionary<string, object> e2 = GdJson.ReadEnvelope(pOut);
                Dictionary<string, object> d2 = Row(Gd.Get(e2, "data"));
                string[] rows = CsvRows(Path.Combine(cliHist, "history.csv"));
                return c1 == 0 && c2 == 0 && d1 != null && d1.Count == 1 && B(d1["Written"])
                    && rows.Length == 2 && rows[1].EndsWith("\"ok\",\"2\",\"cli\"", StringComparison.Ordinal)
                    && d2 != null && d2.Count == 3 && B(d2["Written"]) && S(d2["NoticeLevel"]) == "OK"
                    && S(Gd.Get(e2, "verb")) == "update-ark-event-probe";
            });
            b.Chk("名册第二处(usage 长串)随行自证：未知子命令那句报错里必须同时数到 R9-a 这四枚", delegate
            {
                string outPath = Path.Combine(tmp, "usage3-out.json");
                int code = GdCli.Run("no-such-verb", new string[] { "--out=" + outPath });
                string err = S(Gd.Get(GdJson.ReadEnvelope(outPath), "error"));
                return code == 1 && err.Contains("read-screensaver-config") && err.Contains("disable-screensaver-for-round")
                    && err.Contains("append-round-history") && err.Contains("update-ark-event-probe");
            });

            // ==================== 33) build-round-ctx(刀⑪)：轮 ctx 的 C# 直身 ====================
            // 期望名册逐字抄自 lib\rust-round.ps1 的 Get-GdRoundCtx／Get-GdRoundMaaRunPayload／
            // Get-GdRoundM9aRunPayload(三枚真身随 11-06 刀⑪ 笔(c) 出仓，这半句按历史引文读)。换核那一步的**同帧等值证据**
            // 在 ship 账本 raw\k948-cut11-compare.log＋raw\k948-cut11-parity-ps51.log(STRUCT-DIFFS=0／ORDER-EQ=True／
            // BYTE-EQ=True／SHA-EQ=True／FAILS=0；首拍 k941 那本在册不覆写)，那把尺自己的牙由 raw\k947-cut11-selftest-mutation-report.txt 十七发 mutation 自证；本节往后钉的是"换完核不许漂移"，不再依赖 PS 原身。
            Dictionary<string, object> ctxFx = CtxSettings(null);
            b.Chk("CLI 端到端：build-round-ctx 真挂上分发链，信封 verb/ok 自证＋data 顶层 15 枚键逐枚点名(声明序即协议)", delegate
            {
                string ci = Path.Combine(tmp, "ctx-cli-in.json");
                string co = Path.Combine(tmp, "ctx-cli-out.json");
                GdJson.Write(ci, CtxBody(ctxFx));
                int code = GdCli.Run("build-round-ctx", new string[] { "--in=" + ci, "--out=" + co });
                Dictionary<string, object> env = GdJson.ReadEnvelope(co);
                return code == 0 && B(Gd.Get(env, "ok"))
                    && S(Gd.Get(env, "verb")) == "build-round-ctx"
                    && KeysOf(Gd.Get(env, "data")) == "ids,gates,opt,force,dryRun,skipCleanup,noElevate,"
                        + "nameField,noticeOwner,tag,logLeaf,logPath,pid,maaRun,m9aRun";
            });
            b.Chk("名册第二处(usage 长串)随行自证：未知子命令那句报错里数得到 build-round-ctx", delegate
            {
                string uo = Path.Combine(tmp, "ctx-usage-out.json");
                int code = GdCli.Run("no-such-verb", new string[] { "--out=" + uo });
                string err = S(Gd.Get(GdJson.ReadEnvelope(uo), "error"));
                return code == 1 && err.Contains("build-round-ctx");
            });
            b.Chk("ids 两枚原样到；gates 空项静默滤＋大小写两形都读得到，落账只有小写两键 {id,verb}", delegate
            {
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                object[] gs = Gd.List(d["gates"]);
                Dictionary<string, object> g0 = Row(gs[0]);
                Dictionary<string, object> g1 = Row(gs[1]);
                return Len(d["ids"]) == 2 && S(Gd.List(d["ids"])[0]) == "ark"
                    && gs.Length == 2
                    && KeysOf(g0) == "id,verb" && S(g0["id"]) == "farm" && S(g0["verb"]) == "Invoke-GdArkFarm"
                    && KeysOf(g1) == "id,verb" && S(g1["id"]) == "shop" && S(g1["verb"]) == "Invoke-GdArkShopReset";
            });
            b.Chk("opt 原样透传：值不 stringify(chip 仍是数 3、note 仍是串)；缺 opt 给空对象不抛", delegate
            {
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                Dictionary<string, object> op = Row(d["opt"]);
                Dictionary<string, object> bare = CtxBody(ctxFx);
                bare.Remove("opt");
                Dictionary<string, object> d2 = Row(RoundHost.BuildCtx(bare));
                return op != null && KeysOf(op) == "chip,note" && !(op["chip"] is string)
                    && Convert.ToInt32(op["chip"], CultureInfo.InvariantCulture) == 3
                    && S(op["note"]) == "ctx-cell"
                    && d2 != null && Cnt(d2["opt"]) == 0;
            });
            b.Chk("pid 只认壳写的 ownerPid；缺 ownerPid ⇒ 0 且不抛(本件不补闸，那句 Err 住在核内 round_ctx_parse)", delegate
            {
                object d = RoundHost.BuildCtx(CtxBody(ctxFx));
                Dictionary<string, object> bare = CtxBody(ctxFx);
                bare.Remove("ownerPid");
                object e = RoundHost.BuildCtx(bare);
                return I(Key(d, "pid")) == 4242 && I(Key(e, "pid")) == 0;
            });
            b.Chk("三枚缺省照真身：nameField=Id／noticeOwner=每日／tag=manual", delegate
            {
                Dictionary<string, object> bare = CtxBody(ctxFx);
                bare.Remove("nameField");
                bare.Remove("noticeOwner");
                bare.Remove("tag");
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(bare));
                return S(d["nameField"]) == "Id" && S(d["noticeOwner"]) == "每日" && S(d["tag"]) == "manual";
            });
            b.Chk("logLeaf 五相位(实测眼镜，非推断)：普通／尾分隔符裁掉／盘符根不抛且回 'C:'／裸分隔符回空串／空串回空串不抛"
                + "(分岔照登：Split-Path -Leaf 对 'C:\\' 与 '\\' 两形都回 'C:\\'，真身那句 if ($logFile) 只挡空串)", delegate
            {
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                Dictionary<string, object> t1 = CtxBody(ctxFx);
                t1["logPath"] = @"T:\logs\run-ctx.log\";
                Dictionary<string, object> t2 = CtxBody(ctxFx);
                t2["logPath"] = @"C:\";
                Dictionary<string, object> t3 = CtxBody(ctxFx);
                t3["logPath"] = @"\";
                Dictionary<string, object> t4 = CtxBody(ctxFx);
                t4["logPath"] = "";
                return S(d["logLeaf"]) == "run-ctx.log" && S(d["logPath"]) == @"T:\logs\run-ctx.log"
                    && S(Key(RoundHost.BuildCtx(t1), "logLeaf")) == "run-ctx.log"
                    && S(Key(RoundHost.BuildCtx(t2), "logLeaf")) == "C:"
                    && S(Key(RoundHost.BuildCtx(t3), "logLeaf")) == ""
                    && S(Key(RoundHost.BuildCtx(t4), "logLeaf")) == "";
            });
            b.Chk("maaRun.paths 六枚名册与值逐枚点名", delegate
            {
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                Dictionary<string, object> p = Row(Key(d, "maaRun"));
                Dictionary<string, object> pp = Row(p["paths"]);
                return KeysOf(pp) == "maaDir,maaExe,maaConfig,maaGuiLog,maaAsstLog,arknightsExe"
                    && S(pp["maaDir"]) == @"T:\maa" && S(pp["maaExe"]) == @"T:\maa\MAA.exe"
                    && S(pp["maaConfig"]) == @"T:\maa\config.json" && S(pp["maaGuiLog"]) == @"T:\maa\gui.log"
                    && S(pp["maaAsstLog"]) == @"T:\maa\asst.log" && S(pp["arknightsExe"]) == @"T:\ark\Arknights.exe";
            });
            b.Chk("maaRun.runtime 七枚名册：串 \"8\" 走 [int] 折成 8、串 \"false\" 走 [bool] 折成 false、benign 逐枚 stringify", delegate
            {
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                Dictionary<string, object> rt = Row(Key(d, "maaRun"));
                Dictionary<string, object> rr = Row(rt["runtime"]);
                object[] benign = Gd.List(rr["benignMaaErrors"]);
                return KeysOf(rr) == "arkStartTimeoutMinutes,arkTimeoutMinutes,stallTimeoutMinutes,"
                        + "idleConfirmSeconds,abortOnMaaStallWarning,visionMaxRescue,benignMaaErrors"
                    && I(rr["arkStartTimeoutMinutes"]) == 10 && I(rr["arkTimeoutMinutes"]) == 45
                    && I(rr["stallTimeoutMinutes"]) == 8 && I(rr["idleConfirmSeconds"]) == 90
                    && rr["abortOnMaaStallWarning"] is bool && !(bool)rr["abortOnMaaStallWarning"]
                    && I(rr["visionMaxRescue"]) == 3
                    && benign.Length == 2 && S(benign[0]) == "warn line" && S(benign[1]) == @"C:\a b.exe";
            });
            b.Chk("m9aRun 五段名册＋conn.serial 与 instance 两枚恒空(回落档，段 5 就地覆盖)＋package 取 re1999", delegate
            {
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                Dictionary<string, object> m = Row(d["m9aRun"]);
                Dictionary<string, object> mp = Row(m["paths"]);
                Dictionary<string, object> mc = Row(m["conn"]);
                Dictionary<string, object> mi = Row(m["instance"]);
                return KeysOf(m) == "paths,conn,instance,package,runtime"
                    && KeysOf(mp) == "m9aDir,m9aExe,m9aLogDir" && S(mp["m9aDir"]) == @"T:\m9a"
                    && S(mp["m9aExe"]) == "m9a.exe" && S(mp["m9aLogDir"]) == @"T:\m9a\logs"
                    && KeysOf(mc) == "adb,serial" && S(mc["serial"]) == ""
                    && KeysOf(mi) == "id,name" && S(mi["id"]) == "" && S(mi["name"]) == ""
                    && S(m["package"]) == "com.example.m9a";
            });
            b.Chk("m9aRun.runtime 六枚名册＋m9aCliArgs 三态：settings 没写这枚⇒键不在场(6 枚)；写 false⇒在场且 false(7 枚)；写串 true⇒折成布尔真", delegate
            {
                Dictionary<string, object> d6 = Row(RoundHost.BuildCtx(CtxBody(CtxSettings(null))));
                Dictionary<string, object> d7 = Row(RoundHost.BuildCtx(CtxBody(CtxSettings(false))));
                Dictionary<string, object> d8 = Row(RoundHost.BuildCtx(CtxBody(CtxSettings("true"))));
                Dictionary<string, object> a = Row(d6["m9aRun"]);
                Dictionary<string, object> b7 = Row(d7["m9aRun"]);
                Dictionary<string, object> b8 = Row(d8["m9aRun"]);
                Dictionary<string, object> r6 = Row(a["runtime"]);
                Dictionary<string, object> r7 = Row(b7["runtime"]);
                Dictionary<string, object> r8 = Row(b8["runtime"]);
                return KeysOf(r6) == "m9aInstanceArg,m9aForceStart,visionMaxRescue,r1999StartTimeoutMinutes,"
                        + "r1999TimeoutMinutes,stallTimeoutMinutes"
                    && !r6.ContainsKey("m9aCliArgs") && Cnt(r6) == 6
                    && Cnt(r7) == 7 && r7["m9aCliArgs"] is bool && !(bool)r7["m9aCliArgs"]
                    && Cnt(r8) == 7 && r8["m9aCliArgs"] is bool && (bool)r8["m9aCliArgs"]
                    && S(r6["m9aInstanceArg"]) == "--instance" && B(r6["m9aForceStart"])
                    && I(r6["r1999StartTimeoutMinutes"]) == 12 && I(r6["r1999TimeoutMinutes"]) == 60
                    && I(r6["stallTimeoutMinutes"]) == 8;
            });
            b.Chk("adb 回落档与装载面无关(11-16 摘除已退役的在位闸，两相位改判同值)：临时树没有 lib\\mumu.ps1 照样出数，塞一枚进去出的还是同一个数", delegate
            {
                string mumuPs = Path.Combine(tmp, @"lib\mumu.ps1");
                // 第一相位按生产形态来：先把这枚文件清掉(11-07 刀⑯ 起它在真树上本就不存在)，
                // 断言回落档仍然出数——这一发钉的就是这次连班 error 的那道恒假闸，它回来本格就红。
                // 第二相位再手工塞一枚进临时树(收尾删掉，不落仓内)，证明摘闸没换成反向的另一道闸。
                // GdEnv.Init(null) 的幂等那档仍在这里生效：Root 是临时树，本件读的正是这枚 tmp\lib\mumu.ps1。
                try { if (File.Exists(mumuPs)) File.Delete(mumuPs); } catch { }
                Dictionary<string, object> e = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                Dictionary<string, object> em = Row(Key(e, "m9aRun"));
                Dictionary<string, object> econn = Row(em["conn"]);
                string withoutFile = S(econn["adb"]);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(mumuPs));
                    File.WriteAllText(mumuPs, "#");
                }
                catch { }
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(ctxFx)));
                Dictionary<string, object> mc = Row(Key(d, "m9aRun"));
                Dictionary<string, object> conn = Row(mc["conn"]);
                string withFile = S(conn["adb"]);
                try { if (File.Exists(mumuPs)) File.Delete(mumuPs); } catch { }
                return withoutFile == @"T:\mumu\nx_main\adb.exe" && withFile == withoutFile;
            });
            b.Chk("整块 settings 绝不进 ctx：runtime.visionApiKey 的名字与哨兵值在序列化回执里都搜不到", delegate
            {
                string txt = GdJson.Serialize(RoundHost.BuildCtx(CtxBody(ctxFx)));
                return !txt.Contains("visionApiKey") && !txt.Contains(KeySentinel);
            });
            b.Chk("守门：不喂 settings 夹具就现读临时树那份(故意不建) ⇒ 当场抛 FileNotFoundException，不静默给空 ctx", delegate
            {
                return Thrown<FileNotFoundException>(delegate
                {
                    Dictionary<string, object> bare = CtxBody(ctxFx);
                    bare.Remove("settings");
                    return RoundHost.BuildCtx(bare);
                });
            });
            b.Chk("[int] 的原样怪癖：银行家舍入(2.5→2／3.5→4)；认不出的串抛出去，经 CLI 信封是 ok=false＋原话(话术不因换核分岔)", delegate
            {
                Dictionary<string, object> fx = CtxSettings(null);
                Dictionary<string, object> rp = Row(Gd.Get(fx, "runtime"));
                rp["arkTimeoutMinutes"] = "2.5";
                rp["idleConfirmSeconds"] = "3.5";
                Dictionary<string, object> d = Row(RoundHost.BuildCtx(CtxBody(fx)));
                Dictionary<string, object> rr = Row(Key(Row(d["maaRun"]), "runtime"));
                Dictionary<string, object> bad = CtxSettings(null);
                Row(Gd.Get(bad, "runtime"))["arkTimeoutMinutes"] = "abc";
                bool threw = Thrown<FormatException>(delegate { return RoundHost.BuildCtx(CtxBody(bad)); });
                string bi = Path.Combine(tmp, "ctx-bad-in.json");
                string bo = Path.Combine(tmp, "ctx-bad-out.json");
                GdJson.Write(bi, CtxBody(bad));
                GdCli.Run("build-round-ctx", new string[] { "--in=" + bi, "--out=" + bo });
                Dictionary<string, object> env = GdJson.ReadEnvelope(bo);
                return I(rr["arkTimeoutMinutes"]) == 2 && I(rr["idleConfirmSeconds"]) == 4 && threw
                    && !B(Gd.Get(env, "ok")) && S(Gd.Get(env, "error")).Contains("System.Int32");
            });

            // ==================== 34) 领空折图与 IgnoreShopDone(刀㉑ 笔(b)-2：test-ark t7 那两枚行为钉的对面) ====================
            // 真身＝Round.Data.cs::ComposeDecisionInputs 那四行(igRaw→ignoreShopDone→三元折图→emptyMap 回落)。
            // 判定层(Decisions.Ark)只吃**折好的表**，所以「-IgnoreShopDone 可回看原结论」与「开关置 false 后不再拦截」
            // 这两枚行为钉的对面落在本节，不在 --dectest——dectest 里那张表是入参，折图发生在那之前。
            b.Chk("盘上有领空账 ⇒ ignoreShopDone=true 交空表(回看原结论的那半折在核里，不在壳里)", delegate
            {
                GdJson.Write(shopArk, In("done", In("zztest-event", "2026-10-13 06:05")));
                object d = RoundData.ComposeDecisionInputs(
                    In("game", "ark", "settings", In("ark", In()), "ignoreShopDone", true));
                return Cnt(Key(d, "ShopDone")) == 0 && Key(d, "ShopDone") != null;
            });
            b.Chk("同一枚盘账、入键缺席 ⇒ 照旧交盘上读数(缺省 false＝旧调用方逐字不变，Rust 核臂两发只传 game 走的正是这一路)", delegate
            {
                GdJson.Write(shopArk, In("done", In("zztest-event", "2026-10-13 06:05")));
                object d = RoundData.ComposeDecisionInputs(In("game", "ark", "settings", In("ark", In())));
                Dictionary<string, object> m = Row(Key(d, "ShopDone"));
                return Cnt(Key(d, "ShopDone")) == 1 && m != null && S(m["zztest-event"]) == "2026-10-13 06:05";
            });
            b.Chk("ignoreShopDone 与 shopDoneStopsEvent 两轴同向才都空表：true＋开关 false⇒空；false＋开关 false⇒也空(开关轴没被新键顶掉)", delegate
            {
                GdJson.Write(shopArk, In("done", In("zztest-event", "2026-10-13 06:05")));
                object both = RoundData.ComposeDecisionInputs(In("game", "ark",
                    "settings", In("ark", In("shopDoneStopsEvent", false)), "ignoreShopDone", true));
                object onlyFlag = RoundData.ComposeDecisionInputs(In("game", "ark",
                    "settings", In("ark", In("shopDoneStopsEvent", false)), "ignoreShopDone", false));
                return Cnt(Key(both, "ShopDone")) == 0 && Cnt(Key(onlyFlag, "ShopDone")) == 0;
            });
            b.Chk("入键宽松口径只有两档：布尔原样认、字符串不分大小写等于 true 才算真(\"TRUE\"⇒真；\"1\"／数字 1⇒假)", delegate
            {
                GdJson.Write(shopArk, In("done", In("zztest-event", "2026-10-13 06:05")));
                object up = RoundData.ComposeDecisionInputs(
                    In("game", "ark", "settings", In("ark", In()), "ignoreShopDone", "TRUE"));
                object one = RoundData.ComposeDecisionInputs(
                    In("game", "ark", "settings", In("ark", In()), "ignoreShopDone", "1"));
                object num = RoundData.ComposeDecisionInputs(
                    In("game", "ark", "settings", In("ark", In()), "ignoreShopDone", 1));
                return Cnt(Key(up, "ShopDone")) == 0 && Cnt(Key(one, "ShopDone")) == 1 && Cnt(Key(num, "ShopDone")) == 1;
            });

            // ==================== 35) 领空折图的 r1999 对偶(刀㉓ 笔(a)：test-r1999 [t7] 那三枚行为钉的对面) ====================
            // 折图真身与上面四格是**同一处**四行(Round.Data.cs::ComposeDecisionInputs)，差别只在三件事：
            // game id 走 "r1999"、盘本叫 r1999-shop-done.json(ShopDoneFile 按 id 拼)、settings 块叫 re1999(BlockName 的第二路)。
            // 入键宽松口径那种与 game 无关的形制不在此重复立(它量的是 igRaw 那三行，两棵同一条路)。
            b.Chk("r1999 盘上有领空账 ⇒ ignoreShopDone=true 交空表(-IgnoreShopDone 可回看原结论那一枚的对面)", delegate
            {
                GdJson.Write(shopR1999, In("done", In("9.9-combat", "2026-10-13 06:05")));
                object d = RoundData.ComposeDecisionInputs(
                    In("game", "r1999", "settings", In("re1999", In()), "ignoreShopDone", true));
                return Cnt(Key(d, "ShopDone")) == 0 && Key(d, "ShopDone") != null;
            });
            b.Chk("r1999 同一枚盘账、入键缺席 ⇒ 照旧交盘上读数(缺省 false＝旧调用方逐字不变)", delegate
            {
                GdJson.Write(shopR1999, In("done", In("9.9-combat", "2026-10-13 06:05")));
                object d = RoundData.ComposeDecisionInputs(In("game", "r1999", "settings", In("re1999", In())));
                Dictionary<string, object> m = Row(Key(d, "ShopDone"));
                return Cnt(Key(d, "ShopDone")) == 1 && m != null && S(m["9.9-combat"]) == "2026-10-13 06:05";
            });
            b.Chk("r1999 开关轴没被新键顶掉：re1999.shopDoneStopsEvent=false ⇒ 入键缺省也交空表", delegate
            {
                GdJson.Write(shopR1999, In("done", In("9.9-combat", "2026-10-13 06:05")));
                object off = RoundData.ComposeDecisionInputs(In("game", "r1999",
                    "settings", In("re1999", In("shopDoneStopsEvent", false))));
                object on = RoundData.ComposeDecisionInputs(In("game", "r1999",
                    "settings", In("re1999", In("shopDoneStopsEvent", true))));
                return Cnt(Key(off, "ShopDone")) == 0 && Cnt(Key(on, "ShopDone")) == 1;
            });

            // 收尾：临时树整目录删掉(纪律＝门/自检的临时件不落仓内)
            try { if (File.Exists(lockFile)) File.Delete(lockFile); } catch { }
            try { Directory.Delete(tmp, true); }
            catch (Exception ex) { GdLog.Line("--roundtest 没能清掉临时树 " + tmp + "：" + ex.Message, "WARN"); }

            int rc = b.Result("--roundtest");
            if (rc == 0)
                Console.WriteLine("结论: PASS(②-a 那七枚 + ②-b1 那六枚 + ②-b2 这十枚 + R9-a 收编那四枚的话术/算术/夹具闸/落盘字节形状都对；提权实弹、真 webhook/气泡/真任务/真杀进程、真打活动接口与真 settings/真 state 盘，"
                    + "以及 ②-b2 的 needs-click 真点击、真起端游/等窗口、asst.log 尾 16MB 本体、takeover 那 4 秒、两枚回读不一致抛、认不出相位那一档，"
                    + "R9-a 另有三样：真 SPI 两发(含真把屏保关掉那一发)、真注册表那本屏保账的真读数、真 history.csv 存量续写 —— 仍登记 unobserved)");
            return rc;
        }
    }
}
