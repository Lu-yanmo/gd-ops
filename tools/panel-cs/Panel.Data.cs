// 读数：取一次摘要协议(刀② 起是核内直取，见本文件 BuildDumpSummary)、按协议行解析(含 PROTO 版本对不上时要说的那句话)，
// 再把 state\history.csv 里判定层写下的原因挂到卡片行上。Row 是这一层的数据结构。
// 这一份是 partial class 的一段；文件级用法/编译/自检动词的说明在 Panel.cs 顶部。

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
    // ---------- 协议行 ----------
    sealed class Row
    {
        public string Kind;   // ok / warn / err / dim / info
        public string Game;   // 空 = 不属于任何一路(分页时永远保留)
        public string Text;
        // 完整原因(来自 state\history.csv 的 detail 列)。非空 = 这一行可以点。
        // 为什么单开一个字段而不是塞进 Text：卡片一行只有 272px，判定层那句几百字的
        // "M9A 连模拟器这一步就失败了…" 会把整张「上一轮」卡撑成省略号，反而什么都看不见。
        public string Detail = "";   // 长文本原因(不参与卡片渲染，点这一行才展开)；没有就是空串
        // 原文出处：点了那一行之后，日志区那一句抬头要说的"这段话是从哪儿抄来的"。
        // 空串 = 走默认那套(「上一轮」的行，出处是 state\history.csv 的 detail 列)；
        // SV 行喂的是 state\supervision.json，抬头不说清楚就会让人去翻历史 CSV 找它。
        public string Source = "";
        public Row(string kind, string game, string text) { Kind = kind; Game = game; Text = text; }
        public Row(string kind, string game, string text, string detail) { Kind = kind; Game = game; Text = text; Detail = detail ?? ""; }
    }

    // ---------- 主窗体(这一份装的成员见本文件头) ----------
    sealed partial class PanelForm : Form
    {
        // ---------- 引擎 / 数据源 ----------
        // engine 为空(=这台机器没有 pwsh 7)时子进程全起不来：这句话给各调用点拼错误消息用。
        const string NoEngineHint = "这台机器上没有 pwsh.exe(PowerShell 7)——本程序只支持 PowerShell 7，请先安装：winget install Microsoft.PowerShell";

        internal static string PickEngine()
        {
            // 面板起的每个子进程都要一个 PowerShell。本仓库只支持 PowerShell 7(2026-10-06 C 轮退役 5.1)：
            // 这里只找 pwsh.exe，全落空了回空串(调用点各自把"没引擎"报成一句人话 NoEngineHint，不再静默回落 5.1)。
            // exe 出货链(A0「零依赖」，2026-10-07)：启动器起本面板前会注入 GD_PWSH=自带引擎的路径 ——
            // 认它一个就够了(玩家机器装没装 PowerShell 与本程序无关)。没有(从源码树直接跑本面板、
            // 或别的入口拉起)才回到下面的老路。命名风格跟着已有的 GD_PYTHON 走。
            string fromEnv = Environment.GetEnvironmentVariable("GD_PWSH");
            if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv)) return fromEnv;
            // 老路：旧版把商店版(WindowsApps 别名)整个跳过 —— 那套是跟着 panel.ps1 的 RunAs 顾虑抄来的；
            // 但面板自己从不 RunAs(全是读状态 / dry-run / 触发计划任务)，而本机实测(2026-10-06)只有商店版，
            // 照跳等于这台机器直接没引擎 —— 所以只保留"安装版优先"，商店版进末档。
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            string storeAlias = null;
            foreach (string dir in path.Split(';'))
            {
                if (dir.Trim().Length == 0) continue;
                string cand;
                try { cand = Path.Combine(dir.Trim(), "pwsh.exe"); } catch { continue; }
                if (!File.Exists(cand)) continue;
                if (cand.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0) { if (storeAlias == null) storeAlias = cand; continue; }
                return cand;   // PATH 里的安装版：正统(和面板 PS 侧的"安装版优先"同口径)
            }
            if (storeAlias == null)
                storeAlias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\pwsh.exe");
            if (File.Exists(storeAlias)) return storeAlias;
            string prog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PowerShell\7\pwsh.exe");
            if (File.Exists(prog)) return prog;
            return "";
        }

        // 刀②(2026-10-23，与乙档刀③ 同批)：这一枚原来起一个 pwsh 子进程跑 `tools\panel.ps1 -DumpSummary`，
        // 现在直取核内 GdPanelSummary.Build() —— 判定面已在刀① 逐字转写进 Panel.Summary.cs(等值门验过两侧同文
        // 后随件退役，现由 --selftest 那七格守着)，而下面 ParseSummary 要的就是那份协议原文，
        // 所以返回类型、解析器、两处调用点的 try/catch 语义一字不动，少的只是那枚子进程(冷启一到两秒)。
        // 更名 Run→Build：留着"Run"会指着一枚不再起的进程。
        // 顺带拆掉的隐患：老代码先 StandardOutput.ReadToEnd() 再 ReadToEnd()  stderr、最后才 WaitForExit(60000) ——
        // 这正是「同族管道雷」那条在册的形状(子进程 stderr 写满 4KB 管道缓冲区就互等，60 秒超时是唯一出口)。
        // 现在这一枚不起进程，也就没有管道；代价是面板第一次在自己的 UI 进程里做 Schedule.Service COM
        // (TIME 行，见 Panel.Summary.cs:368 那段横幅起的取证；锚原写 :374，HEAD 帧横幅在 :367 本就漂 7 格，刀⑦ 笔(a) 那本头注 +1 行推到 :368) —— 这几调用 Round.cs 的 read-task-state 已在 CLI 进程里做过，
        // 全同步、毫秒级；真出现挂住，没有 60 秒超时兜着了，这一条登记为刀② 的已知未验面。
        // engine 为空不再在这条路上报 NoEngineHint：摘要这一路本来就不需要 pwsh —— 它要的引擎名字只是
        // 被转述的那一列读数(见 Panel.Summary.cs 的 HEADER／ENV 两行)。真要引擎的那一路(动作、打包、
        // 提权重启)照旧把 NoEngineHint 说清楚：Panel.Run.cs:57／Panel.Console.cs:446、:560。
        string BuildDumpSummary()
        {
            List<string> lines = GdPanelSummary.Build();
            return string.Join("\n", lines.ToArray());
        }

        void ParseSummary(string dump)
        {
            lastDump = dump;    // 诊断页把这份协议原文当"当前读数"存档，不另取一次
            // 刷新会再解析一遍：这些列表必须先清空，否则"上一轮/门/环境"每刷新一次翻一倍
            games.Clear(); rowsLast.Clear(); rowsGates.Clear(); rowsEnv.Clear();
            probesByGame.Clear(); helpRows.Clear(); timesByPage.Clear(); elevNames.Clear();
            protoVer = -1;
            string[] lines = dump.Replace("\r\n", "\n").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.IndexOf('|') < 0) continue;
                string[] f = line.Split('|');
                switch (f[0])
                {
                    case "PROTO":
                        int v;
                        if (f.Length > 1 && int.TryParse(f[1], out v)) protoVer = v;
                        break;
                    case "HEADER":
                        if (f.Length > 2) entryName = f[2];
                        break;
                    case "GAME":
                        if (f.Length > 2) games.Add(new[] { f[1], f[2] });
                        break;
                    case "TIME": Add(timesByPage, "all", RowOf(f, false)); break;
                    case "LAST": rowsLast.Add(RowOf(f, true)); break;
                    case "GATE": rowsGates.Add(RowOf(f, true)); break;
                    case "ENV": rowsEnv.Add(RowOf(f, false)); break;
                    case "SV":
                        // SV|kind|一行文本|详情行1|详情行2…  (v2)：环境卡里那一行"监督：…"，正文之后的每段是一行展开。
                        // 外壳在这里一个新判定都不加：kind 和那一句都是判定层(tools\panel-cs\Supervisor.cs 现算；旧 PS 判据随 11-02 刀③ 出仓)算好的，
                        // 详情段是它逐拍写下的原文 —— 这里只按固定字段位次取文本、拼详情，然后交给 RenderRows。
                        if (f.Length > 2)
                        {
                            Row sv = RowOf(f, false);            // f[2..] 全拼回文本，先把详情段摘掉
                            StringBuilder sd = new StringBuilder();
                            for (int i = 3; i < f.Length; i++)
                            {
                                if (sd.Length > 0) sd.Append('\n');
                                sd.Append(f[i]);
                            }
                            sv.Text = f[2];
                            sv.Detail = sd.ToString();
                            sv.Source = "state\\supervision.json 的现值与逐拍记录(监督 agent 自己写的原文)";
                            rowsEnv.Add(sv);
                        }
                        break;
                    case "PROBE":
                        if (f.Length > 3)
                        {
                            List<Row> l;
                            if (!probesByGame.TryGetValue(f[2], out l)) { l = new List<Row>(); probesByGame[f[2]] = l; }
                            l.Add(RowOf(f, true));   // PROBE|kind|game|text —— 同 LAST/GATE 的字段位次
                        }
                        break;
                    case "HELP":
                        if (f.Length > 2) helpRows.Add(new[] { f[1], f[2] });
                        break;
                    case "FLAGS":
                        if (f.Length > 4)
                        {
                            ackFlagName = f[1]; shopFlagName = f[2];
                            flagsGames = f[3] == "yes"; flagsViaTask = f[4] == "yes";
                            if (f.Length > 5 && f[5].Length > 0) elevNames.AddRange(f[5].Split(','));
                        }
                        break;
                }
            }
            // 对不上协议就说一次，对得上什么都不说(别每 45 秒刷一条横幅盖住真读数)。
            if (protoVer != protoReported)
            {
                protoReported = protoVer;
                string pw = ProtoMismatch();
                if (pw.Length > 0) AppendLog("协议对不上：" + pw);
            }
            // 结论行到位了再补"为什么"：这一步读的是判定层自己写的 history.csv，不加任何新判断。
            LoadLastDetails();
            AttachDetails();
        }

        // 判定层和这个面板对不上协议时该说的那句话；对得上返回空串。
        // 单拆成一个纯函数，是为了让自测能直接问"这份读数你认不认"，不用去翻日志区里的那一条。
        string ProtoMismatch()
        {
            if (protoVer == ProtoKnown) return "";
            if (protoVer < 0) return "判定层给的读数里没有协议版本号(它比这个面板还旧)，按 v" + ProtoKnown + " 尽力渲染 —— 有格子空着先更新仓库";
            if (protoVer > ProtoKnown) return "判定层协议 v" + protoVer + "，这个面板只认 v" + ProtoKnown + " —— 某些格子可能是空的或错位的，重编一次：tools\\panel-cs\\build.ps1";
            return "判定层协议 v" + protoVer + "，比这个面板认的 v" + ProtoKnown + " 还旧 —— 重编一次：tools\\panel-cs\\build.ps1";
        }

        static Row RowOf(string[] f, bool hasGame)
        {
            // 协议：TAG|kind|[game]|文本 —— kind 恒为 f[1]；正文允许再带 '|'，把字段之后全部拼回去
            string kind = f.Length > 1 ? f[1] : "info";
            string game = "";
            int from = 2;
            if (hasGame && f.Length > 2) { game = f[2]; from = 3; }
            StringBuilder sb = new StringBuilder();
            for (int i = from; i < f.Length; i++) { if (sb.Length > 0) sb.Append('|'); sb.Append(f[i]); }
            return new Row(kind, game, sb.ToString());
        }

        static void Add(Dictionary<string, List<Row>> map, string key, Row r)
        {
            List<Row> l;
            if (!map.TryGetValue(key, out l)) { l = new List<Row>(); map[key] = l; }
            l.Add(r);
        }

        string ackFlagName = "", shopFlagName = "";

        // ---------- 上一轮「为什么」：直接读 state\history.csv 的 detail 列 ----------
        // 这不是第二个判定源。detail 那一列本来就是 lib\ark-daily.ps1 / lib\r1999-daily.ps1 收尾时写的原文，
        // PS 面板和 status.ps1 念的是同一句；外壳只是把它从文件里捞出来给人看。
        // 为什么不顺手让它走 -DumpSummary：LAST 行的协议是 kind|game|一行文本，卡片只有 272px 宽，
        // 判定层动辄两三百字的结论塞进去，整张卡就剩省略号了(2026-09-25 那次 blocked 就是这么被吞掉的)。
        string historyPathOverride;          // --whytest 用它喂一份合成 CSV，不碰真历史
        string HistoryPath { get { return string.IsNullOrEmpty(historyPathOverride) ? Path.Combine(root, @"state\history.csv") : historyPathOverride; } }
        readonly Dictionary<string, string> detailByGame = new Dictionary<string, string>();

        void LoadLastDetails()
        {
            detailByGame.Clear();
            List<string[]> recs;
            try { recs = ParseCsv(ReadAllShared(HistoryPath)); }
            catch { return; }    // 没有历史文件/读不动：详情列全空，卡片照旧只报结论，不影响别的卡
            if (recs.Count < 2) return;
            string[] head = recs[0];
            int gi = CsvCol(head, "game"), di = CsvCol(head, "detail");
            if (gi < 0 || di < 0) return;    // 表头对不上(老格式/别的仓) → 宁可不显示，也不按猜的列位读
            for (int i = 1; i < recs.Count; i++)
            {
                string[] r = recs[i];
                if (r.Length <= gi || r.Length <= di) continue;
                string g = r[gi].Trim();
                if (g.Length == 0) continue;
                // 空 detail 也要写进去(覆盖)：留着更早一轮的原因，比"这一轮没说原因"更容易骗人。
                detailByGame[g] = r[di].Trim();
            }
        }

        // 把 detail 挂到「上一轮」那些红/黄行上。判据只有 kind：kind 由核内那一份判定面算好(Panel.Summary.cs 的 BuildDumpSummary)，外壳不另判一次 —— 刀② 之前这一句写的是 panel.ps1。
        void AttachDetails()
        {
            if (detailByGame.Count == 0) return;
            for (int i = 0; i < rowsLast.Count; i++)
            {
                Row r = rowsLast[i];
                if (r.Detail.Length > 0) continue;
                if (r.Kind != "err" && r.Kind != "warn") continue;
                string d;
                if (detailByGame.TryGetValue(r.Game, out d) && d.Length > 0) r.Detail = d;
            }
        }

        internal static string ReadAllShared(string path)
        {
            // 跑着的那轮结束时会往 history.csv 追加，独占读会直接抛 —— 必须 FileShare.ReadWrite。
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, Encoding.UTF8))
                return sr.ReadToEnd();
        }

        // 版本号：唯一来源是仓库根那个 VERSION 文件(PowerShell 侧的 Get-GdVersion 读的是同一个)。
        // 这里不写死任何数字，也不要求它一定在 —— 读不到就退成 "0.0.0"，面板照常开。
        static string ReadVersion(string dir)
        {
            try
            {
                string p = Path.Combine(dir, "VERSION");
                if (!File.Exists(p)) { return "0.0.0"; }
                string first = (ReadAllShared(p) ?? "").Replace("\r\n", "\n").Split('\n')[0].Trim();
                return VersionShape(first);
            }
            catch { return "0.0.0"; }
        }

        static string VersionShape(string raw)
        {
            // 纯函数：形状不对(空、多段、带 v、带空格)一律退成 0.0.0，让"版本号读不出来"在界面上是个看得见的假值。
            if (raw == null) { return "0.0.0"; }
            string s = raw.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d+\.\d+\.\d+$")) { return "0.0.0"; }
            return s;
        }

        internal static int CsvCol(string[] head, string name)
        {
            for (int i = 0; i < head.Length; i++) if (head[i].Trim().Trim('"') == name) return i;
            return -1;
        }

        readonly ToolTip rowTip = new ToolTip();

        static string ToolTipHint(Row r)
        {
            string head = r.Detail.Length > 120 ? r.Detail.Substring(0, 120) + "…" : r.Detail;
            return head + "\r\n\r\n点这一行 = 把完整原因打到下面的日志区";
        }

        // 点卡片行 → 完整原因进日志区(日志区是面板里唯一能装下长句的地方，而且本来就在那里等人看)
        void ShowRowDetail(Row r)
        {
            string src = string.IsNullOrEmpty(r.Source) ? @"state\history.csv 的 detail 列，判定层原文" : r.Source;
            AppendLog("──── " + GameName(r.Game) + " 这一行的完整原文(" + src + ") ────");
            // 先按 '\n' 切成段(SV 行的每一拍是一段)，每段再各自折行 ——
            // 直接把整段丢进 WrapUnits 会让"一拍一行"的排版糊成一团，回看时反而数不清几拍。
            foreach (string seg in (r.Detail ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                if (seg.Trim().Length == 0) continue;
                foreach (string l in WrapUnits(seg, 96)) AppendLog("  " + l);
            }
        }

        // 开机第一眼就把"有原因可看"这件事说清楚：红色的行点了有反应，人才会去点。
        void HintDetails()
        {
            int n = 0;
            for (int i = 0; i < rowsLast.Count; i++) if (rowsLast[i].Detail.Length > 0) n++;
            if (n == 0) return;
            AppendLog("「上一轮」里有 " + n + " 行带完整原因 —— 鼠标停在上面能预览，点一下打到下面的日志区。");
            AnnounceDetails("打开面板时");
        }

        // 按"显示宽度"折行：中文/全角算 2 个单位，ASCII 算 1 个。日志框里是中文混排，
        // 只有按字数硬折才不会在折点上再吞一次换行；96 个单位 ≈ 一行放得下 48 个汉字。
        // 到宽度后先回头找最近的"能断的地方"(空格和停顿标点)，找不到才硬切 ——
        // 2026-09-25 第一版就是硬切，把"--autostart 没生效"折成了"-"/"a 实测无效"，读着像坏了。
        static readonly char[] BreakAfter = new char[] {
            ' ', '\t', ',', ';', ':', ')', ']', '，', '。', '；', '：', '）', '】', '》', '、'
        };

        static List<string> WrapUnits(string text, int units)
        {
            List<string> outLines = new List<string>();
            if (string.IsNullOrEmpty(text)) return outLines;
            List<char> cur = new List<char>();
            int w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                int cw = (ch > 0x1FFF) ? 2 : 1;
                if (w + cw > units && cur.Count > 0)
                {
                    int cut = 0;
                    for (int j = cur.Count - 1; j >= 0 && j >= cur.Count - 16; j--)
                        if (Array.IndexOf(BreakAfter, cur[j]) >= 0 && j + 1 >= 2) { cut = j + 1; break; }
                    if (cut <= 0 || cut > cur.Count) cut = cur.Count;
                    string seg = new string(cur.GetRange(0, cut).ToArray()).TrimEnd(' ', '\t');
                    if (seg.Length > 0) outLines.Add(seg);
                    cur.RemoveRange(0, cut);
                    w = Units(new string(cur.ToArray()));
                }
                cur.Add(ch); w += cw;
            }
            if (cur.Count > 0)
            {
                string tail = new string(cur.ToArray());
                if (tail.Trim().Length > 0) outLines.Add(tail);
            }
            return outLines;
        }

        // 只认 CSV 的这四件事：字段用 " 包、串里的 " 写成 ""、换行分隔记录、行尾 CRLF/LF 都可能。
        // 引号里的逗号和 '|' 都不能当分隔符用 —— history.csv 的 detail 里全是中文标点和路径。
        internal static List<string[]> ParseCsv(string text)
        {
            List<string[]> recs = new List<string[]>();
            List<string> fields = new List<string>();
            StringBuilder cur = new StringBuilder();
            bool inQuote = false, started = false;   // started = 本字段是否已经开过口(区分"空字段"和"还没开始")
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuote)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cur.Append('"'); i++; }
                        else inQuote = false;
                        continue;
                    }
                    cur.Append(ch);
                    continue;
                }
                if (ch == '"' && !started) { inQuote = true; started = true; continue; }
                if (ch == ',') { fields.Add(cur.ToString()); cur.Length = 0; started = false; continue; }
                if (ch == '\r') continue;
                if (ch == '\n')
                {
                    fields.Add(cur.ToString()); cur.Length = 0; started = false;
                    if (fields.Count > 1 || fields[0].Length > 0) recs.Add(fields.ToArray());
                    fields.Clear();
                    continue;
                }
                cur.Append(ch); started = true;
            }
            if (started || cur.Length > 0)
            {
                fields.Add(cur.ToString());
                if (fields.Count > 1 || fields[0].Length > 0) recs.Add(fields.ToArray());
            }
            return recs;
        }

        string EntryPath { get { return Path.Combine(root, string.IsNullOrEmpty(entryName) ? "daily-clear.ps1" : entryName); } }

        List<string> DryRunArgs()
        {
            List<string> a = new List<string>();
            a.Add("-DryRun"); a.Add("-Tag"); a.Add("manual");
            if (flagsGames && games.Count > 1) { a.Add("-Games"); a.Add("all"); }
            return a;
        }

        string ElevNote()
        {
            if (elevNames.Count == 0) return "";
            return "，" + string.Join("/", elevNames.ToArray()) + "那一路要管理员";
        }

        // ---------- 引擎别名的「真身」解析(乙档刀②，2026-10-23 所有者拍板：ENV 那行『面板 PS 版本』不许长期印「读不出」) ----------
        // 为什么需要它：PickEngine()(本件 :44)在只装商店版 PowerShell 的机器上回的是**应用执行别名**
        // (`%LOCALAPPDATA%\Microsoft\WindowsApps\pwsh.exe`)，那枚文件是 ReparsePoint 里的 0 字节存根，
        // FileVersionInfo 读它回**空串** ⇒ 摘要里『面板 PS x.y』那一格只能回「读不出」(刀① 登记为转写差 2)。
        // PS 真身没这一说：原 panel.ps1 的引擎取值那句报的是 `(Get-Process -Id $PID).Path`(该件已随乙档刀③ 删除)，那是 OS 解析之后的包目录路径。
        // 本件**不**按包目录名挑版本(本机现量两枚 PowerShell 包目录，挑一枚＝新判定)，只做一件事：
        // 读别名自己的 reparse blob，按 IO_REPARSE_TAG_APPEXECLINK 的字段位次取真身路径，交给调用方去读版本。
        // 解析不成一律回空串(不抛)，由调用方决定怎么说 —— 读不出就说读不出，不静默补一个数。
        // 本机现量(2026-10-23 仓外 c13-alias-probe.ps1／c13-alias-hex.ps1，五枚别名逐枚读，四枚形状一致)：
        //   blob[0..3]=reparse tag 0x8000001B，blob[4..5]=数据长度，blob[6..7]=保留(现量 0)，blob[8..11]=布局版本(现量 3)，
        //   自 blob[12] 起是以 NUL 分隔的 UTF-16LE 字段：[0]=PackageFamilyName、[1]=AUMID、[2]=真身路径、[3]=一枚标志。
        //   pwsh 那枚的 [2]=`C:\Program Files\WindowsApps\Microsoft.PowerShell_7.6.7.0_x64__8wekyb3d8bbwe\pwsh.exe`，
        //   与 PS 侧 HEADER 第 5 列逐字节相同 ⇒ 解析成功后 HEADER 那一列与版本号那一格两侧同字节，等值门的口子退回「只在解析不成时开」。
        //   ⚠ 不许拿"别名与真身同名"当校验：本机 ArmouryCrate.exe 的真身是 SystemUWPLauncher.exe(别名文件名字面猜不到)，
        //     所以位次取到之后只校"是绝对路径＋文件在场＋不是目录"三条形状，不校名字。
        // ⚠ 三枚声明都写死 EntryPoint + ExactSpelling(本轮实测的坑)：CharSet.Unicode 下 marshaller 会往方法名后
        //   补一个 W 去找 `GdCreateFileW`，找不到就抛 EntryPointNotFoundException —— 而下面 ReadReparseBlob 的
        //   catch 把它当"这台机器读不出"咽掉了，表现是门里 mode 一直停在 alias、ENV 那一格一直印「读不出」，
        //   静默到看不出自己坏了。名字与入口点解耦(带 Gd 前缀防与别的声明相撞)＋把入口点写死，才是这一族该有的形状；
        //   这一形自此由 --coretest 的「入口点在位」那一格咬(它不需要现场别名，见 ReparseEntryPointsOk)。
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr GdCreateFile(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GdDeviceIoControl(IntPtr h, uint code, IntPtr inBuf, uint inLen, byte[] outBuf, uint outLen, out uint returned, IntPtr overlapped);
        [DllImport("kernel32.dll", EntryPoint = "CloseHandle", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GdCloseHandle(IntPtr h);

        const uint GdAccessReadAttributes = 0x0080;
        const uint GdDispositionOpenExisting = 3;
        const uint GdShareAll = 3;                       // READ|WRITE|DELETE：只取属性，不与任何在跑的开句相冲
        const uint GdFlagOpenReparsePoint = 0x00200000;  // 关键：不跟着别名走，读别名**自己**那一坨
        const uint GdFlagBackupSemantics = 0x02000000;
        const uint GdFsctlGetReparsePoint = 0x000900A8;
        const uint GdReparseTagAppExecLink = 0x8000001B;
        const uint GdAppExecLinkLayoutVersion = 3;       // 本机现量；不是这个数就不猜字段位次(回空串，让调用方说读不出)

        static bool BadHandle(IntPtr h)
        {
            return h == IntPtr.Zero || h.ToInt64() == -1L;
        }

        // 应用执行别名 → 真身路径。不是别名／开不了／布局版本不认识／真身不在场 ⇒ 回空串(不抛)。
        internal static string ResolveEngineTarget(string engine)
        {
            if (string.IsNullOrEmpty(engine)) return "";
            try { if ((File.GetAttributes(engine) & FileAttributes.ReparsePoint) == 0) return ""; }
            catch { return ""; }
            byte[] blob = ReadReparseBlob(engine);
            string target = ParseAppExecLinkBlob(blob);
            if (target.Length == 0) return "";
            if (!Path.IsPathRooted(target)) return "";
            try
            {
                if (!File.Exists(target)) return "";
                if ((File.GetAttributes(target) & FileAttributes.Directory) != 0) return "";
            }
            catch { return ""; }
            return target;
        }

        static byte[] ReadReparseBlob(string path)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = GdCreateFile(path, GdAccessReadAttributes, GdShareAll, IntPtr.Zero, GdDispositionOpenExisting,
                                 GdFlagOpenReparsePoint | GdFlagBackupSemantics, IntPtr.Zero);
                if (BadHandle(h)) return null;
                byte[] buf = new byte[8192];
                uint ret;
                if (!GdDeviceIoControl(h, GdFsctlGetReparsePoint, IntPtr.Zero, 0u, buf, (uint)buf.Length, out ret, IntPtr.Zero)) return null;
                if (ret < 16u || ret > (uint)buf.Length) return null;
                byte[] outb = new byte[(int)ret];
                Array.Copy(buf, 0, outb, 0, (int)ret);
                return outb;
            }
            catch { return null; }   // 这是"读不出"，不是"读出了空"：调用方一律按读不出处置
            finally
            {
                if (!BadHandle(h)) { try { GdCloseHandle(h); } catch { } }
            }
        }

        // 纯函数：只按字节形状解，一个文件系统调用都不碰 —— 这样 --coretest 才能喂合成帧自证(机器现场状态不进自检)。
        internal static string ParseAppExecLinkBlob(byte[] blob)
        {
            if (blob == null || blob.Length < 16) return "";
            if (BitConverter.ToUInt32(blob, 0) != GdReparseTagAppExecLink) return "";
            int dataSize = (int)BitConverter.ToUInt16(blob, 4);
            int avail = blob.Length - 8;
            if (dataSize <= 0 || dataSize > avail) dataSize = avail;   // 长度字段与实际读到的字节不符时按短的来
            if (BitConverter.ToUInt32(blob, 8) != GdAppExecLinkLayoutVersion) return "";
            int start = 12;                          // 跳过 tag/长度/保留 8 字节 ＋ 布局版本 4 字节
            int bytes = 8 + dataSize - start;
            if (bytes <= 0) return "";
            string[] fields = Encoding.Unicode.GetString(blob, start, bytes).Split('\0');
            int seen = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Length == 0) continue;
                seen++;
                if (seen == 3) return fields[i];     // 第三枚非空字段＝真身路径(位次见上面那段现量)
            }
            return "";
        }

        // 只给 --coretest 用的一格：上面三枚 kernel32 声明的**入口点**对不对。
        // 为什么单开一格：入口点写错的炸法是 EntryPointNotFoundException，而 ReadReparseBlob 的 catch 把它和
        // "这台机器真的读不出"混成同一个回空串 —— 本轮实测就是这么静默地把一枚已落码的判定读成"没有别名可解析"。
        // 这一格不需要现场那枚别名(只读自检不许要求机器现场状态)：拿一枚注定开不了的路径走一遍，
        // 只要**没抛入口点找不到／模块不在**就算三枚声明成立；开没开上、返回码是什么一概不判(那不是这一格问的事)。
        internal static string ReparseEntryPointsOk()
        {
            try
            {
                IntPtr h = GdCreateFile(@"C:\gd-coretest-no-such-alias.exe", GdAccessReadAttributes, GdShareAll,
                                        IntPtr.Zero, GdDispositionOpenExisting,
                                        GdFlagOpenReparsePoint | GdFlagBackupSemantics, IntPtr.Zero);
                uint ret;
                byte[] buf = new byte[64];
                GdDeviceIoControl(h, GdFsctlGetReparsePoint, IntPtr.Zero, 0u, buf, (uint)buf.Length, out ret, IntPtr.Zero);
                if (!BadHandle(h)) GdCloseHandle(h);
                return "ok";
            }
            catch (EntryPointNotFoundException) { return "entrypoint"; }
            catch (DllNotFoundException) { return "dll"; }
            catch { return "ok"; }
        }
    }
}