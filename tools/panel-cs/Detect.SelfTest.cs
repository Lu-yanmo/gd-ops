// tools/panel-cs/Detect.SelfTest.cs —— --statetest：批次 2(Detect.cs)的无头自检。
// 期望串逐字抄自旧树 lib\detect.ps1 / lib\ark-status.ps1 / lib\r1999-status.ps1 的旧实现(三本均已整件出仓：后两本 11-04 刀④，detect 那本 11-06 刀⑮(a)) ——
// 这里红了就是移植走样，改 Detect.cs，不改这里的期望。
//
// 一个重要坑(写假树前必须知道)：探测的跳过段清单里有 temp/appdata —— 假树要是建在 %TEMP% 下，
// BFS 会把整棵子树当系统目录跳没，测了个寂寞。所以本文件把假树建在用户目录下、用完即删。
// 不碰真注册表结论、不起真进程(批次纪律：只读自检不许要求机器现场状态)：
// 进程名一律用不可能存在的串；卸载表那段走 Detect.HintSource 注入合成 Hint(与 roots 注入同思路，生产恒为 null)；
// roots 用注入的假树。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GdPanel
{
    static class GdDetectSelfTest
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
        static DateTime T(string wall) { return DateTime.ParseExact(wall, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }

        static void Touch(string path, DateTime mtime)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "");
            File.SetLastWriteTime(path, mtime);
        }

        static Dictionary<string, object> Run(object inp)
        {
            return Detect.Installs(Gd.Map(inp)) as Dictionary<string, object>;
        }

        static Dictionary<string, object> InstallsBody(object[] processNames, object[] exeNames, object[] nameHints,
            object[] uninstallLike, string pickProp, string relExe, string trimSegment, bool skipScan, params string[] roots)
        {
            return Run(Gd.Out(
                "processNames", processNames, "exeNames", exeNames, "nameHints", nameHints,
                "uninstallLike", uninstallLike, "pickProp", pickProp, "relExe", relExe,
                "trimSegment", trimSegment, "skipScan", skipScan, "roots", roots));
        }

        static string[] S(params string[] v) { return v; }

        static bool Found(Dictionary<string, object> d) { return d != null && Gd.Truthy(Gd.Get(d, "Found")); }
        static string Val(Dictionary<string, object> d) { return Gd.Str(Gd.Get(d, "Value")); }
        static string Src(Dictionary<string, object> d) { return Gd.Str(Gd.Get(d, "Source")); }

        // ============================ --statetest ============================
        public static int StatTest()
        {
            Box b = new Box();
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                          "gd-detect-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(baseDir);

                // ---------- 路径过滤三件 ----------
                b.Chk("Like：* 通配、大小写不敏感、整串锚定(-like 语义)", delegate
                {
                    return Detect.Like("MyMaaDir", "*MAA*") && Detect.Like("MaaAssistant", "MaaAssistant*")
                        && Detect.Like("明日方舟", "*明日*") && Detect.Like("games", "Game?")
                        && !Detect.Like("MaaAssistant", "*Maa") && !Detect.Like("xMyMaa", "MyMaa");
                });
                b.Chk("JunkPath：downloads/备份 段命中；结尾无分隔符不算；大小写不敏感", delegate
                {
                    return Detect.JunkPath("D:\\Downloads\\M9A\\m9a.exe") && Detect.JunkPath("E:\\备份\\M9A")
                        && Detect.JunkPath("d:\\DOWNLOADS\\x")
                        && !Detect.JunkPath("D:\\Downloads")          // 段两侧都要有分隔符(照抄旧正则)
                        && !Detect.JunkPath("D:\\Tools\\MAA");
                });
                b.Chk("JunkPath：浏览器重命名副本「(N)\\」命中；前面是空格的 (2) 不算(旧实现就如此)", delegate
                {
                    return Detect.JunkPath("C:\\a\\(2)\\m9a.exe") && !Detect.JunkPath("C:\\a\\m9a (2)\\m9a.exe");
                });
                b.Chk("SkipPath：windows/temp/.git 段跳过，Tools 不跳", delegate
                {
                    return Detect.SkipPath("C:\\Windows\\Temp\\MAA") && Detect.SkipPath("C:\\appdata\\x")
                        && Detect.SkipPath("D:\\repo\\.git\\MAA")
                        && !Detect.SkipPath("D:\\Tools\\MAA");
                });
                b.Chk("FixedRoots：形状=去尾杠再加回一个反斜杠(不要求现场状态)", delegate
                {
                    string[] rs = Detect.FixedRoots();
                    if (rs.Length == 0) return false;
                    foreach (string r in rs) if (!r.EndsWith("\\")) return false;
                    return true;
                });

                // ---------- detect-installs：主力假树 ----------
                string root = Path.Combine(baseDir, "R1");
                string maaNew = Path.Combine(root, "Tools\\MAA");
                string maaOld = Path.Combine(root, "Tools\\OldMAA");
                string maaDl = Path.Combine(root, "Downloads\\MAA");
                Touch(Path.Combine(maaNew, "MAA.exe"), T("2026-09-20 10:00:00"));
                Touch(Path.Combine(maaOld, "MAA.exe"), T("2026-09-10 10:00:00"));
                Touch(Path.Combine(maaDl, "MAA.exe"), T("2026-09-21 10:00:00"));   // 最新但副本 → 排最后
                Touch(Path.Combine(root, "Windows\\MAA\\MAA.exe"), T("2026-09-25 10:00:00"));      // windows 段 → 看不见
                Touch(Path.Combine(root, "Tools\\node_modules\\MAA\\MAA.exe"), T("2026-09-25 10:00:00")); // 同理
                // Windows\node_modules 下的命中时间最新，若过滤链走样它们会抢前排 —— 这套期望就是防这个的
                var d = InstallsBody(S("gd_no_such_proc_zzz"), S("MAA.exe"), S("*MAA*"), S(), "Dir", "", "", false, root);
                b.Chk("detect-installs：非副本优先→最近改动次之；系统/依赖段被跳", delegate
                {
                    return Found(d) && Val(d) == maaNew;
                });
                b.Chk("detect-installs：Source 文案逐字(候选数 + 「取值」+ 前几个)", delegate
                {
                    return Src(d) == "探测：候选 3 个，取「" + maaNew + "」(前几个：" + maaNew + " ; " + maaOld + " ; " + maaDl + ")";
                });
                b.Chk("detect-installs：pickProp=Exe → 取可执行全路径", delegate
                {
                    var e = InstallsBody(S("gd_no_such_proc_zzz"), S("MAA.exe"), S("*MAA*"), S(), "Exe", "", "", false, root);
                    return Val(e) == Path.Combine(maaNew, "MAA.exe");
                });

                b.Chk("detect-installs：只剩副本 → Source 带确认后缀", delegate
                {
                    TouchIn(baseDir, "R2\\Downloads\\MyM9A\\m9a.exe");
                    var j = InstallsBody(S("gd_no_such_proc_zzz"), S("m9a.exe"), S("*MyM9A*"), S(), "Dir", "", "", false,
                        Path.Combine(baseDir, "R2"));
                    string dir = Path.Combine(baseDir, "R2\\Downloads\\MyM9A");
                    return Found(j) && Val(j) == dir
                        && Src(j) == "探测：候选 1 个，取「" + dir + "」(前几个：" + dir + ") ← 这个位置像下载/备份副本，确认是不是你要用的那份";
                });

                b.Chk("detect-installs：空命中 → Found:false 且不带 Value", delegate
                {
                    var n = InstallsBody(S("gd_no_such_proc_zzz"), S("nope.exe"), S("*NoSuchThing*"), S(), "Dir", "", "", false, root);
                    return !Found(n) && n.Count == 1 && n.ContainsKey("Found");
                });
                b.Chk("detect-installs：SkipScan → 只剩进程/卸载表两路(都不存在→空)", delegate
                {
                    var n = InstallsBody(S("gd_no_such_proc_zzz"), S("MAA.exe"), S("*MAA*"), S("*NoSuchApp_zzz*"), "Dir", "", "", true, root);
                    return !Found(n);
                });

                b.Chk("detect-installs：relExe+trimSegment —— nx_main 退回安装根、Exe 拼相对路径", delegate
                {
                    // 双层是故意的：扫描按 命中目录+relExe 找 exe，所以命中目录本身就得叫 nx_main 且里面还有 nx_main\exe
                    string r4 = Path.Combine(baseDir, "R4");
                    Touch(Path.Combine(r4, "nx_main\\nx_main\\MuMuManager.exe"), T("2026-09-20 10:00:00"));
                    var m = InstallsBody(S("gd_no_such_proc_zzz"), S("MuMuManager.exe"), S("nx*"), S(), "Dir",
                        Path.Combine("nx_main", "MuMuManager.exe"), "nx_main", false, r4);
                    return Found(m) && Val(m) == r4 && Src(m) == "探测：候选 1 个，取「" + r4 + "」(前几个：" + r4 + ")";
                });
                b.Chk("detect-installs：trim 时 relExe 缺省回退 exeNames[0]；同根多候选去重后计数=1", delegate
                {
                    string r5 = Path.Combine(baseDir, "R5");
                    Touch(Path.Combine(r5, "App\\A.exe"), T("2026-09-20 10:00:00"));
                    Touch(Path.Combine(r5, "App\\B.exe"), T("2026-09-20 10:00:00"));
                    var m = InstallsBody(S("gd_no_such_proc_zzz"), S("A.exe", "B.exe"), S("App"), S(), "Dir", "", "App", false, r5);
                    return Found(m) && Val(m) == r5 && Src(m) == "探测：候选 1 个，取「" + r5 + "」(前几个：" + r5 + ")";
                });

                // ---------- 卸载表源：合成 Hint 注入(不读真注册表；门的两枚"真注册表"场景搬成确定性两档) ----------
                // MuMu 那条的实战链路：InstallLocation 指安装根 → 根下 nx_main\<relExe> 命中 ⇒ 候选就是安装根。
                string u1 = Path.Combine(baseDir, "U1");
                string mumuRoot = Path.Combine(u1, "MuMuPlayer-12.0");
                Touch(Path.Combine(mumuRoot, "nx_main\\MuMuManager.exe"), T("2026-09-20 10:00:00"));
                object[] u1Seen = null;
                Dictionary<string, object> u1Body;
                Detect.HintSource = delegate (object[] pats)
                {
                    u1Seen = pats;
                    return new List<Detect.Hint> {
                        new Detect.Hint { Name = "MuMu Player 12", Prop = "InstallLocation", Value = mumuRoot } };
                };
                try
                {
                    u1Body = InstallsBody(S("gd_no_such_proc_zzz"), S("MuMuManager.exe"), S("*NoSuchHint_zz*"),
                        S("*MuMu*"), "Dir", Path.Combine("nx_main", "MuMuManager.exe"), "nx_main", true);
                }
                finally { Detect.HintSource = null; }
                b.Chk("detect-installs：卸载表 InstallLocation 命中 → nx_main 里的 exe 算候选、Value=安装根", delegate
                {
                    return u1Seen != null && u1Seen.Length == 1 && Gd.Str(u1Seen[0]) == "*MuMu*"
                        && Found(u1Body) && Val(u1Body) == mumuRoot
                        && Src(u1Body) == "探测：候选 1 个，取「" + mumuRoot + "」(前几个：" + mumuRoot + ")";
                });

                // Ark 那条：DisplayIcon 的 ",0" 经 ^[^,;]*, 会被剥成尾段(旧实现就如此，不产候选)；
                // InstallSource 去引号后命中、PickProp=Exe 取可执行全路径 ⇒ 恰好 1 个候选。
                string u2 = Path.Combine(baseDir, "U2");
                string arkDir = Path.Combine(u2, "ArkNights");
                string arkExe = Path.Combine(arkDir, "Arknights.exe");
                Touch(arkExe, T("2026-09-20 10:00:00"));
                Dictionary<string, object> u2Body;
                Detect.HintSource = delegate (object[] pats)
                {
                    return new List<Detect.Hint> {
                        new Detect.Hint { Name = "明日方舟", Prop = "DisplayIcon", Value = arkExe + ",0" },
                        new Detect.Hint { Name = "明日方舟", Prop = "InstallSource", Value = "\"" + arkDir + "\"" } };
                };
                try
                {
                    u2Body = InstallsBody(S("gd_no_such_proc_zzz"), S("Arknights.exe"), S("*NoSuchHint_zz*"),
                        S("*Arknights*", "*明日方舟*"), "Exe", "", "", true);
                }
                finally { Detect.HintSource = null; }
                b.Chk("detect-installs：卸载表 DisplayIcon 的 ',0' 剥成尾段不产候选；InstallSource 去引号后命中、PickProp=Exe", delegate
                {
                    return Found(u2Body) && Val(u2Body) == arkExe
                        && Src(u2Body) == "探测：候选 1 个，取「" + arkExe + "」(前几个：" + arkExe + ")";
                });

                // ---------- maa-queue-lines ----------
                string cfgPath = Path.Combine(baseDir, "configuration.json");
                GdJson.Write(cfgPath, Gd.Out("Configurations", Gd.Out("Default", Gd.Out(
                    "TaskQueue", new object[] {
                        Gd.Out("TaskType", "Fight", "Name", "00:00 刷战斗", "IsEnable", true,
                               "StagePlan", new object[] { "CE-5", "", null, "LS-5" }),
                        null,
                        Gd.Out("TaskType", "Recruit", "Name", "获得干员", "IsEnable", false) },
                    "Gui", Gd.Out(
                        "StartUpSettings", Gd.Out("RunDirectly", true),
                        "RuntimeSettings", Gd.Out("StartGame", false),
                        "ConnectSettings", Gd.Out("Config", "adb"))))));
                b.Chk("maa-queue-lines：行式/plan 过滤空值/null 项跳过/尾三行逐字", delegate
                {
                    var r = Gd.Map(Detect.MaaQueueLines(Gd.Out("cfgPath", cfgPath)));
                    string[] ls = Strs(Gd.Get(r, "Lines"));
                    return ls.Length == 5
                        && ls[0] == string.Format("  {0,-10} {1,-12} IsEnable={2}{3}", "Fight", "00:00 刷战斗", "True", " plan=CE-5,LS-5")
                        && ls[1] == string.Format("  {0,-10} {1,-12} IsEnable={2}{3}", "Recruit", "获得干员", "False", "")
                        && ls[2] == "  RunDirectly(启动即自动开始)= True"
                        && ls[3] == "  StartGame(MAA 自己开游戏)  = False"
                        && ls[4] == "  连接方式 ConnectSettings.Config = adb";
                });
                b.Chk("maa-queue-lines：Gui/TaskQueue 整段缺失 → 三尾行空值照样输出(照抄 PS 的 $null 展开)", delegate
                {
                    string p = Path.Combine(baseDir, "cfg-empty.json");
                    GdJson.Write(p, Gd.Out("Configurations", Gd.Out("Default", Gd.Out())));
                    var r = Gd.Map(Detect.MaaQueueLines(Gd.Out("cfgPath", p)));
                    string[] ls = Strs(Gd.Get(r, "Lines"));
                    return ls.Length == 3 && ls[0] == "  RunDirectly(启动即自动开始)= "
                        && ls[1] == "  StartGame(MAA 自己开游戏)  = " && ls[2] == "  连接方式 ConnectSettings.Config = ";
                });
                // 门 queue/A 的第四项(只有 TaskType 的行)与 queue/D(单对象队列)、queue/E(空数组)/B(顶层 {})
                b.Chk("maa-queue-lines：只有 TaskType 的项也出一行，空 Name/IsEnable 照 PS 展开", delegate
                {
                    string p = Path.Combine(baseDir, "cfg-taskonly.json");
                    GdJson.Write(p, Gd.Out("Configurations", Gd.Out("Default", Gd.Out(
                        "TaskQueue", new object[] { Gd.Out("TaskType", "Invalidate") }))));
                    var r = Gd.Map(Detect.MaaQueueLines(Gd.Out("cfgPath", p)));
                    string[] ls = Strs(Gd.Get(r, "Lines"));
                    return ls.Length == 4 && ls[0] == "  Invalidate              IsEnable="
                        && ls[1] == "  RunDirectly(启动即自动开始)= "
                        && ls[3] == "  连接方式 ConnectSettings.Config = ";
                });
                b.Chk("maa-queue-lines：TaskQueue 是单对象(不是数组)也当一项；StagePlan 缺省仍带 plan= 前缀", delegate
                {
                    string p = Path.Combine(baseDir, "cfg-single.json");
                    GdJson.Write(p, Gd.Out("Configurations", Gd.Out("Default", Gd.Out(
                        "TaskQueue", Gd.Out("TaskType", "Fight", "Name", "单对象", "IsEnable", null),
                        "Gui", Gd.Out("StartUpSettings", Gd.Out("RunDirectly", "yes-manual"))))));
                    var r = Gd.Map(Detect.MaaQueueLines(Gd.Out("cfgPath", p)));
                    string[] ls = Strs(Gd.Get(r, "Lines"));
                    return ls.Length == 4 && ls[0] == "  Fight      单对象          IsEnable= plan="
                        && ls[1] == "  RunDirectly(启动即自动开始)= yes-manual"
                        && ls[3] == "  连接方式 ConnectSettings.Config = ";
                });
                b.Chk("maa-queue-lines：顶层 {} 与空队列数组 → 都是三尾行空值、逐字相同", delegate
                {
                    string p1 = Path.Combine(baseDir, "cfg-brace.json");
                    GdJson.Write(p1, Gd.Out());
                    string p2 = Path.Combine(baseDir, "cfg-arr.json");
                    GdJson.Write(p2, Gd.Out("Configurations", Gd.Out("Default", Gd.Out("TaskQueue", new object[] { }))));
                    string[] l1 = Strs(Gd.Get(Gd.Map(Detect.MaaQueueLines(Gd.Out("cfgPath", p1))), "Lines"));
                    string[] l2 = Strs(Gd.Get(Gd.Map(Detect.MaaQueueLines(Gd.Out("cfgPath", p2))), "Lines"));
                    return l1.Length == 3 && l2.Length == 3
                        && string.Join("|", l1) == string.Join("|", l2)
                        && l1[0] == "  RunDirectly(启动即自动开始)= " && l1[2] == "  连接方式 ConnectSettings.Config = ";
                });

                // ---------- m9a-instance-lines ----------
                string instDir = Path.Combine(baseDir, "config\\instances");
                string appPath = Path.Combine(baseDir, "m9a-app.json");
                Touch(Path.Combine(instDir, "b.json"), T("2026-09-20 10:00:00"));
                Touch(Path.Combine(instDir, "a.json"), T("2026-09-20 10:00:00"));
                Touch(Path.Combine(instDir, "c.json"), T("2026-09-20 10:00:00"));
                File.WriteAllText(Path.Combine(instDir, "b.json"), GdJson.Serialize(Gd.Out(
                    "InstanceName", "Idle", "CurrentTasks", new object[] { "长草<||>x", "日常" })));
                File.WriteAllText(Path.Combine(instDir, "a.json"), GdJson.Serialize(Gd.Out(
                    "InstanceName", "Event", "CurrentTasks", new object[] { "活动<||>a" })));
                File.WriteAllText(Path.Combine(instDir, "c.json"), GdJson.Serialize(Gd.Out("InstanceName", "Blank")));
                // 键名整个是 "Instances.LastActiveName"(带引号属性访问的照抄)，故意用大小写差验证 -eq 不敏感
                GdJson.Write(appPath, Gd.Out("Instances.LastActiveName", "eVeNt"));
                b.Chk("m9a-instance-lines：名序/标记大小写不敏感/'<||>'取首段/'—'连接(BaseName 剥掉最后一段扩展名)", delegate
                {
                    var r = Gd.Map(Detect.M9aInstanceLines(Gd.Out("dir", instDir, "appFile", appPath)));
                    string[] ls = Strs(Gd.Get(r, "Lines"));
                    return ls.Length == 3
                        && ls[0] == "  Event(a) <== 当前 — 活动"
                        && ls[1] == "  Idle(b) — 长草/日常"
                        && ls[2] == "  Blank(c) — ";
                });
                b.Chk("m9a-instance-lines：目录不存在 → 空 Lines(旧 PS 对不存在目录同样一步一跳)", delegate
                {
                    var r = Gd.Map(Detect.M9aInstanceLines(Gd.Out("dir", Path.Combine(baseDir, "nope"), "appFile", appPath)));
                    return Strs(Gd.Get(r, "Lines")).Length == 0;
                });
                // 门 inst/A 的第四行(无 InstanceName 的实例)、inst/B(存在但空的目录)、inst/C(缺 app → THROW)
                b.Chk("m9a-instance-lines：无 InstanceName 的实例 → 空名一行「(d4)」(门 inst/A 的第四行)", delegate
                {
                    string noNameDir = Path.Combine(baseDir, "inst-noname\\instances");
                    Directory.CreateDirectory(noNameDir);
                    File.WriteAllText(Path.Combine(noNameDir, "d4.json"), "{}");
                    var r = Gd.Map(Detect.M9aInstanceLines(Gd.Out("dir", noNameDir, "appFile", appPath)));
                    string[] ls = Strs(Gd.Get(r, "Lines"));
                    return ls.Length == 1 && ls[0] == "  (d4) — ";
                });
                b.Chk("m9a-instance-lines：目录在但一个实例文件都没有 → 0 行(门 inst/B)", delegate
                {
                    string emptyInstDir = Path.Combine(baseDir, "inst-empty\\instances");
                    Directory.CreateDirectory(emptyInstDir);
                    var r = Gd.Map(Detect.M9aInstanceLines(Gd.Out("dir", emptyInstDir, "appFile", appPath)));
                    return Strs(Gd.Get(r, "Lines")).Length == 0;
                });
                b.Chk("m9a-instance-lines：缺 app 文件 → 抛「找不到文件：」原话(展示层不吞，门 inst/C)", delegate
                {
                    string miss = Path.Combine(baseDir, "no-app.json");
                    try { Detect.M9aInstanceLines(Gd.Out("dir", instDir, "appFile", miss)); return false; }
                    catch (FileNotFoundException ex) { return ex.Message == "找不到文件：" + miss; }
                });

                // ---------- verb 信封接线(过 GdCli.Run，不绕过 catch 链) ----------
                b.Chk("接线：maa-queue-lines 缺文件 → 退出码 2、error=「找不到文件：」原话(无前缀)", delegate
                {
                    string miss = Path.Combine(baseDir, "no-such-cfg.json");
                    string ip = Path.Combine(baseDir, "q.in.json");
                    string op = Path.Combine(baseDir, "q.out.json");
                    GdJson.Write(ip, Gd.Out("cfgPath", miss));
                    int code = GdCli.Run("maa-queue-lines", new string[] { "--in=" + ip, "--out=" + op });
                    var env = GdJson.ReadEnvelope(op);
                    return code == 2 && !(bool)Gd.Get(env, "ok") && Gd.Str(Gd.Get(env, "error")) == "找不到文件：" + miss;
                });
                b.Chk("接线：detect-installs 的 Found:false 走 ok:true + data 单键(信封不吞 null)", delegate
                {
                    string ip = Path.Combine(baseDir, "d.in.json");
                    string op = Path.Combine(baseDir, "d.out.json");
                    GdJson.Write(ip, Gd.Out("processNames", new object[] { "gd_no_such_proc_zzz" },
                        "exeNames", new object[] { "nope.exe" }, "nameHints", new object[] { "*NoSuch_zzz*" },
                        "uninstallLike", new object[] { }, "pickProp", "Dir", "relExe", "", "trimSegment", "",
                        "skipScan", true, "roots", new object[] { }));
                    int code = GdCli.Run("detect-installs", new string[] { "--in=" + ip, "--out=" + op });
                    var env = GdJson.ReadEnvelope(op);
                    var data = Gd.Map(Gd.Get(env, "data"));
                    return code == 0 && data != null && data.Count == 1 && !Gd.Truthy(data["Found"]);
                });
                // ---- 刀④ status 族接线(2026-11-04)：体检链问核的两枚纯数据出口(两本 lib\*-status.ps1 四枚写死名册的直身) ----
                // 期望串逐字抄自出仓前的 lib\ark-status.ps1／lib\r1999-status.ps1 与 status.ps1 外壳那把三态尺——
                // 「换答方不换问法」的牙：行集/标签/行序/三态语义任一漂移，这里当场红。
                b.Chk("resolve-status-env-rows：ark 三行的标签与行序逐字(MAA／MAA 配置／方舟端游)", delegate
                {
                    var rr = Gd.Map(Detect.StatusEnvRows(Gd.Map(Gd.Out("id", "ark",
                        "maaDir", "Z:\\gd_no_such_dir_zzz", "maaConfig", "configuration.json", "arknightsExe", ""))));
                    var rows = Gd.List(Gd.Get(rr, "rows"));
                    if (rows.Length != 3) return false;
                    return Gd.Str(Gd.Get(Gd.Map(rows[0]), "label")) == "MAA"
                        && Gd.Str(Gd.Get(Gd.Map(rows[1]), "label")) == "MAA 配置"
                        && Gd.Str(Gd.Get(Gd.Map(rows[2]), "label")) == "方舟端游";
                });
                b.Chk("resolve-status-env-rows：三态齐拍(在场文件=ok／不存在路径=missing／空串=empty)", delegate
                {
                    string hit = Path.Combine(baseDir, "env-row-hit.txt");
                    File.WriteAllText(hit, "x");
                    var rr = Gd.Map(Detect.StatusEnvRows(Gd.Map(Gd.Out("id", "ark",
                        "maaDir", hit, "maaConfig", "", "arknightsExe", "Z:\\gd_no_such_exe_zzz"))));
                    var rows = Gd.List(Gd.Get(rr, "rows"));
                    if (rows.Length != 3) return false;
                    string s0 = Gd.Str(Gd.Get(Gd.Map(rows[0]), "state"));   // base 在场文件 → ok
                    string s1 = Gd.Str(Gd.Get(Gd.Map(rows[1]), "state"));   // rel 没填 → 只出 base(=在场文件) → ok
                    string s2 = Gd.Str(Gd.Get(Gd.Map(rows[2]), "state"));   // 填了但不存在 → missing
                    var e = Gd.Map(Detect.StatusEnvRows(Gd.Map(Gd.Out("id", "ark", "maaDir", ""))));
                    string s3 = Gd.Str(Gd.Get(Gd.Map(Gd.List(Gd.Get(e, "rows"))[0]), "state"));  // 没填 → empty
                    return s0 == "ok" && s1 == "ok" && s2 == "missing" && s3 == "empty";
                });
                b.Chk("resolve-status-env-rows：base 没填时 rel 不许单独成立(Join-GdPath 语义·shown 出空串)", delegate
                {
                    var rr = Gd.Map(Detect.StatusEnvRows(Gd.Map(Gd.Out("id", "ark",
                        "maaDir", "", "maaConfig", "configuration.json", "arknightsExe", ""))));
                    var rows = Gd.List(Gd.Get(rr, "rows"));
                    var row1 = Gd.Map(rows[1]);
                    return Gd.Str(Gd.Get(row1, "shown")) == "" && Gd.Str(Gd.Get(row1, "state")) == "empty";
                });
                b.Chk("resolve-status-env-rows：r1999 三行的标签与行序逐字(M9A／M9A 实例／MuMu)", delegate
                {
                    var rr = Gd.Map(Detect.StatusEnvRows(Gd.Map(Gd.Out("id", "r1999",
                        "m9aDir", "Z:\\gd_no_such_dir_zzz", "m9aInstancesDir", "instances", "mumuDir", "", "mumuManager", "MuMuManager.exe"))));
                    var rows = Gd.List(Gd.Get(rr, "rows"));
                    if (rows.Length != 3) return false;
                    return Gd.Str(Gd.Get(Gd.Map(rows[0]), "label")) == "M9A"
                        && Gd.Str(Gd.Get(Gd.Map(rows[1]), "label")) == "M9A 实例"
                        && Gd.Str(Gd.Get(Gd.Map(rows[2]), "label")) == "MuMu"
                        && Gd.Str(Gd.Get(Gd.Map(rows[2]), "shown")) == "";   // mumuDir 没填 → MuMu 行整行空
                });
                b.Chk("resolve-status-proc-names：两路名册逐字(ark：MAA／Arknights·r1999：m9a)", delegate
                {
                    var a = Gd.List(Gd.Get(Gd.Map(Detect.StatusProcNames(Gd.Map(Gd.Out("id", "ark")))), "names"));
                    var r = Gd.List(Gd.Get(Gd.Map(Detect.StatusProcNames(Gd.Map(Gd.Out("id", "r1999")))), "names"));
                    return a.Length == 2 && Gd.Str(a[0]) == "MAA" && Gd.Str(a[1]) == "Arknights"
                        && r.Length == 1 && Gd.Str(r[0]) == "m9a";
                });
                b.Chk("接线：resolve-status-env-rows 的未知 id 走退出码 2＋ok:false(GdRuleException 判拒、原话给壳 throw，不吞成空名册)", delegate
                {
                    string ip = Path.Combine(baseDir, "se.in.json");
                    string op = Path.Combine(baseDir, "se.out.json");
                    GdJson.Write(ip, Gd.Out("id", "nosuch"));
                    int code = GdCli.Run("resolve-status-env-rows", new string[] { "--in=" + ip, "--out=" + op });
                    var env = GdJson.ReadEnvelope(op);
                    return code == 2 && !(bool)Gd.Get(env, "ok") && Gd.Str(Gd.Get(env, "error")).Contains("不认识 id");
                });
            }
            finally
            {
                try { Directory.Delete(baseDir, true); } catch { }
            }
            return b.Result("--statetest");
        }

        // 副本用例里先建树再探测：Touch 需要一个"现在就存在"的目录路径，单独留个口子
        static void TouchIn(string baseDir, string rel)
        {
            Touch(Path.Combine(baseDir, rel), T("2026-09-20 10:00:00"));
        }

        static string[] Strs(object v)
        {
            var outList = new List<string>();
            foreach (object o in Gd.List(v)) outList.Add(Gd.Str(o));
            return outList.ToArray();
        }
    }
}
