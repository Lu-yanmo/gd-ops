// tools/panel-cs/Round.cs —— B-3②-a(2026-10-13)：轮骨架与入口面的**现场件**七枚 verb
// (切分与理由见 docs\rust-core-plan.md §11.50 ②-1，题面＝那里那句"现场五枚 verb ＋ 一枚 ctx verb，全部新立")。
//
// 这一份管的是**日常一轮**跑起来时**必须碰这台机器**的那几件事：读 settings 取键、
// 真起提权子进程并等退出码、发通知、登记/撤掉运行锁、读计划任务状态、按名收尾。
// 它当年的对面是 `lib\modules.ps1` 的 `Invoke-GdDailyRound`(§11.50 那句"坐标口径钉 HEAD"量的就是它)——那枚十段真身
// 与它的逐路执行器已于 2026-10-20 乙批② 划掉(§11.105)，本件从此**不引那两枚的行号**，十段的基线只剩轮骨架门钉住的 git blob。
// Rust 核(②-b 的 `--daily-round`)只做骨架编排，这七件事一律外调本文件 —— 与 B-2b 七枚、B-2d③ 四枚同一条定档：
// **Windows 交互与本机值留在 C# 这一侧，不进核**。
//
// 七枚名单(全部走 `--in` JSON，回执走 `--out` 信封，形状与既有六十几枚一字不相干 —— 判据见 §11.50 ②-1 末句)：
//   resolve-round-context  ①-4 那把"轮 ctx 取键名册"：逐枚点名要 runtime 键、判动态 flag、判在位性、判管理员；
//   start-elevated-round   Start-GdElevatedSelf(common.ps1:771) 的真起那半 —— 拼参那半不重做(build-relaunch-args 已在场)；
//   send-round-notice      Send-GdNotice(common.ps1 已摘于本批) 的三档容错：落盘无条件、webhook 失败只 WARN、气泡失败也只 WARN；
//   start-run-lock         Start-GdRunLock(common.ps1:902)：先报"已有一轮在跑"再登记，**只说不拦**；
//   stop-run-lock          Stop-GdRunLock(common.ps1:959)：只删自己登记的那一份；
//   read-task-state        Get-GdTaskState(common.ps1:1031)：走 Schedule.Service COM，不走 schtasks(它的状态标签是本地化的)；
//   stop-process-tree      Stop-GdProcessTree(common.ps1:478)：名曰"树"、实为按进程名(原样怪癖，见下)。
// 睡眠屏蔽(SetThreadExecutionState)**不在这份名册里**：跨进程会当场语义失效(①-8)，由 ②-b 的宿主在那根常驻线程上自己设。
//
// 双源登记(§11.50 ⑤ 那一族，改一句要两侧同改)：
//   * 上面每一句 WARN/ERROR 原话与 modules.ps1 的旧路**并存**：生产接线(②-d)之前旧路仍是事实源，
//     本文件抄它，不许反过来"以这里为准"去改 lib；
//   * `Get-GdElevateBatchLine`(common.ps1:768) 那枚纯函数仍住壳侧(①-6 明写"本批不重造拼参"，但批处理那一行
//     只有真起 cmd 载体才用得到)：这里的 BatchLine 是它的同形复刻，两侧若要长期并存就得等 B-4 退役那批再收；
//   * 运行锁的**读侧**在本文件收成了唯一真身(ParseLock/LockAt)，`Panel.Run.cs:ReadExtRun` 已改为委托它
//     —— 这是 §11.50 ⑤-2 那句"写侧与读侧是同一条契约，不许分两批动"的兑现处，随行自证见 --exttest 与 --roundtest。
//   * **batchPwsh 的默认值是与真身有意分岔的一处**(本批 CLI 冒烟抓到)：真身第一级取
//     `(Get-Process -Id $PID).Path`(common.ps1:800)，那句在 PS 宿主里取到的是"跑这个脚本的解释器"；
//     本 verb 住在 panel-cs.exe 里，照抄会得到**面板自己的绝对路径** ⇒ 批处理行拼成
//     `panel-cs.exe -File x.ps1`，而面板认不得 `-File`，提权起来是一发空打。这里把三级折成两级
//     (Get-GdLocalPwsh 那枚 → 按 PATH 找 pwsh.exe，商店版同样跳过)，两级都空仍照 common.ps1:802 那句原话抛。
//     真弹那一发(载体 cmd 那一支)仍登记 unobserved，等 ②-b 的 UAC 实弹。
//
// 原样怪癖(§11.50 ⑦ 登记的几笔，这里一律照抄不"顺手修")：
//   1. **通知标题的 `${NoticeOwner}`**：大括号不可省(CJK 是合法变量名字符，`lib\modules.ps1` 里那段注释记着实机踩过一次
//      "展开成空串 → Mandatory Title 绑不上"；那段注释已随乙批②(`e3dec2b`)删十段真身一起出仓 ⇒ 码面自此只剩本行这一处记载(docs 面 `§11.50 ⑦` 那笔另记一句，而它引的正是出仓前的那枚码面注释，见上一行)，按坐标清账批口径不引行号)。宿主版不插值，直接按 owner 拼同一句 ⇒ NoticeTitle 那枚内部件被 --roundtest 钉死。
//   2. **残留按"没在跑"**：Get-GdRunLock 的码面注释写死了理由——"否则一次硬杀会把面板永久挡在门外"。
//      这里不许把残留判成"仍在跑"；同时面板那侧多一条它自己的规矩(读不到进程细节时宁可当成在跑，防二次点火)，
//      两把尺子由 LockAt(unhandledMeansAlive) 这一个布尔分开，**不统一**。
//   3. **own 判据不分大小写**：`"pid"\s*:…` 那条 -match 是不分大小写的，所以 {"PID":1234} 也算自己那份
//      (common.ps1:964 的 K2 实测注记)⇒ OwnsText 用 RegexOptions.IgnoreCase。
//   4. **Stop-GdProcessTree 名曰树、实为按进程名**：Get-Process -Name 匹配几个就返回几，
//      不递归子进程 ⇒ 回执里的 Killed 就是"匹配到的进程数"这个原样语义，别当成"真杀了几枚"。
//   5. **提权支让位**：骨架 `return [int]$code` 直接把子轮退出码当本轮结论(父轮不写历史、不发通知)。
//      ⇒ 本枚只回 Code，**不做任何"替子轮汇总"的事**。
//   6. **`$S.runtime.$flag -ne $false` 由左侧类型决定怎么折 $false**：左侧是串 ⇒ 折成 "False" 按串比 ⇒
//      字符串 "0" 算**开着**(不是关掉)；左侧是数 ⇒ 折成 0 ⇒ 只有 0 算关。FlagEnabled 按类型分支复刻这一格，
//      --roundtest 里那一格就是它第一版(把 TryParse 摊到字符串上判反了)的守门。
//
// 安全边界(自检纪律，与 Live.cs 同一条)：
//   * --roundtest **不真弹 UAC、不真打 webhook、不起气泡、不真杀进程、不读真计划任务**；
//     现场量一律夹具注入(旋钮身份同 Cli.cs 的 --garbage)，按面分：
//       ctx 面   settings / paths / runtimeKeys / elevFlags / pathKeys / isAdmin / now；
//       提权面   localPwsh / pshome / batchPwsh / workdir / argList / script / omit / force / entries / noSpawn / fakeCode；
//       通知面   owner / title / text / level / nowText / settings / noSend / fakeWebhookErr / fakeToastErr；
//       锁与现场 pid / games / tag / log / started / name / fake(任务) / taskPath / fakeProcs / noKill / force / graceSeconds；
//   * 漏喂夹具就会去读真配置 ⇒ 当场红，这就是"只读自检不许要求机器现场状态"的守门方式；
//     (自检唯一一次起子进程是拿一个**已退出的 PID**那一发 `cmd /d /c exit`，与 --exttest 的同名助手同一先例；
//      判活两把尺的分岔只在"读不到进程细节"那一档，自检造不出那种进程 ⇒ 登记 unobserved)；
//   * webhook URL、任务路径、日志绝对值**不进信封**(只有异常原话进 Lines，与 PS 侧同形)；
//     API key 与本文件无关(那七枚在 Live.cs)。
//   * 落盘的两枚(state\last-notice.json、state\running.json)按 GdEnv.State 走，自检打在 InitForTest 的临时树上。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class Round
    {
        // ==================== 入参取数：缺键=自己现读(现场)，有键=用夹具(与 Live.cs 同一套) ====================
        // ⚠ 2026-11-04 刀⑧：这一族六枚由 private 改 internal —— Gate.Host.cs 的两枚闸臂直身吃同一套入参形状。
        //   改的是可见性，判据一个字没动；另起一份"只给闸用"的取数件就是本主线一直在删的那本双账。

        internal static Dictionary<string, object> View(Dictionary<string, object> raw)
        {
            Dictionary<string, object> d = Gd.Ci(raw);
            if (d == null) d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            return d;
        }

        static bool Given(Dictionary<string, object> c, string k)
        {
            object v;
            return c.TryGetValue(k, out v) && v != null;
        }

        internal static string OptStr(Dictionary<string, object> c, string k, Func<string> def)
        {
            object v;
            if (c.TryGetValue(k, out v) && v != null) return Gd.Str(v);
            return def == null ? "" : def();
        }

        internal static bool OptFlag(Dictionary<string, object> c, string k, Func<bool> def)
        {
            object v;
            if (c.TryGetValue(k, out v)) return Gd.Truthy(v);
            return def == null ? false : def();
        }

        internal static long OptLng(Dictionary<string, object> c, string k, long def)
        {
            object v;
            if (!c.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        internal static List<string> OptList(Dictionary<string, object> c, string k)
        {
            object v;
            if (!c.TryGetValue(k, out v)) return new List<string>();
            List<string> outList = new List<string>();
            foreach (object o in Gd.List(v)) outList.Add(Gd.Str(o));
            return outList;
        }

        static Dictionary<string, object> Sub(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        // ==================== settings：现场读只在该读时才读(缺 settings 夹具才会落到真盘) ====================

        static Dictionary<string, object> settingsCache;

        // ⚠ 2026-11-05 转接刀·运行面刀⑪：这一枚由 private 改 internal —— Round.Host.cs 的 ctx 直身吃同一份
        //   settings 事实源。改的是可见性，判据一个字没动；另起一份"只给 ctx 用"的读数件就是本主线在删的双账。
        internal static Dictionary<string, object> SettingsNow()
        {
            if (settingsCache != null) return settingsCache;
            List<string> missing;
            // quiet=true ＋ requirePaths 给空表：这一枚只取键、不催助手目录(催是调用方 Stop-GdBeforeRun 的事)。
            // 缺 settings.json 时 GdSettings.Load 抛 FileNotFoundException ⇒ Cli 换算成退出码 2(前置不满足这一轮没跑)。
            settingsCache = GdSettings.Load(out missing, true, new List<string>());
            return settingsCache;
        }

        static object RuntimeOf(Dictionary<string, object> c, string key)
        {
            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "settings"));
            Dictionary<string, object> s = given != null ? given : SettingsNow();
            return Gd.Get(Gd.Get(s, "runtime"), key);
        }

        // PS 的 `$S.runtime.$flag -ne $false` 语义(10-13 现量，写码依据)：
        //   缺键/null/空串/'0' 字符串 → 真(算开着)；$false / 0 / 'false' 字符串 → 假(明确关掉)。
        // ⇒ "没填"和"填了 0 字符串"都开着，只有**真值意义上的 false** 才算关。别按 C# 的 != 直觉写。
        // ⚠ 关键在"-ne 由**左侧**的类型决定怎么折 $false"(PS 比较运算的规矩)：
        //   左侧是串 ⇒ $false 折成 "False" 按串比 ⇒ 除了 "false"(不分大小写)以外全都算开着，**"0" 这一格是开着**；
        //   左侧是数 ⇒ $false 折成 0 ⇒ 只有 0 算关；左侧是布尔 ⇒ 就是它本身。
        //   写码时先按类型分支再判，把数字支的 TryParse 摊到字符串上就会把 "0" 判成关掉(与旧路反向，--roundtest 钉这一格)。
        internal static bool FlagEnabled(object v)
        {
            if (v == null) return true;
            if (v is bool) return (bool)v;
            if (v is string) return !((string)v).Equals("false", StringComparison.OrdinalIgnoreCase);
            double d;
            if (double.TryParse(Gd.Str(v), NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d != 0.0;
            return true;
        }

        static bool IsAdminNow()
        {
            // Test-GdAdmin 的形状：读不到就当不是(宁可这一轮不提权，也别谎报已经提了)。
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // ==================== 运行锁：读侧唯一真身(Panel.Run.cs 已委托这里，见文件头双源登记) ====================

        internal sealed class Lock
        {
            public bool Found;            // false = 压根没有标记，或标记里没有 pid(PS 的 Get-GdRunLock 回 $null 那一格)
            public bool Alive;
            public int Pid;
            public string Started = "";
            public string Games = "";
            public string Tag = "";
            public string Log = "";
        }

        static readonly Regex LockPidRe = new Regex(@"""pid""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
        static readonly Regex LockLogRe = new Regex(@"""log""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.IgnoreCase);
        static readonly Regex LockStartedRe = new Regex(@"""started""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
        static readonly Regex LockGamesRe = new Regex(@"""games""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
        static readonly Regex LockTagRe = new Regex(@"""tag""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);

        // 纯：从 running.json 的原文抽四格＋tag。怪癖照抄 Panel.Run.cs 那三行：log 的反斜杠是**双重转义还原**，
        // 抽出来的串要能直接 File.Exists(--exttest 拿真路径钉过这一格，改成 JSON 解析就换了语义)。
        internal static Lock ParseLock(string text)
        {
            Lock lk = new Lock();
            if (text == null) return lk;
            Match mp = LockPidRe.Match(text);
            if (!mp.Success) return lk;                 // 没有 pid ⇒ 与"没标记"同格(PS：if ($null -eq $o.pid) { return $null })
            lk.Found = true;
            try { lk.Pid = int.Parse(mp.Groups[1].Value, CultureInfo.InvariantCulture); } catch { lk.Found = false; return lk; }
            Match ml = LockLogRe.Match(text);
            if (ml.Success) lk.Log = ml.Groups[1].Value.Replace("\\\\", "\\");
            Match mt = LockStartedRe.Match(text); if (mt.Success) lk.Started = mt.Groups[1].Value;
            Match mg = LockGamesRe.Match(text); if (mg.Success) lk.Games = mg.Groups[1].Value;
            Match mg2 = LockTagRe.Match(text); if (mg2.Success) lk.Tag = mg2.Groups[1].Value;
            return lk;
        }

        // 判活的两把尺(见文件头原样怪癖第 2 笔)：进程不在 → 假；读不到细节时面板宁可当真、PS 当真不了。
        static bool PidAlive(int pid, bool unhandledMeansAlive)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid)) return !p.HasExited;
            }
            catch (ArgumentException) { return false; }
            catch { return unhandledMeansAlive; }
        }

        // 读盘＋判活。path 为空/读不动一律"没标记"(与旧 PS 的 catch { return $null } 同形)。
        internal static Lock LockAt(string path, bool unhandledMeansAlive)
        {
            Lock lk = new Lock();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return lk;
                lk = ParseLock(File.ReadAllText(path));
                if (!lk.Found) return lk;
                lk.Alive = PidAlive(lk.Pid, unhandledMeansAlive);
                return lk;
            }
            catch { lk.Found = false; lk.Alive = false; return lk; }
        }

        // "只删自己登记的那一份"：别的轮已经覆盖了它，删就等于把别人的撞车保护拆了。
        internal static bool OwnsText(string text, int pid)
        {
            if (text == null) return false;
            try
            {
                return Regex.IsMatch(text, @"\""pid\""\s*:\s*" + pid + @"[\s,}]", RegexOptions.IgnoreCase);
            }
            catch { return false; }
        }

        static string LockPath { get { GdEnv.Init(null); return Path.Combine(GdEnv.State, "running.json"); } }

        // ==================== verb: resolve-round-context ====================
        // ①-12 结案的落点：核不读盘，轮 ctx 走这一枚。逐枚点名取键(与 maaRun 十三枚/m9aRun 十五枚同一先例)，
        // 缺键一律回 null 让调用方按"没填"处理 —— **整块 runtime 绝不回给核**(visionApiKey 就住在 runtime 里)。
        internal static object Context(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);

            // 要哪些 runtime 键由调用方点名；给了空表就是空表(不许"顺手"把整块 runtime 递出去)。
            List<string> wantRuntime = OptList(c, "runtimeKeys");
            Dictionary<string, object> runtime = new Dictionary<string, object>();
            foreach (string k in wantRuntime) runtime[k] = RuntimeOf(c, k);

            // 动态 flag 名册(清单声明的 ElevationFlag，值形如 'arkRequiresElevation')：把 -ne $false 那一步在这侧算完，
            // 免得 Rust 侧再抄一份 PowerShell 的真值语义。
            List<string> flags = OptList(c, "elevFlags");
            Dictionary<string, object> elevation = new Dictionary<string, object>();
            foreach (string f in flags) elevation[f] = FlagEnabled(RuntimeOf(c, f));

            // 在位性那把尺(①-13)：不新建第三棵名册，就用 Get-GdRequiredPathKeys —— 这棵树上真装了哪几路的库。
            List<string> keys = OptList(c, "pathKeys");
            if (keys.Count == 0) keys = GdSettings.RequiredPathKeys();
            Dictionary<string, object> pathsSrc = Gd.Map(Gd.Get(c, "paths"));
            if (pathsSrc == null)
            {
                Dictionary<string, object> s = Gd.Map(Gd.Get(c, "settings"));
                object pObj = s != null ? Gd.Get(s, "paths") : Gd.Get(SettingsNow(), "paths");
                pathsSrc = Gd.Map(pObj);
            }
            Dictionary<string, object> ready = new Dictionary<string, object>();
            Dictionary<string, object> paths = new Dictionary<string, object>();
            List<object> missing = new List<object>();
            List<object> absent = new List<object>();
            foreach (string k in keys)
            {
                object v = pathsSrc == null ? null : Gd.Get(pathsSrc, k);
                string p = Gd.Str(v);
                paths[k] = p.Length == 0 ? null : (object)p;
                bool isReady = GdEnv.PathReady(p);
                ready[k] = isReady;
                if (p.Length == 0) missing.Add(k);
                else if (!isReady) absent.Add(k + " → " + p);
            }

            long nowMs = OptLng(c, "now", NowMs());
            DateTime local;
            try { local = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).LocalDateTime; }
            catch { local = DateTime.Now; }

            return Gd.Out(
                "Runtime", runtime,
                "Elevation", elevation,
                "IsAdmin", OptFlag(c, "isAdmin", delegate { return IsAdminNow(); }),
                "RequiredPathKeys", keys.ToArray(),
                "Paths", paths,
                "Ready", ready,
                "Missing", missing.ToArray(),
                "Absent", absent.ToArray(),
                "NowMs", nowMs,
                "NowText", local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                "Dow", (int)local.DayOfWeek);
        }

        static long NowMs()
        {
            try { return new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds(); }
            catch { return 0; }
        }

        // ==================== verb: start-elevated-round ====================
        // Start-GdElevatedSelf 的真身两支照抄：本机 pwsh 优先 -Verb RunAs；失败落 cmd 载体；两支都不成就 -1。
        // 拼参那半**不重做**：给了 argList 就用它，否则直调同装配里的 Processes.RelaunchArgs(verb build-relaunch-args)。
        internal static object Elevate(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            List<string> args = new List<string>();
            string script = OptStr(c, "script", null);
            if (Given(c, "argList")) args = OptList(c, "argList");      // 给了就用它，**空数组也算给**(拼好的参数可以一个都没有)
            else
            {
                if (script.Length == 0)
                    throw new GdRuleException("start-elevated-round：既没给 argList，也没给 script(拼参那半要么已经拼好，要么交回 build-relaunch-args)");
                Dictionary<string, object> built = Processes.RelaunchArgs(Sub(
                    "script", script,
                    "omit", Gd.Get(c, "omit"),
                    "force", Gd.Get(c, "force"),
                    "entries", Gd.Get(c, "entries"))) as Dictionary<string, object>;
                foreach (object o in Gd.List(built == null ? null : built["Args"])) args.Add(Gd.Str(o));
            }
            string workdir = OptStr(c, "workdir", delegate
            {
                if (script.Length == 0) return "";
                try { return Path.GetDirectoryName(Path.GetFullPath(script)) ?? ""; } catch { return ""; }
            });

            string local = OptStr(c, "localPwsh", delegate { return LocalPwsh(OptStr(c, "pshome", null), false); });
            bool noSpawn = OptFlag(c, "noSpawn", delegate { return false; });
            int fakeCode = (int)OptLng(c, "fakeCode", 0);
            List<object> lines = new List<object>();
            string carrier;
            int code;

            string batchPwsh = OptStr(c, "batchPwsh", delegate
            {
                // ⚠ 这里**不许**照抄真身的第一级(Get-Process -Id $PID).Path：那句在 PS 宿主里取到的是
                // "跑这个脚本的解释器"(pwsh.exe)，而本 verb 住在 panel-cs.exe 里，自路径就是面板那枚 exe，
                // 写进批处理行得到 "panel-cs.exe -File x.ps1" —— 面板认不得 -File，提权起来是一发空打。
                // 于是 exe 宿主把三级顺序折成两级：Get-GdLocalPwsh 那枚 → 按 PATH 找 pwsh.exe(Get-Command 的同形)，
                // 都没有就照 common.ps1:802 那句原话抛(退出码 2，原话给壳继续 throw)。
                if (local.Length > 0) return local;
                string onPath = FindOnPath("pwsh.exe", false);
                if (onPath.Length > 0) return onPath;
                throw new GdRuleException("找不到 pwsh.exe，无法提权重跑");
            });
            string line = BatchLine(batchPwsh, args);

            if (noSpawn) { carrier = local.Length > 0 ? "pwsh" : "cmd"; code = fakeCode; }
            else if (local.Length > 0)
            {
                carrier = "pwsh";
                try { code = RunElevated(local, string.Join(" ", args.ToArray()), workdir, false); }
                catch (Exception ex)
                {
                    // 原话逐字(common.ps1:796)：商店版 pwsh 提不了就是走这一支落进 cmd 载体的。
                    string msg = "本机 pwsh 提权失败(" + ex.Message + ") → 改用 cmd 载体";
                    GdLog.Line(msg, "WARN");
                    lines.Add(msg);
                    carrier = "cmd";
                    code = RunCmdCarrier(line, c, lines);
                }
            }
            else { carrier = "cmd"; code = RunCmdCarrier(line, c, lines); }

            return Gd.Out("Code", code, "Carrier", carrier, "BatchLine", line, "Lines", lines.ToArray());
        }

        // cmd 载体那支：临时 .cmd 四行 ascii → cmd.exe /d /c "<tmp>" 提权隐藏跑 → 无论如何删 tmp。
        // (⑥ Q3 判"exe 宿主不再需要这一支"是**推断**，所以留着；真弹那一发要等 ②-b 的 UAC 实弹，本批记 unobserved。)
        static int RunCmdCarrier(string line, Dictionary<string, object> c, List<object> lines)
        {
            string tmp = Path.Combine(Path.GetTempPath(),
                "gd-elevate-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".cmd");
            int code = -1;
            try
            {
                StringBuilder body = new StringBuilder();
                body.Append("@echo off\r\n");
                body.Append("chcp 65001 >nul\r\n");
                body.Append(line).Append("\r\n");
                body.Append("exit /b %ERRORLEVEL%\r\n");
                File.WriteAllText(tmp, body.ToString(), Encoding.ASCII);
                string root = Environment.GetEnvironmentVariable("SystemRoot");
                if (string.IsNullOrEmpty(root)) root = @"C:\Windows";
                code = RunElevated(Path.Combine(root, @"System32\cmd.exe"), "/d /c \"" + tmp + "\"", "", true);
            }
            catch (Exception ex)
            {
                // 原话逐字(common.ps1:813)：UAC 被取消也走这里 ⇒ -1 这一个码混着"取消"与"失败"两种意思，照抄不拆。
                string msg = "提权被取消或失败：" + ex.Message;
                GdLog.Line(msg, "ERROR");
                lines.Add(msg);
                code = -1;
            }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
            return code;
        }

        // Get-GdLocalPwsh 的同一条顺序：调用引擎那一版优先(由 pshome 喂进来，exe 宿主没有这一层就不喂)，
        // 其次 Program Files 的 7。
        // ⚠ 2026-11-04 转接刀·运行面刀⑧：这一枚自此带 allowStore 参数，**不再只管提权那一路**。
        //   allowStore=false(提权面，一字未改的老规矩)：WindowsApps(商店版)一律跳过——它 runas 提不了，
        //     正是 cmd 载体存在的理由。
        //   allowStore=true(不起进程的 -File 直跑面，两枚闸臂用)：商店版**算命中**。判据变更的现拍依据：
        //     本机 PSHOME 不随子进程继承(实测 ENV_PSHOME 为空)，而本机唯一在场的 pwsh 7 就是商店版
        //     (%LOCALAPPDATA%\Microsoft\WindowsApps\pwsh.exe 那枚执行别名，实测 -NoProfile -File 起得来、
        //     退出码回传正常)。若照搬 false 那一档，两枚闸在这台机器上一枚引擎都找不到 ⇒ 恒落
        //     "闸本身抛错"WARN ＝行为回归。这两枚闸不提权，商店版对 -File 完全合法，所以这一岔按用途分档。
        internal static string LocalPwsh(string pshome, bool allowStore)
        {
            List<string> cands = new List<string>();
            if (!string.IsNullOrEmpty(pshome)) cands.Add(Path.Combine(pshome, "pwsh.exe"));
            string pf = Environment.GetEnvironmentVariable("ProgramFiles");
            if (string.IsNullOrEmpty(pf)) pf = @"C:\Program Files";
            cands.Add(Path.Combine(pf, @"PowerShell\7\pwsh.exe"));
            foreach (string cand in cands)
            {
                try
                {
                    if (cand.Length == 0 || !File.Exists(cand)) continue;
                    if (!allowStore && Regex.IsMatch(cand, @"\\WindowsApps\\", RegexOptions.IgnoreCase)) continue;
                    return cand;
                }
                catch { }
            }
            return "";
        }

        // (Get-Command pwsh.exe -ErrorAction SilentlyContinue).Source 的同形：按 PATH 逐目录找那枚 exe，
        // 找不到回空串(由调用方决定照旧抛)。商店版 WindowsApps 允不允许命中由 allowStore 决定，
        // 理由与 LocalPwsh 同一条(提权面不许，-File 直跑面许)。
        internal static string FindOnPath(string exeName, bool allowStore)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path)) return "";
            foreach (string dir in path.Split(';'))
            {
                if (dir.Trim().Length == 0) continue;
                string cand;
                try { cand = Path.Combine(dir.Trim(), exeName); } catch { continue; }
                try
                {
                    if (!File.Exists(cand)) continue;
                    if (!allowStore && Regex.IsMatch(cand, @"\\WindowsApps\\", RegexOptions.IgnoreCase)) continue;
                    return cand;
                }
                catch { }
            }
            return "";
        }

        // Get-GdElevateBatchLine(common.ps1:768) 的同形复刻：带空格才加引号，参数用一个空格拼。
        internal static string BatchLine(string pwsh, List<string> argList)
        {
            string exe = Regex.IsMatch(pwsh ?? "", @"\s") ? "\"" + pwsh + "\"" : pwsh;
            return exe + " " + string.Join(" ", argList.ToArray());
        }

        // 真起一发提权：UseShellExecute=true 是 -Verb RunAs 的前提(所以这一支一个字都不能重定向)。
        // ⚠ 未实测项(②-b 的 UAC 实弹要验三件事：ExitCode 读不读得到、取消那一支的异常原话、-1 的语义)：
        //   这里读不到 ExitCode 就抛，让它照真身那样落进"本机 pwsh 提权失败 → 改用 cmd 载体"。
        static int RunElevated(string file, string arguments, string workdir, bool hidden)
        {
            ProcessStartInfo psi = new ProcessStartInfo(file);
            psi.Arguments = arguments;
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            if (!string.IsNullOrEmpty(workdir)) { try { psi.WorkingDirectory = workdir; } catch { } }
            if (hidden) psi.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();          // 真身也是无限等：这一轮就是为了让子轮跑完再退
                try { return p.ExitCode; }
                catch (Exception ex) { throw new InvalidOperationException("读不到提权子进程的退出码：" + ex.Message); }
            }
        }

        // ==================== verb: send-round-notice ====================
        // Send-GdNotice(common.ps1 已摘于本批) 的三档容错照抄：**落盘无条件做**，webhook 与气泡各自一把 try，
        // 两把都只 WARN —— "发通知"不许合并成一次 try(那样气泡失败会把已推送的 webhook 也说成没推)。
        internal static object Notice(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string owner = OptStr(c, "owner", delegate { return "每日"; });
            bool allOk = OptFlag(c, "allOk", delegate { return true; });
            string title = OptStr(c, "title", delegate { return NoticeTitle(owner, allOk); });
            string text = OptStr(c, "text", null);
            string level = OptStr(c, "level", delegate { return allOk ? "OK" : "WARN"; });
            string time = OptStr(c, "nowText", delegate { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); });

            // 真身(common.ps1 已摘于本批)这一句**不在 try 里**：落盘是无条件的第一件事，盘坏了就该把整枚 verb 抛出去
            // (Cli 换算成退出码 1)，而不是"落盘失败只吭一声、后面 webhook 照打"—— 那是给通知加了它没有的容错。
            // 真身是 ConvertTo-Json 缩进版(带换行)，这里是单行 —— 读的只有人和面板，形状差登记在文件头。
            Dictionary<string, object> payload = Sub(
                "time", time, "title", title, "text", text, "level", level);
            GdJson.Write(Path.Combine(GdEnv.State, "last-notice.json"), payload);
            bool wrote = true;

            Dictionary<string, object> given = Gd.Map(Gd.Get(c, "settings"));
            Dictionary<string, object> s = given != null ? given : SafeSettings();
            bool noSend = OptFlag(c, "noSend", delegate { return false; });
            List<object> lines = new List<object>();

            string webhook = "";
            if (s != null) { object n = Gd.Get(s, "notify"); webhook = Gd.Str(Gd.Get(n, "webhook")); }
            string hookState;
            if (webhook.Length == 0) hookState = "none";
            else if (noSend) hookState = "skipped";
            else
            {
                string fakeErr = OptStr(c, "fakeWebhookErr", null);
                if (fakeErr.Length > 0) { hookState = "failed"; lines.Add("webhook 推送失败：" + fakeErr); }
                else
                {
                    try
                    {
                        PostJson(webhook, Sub("time", time, "title", title, "text", text, "level", level));
                        hookState = "sent";
                        lines.Add("已推送 webhook 通知");
                    }
                    catch (Exception ex)
                    {
                        hookState = "failed";
                        lines.Add("webhook 推送失败：" + ex.Message);
                    }
                }
            }

            string toastState;
            object toastRaw = s == null ? null : Gd.Get(Gd.Get(s, "notify"), "toast");
            bool wantToast = s == null || Gd.Truthy(toastRaw);        // 真身：(-not $settings) -or $settings.notify.toast
            if (!wantToast) toastState = "off";
            else if (noSend) toastState = "skipped";
            else
            {
                string fakeErr = OptStr(c, "fakeToastErr", null);
                // 原话逐字(common.ps1 已摘于本批)：气泡失败也是"不影响日常本身"这一档，别写成 ERROR。
                if (fakeErr.Length > 0) { toastState = "failed"; lines.Add("桌面通知发送失败(不影响日常本身)：" + fakeErr); }
                else
                {
                    try { Balloon(title, text); toastState = "sent"; }
                    catch (Exception ex) { toastState = "failed"; lines.Add("桌面通知发送失败(不影响日常本身)：" + ex.Message); }
                }
            }

            foreach (object lo in lines)
            {
                string l = Gd.Str(lo);
                // 真身那三句的档位照抄：只有"已推送 webhook 通知"是默认 INFO(-Quiet)，两条失败都是 WARN。
                GdLog.Line(l, l.StartsWith("已推送") ? "INFO" : "WARN");
            }
            return Gd.Out(
                "Title", title, "Level", level, "Time", time,
                "File", wrote, "FileKey", @"state\last-notice.json",
                "Webhook", hookState, "Toast", toastState,
                "Lines", lines.ToArray());
        }

        static Dictionary<string, object> SafeSettings()
        {
            try { List<string> m; return GdSettings.Load(out m, true, new List<string>()); }
            catch { return null; }      // 真身这里也是 catch{} 后 $settings=$null ⇒ 通知照发(默认 toast 开)
        }

        // ⑦-1 那笔原样怪癖的落点：标题这句**在 C# 侧只有一个来源**，②-b 的宿主也从这里取(内部件，--roundtest 钉)。
        internal static string NoticeTitle(string owner, bool allOk)
        {
            return (owner ?? "") + (allOk ? "日常已完成" : "日常需要人工看一眼");
        }

        // Enable-GdTls12 + Invoke-RestMethod(10s) 的同形：只 POST 那份 JSON，回执一个字都不看。
        static void PostJson(string url, Dictionary<string, object> payload)
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072 | (SecurityProtocolType)768;
            }
            catch { }
            byte[] body = new UTF8Encoding(false).GetBytes(GdJson.Serialize(payload));
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Timeout = 10000;
            req.ReadWriteTimeout = 10000;
            req.ContentLength = body.Length;
            using (Stream st = req.GetRequestStream()) st.Write(body, 0, body.Length);
            using (WebResponse resp = req.GetResponse()) { }
        }

        // NotifyIcon 气泡那一段逐形照抄：Information 图标、ToolTipIcon.Info、换行替空格、12000ms、睡 3 秒再 Dispose。
        // (那 3 秒是给气泡的：不等就 Dispose 等于没弹 —— 真身注释说过，这里原样保留。)
        static void Balloon(string title, string text)
        {
            System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
            try
            {
                ni.Icon = System.Drawing.SystemIcons.Information;
                ni.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
                ni.BalloonTipTitle = title;
                ni.BalloonTipText = Regex.Replace(text ?? "", @"\r?\n", " ");
                ni.Visible = true;
                ni.ShowBalloonTip(12000);
                System.Threading.Thread.Sleep(3000);
            }
            finally { try { ni.Dispose(); } catch { } }
        }

        // ==================== verb: start-run-lock ====================
        // Start-GdRunLock(common.ps1:902)：**先说话再登记，不拦**。拦的活交给面板和计划任务那两侧——
        // 在判定层硬拦会让 06:00/18:00 撞上时整轮直接失败，而"失败"对无人值守的人比"多跑一遍"更难发现。
        internal static object LockStart(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            // pid 必传：verb 子进程自己退出即残留，把"我这一下"登记成活锁会让面板的撞车保护失真。
            if (!Given(c, "pid"))
                throw new GdRuleException("start-run-lock：必须显式给 pid(登记运行锁的那个 PID 只能是调用方那一轮，不能是这枚一次性 verb)");
            int pid = (int)OptLng(c, "pid", 0);
            string games = OptStr(c, "games", delegate { return "all"; });
            string tag = OptStr(c, "tag", delegate { return ""; });
            string log = OptStr(c, "log", delegate { return GdLog.LogFile ?? ""; });
            string started = OptStr(c, "started", delegate { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); });

            List<object> lines = new List<object>();
            bool prevAlive = false;
            Lock prev = LockAt(LockPath, false);          // PS 那把尺：读不到细节算"没了"
            if (prev.Found && prev.Alive)
            {
                prevAlive = true;
                string msg = "已有一轮在跑(PID " + prev.Pid + "，" + prev.Started + " 起，games=" + prev.Games +
                             ") —— 本轮照跑，两轮会抢同一个助手/端游";
                GdLog.Line(msg, "WARN");
                lines.Add(msg);
            }

            // 键序 pid/started/games/tag/log ＋ 压缩单行 = ConvertTo-Json -Compress 的 [ordered] 形状。
            Dictionary<string, object> obj = Sub(
                "pid", pid, "started", started, "games", games, "tag", tag, "log", log);
            bool wrote = false;
            try
            {
                GdEnv.Init(null);
                File.WriteAllText(LockPath, GdJson.Serialize(obj), new UTF8Encoding(false));
                wrote = true;
            }
            catch (Exception ex)
            {
                string msg = "活体标记没写进去：" + ex.Message + "(面板这一轮就认不出有人在跑)";
                GdLog.Line(msg, "WARN");
                lines.Add(msg);
            }
            return Gd.Out(
                "Wrote", wrote, "Pid", pid, "Started", started, "Games", games, "Tag", tag, "Log", log,
                "PrevAlive", prevAlive, "Lines", lines.ToArray());
        }

        // ==================== verb: stop-run-lock ====================
        internal static object LockStop(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            if (!Given(c, "pid"))
                throw new GdRuleException("stop-run-lock：必须显式给 pid(撤锁只撤自己登记的那一份)");
            int pid = (int)OptLng(c, "pid", 0);
            bool had = false, removed = false;
            // 真身整枚函数是 catch{}：读不到/删不掉都不吭声，撤锁失败绝不能把收尾这一路带崩。
            try
            {
                string path = LockPath;
                if (File.Exists(path))
                {
                    had = true;
                    string txt = File.ReadAllText(path);
                    if (OwnsText(txt, pid)) { File.Delete(path); removed = true; }
                }
            }
            catch { }
            return Gd.Out("Had", had, "Removed", removed, "Pid", pid);
        }

        // ==================== verb: read-task-state ====================
        // Get-GdTaskState(common.ps1:1031) 的四格回执。走 Schedule.Service COM 而不是 schtasks.exe：
        // 后者的状态列是本地化标签("就绪")，而调用方(run-via-task.ps1:85-162)比的是 PS 那几个英文词。
        internal static object TaskState(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string name = OptStr(c, "name", null);
            if (name.Length == 0)
                throw new GdRuleException("read-task-state：没给任务名(任务名属现场量，由调用方按这棵树的名字送进来)");

            Dictionary<string, object> fake = Gd.Map(Gd.Get(c, "fake"));
            if (fake != null)
            {
                object st = Gd.Get(fake, "state");
                return Gd.Out(
                    "Found", OptFlag(fake, "found", delegate { return true; }),
                    "State", st == null ? "" : Gd.Str(st),
                    "Last", Gd.Str(Gd.Get(fake, "last")),
                    "Result", Gd.Get(fake, "result"),
                    "Next", Gd.Str(Gd.Get(fake, "next")));
            }

            object svc = null;
            try
            {
                Type t = Type.GetTypeFromProgID("Schedule.Service");
                if (t == null) throw new GdRuleException("这台机器没有 Schedule.Service 组件，计划任务状态读不出来");
                svc = Activator.CreateInstance(t);
                Invoke(svc, "Connect");
                object folder = Invoke(svc, "GetFolder", OptStr(c, "taskPath", delegate { return "/"; }));
                object task;
                try { task = Invoke(folder, "GetTask", name); }
                catch { return Gd.Out("Found", false, "State", "", "Last", "", "Result", null, "Next", ""); }
                long st = Num(Prop(task, "state"));
                string state = TaskStateName(st);
                object info = Prop(task, "Information");
                object lastRaw = info == null ? null : Prop(info, "LastRunTime");
                object nextRaw = info == null ? null : Prop(info, "NextRunTime");
                object result = null;
                try { if (info != null) result = Convert.ToInt32(Prop(info, "LastTaskResult"), CultureInfo.InvariantCulture); }
                catch { result = null; }
                return Gd.Out(
                    "Found", true,
                    "State", state,
                    "Last", DateText(lastRaw),
                    "Result", result,
                    "Next", DateText(nextRaw));
            }
            finally
            {
                try { if (svc != null && Marshal.IsComObject(svc)) Marshal.ReleaseComObject(svc); } catch { }
            }
        }

        // internal(乙档刀①)：Panel.Summary.cs 的定时那一格要读同一套 COM 助手，不许在那边另编一份属性反射。
        internal static object Prop(object target, string name)
        {
            if (target == null) return null;
            try { return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null); }
            catch { return null; }
        }

        internal static object Invoke(object target, string name, params object[] args)
        {
            return target.GetType().InvokeMember(
                name, BindingFlags.InvokeMethod | BindingFlags.GetProperty, null, target, args);
        }

        internal static long Num(object v)
        {
            if (v == null) return -1;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return -1; }
        }

        // TASK_STATE 与 PS 的 TaskState 同一套序号(0..4)：Unknown/Disabled/Queued/Ready/Running。
        // 数外的值原样回数字 —— PS 对枚举越界也是回数字，别在这里编一个新词。
        internal static string TaskStateName(long state)
        {
            if (state == 0) return "Unknown";
            if (state == 1) return "Disabled";
            if (state == 2) return "Queued";
            if (state == 3) return "Ready";
            if (state == 4) return "Running";
            return state.ToString(CultureInfo.InvariantCulture);
        }

        // TASK_PRINCIPAL_RUN_LEVEL(0=Limited／1=Highest)：PS 侧 [string]$task.Principal.RunLevel 拿到的是同名枚举的
        // 英文字，这里拿到的是 Int32 ⇒ 换成同一个词。数外的值原样回数字，与 TaskStateName 同规矩(不编新词)。
        // 取不到属性的调用方(Round.TaskState 不报这一格)不受影响；乙档刀① 的定时行是第一个消费方。
        internal static string RunLevelName(long level)
        {
            if (level == 0) return "Limited";
            if (level == 1) return "Highest";
            return level.ToString(CultureInfo.InvariantCulture);
        }

        // [string]$info.LastRunTime 的同形：VT_DATE 可能是 DateTime 也可能是 double(OleDate)，都收。
        static string DateText(object v)
        {
            if (v == null) return "";
            if (v is DateTime) return ((DateTime)v).ToString(CultureInfo.CurrentCulture);
            try
            {
                double oa = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                return DateTime.FromOADate(oa).ToString(CultureInfo.CurrentCulture);
            }
            catch { return Gd.Str(v); }
        }

        // ==================== verb: stop-process-tree ====================
        // Stop-GdProcessTree(common.ps1:478)：**名曰树、实为按进程名**(文件头原样怪癖第 4 笔)。
        // -Force 直接 Kill；否则 CloseMainWindow ＋ 20 秒轮询(800ms) ＋ 补 Kill；返回的是**匹配到的进程数**。
        internal static object ProcTree(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string name = OptStr(c, "name", null);
            if (name.Length == 0)
                throw new GdRuleException("stop-process-tree：没给进程名(这一枚按进程名匹配，不给就是空打)");
            bool force = OptFlag(c, "force", delegate { return false; });
            int graceSeconds = (int)OptLng(c, "graceSeconds", 20);
            bool noKill = OptFlag(c, "noKill", delegate { return false; });

            List<int> ids = new List<int>();
            if (noKill)
            {
                // 夹具档：自检要钉"返回匹配数"这一格又不许真杀，就把 pid 名册喂进来(真跑一侧不许送 fakeProcs)。
                foreach (object o in Gd.List(Gd.Get(c, "fakeProcs"))) ids.Add((int)Num(o));
            }
            else
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName(name)) { try { ids.Add(p.Id); } catch { } finally { try { p.Dispose(); } catch { } } }
                }
                catch { }
            }
            int matched = ids.Count;
            if (matched == 0 || noKill)
                return Gd.Out("Matched", matched, "Killed", matched, "Force", force, "Skipped", noKill);

            foreach (int pid in ids)
            {
                try
                {
                    using (Process p = Process.GetProcessById(pid))
                    {
                        if (force) p.Kill();
                        else p.CloseMainWindow();
                    }
                }
                catch { }   // 真身逐枚 try{}catch{}：这一枚没了不影响别的
            }
            if (!force)
            {
                DateTime deadline = DateTime.Now.AddSeconds(graceSeconds);
                while (DateTime.Now < deadline && AnyByName(name)) System.Threading.Thread.Sleep(800);
                foreach (Process p in Snapshot(name))
                {
                    try { p.Kill(); } catch { } finally { try { p.Dispose(); } catch { } }
                }
            }
            return Gd.Out("Matched", matched, "Killed", matched, "Force", force, "Skipped", false);
        }

        static bool AnyByName(string name)
        {
            Process[] ps = Snapshot(name);
            bool any = ps.Length > 0;
            foreach (Process p in ps) { try { p.Dispose(); } catch { } }
            return any;
        }

        static Process[] Snapshot(string name)
        {
            try { return Process.GetProcessesByName(name); } catch { return new Process[0]; }
        }

        // ==================== 三态取数(R9-a 新立) ====================
        // 为什么这里要新立一把尺，而不直接用上面的 Given：`Given` 把"JSON 里写了 null"与"没写这枚键"当成同一件事
        // (它判的是 `v != null`)。R9-a 这三枚要钉的恰恰是**"读不到"那一档** —— 屏保 SPI 读不到时真身给的是
        // `Read=false`(本轮不动屏保)，而"没给夹具"走的是真读现场；两档在旧那把尺里塌成一档就必然有一档测不到。
        // 于是：键**没出现** = 真读现场；键出现且值为 null = 按读不到；键出现且有值 = 用夹具。
        internal static bool Has(Dictionary<string, object> c, string k)
        {
            return c.ContainsKey(k);
        }

        internal static object Raw(Dictionary<string, object> c, string k)
        {
            object v;
            c.TryGetValue(k, out v);
            return v;
        }

        // ==================== verb: read-screensaver-config(R9-a 收编 svdetail 的取数半段) ====================
        // 原 Get-GdScreensaverConfig(common.ps1 旧 359-378，那是历史引文不是活锚)的真身：只读注册表(HKCU\Control Panel\Desktop)，
        // **刻意不碰 SPI** —— 那边旧头注写了理由(会话被屏保接管时 SPI 与注册表不一致，
        // 而体检要看的正是"下次到点会不会触发"那个稳态)。这一格要的正是那枚稳态。
        // 三枚值逐枚 try 的形状照真身：`SCRNSAVE.EXE` 取不到算空串、`ScreenSaveTimeOut` 取不到算 0、
        // 产品名只在有 .scr 时才去取(FileVersionInfo 读不到回空)。
        // 整块读不到(键开不了) ⇒ `Found:false` —— **调用方绝不许据此判"没装屏保"**(原真身那句 return $null 同形)；
        // 核内那一臂折成 cfg=Null 交给同装配的 `resolve-screensaver-verdict`，判定给 unknown 档那句"不代表没事"。
        // ⚠ 转接刀·屏保稳态族(2026-11-03，§11.215)之后 PS 侧那三枚真身已出仓：这一折自此**归调用方自做**，
        // 仓内四处(status.ps1／lib\ark-daily.ps1／tools\unarm-screensaver.ps1／tools\test-lib.ps1)都按 Found:false⇒cfg=null 写。
        internal static object ScreensaverConfig(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);

            if (Has(c, "reg") && Gd.Get(c, "reg") == null)
                return Gd.Out("Found", false, "Saver", "", "Product", "", "Active", "", "TimeoutSec", 0);

            string saver = "";
            string active = "";
            long timeout = 0;
            string product = null;

            Dictionary<string, object> reg = Gd.Map(Gd.Get(c, "reg"));
            if (reg != null)
            {
                saver = Gd.Str(Gd.Get(reg, "Saver"));
                active = Gd.Str(Gd.Get(reg, "Active"));
                object to = Gd.Get(reg, "TimeoutSec");
                try { if (to != null && Gd.Str(to).Length > 0) timeout = Convert.ToInt64(to, CultureInfo.InvariantCulture); }
                catch { timeout = 0; }
                if (Has(reg, "Product")) product = Gd.Str(Gd.Get(reg, "Product"));
            }
            else
            {
                string sub = OptStr(c, "regPath", delegate { return "Control Panel\\Desktop"; });
                Microsoft.Win32.RegistryKey rk = null;
                try { rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(sub); } catch { rk = null; }
                if (rk == null)
                    return Gd.Out("Found", false, "Saver", "", "Product", "", "Active", "", "TimeoutSec", 0);
                try
                {
                    try { object o = rk.GetValue("SCRNSAVE.EXE"); if (o != null) saver = Gd.Str(o); } catch { saver = ""; }
                    try { object o = rk.GetValue("ScreenSaveActive"); if (o != null) active = Gd.Str(o); } catch { active = ""; }
                    // 真身那句 `if ($dp.ScreenSaveTimeOut) { $to = [int]… }`：空值/读不动都留在 0，不吭声。
                    try
                    {
                        object o = rk.GetValue("ScreenSaveTimeOut");
                        if (o != null && Gd.Str(o).Length > 0) timeout = Convert.ToInt64(o, CultureInfo.InvariantCulture);
                    }
                    catch { timeout = 0; }
                }
                finally { try { rk.Close(); } catch { } }
            }

            if (product == null)
            {
                product = "";
                if (saver.Length > 0)
                {
                    // 真身那句 `Get-Item $scr).VersionInfo.ProductName` 外面套了 try/catch ⇒ 读不到就是空串，
                    // 归因规则(WE 代理名归一那半)在 `resolve-screensaver-who`，这里一个字都不判。
                    try { product = FileVersionInfo.GetVersionInfo(saver).ProductName ?? ""; } catch { product = ""; }
                }
            }
            return Gd.Out("Found", true, "Saver", saver, "Product", product, "Active", active, "TimeoutSec", (int)timeout);
        }

        // ==================== verb: disable-screensaver-for-round(R9-a 收编 round-screensaver) ====================
        // 转接层当年那六行(lib\rust-round.ps1 里那对补位件，不引行号) ＝ 真身 `Enter-GdNoSleep` 里 keepScreenOn 那一段
        // (真身整枚已于丙档落码刀①从 lib\common.ps1 删除，去向见 docs\rust-core-plan.md §11.116)：**清旧机制那本 state 文件 → 现读 SPI → 只关不复**。四档话术一枚都不在这里打
        // (它们在核内段 8，逐字照当年真身里那四句 Write-GdLog)，这里只回三格读数。
        // SPI 的声明用 tools\gdinterop\GdPower.cs 那份唯一真身(build.ps1 按 GdWin.cs 的先例把它编进本装配，
        // **不在 panel-cs 抄第二份 P/Invoke**)；`Get-/Set-GdScreensaverActive` 那两枚 PS 转发壳一字不动。
        // ↑ 末句是 R9-a 的**设计期声明**，丙档落码刀②(10-22)起只剩后半成立：`Get-GdScreensaverActive` 连 `Get-GdScreensaverStateFile` 已从 lib\common.ps1 删除
        // (`Set-` 那枚被 tools\unarm-screensaver.ps1 真调着、照旧在场)，去向见 docs\rust-core-plan.md §11.118 ⇒ SPI 现读这一格的事实源自此只有本枚 verb。
        // ⚠ 键序是 `{Read, WasActive, Disabled}`：与转接层当年写进 res 的那枚逐字同序，门的投影才不用跟着改。
        internal static object ScreensaverOff(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string dir = OptStr(c, "stateDir", delegate { GdEnv.Init(null); return GdEnv.State; });

            // 真身那两句 `if (Test-Path $sf) { Remove-Item … -ErrorAction SilentlyContinue }`：删不掉不吭声。
            try
            {
                string sf = Path.Combine(dir, "screensaver.restore");
                if (File.Exists(sf)) File.Delete(sf);
            }
            catch { }

            bool read;
            bool wasActive;
            bool faked = Has(c, "active");
            if (faked && Gd.Get(c, "active") == null) { read = false; wasActive = false; }      // 那一档＝SPI 读不到
            else if (faked) { read = true; wasActive = Gd.Truthy(Gd.Get(c, "active")); }
            else
            {
                bool v = false;
                read = GdPower.SPIGetActive(GdPower.SPI_GETSCREENSAVEACTIVE, 0, ref v, 0);
                wasActive = read && v;
            }

            if (!read) return Gd.Out("Read", false, "WasActive", false, "Disabled", false);
            if (!wasActive) return Gd.Out("Read", true, "WasActive", false, "Disabled", false);

            bool disabled;
            if (faked) disabled = OptFlag(c, "setOk", delegate { return true; });
            else disabled = GdPower.SPISetActive(GdPower.SPI_SETSCREENSAVEACTIVE, 0, IntPtr.Zero, GdPower.SPIF_SENDWININICHANGE);
            return Gd.Out("Read", true, "WasActive", true, "Disabled", disabled);
        }

        // ==================== verb: append-round-history(R9-a 收编 round-history 两枚) ====================
        // 原 Add-GdHistory(lib/common.ps1 旧 712-740；该 PS 真身已于 11-07 刀⑰ 笔(c)-1 整枚出仓，全树零活调用，去向见 docs\rust-core-plan.md §11.274) 的**整枚**：表头、舍入、`-0` 那一格、detail 折行、落盘字节形状都在这里。
        // 三处形状的真身只有一份 = tools\gdcore\GdCore.Logs.cs 的 `GdCore.Csv`(PS 侧靠 Add-GdInterop 现编同一份)，
        // build.ps1 把它按 GdWin.cs 的先例编进本装配 ⇒ **不在这里抄第二份 CSV 规则**；舍入仍按 `[math]::Round`
        // 那把中点取偶(Math.Round 默认 ToEven)，`-0` 由 DurationText 钉成 "0"(存量 history.csv 按 5.1 那支写的)。
        // 字节形状是本轮实测定的(§11.67)：pwsh 7 的 `Set-Content/Add-Content -Encoding utf8` 写出来是
        // **无 BOM ＋ CRLF**(5.1 才写 EF BB BF)，所以这里用 `new UTF8Encoding(false)` ＋ 每行自带 "\r\n"。
        // ⚠ 时间戳**由这一枚现读**而不是核内喂：真身每写一行调一次 Get-Date，而 ctx 的 NowText 是起轮那一刻的快照，
        //   喂进来就是把每一行的时间都盖成 06:00:00(自检要钉某一行的时间时才给 nowText)。
        internal static object RoundHistory(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string dir = OptStr(c, "stateDir", delegate { GdEnv.Init(null); return GdEnv.State; });
            string file = Path.Combine(dir, "history.csv");
            string time = OptStr(c, "nowText", delegate { return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); });

            string dur = "";
            if (Has(c, "durationSec") && Gd.Get(c, "durationSec") != null)
            {
                double d = 0;
                try { d = Convert.ToDouble(Gd.Get(c, "durationSec"), CultureInfo.InvariantCulture); } catch { d = 0; }
                dur = GdCore.Csv.DurationText(Math.Round(d));
            }

            // 字段序照真身那枚 $fields：time,game,mode,status,duration_sec,detail(表头同名同序)。
            string[] fields = new string[]
            {
                time,
                OptStr(c, "id", null),
                OptStr(c, "mode", null),
                OptStr(c, "status", null),
                dur,
                GdCore.Csv.Flatten(OptStr(c, "detail", null))
            };
            string row = GdCore.Csv.BuildRow(fields);

            UTF8Encoding noBom = new UTF8Encoding(false);
            if (!File.Exists(file))
                File.WriteAllText(file, "\"time\",\"game\",\"mode\",\"status\",\"duration_sec\",\"detail\"\r\n", noBom);
            // 真身不建 state 目录(Set-Content 直接落)，这里也不建：目录不在就该把这一轮拦下来，
            // 而不是悄悄把账写到别处 —— 与 common.ps1:879 那句 Test-Path 同一把尺。
            File.AppendAllText(file, row + "\r\n", noBom);
            return Gd.Out("Written", true);
        }
    }
}
