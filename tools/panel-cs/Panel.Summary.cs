// tools/panel-cs/Panel.Summary.cs —— 乙档刀①(2026-10-23)：`tools\panel.ps1 -DumpSummary` 那面机器读数的 C# 真身。
//
// 这一份**只是转写**：判据、阈值、措辞、行序全部对着 tools\panel.ps1 抄(下面每段都标了被抄处的行号)，
// 刀① 落地时一个新判定都不加、一个新措辞都不改。它存在的唯一目的，是给 刀③「删 tools\panel.ps1」提供一张可对拍的对面 ——
// 那道等值门(两侧现拍、逐行比)已随件退役(现由 --selftest 那七格守着)，而刀① 拍绿那一帧，C# 面板读数的路一个字都没换(仍走 pwsh 子进程)，
// 生产上这句"判定面进 C#"还没有落地；刀① 只是把对面立起来。
// 刀②(2026-10-23 同日)换的是**接线**：面板摘要改读本件(不再起 pwsh 子进程)，并按所有者拍板补进**一枚**新判定 ——
// "应用执行别名→真身路径"的解析(Panel.Data.cs 的 ResolveEngineTarget)。它只改 HEADER 第 4/5 列与 ENV 版本号那一格的**取源**，
// 不改"哪颗引擎、算哪一路、什么时候算就绪"里的任何一条结论；除这一枚之外本件仍是一字不增判定的转写体。
//
// 编译注意同全工程：系统 csc 是 C# 5(.NET Framework 4)，插值串／?.／表达式体成员／nameof 一概不能用。
//
// 三处"取值换了地方但话没换"的转写差，登记在此免得将来当成第二处真身：
//   1. HEADER 第 3/4/5 列(引擎文件名／绝对路径／root)：PS 侧报的是"此刻正在跑 panel.ps1 的那枚 pwsh"
//      (原 panel.ps1 的引擎取值那句，已随件删除：`$script:engine = [string](Get-Process -Id $PID).Path`，另两行拼进 HEADER)，而 panel.ps1
//      本就是 C# 面板用 `PanelForm.PickEngine()`(Panel.Data.cs:44)挑出来的引擎起的 ⇒ 这里复用同一枚 PickEngine() 挑同一颗引擎，
//      两侧同一条链(刀② 之后报的是这枚引擎解析成的真身路径，不再是别名的名字)。2026-10-23 本机现读：PS 那枚 = `C:\Program Files\WindowsApps\Microsoft.PowerShell_7.6.7.0_x64__8wekyb3d8bbwe\pwsh.exe`
//      (OS 解析后的包目录)，C# 这枚 = `%LOCALAPPDATA%\Microsoft\WindowsApps\pwsh.exe`(PATH 扫描到的应用执行别名)
//      —— 同一个 exe 的两个名字。**刀② 已按所有者拍板补上"别名→真身"的解析**：读别名自己的 reparse blob，
//      按 IO_REPARSE_TAG_APPEXECLINK 的字段位次取真身路径(现量与形状校三条见 Panel.Data.cs 末段)；这**不是**按包目录名挑版本
//      —— 本机 `Program Files\WindowsApps` 下现存两枚 PowerShell 包目录(`…_2026.1010.44.0_neutral_~_8wekyb3d8bbwe` 与
//      `…_7.6.7.0_x64__8wekyb3d8bbwe`)，挑哪一枚仍是新判定，blob 里那枚路径没有这一问。
//      解析成功 ⇒ 第 3/4 列两侧同字节，等值门那一列的口子自动不开(门报 HEADER_PATH_MODE=exact)；解析不成(不是别名／
//      blob 布局版本不是 3／真身不在场)⇒ C# 侧照旧报别名路径，门里在 mode=alias 时**只宽"绝对路径"这一列**(它自己的位次计数叫第 5 列)，
//      同一行的其余字段逐字段照比(GUN1/GUN3/GUN5 各咬一发)。
//      (消费方 Panel.cs 的 ParseSummary 只读 HEADER 的第 2 列=入口名，3/4/5 列它自己从不取。)
//   2. ENV 那句『面板 PS 7.x · 子进程 pwsh.exe』(原 panel.ps1 的 ENV 那两行，已随件删除；取的是 $PSVersionTable = 本进程，rail 标签同款文案也在那一件里)：
//      措辞逐字保留，取值处换成**选中引擎文件的 FileVersionInfo 的主.次**。件退役后这句话没有进程可报，语义变成"面板要用的那颗引擎是 PS 几"。
//      本机实测(仓外 c13-enginevi.ps1，两枚路径对照)：别名形是 ReparsePoint，`ProductVersion` 回**空串** ⇒ 这一格回「读不出」；
//      包目录那枚回 `7.6.7 SHA: …` ⇒ 主.次取到 7.6。装了安装版(本机 `C:\Program Files\PowerShell\7\pwsh.exe` **不存在**)的机器上
//      PickEngine 走 :65 那一路返回安装版，两侧同字节、门里一个口子都不开(GUN5 就是拿全等帧验这一点)。
//      读不出时明说"读不出"，不静默补 0 —— 两侧不等会当场红在门里，而不是绿着骗人。
//      刀②(2026-10-23 所有者拍板"补别名→真身解析")处置完这一差：BuildCtx 先把 PickEngine() 那枚过一遍
//      `PanelForm.ResolveEngineTarget()`(Panel.Data.cs 末段)，解析成功就读真身的 ProductVersion。
//      本机现拍(2026-10-23 换接线后，仓外 c13-parity-keep.log ＋ 门内 -Keep 留的 ps-frame-1／cs-frame 两帧对读)：
//      ENV 那一格回 `面板 PS 7.6 · 子进程 pwsh.exe`，与 PS 侧逐字节相同；HEADER 第 5 列两侧同为包目录那枚真身，
//      门报 HEADER_PATH_MODE=exact、ATOMS_EXCEPTED=0 —— 这两枚口子在本机从此不开，只在"解析不成"的机器上开。
//      解析不成的机器仍回「读不出」，那一口子留着(门 GUN4 咬它)。
//   3. "这棵树上有哪几路"的取法：PS 侧 dot-source lib\modules.ps1 之后读注册表($global:GdModules)；C# 侧不跑 PS，
//      所以按 lib\*-module.ps1 逐份读**平键**(Id/ShortName/AssistantLib/RequiresAdmin)加 EnvProbes 那一段的
//      『标签 → 路径表达式』。知识仍然只在那些清单文件里(这里不是第二套清单)，但这是一条**形状假设**：
//      只认"一行一个键"的值和两种路径表达式(`$S.paths.K` 与 `(Join-GdPath $S.paths.A @($S.paths.B))`)；
//      第三种形状认不出时这一路的探针行会少一行，而"少一行"是等值门里的红，不会被咽掉。
//      ⚠ 可用性判据没换：还是"助手库在不在"(Test-GdModuleInstalled 那句)，落点 = lib\<AssistantLib>。
//
// 与 panel.ps1 的**故意不同**(两处，都不改结论)：
//   * PS 侧 Dump-PanelSummaryMachine(原 panel.ps1 里那半段，已随件删除) 为了取 SV 详情把 supervision.json **读了第二遍**(第一遍在
//     Get-PanelSummaryParts 那句)。这里同样读第二遍 —— 照真身的形状走，哪怕两遍之间文件被监督进程改写过，
//     两侧也是同一种"可能不一致"，等值门才不会把引擎差当成现场差。
//   * PS 侧入口脚本一个都不在时是 panel.ps1 启动就 throw(那半段已随件删除)；这里回空串继续出格，因为 C# 面板不许因为
//     一份清单读不出来而整面没有读数。这一差在等值门上表现为"一侧红"，不会出现"绿的假面"。
//
// 等值门首拍全绿读数(2026-10-23，pwsh 7 单引擎；码面=本树，数据面=main 那棵树，PS 侧 panel.ps1 与 HEAD 同字节)：
//   FAIL=0 GUNS=7 ／ 三帧 35·35·35 行 ／ 噪声底(同 root 连拍两帧 PS 侧)=差 0 处 ／ 逐行等值=红 0 处
//   ／ HEADER_PATH_MODE=alias ／ ATOMS_COMPARED=40 ATOMS_EXCEPTED=2(就是上面差 1 与差 2 那两枚，条件口子，见门头注)
//   ／ 十族齐 ENV=5 FLAGS=1 GAME=2 GATE=4 HEADER=1 HELP=10 LAST=2 PROBE=6 PROTO=1 SV=1 TIME=2，
//     其中 TIME_LIVE=2(两格都真问到了计划任务、都印出「→ 日期·状态·UAC」，不是「未注册」兜底)、PROBE_OK=6、NOT_COVERED=none。
//   门另有三帧是"造出来的假帧"咬的(GUN1-7)，不吃现场状态；现场那一路的自证=噪声底＋CS 信封三格(RC／PROTO=2／条数)+PS_SIDE_WORKING_DIRTY。
//
// 刀② 换接线后现拍(2026-10-23，数据面=本树 wt：这棵树没有 config\settings.json，是一副**退化帧**)：
//   FAIL=0 GUNS=7 ／ 三帧 28·28·28 行 ／ 噪声底=差 0 处 ／ 逐行等值=红 0 处
//   ／ HEADER_PATH_MODE=**exact** ／ ATOMS_COMPARED=28 ATOMS_EXCEPTED=**0**
//   —— 上面那段首拍里"宽两处"的两枚口子，在换接线这一帧**一枚都没开**：第 5 列与版本号两侧同字节，走的是逐字符全等那条默认路。
//   退化帧的自觉读数：TIME_LIVE=0 PROBE_OK=0(问不到真任务、读不到环境清单)，NOT_COVERED 三族 TIME／PROBE／SV，
//   逐族 ENV=5 FLAGS=1 GAME=2 GATE=4 HEADER=1 HELP=10 LAST=1 PROBE=2 PROTO=1 TIME=1(共 28，无 SV 族)
//   ⇒ 这三族在本批**没有**被现场拍到，等值门对它们的自证仍只在 GUN 那几发假帧里。
//
// ⚠ 刀③ 删 tools\panel.ps1 时，本头注与各段里的 `panel.ps1:NNN` 锚会一起失效；随行改口按丙档落码刀① 的规矩
//   改成"原 panel.ps1:…(乙档刀③ 已随件删)"，不许留着指不中的坐标。—— 本件各段的 panel.ps1 锚已按此形改口完毕。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    // 一份 lib\*-module.ps1 里这一层取得到的那几项。只装摘要面要用的键，其余一律不认。
    sealed class GdPanelModule
    {
        public string File = "";
        public string Id = "";
        public string ShortName = "";
        public string AssistantLib = "";
        public bool RequiresAdmin;
        public List<string[]> Probes = new List<string[]>();   // 每枚 = [标签, 原始表达式文本]
    }

    // 一行卡片读数：Kind 决定颜色，Game 决定分页，Slot 决定它走 ENV 还是 SV。
    sealed class GdSummaryRow
    {
        public string Kind = "dim";
        public string Game = "";
        public string Text = "";
        public string Slot = "";
        public GdSummaryRow(string kind, string game, string text, string slot)
        {
            Kind = kind; Game = game; Text = text; Slot = slot;
        }
    }

    static class GdPanelSummary
    {
        // = 原 tools\panel.ps1 的 $script:GdPanelProtoVer 那句(该件已随乙档刀③ 删除)；协议规格本体是它上方那段注释。
        // 消费方 Panel.cs 的 GdPanelProtoKnown 认的是同一个数，改协议要三处同批(件删后剩两处)。
        internal const int Proto = 2;

        // ==================== verb: emit-panel-summary ====================
        // 机读面：--in 可以是空对象(这一枚不接任何注入值，全读现场)；回执 data = {Proto, Count, Lines[]}。
        // 走信封是 Cli.cs 的三通道铁律：等值门从 --out 的 JSON 里取 Lines，一行都不许走 stdout。
        internal static object Emit(Dictionary<string, object> raw)
        {
            List<string> lines = Build();
            return Gd.Out(
                "Proto", Proto,
                "Count", lines.Count,
                "Lines", lines.ToArray());
        }

        // 面板形态(刀② 接线时)要的也是这一份 —— 协议行的组装只此一处，verb 只是它的无头外壳。
        internal static List<string> Build()
        {
            GdEnv.Init(null);
            Ctx c = BuildCtx();
            List<string> o = new List<string>();

            o.Add("PROTO|" + Proto.ToString(CultureInfo.InvariantCulture));
            o.Add("HEADER|" + Join(c.GameIds(), " ") + "|" + c.EntryName + "|" +
                  EngineName(c.EngineTarget) + "|" + c.EngineTarget + "|" + c.Root);

            foreach (GdPanelModule m in c.Games)
                o.Add("GAME|" + m.Id + "|" + (m.ShortName.Length > 0 ? m.ShortName : m.Id));

            foreach (GdSummaryRow r in TimeRows(c)) o.Add("TIME|" + r.Kind + "|" + r.Text);
            foreach (GdSummaryRow r in LastRows(c)) o.Add("LAST|" + r.Kind + "|" + r.Game + "|" + r.Text);
            foreach (GdSummaryRow r in GateRows(c)) o.Add("GATE|" + r.Kind + "|" + r.Game + "|" + r.Text);

            // 原 panel.ps1 的 ENV 装配那段(已随件删除)：环境行里 Slot=supervisor 的那一枚**不进 ENV**，改走 SV(带展开详情)。
            // 两个分支互斥，所以同一句话不会在环境卡出现两遍。
            GdSummaryRow svRow = null;
            foreach (GdSummaryRow r in EnvRows(c))
            {
                if (r.Slot == "supervisor") { svRow = r; continue; }
                o.Add("ENV|" + r.Kind + "|" + r.Text);
            }
            if (svRow != null)
            {
                Dictionary<string, object> sv = ReadSupervisor();          // 第二遍，照原 panel.ps1 那段的形状(已随件删除)
                StringBuilder line = new StringBuilder();
                line.Append("SV|").Append(svRow.Kind).Append("|").Append(svRow.Text);
                foreach (object d in DetailLines(sv))
                    line.Append("|").Append(Gd.Str(d).Replace("|", "¦"));   // 详情正文里不许再出现分隔符(原 panel.ps1 那三句，已随件删除)
                o.Add(line.ToString());
            }

            foreach (GdPanelModule m in c.Games)
                foreach (GdSummaryRow r in ModuleProbeRows(c, m))
                    o.Add("PROBE|" + r.Kind + "|" + m.Id + "|" + r.Text);

            o.Add("FLAGS|" + c.FlagAck + "|" + c.FlagShopDone + "|" +
                  (c.FlagGames ? "yes" : "") + "|" + (c.RunViaTask ? "yes" : "") + "|" +
                  Join(c.AdminNames, ","));

            foreach (string[] h in c.Help) o.Add("HELP|" + h[0] + "|" + h[1]);

            return o;
        }

        // ==================== 现场上下文(一次组装，各段行只读它) ====================
        sealed class Ctx
        {
            public string Root = "";
            public List<GdPanelModule> Games = new List<GdPanelModule>();
            public string EntryName = "";
            public string Engine = "";
            public string EngineTarget = "";              // 刀②：别名解析后的真身；解析不成本身就等于 Engine
            public Dictionary<string, object> Settings;          // null = 读不到 settings.json(和 PS 的 $S=$null 同形)
            public List<string> Times = new List<string>();
            public string Prefix = "GameDaily";
            public string FlagAck = "";
            public string FlagShopDone = "";
            public bool FlagGames;
            public bool RunViaTask;
            public List<string> AdminNames = new List<string>();
            public List<string[]> Help = new List<string[]>();

            public List<string> GameIds()
            {
                List<string> ids = new List<string>();
                foreach (GdPanelModule m in Games) ids.Add(m.Id);
                return ids;
            }
        }

        static Ctx BuildCtx()
        {
            Ctx c = new Ctx();
            c.Root = GdEnv.Root;
            c.Games = LoadModules(c.Root);
            c.Engine = PanelForm.PickEngine();
            // 刀②(2026-10-23 所有者拍板)：商店版那枚引擎在 PATH 里是**应用执行别名**，直接读它的 FileVersionInfo 回空串。
            // 先把别名解析成 reparse blob 里的真身路径，HEADER 第 4/5 列与『面板 PS x.y』那一格都改读解析后的那枚 ——
            // PS 侧这两处读的是 `(Get-Process -Id $PID).Path`(OS 已解析的包目录)，所以解析成功时两侧同字节。
            // 解析不成(不是别名／布局版本不是 3／真身不在)时 EngineTarget 照旧等于 Engine，行为退回刀① 登记的差 1／差 2，
            // 版本号那一格照旧明说「读不出」——不静默补 0，也不另开一条"按目录名挑版本"的选路。
            string resolved = PanelForm.ResolveEngineTarget(c.Engine);
            if (resolved.Length > 0) c.EngineTarget = resolved;
            else c.EngineTarget = c.Engine;

            // 入口名三选一(原 panel.ps1 的入口探量那段，已随件删除)：顺序不许动，但那句"靠这三枚候选分家"自 11-04 刀⑦ 笔(a) 起按历史引文读——
            // 三棵树(合体＋两棵导出)的根目录都有 publish-split 复制过去的 daily-clear.ps1，第一枚恒命中，后两枚从未生效(登记不代改：摘候选＝形状变)。
            foreach (string cand in new string[] { "daily-clear.ps1", "ark-daily.ps1", "r1999-daily.ps1" })
            {
                if (File.Exists(Path.Combine(c.Root, cand))) { c.EntryName = cand; break; }
            }

            // 参数名反射(原 panel.ps1 的参数名反射那段，已随件删除)：PS 用 Get-Command 读 param 块，这里按"声明位上的 $名"读同一份文本(见 DeclaredParams)。
            HashSet<string> declared = DeclaredParams(Path.Combine(c.Root, c.EntryName));
            c.FlagAck = PickFlag(declared, new string[] { "AckArk", "Ack" });
            c.FlagShopDone = PickFlag(declared, new string[] { "ArkShopDone", "ShopDone" });
            c.FlagGames = PickFlag(declared, new string[] { "Games" }).Length > 0;

            // 哪几路要管理员(原 panel.ps1 的 RequiresAdmin 清单那段，已随件删除)：只认清单里的 RequiresAdmin，话术里的短名用 ShortName。
            foreach (GdPanelModule m in c.Games) if (m.RequiresAdmin) c.AdminNames.Add(m.ShortName);

            List<string> missing;
            try { c.Settings = GdSettings.Load(out missing, true, null); }
            catch { c.Settings = null; }      // 没有 settings.json / 读坏了：PS 侧也是 catch 掉当 $null 用(原 panel.ps1 那句，已随件删除)

            Dictionary<string, object> schedule = SubMap(c.Settings, "schedule");
            foreach (object t in Gd.List(Gd.Get(schedule, "times"))) c.Times.Add(Gd.Str(t));
            string prefix = Gd.Str(Gd.Get(schedule, "taskPrefix"));
            if (prefix.Length > 0) c.Prefix = prefix;

            c.RunViaTask = File.Exists(Path.Combine(c.Root, "run-via-task.ps1"));
            c.Help = BuildHelp(c);
            return c;
        }

        // ==================== 模块清单那几枚平键(原 panel.ps1 的平键装配那段，已随件删除) ====================
        static List<GdPanelModule> LoadModules(string root)
        {
            List<GdPanelModule> outList = new List<GdPanelModule>();
            string lib = Path.Combine(root, "lib");
            string[] files;
            try { files = Directory.GetFiles(lib, "*-module.ps1"); }
            catch { return outList; }
            Array.Sort(files, delegate(string a, string b)
            {
                // 注册序 = 文件名序(Import-GdModules 那句"按文件名排序")；文件名都是 ASCII，Ordinal 就够。
                return string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.Ordinal);
            });
            foreach (string f in files)
            {
                GdPanelModule m = ParseModule(f);
                if (m == null || m.Id.Length == 0) continue;
                // Test-GdModuleInstalled 那把尺：助手库不在 ⇒ 这一路不算装在这棵树上。
                if (m.AssistantLib.Length == 0 || !File.Exists(Path.Combine(lib, m.AssistantLib))) continue;
                outList.Add(m);
            }
            return outList;
        }

        static GdPanelModule ParseModule(string file)
        {
            string txt;
            try { txt = File.ReadAllText(file); } catch { return null; }
            GdPanelModule m = new GdPanelModule();
            m.File = Path.GetFileName(file);
            m.Id = FlatKey(txt, "Id");
            m.ShortName = FlatKey(txt, "ShortName");
            m.AssistantLib = FlatKey(txt, "AssistantLib");
            m.RequiresAdmin = Regex.IsMatch(txt, @"(?m)^\s*RequiresAdmin\s*=\s*\$true\b");
            m.Probes = ProbeSpecs(txt);
            return m;
        }

        static string FlatKey(string txt, string key)
        {
            Match mm = Regex.Match(txt, @"(?m)^\s*" + key + @"\s*=\s*'([^']*)'");
            if (mm.Success) return mm.Groups[1].Value;
            mm = Regex.Match(txt, @"(?m)^\s*" + key + @"\s*=\s*\x22([^\x22]*)\x22");
            return mm.Success ? mm.Groups[1].Value : "";
        }

        // EnvProbes 钩子里那一段『'标签' = 表达式』。形状假设见文件头第 3 条：一行一个键，收尾是一枚独占一行的 }。
        static List<string[]> ProbeSpecs(string txt)
        {
            List<string[]> rows = new List<string[]>();
            string[] lines = txt.Replace("\r\n", "\n").Split('\n');
            int start = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"^\s*EnvProbes\s*=\s*\{\s*$")) { start = i + 1; break; }
            }
            if (start < 0) return rows;
            for (int i = start; i < lines.Length && i < start + 40; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                if (t.StartsWith("param(") || t.StartsWith("return ")) continue;
                if (t == "}") break;                                     // 那枚 [ordered]@{ 的收尾
                if (Regex.IsMatch(t, @"^\w+\s*=\s*\{\s*$")) break;        // 下一个钩子开始了却没 } ⇒ 形状不认识，收手
                Match mm = Regex.Match(t, @"^'([^']+)'\s*=\s*(.+)$");
                if (!mm.Success) continue;
                string expr = mm.Groups[2].Value.Trim();
                if (expr.EndsWith(",")) expr = expr.Substring(0, expr.Length - 1).Trim();
                rows.Add(new string[] { mm.Groups[1].Value, expr });
            }
            return rows;
        }

        // 表达式 → 路径值。认不出形状时回 false，调用方把这一行**丢掉**(丢行＝少一枚 PROBE，等值门会红)。
        static bool ResolveProbe(string expr, Dictionary<string, object> paths, out string value)
        {
            value = "";
            Match mm = Regex.Match(expr, @"^\(?Join-GdPath\s+\$S\.paths\.(\w+)\s+@\(\s*\$S\.paths\.(\w+)\s*\)\)?$");
            if (mm.Success)
            {
                value = GdEnv.JoinPath(Gd.Str(Gd.Get(paths, mm.Groups[1].Value)),
                                       Gd.Str(Gd.Get(paths, mm.Groups[2].Value)));
                return true;
            }
            mm = Regex.Match(expr, @"^\$S\.paths\.(\w+)$");
            if (mm.Success) { value = Gd.Str(Gd.Get(paths, mm.Groups[1].Value)); return true; }
            return false;
        }

        // ==================== param 块里的声明名(Get-Command .Parameters 的同形替身) ====================
        // 认"紧挨着一个 ] 之后的 $名"= 一枚声明(三枚入口的参数都带 [switch]/[string]/[ValidateSet()][string] 属性)，
        // 块的范围按"从 param( 那一行起到独占一行的 ) 为止"取 —— 参数块里的注释含括号(daily-clear.ps1 那段就是)，
        // 按括号配平会连注释里的括号一起数，所以宁可押这条更浅的形状假设：认不出 = 空集 = FLAGS 三格全空，门里当场红。
        static HashSet<string> DeclaredParams(string entryPath)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(entryPath) || !File.Exists(entryPath)) return names;
            string txt;
            try { txt = File.ReadAllText(entryPath); } catch { return names; }
            string[] lines = txt.Replace("\r\n", "\n").Split('\n');
            bool inBlock = false;
            foreach (string raw in lines)
            {
                string t = raw.Trim();
                if (!inBlock)
                {
                    if (!Regex.IsMatch(t, @"^\s*param\s*\(")) continue;
                    if (t.EndsWith(")")) continue;          // 单行 param(...)：本工程的三枚入口都不是，遇到了就跳过(不猜)
                    inBlock = true;
                    continue;
                }
                if (t.StartsWith(")")) break;
                foreach (Match mm in Regex.Matches(t, @"\]\s*\$(\w+)")) names.Add(mm.Groups[1].Value);
            }
            return names;
        }

        static string PickFlag(HashSet<string> declared, string[] candidates)
        {
            foreach (string c in candidates) if (declared.Contains(c)) return c;
            return "";
        }

        // ==================== TIME：逐个时间点问计划任务(原 panel.ps1 的 TIME 段，已随件删除) ====================
        // 走 Schedule.Service COM 而不是 schtasks.exe —— Round.cs 的 read-task-state 已经是这个理由。那枚 verb 不报
        // RunLevel，而这一行要的正是"免不免 UAC"，所以这里另取 Principal.RunLevel(既有 verb 的回执形状一个字不动)。
        //
        // 不取 schtasks.exe 的文本是 2026-10-23 现量过的两条，不是"觉得别扭"：
        //   * 那句"下次运行时间"是**本地化文本**，解析它=把面板绑在这台机器的区域性上。本机 zh-CN 的
        //     ShortDatePattern 实测 `yy.M.d`，C# 用 CurrentCulture 解析 `26.10.24 6:00:00` 得 2026-10-24(与 PS 侧
        //     NextRunTime 那句同一天)，用 InvariantCulture 解析当场抛 FormatException ⇒ 换一台机器就换一把尺。
        //   * 未启用的任务在 schtasks 的 verbose CSV 里那列回 `N/A`，而 COM 的 NextRunTime 照样给日期 —— 两侧形状不同源，
        //     对拍时这类差既不是转写错也不是现场错，属于门里咽不下的那类。
        //
        // 两处坐标是 2026-10-23 在这台机器上现读出来的，不是照文档抄的(仓外探针 c13-com-probe2/3/4.exe)：
        //   * GetFolder 的参数是 "\"，不是 "/"。传 "/" 当场 0x8007007B(路径格式无效) ⇒ com=false ⇒ 每一格都印
        //     「未注册」，而 PS 侧同一时刻印的是「10-24 06:00·Ready·免 UAC」。这一枚是本轮等值门的第一处红因。
        //   * task.Information 在这台机器上是 DISP_E_UNKNOWNNAME(名字不存在，Round.Prop 吞掉回 null)，
        //     而 NextRunTime／LastRunTime 直接挂在 task 上就是真 DateTime。所以这里不套 Information 那一层：
        //     PS 侧 $info 是 Get-ScheduledTaskInfo **造出来的一枚 CIM 适配器**，COM 侧没有对应的一层壳。
        //     (verb read-task-state 走的仍是 Information 那一层 ⇒ 现场坏，零调用方所以没红；另案，不在这刀里改钉死的 verb。)
        static List<GdSummaryRow> TimeRows(Ctx c)
        {
            List<GdSummaryRow> rows = new List<GdSummaryRow>();
            object svc = null, folder = null;
            bool com = false;
            try
            {
                Type t = Type.GetTypeFromProgID("Schedule.Service");
                if (t != null)
                {
                    svc = Activator.CreateInstance(t);
                    Round.Invoke(svc, "Connect");
                    folder = Round.Invoke(svc, "GetFolder", "\\");
                    com = folder != null;
                }
            }
            catch { com = false; }        // 组件不在／连不上：PS 侧那一族 try/catch 也是同一落点 ⇒ 每格都算"未注册"

            foreach (string time in c.Times)
            {
                string nm = c.Prefix + "-" + time.Replace(":", "");
                object task = null;
                if (com) { try { task = Round.Invoke(folder, "GetTask", nm); } catch { task = null; } }
                // PS 那句判据是 `$null -eq $info`(原 panel.ps1 的 TIME 段，已随件删除)，而 $info 由 `$task | Get-ScheduledTaskInfo 造：
                // $task 取不到 ⇒ 管道无输入 ⇒ $info 为 null。所以这里"未注册"的等价判据就是 task 取没取到，
                // 不掺读数(读数取不到是另一件事：那一格回「未排期」，PS 也一样)。
                if (task == null)
                {
                    rows.Add(new GdSummaryRow("warn", "", time + " 未注册 → 跑 install-task.ps1", ""));
                    continue;
                }
                DateTime? next = AsDate(Round.Prop(task, "NextRunTime"));
                string when = (next != null && next.Value.Year > 2000)
                    ? next.Value.ToString("MM-dd HH:mm", CultureInfo.CurrentCulture)
                    : "未排期";
                string st = Round.TaskStateName(Round.Num(Round.Prop(task, "state")));
                // RunLevel 挂在 Definition.Principal 上(v2 模型)，task.Principal 同样是 DISP_E_UNKNOWNNAME；
                // 拿到的是 0/1，Round.RunLevelName 换成 PS 那个枚举的英文字，判据(含 Regex)与 PS 逐字同形。
                object def = Round.Prop(task, "Definition");
                object prin = Round.Prop(def, "Principal");
                string lvl = Round.RunLevelName(Round.Num(Round.Prop(prin, "RunLevel")));
                string kind = "ok";
                string run = "会弹 UAC";
                if (Regex.IsMatch(lvl, "Highest")) run = "免 UAC";
                else kind = "warn";
                if (!Regex.IsMatch(st, "Ready|Running")) kind = "warn";
                rows.Add(new GdSummaryRow(kind, "", time + " → " + when + "·" + st + "·" + run, ""));
            }
            if (rows.Count == 0) rows.Add(new GdSummaryRow("warn", "", "没读到时间点(先跑 setup / 装定时)", ""));
            try { if (svc != null && Marshal.IsComObject(svc)) Marshal.ReleaseComObject(svc); } catch { }
            return rows;
        }

        // VT_DATE 两头都可能：DateTime 或 OleDate 的双精度(Round.cs 的 DateText 同形，那边只要文本，这边要年份)。
        static DateTime? AsDate(object v)
        {
            if (v == null) return null;
            if (v is DateTime) return (DateTime)v;
            try { return DateTime.FromOADate(Convert.ToDouble(v, CultureInfo.InvariantCulture)); }
            catch { return null; }
        }

        // ==================== LAST：每个游戏只留最近一条(原 panel.ps1 的 LAST 段，已随件删除) ====================
        // CSV 的读法复用 Panel.Data.cs 那份真身(ParseCsv/ReadAllShared/CsvCol) —— 引号里的逗号与 '|' 不能当分隔符，
        // history.csv 的 detail 列全是中文标点和路径，另写一把切分就是第二个事实源。
        static List<GdSummaryRow> LastRows(Ctx c)
        {
            List<GdSummaryRow> rows = new List<GdSummaryRow>();
            string hist = Path.Combine(GdEnv.State, "history.csv");
            if (File.Exists(hist))
            {
                try
                {
                    List<string[]> recs = PanelForm.ParseCsv(PanelForm.ReadAllShared(hist));
                    if (recs.Count >= 2)
                    {
                        string[] head = recs[0];
                        int gi = PanelForm.CsvCol(head, "game");
                        int ti = PanelForm.CsvCol(head, "time");
                        int mi = PanelForm.CsvCol(head, "mode");
                        int si = PanelForm.CsvCol(head, "status");
                        int di = PanelForm.CsvCol(head, "duration_sec");
                        Dictionary<string, string[]> byGame = new Dictionary<string, string[]>(StringComparer.Ordinal);
                        List<string> order = new List<string>();
                        for (int i = 1; i < recs.Count; i++)
                        {
                            string g = Cell(recs[i], gi);
                            if (g.Length == 0) continue;
                            if (!byGame.ContainsKey(g)) order.Add(g);
                            byGame[g] = recs[i];        // 后写覆盖前写 = "每路只留最近一条"(PS 那枚 @{} 同形)
                        }
                        order.Sort(delegate(string a, string b)
                        {
                            // PS 的 Sort-Object 走当前区域性；ark/r1999 两种比法同序，但仍按文化比，不押 ordinal。
                            return string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
                        });
                        foreach (string g in order)
                        {
                            string[] r = byGame[g];
                            string dur = Cell(r, di);
                            if (dur.Length > 0)
                            {
                                // '{0:N0}s'：PS 的 -f 用当前区域性，分组符也跟着它。取不出整数就照真身那样把整段读判成坏。
                                int secs = int.Parse(dur, NumberStyles.Integer, CultureInfo.CurrentCulture);
                                dur = secs.ToString("N0", CultureInfo.CurrentCulture) + "s";
                            }
                            else dur = "-";
                            string st = Cell(r, si);
                            string kind = "ok";
                            if (Regex.IsMatch(st, "stall|timeout|error|fail|not-started|unknown|blocked")) kind = "err";
                            else if (!Regex.IsMatch(st, "^(ok|done|完成)")) kind = "warn";
                            string time = Cell(r, ti);
                            Match mm = Regex.Match(time, @"\d{4}-\d{2}-\d{2}\s+(\d{2}:\d{2})");
                            if (mm.Success) time = mm.Groups[1].Value;
                            string txt = time + " " + g + " → " + st + " " + dur;
                            string withMode = time + " " + g + " " + Cell(r, mi) + "→" + st + " " + dur;
                            if (withMode.Length <= 38) txt = withMode;      // 卡片只有 272px：装得下才带 mode
                            rows.Add(new GdSummaryRow(kind, g, txt, ""));
                        }
                    }
                }
                catch (Exception ex)
                {
                    rows = new List<GdSummaryRow>();
                    rows.Add(new GdSummaryRow("warn", "", "history.csv 读不动：" + ex.Message, ""));
                }
            }
            if (rows.Count == 0) rows.Add(new GdSummaryRow("dim", "", "这台机器还没跑过", ""));
            return rows;
        }

        static string Cell(string[] r, int i)
        {
            if (i < 0 || r == null || i >= r.Length) return "";
            return r[i].Trim();
        }

        // ==================== GATE：两道门按路分开报(原 panel.ps1 的 GATE 段，已随件删除) ====================
        // 第一道门两路措辞**不许统一**：ark 走每轮机器自证(state\ark-event-probe.json)，r1999 照旧报 ack 表(2026-10-12 第 1 刀)。
        // 两张表的读法复用 Round.Data.cs 那几枚助手 —— 文件名与外层键都是既有协议，这里不另开第四本账。
        static List<GdSummaryRow> GateRows(Ctx c)
        {
            List<GdSummaryRow> rows = new List<GdSummaryRow>();
            bool haveDecisions = File.Exists(Path.Combine(c.Root, @"lib\decisions.ps1"));   // = 原 panel.ps1 的 $haveDecisions 那句(该件已删)
            foreach (GdPanelModule m in c.Games)
            {
                string g = m.Id;
                if (!haveDecisions)
                {
                    rows.Add(new GdSummaryRow("warn", g, g + " 判定库不在，门状态未知", ""));
                    continue;
                }
                if (g == "ark")
                {
                    Dictionary<string, object> pb = RoundData.ReadProbe();
                    if (pb != null)
                    {
                        string txt = "进得去 → 理智照刷活动图";
                        string kind = "ok";
                        if (!Gd.Truthy(Gd.Get(pb, "Entered")))
                        {
                            txt = "进不去 → 当日改刷长草(打通后下一轮自动切回)";
                            kind = "warn";
                        }
                        rows.Add(new GdSummaryRow(kind, g,
                            g + " 活动自证(" + Gd.Str(Gd.Get(pb, "Date")) + ") " +
                            Gd.Str(Gd.Get(pb, "EventKey")) + "/" + Gd.Str(Gd.Get(pb, "Stage")) + "：" + txt, ""));
                    }
                    else rows.Add(new GdSummaryRow("dim", g, g + " 活动自证：还没有结论(下一轮即以活动最后一关为探针)", ""));
                }
                else
                {
                    Dictionary<string, object> ack = RoundData.ReadGateMap(RoundData.AckFile(g), "acked");
                    if (ack.Count > 0)
                        rows.Add(new GdSummaryRow("ok", g, g + " 活动确认：已确认 " + SortedKeys(ack), ""));
                    else rows.Add(new GdSummaryRow("warn", g, g + " 活动确认：未确认 → 新活动先跳过", ""));
                }
                Dictionary<string, object> shop = RoundData.ReadGateMap(RoundData.ShopDoneFile(g), "done");
                if (shop.Count > 0)
                    rows.Add(new GdSummaryRow("ok", g, g + " 商店领空：已标记 " + SortedKeys(shop) + "(剩余理智转长草)", ""));
                else rows.Add(new GdSummaryRow("dim", g, g + " 商店领空：未标记", ""));
            }
            return rows;
        }

        static string SortedKeys(Dictionary<string, object> map)
        {
            List<string> ks = new List<string>(map.Keys);
            ks.Sort(delegate(string a, string b) { return string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase); });
            return string.Join(",", ks.ToArray());
        }

        // ==================== ENV：路径就绪 + 屏保 + 监督 + 三行说明(原 panel.ps1 的 ENV 段，已随件删除) ====================
        // 行序就是这一段的可押面：卡装不下时从尾部省，所以"armed 的屏保插在前面、常态屏保落最后"这条得原样搬。
        static List<GdSummaryRow> EnvRows(Ctx c)
        {
            List<GdSummaryRow> rows = new List<GdSummaryRow>();
            Dictionary<string, object> paths = SubMap(c.Settings, "paths");

            List<string> keys = GdSettings.RequiredPathKeys();
            List<string> notReady = new List<string>();
            foreach (string k in keys) if (!GdEnv.PathReady(Gd.Str(Gd.Get(paths, k)))) notReady.Add(k);
            if (notReady.Count > 0)
            {
                // 环境卡只有 264px：键名超过三个就截到三枚并报总数，否则整行被省略号吞掉(原 panel.ps1 的 ENV 段，已随件删除)
                string tail = "";
                List<string> show = new List<string>(notReady);
                if (show.Count > 3) { tail = "… 共 " + show.Count.ToString(CultureInfo.InvariantCulture) + " 项"; show = show.GetRange(0, 3); }
                rows.Add(new GdSummaryRow("err", "", "配置还差：" + string.Join(" , ", show.ToArray()) + tail + " → 跑 setup.ps1", ""));
            }
            else if (keys.Count > 0)
                rows.Add(new GdSummaryRow("ok", "", "助手路径 " + keys.Count.ToString(CultureInfo.InvariantCulture) + " 项全部就绪", ""));

            Dictionary<string, object> v = Gd.Map(Decisions.ScreensaverVerdict(
                Gd.Out("cfg", ScreensaverConfigOrNull(), "floorSec", 0)));
            GdSummaryRow scv = null;
            bool scRisk = v != null && Gd.Truthy(Gd.Get(v, "Risk"));
            if (v != null) scv = new GdSummaryRow(scRisk ? "warn" : "dim", "", Gd.Str(Gd.Get(v, "Text")), "screensaver");
            if (scv != null && scRisk) rows.Add(scv);

            GdSummaryRow svRow = SupervisorRow(c);
            if (svRow != null) rows.Add(svRow);

            rows.Add(new GdSummaryRow("dim", "", "这一仓：" + Join(c.GameIds(), " + "), ""));
            rows.Add(new GdSummaryRow("dim", "", "入口：" + c.EntryName, ""));
            rows.Add(new GdSummaryRow("dim", "", "面板 PS " + EnginePsVersion(c.EngineTarget) + " · 子进程 " + EngineName(c.EngineTarget), ""));
            if (scv != null && !scRisk) rows.Add(scv);   // 常态屏保 → 排最后，装不下先省它
            return rows;
        }

        // 原 Get-GdScreensaverConfig(common.ps1 旧 359-378，历史引文非活锚)的对面就是 R9-a 收编的 read-screensaver-config；
        // 那一枚 verb 的 Found:false 等价于真身那句 return $null ⇒ 交给 verdict 当 unknown，不许当成"没装屏保"。
        static object ScreensaverConfigOrNull()
        {
            Dictionary<string, object> cfg = Gd.Map(Round.ScreensaverConfig(new Dictionary<string, object>()));
            if (cfg == null) return null;
            if (!Gd.Truthy(Gd.Get(cfg, "Found"))) return null;
            return Gd.Out(
                "Saver", Gd.Str(Gd.Get(cfg, "Saver")),
                "Product", Gd.Str(Gd.Get(cfg, "Product")),
                "Active", Gd.Str(Gd.Get(cfg, "Active")),
                "TimeoutSec", Gd.Get(cfg, "TimeoutSec"));
        }

        // 监督在岗三形：在岗 / 已收工 / 进程没了但没说收工。文件不在就一行都不占(不是错误)。
        // 11-02 刀③ 摘件笔：原来这里第一行还钉着"lib\supervisor.ps1 在不在盘上"(抄的是已删的 panel.ps1 那句 $haveSupervisor)。
        //   监督面迁到本 assembly 后，那枚 PS 件只是兜底壳，它摘与不摘都不该决定给人看的那一行占不占位——
        //   「绿着变空」比红难发现，所以闸门只留下一条：下面 ReadSupervisor() 在 state\supervision.json 缺席时本来就回 null。
        static GdSummaryRow SupervisorRow(Ctx c)
        {
            Dictionary<string, object> sv = ReadSupervisor();
            if (sv == null) return null;

            List<string> parts = new List<string>();
            bool odd = false;
            object[] beats = Gd.List(Gd.Get(sv, "beats"));
            if (beats.Length > 0)
            {
                foreach (object b in Gd.List(Gd.Get(beats[beats.Length - 1], "games")))
                {
                    string st = Gd.Str(Gd.Get(b, "state"));
                    if (st != "busy" && st != "done")
                    {
                        parts.Add(Gd.Str(Gd.Get(b, "game")) + "=" + st);
                        odd = true;
                    }
                }
            }
            string svTxt = string.Join(" ", parts.ToArray());
            int ivN = Gd.List(Gd.Get(sv, "actions")).Length;
            if (ivN > 0) svTxt += " · 介入 " + ivN.ToString(CultureInfo.InvariantCulture) + " 次";
            if (svTxt.StartsWith(" · ")) svTxt = svTxt.Substring(3);      // 只有缀句没有异常态时，别把分隔符留在行首

            bool alive = false;
            int pid = Gd.Int(Gd.Get(sv, "pid"), 0);
            if (pid != 0)
            {
                Process pr = null;
                try { pr = Process.GetProcessById(pid); alive = true; }
                catch { alive = false; }
                finally { if (pr != null) { try { pr.Dispose(); } catch { } } }   // PS 侧那句 $procSv.Dispose() 同形
            }

            string stopped = Gd.Str(Gd.Get(sv, "stopped"));
            if (stopped.Length > 0)
            {
                string when = stopped;
                string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (when.Length >= 19 && when.Substring(0, 10) == today) when = when.Substring(11, 8);
                return new GdSummaryRow(odd ? "warn" : "dim", "",
                    "监督：已收工 " + when + (svTxt.Length > 0 ? " · " + svTxt : ""), "supervisor");
            }
            if (alive)
                return new GdSummaryRow(odd ? "warn" : "ok", "",
                    "监督：在岗" + (svTxt.Length > 0 ? " · " + svTxt : ""), "supervisor");
            return new GdSummaryRow("err", "",
                "监督：进程没了但没说收工(最后心跳 " + Gd.Str(Gd.Get(sv, "updated")) + ") → 看 supervision.jsonl", "supervisor");
        }

        // Read-GdSupervisorSummary 的对面(那枚 PS 件 11-02 刀③ 退役，旧坐标锚按改口登记、不再指向具体行)：
        // 读不动／空文件／顶层不是对象都算"没监督过"，回 null。
        // FileShare 必须是 ReadWrite —— 监督进程正握着这份文件写下一拍，独占读会当场抛(和 history.csv 同一条坑)。
        static Dictionary<string, object> ReadSupervisor()
        {
            string p = Path.Combine(GdEnv.State, "supervision.json");
            if (!File.Exists(p)) return null;
            try
            {
                string txt = PanelForm.ReadAllShared(p);
                if (txt.Trim().Length == 0) return null;
                return Gd.Map(GdJson.Deserialize(txt));
            }
            catch { return null; }
        }

        // 展开详情的真身在 Supervisor.cs(批次 6)，PS 侧那枚 Get-GdSupervisorDetailLines 只是转发壳：
        // 壳里只剩两件现场活 —— 把 $Sv 原样喂进去、把日期戳以 date 注入(yyyyMMdd)；maxBeats 走壳的默认值 6。
        static object[] DetailLines(Dictionary<string, object> sv)
        {
            if (sv == null) return new object[0];
            Dictionary<string, object> d = Gd.Map(Supervisor.Detail(Gd.Out(
                "sv", sv,
                "maxBeats", 6,
                "date", DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture))));
            return d == null ? new object[0] : Gd.List(Gd.Get(d, "Lines"));
        }

        // ==================== PROBE：这一路自己的路径探针(原 panel.ps1 的 PROBE 段与 ENV 行那两段，已随件删除) ====================
        // 卡片顺序、标签措辞、"没填或不存在 → 跑 setup.ps1"那一句都由 Get-PanelModuleEnvRows 那段定；
        // 清单本体来自 lib\*-module.ps1 的 EnvProbes 钩子(取法见文件头第 3 条)，面板不另写一套路径知识。
        static List<GdSummaryRow> ModuleProbeRows(Ctx c, GdPanelModule m)
        {
            List<GdSummaryRow> rows = new List<GdSummaryRow>();
            // 三档分开，档位来自 Get-PanelModuleEnvRows 那两句 catch 的实际形状：
            //   settings.json 读不到(不在/坏) → PS 那句 `$S = Get-GdSettings -Quiet` 抛、$S 停在 null，
            //     随后 `-S $null` 撞 [Parameter(Mandatory)] 又抛 → 整段折成一行 warn「这一路的环境清单读不到」；
            //     这里不许"退化成全部没填"——那是另一种说法，会把"读不出"报成"玩家没配"。
            //   settings.json 在读得到，但某一项空/指错 → 逐项 err(玩家侧的话)，与 PS 的 Test-GdPathReady 同判据。
            if (c.Settings == null)
            {
                rows.Add(new GdSummaryRow("warn", m.Id, m.Id + " 这一路的环境清单读不到", ""));
                return rows;
            }
            Dictionary<string, object> paths = SubMap(c.Settings, "paths");
            foreach (string[] p in m.Probes)
            {
                string value;
                if (!ResolveProbe(p[1], paths, out value)) continue;      // 认不出的表达式 ⇒ 少一枚 PROBE，门里红
                if (GdEnv.PathReady(value)) rows.Add(new GdSummaryRow("ok", m.Id, p[0] + " 就绪", ""));
                else rows.Add(new GdSummaryRow("err", m.Id, p[0] + " 没填或不存在 → 跑 setup.ps1", ""));
            }
            return rows;
        }

        // ==================== HELP：按钮对应的命令行(原 panel.ps1 的 HELP 段，已随件删除) ====================
        // Need 里点名的文件有一个不在，这一行就不显示 —— 面板要原样进两仓，那边没有 start.cmd / tools\menu.ps1，
        // 照着提示敲等于去撞一个不存在的文件。顺序＝Add-PanelHelpRow 那十一枚调用的顺序，两枚条件行照条件走。
        static List<string[]> BuildHelp(Ctx c)
        {
            List<string[]> rows = new List<string[]>();
            string timesTxt = "HH:mm,HH:mm";      // 读不到 settings 的 times 时的那句占位(原 panel.ps1 的 HELP 段，已随件删除)
            if (c.Times.Count > 0) timesTxt = string.Join(",", c.Times.ToArray());

            Help(rows, c, "今天会跑什么", @".\" + c.EntryName + " -DryRun", new string[] { c.EntryName });
            Help(rows, c, "跑一轮", @".\run-via-task.ps1", new string[] { "run-via-task.ps1" });
            if (c.Games.Count > 1)
            {
                // 两款时正好等于原来那句"-Games ark   (或 r1999)"；三款以上把后面的并列进括号里，别套两层括号
                List<string> rest = new List<string>();
                for (int i = 1; i < c.Games.Count; i++) rest.Add(c.Games[i].Id);
                Help(rows, c, "只跑一路",
                     @".\" + c.EntryName + " -Games " + c.Games[0].Id + "   (或 " + string.Join(" / ", rest.ToArray()) + ")",
                     new string[] { c.EntryName });
            }
            Help(rows, c, "环境体检", @".\status.ps1", new string[] { "status.ps1" });
            Help(rows, c, "首次设置", @".\setup.ps1 -DryRun", new string[] { "setup.ps1" });
            Help(rows, c, "装定时", @".\install-task.ps1", new string[] { "install-task.ps1" });
            Help(rows, c, "改时间点", @".\tools\reschedule-daily.ps1 -Times " + timesTxt,
                 new string[] { @"tools\reschedule-daily.ps1" });
            if (c.FlagAck.Length > 0)
                Help(rows, c, "我打过活动了", @".\" + c.EntryName + " -" + c.FlagAck + " -DryRun", new string[] { c.EntryName });
            if (c.FlagShopDone.Length > 0)
                Help(rows, c, "商店领空", @".\" + c.EntryName + " -" + c.FlagShopDone + " -DryRun", new string[] { c.EntryName });
            Help(rows, c, "图形面板", @".\panel.cmd   (或 exe 加 --panel)", new string[] { "panel.cmd" });
            Help(rows, c, "命令行菜单", @".\start.cmd   (双击=打开这个菜单)", new string[] { "start.cmd", @"tools\menu.ps1" });
            return rows;
        }

        static void Help(List<string[]> rows, Ctx c, string label, string cmd, string[] need)
        {
            foreach (string n in need)
                if (n.Length == 0 || !File.Exists(Path.Combine(c.Root, n))) return;
            rows.Add(new string[] { label, cmd });
        }

        // ==================== 小工具 ====================
        static Dictionary<string, object> SubMap(Dictionary<string, object> m, string key)
        {
            if (m == null) return null;
            return Gd.Map(Gd.Get(m, key));
        }

        static string Join(List<string> items, string sep)
        {
            return string.Join(sep, items.ToArray());
        }

        static string EngineName(string engine)
        {
            if (string.IsNullOrEmpty(engine)) return "";
            try { return Path.GetFileName(engine); } catch { return ""; }
        }

        // 『面板 PS 7.x』那句的取值处(见文件头差 2)：读的是**要用的那颗引擎**的文件版本主.次，
        // 不是本进程 —— 本件是 .NET Framework 4 的 winexe，报它自己那串数字只会骗人。
        static string EnginePsVersion(string engine)
        {
            if (string.IsNullOrEmpty(engine)) return "读不出";
            try
            {
                string pv = FileVersionInfo.GetVersionInfo(engine).ProductVersion ?? "";
                Match mm = Regex.Match(pv, @"(\d+)\.(\d+)");
                if (mm.Success) return mm.Groups[1].Value + "." + mm.Groups[2].Value;
            }
            catch { }
            return "读不出";
        }

    }
}
