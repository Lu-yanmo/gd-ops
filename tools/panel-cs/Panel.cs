// tools/panel-cs/Panel.cs —— 每日清体力的图形状态面板(C# / WinForms，.NET Framework 4，系统自带 csc.exe 直接编译)。
//
// 判定面的所在(乙档刀②，2026-10-23 换的接线)：不再起子进程问 `panel.ps1 -DumpSummary`，
// 面板摘要那一面读数的真身就是本目录的 Panel.Summary.cs(GdPanelSummary.Build()) —— 本文件不算任何结论，
// 它做的只有这几件事：
//   1. 取一次协议行(PROTO/HEADER/GAME/TIME/LAST/GATE/ENV/PROBE/FLAGS/HELP)，'|' 分隔；
//      第一行 PROTO|<版本> 是这份读数的版本号：不认识时面板要在日志区明说"判定层协议 v几 / 我只认 v几"，
//      而不是让某个格子静默变空 —— 面板只会转达，读不懂必须说出来。
//   2. 把行画成卡片/导航/日志，并把按钮动作转调现成脚本(daily-clear / status / run-via-task)；
//   3. 黑/白双主题切换(唯一的"外观判定"，存在 state\panel-theme.txt)；
//   4. 「实验功能」卡：只碰 runtime 里的三个布尔键(arkVisionRescue / mumuVisionRescue / visionAllowReward) ——
//      写之前先备份到 backups\，用整文件行内替换、别的一个字节不动(键本来没有时在 runtime 段补一行)；
//      用不用视觉兜底、准不准它点"领取"，仍由 lib\vision-rescue.ps1 读同一批键决定。
//      卡上那颗「只问不点」只是代跑 tools\vision-ask.ps1(只截屏问模型、不点任何东西)，面板自己不加任何判定。
//   5. 主控台四页(配置/定时/历史/诊断)：把原来只能敲命令行的日常操作搬进界面。
//      读 = 行内读 settings.json 的那几个键 + 转达摘要读数里已有的 PROBE/TIME 行 + 直读 history.csv；
//      写 = 路径改动代跑 setup.ps1、装卸任务代跑 install-task.ps1、换时间点代跑 reschedule-daily.ps1，
//            只有视觉模型/地址/Key/Webhook 这类标量走行内替换(和 arkVisionRescue 同一套规矩)；
//      一条硬的：**视觉 Key 不进命令行参数**(命令行在别的用户/工具面前是明文)，也不写进日志。
//      面板仍然不加判定："这一路就绪没有""下一个时间点几点"都是脚本说的原话。
// 所以 Panel.Summary.cs 里的措辞或 Kind 判据一改，这里一个字不改也跟着变 —— 不会出现两个面板各说各话。
// (刀② 之前这一句说的是 panel.ps1；面板与 PS 侧最后的对拍面(那道等值门)也已整件退役，处置写在
//  Panel.Summary.cs 的头注里。)
//
// 用法：
//   panel-cs.exe                 打开面板(左侧导航：总览 / 每一路 / 配置 / 定时 / 历史 / 诊断)
//   panel-cs.exe --selftest      不显示窗口：读一次协议、渲染、查布局，有红项退出码 1
//   panel-cs.exe --consoletest   不显示窗口：主控台四页的读写回环(打在临时副本上)、版式、抹 key，全对才 0
//   panel-cs.exe --headless      窗口挪到屏幕外，自动起一次预演，验"子进程→日志区→忙碌复位"这条链后自关(零弹窗)
//   panel-cs.exe --exptest       实验功能读写回环自测：翻一次开关→读回→翻回去→读回，全对才 0(会动 settings.json，只在测试树上跑)
//   panel-cs.exe <verb> --k=v…   无头子命令：批次 0 echo；批次 1 decide-ark/decide-r1999/check-stages/r1999-instance；
//                                批次 2 detect-installs/maa-queue-lines/m9a-instance-lines。
//                                机读结果走 --out 文件，退出码 0/1/2/3=完成/内部错/前置缺/判定拒。
//                                三通道契约见 docs\ps-migration-batch0.md —— PS→C# 迁移期间 PS 转发壳就按这份转调。
//   panel-cs.exe --coretest      不显示窗口：Core 四件套(Core.Env/Json/Log/Settings)喂临时树断言，不要求机器现场状态
//   panel-cs.exe --echotest      不显示窗口：echo 样板的契约回环(信封/退出码/违约识别) + 真子进程走一遍 Main 分发
//   panel-cs.exe --dectest       不显示窗口：批次 1 判定层(Decisions.cs)假数据回环 —— 期望文案逐字抄自旧 PS，红即移植走样
//   panel-cs.exe --statetest     不显示窗口：批次 2 探测/状态行(Detect.cs)假树回环 —— 同上，期望串逐字抄自旧实现
//   panel-cs.exe --proctest      不显示窗口：批次 3 外部进程层(Processes.cs)纯函数回环 —— 同上，不起进程不写文件
//   panel-cs.exe --visiontest    不显示窗口：批次 4 视觉兜底层(Vision.cs)纯规则段回环 —— 同上，不联网不碰窗口
//   panel-cs.exe --orchtest      不显示窗口：批次 5 编排/排程层(Orchestration.cs)纯规则段回环 —— 同上，不起进程不读盘
//   panel-cs.exe --svtest        不显示窗口：批次 6 监督 agent(Supervisor.cs)纯判定段回环 —— 同上，不读盘不起进程
//   panel-cs.exe --looptest      不显示窗口：批次 7 活段判据(RunLoops.cs)tick 剧本回环 —— 同上，读数/回执全由场景直喂
//   panel-cs.exe --agenttest     不显示窗口：对话小窗那条链的纯规则段(Agent.cs) —— 找解释器的顺序、CRT 命令行转义、
//                                信封投影。不起进程、不联网、不要求这台机器装了 Python(喂的是假尺子和手写 JSON)。
//   panel-cs.exe --livetest      不显示窗口：B-2b 现场件(Live.cs)七枚 verb 的纯规则段回环 —— 尺寸闸两分支、
//                                六道闸门话术、请求装配九字段、子进程 stdout 回扫五档、内存位图跑亮度签名、
//                                请求目录裁剪。真窗口与真 HTTP 一律不进门(只读自检不许要求机器现场状态)。
//   panel-cs.exe --roundtest     不显示窗口：B-3②-a 轮骨架现场件(Round.cs)七枚 verb 的纯规则段回环 —— PS 的
//                                `-ne $false` 真值语义、运行锁读侧(判活/判残留/双重转义/own 判据不分大小写)、
//                                轮 ctx 逐枚点名取键(整块 runtime 绝不外泄)、通知三档容错与标题两句、提权拼参。
//                                不弹 UAC、不打 webhook、不起气泡、不真杀进程、不读真计划任务；写盘两枚打在临时树上。
//
// 编译：pwsh -File tools\panel-cs\build.ps1   (用 C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GdPanel
{
    // ---------- 主窗体 ----------
    sealed partial class PanelForm : Form
    {
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        // 无边框窗体：CS_DROPSHADOW 让 Win10/11 给一块淡投影，不然贴屏幕上像张纸
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x00020000; return cp; }
        }

        const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2, WM_NCHITTEST = 0x84;

        const int RailW = 200;
        const int X0 = 212;        // 内容区左边界
        const int CW = 916;        // 内容区宽度
        const int RowY0 = 32, RowH = 18, RowPitch = 20;   // 卡片行几何(和 panel.ps1 一致：132 高正好 5 行)

        readonly string root;
        readonly string engine;
        string entryName = "daily-clear.ps1";   // 由 HEADER 协议行赋值(不能 readonly)
        // -DumpSummary 的协议版本：判定层第一行 PROTO|<n> 报上来的。面板只认 ProtoKnown，
        // 对不上就在日志区说一次(-1 = 判定层压根没给版本行，那是它比这个面板旧)。
        // v2(2026-09-26)：多了 SV 行(监督 agent 那一行 + 它的展开详情)，那一行不再出现在 ENV 里。
        const int ProtoKnown = 2;
        int protoVer = -1;
        int protoReported = -2;    // 只在不认识/版本变了的那一次说话，别每 45 秒刷一遍横幅
        bool flagsGames, flagsViaTask;   // FLAGS 协议行赋值
        readonly List<string[]> games = new List<string[]>();   // [id, 显示名]
        readonly List<string> elevNames = new List<string>();
        readonly List<string[]> helpRows = new List<string[]>(); // [标签, 命令]
        readonly Dictionary<string, List<Row>> timesByPage = new Dictionary<string, List<Row>>();
        List<Row> rowsLast = new List<Row>(), rowsGates = new List<Row>(), rowsEnv = new List<Row>();
        readonly Dictionary<string, List<Row>> probesByGame = new Dictionary<string, List<Row>>();

        Theme th;
        string page = "all";
        bool busy; DateTime confirmAt = DateTime.MinValue, ackAt = DateTime.MinValue;
        string idleRunText = "开始跑一轮(全部)";
        string version = "0.0.0";             // 仓库根 VERSION 的读数(唯一来源是那个文件，这里只缓存)
        // 外部轮次(别的进程/计划任务起的这一轮)：面板既不能当它不存在(会二次点火撞车)，
        // 也不能把它当自己的子进程(停不掉、也不该由面板的退出码来下结论)。
        bool extBusy; string extNote = "";
        string extLog; long extPos;   // 外部那一轮的日志：面板只跟随，不判定成败

        readonly Panel rail = new Panel();
        readonly Panel hdr = new Panel();
        readonly Label pageTitle = new Label();
        readonly Label runChip = new Label();
        readonly Label stampLabel = new Label();
        readonly PictureBox avatar = new PictureBox();
        Label brandTitle, brandSub, foot1, foot2;
        readonly Dictionary<string, Button> navBtns = new Dictionary<string, Button>();
        readonly Dictionary<string, Panel> navStripes = new Dictionary<string, Panel>();
        readonly Dictionary<string, Label> navDots = new Dictionary<string, Label>();
        readonly Panel cardTimes = new Panel(), cardLast = new Panel(), cardGates = new Panel(), cardEnv = new Panel(), cardExp = new Panel();
        readonly Panel act = new Panel();
        readonly Panel logCard = new Panel();
        readonly RichTextBox logBox = new RichTextBox();
        Button runBtn, btnDry, btnStatus, btnRefresh, ackBtn, btnShop, btnLog, btnHelp, btnTheme, expAskBtn;
        // 实验功能卡上的三只勾选(各写一个布尔键) + 那行随状态变色的说明
        CheckBox expArkChk, expMumuChk, expRewardChk;
        Label expHint;
        // "我们要求显示"的那四颗把手 —— 自检只能认这四个字段，认控件的 Visible 会在没显示窗体时全假(见 RefreshCards)
        bool expArkUp, expMumuUp, expRewardUp, expAskUp;
        Button btnOnly;             // 只跑当前页那一路(单游戏仓会自动藏起来)
        DateTime onlyAt = DateTime.MinValue;
        // 「问需求管家」：入口在左栏，窗子是另一只独立小窗(不是主控台的一页 —— 它不转达状态读数，它说话)。
        // 只保一份引用：重复点入口应当把那扇窗叫到眼前，而不是开出第二个对话(两个窗子会各持一份待确认的计划)。
        Button btnAgent;
        AgentChatForm agentWin;
        Button btnMin, btnMax, btnClose;   // 无边框后的自绘"标题栏"三键
        readonly System.Windows.Forms.Timer pump = new System.Windows.Forms.Timer();
        // beat：状态自动刷新。没有它的时候"现在到底跑没跑/跑到哪"只能靠手点刷新，
        // 而日常一轮要跑十几到几十分钟，要盯着的人恰恰最不该去点按钮。
        readonly System.Windows.Forms.Timer beat = new System.Windows.Forms.Timer();
        int beatSec = 45;             // 状态自动刷新的节拍。刀② 之前这一枚 45 秒是「起一个 pwsh 子进程问 panel.ps1 -DumpSummary」的代价折中；接线换进核内(beat 那一趟调 BuildDumpSummary)之后不再有子进程，45 这一档原样留着——它是给人看的刷新粒度，不是电气约束，要动属改体验、另批再拍。
        // --no-beat 关掉自动刷新，只留手点「刷新状态」(自检和极慢机器用)。
        // 走 static 是因为要在构造之前传进来：构造函数的签名已经有 headless 一个参数，
        // 不为这个开关去动它的调用点和重载。
        public static bool AutoBeatDefault = true;
        bool autoBeat = AutoBeatDefault;

        Process child;
        string outPath, errPath, tailPath;
        long tailPos;
        DateTime tailSince;
        int lastExit = int.MinValue;   // 子进程走事件队列读，outPos/errPos 那对文件偏移已经不需要了

        // 两种不进屏幕的验证路径共用
        public string HlResult;
        readonly DateTime hlDeadline;
        // headless 的统计面(2026-10-23 刀② 从 panel.ps1 的 Get-PanelHeadlessStats 逐字转写)：
        // 窗体一 Close，控件里的文本跟着释放，所以这六格必须在关窗前一拍抓下来 —— 和 PS 侧同一个理由。
        bool hlCaptured;
        int hlLines, hlErrLines;
        bool hlChild, hlDone, hlExitLine, hlIdle;
        string[] hlTail = new string[0];

        public PanelForm(bool headless)
        {
            // 定位仓库根：exe 在 <root>\tools\panel-cs\ 下；被复制走(桌面快捷方式等)时退回当前目录。
            // 刀②(2026-10-23)认的锚从 `tools\panel.ps1` 换成仓库根那个 `VERSION`：判定面已进核，
            // panel.ps1 是同批要退役的件 —— 拿一个即将不在场的文件当"这是不是一棵仓库根"的判据，
            // 退役当天就会让面板把自己认成"当前目录"。VERSION 是全工程唯一版本来源，出货 zip 里也带着它，
            // 三棵树形状(源码树／首跑释放出的 app 目录／和仓库摆一起的快捷方式)都成立。
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string guess = Path.GetFullPath(Path.Combine(exeDir, @"..\.."));
            if (File.Exists(Path.Combine(guess, "VERSION"))) root = guess;
            else if (File.Exists(Path.Combine(exeDir, "VERSION"))) root = exeDir;
            else root = Directory.GetCurrentDirectory();

            engine = PickEngine();
            th = LoadTheme();
            version = ReadVersion(root);

            // 版本号来自仓库根那个 VERSION(唯一来源)，标题栏带它是为了"玩家一抬头就知道自己跑的是哪一版"。
            // 窗体是无边框的，这一句主要给任务栏/截图脚本/自检读；看得见的位置在启动日志和诊断页。
            Text = "每日清体力 · 状态面板 · v" + version;
            StartPosition = FormStartPosition.CenterScreen;
            // 无边框：系统白标题栏去掉，标题区由 hdr 自绘(拖拽 + — □ × 三个键)。
            // 左边缘被 rail 子窗口盖住，鼠标命中测试到不了本窗体 → 左边不提供拉宽，用最大化按钮兜住。
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(1140, 730);
            MinimumSize = new Size(1000, 640);
            Font = new Font("Microsoft YaHei UI", 9.5f);
            try
            {
                Icon ic = LoadEmbeddedIcon();
                if (ic != null) Icon = ic;
            }
            catch { }

            BuildUi();
            ApplyTheme();

            string dump;
            try { dump = BuildDumpSummary(); }
            catch (Exception ex)
            {
                games.Add(new[] { "all", "总览" });
                dump = null;
                AppendLog("[ERROR] 读状态失败：" + ex.Message);
            }
            if (dump != null) ParseSummary(dump);
            RebuildNav();
            RefreshCards();
            AppendLog("提示：面板只转达，不替你判定 —— 结论和命令行跑同一套脚本得到的一样。(C# 外壳 v" + version + " · " + (th.Name == "dark" ? "黑" : "白") + "底)");
            HintDetails();

            pump.Interval = 800;
            pump.Tick += OnPumpTick;
            pump.Start();

            // 自动刷新：先把"有没有别处在跑"认一次(否则开机时外部那轮要到 45 秒后才看得见)，
            // 再按 beatSec 一拍一拍走。--no-beat 时只留手点「刷新状态」，行为回到加这个功能之前。
            RefreshExternal();
            beat.Interval = beatSec * 1000;
            beat.Tick += OnBeatTick;
            if (autoBeat) beat.Start();

            if (headless)
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(-16000, -16000);
                ShowInTaskbar = false;
                hlDeadline = DateTime.Now.AddSeconds(150);
                Shown += delegate
                {
                    AppendLog("[headless] 自动验证：起一次预演(dry-run，不会启动任何游戏/助手)");
                    StartTask(entryName, DryRunArgs(), "headless 预演", false);
                };
            }
            else hlDeadline = DateTime.MinValue;
        }


        public int Headless()
        {
            Show();
            Application.Run(this);
            Console.WriteLine("---- panel-cs headless(子进程输出 -> 事件队列 -> 日志区) ----");
            if (!hlCaptured)
            {
                Console.WriteLine("没抓到任何统计：定时器一次都没进回调，这条链直接判失败。");
                Console.WriteLine("结论：        " + (HlResult ?? "TIMEOUT"));
                return 1;
            }
            Console.WriteLine("日志区行数：  " + hlLines);
            Console.WriteLine("看到子进程行：" + hlChild);
            Console.WriteLine("看到结论行：  " + hlDone);
            Console.WriteLine("看到退出码：  " + hlExitLine);
            Console.WriteLine("stderr 行数： " + hlErrLines);
            Console.WriteLine("忙碌已复位：  " + hlIdle);
            Console.WriteLine("结论：        " + (HlResult ?? "TIMEOUT"));
            if (HlResult != "PASS")
            {
                Console.WriteLine("末 20 行日志：");
                foreach (string l in hlTail) Console.WriteLine("  " + l);
                return 1;
            }
            // 和 PS 侧同一条对冲：PASS 的判据里有"看到过子进程的行"，一句没见着就是泵没生效，
            // 这一格不许跟着结论一起绿。
            if (!hlChild) { Console.WriteLine("PASS 但没看到子进程输出：泵没生效，判失败。"); return 1; }
            return 0;
        }

        // 关窗前一拍把六格抓下来(窗体一 Close，logBox 里的文本跟着释放，事后再读只能读到空)。
        // 判据与 panel.ps1 的 Get-PanelHeadlessStats 逐字同一把：三棵树的开场横幅措辞不同
        // (合体=「每日清体力日常开始」，单游戏仓=「方舟日常开始」/「1999 日常开始」)，
        // 认的都是 Write-GdLog 那句 "=== …日常开始(tag=…)" 的共同后缀，不把合体那一份写死。
        void CaptureHeadlessStats()
        {
            string txt = "";
            string[] lines = new string[0];
            try { txt = logBox.Text; lines = logBox.Lines; } catch { }
            hlLines = lines.Length;
            hlChild = Regex.IsMatch(txt, @"日常开始\(tag=");
            hlDone = Regex.IsMatch(txt, @"\[dry-run\] 完成");
            hlExitLine = Regex.IsMatch(txt, @"退出码 \d+");
            int errs = 0;
            foreach (string l in lines) if (l.StartsWith("[stderr")) errs++;
            hlErrLines = errs;
            hlIdle = !busy && runBtn != null && runBtn.Text == idleRunText;
            hlCaptured = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (hlDeadline == DateTime.MinValue) return;
            System.Windows.Forms.Timer check = new System.Windows.Forms.Timer();
            check.Interval = 1000;
            check.Tick += delegate
            {
                string txt = logBox.Text;
                bool ended = txt.Contains("退出码 ") || txt.Contains("没取到退出码");
                if (!busy && txt.Contains("[dry-run] 完成") && ended)
                { CaptureHeadlessStats(); HlResult = "PASS"; check.Stop(); Close(); return; }
                if (DateTime.Now > hlDeadline)
                {
                    AppendLog("[headless] 超时，停止跟随。");
                    CaptureHeadlessStats();
                    string[] all = logBox.Lines;
                    int from = all.Length > 20 ? all.Length - 20 : 0;
                    hlTail = new string[all.Length - from];
                    Array.Copy(all, from, hlTail, 0, hlTail.Length);
                    HlResult = "TIMEOUT";
                    check.Stop();
                    StopChild();
                    Close();
                }
            };
            check.Start();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] argv)
        {
            // 2026-10-23 版号轮：编码这件事得赶在**任何**输出之前处理，而且不能只装在 verb 分发那一条路上。
            // 这一枚是 /target:winexe(见 build.ps1 的编译命令行)，.NET Framework 不给 winexe 查控制台码页 ⇒ Console.OutputEncoding
            // 落到系统 ANSI(本机 cp936)，那十几枚无头档(--selftest/--headless/--coretest…)写的中文全是 GBK 字节，而同一根
            // 管道里 GDLauncher(控制台子系统，共享 pwsh 设的 65001)那段是 UTF-8：一次 stdout、两套编码。build-exe 的冒烟门
            // 用 & 收流、按 UTF-8 解码，「文件都在: True」「布局自检: OK」必然 miss —— 门红而面板内容完好(前后退出码恒 0、
            // 十一行文本一字不差)。处方按在册纪律回修转写体(不改期望、不放宽比对)，同批把 build.ps1 那把读数眼镜从 OEM
            // 码页翻成 UTF-8，否则 Get-GdSelfTestTally 判的那句中文「全绿|有红项」就是一枚空尺。⚠ 首版这里抄的是
            // TryAttachConsole 那三行，错在它的第一件事是 AttachConsole：一枚 winexe 贴上父控制台之后，系统把它**已经继承来**
            // 的标准句柄改挂到那块屏幕缓冲上 ⇒ 上游那根管道、cmd 的那个 > 文件从此一个字节都收不到(同日现量两臂 0 字节，
            // 只有不经 attach 的 Start-Process -Redirect 那一臂拿到 731)。⇒ 无头档只换写手、不贴控制台(见 Cli.cs 那枚
            // UseUtf8IfRedirected)，真控制台里人肉跑 --selftest 那一臂不改(按系统 ANSI 写、同一码页渲染，中文本来是对的)。
            // 射程仍只认**带 -- 旗标的无头档**：verb 分发下面那段在 GdCli.Run 里已装过一次，裸双击是去起窗体、装一次控制台
            // 会把玩家那个 cmd 窗口的码页一起翻掉，不值得为它动别人那台控制台。⇒ 扫 argv。
            for (int ai = 0; ai < argv.Length; ai++)
            {
                if (argv[ai].StartsWith("--")) { GdCli.UseUtf8IfRedirected(); break; }
            }
            // verb 分发(批次 0，契约见 docs\ps-migration-batch0.md)：第一个参数不带 -- 就是子命令，
            // 无头跑完直接退 —— 必须赶在任何 WinForms/PanelForm 之前，绝不碰窗体构造里那一趟 BuildDumpSummary
            // (刀② 之前它是「起 pwsh 子进程问 panel.ps1」，现在它在本进程里读判定面；两者都不该被 verb 分发路过)。
            if (argv.Length > 0 && !string.IsNullOrEmpty(argv[0]) && !argv[0].StartsWith("--"))
            {
                string[] tail = new string[argv.Length - 1];
                Array.Copy(argv, 1, tail, 0, tail.Length);
                Environment.Exit(GdCli.Run(argv[0], tail));
            }
            bool selftest = false, headless = false, dump = false, exptest = false, exttest = false, whytest = false, consoletest = false;
            bool coretest = false, echotest = false, dectest = false, statetest = false, proctest = false, visiontest = false, orchtest = false, svtest = false, looptest = false, agenttest = false, livetest = false, roundtest = false;
            foreach (string a in argv)
            {
                string l = a.ToLowerInvariant();
                if (l == "--selftest") selftest = true;
                if (l == "--headless") headless = true;
                if (l == "--dump") dump = true;
                if (l == "--exptest") exptest = true;
                if (l == "--exttest") exttest = true;
                if (l == "--whytest") whytest = true;
                if (l == "--consoletest") consoletest = true;
                if (l == "--coretest") coretest = true;
                if (l == "--echotest") echotest = true;
                if (l == "--dectest") dectest = true;
                if (l == "--statetest") statetest = true;
                if (l == "--proctest") proctest = true;
                if (l == "--visiontest") visiontest = true;
                if (l == "--orchtest") orchtest = true;
                if (l == "--svtest") svtest = true;
                if (l == "--looptest") looptest = true;
                if (l == "--agenttest") agenttest = true;
                if (l == "--livetest") livetest = true;
                if (l == "--roundtest") roundtest = true;
                if (l == "--no-beat") PanelForm.AutoBeatDefault = false;
            }
            // Core/契约自检同理走无头早退：它们只喂临时树，不需要(也不该)把面板整副构造起来。
            if (coretest) Environment.Exit(GdCliSelfTest.CoreTest());
            if (echotest) Environment.Exit(GdCliSelfTest.EchoTest());
            if (dectest) Environment.Exit(GdDecisionsSelfTest.DecTest());
            if (statetest) Environment.Exit(GdDetectSelfTest.StatTest());
            if (proctest) Environment.Exit(GdProcSelfTest.ProcTest());
            if (visiontest) Environment.Exit(VisionSelfTest.VisionTest());
            if (orchtest) Environment.Exit(GdOrchSelfTest.OrchTest());
            if (svtest) Environment.Exit(GdSvSelfTest.SvTest());
            if (looptest) Environment.Exit(GdLoopSelfTest.LoopTest());
            if (agenttest) Environment.Exit(GdAgentSelfTest.AgentTest());
            if (livetest) Environment.Exit(LiveSelfTest.LiveTest());
            if (roundtest) Environment.Exit(RoundSelfTest.RoundTest());
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            PanelForm f = new PanelForm(headless);
            if (exptest) Environment.Exit(f.ExpTest());
            if (exttest) Environment.Exit(f.ExtTest());
            if (whytest) Environment.Exit(f.WhyTest());
            if (consoletest) Environment.Exit(f.ConsoleTest());
            if (dump) { f.DumpTree(); return; }
            if (selftest) Environment.Exit(f.SelfTest());
            if (headless) Environment.Exit(f.Headless());
            Application.Run(f);
            Console.WriteLine("面板已关闭。定时任务不受影响，到点照样自己跑。");
        }
    }
}