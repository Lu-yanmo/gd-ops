// tools/panel-cs/Live.cs —— B-2b(2026-10-08)：视觉兜底的**现场件**七枚 verb(切分与理由见 docs\rust-core-plan.md §11.34)。
// 10-11 追加第八枚 `check-ark-launcher`(启动器更新页的五档相位，见本文件末尾与 docs\rust-core-plan.md §11.40)：
// 它是**新能力**不是搬家 —— 旧 lib 那侧没有真身可抄(旧判定只看窗口标题，而更新页与主界面标题同为"明日方舟")，
// 所以这一枚的话术以本文件＋--livetest 第 8 组为唯一事实源，改动要连话术一起改，别单侧动。
//
// 10-12 追加(B-2d③)第九~十二枚现场件 `adb-connect`／`adb-game-pid`／`mumu-screen-shot`／`mumu-screen-click`，
// 并把 `resolve-rescue-context` 从"只认 ark"扩成"ark／mumu 两路"(切分与理由见 docs\rust-core-plan.md §11.42)。
// **这一路是真双源**：每一句原话抄自 lib\vision-mumu.ps1 与 lib\mumu.ps1，而那两个文件整份留在 PS 侧还在用
// ⇒ 话术改一处要两处同改(改一侧就等着 --livetest 与 m9a 那两侧的门分岔)。mumu 的 Scene/UserText 两份默认话术
// 同样是双源(vision-mumu.ps1:184-189)：核那一侧只从 resolve-rescue-context 的回执取这两格，所以它们必须住在这里。
// **端点三源两正则绝不进来**(`Get-GdMuMuAdbEndpoint`：MuMuManager 的 adb_path、版本号形态、"恰好只有一台"那次机会)：
// adb 路径与序列号一律由编排侧现查后当键喂，本文件只打"拿到端点之后那三次 adb 调用"。
//
// 这一份管"必须碰这台机器"的那一半：user32 拍窗口/点窗口/抢前台、枚举进程找端游、System.Drawing 算亮度签名、
// 起 Python 子进程问模型。Rust 核(B-2b③)只做编排与落盘，Windows 交互与 key 一律待在这一侧 —— 宿主 A 形态的落点，
// 也是 §11.34 ① 那条"P/Invoke 真身仍一份"的兑现处：源文件仍是 tools\gdinterop\GdWin.cs，由 build.ps1 多带一条
// 路径编进同一份装配，这里不抄第二份签名(改那份 .cs 从此要跑两套装配：PS 现编门 t14 与 panel-cs 编译)。
//
// 单一事实源：话术逐字抄自 lib\vision-ark.ps1 与 lib\vision-rescue.ps1 的真身(那两个文件在生产接线 B-2c 之前
// 仍是老编排的事实源)。改一句文案 = 老路新路各说各话，先跑 gdcore --selftest(第 30 节钉这一路)与 tools\test-ark.ps1。
// "截图失败：" 的**双前缀**是原样怪癖(Save-GdWindowShot 的 catch 已经带过一次，旧编排 Invoke-GdVisionRescue 又包一次；那枚壳随第二十刀刀③ 出仓)：
// 本文件把包装做在 Skip 里，正是要让那两次叠加照旧发生 —— 别"顺手修好"。
//
// 安全边界(照 lib\vision-rescue.ps1 文件头执行，一条不松)：
//   * API key 只在本文件内部取值(env DASHSCOPE_API_KEY → settings.runtime.visionApiKey，都要 Trim)，
//     **只写进子进程的 EnvironmentVariables**：不进命令行、不进请求文件、不进日志、不进信封、不进核；
//   * hasKey / pythonExe / noSpawn / fakeOut 这几枚是**自检与对拍夹具的注入旋钮**(身份同 Cli.cs 的 --garbage)：
//     真跑一侧不许送 hasKey=true —— 那会让子进程拿到一个假 key，请求必然 401。缺键才现读，所以默认路径不碰夹具。
//
// 与旧实现的两处已知形态差(都不是判据，如实记在这)：
//   * Get-GdPythonExe 把找到的路径 .ToLowerInvariant() 之后再返回，这里保留原样大小写(Windows 不分大小写)；
//   * 请求文件从 Write-GdJson 的缩进变成 GdJson.Write 的单行 —— 这文件只有 tools\vision\ask.py 读(它按 utf-8-sig
//     解析 JSON)，人眼回看的那本账是 state\vision\calls.jsonl，没受影响。
//
// 批次纪律：--livetest 只钉纯规则段(尺寸闸分支/闸门话术/请求装配/内存位图跑 stats/stdout 回扫)；
// 真窗口与真 HTTP 不进门(只读自检不许要求机器现场状态)，那一轮实跑另留。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GdPanel
{
    static class Live
    {
        // ==================== 入参取数：缺键=自己现读(现场)，有键=用夹具 ====================

        static Dictionary<string, object> View(Dictionary<string, object> raw)
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

        // 默认值用 Func 给：现场读(注册表之外的盘/settings/探测子进程)只在该读时才读，
        // 否则自检喂了夹具也照样把 settings.json 与 python 探测走一遍 —— 那是"要求机器现场状态"。
        static string OptStr(Dictionary<string, object> c, string k, Func<string> def)
        {
            object v;
            if (c.TryGetValue(k, out v) && v != null) return Gd.Str(v);
            return def == null ? "" : def();
        }

        static bool OptFlag(Dictionary<string, object> c, string k, Func<bool> def)
        {
            object v;
            if (c.TryGetValue(k, out v)) return Gd.Truthy(v);
            return def == null ? false : def();
        }

        static long OptLng(Dictionary<string, object> c, string k, long def)
        {
            object v;
            if (!c.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        static Dictionary<string, object> Sub(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        static long Num(object v)
        {
            if (v == null) return 0;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        static string StrOf(object v, string def) { return v == null ? def : Gd.Str(v); }

        // ==================== 共同的小现场件：settings / key / python / 管理员 ====================

        static Dictionary<string, object> settingsCache;
        static string settingsTextCache;
        static string pythonCache;
        static bool pythonProbed;

        static Dictionary<string, object> Settings()
        {
            if (settingsCache != null) return settingsCache;
            List<string> missing;
            // requirePaths 给空表：现场 verb 不催助手目录(那是 Stop-GdBeforeRun 的事)，只要读得出 runtime 那几格。
            settingsCache = GdSettings.Load(out missing, true, new List<string>());
            return settingsCache;
        }

        static object Runtime(string key) { return Gd.Get(Gd.Get(Settings(), "runtime"), key); }

        static string SettingsText()
        {
            if (settingsTextCache != null) return settingsTextCache;
            try { settingsTextCache = File.ReadAllText(Path.Combine(GdEnv.Root, @"config\settings.json")); }
            catch { settingsTextCache = ""; }
            return settingsTextCache;
        }

        // 原话逐字：lib\vision-rescue.ps1 的 Get-GdVisionApiKey(环境变量优先，其次 settings，都要 Trim)。
        // 返回值只在进程内递给子进程环境变量，永不进信封、永不进日志、永不回给核。
        static string VisionKey()
        {
            string k = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
            if (k == null || k.Length == 0) k = StrOf(Runtime("visionApiKey"), "");
            if (k.Length == 0) return "";
            return k.Trim();
        }

        static bool IsAdminNow()
        {
            // Test-GdAdmin 的形状：读不到就当不是(宁可这一轮不救，也不硬点)。
            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // Get-GdPythonExe 的同一条顺序(见 Agent.cs) + 同一份"只起一次"的缓存(没找到也要缓存，
        // 否则每次问都白起一遍探测进程)。
        static string PythonExe()
        {
            if (pythonProbed) return pythonCache;
            pythonCache = GdAgentLink.FindPython(SettingsText(),
                Environment.GetEnvironmentVariable("GD_PYTHON"), GdEnv.Root,
                Environment.GetEnvironmentVariable("PATH"), GdAgentLink.PythonUsable);
            pythonProbed = true;
            return pythonCache;
        }

        // ==================== 端游窗口(原 Get-GdArkWindow) ====================

        sealed class ArkWin
        {
            public bool Found;
            public IntPtr Handle;
            public long ProcId;
            public string Title = "";
        }

        // 用进程名匹配，不用窗口标题(标题会随版本变，进程名跟着 settings 的 paths.arknightsExe 走) —— 逐字照旧实现。
        static string ArkProcName()
        {
            string s = StrOf(Gd.Get(Gd.Get(Settings(), "paths"), "arknightsExe"), "");
            if (s.Length == 0) return "";
            try { return Path.GetFileNameWithoutExtension(s); } catch { return ""; }
        }

        static ArkWin FindArk(string procName)
        {
            ArkWin none = new ArkWin();
            if (string.IsNullOrEmpty(procName)) return none;
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero) continue;
                    // PS 的 -eq 比字符串不分大小写，这里同形
                    if (!string.Equals(p.ProcessName, procName, StringComparison.OrdinalIgnoreCase)) continue;
                    ArkWin w = new ArkWin();
                    w.Found = true;
                    w.Handle = p.MainWindowHandle;
                    w.ProcId = p.Id;
                    w.Title = p.MainWindowTitle == null ? "" : p.MainWindowTitle;
                    return w;
                }
                catch { }     // 枚举期间进程退了：跳过这一个(旧实现 Get-Process -ErrorAction SilentlyContinue 同形)
                finally { try { p.Dispose(); } catch { } }
            }
            return none;
        }

        static string ProcNameOf(Dictionary<string, object> c)
        {
            return OptStr(c, "procName", delegate { return ArkProcName(); });
        }

        static ArkWin WindowFrom(Dictionary<string, object> c)
        {
            long hv = OptLng(c, "handle", 0);
            if (hv == 0) return FindArk(ProcNameOf(c));
            ArkWin w = new ArkWin();
            w.Found = true;
            w.Handle = new IntPtr(hv);
            return w;
        }

        // verb: ark-find-window —— 只回答"端游窗口在不在、句柄是多少"，不动它也不点它。
        internal static object ArkFindWindow(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string name = ProcNameOf(c);
            ArkWin w = FindArk(name);
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Found"] = w.Found;
            data["Name"] = name;
            data["Handle"] = w.Found ? w.Handle.ToInt64() : 0L;
            data["ProcId"] = w.Found ? w.ProcId : 0L;
            data["Title"] = w.Title;
            return data;
        }

        // ==================== verb: ark-desktop-state(原 Get-GdArkDesktopState 的"现场量"那半) ====================
        // 拼话术的规矩仍在 Vision.cs(compose-ark-desktop-state)，这一枚只补"那几个量从哪来"：
        // 输入桌面名 / 前台归属 / 最小化 / 可见 / 窗口矩形 / 光标。没句柄就别碰窗口调用 —— 旧实现也只在有窗口时
        // 才读那三项，"没读"与"读到 false"在话术里不是一回事。

        internal static object ArkDesktopState(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            long hv = OptLng(c, "handle", 0);
            bool hwndFound = hv != 0;
            IntPtr hwnd = new IntPtr(hv);

            string desk = OptStr(c, "desk", delegate { return GdWin.InputDesktopName(); });
            bool onFg;
            string fgProcess;
            if (Given(c, "onFg"))
            {
                onFg = OptFlag(c, "onFg", null);
                fgProcess = OptStr(c, "fgProcess", null);
            }
            else
            {
                IntPtr fg = GdWin.ForegroundWindow();
                onFg = hwndFound && fg != IntPtr.Zero && fg.ToInt64() == hv;
                fgProcess = "";
                try
                {
                    uint pid = GdWin.WindowPid(fg);
                    if (pid > 0)
                    {
                        Process pf = Process.GetProcessById((int)pid);
                        try { fgProcess = pf.ProcessName == null ? "" : pf.ProcessName; }
                        finally { try { pf.Dispose(); } catch { } }
                    }
                }
                catch { fgProcess = ""; }     // 前台进程已退/查不到：PS 那边 SilentlyContinue 出来就是空串
            }
            string cursor = OptStr(c, "cursor", delegate { return GdWin.CursorText(); });
            bool iconic = OptFlag(c, "iconic", delegate { return hwndFound && GdWin.IsIconic(hwnd); });
            bool visible = OptFlag(c, "visible", delegate { return hwndFound && GdWin.Visible(hwnd); });
            string rectText = OptStr(c, "rectText", delegate { return hwndFound ? GdWin.WindowRectText(hwnd) : ""; });

            Dictionary<string, object> v = Vision.DesktopState(Sub(
                "desk", desk, "hwndFound", hwndFound, "onFg", onFg, "fgProcess", fgProcess,
                "iconic", iconic, "visible", visible, "rectText", rectText, "cursor", cursor)) as Dictionary<string, object>;

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Desktop"] = desk;
            data["Locked"] = v == null ? null : v["Locked"];      // null=读不到桌面名("不知道")，不是"锁屏了"
            data["LockedKnown"] = v != null && Gd.Truthy(v["LockedKnown"]);
            data["GameOnFg"] = onFg;
            data["FgProcess"] = fgProcess;
            data["Cursor"] = cursor;
            data["Detail"] = v == null ? "" : v["Detail"];
            return data;
        }

        // ==================== verb: ark-grab-foreground(原 Invoke-GdArkForegroundGrab) ====================
        // ALT hack 抢前台：只动窗口状态、不点客户区，所以不会误点游戏里的按钮。抢不到照实回 false。
        // Detail 那两句("已把端游窗口抢到前台(标题) —— …"/"抢前台没成功 —— …")是 test-ark 的锚，逐字不许改。

        internal static object ArkGrab(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            ArkWin w = FindArk(ProcNameOf(c));
            Dictionary<string, object> data = new Dictionary<string, object>();
            if (!w.Found)
            {
                data["Ok"] = false;
                data["Handle"] = 0L;
                data["Detail"] = "端游窗口没找到，抢前台无从谈起";
                return data;
            }
            bool ok = GdWin.TryForeground(w.Handle);
            Dictionary<string, object> st = ArkDesktopState(Sub("handle", w.Handle.ToInt64())) as Dictionary<string, object>;
            string detail = st == null ? "" : Gd.Str(st["Detail"]);
            data["Ok"] = ok;
            data["Handle"] = w.Handle.ToInt64();
            data["Detail"] = ok
                ? "已把端游窗口抢到前台(" + w.Title + ") —— " + detail
                : "抢前台没成功 —— " + detail;
            return data;
        }

        // ==================== verb: ark-window-shot(原 Save-GdWindowShot + ark 靶子的 $shot 闭包) ====================

        // 16x9=144 格粗亮度签名 + 平均亮度：逐字照 lib\vision-rescue.ps1 的 Get-GdShotStats
        // (Step 7、R*30+G*59+B*11 除 100 后取整、gy 封顶 15 / gx 封顶 8、格均值再 /8 量化)。
        // 两处 [int] 与两处 Round 在 PS 里都是**ToEven(银行家舍入)**，不是截断 —— §11.34 ⑦-1 那条地雷，
        // 夹具因此专门喂 AvgLum 7.5/7.4/8.0 三档。AvgLum 落成 long：PS 的 [math]::Round 给的是整值 double，
        // 插进人话就是"8"这种写法，两侧读出来同一个词。
        internal static Dictionary<string, object> Stats(Bitmap bmp, int w, int h, int step)
        {
            long sum = 0L;
            int n = 0;
            int[] acc = new int[144];
            int[] cnt = new int[144];
            for (int y = 0; y < h; y += step)
            {
                for (int x = 0; x < w; x += step)
                {
                    Color px = bmp.GetPixel(x, y);
                    int l = Convert.ToInt32((px.R * 30 + px.G * 59 + px.B * 11) / 100.0);
                    sum += l;
                    n++;
                    int gy = Convert.ToInt32(y / (double)h * 16); if (gy > 15) gy = 15;
                    int gx = Convert.ToInt32(x / (double)w * 9); if (gx > 8) gx = 8;
                    int k = gy * 9 + gx;
                    acc[k] += l;
                    cnt[k]++;
                }
            }
            StringBuilder sig = new StringBuilder();
            for (int k = 0; k < 144; k++)
            {
                if (k > 0) sig.Append(',');
                double cell = cnt[k] > 0 ? (double)acc[k] / cnt[k] / 8.0 : 0.0;
                sig.Append(Convert.ToInt32(Math.Round(cell, MidpointRounding.ToEven), CultureInfo.InvariantCulture));
            }
            return Sub(
                "AvgLum", Convert.ToInt64(Math.Round(sum / (double)Math.Max(1, n), MidpointRounding.ToEven), CultureInfo.InvariantCulture),
                "Sig", sig.ToString());
        }

        // 尺寸闸的两种话术(旧实现按"恢复之后还在最小化吗"分开报，别合成一句)。
        internal static string ShotSizeWhy(int w, int h, bool iconic)
        {
            if (w >= 200 && h >= 150) return "";
            return iconic ? "端游窗口仍最小化(拍不到东西) " + w + "x" + h
                          : "窗口尺寸异常 " + w + "x" + h;
        }

        static Dictionary<string, object> ShotFail(string why)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["Ok"] = false;
            d["Why"] = why;
            d["CanContinue"] = false;
            // 旧实现这一层的原话是 Invoke-GdVisionRescue 里的 "截图失败：$($before.Why)"；而 catch 那条的 Why
            // 自己就叫 "截图失败：…" —— 于是异常路径上印两次前缀。原样怪癖，别修。
            d["Skip"] = "截图失败：" + why;
            d["LogLine"] = "";
            return d;
        }

        // 黑屏闸的话术是判据(阈值为什么是 8 见 vision-rescue 头注)，留在这里；包装见 ShotFail 那条注释。
        // 单独抽成一枚 internal：--livetest 不碰真窗口，够不着 WindowShot，这句就得靠它自己钉住。
        internal static string BlackScreenSkip(long lum)
        {
            return "截图几乎是全黑(平均亮度 " + lum + ")：桌面可能锁了或窗口没在渲染，问模型只会白花钱";
        }

        // "视觉兜底：已拍…" 这句 ark 与 mumu 两路**逐字相同**(旧真身在通用层 vision-rescue.ps1:349，那枚壳随第二十刀刀③ 出仓，
        // 只把 $t.Name 与图尺寸按靶子代入)⇒ 抽成一枚共用，两路谁也别偷偷长出第二个版本。
        internal static string ShotLogLine(string name, long w, long h, long lum)
        {
            return "视觉兜底：已拍" + name + "(" + w + "x" + h + "，亮度 " + lum + ")，交给模型判断";
        }

        internal static object WindowShot(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string path = OptStr(c, "path", null);
            if (path.Length == 0) throw new GdRuleException("ark-window-shot：没给 path(截图要落盘的路径属现场量，由编排侧拼 ark-<Tag>-时间-before.png 送进来)");
            string name = OptStr(c, "name", delegate { return "端游窗口"; });
            ArkWin w = WindowFrom(c);
            if (!w.Found) return ShotFail("端游窗口没找到(可能已经关了)");

            IntPtr hwnd = w.Handle;
            Bitmap bmp = null;
            Graphics g = null;
            try
            {
                if (GdWin.IsIconic(hwnd))
                {
                    GdLog.Line("端游窗口最小化中 → 先 SW_RESTORE 叫回来再拍", "WARN");
                    GdWin.Restore(hwnd);
                    System.Threading.Thread.Sleep(1500);       // 给它一帧时间重绘，否则拍到的还是空位图
                }
                GdRect wr = GdWin.WindowRect(hwnd);
                int cw = wr.Right - wr.Left, ch = wr.Bottom - wr.Top;
                string sizeWhy = ShotSizeWhy(cw, ch, GdWin.IsIconic(hwnd));
                if (sizeWhy.Length > 0) return ShotFail(sizeWhy);

                bmp = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
                g = Graphics.FromImage(bmp);
                IntPtr hdc = g.GetHdc();
                // 2 = PW_RENDERFULLCONTENT：Win10 之后带 DirectComposition/硬件加速的窗口不加这个会拍成黑屏
                bool ok = GdWin.PrintWindow(hwnd, hdc, 2);
                if (!ok) ok = GdWin.PrintWindow(hwnd, hdc, 0);   // 老一点的窗口过程只认 0，再试一次
                g.ReleaseHdc(hdc);
                if (!ok) return ShotFail("PrintWindow 返回 false(权限不够或窗口已关闭)");

                Dictionary<string, object> stats = Stats(bmp, cw, ch, 7);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                bmp.Save(path, ImageFormat.Png);
                GdPoint co = GdWin.ClientOrigin(hwnd);
                GdRect cr = GdWin.ClientRect(hwnd);
                long lum = Convert.ToInt64(stats["AvgLum"], CultureInfo.InvariantCulture);

                Dictionary<string, object> data = new Dictionary<string, object>();
                data["Ok"] = true;
                data["Path"] = path;
                data["ImgW"] = cw;
                data["ImgH"] = ch;
                data["ClientLeft"] = co.X - wr.Left;
                data["ClientTop"] = co.Y - wr.Top;
                data["ClientW"] = cr.Right;
                data["ClientH"] = cr.Bottom;
                data["AvgLum"] = lum;
                data["Sig"] = stats["Sig"];
                data["Hash"] = Md5File(path);
                data["Why"] = "";
                data["CanContinue"] = lum >= 8;
                data["Skip"] = lum >= 8 ? "" : BlackScreenSkip(lum);
                data["LogLine"] = ShotLogLine(name, cw, ch, lum);
                return data;
            }
            catch (Exception ex) { return ShotFail("截图失败：" + ex.Message); }
            finally
            {
                if (g != null) { try { g.Dispose(); } catch { } }
                if (bmp != null) { try { bmp.Dispose(); } catch { } }
            }
        }

        // Get-FileHash -Algorithm MD5 给大写十六进制。
        static string Md5File(string path)
        {
            using (MD5 m = MD5.Create())
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] hash = m.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        // ==================== verb: ark-window-click(原 Send-GdArkClick + ark 靶子的 $click 闭包) ====================
        // 默认"抢前台 + 真实鼠标"，抢不到才退回 PostMessage —— 宁可点不动，也不能把真实点击漏进用户正在用的窗口。
        // 为什么不只靠 PostMessage、也别把它说成"MAA 也这么点"：两次实测都写在 lib\vision-ark.ps1 那段注释里。

        internal static object WindowClick(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            int x = Convert.ToInt32(OptLng(c, "clientX", 0), CultureInfo.InvariantCulture);
            int y = Convert.ToInt32(OptLng(c, "clientY", 0), CultureInfo.InvariantCulture);
            string name = OptStr(c, "name", delegate { return "端游窗口"; });
            string what = OptStr(c, "what", null);
            string why = OptStr(c, "why", null);

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["ClientX"] = x;
            data["ClientY"] = y;
            ArkWin w = WindowFrom(c);
            if (!w.Found)
            {
                data["Mode"] = "端游窗口已消失";
                data["Sent"] = false;
                data["TryPiece"] = "";
                data["LogLine"] = "";
                return data;
            }
            IntPtr hwnd = w.Handle;
            string mode;
            bool sent;
            if (GdWin.TryForeground(hwnd))
            {
                GdPoint pt = GdWin.ToScreen(hwnd, x, y);
                GdPoint old = GdWin.CursorPos();
                bool real = GdWin.RealClick(pt.X, pt.Y);
                System.Threading.Thread.Sleep(200);
                GdWin.SetCursorPos(old.X, old.Y);              // 鼠标放回原处，别留在游戏窗口里
                if (real) { mode = "真实点击"; sent = true; }
                else { mode = "PostMessage(没抢到前台，很可能点不动)"; sent = GdWin.ClickOnce(hwnd, x, y); }
            }
            else
            {
                mode = "PostMessage(没抢到前台，很可能点不动)";
                sent = GdWin.ClickOnce(hwnd, x, y);
            }
            data["Mode"] = mode;
            data["Sent"] = sent;
            // 旧实现在这里先打一行 WARN 日志、再把 "点位 X,Y(Mode)" 累进 tries 数组；两样都在这一枚出，
            // 编排侧只做"试完了用什么句子收尾"。
            data["TryPiece"] = "点位 " + x + "," + y + "(" + mode + ")";
            data["LogLine"] = "视觉兜底：" + what + " → 点" + name + " " + x + "," + y + "(" + why + ")";
            return data;
        }

        // ==================== B-2d③(10-12)：mumu(adb) 这一路的四枚现场件 ====================
        // 三句"端点没定下来"的话术在真身里**不成对称**，各抄各的：截图那一路是 Save-GdMuMuShot(lib\vision-mumu.ps1:85) 的
        // "adb 或序列号没定下来，拍不了屏"(它挡的是 Test-GdPathReady(路径在不在)+序列号空不空)，
        // 点击那一路是 $click 闭包(lib\vision-mumu.ps1:173) 的"adb 通道断了"(它只判端点算没算出来，连路径都不验)。
        // 别把两句统一成一句 —— 统一了就说明这一路不再照抄真身。
        // 出口格子也照真身：adb-connect 只回 Ok(真身回 [bool])、adb-game-pid 回 Found/Pid(真身回"整数或 $null"，
        // 核只问 Found)、mumu-screen-click 只回 Mode/Sent(vision-mumu.ps1:131-133 就这两格)。
        // ark 那一路的 TryPiece/LogLine 不在这里补：核拿 Mode 自己拼"点位 X,Y(Mode)→…"，补了就是给一条真身没有的话。

        sealed class AdbRun
        {
            public int Code;
            public string Out = "";
            public string Err = "";
        }

        // Invoke-GdNative 的同形：收两条流、超时把进程杀掉并回 Code=-1(与 RunChild 同一把尺子)。
        // 与 RunChild 唯一要紧的分别：**这里一个字都不注入环境变量** —— adb 用不着 key，
        // 少一处"顺手把父进程环境整块带过去"就少一处泄漏面。
        static AdbRun RunAdb(string adbExe, string[] args, int timeoutSeconds)
        {
            AdbRun r = new AdbRun();
            ProcessStartInfo psi = new ProcessStartInfo(adbExe);
            StringBuilder a = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) a.Append(' ');
                a.Append(GdAgentLink.QuoteArg(args[i]));        // 序列号/包名带空格也要保住一个参数是一参数
            }
            psi.Arguments = a.ToString();
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            try { psi.WorkingDirectory = GdEnv.Root; } catch { }
            try
            {
                using (Process p = Process.Start(psi))
                {
                    Task<string> tOut = p.StandardOutput.ReadToEndAsync();
                    Task<string> tErr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(timeoutSeconds * 1000))
                    {
                        try { p.Kill(); } catch { }
                        r.Code = -1;
                        r.Err = "timeout after " + timeoutSeconds + "s";
                        return r;
                    }
                    r.Code = p.ExitCode;
                    try { r.Out = tOut.Result ?? ""; } catch { r.Out = ""; }
                    try { r.Err = tErr.Result ?? ""; } catch { r.Err = ""; }
                    return r;
                }
            }
            catch (Exception ex)
            {
                // adb 自己被杀软拦/路径已经没了：旧实现是 Invoke-GdNative 回一个非 0 码 + stderr 一句话，
                // 这里同形成 Code=-1，让上层那句"退出码 -1"照实说出去(不另编一句人话)。
                r.Code = -1;
                r.Err = ex.Message;
                return r;
            }
        }

        // 真身那半句是 `($cap.Out + $cap.Err) -replace '\s+',' '` 再 `.Trim()`：**先拼后压**，两团之间不补空格。
        static string AdbFlatten(string outText, string errText)
        {
            return Regex.Replace((outText == null ? "" : outText) + (errText == null ? "" : errText), @"\s+", " ").Trim();
        }

        // verb: adb-connect —— 原 Connect-GdAdb(mumu.ps1:61-69) 的三步：先挡 adb 在场，再 connect(30s，回码真身不要，
        // 因为"连不上"本身就是要在 devices 里看出来的事)，再 devices(30s)，最后把 stdout 交给纯判定段 parse-adb-devices。
        // 那枚判定住 Processes.cs(批次 3)，这里同装配内直调，不 verb 套 verb。
        // 夹具档 noSpawn + fakeDevicesOut：自检要在不起进程的前提下钉住"接线"这一格(真跑一侧不许送)。
        internal static object AdbConnect(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string adb = OptStr(c, "adb", null);
            string serial = OptStr(c, "serial", null);
            if (adb.Length == 0)
                throw new GdRuleException("adb-connect：没给 adb(adb 可执行文件的路径属现场量，由编排侧算完端点送进来)");
            // 真身第一句 throw 的整字面(Test-Path 挡不住空串，Test-GdPathReady 那条注释就是对着这个写的)。
            if (!File.Exists(adb)) throw new GdRuleException("adb 不存在：" + adb);

            string devOut;
            if (OptFlag(c, "noSpawn", delegate { return false; })) devOut = OptStr(c, "fakeDevicesOut", null);
            else
            {
                RunAdb(adb, new string[] { "connect", serial }, 30);
                devOut = RunAdb(adb, new string[] { "devices" }, 30).Out;
            }
            Dictionary<string, object> d = Processes.AdbDevices(Sub("out", devOut, "serial", serial)) as Dictionary<string, object>;
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Ok"] = d != null && Gd.Truthy(d["Ok"]);
            return data;
        }

        // resolve-rescue-context 的闸⑥专用：真身 `.Alive`(vision-mumu.ps1:147-161) 那三步的复刻。
        // 与 `AdbConnect` 唯一要紧的分别是**次序**：真身先问 devices(20s)、序列号数得出来就直接算在场，
        // 数不出来才补一次 connect(它自己那条路是 connect→devices→parse) —— 别把这两枚合成一枚，
        // 合了就是"每次兜底都白跑一趟 connect"，而真身在设备本来就列着的时候一趟都不起。
        // 端点(adb 路径＋序列号)由编排侧喂(见文件头那条)，真身那一步 Get-GdMuMuAdbEndpoint 不在这里复刻；
        // 它拿不到端点时回的是 `$false` 而不是抛 ⇒ 这边同形：路径空/不存在一律回假，不起进程也不抛。
        static bool MumuAlive(string adb, string serial)
        {
            if (adb.Length == 0 || serial.Length == 0) return false;
            if (!File.Exists(adb)) return false;      // 真身：devices 起不来 → 补 connect → Test-Path 抛 → catch 回假
            AdbRun dev = RunAdb(adb, new string[] { "devices" }, 20);
            // 真身那句是 `([string]$dev.Out) -match [regex]::Escape($Ser)` —— 只看 stdout、按字面找序列号。
            if ((dev.Out ?? "").Contains(serial)) return true;
            try
            {
                Dictionary<string, object> d = AdbConnect(Sub("adb", adb, "serial", serial)) as Dictionary<string, object>;
                return d != null && Gd.Truthy(d["Ok"]);
            }
            catch (GdRuleException) { return false; }  // 真身那半边的 catch { return $false }
        }

        // verb: adb-game-pid —— 原 Get-Gd1999Pid(mumu.ps1:71-78)：adb -s <serial> shell pidof <package>(25s)，
        // stdout 交给 parse-adb-pidof。**一句守卫都不加**，真身也没挡 adb 在场与否(它就在循环里每轮问一次)，
        // 所以 adb 起不来会落成 Code=-1 + 空 stdout → Found:false，正是"游戏没在跑"那一格语义。
        internal static object AdbGamePid(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string adb = OptStr(c, "adb", null);
            string serial = OptStr(c, "serial", null);
            string package = OptStr(c, "package", null);
            string outText;
            if (OptFlag(c, "noSpawn", delegate { return false; })) outText = OptStr(c, "fakeOut", null);
            else outText = RunAdb(adb, new string[] { "-s", serial, "shell", "pidof", package }, 25).Out;
            Dictionary<string, object> d = Processes.AdbPidof(Sub("out", outText)) as Dictionary<string, object>;
            Dictionary<string, object> data = new Dictionary<string, object>();
            // Processes.AdbPidof 在"没数字"那一支只回 Found:false(不带 Pid 键)，所以这里走 Gd.Get 而不是裸索引：
            // 裸索引会当场 KeyNotFoundException(实测踩过)。Found:false 时 Pid 恒 0，与旧壳"回 $null"同语义。
            data["Found"] = d != null && Gd.Truthy(Gd.Get(d, "Found"));
            data["Pid"] = Gd.Truthy(data["Found"]) ? Num(Gd.Get(d, "Pid")) : 0L;
            return data;
        }

        // verb: input-desktop-name —— 刀⑰ 笔(b)(2026-11-07)：Get-GdInputDesktopName(common.ps1 真身，随批出仓；
        // 连带 $global:GdDeskTypeDef 与 tools\gdinterop\GdDesk.cs 整枚退役——Add-GdInterop 按目录现扫，名册零手改)
        // 的出口露出：读的就是 GdWin.InputDesktopName 那把尺(OpenInputDesktop/GetUserObjectInformationW/CloseDesktop
        // 全封装直身，拿不到回空串)，规则零新增。body {}(desk 夹具键给了原样回，与 ArkDesktopState:251 同一形制)；
        // 回执 {Name}；读不到 ⇒ ''(不 throw)。unarm-screensaver 的 ''⇒印「读不到」那一路由消费面自证(真调用盲区已登记)。
        internal static object InputDesktopNameV(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string desk = OptStr(c, "desk", delegate { return GdWin.InputDesktopName(); });
            return Gd.Out("Name", desk == null ? "" : desk);
        }

        // 尺寸闸这一句与 ark 的两句**不同源**(ark 看窗口矩形+"还最小化吗"，mumu 看安卓给回来的 PNG 尺寸)，
        // 所以不复用 ShotSizeWhy，也不许"顺手统一"。阈值两路都是 200x150。
        internal static string MuMuSizeWhy(int w, int h)
        {
            if (w >= 200 && h >= 150) return "";
            return "模拟器截屏尺寸异常 " + w + "x" + h + "(安卓那边还没起画面)";
        }

        // 读图段(可无头)：打开 PNG → 尺寸闸 → **复用 Live.Stats**(144 格签名与平均亮度两路同一套算术，
        // 真身那边也是同一个 Get-GdShotStats) → 回执。真身回十二格(vision-mumu.ps1:107-111)，这里补
        // CanContinue/Skip/LogLine 三格只为让核侧两路消费同一形状(黑屏闸与那句"已拍…"在通用层，不按 kind 分派)；
        // 十二格本身一字未动，ark 那十五格的键名与顺序也照旧。
        internal static Dictionary<string, object> MuMuShotStats(string pngPath, string path, string name)
        {
            string hash = "";
            Bitmap bmp = null;
            try
            {
                // 先哈希再解码：new Bitmap(path) 会占住这个文件(GDI+ 的锁)，顺序反了就是自己撞自己。
                // 真身哈希的是 pull 回来那张($Path)；夹具把图喂在别处时按"实际读到的那一张"算。
                hash = Md5File(pngPath);
                bmp = new Bitmap(pngPath);
                int w = bmp.Width, h = bmp.Height;
                string sizeWhy = MuMuSizeWhy(w, h);
                if (sizeWhy.Length > 0) return ShotFail(sizeWhy);
                Dictionary<string, object> stats = Stats(bmp, w, h, 7);
                long lum = Convert.ToInt64(stats["AvgLum"], CultureInfo.InvariantCulture);

                Dictionary<string, object> data = new Dictionary<string, object>();
                data["Ok"] = true;
                data["Path"] = path;
                data["ImgW"] = w;
                data["ImgH"] = h;
                data["ClientLeft"] = 0;      // 整块屏就是"客户区"：真身那四格就是这么写的(lib\vision-mumu.ps1:109)，
                data["ClientTop"] = 0;       // 于是通用层的千分比换算直接落在设备像素上
                data["ClientW"] = w;
                data["ClientH"] = h;
                data["AvgLum"] = lum;
                data["Sig"] = stats["Sig"];
                data["Hash"] = hash;
                data["Why"] = "";
                data["CanContinue"] = lum >= 8;
                data["Skip"] = lum >= 8 ? "" : BlackScreenSkip(lum);
                data["LogLine"] = ShotLogLine(name, w, h, lum);
                return data;
            }
            catch (Exception ex) { return ShotFail("读不了这张 PNG：" + ex.Message); }
            finally { if (bmp != null) { try { bmp.Dispose(); } catch { } } }
        }

        // verb: mumu-screen-shot —— 原 Save-GdMuMuShot(75-117) 的两步制：设备里落一张 + pull 出来。
        // 为什么不用 `exec-out screencap -p` 直接接 stdout(真身注释原话)：子进程输出按**文本**读，
        // PNG 二进制过一遍编码就废了 —— 这一条在 C# 这边一样成立，所以两步制不是历史包袱。
        // 夹具档 localPng：图已经在本地(自检在 %TEMP% 造真 PNG 走读图段；门里那一侧不喂，走真 adb)。
        internal static object MuMuShot(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string path = OptStr(c, "path", null);
            if (path.Length == 0) throw new GdRuleException("mumu-screen-shot：没给 path(截图要落盘的路径属现场量，由编排侧拼 mumu-<Tag>-时间-before.png 送进来)");
            string name = OptStr(c, "name", delegate { return "MuMu 模拟器画面"; });
            string png = OptStr(c, "localPng", null);
            if (png.Length > 0) return MuMuShotStats(png, path, name);

            string adb = OptStr(c, "adb", null);
            string serial = OptStr(c, "serial", null);
            if (!File.Exists(adb) || serial.Length == 0) return ShotFail("adb 或序列号没定下来，拍不了屏");

            const string remote = "/sdcard/gd-rescue-shot.png";
            AdbRun cap = RunAdb(adb, new string[] { "-s", serial, "shell", "screencap", "-p", remote }, 40);
            if (cap.Code != 0)
                return ShotFail("screencap 失败(退出码 " + cap.Code + ")：" + AdbFlatten(cap.Out, cap.Err));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AdbRun pull = RunAdb(adb, new string[] { "-s", serial, "pull", remote, path }, 40);
            // 真身这一格的判据是 `-or -not (Test-Path $Path)`：pull 回码 0 却没落地也算失败(安卓那边骗人)，照抄。
            if (pull.Code != 0 || !File.Exists(path))
                return ShotFail("pull 失败(退出码 " + pull.Code + ")：" + AdbFlatten(pull.Out, pull.Err));
            return MuMuShotStats(path, path, name);
        }

        // verb: mumu-screen-click —— 原 Send-GdMuMuTap(119-134)：adb shell input tap，30 秒。
        // 为什么这里可以很"直接"、不需要端游那套"抢前台才敢真实点击"(真身注释原话)：
        // 事件投进的是虚拟机里的安卓，宿主机的前台窗口与用户正在用的程序都碰不到。
        // 夹具档 fakeCode：只钉三句 Mode 的分派，不起进程(与上面几枚同一身份)。
        internal static object MuMuClick(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            int x = Convert.ToInt32(OptLng(c, "clientX", 0), CultureInfo.InvariantCulture);
            int y = Convert.ToInt32(OptLng(c, "clientY", 0), CultureInfo.InvariantCulture);
            string adb = OptStr(c, "adb", null);
            string serial = OptStr(c, "serial", null);
            Dictionary<string, object> data = new Dictionary<string, object>();
            if (adb.Length == 0 || serial.Length == 0)
            {
                data["Mode"] = "adb 通道断了";
                data["Sent"] = false;
                return data;
            }
            int code;
            if (Given(c, "fakeCode")) code = (int)OptLng(c, "fakeCode", 0);
            else
                code = RunAdb(adb, new string[] { "-s", serial, "shell", "input", "tap",
                        x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture) }, 30).Code;
            data["Mode"] = code != 0 ? "adb input tap 失败(退出码 " + code + ")"
                                     : "adb input tap(点进虚拟机，不碰宿主机窗口)";
            data["Sent"] = code == 0;
            return data;
        }

        // ==================== verb: resolve-rescue-context(七道闸里那六道现场闸) ====================
        // 闸门顺序不许调换(旧对面 lib\vision-rescue.ps1 的 Invoke-GdVisionRescue 头注明写，那枚壳已随第二十刀刀③ 出仓 ⇒ 这段顺序自此只住这里)：
        // 后端在场 → 开关 → 权限 → key → Python → 对象在场。第七道"截图"与黑屏闸在 ark-window-shot 里，
        // 因为那一道必须先拍到东西才判得了。这一枚一个像素都不拍、也不起 python 探测以外的子进程。
        // skip 话术全部出自这里，编排侧一个字不改 —— 关开关那句必须带 settings 的键名，--livetest 按那几个字断言"关了确实一次都不试"(旧的 t13 真跑两格随摘件改口到这里)。

        internal static object RescueContext(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string target = OptStr(c, "target", delegate { return "ark"; });
            string kind = OptStr(c, "kind", delegate { return target; }).ToLowerInvariant();  // New-GdVisionTarget-$($Kind.ToLowerInvariant())
            // 10-12(B-2d③)：原来一枚 `known` 就够(只认 ark)，现在拆成两路各自判 —— 闸①的默认值是
            // "这棵树上有没有这一路的靶子可建"(ark=New-GdVisionTarget-ark、mumu=New-GdVisionTarget-mumu 都在)，
            // 而其余每一格的默认值都**按路**取，不共用一个"不认识就回空串"的偷懒分支。
            bool isArk = kind == "ark";
            bool isMumu = kind == "mumu";
            bool known = isArk || isMumu;

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Target"] = target;
            data["Kind"] = kind;
            data["Name"] = OptStr(c, "name", delegate { return isArk ? "端游窗口" : (isMumu ? "MuMu 模拟器画面" : ""); });
            data["NeedsAdmin"] = OptFlag(c, "needsAdmin", delegate { return isArk; });   // mumu：adb 连的是虚拟机里的安卓，宿主机提不提权与它无关
            data["AdminWhy"] = OptStr(c, "adminWhy", delegate { return isArk ? "端游是 requireAdministrator，这个权限下点它不会有任何反应" : ""; });
            // ark 两格默认空串(= 用 C# 那两份默认话术，逐字不许变)；mumu 两份默认话术在靶子里写死 ⇒ 双源，见文件头。
            data["Scene"] = OptStr(c, "scene", delegate { return isMumu ? MuMuScene : ""; });
            data["UserText"] = OptStr(c, "userText", delegate { return isMumu ? MuMuUserText : ""; });
            data["Enabled"] = OptFlag(c, "enabled", delegate { return isArk ? ArkVisionOn() : (isMumu ? MumuVisionOn() : true); });

            if (!OptFlag(c, "hasBuilder", delegate { return known; }))
                return No(data, "这棵树上没有 " + target + " 这一路的视觉兜底后端(没有 New-GdVisionTarget-" + target + " 这个函数)");
            if (!Gd.Truthy(data["Enabled"]))
                return No(data, OptStr(c, "why", delegate
                {
                    if (isArk) return "arkVisionRescue=false，视觉兜底已关闭";
                    if (isMumu) return "mumuVisionRescue=false，模拟器这一路的视觉兜底已关闭";
                    return "";
                }));
            if (Gd.Truthy(data["NeedsAdmin"]) && !OptFlag(c, "isAdmin", delegate { return IsAdminNow(); }))
            {
                string why = Gd.Str(data["AdminWhy"]);
                if (why.Length == 0) why = "这一路的对象要管理员权限，这个权限下点它不会有任何反应";
                return No(data, "当前不是管理员权限：" + why + "，跳过兜底");
            }
            if (!OptFlag(c, "hasKey", delegate { return VisionKey().Length > 0; }))
                return No(data, "没配视觉模型 key(见 settings 里的 runtime.visionApiKey)，本轮不尝试兜底");
            string py = OptStr(c, "pythonExe", delegate { return PythonExe(); });
            data["HasPython"] = py.Length > 0;
            if (py.Length == 0)
                return No(data, "这台机器上没有可用的 Python 3(问模型那一层用它跑)：跑 setup.ps1 -WithPython 装一份到 state\\python\\，或把 runtime.pythonExe 指到已有的那份");

            bool alive = OptFlag(c, "alive", delegate
            {
                if (isArk) return FindArk(ProcNameOf(c)).Found;      // ark：按进程名现枚举一次(不碰窗口)
                if (isMumu)
                {
                    // mumu 这一路的"对象在场"= adb 端点算得出来且 devices/connect 认这台设备。端点那三源两正则
                    // (见文件头那条)绝不在这侧做 —— adb 路径与序列号由编排侧当键喂进来，其余三步在这一侧现查，
                    // 与 ark 那一路"按进程名现枚举一次"同形。
                    // 10-12 实测改口：原 §11.42 ①-12 定的是"核先调 adb-connect 再把 Ok 当 alive 喂进来"，
                    // 而那条把 alive 变成这一问的**入参** ⇒ 核只能排在 ctx 之前；真身的序是"先拿靶子(=这一问)、
                    // 过前五道闸、第六道闸才碰 adb" ⇒ m9arun 门的 H2 当场红 8 处(投影上多一枚 V|adb-connect、
                    // 顺序还正反)，并且"没配 key／开关关着"那一档核会为真身根本不起的那次 adb 调用白起两趟 native。
                    // ⇒ 现查移回这一侧。`alive` 覆盖格**保留**，但身份只剩无头自检(§11.42 ①-9 那条射程边界)。
                    string adb = OptStr(c, "adb", null);
                    string ser = OptStr(c, "serial", null);
                    // 两枚键一枚都没给 = 编排忘了喂端点(编程期错误)，照旧当场抛，绝不猜一个"在"把兜底放行。
                    if (!Given(c, "adb") && !Given(c, "serial"))
                        throw new GdRuleException("resolve-rescue-context：mumu 这一路要么把 adb/serial 喂进来由这一侧现查 alive，要么显式喂 alive 覆盖格(端点三源两正则不住这一侧)");
                    return MumuAlive(adb, ser);
                }
                return true;      // 走不到：闸①已经把不认识的 kind 拦下了
            });
            if (!alive) return No(data, Gd.Str(data["Name"]) + "已经不在了，兜底没有对象");

            data["CanAsk"] = true;
            data["Skip"] = "";
            return data;
        }

        static Dictionary<string, object> No(Dictionary<string, object> data, string why)
        {
            data["CanAsk"] = false;
            data["Skip"] = why;
            return data;
        }

        // arkVisionRescue 的开关语义是"没写就是开着"(旧实现：$S.runtime.arkVisionRescue -ne $false) ——
        // 只有明写成 false 才关，缺键/null/true 都算开。
        static bool ArkVisionOn()
        {
            object v = Runtime("arkVisionRescue");
            if (v is bool) return (bool)v;
            return true;
        }

        // mumuVisionRescue 的开关语义与端游那一个**一致**(旧实现 $S.runtime.mumuVisionRescue -ne $false)：
        // 没写就是开着，只有明写成 false 才关。
        static bool MumuVisionOn()
        {
            object v = Runtime("mumuVisionRescue");
            if (v is bool) return (bool)v;
            return true;
        }

        // mumu 靶子的两份默认话术，逐字抄自 lib\vision-mumu.ps1:184-189(含 .TrimEnd()：末尾那个换行不进内容)。
        // 为什么两份要在这里抄第二份：核那一侧只从 resolve-rescue-context 的回辞取 Scene/UserText
        // (main.rs 问模型那四键就是原样转达)，而 vision-mumu.ps1 整份留在 PS 侧还在用 ⇒ **改一处必须两处同改**，
        // --livetest 第 9 组与 m9a 那两侧的门钉不住对方，只有这条注释负责提醒。
        internal const string MuMuScene =
            "你是《重返未来：1999》自动化流程里的\"看屏幕的人\"。游戏跑在 MuMu 模拟器里，自动化助手(M9A)只认\n" +
            "自己模板集里的界面，遇到没收录过的弹窗或更新提示就会一直在原地重试。给你一张模拟器屏幕截图，\n" +
            "请判断画面上有没有挡住主流程的东西(活动公告、更新/下载提示、登录弹窗、网络错误框这类)，并指出该点哪里把它关掉。";

        internal const string MuMuUserText = "这是模拟器里 1999 的当前画面。按规则只回一个 JSON 对象。";

        // ==================== verb: vision-ask-model(原 Invoke-GdVisionRaw + Invoke-GdVisionAsk) ====================
        // 一次"问模型"的完整往返：装请求 → 起 Python → 回扫 stdout → 顺手抠出 verdict。
        // 起子进程与 key 都只在这一侧，Rust 核不许知道它们；HTTP 分类与 429/5xx 退避重试仍归 tools\vision\ask.py。
        // 分工没变的那一半：抠 JSON 的规则仍在 Vision.cs(parse-vision-verdict)，这里直接**同装配内调**它，不 verb 套 verb。

        // 用户那句话 + 处境。截 200 字的规矩是"这一句是给模型听的处境说明，不是日志转储"(旧注释原话)，
        // 拼接串里那两个空格也是原样("$UserText  自动化这边的处境：")。
        internal static string AskUserText(string userText, string context)
        {
            string t = userText == null ? "" : userText;
            if (t.Length == 0) t = "这是当前画面。按规则只回一个 JSON 对象。";
            string ctx = context == null ? "" : context;
            if (ctx.Length > 0)
            {
                ctx = Regex.Replace(ctx, @"\s+", " ");
                if (ctx.Length > 200) ctx = ctx.Substring(0, 200) + "…";
                t = t + "  自动化这边的处境：" + ctx;
            }
            return t;
        }

        // 请求文件里的九个字段：顺序照旧实现的 [ordered]@{}(那是本层唯一的协议)。key 不在里面，一个都不许进。
        // sceneId(10-11 追加，第十个字段**不进请求文件**)：只是告诉 BuildPrompt 走哪一档话术，
        // ask.py 那边一个字段都不用改 —— 启动器那一档换的只是 system_prompt 的内容。
        internal static Dictionary<string, object> BuildRequest(string imagePath, string scene, string userText,
            string model, int sec, bool allowReward, string baseUrl, string reqPath, string auditPath)
        {
            return BuildRequest(imagePath, scene, userText, model, sec, allowReward, baseUrl, reqPath, auditPath, null);
        }

        internal static Dictionary<string, object> BuildRequest(string imagePath, string scene, string userText,
            string model, int sec, bool allowReward, string baseUrl, string reqPath, string auditPath, string sceneId)
        {
            Dictionary<string, object> req = new Dictionary<string, object>();
            req["image"] = imagePath;
            req["system_prompt"] = Vision.BuildPrompt(sceneId, scene, allowReward);
            req["user_text"] = userText;
            Dictionary<string, object> ep = Vision.Endpoint(Sub("base", baseUrl)) as Dictionary<string, object>;
            req["endpoint"] = ep == null ? "" : Gd.Str(ep["Url"]);
            req["model"] = model;
            req["temperature"] = 0;                 // 兜底要的是可复现，不是发挥
            req["max_tokens"] = 300;
            req["timeout_seconds"] = sec;
            req["audit"] = auditPath;
            return req;
        }

        // 子进程回来以后那团读数的解读：Code=-1 是"整段没回来"(超时)，其余按旧实现的三段兜次说人话；
        // stdout 逐行挑哪个对象仍住在 Vision.cs(parse-vision-reply)，包括"解释器先打了版本警告"这一格实测坑。
        internal static Dictionary<string, object> MapChild(int code, string outText, string errText, string reqPath, string nowText)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["RequestPath"] = reqPath;
            object parsed = null;
            Dictionary<string, object> pick = Vision.Reply(Sub("out", outText == null ? "" : outText)) as Dictionary<string, object>;
            if (pick != null && Gd.Truthy(pick["Found"])) parsed = Gd.Get(pick, "Parsed");

            if (parsed == null)
            {
                string why = "Python 没回 JSON(退出码 " + code + ")";
                if (code == -1) why = "问模型超时(Python 侧 " + nowText + " 前没回)";
                else
                {
                    string tail = (errText == null ? "" : errText).Trim();
                    if (tail.Length > 0) why += "：" + tail;
                    else
                    {
                        string o = (outText == null ? "" : outText).Trim();
                        if (o.Length > 0) why += "：" + o;
                    }
                }
                data["Ok"] = false;
                data["Error"] = why;
                return data;
            }
            Dictionary<string, object> p = parsed as Dictionary<string, object>;
            data["Ok"] = Gd.Truthy(Gd.Get(p, "ok"));
            data["Text"] = StrOf(Gd.Get(p, "text"), "");
            data["Model"] = StrOf(Gd.Get(p, "model"), "");
            data["HttpStatus"] = Num(Gd.Get(p, "http_status"));
            data["ElapsedMs"] = Num(Gd.Get(p, "elapsed_ms"));
            data["Attempts"] = Num(Gd.Get(p, "attempts"));
            data["Error"] = StrOf(Gd.Get(p, "error"), "");
            return data;
        }

        internal static object VisionAsk(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            string imagePath = OptStr(c, "imagePath", null);
            if (imagePath.Length == 0) throw new GdRuleException("vision-ask-model：没给 imagePath(问模型必须有一张已经落盘的截图)");
            string scene = OptStr(c, "scene", null);
            string sceneId = OptStr(c, "sceneId", null);
            string context = OptStr(c, "context", null);

            // 三道前置与旧实现同一个顺序：key → ask.py → Python。
            if (!OptFlag(c, "hasKey", delegate { return VisionKey().Length > 0; }))
                return AskFail("没有视觉模型的 API key(settings.runtime.visionApiKey 或环境变量 DASHSCOPE_API_KEY)", "");
            string script = OptStr(c, "scriptPath", delegate { return Path.Combine(GdEnv.Root, @"tools\vision\ask.py"); });
            if (!File.Exists(script)) return AskFail("缺少 tools\\vision\\ask.py(问模型这一层的实现)，这一棵树上问不了模型", "");
            string py = OptStr(c, "pythonExe", delegate { return PythonExe(); });
            if (py.Length == 0)
                return AskFail("没找到可用的 Python 3(问模型这一层用它跑)：跑 setup.ps1 -WithPython 自动装一份到 state\\python\\，或把 runtime.pythonExe 指到自己装的那份", "");

            string model = OptStr(c, "model", delegate { return StrOf(Runtime("visionModel"), "qwen3.8-flash"); });
            // 缺键才去读 settings(理由见 OptStr 那条：自检不许被 settings 拖着走)；给了就按给的走同一条钳。
            int sec = SecOf(Given(c, "timeoutSeconds") ? Gd.Get(c, "timeoutSeconds") : Runtime("visionTimeoutSeconds"));
            string userText = AskUserText(OptStr(c, "userText", null), context);
            string dir = OptStr(c, "askDir", delegate { return Path.Combine(GdEnv.Root, @"state\vision"); });
            string reqPath = Path.Combine(dir, "ask-" + DateTime.Now.ToString("MMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".json");
            string audit = Path.Combine(dir, "calls.jsonl");

            Dictionary<string, object> data;
            if (OptFlag(c, "noSpawn", delegate { return false; }))
            {
                // 夹具档：不起子进程，直接把"子进程会给回的东西"手递进来(自检与对拍门都走这一格)。
                data = MapChild((int)OptLng(c, "fakeCode", 0), OptStr(c, "fakeOut", null), OptStr(c, "fakeErr", null),
                    reqPath, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                data["Model"] = model;
                data["SkippedSpawn"] = true;
            }
            else
            {
                GdJson.Write(reqPath, BuildRequest(imagePath, scene, userText, model, sec,
                    OptFlag(c, "allowReward", delegate { return Gd.Truthy(Runtime("visionAllowReward")); }),
                    OptStr(c, "baseUrl", delegate { return StrOf(Runtime("visionBaseUrl"), ""); }), reqPath, audit,
                    sceneId.Length == 0 ? null : sceneId));
                data = RunChild(py, script, reqPath, sec * 3 + 30, Given(c, "hasKey"));
                if (Gd.Truthy(data["Ok"])) TrimAskDir(dir, 12);   // 问成了才清(旧实现同形；calls.jsonl 那本账不删)
            }
            if (!Gd.Truthy(data["Ok"]))
            {
                if (!Given(data, "Error")) data["Error"] = "";
                data["Verdict"] = null;
                return data;
            }
            Dictionary<string, object> v = null;
            string text = StrOf(Gd.Get(data, "Text"), "");
            if (text.Trim().Length > 0) v = Vision.Verdict(Sub("text", text)) as Dictionary<string, object>;
            data["Verdict"] = v;              // 空文本/抠不出 JSON → null，与旧实现"返回 $null"同形
            return data;
        }

        static Dictionary<string, object> AskFail(string err, string reqPath)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["Ok"] = false;
            d["Error"] = err;
            d["RequestPath"] = reqPath;
            d["Verdict"] = null;
            return d;
        }

        static int SecOf(object v)
        {
            int sec;
            try { sec = Convert.ToInt32(v == null ? 0 : v, CultureInfo.InvariantCulture); } catch { sec = 0; }
            return sec <= 0 ? 45 : sec;
        }

        // Invoke-GdChild 的现场版：只给这一个子进程加 DASHSCOPE_API_KEY，其余环境照父进程；
        // 总超时 = 单次超时 ×(重试次数+1)+ 启动余量，超时把进程杀掉并回 Code=-1 —— 旧实现同形。
        static Dictionary<string, object> RunChild(string py, string script, string reqPath, int timeoutSeconds, bool injectedKey)
        {
            ProcessStartInfo psi = new ProcessStartInfo(py);
            // 路径里有空格(用户名带空格太常见了)必须自己加引号 —— Invoke-GdChild 那条理由在这儿一样成立。
            psi.Arguments = GdAgentLink.QuoteArg(script) + " --request " + GdAgentLink.QuoteArg(reqPath);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            try { psi.WorkingDirectory = GdEnv.Root; } catch { }
            try { psi.EnvironmentVariables["DASHSCOPE_API_KEY"] = injectedKey ? "(injected)" : VisionKey(); } catch { }
            try
            {
                using (Process p = Process.Start(psi))
                {
                    Task<string> tOut = p.StandardOutput.ReadToEndAsync();
                    Task<string> tErr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(timeoutSeconds * 1000))
                    {
                        try { p.Kill(); } catch { }
                        return MapChild(-1, "", "timeout after " + timeoutSeconds + "s", reqPath,
                            DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                    }
                    int code = p.ExitCode;
                    string outText, errText;
                    try { outText = tOut.Result; } catch { outText = ""; }
                    try { errText = tErr.Result; } catch { errText = ""; }
                    return MapChild(code, outText, errText, reqPath, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex)
            {
                // 起进程本身就失败(解释器被杀软拦/路径没了)：旧实现是整层上抛、由兜底 catch 成"模型调用失败"。
                // verb 不抛(抛了就成了 exit 1 的内部错)，照实回一句原话给编排去包那一层。
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["Ok"] = false;
                d["Error"] = ex.Message;
                d["RequestPath"] = reqPath;
                return d;
            }
        }

        // Clear-GdVisionFiles 的同款：按最后写入时间留最近 Keep 个。Pattern 只吃 ask-*.json，
        // calls.jsonl 不在里面(它是账本，一行一条请求，不随兜底清)。
        internal static int TrimAskDir(string dir, int keep)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return 0;
            List<FileInfo> files = new List<FileInfo>();
            foreach (string f in Directory.GetFiles(dir, "ask-*.json"))
            {
                try { files.Add(new FileInfo(f)); } catch { }
            }
            files.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
            int gone = 0;
            for (int i = keep; i < files.Count; i++)
            {
                try { files[i].Delete(); gone++; } catch { }
            }
            return gone;
        }

        // ==================== verb: check-ark-launcher(10-11 新增：启动器更新页的相位判定) ====================
        // 背景(2026-10-11 06:00 那班)：版本日端游会停在启动器的更新页上，得有人点『更新』才开始拉资源。
        // 旧判定(PS 真身 Start-GdArkClient，11-03 转接刀·方舟拉起族整枚出仓)只等"窗口标题出现"，而**更新页的标题和主界面都是"明日方舟"**(现场实锤：
        // 那班的"端游窗口就绪 [Arknights#31564]"拍到的其实是启动器页)⇒ 标题不是分岔，像素才是。
        // 这一枚把"看一眼这一页"做成一次完整往返：找窗口 → 拍图 → 按 ark-launcher 那一档话术问模型 → 抠点位 → 出相位。
        // 相位映射是判据，所以住在 C#；睡眠、循环、真点击的"活"自此在 Chain.cs 那一发 start-ark-client 的直答里(旧 lib\maa.ps1 的 Wait-GdArkLauncherReady 已于 11-03 转接刀·方舟拉起族整枚出仓)。
        //
        // 五档相位(编排侧按字面分派，别猜)：
        //   no-window   端游窗口没找到 —— 检查没有对象，立刻收工(不等，窗口不会自己长回来)
        //   uncertain   拍不到/问不动/抠不出 JSON/点击预算用尽 —— **交回原路继续**，这一枚不许把日常链拦死
        //   needs-click 这一页有个要点的东西，点位已换算成客户区坐标(编排侧调 ark-window-click 真点一次)
        //   waiting     正在下载/校验/拿不准 —— 继续等，不许点(点了可能打断下载)
        //   clear       模型说这一页不用管了 —— 可以放行走 MAA
        //
        // 夹具注入(自检与对拍门用，真跑一侧不许送)：shot=直接给一张"已经拍好的图"的读数，绕过真 PrintWindow；
        // clicksAlready/maxClicks 由编排送进来(计数的"活"在那侧)，noSpawn/fakeOut 等按 vision-ask-model 原样透传。
        // 花钱口径：每一档最多问模型一次；no-window/拍不到 那两档**一次都不问**。

        static Dictionary<string, object> Phase(Dictionary<string, object> data, string phase, string logLine, string level)
        {
            data["Phase"] = phase;
            data["LogLine"] = logLine == null ? "" : logLine;
            data["Level"] = level == null ? "" : level;
            return data;
        }

        internal static object ArkLauncherCheck(Dictionary<string, object> raw)
        {
            Dictionary<string, object> c = View(raw);
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["Phase"] = "";
            data["LogLine"] = "";
            data["Level"] = "";
            data["Detail"] = "";
            data["Error"] = "";
            data["What"] = "";
            data["Model"] = "";
            data["ShotPath"] = "";
            data["ClientX"] = 0;
            data["ClientY"] = 0;
            data["Why"] = "";

            int clicksAlready = (int)Num(Gd.Get(c, "clicksAlready"));
            object mv = Given(c, "maxClicks") ? Gd.Get(c, "maxClicks") : Runtime("arkLauncherMaxChecks");
            int maxClicks = Num(mv) > 0 ? (int)Num(mv) : 6;    // 缺键/写坏(0 或负) → 6；不许让配置一坏就变成"一次都不许点"的死闸
            data["ClicksAlready"] = clicksAlready;
            data["MaxClicks"] = maxClicks;

            // 1) 找窗口：句柄/进程号一并带回，编排侧点的时候不必再找一遍(再找一次就多一次枚举，
            //    而"点的时候窗口已经换了"这种竞争，用同一个句柄反而更容易在回执里看出来)。
            ArkWin w = WindowFrom(c);
            data["Handle"] = w.Found ? w.Handle.ToInt64() : 0L;
            data["ProcId"] = w.Found ? w.ProcId : 0L;
            data["Title"] = w.Title;
            if (!w.Found)
            {
                data["Detail"] = "端游窗口没找到(可能已经关了)";
                return Phase(data, "no-window", "启动器检查：端游窗口没找到(可能已经关了)，这一轮不再检查更新页", "WARN");
            }

            // 2) 拍一张(尺寸闸/黑屏闸都在 WindowShot 里，那一套话术原样复用，这里不重说一遍)
            string path = OptStr(c, "path", delegate
            {
                return Path.Combine(Path.Combine(GdEnv.Root, @"state\vision"),
                    "ark-launcher-" + DateTime.Now.ToString("MMdd-HHmmss", CultureInfo.InvariantCulture) + ".png");
            });
            Dictionary<string, object> shot;
            if (Given(c, "shot"))
            {
                Dictionary<string, object> inj = c["shot"] as Dictionary<string, object>;
                shot = inj == null ? new Dictionary<string, object>() : Gd.Ci(inj);
            }
            else
            {
                shot = WindowShot(Sub("path", path, "handle", w.Handle.ToInt64(), "name", "启动器窗口"))
                       as Dictionary<string, object>;
                if (shot == null) shot = ShotFail("截图没有回执(内部返回了空对象)");
            }
            data["AvgLum"] = Num(Gd.Get(shot, "AvgLum"));
            string shotPath = StrOf(Gd.Get(shot, "Path"), "");
            data["ShotPath"] = shotPath;
            if (!Gd.Truthy(Gd.Get(shot, "Ok")) || !Gd.Truthy(Gd.Get(shot, "CanContinue")))
            {
                string why = StrOf(Gd.Get(shot, "Skip"), "");
                if (why.Length == 0) why = StrOf(Gd.Get(shot, "Why"), "截图没成功");
                data["Detail"] = why;
                return Phase(data, "uncertain", "启动器检查：拍不到画面(" + why + ")，分不清是不是更新页，按原样放行", "WARN");
            }

            // 3) 问模型(走已立的 vision-ask-model 那一条，同装配内直接调；sceneId 换成启动器那一档话术)
            Dictionary<string, object> body = new Dictionary<string, object>(c, StringComparer.OrdinalIgnoreCase);
            body["imagePath"] = shotPath.Length == 0 ? path : shotPath;
            body["sceneId"] = "ark-launcher";
            body["scene"] = "";                      // 启动器那一档不看 scene：整段 head/body 都换掉了
            if (!Given(body, "context"))
                body["context"] = "这是明日方舟端游的启动器/更新器窗口。自动化要等它把资源更新完、进到游戏里才能开工。";
            Dictionary<string, object> ask = VisionAsk(body) as Dictionary<string, object>;
            if (ask == null)
            {
                data["Detail"] = "问模型没有回执";
                return Phase(data, "uncertain", "启动器检查：问模型没有回执，按原样放行", "WARN");
            }
            data["Model"] = StrOf(Gd.Get(ask, "Model"), "");
            if (!Gd.Truthy(Gd.Get(ask, "Ok")))
            {
                string err = StrOf(Gd.Get(ask, "Error"), "问模型没成功");
                data["Error"] = err;
                data["Detail"] = err;
                return Phase(data, "uncertain", "启动器检查：问模型没成功(" + err + ")，分不清是不是更新页，按原样放行", "WARN");
            }
            Dictionary<string, object> v = Gd.Get(ask, "Verdict") as Dictionary<string, object>;
            if (v == null)
            {
                data["Detail"] = "模型回话里抠不出 JSON";
                return Phase(data, "uncertain", "启动器检查：模型回话里抠不出 JSON，按原样放行", "WARN");
            }
            data["What"] = StrOf(Gd.Get(v, "What"), "");

            // 4) 相位映射
            if (!Gd.Truthy(Gd.Get(v, "Blocking")))
            {
                data["Detail"] = "不在更新页";
                return Phase(data, "clear", "启动器检查：已经不在更新页了(模型说：" + data["What"] + ")，可以开始了", "OK");
            }

            // 要点东西 → 先换算点位(千分比→客户区那把尺子在 Vision.cs，越界一律拒)：
            // verdict 的 X/Y 是 clicks 第一枚，缺任一就没得点(与 lib\vision-rescue.ps1 的前置同一形状)。
            object vx = Gd.Get(v, "X");
            object vy = Gd.Get(v, "Y");
            if (vx == null || vy == null)
            {
                data["Detail"] = "在下载或拿不准";
                return Phase(data, "waiting", "启动器检查：这一页还不能进游戏(模型说：" + data["What"] + ")，没有该点的地方，继续等", "");
            }
            Dictionary<string, object> cp = Vision.ClickPoint(Sub(
                "nx", vx, "ny", vy,
                "imgW", Num(Gd.Get(shot, "ImgW")), "imgH", Num(Gd.Get(shot, "ImgH")),
                "clientLeft", Num(Gd.Get(shot, "ClientLeft")), "clientTop", Num(Gd.Get(shot, "ClientTop")),
                "clientW", Num(Gd.Get(shot, "ClientW")), "clientH", Num(Gd.Get(shot, "ClientH")),
                "max", 1000, "why", StrOf(Gd.Get(v, "Why"), ""))) as Dictionary<string, object>;
            if (cp == null || !Gd.Truthy(Gd.Get(cp, "Found")))
            {
                // 模型给了点位却越界/换算不出来：宁可当"这一页还要等"，不要凭半个坐标真去点一下。
                data["Detail"] = "模型给的点位用不了(" + StrOf(vx, "?") + "," + StrOf(vy, "?") + ")";
                return Phase(data, "waiting", "启动器检查：模型想指一个地方但点位用不了(" + data["Detail"] + ")，继续等", "WARN");
            }
            if (clicksAlready >= maxClicks)
            {
                data["Detail"] = "已经点过 " + clicksAlready + " 次还在要点击";
                return Phase(data, "uncertain", "启动器检查：已经点过 " + clicksAlready + " 次还在要点『更新』(不像是点一次就完的事)，按原样放行", "WARN");
            }
            data["ClientX"] = Num(Gd.Get(cp, "ClientX"));
            data["ClientY"] = Num(Gd.Get(cp, "ClientY"));
            data["Why"] = StrOf(Gd.Get(cp, "Why"), "");
            data["Detail"] = "要点一下：" + data["Why"];
            return Phase(data, "needs-click",
                "启动器检查：这一页要点一下(模型说：" + data["What"] + "，点位 " + data["ClientX"] + "," + data["ClientY"] + " " + data["Why"] + ")", "STEP");
        }
    }
}
