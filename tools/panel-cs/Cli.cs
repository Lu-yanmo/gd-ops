// tools/panel-cs/Cli.cs —— verb 子命令入口(无头、绝不 Show 窗体)。契约全文见 docs\ps-migration-batch0.md §2。
// ⚠ 名册第二处(C-0′起)：verb 的**逐枚名册与枚数**住在 Cli.Verbs.cs(`GdCliVerbs`)，那句"未知子命令"的报错由它生成，
//   `Cli.SelfTest` 的 --coretest 里三格钉死(枚数／名册⊆分派链／分派链⊆名册)。下面那段批次流水只是**历史说明**，
//   不再是名册本身 —— 加一枚 verb ＝分派链与 GdCliVerbs.Names 同批改，漏一改当场红(此前十五枚在报错里只写通配或计数)。
// 批次 0：echo(契约样板)。批次 1：decide-ark / decide-r1999 / check-stages / r1999-instance —— PS 转发壳把
// 判定所需的一切**值**组装成 JSON 送 --in，这边只出结论、不读配置不写盘。
// 批次 2：detect-installs / maa-queue-lines / m9a-instance-lines —— 探测与状态行要读现场(盘/注册表/配置文件)，
// 读的动作也搬了进来：detect 的 roots 可由壳注入(golden 用假树)，两个 *-lines 只按路径读、不写任何东西。
// 批次 3：外部进程层(maa/m9a/mumu)的纯解析/纯规划十件(见 Processes.cs 头注的切分口径)——
// parse-asst-* / plan-maa-queue / m9a-launch-args / parse-m9a-lines / resolve-m9a-status / parse-mumu-* / parse-adb-*。
// 批次 4：视觉兜底层的纯规则段八件(见 Vision.cs 头注)——build-vision-prompt / parse-vision-verdict /
// vision-click-point / resolve-vision-endpoint / shot-changed / parse-vision-reply / rescue-entry /
// compose-ark-desktop-state。截图、点击、起子进程、兜底编排与整个 vision-mumu(定义文本被自检钉死)留 PS。
// 批次 5：编排/排程层的纯规则段七件(见 Orchestration.cs 头注)——parse-farm-target-spec / parse-ark-drop-text /
// resolve-ark-farm-plan / apply-ark-daily-intent / apply-r1999-daily-intent / parse-maa-gui-lines / split-task-args。
// 起进程、监控循环、写文件仍全在 PS 壳；plan-maa-queue 出的 NewConfig 由壳决定写不写。
// 批次 6：监督 agent 纯判定段四件(见 Supervisor.cs 头注)——eval-supervisor-settings / parse-supervisor-state /
// resolve-supervisor-action / compose-supervisor-detail。采集/问画面/重启三个副作用函数与读写盘、起停进程留 PS。
// 批次 7：活段监控循环的 tick 判据(见 RunLoops.cs 头注，口径 docs\ps-migration-live-caliber.md)——
// decide-maa-run-tick 一个 verb：壳每 tick 采读数(now/live/flags/cfg)喂进来，拿回"继续/早退/收尾"+
// 同 tick 挂起的副作用指令(grab/rescue/record)。while+sleep、挂钟、读盘、真兜底真抢前台、记账、收尾时长全留 PS 壳。
// 批次 8：活段第二批(见 RunLoops.cs / Orchestration.cs 头注)——decide-m9a-run-tick 一个 verb：m9a 监控循环的
// 反应段判据搬进 RunLoops.cs；ark/r1999 两份 $retryable 分流搬进 Orchestration.cs(decide-daily-retry)。
// 批次 9：两路监控循环的"事件→状态"回灌段(见 RunLoops.cs 对应段头注)——apply-maa-gui-events /
// finish-maa-run / apply-m9a-events 三个 verb：事件列表+旧状态进来，新状态+日志出去；
// Get-Date/Sort-Object/hashtable 重建这些现场量仍由壳做，Register-M9AEvent 与回灌 switch 只剩转发壳。
// 批次 10(收官)：common.ps1 三段从未搬过的小纯规则——resolve-screensaver-who / resolve-screensaver-verdict
// 两个 verb(见 Decisions.cs 屏保段：注册表读数由壳注入，屏保根因文案在这边拼)与 build-relaunch-args
// (见 Processes.cs 提权段：-Force/-Omit/$Bound 分类全部由壳组装成值进来，只出参数数组)。
// ⚠ 转接刀·屏保稳态族(2026-11-03，docs\rust-core-plan.md §11.215)改口：上面那句"注册表读数由壳注入"与这里的
// Get-GdScreensaverConfig 留壳都已作废——屏保那三枚真身已出仓，注册表读数改由 read-screensaver-config 现答；
// Get-GdElevateBatchLine(四行小拼接)仍在场。
// B-2b(2026-10-08)：视觉兜底的**现场件**七枚(见 Live.cs 头注，切分 docs\rust-core-plan.md §11.34)——
// resolve-rescue-context / ark-find-window / ark-window-shot / ark-window-click / ark-grab-foreground /
// ark-desktop-state / vision-ask-model。这批和上面十批不一样：**它们要碰这台机器**(user32 拍窗口、枚举进程、
// System.Drawing、起 Python 子进程)，Rust 核只做编排。API key 只在这七枚内部取值并写进子进程环境变量，
// 不进命令行/请求文件/日志/信封，更不进核。兜底编排、rescue.json 落盘、黑屏之后的重试仍在那一侧(B-2b③)。
//
// 三通道铁律：
//   * 机读结果 **只走 --out 文件**(winexe 的 stdout 从控制台启动本来就不可靠，见 Panel.cs 头注)；
//   * 退出码 = 结论摘要：0 完成 / 1 我们自己的代码或参数错了 / 2 前置不满足这一轮没跑(Stop-GdBeforeRun 同型) /
//     3 预留给批次 1 的"环境行但不该干"(确认门/拒绝)；
//   * stdout 只是给人看的，AttachConsole 之后才真的可见 —— 双击启动没父控制台，attach 自然失败，忽略。
// 参数规矩：只认 `--键=值` 单 token(值含空格/中文都不拆)，`--键`(无 =)当开关；结构化输入走 --in 文件不走 stdin。
// 信封永远写出：连内部异常也要落成 ok:false + error 再退 —— PS 转发壳只面对"文件在/不在"两种情况。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GdPanel
{
    static class GdCli
    {
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int dwProcessType);
        const int ATTACH_PARENT_PROCESS = -1;

        // 参数/契约类错误(调用方的锅)：退出码 1；配置前置类(环境的锅)：GdPrecondition → 2。
        sealed class GdUsageException : Exception
        {
            public GdUsageException(string msg) : base(msg) { }
        }
        sealed class GdPreconditionException : Exception
        {
            public GdPreconditionException(string msg) : base(msg) { }
        }

        sealed class VerbResult
        {
            public bool Ok = true;
            public int Exit = 0;
            public object Data = null;
            public string Error = null;
        }

        public static void TryAttachConsole()
        {
            try
            {
                if (!AttachConsole(ATTACH_PARENT_PROCESS)) return;
                UTF8Encoding enc = new UTF8Encoding(false);
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), enc) { AutoFlush = true });
                Console.SetError(new StreamWriter(Console.OpenStandardError(), enc) { AutoFlush = true });
                try { Console.OutputEncoding = enc; } catch { }
            }
            catch { }
        }

        // Panel.cs 的 Main 看到 argv[0] 不带 -- 就进来，跑完直接 Environment.Exit —— 中间不碰 WinForms。
        public static int Run(string verb, string[] tail)
        {
            TryAttachConsole();
            GdEnv.Init(null);
            Dictionary<string, string> a = null;
            VerbResult r = new VerbResult();
            try
            {
                a = Parse(tail);
                if (Has(a, "--transcript")) GdLog.StartCli(verb);
                if (verb == "echo") r = Echo(a);
                else if (verb == "decide-ark") r = Decide(a, Decisions.Ark);
                else if (verb == "decide-r1999") r = Decide(a, Decisions.R1999);
                else if (verb == "check-stages") r = Decide(a, Decisions.CheckStages);
                else if (verb == "r1999-instance") r = Decide(a, Decisions.R1999Instance);
                else if (verb == "resolve-screensaver-who") r = Decide(a, Decisions.ScreensaverWhoV);
                else if (verb == "resolve-screensaver-verdict") r = Decide(a, Decisions.ScreensaverVerdict);
                else if (verb == "build-relaunch-args") r = Decide(a, Processes.RelaunchArgs);
                else if (verb == "detect-installs") r = Decide(a, Detect.Installs);
                else if (verb == "maa-queue-lines") r = Decide(a, Detect.MaaQueueLines);
                else if (verb == "m9a-instance-lines") r = Decide(a, Detect.M9aInstanceLines);
                else if (verb == "parse-asst-state") r = Decide(a, Processes.AsstState);
                else if (verb == "parse-asst-hint") r = Decide(a, Processes.AsstHint);
                else if (verb == "plan-maa-queue") r = Decide(a, Processes.PlanMaaQueue);
                else if (verb == "m9a-launch-args") r = Decide(a, Processes.M9aLaunchArgs);
                else if (verb == "parse-m9a-lines") r = Decide(a, Processes.M9aLines);
                else if (verb == "resolve-m9a-status") r = Decide(a, Processes.M9aStatus);
                else if (verb == "parse-mumu-state") r = Decide(a, Processes.MumuState);
                else if (verb == "parse-mumu-vms") r = Decide(a, Processes.MumuVms);
                else if (verb == "parse-adb-devices") r = Decide(a, Processes.AdbDevices);
                else if (verb == "parse-adb-pidof") r = Decide(a, Processes.AdbPidof);
                else if (verb == "build-vision-prompt") r = Decide(a, Vision.Prompt);
                else if (verb == "parse-vision-verdict") r = Decide(a, Vision.Verdict);
                else if (verb == "vision-click-point") r = Decide(a, Vision.ClickPoint);
                else if (verb == "resolve-vision-endpoint") r = Decide(a, Vision.Endpoint);
                else if (verb == "shot-changed") r = Decide(a, Vision.ShotChanged);
                else if (verb == "parse-vision-reply") r = Decide(a, Vision.Reply);
                else if (verb == "rescue-entry") r = Decide(a, Vision.Entry);
                else if (verb == "compose-ark-desktop-state") r = Decide(a, Vision.DesktopState);
                else if (verb == "parse-farm-target-spec") r = Decide(a, Orchestration.FarmTargetSpec);
                else if (verb == "parse-ark-drop-text") r = Decide(a, Orchestration.FarmDropText);
                else if (verb == "resolve-ark-farm-plan") r = Decide(a, Orchestration.FarmPlan);
                else if (verb == "apply-ark-daily-intent") r = Decide(a, Orchestration.ArkIntent);
                else if (verb == "apply-r1999-daily-intent") r = Decide(a, Orchestration.R1999Intent);
                else if (verb == "parse-maa-gui-lines") r = Decide(a, Orchestration.MaaGuiLines);
                else if (verb == "split-task-args") r = Decide(a, Orchestration.TaskArgs);
                else if (verb == "eval-supervisor-settings") r = Decide(a, Supervisor.Settings);
                else if (verb == "parse-supervisor-state") r = Decide(a, Supervisor.State);
                else if (verb == "resolve-supervisor-action") r = Decide(a, Supervisor.Action);
                else if (verb == "compose-supervisor-detail") r = Decide(a, Supervisor.Detail);
                else if (verb == "decide-maa-run-tick") r = Decide(a, RunLoops.MaaTick);
                else if (verb == "decide-m9a-run-tick") r = Decide(a, RunLoops.M9aTick);
                else if (verb == "decide-daily-retry") r = Decide(a, Orchestration.DailyRetry);
                else if (verb == "apply-maa-gui-events") r = Decide(a, RunLoops.MaaGuiEvents);
                else if (verb == "finish-maa-run") r = Decide(a, RunLoops.MaaFinish);
                else if (verb == "apply-m9a-events") r = Decide(a, RunLoops.M9aEvents);
                else if (verb == "detect-maa-fight-skip") r = Decide(a, Orchestration.MaaFightSkip);
                else if (verb == "observe-maa-stage-open") r = Decide(a, Orchestration.MaaStageObserve);
                else if (verb == "decide-ark-event-probe") r = Decide(a, Orchestration.ArkEventProbe);
                else if (verb == "resolve-rescue-context") r = Decide(a, Live.RescueContext);
                else if (verb == "ark-find-window") r = Decide(a, Live.ArkFindWindow);
                else if (verb == "ark-window-shot") r = Decide(a, Live.WindowShot);
                else if (verb == "ark-window-click") r = Decide(a, Live.WindowClick);
                else if (verb == "ark-grab-foreground") r = Decide(a, Live.ArkGrab);
                else if (verb == "ark-desktop-state") r = Decide(a, Live.ArkDesktopState);
                else if (verb == "vision-ask-model") r = Decide(a, Live.VisionAsk);
                else if (verb == "check-ark-launcher") r = Decide(a, Live.ArkLauncherCheck);
                else if (verb == "adb-connect") r = Decide(a, Live.AdbConnect);
                else if (verb == "adb-game-pid") r = Decide(a, Live.AdbGamePid);
                else if (verb == "mumu-screen-shot") r = Decide(a, Live.MuMuShot);
                else if (verb == "mumu-screen-click") r = Decide(a, Live.MuMuClick);
                else if (verb == "resolve-round-context") r = Decide(a, Round.Context);
                else if (verb == "start-elevated-round") r = Decide(a, Round.Elevate);
                else if (verb == "send-round-notice") r = Decide(a, Round.Notice);
                else if (verb == "start-run-lock") r = Decide(a, Round.LockStart);
                else if (verb == "stop-run-lock") r = Decide(a, Round.LockStop);
                else if (verb == "read-task-state") r = Decide(a, Round.TaskState);
                else if (verb == "stop-process-tree") r = Decide(a, Round.ProcTree);
                else if (verb == "read-ark-activity") r = Decide(a, RoundData.ArkActivity);
                else if (verb == "read-r1999-activity") r = Decide(a, RoundData.R1999Activity);
                else if (verb == "read-decision-state") r = Decide(a, RoundData.DecisionState);
                else if (verb == "compose-decision-inputs") r = Decide(a, RoundData.ComposeDecisionInputs);
                else if (verb == "resolve-ark-farm-decision") r = Decide(a, Chain.ResolveArkFarmDecision);
                else if (verb == "set-ark-shop-state") r = Decide(a, RoundData.ArkShopState);
                else if (verb == "set-r1999-ack") r = Decide(a, RoundData.R1999Ack);
                else if (verb == "set-r1999-shop-state") r = Decide(a, RoundData.R1999ShopState);
                else if (verb == "takeover-processes") r = Decide(a, Chain.Takeover);
                else if (verb == "stop-maa-and-game") r = Decide(a, Chain.StopMaaAndGame);
                else if (verb == "stop-m9a") r = Decide(a, Chain.StopM9A);
                else if (verb == "read-maa-fight-skip") r = Decide(a, Chain.ReadFightSkip);
                else if (verb == "read-maa-stage-observation") r = Decide(a, Chain.ReadStageObservation);
                else if (verb == "start-ark-client") r = Decide(a, Chain.StartArkClient);
                else if (verb == "start-mumu") r = Decide(a, Chain.StartMuMu);
                // ---- 刀①(2026-11-03)：stop-mumu 照 start-mumu 排进链体起进程侧之后，不加任何分支语义 ----
                else if (verb == "stop-mumu") r = Decide(a, Chain.StopMuMu);
                else if (verb == "apply-maa-daily-config") r = Decide(a, Chain.ApplyMaaDailyConfig);
                else if (verb == "apply-m9a-instance") r = Decide(a, Chain.ApplyM9AInstance);
                else if (verb == "update-ark-farm-ledger") r = Decide(a, Chain.UpdateFarmLedger);
                // ---- R9-a(2026-10-17) 轮骨架四格收编新立四枚：位置一律排在既有链**之后**，不改任何旧分支的顺序 ----
                else if (verb == "read-screensaver-config") r = Decide(a, Round.ScreensaverConfig);
                else if (verb == "disable-screensaver-for-round") r = Decide(a, Round.ScreensaverOff);
                else if (verb == "append-round-history") r = Decide(a, Round.RoundHistory);
                else if (verb == "update-ark-event-probe") r = Decide(a, RoundData.ArkEventProbeUpdate);
                // ---- 乙档刀①(2026-10-23)：面板状态摘要的判定面进核 —— 照读 --in(可以是空 {})，摘要只吃盘上四样，不吃输入字段 ----
                else if (verb == "emit-panel-summary") r = PanelSummaryRun(a);
                // ---- 宿主批(监督 agent 的 C# 直身)：这两枚是**宿主侧入口**，不是轮骨架外发名册里的动作 ----
                else if (verb == "start-supervisor") r = StartSupervisor(a);
                else if (verb == "stop-supervisor") r = StopSupervisor(a);
                // ---- supervise-loop 是 start-supervisor 起的那枚子进程自己跑的回路(阻塞到收工)，不是给调用方调的出口 ----
                else if (verb == "supervise-loop") r = SuperviseLoop(a);
                // ---- 转接刀·时间换算族(2026-11-03)：这两枚只做"信封 → 现成本体 → 信封"，规则一律住 Decisions.cs ----
                else if (verb == "convert-utc8-to-ms") r = Decide(a, Decisions.Utc8ToMsV);
                else if (verb == "format-unix-ms") r = Decide(a, Decisions.UnixMsFmtV);
                // ---- 刀②(2026-11-03)：round-preview 两臂直答的两枚新出口，照 stop-mumu 先例排链尾 ----
                //   mumu-state ＝ Get-GdMuMuState 的整枚直身(MuMu 号牌由回执 Index 带)；
                //   read-m9a-connection ＝ 已退役的 Get-M9AConnection 的同形(11-07 刀⑱ 笔(a) PS 真身出仓；探活只读实例自己的 json，appsettings 归 apply-m9a-instance)。
                else if (verb == "mumu-state") r = Decide(a, Chain.MuMuState);
                else if (verb == "read-m9a-connection") r = Decide(a, Chain.ReadM9AConnection);
                // ---- 刀④(2026-11-04)：status 体检链改问核的两枚新出口，照 stop-mumu／mumu-state 先例排链尾 ----
                //   resolve-status-env-rows ＝ 两枚 EnvRows 的名册＋三态判尺直身(染色话术留 PS 壳)；
                //   resolve-status-proc-names ＝ 两枚 ProcNames 的写死名册直身。两枚都不读配置、不写盘。
                else if (verb == "resolve-status-env-rows") r = Decide(a, Detect.StatusEnvRows);
                else if (verb == "resolve-status-proc-names") r = Decide(a, Detect.StatusProcNames);
                // ---- 刀⑧(2026-11-04)：rust-round 外发名册两枚闸臂(updgate／audit)的直身，照先例排链尾 ----
                //   run-m9a-update／run-1999-event-audit：判据(脚本名、-Apply、挑行正则、内层上限)全在 Gate.Host.cs，
                //   入参只有自检夹具三枚旋钮；这两枚是**会起子进程**的出口，与 status 那两枚纯数据形不同档。
                else if (verb == "run-m9a-update") r = Decide(a, GateHost.M9aUpdgate);
                else if (verb == "run-1999-event-audit") r = Decide(a, GateHost.EventAudit);
                // ---- 刀⑪(2026-11-05)：lib\rust-round.ps1 的 ctx 三枚(装配＋maaRun＋m9aRun)直身，照先例排链尾 ----
                //   build-round-ctx：壳给旋钮(ids/gates/opt/四开关/nameField/noticeOwner/tag/logPath/ownerPid)，
                //   settings 由本件现读——整块 settings 不进 body(runtime 里住着 visionApiKey)。判据在 Round.Host.cs。
                else if (verb == "build-round-ctx") r = Decide(a, RoundHost.BuildCtx);
                // ---- 刀⑮ 笔(c)(2026-11-06)：MuMu adb 端点直身露成出口，照先例排链尾 ----
                //   resolve-mumu-adb-endpoint：三源两正则全住 Gate.Host.cs(本枚只是把它露出来，一条规则都不新写)；
                //   settings 现读、整块不进 body，body 旋钮 settings/conn 只给自检夹具。1999-event-audit 的落点轮询自此问核。
                else if (verb == "resolve-mumu-adb-endpoint") r = Decide(a, GateHost.EndpointV);
                // ---- 刀⑰ 笔(b)(2026-11-07)：三枚 PS 真身的对面出口，照先例排链尾(注册三处同批：本链＋Cli.Verbs.cs 名册＋枚数) ----
                //   read-file-tail        ＝ Chain.ReadTail 直身露出(镜像日志按字节偏移续读)，零规则新增；
                //   input-desktop-name    ＝ GdWin.InputDesktopName 直身露出，desk 夹具键照 ArkDesktopState/Live.cs:251 先例；
                //   wait-adb-game-pid     ＝ 单用途形(拒绝通用 wait-condition 重入形)：Chain.WaitUntil × 进程内 Live.AdbGamePid 的 Found；
                //                            三枚全部只复用在场真身，处理器与私有助手同类，不改任何可见性。
                else if (verb == "read-file-tail") r = Decide(a, Chain.ReadFileTail);
                else if (verb == "input-desktop-name") r = Decide(a, Live.InputDesktopNameV);
                else if (verb == "wait-adb-game-pid") r = Decide(a, Chain.WaitAdbGamePid);
                // ---- 刀⑰ 笔(c)-2 第 1 批(2026-11-07)：B1 文本判定对面，照先例排链尾(注册三处同批：本链＋Cli.Verbs.cs 名册＋枚数) ----
                //   scan-source-patterns：门脚本族七成「拿 PS 文本当尺」判据的对面(契约格 §11.276)；
                //   规则正当新增一枚——扫描与判定就是本体的本体，输入闸四枚走拒跑形，PS 门侧只余组参与按格 Check。
                else if (verb == "scan-source-patterns") r = Decide(a, Chain.ScanSourcePatterns);
                // ---- 刀⑰ 笔(c)-2 第 2 批(2026-11-07) B2：清单完备性＋入口开关名对账的对面，照先例排链尾(注册三处同批：本链＋Cli.Verbs.cs 名册＋枚数) ----
                //   check-module-manifests：test-modules [m2][m6] 两段里「拿清单文本＋入口参数表当尺」那十三枚规则的对面(契约格 §11.278，
                //   落码时的四处改判 §11.279)；四键必填＝门侧现问供数(路径键名册/必需钩子名册都在 PS 侧的家)，核内只做读文本＋集合比对，
                //   一张名册都不抄第二份(§11.186 点名的病灶)。唯一进核的表＝Ack 退场名册(原住 test-modules.ps1:306 那句硬编码)。
                else if (verb == "check-module-manifests") r = Decide(a, Chain.CheckModuleManifests);
                // ---- 刀㉔(2026-11-08)：m9a-update 两枚待承接纯规则进核，照先例排链尾(注册三处同批：本链＋Cli.Verbs.cs 名册＋枚数) ----
                //   read-m9a-install    ← Get-GdM9AInstall：只读 settings.paths 与 interface.json 两个键，不动盘；
                //   compare-version-gap ← Test-GdM9AVersionGap：纯比较。两枚都不碰 curl／robocopy 那些现场段(那三枚按表账照旧不许动)。
                else if (verb == "read-m9a-install") r = Decide(a, Chain.ReadM9AInstall);
                else if (verb == "compare-version-gap") r = Decide(a, Chain.CompareVersionGap);
                else throw new GdUsageException("未知子命令：" + verb + "(现有：" + GdCliVerbs.Roster() + "；后续批次见 docs\\ps-migration-batch0.md 与 docs\\ps-migration-live-caliber.md)");
            }
            catch (GdUsageException ex)
            {
                r.Ok = false; r.Exit = 1; r.Error = ex.Message;
            }
            catch (GdPreconditionException ex)
            {
                r.Ok = false; r.Exit = 2; r.Error = ex.Message;
            }
            catch (GdRuleException ex)
            {
                // 判定层的"规则说不干"(如实例找不到)：不是代码 bug，原话给 PS 壳继续 throw。
                r.Ok = false; r.Exit = 2; r.Error = ex.Message;
            }
            catch (FileNotFoundException ex)
            {
                // 批次 2 的状态行 verb 读配置文件缺文件：PS 侧原本就是"红字 + 原话"，
                // 不能走下面的通用 catch 被冠上 "FileNotFoundException: " 前缀 —— 原话进 error，退出码 2(前置缺)。
                r.Ok = false; r.Exit = 2; r.Error = ex.Message;
            }
            catch (Exception ex)
            {
                r.Ok = false; r.Exit = 1; r.Error = ex.GetType().Name + ": " + ex.Message;
            }
            if (r.Error != null) GdLog.Line(r.Error, "ERROR");
            try { WriteOut(a, verb, r); }
            catch (Exception ex)
            {
                // 连结果文件都写不出去(盘坏/路径被占)才是真正没法挽回的内部错误：说清楚，仍是 1。
                GdLog.Line("写结果文件失败：" + ex.Message, "ERROR");
                if (r.Ok) r.Exit = 1;
            }
            return r.Exit;
        }

        static void WriteOut(Dictionary<string, string> a, string verb, VerbResult r)
        {
            string outPath = Get(a, "--out");
            if (outPath == null)
            {
                if (!Has(a, "--no-out")) GdLog.Line("未给 --out：机读结果没处写(契约要求 verb 调用必带 --out)", "WARN");
                return;
            }
            if (Has(a, "--garbage"))
            {
                // --no-out / --garbage 是给 --echotest 和探针用的"故意违约"模拟旋钮(§5)：
                // 违约必须能被调用方识别出来，识别不出来才是契约没立住。
                string dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outPath, "{这不是合法的 JSON", new UTF8Encoding(false));
                return;
            }
            if (Has(a, "--no-out")) return;
            GdJson.Write(outPath, GdJson.Envelope(verb, r.Ok, r.Exit, r.Data, r.Error));
        }

        // ---------- echo：契约样板，不含任何业务判定。--exit=N 模拟结论码，--in 指向一份 JSON。 ----------
        static VerbResult Echo(Dictionary<string, string> a)
        {
            RequireKnown(a, "--in", "--out", "--exit", "--no-out", "--garbage", "--transcript");
            object data = new Dictionary<string, object>();
            string inPath = Get(a, "--in");
            if (inPath != null) data = GdJson.Read(inPath);   // 读不到就抛：那是调用方给的参数错 → 1
            int code = 0;
            string es = Get(a, "--exit");
            if (es != null)
            {
                if (!int.TryParse(es, out code)) throw new GdUsageException("--exit 不是整数：" + es);
            }
            VerbResult r = new VerbResult();
            r.Ok = code == 0;
            r.Exit = code;
            r.Data = data;
            r.Error = code == 0 ? null : "(echo) 模拟结论码 " + code;
            return r;
        }

        // ---------- 批次 1 判定 verb 的共同骨架：输入是一份 PS 壳组装好的 JSON(--in)，输出进 data。 ----------
        static VerbResult Decide(Dictionary<string, string> a, Func<Dictionary<string, object>, object> run)
        {
            RequireKnown(a, "--in", "--out", "--no-out", "--garbage", "--transcript");
            string inPath = Get(a, "--in");
            if (inPath == null) throw new GdUsageException("该子命令需要 --in <文件>：判定输入是 PS 转发壳组装的 JSON 对象");
            object input = GdJson.Read(inPath);              // 读不到就抛：那是调用方给的参数错 → 1
            Dictionary<string, object> map = input as Dictionary<string, object>;
            if (map == null) throw new GdUsageException("--in 指向的 JSON 顶层不是对象({})：" + inPath);
            VerbResult r = new VerbResult();
            r.Data = run(map);
            return r;
        }

        // ---------- emit-panel-summary 的骨架：在 Decide 之上多认一枚 --root(换树读数用) ----------
        // 为什么要 --root：编译树里既没有 config\settings.json 也没有 state\history.csv，
        // 只在这一棵树上比 = 只演"读不到"那一档，判定面的其余形状(满档/已确认/带详情)一次都没被对拍过 ——
        // "恒绿的正是没验过的正例面"这条在案纪律要求门能给一枚**别的树**当靶。
        // --root 干的就这一件：把摘要指到另一棵树(现场那棵只读取数)；原等值门靠它让两侧同树，那道门已随件退役。
        // 换的是**读数来源**，不是判据：摘要里 HEADER 第 6 枚会把 root 印出来，自检拿它认"这一帧读的正是那棵树"，
        // 指错了当场红，不会悄悄把两棵树的读数混成一份。
        static VerbResult PanelSummaryRun(Dictionary<string, string> a)
        {
            RequireKnown(a, "--in", "--out", "--no-out", "--garbage", "--transcript", "--root");
            string root = Get(a, "--root");
            if (!string.IsNullOrEmpty(root))
            {
                string full;
                try { full = Path.GetFullPath(root.TrimEnd('\\', '/')); }
                catch { throw new GdUsageException("--root 不是一条能解析的路径：" + root); }
                if (!Directory.Exists(full)) throw new GdPreconditionException("--root 指的目录不在：" + full);
                GdEnv.InitForTest(full);        // 连 VERSION 缓存一起清：读数是那棵树的(和只读自检同一把尺)
                a.Remove("--root");             // 已经用过了，剩下的照 Decide 的老名册判，别让它在下一道闸里被当成未知参数
            }
            return Decide(a, GdPanelSummary.Emit);
        }

        // ---------- 宿主批：监督 agent 的起停（SupervisorHost 的 verb 外衣） ----------
        // 回执形状照 lib\rust-round.ps1 那一臂的 [pscustomobject]@{Available;Err}，键名与两半语义同格：
        //   Available=false ⇒ "开关没开／这一枚根本不在场"（旧版 Get-Command 不在场那一档），不算错；
        //   Err 非空 ⇒ 起/停这一步抛了原文。两枚都不许把 Available 与 Err 混成一枚布尔。
        static VerbResult StartSupervisor(Dictionary<string, string> a)
        {
            RequireKnown(a, "--in", "--out", "--no-out", "--garbage", "--transcript");
            Dictionary<string, object> S = null;
            string games = "all";
            int ownerPid = 0;
            string inPath = Get(a, "--in");
            if (inPath != null)
            {
                object input = GdJson.Read(inPath);
                Dictionary<string, object> map = input as Dictionary<string, object>;
                if (map == null) throw new GdUsageException("--in 指向的 JSON 顶层不是对象({})：" + inPath);
                S = Gd.Map(Gd.Get(map, "settings"));
                string g = Gd.Str(Gd.Get(map, "games"));
                if (g != null && g.Length > 0) games = g;
                ownerPid = Gd.Int(Gd.Get(map, "ownerPid"), 0);
            }
            HostResult hr = SupervisorHost.Start(S, games, ownerPid);
            VerbResult r = new VerbResult();
            r.Data = Gd.Out("Available", hr.Available, "Err", hr.Err == null ? "" : hr.Err);
            return r;
        }

        // supervise-loop：start-supervisor 起的那枚分离子进程的前台入口，阻塞到收工才返回。
        // 退出码直接照退役前 tools\supervise.ps1 的码面(0=正常收工，3=起不来；那件 11-02 刀③ 出仓，码面自此由本 assembly 自己守)，所以这里不复读原话进 Error ——
        // 两档失败的话术 Loop 内部已经按旧版逐字落过日志了，再套一层 "Error" 只会把同一句印两遍。
        // --owner 非整数是参数错(走 GdUsageException，退 1)；owner 没给(<=0)不算错，那一臂不触发。
        static VerbResult SuperviseLoop(Dictionary<string, string> a)
        {
            RequireKnown(a, "--in", "--out", "--no-out", "--garbage", "--transcript",
                         "--games", "--owner", "--once", "--cap");
            int owner = 0;
            string os = Get(a, "--owner");
            if (os != null && os.Length > 0)
            {
                int parsed;
                if (!int.TryParse(os, out parsed))
                    throw new GdUsageException("--owner 不是一个进程号：" + os);
                owner = parsed;
            }
            string games = Get(a, "--games");
            if (games == null || games.Length == 0) games = "all";
            bool once = Has(a, "--once") && !string.Equals(Get(a, "--once"), "false", StringComparison.OrdinalIgnoreCase);
            int cap = SupervisorHost.CapDefault;
            string cs = Get(a, "--cap");
            if (cs != null && cs.Length > 0)
            {
                int parsed;
                if (!int.TryParse(cs, out parsed))
                    throw new GdUsageException("--cap 不是一个分钟数：" + cs);
                cap = parsed;
            }
            int code = SupervisorHost.Loop(owner, games, once, cap);
            VerbResult r = new VerbResult();
            r.Ok = code == 0;
            r.Exit = code;
            return r;
        }

        static VerbResult StopSupervisor(Dictionary<string, string> a)
        {
            RequireKnown(a, "--in", "--out", "--no-out", "--garbage", "--transcript");
            HostResult hr = SupervisorHost.Stop();
            VerbResult r = new VerbResult();
            r.Data = Gd.Out("Available", hr.Available, "Err", hr.Err == null ? "" : hr.Err);
            return r;
        }

        // ---------- 参数工具 ----------
        static Dictionary<string, string> Parse(string[] tail)
        {
            Dictionary<string, string> a = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string t in tail)
            {
                if (t == null || !t.StartsWith("--"))
                    throw new GdUsageException("verb 参数必须以 --键=值 形式给出，收到：" + t);
                int eq = t.IndexOf('=');
                if (eq < 0) a[t] = null;
                else a[t.Substring(0, eq)] = t.Substring(eq + 1);
            }
            return a;
        }

        static void RequireKnown(Dictionary<string, string> a, params string[] known)
        {
            foreach (KeyValuePair<string, string> kv in a)
            {
                bool ok = false;
                foreach (string k in known) if (string.Equals(k, kv.Key, StringComparison.OrdinalIgnoreCase)) { ok = true; break; }
                if (!ok) throw new GdUsageException("未知参数：" + kv.Key + "(这一子命令只认 " + string.Join(" ", known) + ")");
            }
        }

        public static string Get(Dictionary<string, string> a, string k)
        {
            if (a == null) return null;
            string v;
            return a.TryGetValue(k, out v) ? v : null;
        }

        public static bool Has(Dictionary<string, string> a, string k)
        {
            return a != null && a.ContainsKey(k);
        }

        // 2026-10-23 版号轮第二形：无头档要的是"字节按 UTF-8 落到本来就在那里的句柄上"，不是"贴一台控制台"。
        // 上面那枚 TryAttachConsole 的第一件事是 AttachConsole(ATTACH_PARENT_PROCESS)，而一枚 /target:winexe 一旦
        // 贴上父控制台，系统就把它**已经继承来**的标准句柄改挂到那块屏幕缓冲上 —— 上游那根管道或 cmd 重定向出去的
        // 那个文件从此一个字节都收不到(同日现量：& 收流臂与 cmd 重定向臂各 0 字节，只有不经 attach 的
        // Start-Process -RedirectStandardOutput 那一臂拿到 731 字节)。这里不碰控制台：只在这一路确实被重定向时
        // 把 Console.Out/Error 换成不带 BOM 的 UTF-8 写手；真控制台里人肉跑无头档那一臂什么都不做(winexe 默认按
        // 系统 ANSI 写字节、控制台按同一码页渲染，中文本来是对的，去翻它反倒动玩家那台窗口的码页)。verb 分发那条
        // 路仍走 TryAttachConsole —— 那是长期生产行为，机读只认 --out 文件，本批不动它。
        public static void UseUtf8IfRedirected()
        {
            try
            {
                UTF8Encoding enc = new UTF8Encoding(false);
                if (Console.IsOutputRedirected)
                    Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), enc) { AutoFlush = true });
                if (Console.IsErrorRedirected)
                    Console.SetError(new StreamWriter(Console.OpenStandardError(), enc) { AutoFlush = true });
            }
            catch { }
        }
    }
}
