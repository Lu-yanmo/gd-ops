// tools/panel-cs/Gate.Host.cs —— 宿主批的地盘闸：MuMu 的 adb 端点定档与虚拟机探活。
//
// 这一件的来处是 lib\vision-mumu.ps1 的 Get-GdMuMuAdbEndpoint(lib\mumu.ps1 的 Get-GdMuMu 供路径算术)
// 与 lib\supervisor.ps1 的问画面那一段。搬它的原因是宿主批(Round.Host / Supervisor.Host)不许再经过
// powershell —— 端点取法住在 PS 就等于每一拍问画面都要起一枚 pwsh，而"问一次画面"本身是纯取数。
//
// 三源两正则的**顺序**照真身，不许调：① 调用方给的 Conn(实例配置里那一路真要用的设备)；
// ② MuMuManager 的 `adb -v <index>`；③ `adb devices` 里**恰好只有一台**时的那枚序列号。
// ② 的两副眼镜也照真身：先抠连续的 ip:port，落空再按 "adb_host"/"adb_port" 分字段拼回(09-30 实锤：
// 本机那版回的是分字段形态，只认前一副会把"虚拟机明明在跑"误报成 vm not running)。
// ③ 只在恰好一台时才认：挑错设备会把点击打进另一台模拟器实例，宁可不认。
// 绝不写死 16384/7555 这类端口 —— 换了 MuMu 版本或实例编号就错，本机专属值也不许进源码。
//
// ⚠ 2026-11-04 转接刀·运行面刀⑧ 兑现并改口：下面这段是原话，登记的是"先把名字占在这里等于登记一个
//   没有实现的出口"——本批把那一臂的直身真的写进来了，所以这三行按新形状重写：
// 本件放三样：端点与探活两枚(老账)、外发名册里 `updgate`／`audit` 两臂的直身(本批新立，出口＝
// verb run-m9a-update／run-1999-event-audit)、以及它们共用的那半段起进程＋挑行＋拼串。
// 两把闸的**本体**仍在 tools\m9a-update.ps1／tools\1999-event-audit.ps1(共 404 行，表账三枚"不许动")，
// 搬进 C# 的只有 lib\rust-round.ps1 那 23 行**壳**(起引擎、挑行、拼 joined、包 data)——把"摘臂"报成
// "退役那两本工具"就是把减法记成加法。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class GateHost
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // 两枚闸的内层上限(秒)。判据：更新闸走的是 M9A 整包下载，链体注释写着"整段最坏十几分钟"，
        // 所以这一档必须明显高于十几分钟；但它**必须低于**核侧 call_panel_verb 给这两枚 verb 配的等待上限，
        // 让"放弃这一枚闸"由内层先判出、归因清楚(外层掐是"verb 没回话"，内层掐是"闸自己跑到上限")。
        // 语义变更如实登记：老形里外层硬闸收尸整轮，新形里闸自己放弃、当日轮照跑 —— 与链体
        // "任何失败只 WARN 不挡当日轮"同向。具体秒数是本机现量，不是判据，改动请按链体那句"十几分钟"重估。
        const int GateRunBoundSeconds = 2400;

        sealed class ProcResult
        {
            public int Code = 0;
            public string Out = "";
            public string Err = "";
            public bool TimedOut = false;
        }

        // ==================== 公开面 ====================

        // Get-GdMuMuAdbEndpoint 的直身。返回 {Adb;Serial;Why}，Why 是能进日志的一句话。
        // conn 那一源由调用方喂(m9a 的实例配置)，没有就传 null。
        internal static Dictionary<string, object> MuMuEndpoint(Dictionary<string, object> S)
        {
            return MuMuEndpoint(S, null);
        }

        internal static Dictionary<string, object> MuMuEndpoint(Dictionary<string, object> S, Dictionary<string, object> conn)
        {
            string adb = "";
            if (conn != null) adb = Gd.Str(Gd.Get(conn, "adb")) ?? "";
            Dictionary<string, object> mm = MuMuPaths(S);
            if (adb.Length == 0) adb = Gd.Str(Gd.Get(mm, "Adb")) ?? "";
            // 到这儿就没了：paths.mumuAdb 存的是"相对 mumuDir 的文件名"，再兜一层只会把相对路径
            // 当 adb 用、报出一句看不懂的"命令不存在"(真身同一句守卫)。
            if (adb.Length == 0 || !File.Exists(adb))
                return Gd.Out("Adb", "", "Serial", "", "Why", "找不到 MuMu 自带的 adb(先跑 setup.ps1 填 paths.mumuDir)");

            string serial = "";
            if (conn != null) serial = Gd.Str(Gd.Get(conn, "serial")) ?? "";
            if (serial.Length == 0) serial = MuMuManagerSerial(mm);

            if (serial.Length == 0)
            {
                // 最后一次机会：已经连着的设备里挑一个，且只在恰好只有一台时才认(理由见文件头)。
                ProcResult dev = RunProc(adb, new string[] { "devices" }, 20);
                List<string> ons = new List<string>();
                foreach (Match m in Regex.Matches(dev.Out ?? "", @"^(\S+)\s+device\b", RegexOptions.Multiline)) ons.Add(m.Groups[1].Value);
                if (ons.Count == 1) serial = ons[0];
            }
            if (serial.Length == 0)
                return Gd.Out("Adb", adb, "Serial", "", "Why",
                    "拿不到 adb 序列号：虚拟机没在跑，或这版 MuMuManager 的 adb -v 输出形态两种认法都落空(09-30 已兜 JSON 分字段形态)");
            return Gd.Out("Adb", adb, "Serial", serial, "Why", "");
        }

        // 真身 `.Alive` 那三步(vision-mumu.ps1)的直身，与 Live 类内那枚 private 同判据、不同源不复用：
        // 先问 devices，序列号数得出来就直接算在场；数不出来才补一次 connect。
        // 不许合成一枚 —— 合了就是"每次探活都白跑一趟 connect"，而真身在设备本来就列着时一趟都不起。
        internal static bool MuMuVmAlive(string adb, string serial)
        {
            if (adb == null) adb = "";
            if (serial == null) serial = "";
            if (adb.Length == 0 || serial.Length == 0) return false;
            if (!File.Exists(adb)) return false;         // 真身：起不来 → 补 connect → 路径判据抛 → catch 回假
            ProcResult dev = RunProc(adb, new string[] { "devices" }, 20);
            if ((dev.Out ?? "").Contains(serial)) return true;
            ProcResult c = RunProc(adb, new string[] { "connect", serial }, 30);
            return c.Code == 0 && (c.Out ?? "").IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ==================== verb: resolve-mumu-adb-endpoint(运行面刀⑮ 笔(c)，2026-11-06) ====================
        // 这一枚不新写任何规则——它只是把上面早就在场的 MuMuEndpoint 直身(三源两正则)露成命令行出口，
        // 形制照"转接刀·时间换算族"与 build-round-ctx 的先例。settings 由本出口现读 config\settings.json，
        // **整块 settings 不进 body**(与刀⑪ 那条同一规矩)；body 的两枚旋钮只给自检夹具：
        //   settings = 注入假树；conn = 实例 json 那一源的 adb/serial。生产另一路消费者
        //   (Supervisor.Host.RequestScreen)仍是进程内直调，不经过这里。
        // PS 侧对面 Get-GdMuMuAdbEndpoint(tools\vision-mumu.ps1)随本刀笔(d) 摘件出仓。

        internal static object EndpointV(Dictionary<string, object> inp)
        {
            Dictionary<string, object> s = Gd.Map(Gd.Get(inp, "settings"));
            if (s == null) s = Round.SettingsNow();
            Dictionary<string, object> conn = Gd.Map(Gd.Get(inp, "conn"));
            return MuMuEndpoint(s, conn);
        }

        // ==================== 两把闸的臂直身(转接刀·运行面刀⑧) ====================

        // verb: run-m9a-update —— lib\rust-round.ps1 外发名册 'updgate' 那臂的 C# 直身。
        // 判据全部留在这里(脚本相对名、-Apply、挑行正则、内层上限)，**不从入参给**：
        // 入参一旦能改脚本名，"闸跑的是哪本工具"就不再是代码里的常量而是现场量，那正是这一族要删的东西。
        // 入参面只剩自检夹具三枚旋钮 noSpawn／fakeCode／fakeLines(身份同 Cli.cs 的 --garbage，真跑一侧不许送)。
        // 回执三枚键 Code／Line／Joined 用 PascalCase(与全工程其它 verb 同形)，核内答手按 jv_get_ci
        // 折成 PS 臂当年写进 res 的那三枚小写字面(code／line／joined)，journal 才两侧对得平 —— 键名一改，
        // 对拍当场分歧，不许"顺手统一"。
        internal static object M9aUpdgate(Dictionary<string, object> raw)
        {
            return GateRun(raw, @"tools\m9a-update.ps1", "已是最新|更新完成");
        }

        // verb: run-1999-event-audit —— 同臂的 'audit' 那一路，只换脚本名与挑行正则两枚常量。
        internal static object EventAudit(Dictionary<string, object> raw)
        {
            return GateRun(raw, @"tools\1999-event-audit.ps1", "审计结论|无事发生|维持 event-pending");
        }

        // 两枚臂共用的那半段：起引擎跑 -File <script> -Apply 并把 2>&1 的合流读数收成三格。
        // 与 PS 臂逐字对齐的四条：参数串(-NoProfile -ExecutionPolicy Bypass -File … -Apply)、
        // 挑行(在全部行里找**最后**一条命中正则的，挑不中回空串)、joined(全部行按 ' / ' 拼)、
        // 退出码($LASTEXITCODE 的同位：进程码，超时那一支不进这里而是抛)。
        // 分档照 PS 臂的 try/catch：引擎找不到／跑到内层上限 ⇒ 抛(GdRuleException ⇒ 退出码 2 ⇒
        //   核侧 call_panel_verb Err ⇒ 链体"闸本身抛错，跳过它照常跑"WARN)；
        //   脚本不在 ⇒ **不挡**，让子进程自己非 0 退出(PS 臂也没做 File.Exists 闸门)，走"退出码非 0"那条 WARN。
        static object GateRun(Dictionary<string, object> raw, string scriptRel, string pickPattern)
        {
            Dictionary<string, object> c = Round.View(raw);
            List<string> lines = new List<string>();
            int code = 0;
            if (Round.OptFlag(c, "noSpawn", delegate { return false; }))
            {
                code = (int)Round.OptLng(c, "fakeCode", 0);
                lines = Round.OptList(c, "fakeLines");
            }
            else
            {
                string engine = GateEngine(c);
                if (engine.Length == 0)
                    throw new GdRuleException("找不到 pwsh 引擎，这一枚闸不起(" + scriptRel + ")");
                string script = GdEnv.JoinPath(GdEnv.Root, scriptRel);
                ProcResult p = RunProc(engine, new string[]
                {
                    "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Apply"
                }, GateRunBoundSeconds, true);
                if (p.TimedOut)
                    throw new GdRuleException("闸跑到内层上限仍未退出，放弃它照常跑：" + scriptRel);
                code = p.Code;
                lines = SplitLines(p.Out);
            }
            return Gd.Out(
                "Code", code,
                "Line", PickLine(lines, pickPattern),
                "Joined", string.Join(" / ", lines.ToArray()));
        }

        // 引擎候选序。真身那臂用的是 `(Get-Process -Id $PID).Path` —— PS 宿主里那就是"跑这一轮的引擎"，
        // 而本件住在 panel-cs.exe 里，自路径是面板那枚 exe(与 Round.cs:362-366 那条警示同一件事)，
        // 且本机实测 PSHOME 不随子进程继承 ⇒ 调用方引擎这一级在 exe 宿主里根本取不到。
        // 于是这一枚按"不起进程只 -File"的用途另立候选序，且**允许商店版命中**(判据与形状差登记在
        // Round.LocalPwsh 的头注里：提权面仍旧跳商店版，这一面不跳，否则本机一枚都找不到 ⇒ 两枚闸恒抛＝回归)：
        //   ① 入参 pshome 喂的那一版(留给调用方与自检) → ② Program Files 的 7 →
        //   ③ %LOCALAPPDATA%\Microsoft\WindowsApps\pwsh.exe(商店版执行别名，本机实测在场且 -File 起得来) →
        //   ④ PATH 逐目录(与 Get-Command pwsh.exe 同形，允许商店版)。
        // 全都落空回空串，由调用方决定抛(照 Round.cs 那句"找不到 pwsh.exe"的先例)。
        static string GateEngine(Dictionary<string, object> c)
        {
            string eng = Round.LocalPwsh(Round.OptStr(c, "pshome", null), true);
            if (eng.Length > 0) return eng;
            string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(la))
            {
                string alias = Path.Combine(la, @"Microsoft\WindowsApps\pwsh.exe");
                try { if (File.Exists(alias)) return alias; } catch { }
            }
            return Round.FindOnPath("pwsh.exe", true);
        }

        // 合流文本按行拆。RunProc 的读手是逐行 AppendLine，所以尾部必然多出一枚幻影空元素
        // (line-count-split-trailing-empty 那条在册坑)——不剥，joined 末尾就多一个 ' / '、
        // 与 PS 臂 $gOut 的枚数不同轴。只剥末位这一枚：中间与真正末尾的空行照旧保留。
        // internal 只为 --svtest 能直接喂合流文本钉这一枚(纯函数、不起进程)，不是给生产面用的第二个出口。
        internal static List<string> SplitLines(string text)
        {
            List<string> outList = new List<string>();
            if (string.IsNullOrEmpty(text)) return outList;
            foreach (string s in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')) outList.Add(s);
            if (outList.Count > 0 && outList[outList.Count - 1].Length == 0) outList.RemoveAt(outList.Count - 1);
            return outList;
        }

        // PS 臂那句 `... | Select-Object -Last 1` 的同形：最后一条命中，挑不中回空串(不是 null)。
        // -match 不分大小写 ⇒ RegexOptions.IgnoreCase；正则字面与 PS 臂逐字相同，改一处就是改判据。
        static string PickLine(List<string> lines, string pattern)
        {
            string hit = "";
            Regex rx = new Regex(pattern, RegexOptions.IgnoreCase);
            foreach (string s in lines) if (rx.IsMatch(s)) hit = s;
            return hit;
        }

        // ==================== 取数段 ====================

        // Get-GdMuMu(lib\mumu.ps1)的直身：四条算术，不起任何进程。
        // ⚠ 2026-11-05 转接刀·运行面刀⑪：由 private 改 internal —— ctx 的 m9aRun.conn.adb 回落档要的就是
        //   这一格的 Adb(纯路径算术)。改的是可见性，四格算术一个字没动；另抄一份路径算术就是双账。
        internal static Dictionary<string, object> MuMuPaths(Dictionary<string, object> S)
        {
            Dictionary<string, object> paths = Gd.Map(Gd.Get(S, "paths")) ?? new Dictionary<string, object>();
            Dictionary<string, object> mumu = Gd.Map(Gd.Get(S, "mumu")) ?? new Dictionary<string, object>();
            string dir = Gd.Str(Gd.Get(paths, "mumuDir")) ?? "";
            return Gd.Out(
                "Manager", JoinRel(dir, Gd.Get(paths, "mumuManager")),
                "Adb", JoinRel(dir, Gd.Get(paths, "mumuAdb")),
                "Index", Gd.Str(Gd.Get(mumu, "vmIndex")) ?? "",
                "Name", Gd.Str(Gd.Get(mumu, "vmNameHint")) ?? "");
        }

        // paths 里那两枚既写成一枚字符串也写成一段清单都算对(与 Join-GdPath 的 @(...) 同口径)：取第一枚。
        static string JoinRel(string dir, object rel)
        {
            string r = "";
            object[] list = Gd.List(rel);
            if (list != null && list.Length > 0) r = Gd.Str(list[0]) ?? "";
            else r = Gd.Str(rel) ?? "";
            if (dir.Length == 0) return r;
            if (r.Length == 0) return "";
            return GdEnv.JoinPath(dir, r);
        }

        // ② 那一源：MuMuManager `adb -v <index>` 的两副眼镜。拿不到回空串(不抛)。
        static string MuMuManagerSerial(Dictionary<string, object> mm)
        {
            string manager = Gd.Str(Gd.Get(mm, "Manager")) ?? "";
            if (manager.Length == 0 || !File.Exists(manager)) return "";
            ProcResult r = RunProc(manager, new string[] { "adb", "-v", Gd.Str(Gd.Get(mm, "Index")) ?? "" }, 30);
            string txt = (r.Out ?? "") + (r.Err ?? "");
            Match hit = Regex.Match(txt, @"\b\d{1,3}(?:\.\d{1,3}){3}:\d{2,5}\b");
            if (hit.Success) return hit.Value;
            Match h = Regex.Match(txt, @"""adb_host(?:_ip)?""\s*:\s*""([^""]+)""");
            Match p = Regex.Match(txt, @"""adb_port""\s*:\s*""?(\d+)""?");
            if (h.Success && p.Success) return h.Groups[1].Value + ":" + p.Groups[1].Value;
            return "";
        }

        // 起一枚短命令并收三格读数。stdout/stderr 各一根线程收(先挂读手再等退出)，
        // 否则输出满了管道就是死锁 —— PS 侧那两枚现出口防的是同一件事，这里不靠超时侥幸。
        static ProcResult RunProc(string file, string[] args, int timeoutSeconds)
        {
            return RunProc(file, args, timeoutSeconds, false);
        }

        // merge=true：stderr 与 stdout 收进**同一根**缓冲(同一把锁、按到达次序追加)，
        // 对回 PS 臂那句 `… 2>&1` 的交织序；此时 res.Err 恒空串，行全在 res.Out 里。
        // 不要默认合流：老那几格(adb devices/connect、MuMuManager adb -v)读的是分开的两桶
        // (MuMuManagerSerial 就是 stdout+Err 拼起来再抠的)，合了会把"两桶各扫"变成"一桶扫"。
        static ProcResult RunProc(string file, string[] args, int timeoutSeconds, bool merge)
        {
            ProcResult res = new ProcResult();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(file);
                // .NET Framework 4.0 的 ProcessStartInfo 没有 ArgumentList(那是后来自带运行时才有的)，
                // 只能手拼这一串：每枚参数一律加双引号，空参数干脆不拼(拼进去会多出一枚空位参数)。
                StringBuilder cl = new StringBuilder();
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? "";
                    if (a.Length == 0) continue;
                    if (cl.Length > 0) cl.Append(' ');
                    cl.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
                }
                psi.Arguments = cl.ToString();
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    StringBuilder so = new StringBuilder();
                    StringBuilder se = merge ? so : new StringBuilder();
                    object gate = new object();
                    p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        lock (gate) so.AppendLine(e.Data);
                    };
                    p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) return;
                        lock (gate) se.AppendLine(e.Data);
                    };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutSeconds * 1000))
                    {
                        res.TimedOut = true;
                        try { p.Kill(); } catch { }
                        res.Code = -1;
                    }
                    else
                    {
                        // WaitForExit(带超时)只保证到"退出信号"，异步读手可能还有尾巴没吐完：
                        // 不带参数的这一次才是"流也收干净了"，只在已经退出的那一支做。
                        p.WaitForExit();
                        res.Code = p.ExitCode;
                    }
                    lock (gate) { res.Out = so.ToString(); res.Err = se.ToString(); }
                }
                return res;
            }
            catch (Exception ex)
            {
                res.Code = -1;
                res.Err = ex.Message;
                return res;
            }
        }
    }
}
