// tools/panel-cs/Cli.SelfTest.cs —— --coretest / --echotest：批次 0 验收门 §6.2/§6.3。
// 纪律(迁移前就立好的老规矩)：
//   * 断言必须计入退出码 —— "结论 PASS 却印着 FAIL"那种事不许再发生；
//   * 全部喂 %TEMP% 下的临时树，不读真配置、不起真助手进程 —— 只读自检不许要求机器现场状态；
//   * JSS 的数字怪癖(int/double 混发)在这里**钉死**而不是祈祷：批次 1 的判定值要过这道形状关。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class GdCliSelfTest
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

        // ---------- 临时树 ----------
        static string MakeTempTree()
        {
            string tmp = Path.Combine(Path.GetTempPath(),
                "gd-" + Process.GetCurrentProcess().Id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-"
                + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Path.Combine(tmp, "lib"));
            Directory.CreateDirectory(Path.Combine(tmp, "config"));
            Directory.CreateDirectory(tmp);   // logs/state/backups 由 Init 补
            return tmp;
        }

        static void CleanTemp(string tmp)
        {
            try { Directory.Delete(tmp, true); } catch { }
        }

        static object SampleData()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["消息"] = "他说\"停\"，路径 C:\\x";
            d["n"] = 5;
            d["f"] = 1.2;
            d["t"] = true;
            d["nul"] = null;
            d["arr"] = new object[] { "CI", "CR" };
            Dictionary<string, object> nest = new Dictionary<string, object>();
            nest["s"] = new object[] { new Dictionary<string, object> { { "code", "CI" }, { "ap", 30 } } };
            d["nest"] = nest;
            return d;
        }

        static bool SameJson(object a, object b)
        {
            return GdJson.Serialize(a) == GdJson.Serialize(b);
        }

        static object EnvGet(Dictionary<string, object> env, string key)
        {
            object v;
            return env.TryGetValue(key, out v) ? v : null;
        }

        // verb 名册对账用的源码读法：Cli.cs 与 panel-cs.exe 同目录(树内跑、首跑释放的 app 目录也带着源码)。
        // 读不到就当场抛 —— 按"空集与读不出必须分档"的纪律，这一格不许在源码缺席时冒充绿。
        static string CliSourceText()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string p = Path.Combine(dir, "Cli.cs");
            if (!File.Exists(p)) throw new FileNotFoundException("verb 名册对账要求 Cli.cs 源码在位(与 panel-cs.exe 同目录)：" + p);
            return File.ReadAllText(p, Encoding.UTF8);
        }

        // 合成一枚 IO_REPARSE_TAG_APPEXECLINK 的 reparse blob —— 形状对着 Panel.Data.cs:424 那段本机现量拼：
        // [0..3] tag 0x8000001B｜[4..5] 数据长度(不含这 8 字节的头)｜[6..7] 保留(现量 0)｜[8..11] 布局版本(现量 3)
        // ｜[12..] 以 NUL 分隔的 UTF-16LE 字段：包家族名、AUMID、真身路径、标志。
        // 自检喂的是这一份，不喂现场那枚别名：现场解析结果归等值门现拍(只读自检不许要求机器现场状态)。
        static byte[] AppExecLinkBlob(string pkgFamilyName, string aumid, string target, string flags)
        {
            string body = pkgFamilyName + "\0" + aumid + "\0" + target + "\0" + flags + "\0";
            byte[] fields = Encoding.Unicode.GetBytes(body);
            byte[] blob = new byte[12 + fields.Length];
            blob[0] = 0x1B; blob[1] = 0x00; blob[2] = 0x00; blob[3] = 0x80;   // 小端的 0x8000001B(本机现量头四字节 1B 00 00 80)
            blob[4] = (byte)(fields.Length & 0xFF); blob[5] = (byte)((fields.Length >> 8) & 0xFF);
            blob[8] = 3;                                                       // 布局版本不是这个数，解析器就该回空串
            Array.Copy(fields, 0, blob, 12, fields.Length);
            return blob;
        }

        // ============================ --coretest ============================
        public static int CoreTest()
        {
            GdCli.TryAttachConsole();
            Box b = new Box();
            // === C-0′「量尺」：verb 名册(数据面)与分派链两处逐枚对账 ===
            // 为什么在 coretest 而不是新开一档：名册与分派链本来就是同一枚 verb 的两处，加 verb 时两处必须同批；
            // 本批之前这两处**没有任何计数断言**，新增 verb 只在报错文案里静默漏名(B-3②-b1/b2 两批名册 74→84 全靠人工现扫)。
            b.Chk("名册钉数：Names 枚数 == ExpectedCount 且无重复", delegate
            {
                return GdCliVerbs.Names.Length == GdCliVerbs.ExpectedCount
                    && GdCliVerbs.NameSet().Count == GdCliVerbs.Names.Length;
            });
            b.Chk("名册正向：Names 里每一枚都能在 Cli.cs 分派链逐字命中", delegate
            {
                string src = CliSourceText();
                string miss = "";
                for (int i = 0; i < GdCliVerbs.Names.Length; i++)
                {
                    if (src.IndexOf("verb == \"" + GdCliVerbs.Names[i] + "\"", StringComparison.Ordinal) < 0)
                    {
                        miss = miss.Length == 0 ? GdCliVerbs.Names[i] : miss + "," + GdCliVerbs.Names[i];
                    }
                }
                if (miss.Length > 0) Console.WriteLine("  ROSTER-MISS-IN-DISPATCH=" + miss);
                return miss.Length == 0;
            });
            b.Chk("名册反向：Cli.cs 分派链里每一枚都在 Names 里(防「能分发、名册没人钉」)", delegate
            {
                string src = CliSourceText();
                HashSet<string> known = GdCliVerbs.NameSet();
                MatchCollection mc = Regex.Matches(src, "verb == \"([a-z0-9-]+)\"");
                string miss = "";
                int uniq = 0;
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < mc.Count; i++)
                {
                    string v = mc[i].Groups[1].Value;
                    if (!seen.Add(v)) continue;
                    uniq++;
                    if (!known.Contains(v)) miss = miss.Length == 0 ? v : miss + "," + v;
                }
                Console.WriteLine("  DISPATCH-VERB-COUNT=" + uniq.ToString() + " ROSTER-COUNT=" + GdCliVerbs.Names.Length.ToString());
                if (miss.Length > 0) Console.WriteLine("  ROSTER-MISS-IN-ROSTER=" + miss);
                return miss.Length == 0 && uniq == GdCliVerbs.Names.Length;
            });
            string tmp = MakeTempTree();
            try
            {
                // 只装方舟那一路的库：RequiredPathKeys 的口径就是"文件在才算装了那一路"
                File.WriteAllText(Path.Combine(tmp, "lib", "maa.ps1"), "#");
                File.WriteAllText(Path.Combine(tmp, "VERSION"), "9.9.9\n");
                Dictionary<string, object> paths = new Dictionary<string, object>();
                paths["maaDir"] = "";
                paths["m9aDir"] = Path.Combine(tmp, "no-such-dir");
                paths["mumuDir"] = Path.Combine(tmp, "lib");
                Dictionary<string, object> settings = new Dictionary<string, object>();
                settings["paths"] = paths;
                settings["runtime"] = new Dictionary<string, object> { { "arkVisionRescue", true } };
                GdJson.Write(Path.Combine(tmp, @"config\settings.json"), settings);

                GdEnv.InitForTest(tmp);

                b.Chk("VERSION 读数 = 临时树里那份", delegate { return GdEnv.Version == "9.9.9"; });
                b.Chk("VERSION 形状不对回 0.0.0 不抛", delegate
                {
                    File.WriteAllText(Path.Combine(tmp, "VERSION"), "v2026.09\n");
                    GdEnv.InitForTest(tmp);
                    return GdEnv.Version == "0.0.0";
                });
                File.WriteAllText(Path.Combine(tmp, "VERSION"), "9.9.9\n");
                GdEnv.InitForTest(tmp);

                b.Chk("JoinPath：空 base 返空串不抛(全工程靠这条显示『未填写』)", delegate
                {
                    return GdEnv.JoinPath("", "resource", "stages.json") == ""
                        && GdEnv.JoinPath(null, "a") == "";
                });
                b.Chk("JoinPath：多级拼接 + 跳过空段", delegate
                {
                    string want = Path.Combine(Path.Combine(tmp, "resource"), "stages.json");
                    return GdEnv.JoinPath(tmp, "", "resource", "stages.json") == want;
                });
                b.Chk("PathReady：空/不存在=false，文件与目录=true", delegate
                {
                    return !GdEnv.PathReady("")
                        && !GdEnv.PathReady(Path.Combine(tmp, "ghost"))
                        && GdEnv.PathReady(tmp)
                        && GdEnv.PathReady(Path.Combine(tmp, "lib", "maa.ps1"));
                });

                string roundPath = Path.Combine(tmp, "round.json");
                GdJson.Write(roundPath, SampleData());
                object back = GdJson.Read(roundPath);
                b.Chk("JSON 往返：中文/引号/反斜杠/嵌套/数组/布尔/null 全等", delegate { return SameJson(back, SampleData()); });
                b.Chk("写出无 BOM(对齐 Write-GdJson 的 UTF8Encoding($false))", delegate
                {
                    byte[] head = File.ReadAllBytes(roundPath);
                    return !(head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF);
                });
                b.Chk("JSS 形状钉死(实测)：5→Int32 / 1.2→**Decimal** / 数组→Object[] —— 批次 1 取数一律 Convert.ToXxx，禁裸强转", delegate
                {
                    Dictionary<string, object> d = (Dictionary<string, object>)back;
                    bool nOk = d["n"] is int && (int)d["n"] == 5;
                    bool fOk = d["f"] is decimal && Convert.ToDouble(d["f"]) == 1.2;   // Convert 通道必须走得通，这是批次 1 的取数规矩
                    bool aOk = d["arr"] is object[] && ((object[])d["arr"]).Length == 2;
                    if (!(nOk && fOk && aOk))
                        throw new Exception("n=" + (d["n"] == null ? "null" : d["n"].GetType().Name)
                            + " f=" + (d["f"] == null ? "null" : d["f"].GetType().Name)
                            + " arr=" + (d["arr"] == null ? "null" : d["arr"].GetType().Name));
                    return true;
                });
                b.Chk("缺文件的读法说 PS 原话：找不到文件：<路径>", delegate
                {
                    try { GdJson.Read(Path.Combine(tmp, "ghost.json")); return false; }
                    catch (FileNotFoundException ex) { return ex.Message.StartsWith("找不到文件："); }
                });
                b.Chk("信封形状：proto/verb/ok/exit/data/error 齐、proto=1", delegate
                {
                    Dictionary<string, object> e = GdJson.Envelope("echo", true, 0, SampleData(), null);
                    return (int)e["proto"] == GdJson.Proto && (string)e["verb"] == "echo"
                        && (bool)e["ok"] && (int)e["exit"] == 0 && e["data"] != null && e["error"] == null;
                });

                b.Chk("RequiredPathKeys：只点这棵树上真装了库的那一路", delegate
                {
                    List<string> keys = GdSettings.RequiredPathKeys();
                    return keys.Count == 1 && keys[0] == "maaDir";
                });
                b.Chk("Get-GdSettings 的 Quiet 双人格：列缺不抛，missing 只收空路径项", delegate
                {
                    List<string> missing;
                    GdSettings.Load(out missing, true, null);
                    return missing.Count == 1 && missing[0] == "maaDir";   // m9aDir 是"填了指错"，另一类，只 WARN
                });
                b.Chk("缺配置文件的报错是 setup 文案，不是堆栈", delegate
                {
                    GdEnv.InitForTest(Path.Combine(tmp, "no-root-here"));
                    List<string> missing;
                    try { GdSettings.Load(out missing, true, new List<string>()); return false; }
                    catch (FileNotFoundException ex) { return ex.Message.Contains("setup.ps1"); }
                    finally { GdEnv.InitForTest(tmp); }
                });

                b.Chk("日志两层时间戳：落盘行 = [yyyy-MM-dd HH:mm:ss][HH:mm:ss][LEVEL]", delegate
                {
                    string logPath = Path.Combine(tmp, "fmt.log");
                    GdLog.LogFile = logPath;
                    GdLog.ToConsole = false;
                    try { GdLog.Line("你好", "WARN"); }
                    finally { GdLog.LogFile = null; GdLog.ToConsole = true; }
                    string line = File.ReadAllLines(logPath)[0];
                    return Regex.IsMatch(line, @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]\[\d{2}:\d{2}:\d{2}\]\[WARN\] 你好$");
                });

                b.Chk("BackupFile：备份成功、内容一致、命名 <名>.<stamp>.bak", delegate
                {
                    string victim = Path.Combine(tmp, "victim.txt");
                    File.WriteAllText(victim, "A");
                    string bak = GdEnv.BackupFile(victim);
                    return bak != null && File.Exists(bak) && File.ReadAllText(bak) == "A"
                        && Path.GetFileName(bak).StartsWith("victim.txt.") && bak.EndsWith(".bak");
                });

                // ---- 乙档刀②(2026-10-23)那枚新判定：应用执行别名 → 真身路径 ----
                // 六形全部喂合成帧／临时普通件，不要求这台机器装有商店版 PowerShell ——
                // 现场那一枚别名的真实解析结果不进自检(它是跟着机器/包版本走的现场读数，留证在 Panel.Data.cs
                // 那段现量记录里；"现场实测留证 ≠ 门已绿"是同一条规矩)。
                b.Chk("别名 blob 正形：第三枚非空字段＝真身路径(位次不是名字)", delegate
                {
                    string target = @"C:\Program Files\WindowsApps\Microsoft.PowerShell_7.6.7.0_x64__8wekyb3d8bbwe\pwsh.exe";
                    byte[] blob = AppExecLinkBlob("Microsoft.PowerShell_7.6.7.0_8wekyb3d8bbwe", "Microsoft.PowerShell_7.6.7.0!pwsh", target, "1");
                    return PanelForm.ParseAppExecLinkBlob(blob) == target;
                });
                b.Chk("错 reparse tag(不是 APPEXECLINK)：回空串，不猜字段位次", delegate
                {
                    byte[] blob = AppExecLinkBlob("pkg", "aumid", @"C:\x\pwsh.exe", "1");
                    blob[0] = 0x03; blob[1] = 0x00; blob[2] = 0x00; blob[3] = 0xA0;   // IO_REPARSE_TAG_MOUNT_POINT
                    return PanelForm.ParseAppExecLinkBlob(blob).Length == 0;
                });
                b.Chk("布局版本不是 3：回空串(本机现量只有 3，别的形状不认)", delegate
                {
                    byte[] blob = AppExecLinkBlob("pkg", "aumid", @"C:\x\pwsh.exe", "1");
                    blob[8] = 2;
                    return PanelForm.ParseAppExecLinkBlob(blob).Length == 0;
                });
                b.Chk("截断／只有两枚非空字段／整坨为空：三形都回空串且不抛", delegate
                {
                    byte[] full = AppExecLinkBlob("pkg", "aumid", @"C:\x\pwsh.exe", "1");
                    byte[] shortBlob = new byte[10];
                    Array.Copy(full, 0, shortBlob, 0, 10);
                    byte[] twoFields = AppExecLinkBlob("pkg", "aumid", "", "");
                    return PanelForm.ParseAppExecLinkBlob(null).Length == 0
                        && PanelForm.ParseAppExecLinkBlob(shortBlob).Length == 0
                        && PanelForm.ParseAppExecLinkBlob(twoFields).Length == 0
                        && PanelForm.ParseAppExecLinkBlob(new byte[0]).Length == 0;
                });
                b.Chk("真身是相对路径／不在场／是目录：ResolveEngineTarget 三形一律回空串", delegate
                {
                    byte[] rel = AppExecLinkBlob("pkg", "aumid", @"pwsh.exe", "1");
                    if (PanelForm.ParseAppExecLinkBlob(rel) != @"pwsh.exe") return false;   // 纯函数只管位次，形状校在上一层
                    if (PanelForm.ResolveEngineTarget("").Length != 0) return false;
                    if (PanelForm.ResolveEngineTarget(Path.Combine(tmp, "no-such-alias.exe")).Length != 0) return false;
                    // 临时树里那枚普通文件不是 ReparsePoint ⇒ 第一道形状校就该回空串(不进去读 blob)
                    if (PanelForm.ResolveEngineTarget(Path.Combine(tmp, "victim.txt")).Length != 0) return false;
                    return true;
                });
                b.Chk("别名文件名字面猜不到真身：解析按位次、不按同名(本机 ArmouryCrate→SystemUWPLauncher 现量)", delegate
                {
                    byte[] blob = AppExecLinkBlob("pkg", "aumid", @"C:\Windows\System32\SystemUWPLauncher.exe", "1");
                    return PanelForm.ParseAppExecLinkBlob(blob) == @"C:\Windows\System32\SystemUWPLauncher.exe";
                });
                b.Chk("三枚 kernel32 声明的入口点在位(本轮实测：CharSet.Unicode 会补 W 去找不存在的名字，EntryPointNotFound 被 catch 咽成「读不出」)", delegate
                {
                    return PanelForm.ReparseEntryPointsOk() == "ok";
                });
            }
            catch (Exception ex)
            {
                b.Chk("coretest 整体没炸", delegate { throw ex; });
            }
            finally { CleanTemp(tmp); }
            return b.Result("coretest");
        }

        // ============================ --echotest ============================
        static int RunExe(string args)
        {
            ProcessStartInfo pi = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, args);
            pi.UseShellExecute = false;
            pi.CreateNoWindow = true;
            using (Process p = Process.Start(pi))
            {
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } return -99; }
                return p.ExitCode;
            }
        }

        static string Q(string s) { return "\"" + s + "\""; }

        public static int EchoTest()
        {
            GdCli.TryAttachConsole();
            Box b = new Box();
            string tmp = MakeTempTree();
            try
            {
                GdEnv.InitForTest(tmp);
                string inPath = Path.Combine(tmp, "in.json");
                string outPath = Path.Combine(tmp, "out.json");
                GdJson.Write(inPath, SampleData());
                object sampleRead = GdJson.Read(inPath);   // 两侧都过一遍 JSS，int/double 归一到同一形状再比

                b.Chk("回环：--in 的 data 原样进信封，退出码 0", delegate
                {
                    int code = GdCli.Run("echo", new string[] { "--in=" + inPath, "--out=" + outPath });
                    if (code != 0) return false;
                    Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                    return (int)EnvGet(env, "proto") == 1 && (int)EnvGet(env, "exit") == 0
                        && (bool)EnvGet(env, "ok") && EnvGet(env, "error") == null
                        && (string)EnvGet(env, "verb") == "echo"
                        && SameJson((Dictionary<string, object>)EnvGet(env, "data"), sampleRead);
                });
                b.Chk("--exit=2：进程码和信封 exit 一致、ok=false、error 说清是模拟", delegate
                {
                    int code = GdCli.Run("echo", new string[] { "--out=" + outPath, "--exit=2" });
                    Dictionary<string, object> env = GdJson.ReadEnvelope(outPath);
                    return code == 2 && (int)EnvGet(env, "exit") == 2 && !(bool)EnvGet(env, "ok")
                        && ((string)EnvGet(env, "error")).Contains("模拟");
                });
                b.Chk("--no-out：违约=结果文件不在(转发壳要靠这一条报错，绝不面对'非 0 但不知道为什么')", delegate
                {
                    string ghost = Path.Combine(tmp, "ghost-out.json");
                    int code = GdCli.Run("echo", new string[] { "--out=" + ghost, "--no-out" });
                    return code == 0 && !File.Exists(ghost);
                });
                b.Chk("--garbage：违约=文件在但信封读不回来", delegate
                {
                    string g = Path.Combine(tmp, "g.json");
                    int code = GdCli.Run("echo", new string[] { "--out=" + g, "--garbage" });
                    if (code != 0 || !File.Exists(g)) return false;
                    try { GdJson.ReadEnvelope(g); return false; } catch { return true; }
                });
                b.Chk("--in 指空：退出码 2(前置缺) 且**仍写出**带 error 的信封", delegate
                {
                    // 1→2 是有意的契约归位(Cli.cs 的 FileNotFoundException 分支：文件没在场=前置缺，不是内部炸)；
                    // 断言同时收紧 error 必须是原话(不带 "FileNotFoundException: " 前缀)。
                    string e2 = Path.Combine(tmp, "err.json");
                    int code = GdCli.Run("echo", new string[] { "--in=" + Path.Combine(tmp, "none.json"), "--out=" + e2 });
                    Dictionary<string, object> env = GdJson.ReadEnvelope(e2);
                    return code == 2 && (int)EnvGet(env, "exit") == 2 && !(bool)EnvGet(env, "ok")
                        && ((string)EnvGet(env, "error")).Contains("找不到文件：");
                });
                b.Chk("未知子命令 = 退出码 1(127 留给 cmd 层)", delegate
                {
                    return GdCli.Run("no-such-verb", new string[0]) == 1;
                });
                b.Chk("未知参数被点名拒绝而不是咽下", delegate
                {
                    string u = Path.Combine(tmp, "unknown-arg.json");
                    int code = GdCli.Run("echo", new string[] { "--oops=1", "--out=" + u });
                    Dictionary<string, object> env = GdJson.ReadEnvelope(u);
                    return code == 1 && ((string)EnvGet(env, "error")).Contains("未知参数");
                });
                b.Chk("--transcript 显式开才落 logs\\cli-echo-*.log", delegate
                {
                    string t = Path.Combine(tmp, "t.json");
                    try
                    {
                        GdCli.Run("echo", new string[] { "--out=" + t, "--transcript" });
                        bool found = false;
                        foreach (string f in Directory.GetFiles(Path.Combine(tmp, "logs"), "cli-echo-*.log"))
                        {
                            found = true;
                            string first = File.ReadAllLines(f)[0];
                            if (!Regex.IsMatch(first, @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]\[\d{2}:\d{2}:\d{2}\]\[INFO\] 日志文件：")) return false;
                        }
                        return found;
                    }
                    finally { GdLog.LogFile = null; }   // 不落盘回去，后面的用例别都续写进这份临时日志
                });

                // 真子进程各走一遍 Main：证明 verb 分发不弹窗、退出码过进程边界仍成立。
                b.Chk("子进程：panel-cs.exe echo --out=… 退出码 0 + 信封可读", delegate
                {
                    string p = Path.Combine(tmp, "sub.json");
                    File.Delete(p);
                    int code = RunExe("echo --out=" + Q(p));
                    if (code != 0) return false;
                    Dictionary<string, object> env = GdJson.ReadEnvelope(p);
                    return (int)EnvGet(env, "proto") == 1 && (int)EnvGet(env, "exit") == 0;
                });
                b.Chk("子进程：未知 verb 退出码 1", delegate { return RunExe("no-such-verb") == 1; });
            }
            catch (Exception ex)
            {
                b.Chk("echotest 整体没炸", delegate { throw ex; });
            }
            finally { CleanTemp(tmp); }
            return b.Result("echotest");
        }
    }
}
