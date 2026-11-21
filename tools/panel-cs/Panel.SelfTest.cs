// 不弹窗的自检本体：--selftest / --consoletest / --exptest / --whytest / --exttest 的断言都在这儿。
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
    // ---------- 主窗体(这一份装的成员见本文件头) ----------
    sealed partial class PanelForm : Form
    {
        // ---------- 自检(--selftest / --headless / --dump) ----------
        public void DumpTree()
        {
            Console.WriteLine("---- 控件树(调试用) ----");
            Console.WriteLine("---- 解析出的行 ----");
            DumpRows("LAST", rowsLast);
            DumpRows("GATE", rowsGates);
            bool? expFlag = ReadExpFlag();
            bool? mumuFlag = ReadBoolFlag(MumuKey), rewardFlag = ReadAllowReward();
            Console.WriteLine("EXP backend=" + HasVisionBackend() + " 端游=" + (expFlag.HasValue ? expFlag.Value.ToString() : "-") +
                              " 模拟器=" + (mumuFlag.HasValue ? mumuFlag.Value.ToString() : "-") +
                              " 领奖=" + (rewardFlag.HasValue ? rewardFlag.Value.ToString() : "-") +
                              " 该露=" + expArkUp + "/" + expMumuUp + "/" + expRewardUp +
                              " 只问不点=" + expAskUp + " 首行=" + expHint.Text);
            List<Row> tt;
            if (timesByPage.TryGetValue("all", out tt)) DumpRows("TIME", tt);
            Console.WriteLine("---- 开机日志区最后几行(真数据，验提示/播报有没有真说出来) ----");
            for (int i = Math.Max(0, logLines.Count - 6); i < logLines.Count; i++)
                Console.WriteLine("LOG[" + logLines[i][0] + "] " + logLines[i][1]);
            DumpWalk(this, "");
            Close();
        }

        static void DumpRows(string what, List<Row> rows)
        {
            foreach (Row r in rows)
                Console.WriteLine(what + " kind=[" + r.Kind + "] game=[" + r.Game + "] text=[" + r.Text + "]" +
                                  (r.Detail.Length > 0 ? " detail=[" + (r.Detail.Length > 60 ? r.Detail.Substring(0, 60) + "…" : r.Detail) + "](" + r.Detail.Length + "字)" : ""));
        }

        void DumpWalk(Control parent, string indent)
        {
            foreach (Control c in parent.Controls)
            {
                string t = c.Text == null ? "" : c.Text;
                if (t.Length > 24) t = t.Substring(0, 24);
                Console.WriteLine(indent + c.GetType().Name + " \"" + t + "\" bounds=" + c.Bounds +
                    " tag=" + (c.Tag == null ? "-" : c.Tag.ToString()) +
                    " fore=" + (c is Label || c is Button ? ((Control)c).ForeColor.ToString() : "-") +
                    " back=" + c.BackColor.ToString() + " vis=" + c.Visible);
                DumpWalk(c, indent + "  ");
            }
        }

        public int SelfTest()
        {
            int bad = 0;
            Console.WriteLine("---- panel-cs 自检(不显示窗口) ----");
            Console.WriteLine("root=" + root + "  引擎=" + engine);
            Console.WriteLine("这一仓=" + string.Join(" + ", GameIds()) + "  入口=" + entryName + "  FLAGS: Ack=" + ackFlagName + " Shop=" + shopFlagName + " Games=" + flagsGames + " ViaTask=" + flagsViaTask);
            Console.WriteLine("导航项: " + string.Join(" / ", new List<string>(navBtns.Keys).ToArray()));
            Console.WriteLine("帮助行 " + helpRows.Count + " 条");
            List<string> miss = HelpRowMisses();
            Console.WriteLine("帮助行里点名的文件都在: " + (miss.Count == 0 ? "True" : "False") +
                              (miss.Count == 0 ? "" : "  → " + string.Join(" , ", miss.ToArray())));
            bad += SummaryEmitSelfTest();
            Location = new Point(-16000, -16000);
            ShowInTaskbar = false;
            Show();
            Hide();
            foreach (string id in new List<string>(navBtns.Keys))
            {
                SelectPage(id);
                if (IsConsolePage(id))
                {
                    bad += AuditConsole(id);
                    continue;
                }
                if (consoleUp || !cardsUp)
                { Console.WriteLine("  布局问题 " + PageTitle() + " 不是主控台页，操作台却还挡在卡片前面"); bad++; }
                foreach (Panel card in new[] { cardTimes, cardLast, cardGates, cardEnv, cardExp })
                {
                    int lines = 0;
                    foreach (Control c in card.Controls)
                    {
                        if (c.Tag == null || !c.Tag.ToString().StartsWith("line:")) continue;
                        lines++;
                        Rectangle a = c.Bounds;
                        Rectangle cl = new Rectangle(Point.Empty, card.ClientSize);
                        if (a.X < 0 || a.Y < 0 || a.Right > cl.Right || a.Bottom > cl.Bottom)
                        { Console.WriteLine("  布局问题 " + PageTitle() + " 越出容器 " + cardTitleOf(card) + ": " + a); bad++; }
                    }
                    if (lines == 0) { Console.WriteLine("  布局问题 " + PageTitle() + " 卡片一行都没渲染: " + cardTitleOf(card)); bad++; }
                    List<Control> kids = new List<Control>();
                    foreach (Control c in card.Controls) kids.Add(c);
                    for (int i = 0; i < kids.Count; i++)
                        for (int j = i + 1; j < kids.Count; j++)
                        {
                            Rectangle a = kids[i].Bounds, b = kids[j].Bounds;
                            if (a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom)
                            { Console.WriteLine("  布局问题 " + PageTitle() + " 互相压住(" + cardTitleOf(card) + "): " + a + " × " + b); bad++; }
                        }
                }
                if (logLines.Count == 0) { Console.WriteLine("  布局问题 日志区是空的"); bad++; }
            }
            SelectPage("all");
            // 主题切换链：切一次、再切回来，断言两套色都真的落到了控件上
            Color before = BackColor;
            OnThemeClick(null, EventArgs.Empty);
            if (BackColor == before) { Console.WriteLine("  主题没生效"); bad++; }
            OnThemeClick(null, EventArgs.Empty);
            Console.WriteLine("布局自检: " + (bad == 0 ? "OK，每一页都不重叠、都在各自容器内、各张卡片都有内容" : "有问题(见上)"));
            Close();
            // 死命令和布局问题一样计入退出码 —— 原 panel.ps1 的 -SelfTest 是 `if ($bad.Count -or $miss.Count) { exit 1 }`，
            // 只印不判就等于没有这一格：验收链那一步只认退出码。
            return (bad == 0 && miss.Count == 0) ? 0 : 1;
        }

        // ---------- 摘要机读面(原 tools\probe-panel-summary-parity.ps1，甲档第十一刀退役) ----------
        // 门里七发"假帧枪"测的是门自己的比较器(Compare-Frames)，对面(PS -DumpSummary)随件删除后那套比较器
        // 没有生产面可证，随门一起退；能搬的只有三格活口：verb 跑得通／信封自洽／同一棵树两拍同文。
        // 这里另加一格"verb 只是 Build() 的无头外壳"，把"面板用的那份与机读面那份不是同一份"挡在编译面上。
        // 用临时树跑，不依赖机器现场(只读自检的老规矩)。
        static int SummaryEmitSelfTest()
        {
            int bad = 0;
            string oldRoot = GdEnv.Root;
            string tmp = Path.Combine(Path.GetTempPath(), "gd-summarieself-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(tmp);
                File.WriteAllText(Path.Combine(tmp, "VERSION"), "0.0.0\n", new UTF8Encoding(false));
                GdEnv.InitForTest(tmp);
                Dictionary<string, object> e1 = (Dictionary<string, object>)GdPanelSummary.Emit(new Dictionary<string, object>());
                List<string> l1 = Words(e1, "Lines");
                Dictionary<string, object> e2 = (Dictionary<string, object>)GdPanelSummary.Emit(new Dictionary<string, object>());
                List<string> l2 = Words(e2, "Lines");
                List<string> built = GdPanelSummary.Build();

                SumChk(ref bad, "摘要机读面：Proto=2(消费方 GdPanelProtoKnown 认同一个数)",
                    Gd.Int(Gd.Get(e1, "Proto"), -1) == 2, "实际 " + Gd.Str(Gd.Get(e1, "Proto")));
                SumChk(ref bad, "摘要机读面：Count 与 Lines 条数一致且非空",
                    Gd.Int(Gd.Get(e1, "Count"), -1) == l1.Count && l1.Count > 0, Gd.Int(Gd.Get(e1, "Count"), -1) + "/" + l1.Count);
                SumChk(ref bad, "摘要机读面：首行就是协议版本 PROTO|2",
                    l1.Count > 0 && l1[0] == "PROTO|2", l1.Count > 0 ? l1[0] : "(空帧)");
                string hdr = "";
                foreach (string l in l1) if (l.StartsWith("HEADER|", StringComparison.Ordinal)) { hdr = l; break; }
                string[] hf = hdr.Length > 0 ? hdr.Split('|') : new string[0];
                SumChk(ref bad, "摘要机读面：HEADER 六列齐、第 6 列回指这棵临时树",
                    hf.Length == 6 && hf[5] == tmp, hf.Length + " 列 " + (hf.Length == 6 ? "[" + hf[5] + "]" : ""));
                bool flags = false;
                foreach (string l in l1) if (l.StartsWith("FLAGS|", StringComparison.Ordinal)) { flags = true; break; }
                SumChk(ref bad, "摘要机读面：FLAGS 行在场(空仓也要有那一行)", flags, "");
                SumChk(ref bad, "摘要机读面：同一棵树连拍两帧逐字相同(现场差不算转写差)",
                    string.Join("\n", l1.ToArray()) == string.Join("\n", l2.ToArray()), "");
                SumChk(ref bad, "摘要机读面：verb 的 Lines 与面板直取 Build() 同文(verb 只是无头外壳)",
                    string.Join("\n", built.ToArray()) == string.Join("\n", l1.ToArray()), "");
            }
            catch (Exception ex)
            {
                SumChk(ref bad, "摘要机读面：整段不该抛(" + ex.Message + ")", false, "");
            }
            finally
            {
                if (oldRoot != null) GdEnv.InitForTest(oldRoot);
                try { Directory.Delete(tmp, true); } catch { }
            }
            return bad;
        }

        static List<string> Words(Dictionary<string, object> d, string key)
        {
            List<string> o = new List<string>();
            foreach (object v in Gd.List(Gd.Get(d, key))) o.Add(Gd.Str(v));
            return o;
        }

        static void SumChk(ref int bad, string name, bool ok, string extra)
        {
            if (!ok) bad++;
            Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + name +
                (extra.Length > 0 ? "　(" + extra + ")" : ""));
        }

        // 帮助行(「命令行怎么跑」那几行)里点名的 .ps1/.cmd 必须真在这棵树里。
        // 逐字转写原 panel.ps1 的 Get-PanelHelpRowMisses(该件已随乙档刀③ 删除)：上面按 Need 滤过一遍，这一条查的是
        // "写行的时候漏了 Need" —— 只有 --selftest 会在导出目录里被跑到，所以它能挡住
        // "发布仓库里教玩家敲一个不存在的命令"。正则、TrimStart、两条查找路径(先 root\ 再 root\tools\)都不许改口径。
        List<string> HelpRowMisses()
        {
            List<string> miss = new List<string>();
            foreach (string[] r in helpRows)
            {
                string cmd = r.Length > 1 && r[1] != null ? r[1] : "";
                foreach (Match m in Regex.Matches(cmd, "[A-Za-z0-9_\\-\\\\]+\\.(?:ps1|cmd)"))
                {
                    string name = m.Value.TrimStart('\\', '.');
                    if (PathIsThere(Path.Combine(root, name)) || PathIsThere(Path.Combine(root, Path.Combine("tools", name)))) continue;
                    miss.Add(r[0] + " → " + name);
                }
            }
            return miss;
        }

        // PS 侧用的是 Test-Path -LiteralPath：目录也算"在"，所以这里不能只写 File.Exists。
        static bool PathIsThere(string p)
        {
            return File.Exists(p) || Directory.Exists(p);
        }

        string cardTitleOf(Panel card)
        {
            foreach (Control c in card.Controls)
                if (c.Tag != null && c.Tag.ToString() == "cardtitle") return c.Text;
            return "?";
        }

        // 主控台页的版式自检：操作台得露着、卡片/动作区得让位，页内控件不许越出容器、不许互相压住。
        // 卡片那套断言在主控台页上没有意义(整块被盖住了)，所以这里换成认 consoleHost。
        int AuditConsole(string id)
        {
            int bad = 0;
            string what = ConsoleTitle(id);
            if (!consoleUp) { Console.WriteLine("  布局问题 " + what + " 页操作台没要求显示"); return 1; }
            if (cardsUp) { Console.WriteLine("  布局问题 " + what + " 页卡片或动作区没让位"); bad++; }
            List<Control> kids = new List<Control>();
            foreach (Control c in consoleHost.Controls) kids.Add(c);
            if (kids.Count < 3) { Console.WriteLine("  布局问题 " + what + " 页只摆了 " + kids.Count + " 个控件，像是没建起来"); bad++; }
            Rectangle cl = new Rectangle(Point.Empty, consoleHost.ClientSize);
            foreach (Control c in kids)
            {
                Rectangle a = c.Bounds;
                if (a.X < 0 || a.Y < 0 || a.Right > cl.Right || a.Bottom > cl.Bottom)
                { Console.WriteLine("  布局问题 " + what + " 页控件越出操作台(" + Describe(c) + "): " + a); bad++; }
            }
            for (int i = 0; i < kids.Count; i++)
                for (int j = i + 1; j < kids.Count; j++)
                {
                    Rectangle a = kids[i].Bounds, b = kids[j].Bounds;
                    if (a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom)
                    { Console.WriteLine("  布局问题 " + what + " 页两个控件互相压住(" + Describe(kids[i]) + " × " + Describe(kids[j]) + ")"); bad++; }
                }
            if (id == "hist")
            {
                int rows = 0;
                if (histHost != null) foreach (Control c in histHost.Controls) if (c is Label) rows++;
                if (File.Exists(HistoryPath) && histRows > 0 && rows == 0)
                { Console.WriteLine("  布局问题 历史页有 " + histRows + " 条历史却一行没铺"); bad++; }
            }
            Console.WriteLine("  " + what + " 页：" + kids.Count + " 个控件，边界/叠压检查" + (bad == 0 ? "通过" : "有红项"));
            return bad;
        }

        static string Describe(Control c)
        {
            string t = c.Text == null ? "" : c.Text;
            if (t.Length > 14) t = t.Substring(0, 14) + "…";
            return c.GetType().Name + (t.Length > 0 ? "「" + t + "」" : "");
        }

        // --consoletest：主控台四页的离线自检。零弹窗、不碰真 settings.json(读写全部打在 %TEMP% 的一份副本上)、
        // 不起 daily-clear/status/setup 那一类脚本(只有打包那一步按需起一次 Compress-Archive，用完即删)。
        // 验的是"把手接得对"：转达的行、转发给 setup.ps1 的参数、写回的键、包里抹没抹掉 key。
        public int ConsoleTest()
        {
            int fail = 0;
            chkFail = 0;    // Chk 的账记在字段上，收尾时并进来(见 Chk 上方注释)
            Console.WriteLine("---- panel-cs 主控台四页自测(不显示窗口；只起一次压缩，不跑日常脚本) ----");
            Console.WriteLine("root=" + root + "  引擎=" + Path.GetFileName(engine) + "  这一仓=" + string.Join(" + ", GameIds()));
            string keepSettings = settingsPathOverride, keepHistory = historyPathOverride;
            string tmpSettings = Path.Combine(Path.GetTempPath(), "gd-consoletest-settings-" + Process.GetCurrentProcess().Id + ".json");
            pump.Stop(); beat.Stop();
            Location = new Point(-16000, -16000);
            ShowInTaskbar = false;
            try
            {
                // ---- 1) JSON 转义往返：路径里的反斜杠/引号/中文/制表都必须原样回来 ----
                // 这几条是**编出来的形状**(有空格、有版本号、有反斜杠)，不许抄本机真路径：
                // lib\common.ps1 的 Test-GdExportLeaks 会拿 settings.json 里的每个本机值来扫源码
                // (tools\test-modules.ps1 [m8] 现在就扫，不用等导出)，
                // 上一轮就是因为把真实的 MAA 目录端游路径粘进测试样本，导出时被这道闸判红。
                string[] probes = new string[] {
                    @"C:\Tools\MAA-v0.0.0-win-x64",
                    @"D:\Games\Arknights Game\Arknights.exe",
                    "带\"引号\"的路径",
                    @"尾巴一个反斜杠\",
                    "tab\t制表 与 中文"
                };
                foreach (string s in probes)
                {
                    string rt = JsonUnescape(JsonEscape(s));
                    Chk("转义往返：" + s.Replace("\t", "\\t"), rt == s, rt == s ? "" : "回来的是 " + rt.Replace("\t", "\\t"));
                }
                // 转义过的串里，每个 " 前头都必须是奇数个反斜杠；偶数(含 0)就是裸引号，会把 settings.json 那一行弄断
                string escq = JsonEscape("他说\"停\"，然后\\继续\"");
                bool naked = false;
                for (int i = 0; i < escq.Length; i++)
                {
                    if (escq[i] != '"') continue;
                    int bs = 0;
                    for (int j = i - 1; j >= 0 && escq[j] == '\\'; j--) bs++;
                    if (bs % 2 == 0) naked = true;
                }
                Chk("转义后没有裸引号(每个 \\\" 前都是奇数个反斜杠)", !naked, escq);

                // ---- 2) 标量读写：全部打在临时副本上，真配置一个字节都不动 ----
                bool haveReal = File.Exists(Path.Combine(root, @"config\settings.json"));
                byte[] realBytes = haveReal ? File.ReadAllBytes(Path.Combine(root, @"config\settings.json")) : null;
                if (haveReal) File.Copy(Path.Combine(root, @"config\settings.json"), tmpSettings, true);
                else File.WriteAllText(tmpSettings, "{\n  \"paths\": {\n    \"maaDir\": \"C:\\\\Fake\\\\MAA\"\n  },\n  \"notify\": {\n    \"webhook\": \"\"\n  }\n}\n", new UTF8Encoding(false));
                settingsPathOverride = tmpSettings;
                string beforeMaa = ReadSettingString("maaDir");
                // 这一格的对面是「这一仓要求玩家填的 paths 键」，不是写死的 maaDir：maaDir 只在装了
                // lib\maa.ps1 的那棵树（合体树／方舟仓）才是必填项，1999 那本 settings.json 里根本没有这一行，
                // 那边读得到的是 m9aDir —— 与下面第 4 段那句「单游戏仓正常，跳过」是同一套分档(判据源都是
                // GdSettings.RequiredPathKeys，它按 lib 里真有的库点名)，两格不许一个认仓、一个写死方舟。
                // 2026-10-24 推仓㊿ 的验收链第一次咬到这格：刀② 把 build.ps1 里"起 winexe 那把尺"换成
                // Start-Process -Wait -PassThru 之后退出码才是真的，此前 `& $exe --consoletest` 从不等退也不更新
                // $LASTEXITCODE ⇒ 这一格在 1999 树上从 2026-09-26 起就一直是红的，只是没人量得到（假绿）。
                List<string> reqKeys = GdSettings.RequiredPathKeys();
                string probeKey = null, probeVal = null;
                foreach (string k in reqKeys)
                {
                    string v = ReadSettingString(k);
                    if (v != null) { probeKey = k; probeVal = v; break; }
                }
                Chk("临时副本读得到 paths 里的路径键（" + (probeKey == null ? "一枚都没有" : probeKey) + "）", probeKey != null,
                    "这一仓要求的键=" + (reqKeys.Count == 0 ? "无" : string.Join(" , ", reqKeys.ToArray())));
                Chk("键不存在时说的是『老模板、改不动』而不是硬塞",
                    WriteSettingString("no_such_key_for_test", "x").IndexOf("老模板") >= 0, "");

                string hook = "https://example.invalid/hook?a=1&b=2";
                string origHook = ReadSettingString("webhook");
                if (origHook == null) origHook = "";
                string w = WriteSettingString("webhook", hook);
                Chk("写 webhook 有成(" + ShortMsg(w) + ")", w.Length == 0 || w.IndexOf("写好了") >= 0, w);
                Chk("读回来和写进去一模一样", ReadSettingString("webhook") == hook, "读到 " + ReadSettingString("webhook"));
                Chk("改动没牵连别处：" + (probeKey == null ? "paths 键" : probeKey) + " 还是原来那条",
                    probeKey == null || ReadSettingString(probeKey) == probeVal, "");
                string w2 = WriteSettingString("webhook", origHook);
                Chk("能原样写回(" + ShortMsg(w2) + ")", w2.Length == 0 || w2.IndexOf("写好了") >= 0, w2);
                Chk("写回后和动手前一字不差", ReadSettingString("webhook") == origHook, "读到[" + ReadSettingString("webhook") + "]");
                byte[] head = new byte[3];
                using (FileStream fs = new FileStream(tmpSettings, FileMode.Open, FileAccess.Read)) { if (fs.Read(head, 0, 3) < 3) { head = new byte[] { 0, 0, 0 }; } }
                Chk("写出去的还是 UTF-8 无 BOM(5.1 读带 BOM 的中文行会翻车)", !(head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF),
                    head[0].ToString("X2") + head[1].ToString("X2") + head[2].ToString("X2"));
                if (haveReal)
                {
                    byte[] tmpBytes = File.ReadAllBytes(tmpSettings);
                    bool same = SameBytes(tmpBytes, realBytes);
                    string why = tmpBytes.Length + " vs " + realBytes.Length + " 字节";
                    // 差 3 个字节、且多的那 3 个正好是文件开头那串 EF BB BF —— 这不是"哪笔写没回退"，
                    // 是这份 config\settings.json 被 5.1 的 Set-Content -Encoding utf8 写过(它必带 BOM)，
                    // 而面板按这个工程的规矩写出去的一直是"UTF-8 无 BOM"。分开说，别让人去查写回的键。
                    if (!same && realBytes.Length >= 3 && tmpBytes.Length == realBytes.Length - 3 &&
                        realBytes[0] == 0xEF && realBytes[1] == 0xBB && realBytes[2] == 0xBF)
                    {
                        byte[] realNoBom = new byte[realBytes.Length - 3];
                        Array.Copy(realBytes, 3, realNoBom, 0, realNoBom.Length);
                        if (SameBytes(tmpBytes, realNoBom))
                            why = "差的正好是开头那 3 个 BOM 字节：真 config\\settings.json 带 BOM(被 5.1 的 Set-Content 写过)，" +
                                  "面板写出去的是工程规定的无 BOM 那份，键本身一个没变 —— 把配置另存成 UTF-8 无 BOM(或跑一次 setup.ps1)再复测";
                    }
                    Chk("绕完这一圈，副本和真 settings.json 逐字节一样(那几笔写全回退了)", same, why);
                }

                // ---- 3) 四页都能建起来、都让卡片让位、控件不越界不叠压 ----
                Show(); Hide();
                string[] ids = new string[] { "cfg", "sched", "hist", "diag" };
                foreach (string id in ids)
                {
                    SelectPage(id);
                    int btns = 0;
                    foreach (Control c in consoleHost.Controls) if (c is Button) btns++;
                    int a = AuditConsole(id);
                    Chk(id + " 页版式自检", a == 0, "红项 " + a);
                    Chk(id + " 页有能点的把手(按钮 " + btns + " 个)", btns >= 2, "");
                    fail += a;
                    if (btns < 2) fail++;
                }
                SelectPage("all");
                Chk("回总览后卡片回来了、操作台退下去了", consoleUp == false && cardsUp, "");

                // ---- 4) 配置页：现值进框、只把改过的交给 setup.ps1、带引号的路径挡在命令行外 ----
                SelectPage("cfg");
                TextBox maa;
                if (boxByKey.TryGetValue("maaDir", out maa))
                {
                    Chk("配置页的 maaDir 框里就是 settings.json 里那条", maa.Text == beforeMaa, "框[" + maa.Text + "] 文件[" + beforeMaa + "]");
                    string was = maa.Text;
                    maa.Text = @"D:\Tools\MAA 新位置";
                    List<string> rejected = new List<string>();
                    List<string> args = ChangedPathArgs(rejected);
                    Chk("改过的那项变成 -MaaDir + 带引号的值", args.IndexOf("-MaaDir") >= 0 &&
                        (args.IndexOf("\"D:\\Tools\\MAA 新位置\"") >= 0 || args.Contains("\"D:\\Tools\\MAA 新位置\"")), string.Join(" ", args.ToArray()));
                    Chk("没改动的项不跟着下发(其余沿用现有配置)", CountSwitches(args, "-ArkExe") == 0 && CountSwitches(args, "-M9aDir") == 0, string.Join(" ", args.ToArray()));
                    maa.Text = "有\"引号\"的路径";
                    rejected.Clear();
                    List<string> args2 = ChangedPathArgs(rejected);
                    Chk("带引号的路径不交给命令行，并且当面说清为什么", args2.Count == 0 && rejected.Count == 0 == false,
                        "args=" + args2.Count + " 挡下=" + rejected.Count);
                    maa.Text = was;
                    Chk("全部回到原值后不再下发任何参数", ChangedPathArgs(null).Count == 0, "");
                }
                else Chk("这棵树上没有 maaDir 这一项(单游戏仓正常)，跳过第 4 段", true, "");

                // ---- 5) 定时页：面板读到的时间点 == 判定层转达的行数；不像时间点的字符串不落地 ----
                string[] times = ReadTimes();
                List<Row> trows; timesByPage.TryGetValue("all", out trows);
                int tn = trows == null ? 0 : trows.Count;
                Chk("时间点读到了(" + times.Length + " 个)并且和 TIME 行对得上(" + tn + " 行)",
                    times.Length == 0 || tn == 0 || times.Length == tn, times.Length + " vs " + tn);
                TextBox tb;
                if (boxByKey.TryGetValue("times", out tb)) Chk("定时页框里是现值(逗号分隔)", tb.Text.Replace(" ", "") == string.Join(",", times), "[" + tb.Text + "]");

                // ---- 6) 历史页：铺的行数 = min(12, 历史条数)，带原因的行点得动 ----
                SelectPage("hist");
                int recs = 0;
                try { List<string[]> rr = ParseCsv(ReadAllShared(HistoryPath)); recs = rr.Count > 1 ? rr.Count - 1 : 0; } catch { }
                int expect = recs > 12 ? 12 : recs;
                Chk("历史页铺了 " + histRows + " 行，该是 " + expect, histRows == expect, "history.csv 共 " + recs + " 条");
                Label clickable = null, plain = null;
                foreach (Control c in histHost.Controls)
                {
                    Label l = c as Label; if (l == null) continue;
                    if (HasClick(l) && clickable == null) clickable = l;
                    if (!HasClick(l) && plain == null) plain = l;
                }
                if (clickable != null)
                {
                    int n0 = logLines.Count;
                    Chk("点一行带原因的，完整那句进了日志区", RaiseClick(clickable) && logLines.Count > n0, "日志行数 " + n0 + "→" + logLines.Count);
                }
                else Console.WriteLine("  (这份历史里没有带原因的行，跳过分身检查)");
                if (plain != null) Chk("没原因的行不装得能点", !HasClick(plain), "");
                // 行是"数格子"拼出来的(Pad 里中文算两格)，只有等宽字体才对得齐；
                // 这里量两行前三列的像素宽，差太多就说明换字体后列又歪了。
                if (clickable != null)
                {
                    int wArk = TextRenderer.MeasureText(MonoProbeA, MonoFont()).Width;
                    int wR = TextRenderer.MeasureText(MonoProbeB, MonoFont()).Width;
                    Chk("历史表前三列在不同行之间对得齐", Math.Abs(wArk - wR) <= 1, "字体[" + MonoFont().FontFamily.Name + "] 方舟行 " + wArk + "px · 1999 行 " + wR + "px");
                }

                // ---- 7) 诊断包：摘要里没有 key，包本身能生成 ----
                settingsPathOverride = null;                 // 抹除检查要拿真配置说事
                string key = ReadSettingString("visionApiKey");
                string digest = Path.Combine(Path.GetTempPath(), "gd-consoletest-digest-" + Process.GetCurrentProcess().Id + ".txt");
                WriteSettingsDigest(digest);
                string dtxt = File.ReadAllText(digest);
                if (key != null && key.Length > 0)
                {
                    Chk("配置摘要里没有那把视觉 Key", dtxt.IndexOf(key) < 0, "");
                    Chk("抹掉的是值，键名还在(不然看不出配过没配)", dtxt.IndexOf("visionApiKey") >= 0 && dtxt.IndexOf("<已抹掉>") >= 0, "");
                    int leaks = 0;
                    foreach (string[] e in logLines) if (e[1].IndexOf(key) >= 0) leaks++;
                    Chk("逛完四页，日志区里也没露出过 key", leaks == 0, "命中 " + leaks + " 行");
                }
                else Console.WriteLine("  (这台机器没配视觉 key，抹除检查只能看键名)");
                Chk("摘要里带着路径类读数(不然递给人也看不出什么)", dtxt.IndexOf("maaDir") >= 0 || dtxt.IndexOf("m9aDir") >= 0, "");
                string packDir = Path.Combine(Path.GetTempPath(), "gd-consoletest-pack-" + Process.GetCurrentProcess().Id);
                string zip = PackDiag(packDir, false, false);
                Chk("包打出来了", zip.Length > 0 && File.Exists(zip), "回报：" + (zip.Length > 0 ? zip : lastPackError));
                if (zip.Length > 0 && File.Exists(zip))
                {
                    Chk("包不是空的(至少几 KB)", new FileInfo(zip).Length > 2048, new FileInfo(zip).Length + " 字节");
                    if (key != null && key.Length > 0)
                    {
                        byte[] raw = File.ReadAllBytes(zip);
                        Chk("zip 里也搜不到那把 key(明文和转义两种都不在)",
                            IndexOfBytes(raw, Encoding.UTF8.GetBytes(key)) < 0 && IndexOfBytes(raw, Encoding.UTF8.GetBytes(JsonEscape(key))) < 0, "");
                    }
                    try { File.Delete(zip); } catch { }
                }
                try { Directory.Delete(packDir, true); } catch { }

                // ---- 8) 协议版本：读不懂的时候必须说出来，不能让格子静默变空 ----
                // 前四条问的是"这份真读数"，后面几条是把 protoVer 拨到对不上的位置，单问 ProtoMismatch()
                // 那句话长什么样(纯函数，不解析、不起子进程，所以对得上/对不上都不会污染上面的四页检查)。
                Chk("判定层这份读数带版本号，而且正好是这个面板认的那一版", protoVer == ProtoKnown,
                    "读到 v" + protoVer + "，面板认 v" + ProtoKnown);
                int keepProto = protoVer;
                protoVer = -1;
                Chk("没有版本行时说的是「它比这个面板旧」，不是沉默", ProtoMismatch().IndexOf("没有协议版本号") >= 0, ProtoMismatch());
                int newer = ProtoKnown + 7;
                protoVer = newer;
                Chk("判定层比面板新：两个版本号都报出来，并给出重编命令",
                    ProtoMismatch().IndexOf("v" + newer) >= 0 && ProtoMismatch().IndexOf("build.ps1") >= 0, ProtoMismatch());
                protoVer = 0;
                Chk("判定层比面板旧(但报了 v0)：说的是「比这个面板认的还旧」", ProtoMismatch().IndexOf("还旧") >= 0, ProtoMismatch());
                protoVer = keepProto;
                Chk("对得上时一个字都不说(别每 45 秒刷一条横幅盖住真读数)", ProtoMismatch().Length == 0, ProtoMismatch());

                // ---- 8b) SV 行(v2)：监督那一行进环境卡、详情按段回来、点它说的是 supervision.json ----
                // 喂合成读数，不碰 state\supervision.json —— 这台机器这一轮可能根本没开监督 agent，
                // 拿现场文件断言就等于把自检押在机器状态上(和 test-ark 不许赌桌面同一条规矩)。
                string keepDump = lastDump;
                string v2 = "PROTO|2\nHEADER|ark r1999|daily-clear.ps1|pwsh.exe|pwsh|root\n" +
                            "ENV|ok|助手路径 3 项全部就绪\n" +
                            "SV|warn|监督：在岗 · ark=quiet|现值：PID 4321 · 上岗 13:00:00¦收尾 13:20:00|" +
                            "最近 1 拍：13:04:00 ark=quiet 助手日志静默 1810秒 进程 在|这一轮没有介入过\n" +
                            "ENV|dim|这一仓：ark + r1999\n";
                ParseSummary(v2);
                Row sv = null;
                foreach (Row r in rowsEnv) if (r.Text.StartsWith("监督：")) sv = r;
                Chk("SV 行进的是环境卡(不是被当成陌生标签整行跳过)", sv != null, "环境卡 " + rowsEnv.Count + " 行");
                Chk("那一句话原样是判定层给的(外壳一个字没改写)", sv != null && sv.Text == "监督：在岗 · ark=quiet", sv == null ? "" : "[" + sv.Text + "]");
                Chk("第三段之后每一段都是一行详情", sv != null && sv.Detail.Split('\n').Length == 3,
                    sv == null ? "无" : sv.Detail.Split('\n').Length + " 段");
                Chk("正文里的 '¦' 没被当成分隔符(它替的是真竖线，是判定层转义过的)",
                    sv != null && sv.Detail.IndexOf("上岗 13:00:00¦收尾 13:20:00") >= 0, sv == null ? "" : sv.Detail);
                Chk("ENV 行不受影响，仍然只有那两条", rowsEnv.Count == 3, rowsEnv.Count + " 行");
                int beforeSv = logLines.Count;
                if (sv != null) ShowRowDetail(sv);
                string svLog = JoinLog(beforeSv);
                Chk("点这一行 → 抬头说的是 supervision.json，不是 history.csv",
                    svLog.IndexOf("supervision.json") >= 0 && svLog.IndexOf("history.csv") < 0, svLog.Replace("\n", " "));
                // 数的是"往日志区添了几条"，不是拼起来的字符串里有几个 \n ——
                // JoinLog 是把每条日志首尾相接返回的(日志区本身就是一行一条)，拿它数换行会永远数出 1。
                Chk("详情三段各自成行(没被折成一整块)", logLines.Count - beforeSv >= 4,
                    (logLines.Count - beforeSv) + " 行");
                // 老判定层(v1，没有 SV)必须照样能读：环境卡少一行而已，不许抛、不许连累别的行
                ParseSummary("PROTO|1\nHEADER|ark r1999|daily-clear.ps1|pwsh.exe|pwsh|root\nENV|ok|助手路径 3 项全部就绪\n");
                bool v1ok = true;
                foreach (Row r in rowsEnv) if (r.Text.StartsWith("监督：")) v1ok = false;
                Chk("v1 读数里没有 SV：环境卡少那一行就完了，不报错也不静默吞掉别的行",
                    v1ok && protoVer == 1 && rowsEnv.Count == 1, "环境卡 " + rowsEnv.Count + " 行 / v" + protoVer);
                if (keepDump != null && keepDump.Length > 0) ParseSummary(keepDump);   // 把真读数还回去，后面几段用的是它

                // ---- 8c) 版本号：标题栏那一串必须来自仓库根的 VERSION，而不是代码里抄的一份 ----
                Chk("VERSION 读得到，形状是 x.y.z(读不到会退成 0.0.0，那说明文件没进这一棵树)",
                    System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$") && version != "0.0.0",
                    "读到 " + version + " / 根 " + root);
                Chk("标题栏带的是同一个读数", Text.IndexOf("· v" + version) >= 0, Text);
                Chk("坏形状(带 v / 段数不对 / 空)一律退成 0.0.0",
                    VersionShape("v1.2.3") == "0.0.0" && VersionShape("1.2") == "0.0.0" && VersionShape("") == "0.0.0",
                    VersionShape("v1.2.3") + " " + VersionShape("1.2") + " " + VersionShape(""));
                Chk("读不到文件也照常开(退 0.0.0，不抛)", ReadVersion(Path.GetTempPath()) != null, "非空即算过");

                // ---- 9) 实验功能卡 + 「只问不点」：三颗勾各自对应一个键，命令行上只有模块 id ----
                // 这一页的控件清单会被 RebuildConsole 清掉重建，所以问配置页之前必须先把配置页换上来
                // (上面第 6 段停在历史页，那会儿 boxByKey 里只有历史页那一两个框)。
                bool vb = HasVisionBackend();
                SelectPage("cfg");
                Chk("配置页那两行视觉配置按「有没有后端」给，不再按 ark 给(1999 单仓也要能填 key)",
                    boxByKey.ContainsKey("visionModel") == vb && boxByKey.ContainsKey("visionApiKey") == vb,
                    "后端=" + vb + " 模型框=" + boxByKey.ContainsKey("visionModel") + " Key框=" + boxByKey.ContainsKey("visionApiKey"));
                SelectPage("all");
                int checks = 0;
                foreach (Control c in cardExp.Controls) if (c is CheckBox) checks++;
                Chk("实验功能卡上有三颗勾(端游兜底 / 模拟器兜底 / 领奖白名单)", checks == 3, "数到 " + checks);
                Chk("两路兜底各自只在有这一路时露出来",
                    expArkUp == (vb && HasGame("ark")) && expMumuUp == (vb && HasGame("r1999")),
                    "端游勾=" + expArkUp + " 模拟器勾=" + expMumuUp + " 这一仓=" + string.Join("+", GameIds()));
                Chk("「只问不点」那颗只在 tools\\vision-ask.ps1 在的时候露出来",
                    expAskUp == (vb && File.Exists(Path.Combine(root, @"tools\vision-ask.ps1"))), "要求显示=" + expAskUp);
                Chk("领奖白名单默认不勾(配置里没写这一行 = 硬禁，和 Get-GdVisionSystemPrompt 的默认一致)",
                    !expRewardUp || !expRewardChk.Checked,
                    "该露=" + expRewardUp + " 勾了=" + expRewardChk.Checked + " 键读到=" + Mid(ReadAllowReward()));
                List<string> va = VisionAskArgs("ark");
                Chk("「只问不点」的命令行上只有 -Game 和模块 id", va.Count == 2 && va[0] == "-Game" && va[1] == "ark", string.Join(" ", va.ToArray()));
                Chk("不成一个词的模块 id 不往命令行上拼(宁可这次不跑)",
                    VisionAskArgs("ark; Remove-Item x").Count == 0 && VisionAskArgs("").Count == 0 && VisionAskArgs("R1999").Count == 0, "");
                if (key != null && key.Length > 0)
                    Chk("那把视觉 Key 不会出现在要带上命令行的任何参数里", string.Join(" ", va.ToArray()).IndexOf(key) < 0, "");
                Chk("在总览上点「只问不点」问的是这一仓解析出来的第一路", AskTargetId() == (games.Count > 0 ? games[0][0] : ""), AskTargetId());
                SelectPage(games.Count > 0 ? games[0][0] : "all");
                Chk("切到某一路那一页，问的就是那一路", AskTargetId() == (games.Count > 0 ? games[0][0] : page), AskTargetId());
                SelectPage("all");
            }
            catch (Exception ex)
            {
                Console.WriteLine("  FAIL 自测自己炸了：" + ex.Message);
                if (ex.StackTrace != null)
                {
                    // 只点到第一处自家帧，别把整条 WinForms 栈刷下来
                    string[] fr = ex.StackTrace.Split('\n');
                    for (int i = 0; i < fr.Length && i < 6; i++) Console.WriteLine("      " + fr[i].Trim());
                }
                fail++;
            }
            finally
            {
                settingsPathOverride = keepSettings;
                historyPathOverride = keepHistory;
                try { if (File.Exists(tmpSettings)) File.Delete(tmpSettings); } catch { }
                try { if (File.Exists(Path.Combine(Path.GetTempPath(), "gd-consoletest-digest-" + Process.GetCurrentProcess().Id + ".txt")))
                        File.Delete(Path.Combine(Path.GetTempPath(), "gd-consoletest-digest-" + Process.GetCurrentProcess().Id + ".txt")); } catch { }
                if (autoBeat) beat.Start();
            }
            fail += chkFail;   // 把 Chk 的账并进来，红项数才和上面打印的一条条对得上
            Console.WriteLine("结论: " + (fail == 0 ? "PASS(四页都接对了)" : "FAIL —— 红 " + fail + " 项"));
            return fail == 0 ? 0 : 1;
        }

        // Chk = 断言 + 计数。以前只打印不计数，"结论 PASS"和退出码都盖不住这里的红项(2026-09-25 发现，已修)：
        // 任何一条 Chk(false) 都必须让 --consoletest 整体变红、退出码非 0。
        // 计数走自家字段(各测试方法里的 fail 是局部变量，方法够不着)，ConsoleTest 在收尾时把它并进 fail。
        int chkFail;
        void Chk(string name, bool ok, string extra)
        {
            if (!ok) chkFail++;
            Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + name + (extra.Length > 0 && !ok ? "　[" + extra + "]" : (extra.Length > 0 ? "　(" + extra + ")" : "")));
        }

        static string ShortMsg(string m)
        {
            if (m.Length == 0) return "无告警";
            return m.Length > 24 ? m.Substring(0, 24) + "…" : m;
        }

        static int CountSwitches(List<string> args, string sw)
        {
            int n = 0;
            foreach (string a in args) if (a == sw) n++;
            return n;
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static int IndexOfBytes(byte[] hay, byte[] needle)
        {
            if (hay == null || needle == null || needle.Length == 0 || hay.Length < needle.Length) return -1;
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                bool hit = true;
                for (int j = 0; j < needle.Length; j++) if (hay[i + j] != needle[j]) { hit = false; break; }
                if (hit) return i;
            }
            return -1;
        }

        // --exptest：实验功能三个布尔键的读写回环自测。
        // 2026-09-26：这张卡管的不再是一个键(端游兜底)，还有模拟器兜底和"领奖白名单"，
        // 而后两个在单游戏仓的老 settings.json 里常常**根本没有这一行** —— 那正是最容易写坏的那种情况，
        // 所以三档都要过，并且过完必须能说出"除这一行以外，副本和真配置逐字节一样"。
        // 写全部打在 %TEMP% 的一份副本上：真 config\settings.json 一个字节都不动(它是玩家自己的配置)。
        public int ExpTest()
        {
            Console.WriteLine("---- panel-cs 实验功能读写自测(打在临时副本上，真配置只读不写) ----");
            if (!ExpImplemented()) { Console.WriteLine("这一仓没有\"问模型\"那一层(tools\\vision\\ask.py 不在)，卡片就该是空的 —— 没得测。"); return 0; }
            string real = Path.Combine(root, @"config\settings.json");
            if (!File.Exists(real)) { Console.WriteLine("没有 " + real + " —— 把 config\\settings.example.json 复制成 settings.json 再跑。"); return 1; }
            string tmp = Path.Combine(Path.GetTempPath(), "gd-exptest-" + Process.GetCurrentProcess().Id + ".json");
            string keep = settingsPathOverride;
            int bad = 0;
            try
            {
                byte[] real0 = File.ReadAllBytes(real);
                File.Copy(real, tmp, true);
                settingsPathOverride = tmp;
                bad += RoundTripBool(ExpKey, "端游那一路的视觉兜底", true);
                bad += RoundTripBool(MumuKey, "模拟器那一路的视觉兜底", true);
                bad += RoundTripBool(RewardKey, "领奖白名单", false);
                // 补不了的时候必须一个字都不动：空 runtime、整段一行写完这两种形状都要当面拒掉
                bad += RefuseInsert("{\n  \"runtime\": {\n  }\n}\n", 1, "runtime 是个空对象");
                bad += RefuseInsert("{\n  \"runtime\": { \"arkVisionRescue\": true }\n}\n", 2, "runtime 整段一行写完");
                byte[] real1 = File.ReadAllBytes(real);
                Tell("绕完这三档，真 config\\settings.json 逐字节没动", SameBytes(real0, real1),
                     real0.Length + " vs " + real1.Length + " 字节");
                if (!SameBytes(real0, real1)) bad++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("  FAIL 自测自己炸了：" + ex.Message);
                bad++;
            }
            finally
            {
                settingsPathOverride = keep;
                foreach (string f in new[] {
                    Path.Combine(Path.GetTempPath(), "gd-exptest-" + Process.GetCurrentProcess().Id + ".json"),
                    Path.Combine(Path.GetTempPath(), "gd-exptest-" + Process.GetCurrentProcess().Id + "-1.json"),
                    Path.Combine(Path.GetTempPath(), "gd-exptest-" + Process.GetCurrentProcess().Id + "-2.json") })
                { try { if (File.Exists(f)) File.Delete(f); } catch { } }
            }
            Console.WriteLine("结论: " + (bad == 0 ? "PASS(三个键都读写一致，真配置没被动过)" : "FAIL —— 红 " + bad + " 项"));
            return bad == 0 ? 0 : 1;
        }

        // 一个键的回环：读现值 → 写反 → 读回 → 写回 → 读回 → 断言"只有这一行变过"。
        // defaultOn = 这一档"配置里没写这一行时"判定层的默认态(两路兜底=开，领奖白名单=关)，
        // 面板显示的必须和它一致，否则界面说开着、实际兜底不生效(见 RefreshCards 同一处注释)。
        int RoundTripBool(string key, string label, bool defaultOn)
        {
            bool? before = ReadBoolFlag(key);
            bool shown = before.HasValue ? before.Value : defaultOn;
            string text0 = File.ReadAllText(SettingsPath);
            string m1 = WriteBoolFlag(key, !shown, label, "已打开", "已关掉");
            bool? mid = ReadBoolFlag(key);
            string m2 = WriteBoolFlag(key, shown, label, "已打开", "已关掉");
            bool? after = ReadBoolFlag(key);
            string text1 = File.ReadAllText(SettingsPath);
            int bad = 0;
            bool okMid = mid.HasValue && mid.Value == !shown;
            bool okBack = after.HasValue && after.Value == shown;
            // 原来没这一行时走的是"补一行"那条路：行数该 +1；原来就有则是原地替换，行数不变。
            int n0 = CountLines(text0), n1 = CountLines(text1);
            int wantLines = n0 + (before.HasValue ? 0 : 1);
            bool only = StripBoolLine(text0, key) == StripBoolLine(text1, key);
            Console.WriteLine("  " + key + "(" + label + ")：" +
                              (before.HasValue ? "现值 " + before.Value : "配置里本来没这一行，按默认当 " + defaultOn) +
                              " → 写 " + !shown + " 读到 " + Mid(mid) + " → 写回 " + shown + " 读到 " + Mid(after));
            Console.WriteLine("      " + m1);
            Console.WriteLine("      " + m2);
            Tell(key + "：翻一次读得回来", okMid, Mid(mid) + " 期望 " + !shown); if (!okMid) bad++;
            Tell(key + "：再翻回去也读得回来", okBack, Mid(after) + " 期望 " + shown); if (!okBack) bad++;
            Tell(key + "：只有这一行动过(别的一个字节不动)", only, ""); if (!only) bad++;
            bool shape = JsonShapeOk(text1);
            Tell(key + "：写完这份还过得了引号/括号配平(补行最怕留下多余逗号)", shape, ""); if (!shape) bad++;
            // 只有"原来没这一行、这次真补了一行"的那一路才请 PowerShell 复述：原地替换那条路的形状本来就是文件里现成的，多问一次白等两秒。
            if (!before.HasValue)
            {
                string note;
                bool parsed = RunJsonProbe(SettingsPath, key, shown, out note);
                Tell(key + "：补出来的这一行 ConvertFrom-Json 读得回来(判定层用的就是它)", parsed, note);
                if (!parsed) bad++;
            }
            Tell(key + "：整份文件只多出这一行(原来没有才补，补一次不补第二次)", n1 == wantLines, n0 + " → " + n1 + "，期望 " + wantLines);
            if (n1 != wantLines) bad++;
            return bad;
        }

        // 认不出来的形状必须"拒 + 一个字都不动"，不能"试着写一下看看"。
        // 这两份是手写的合成配置(不是本机那份)：空 runtime、整段一行写完，都是补行会留下坏 JSON 的形状。
        int RefuseInsert(string shape, int which, string what)
        {
            string keep = settingsPathOverride;
            string tmp = Path.Combine(Path.GetTempPath(), "gd-exptest-" + Process.GetCurrentProcess().Id + "-" + which + ".json");
            try
            {
                File.WriteAllText(tmp, shape, new UTF8Encoding(false));
                settingsPathOverride = tmp;
                byte[] before = File.ReadAllBytes(tmp);
                string msg = WriteBoolFlag(RewardKey, true, "领奖白名单", "已勾上", "已取消");
                bool refused = msg.IndexOf("补不进去") >= 0;
                bool untouched = SameBytes(before, File.ReadAllBytes(tmp));
                Tell(what + "：拒着不写，而且文件一个字没动", refused && untouched, refused ? "" : "它却说：" + msg);
                return refused && untouched ? 0 : 1;
            }
            finally
            {
                settingsPathOverride = keep;
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        // RunJsonProbe = 请 PowerShell 自己的 JSON 解析器把这份文件读一遍，并回述 runtime.<key> 的值。
        // 为什么不能只信 JsonShapeOk：那是自家写的"引号配对 + 括号配平"近似判断，而判定层(lib\common.ps1 的 Read-GdJson)
        // 是 $text | ConvertFrom-Json。面板补出来的这一行只要让它报错，结果就是"界面上说写好了、整份配置其实已经读不出来"，
        // 那是最坏的一种假成功 —— 所以这一步必须由**真正的读方**开口，而不是自家再猜一遍形状。
        // 引擎用 PickEngine()(面板自己挑的那枚 pwsh 7)：判定层跑在哪个引擎上，探针就用同一个，
        // "面板读得回来、命令行读不回来"这类跨引擎差异才有机会在这条自检里露出来。
        // (旧版刻意挑系统 5.1——"它对编码更挑"能多抓一档毛病；C 轮退役 5.1 后换成同引擎口径，
        //  当年的老坑照旧留在下面的守则里。)
        // 读文件那一行必须照抄 lib\common.ps1 的 Read-GdJson([IO.File]::ReadAllText + 剥 BOM)，
        // **不能用 Get-Content -Raw**：那份 settings.json 是无 BOM 的 UTF-8(Write-GdJson 明确用 UTF8Encoding($false) 写的)，
        // 当年 5.1 的 Get-Content 没有 BOM 就按机器 ANSI 码页猜，中文注释里的字节能猜出 0x5C(反斜杠)，
        // 于是 JSON 字符串的转义被弄乱、ConvertFrom-Json 报"传入的数组无效" —— 探针就红了，但文件其实没坏(2026-09-26 实测踩过)。
        // 探针脚本内容仍保持全 ASCII：这是从"5.1 读无 BOM 非 ASCII .ps1 会按码页猜中文"那会儿留下的习惯，留着不碍事。
        bool RunJsonProbe(string file, string key, bool want, out string note)
        {
            note = "";
            string ps1 = Path.Combine(Path.GetTempPath(), "gd-exptest-probe-" + Process.GetCurrentProcess().Id + ".ps1");
            try
            {
                File.WriteAllText(ps1, ProbeScript, new UTF8Encoding(false));
                string exe = PickEngine();
                if (exe.Length == 0) { note = NoEngineHint; return false; }
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + ps1 + "\" -Path \"" + file +
                                "\" -Key " + key + " -Want " + (want ? "True" : "False");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                string so, se; int code;
                using (Process p = Process.Start(psi))
                {
                    so = p.StandardOutput.ReadToEnd();
                    se = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    code = p.ExitCode;
                }
                string first = so.Replace("\r", "").Trim();
                if (first.Length == 0) first = se.Replace("\r", "").Trim();
                // ConvertFrom-Json 报错会把**整份文件**抄进消息里(还会带着按错码页猜出来的乱码字)，
                // 这里只留开头那句：够定位"是没解析出来"还是"解析出来了但值不对"，不至于把自检刷成一屏垃圾。
                first = first.Replace("\n", " ");
                if (first.Length > 96) first = first.Substring(0, 96) + "…";
                note = key + "=" + (want ? "True" : "False") + " → " +
                       (first.Length > 0 ? first : "探针一个字都没说(退出码 " + code + ")");
                return first == "JSONOK";
            }
            catch (Exception ex)
            {
                note = "探针自己炸了：" + ex.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(ps1)) File.Delete(ps1); } catch { }
            }
        }

        static readonly string ProbeScript =
            "param([string]$Path,[string]$Key,[string]$Want)\n" +
            "$ErrorActionPreference='Stop'\n" +
            "try{\n" +
            "  $t=[System.IO.File]::ReadAllText($Path)\n" +
            "  if($t.Length -gt 0 -and $t[0] -eq [char]0xFEFF){$t=$t.Substring(1)}\n" +
            "  $j=$t | ConvertFrom-Json\n" +
            "  $v=$j.runtime.$Key\n" +
            "  if($null -eq $v){'MISSING';exit 0}\n" +
            "  if([string]$v -eq $Want){'JSONOK'}else{'JSONDIFF '+[string]$v}\n" +
            "}catch{'JSONERR '+$_.Exception.Message}\n";

        static string Mid(bool? v) { return v.HasValue ? v.Value.ToString() : "(没读到)"; }

        static int CountLines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').Length;
        }

        // 把目标键那一行摘掉再比：这样"只有这一行变过"就成一个能算的等式，而不是靠人眼看 diff。
        static string StripBoolLine(string text, string key)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string l in text.Replace("\r\n", "\n").Split('\n'))
                if (!Regex.IsMatch(l, "^[^\"]*\"" + Regex.Escape(key) + "\"\\s*:")) sb.Append(l).Append('\n');
            return sb.ToString();
        }

        void Tell(string name, bool ok, string extra)
        {
            Console.WriteLine("  " + (ok ? "PASS  " : "FAIL  ") + name + (!ok && extra.Length > 0 ? "　[" + extra + "]" : ""));
        }

        // --whytest：「上一轮为什么」这条读数链路的离线自测。
        // 喂的是临时目录里的合成 CSV 和合成 -DumpSummary 文本：不碰 state\history.csv、不起 pwsh 子进程、不弹窗。
        // 要验的四件事：CSV 解析(逗号/双引号/'|' 都不能当分隔符)、按表头取列而不是按位置、
        // detail 只挂到红/黄行上、以及同一路后来的空 detail 会把旧原因覆盖掉(留着更早期原因比没有更误导)。
        public int WhyTest()
        {
            Console.WriteLine("---- panel-cs「上一轮原因」自测(合成 CSV，不碰真历史) ----");
            int fail = 0;
            string tmp = Path.Combine(Path.GetTempPath(), "gd-whytest-" + Process.GetCurrentProcess().Id + ".csv");
            string keepOverride = historyPathOverride;
            // 判定层真写出来的那种句子：中英文混排 + 逗号 + 方括号 + 箭头 + 竖线
            string nasty = "M9A 有 7 个任务报『任务失败』(第一个任务[启动游戏]就失败)；资源/程序落后：当前 v4.7.1 → 最新 v4.10.0 | 整包更新，命令行没有更新入口";
            try
            {
                StringBuilder csv = new StringBuilder();
                // 表头故意打乱列序(game 放到最后一列)：按位置读的实现会在这里就崩
                csv.Append("\"time\",\"status\",\"detail\",\"game\"").Append("\r\n");
                csv.Append("\"2026-09-25 19:30:00\",\"ok\",\"完成链路：Award, Fight, Infrast\",\"ark\"").Append("\r\n");
                csv.Append("\"2026-09-25 19:36:15\",\"blocked\",\"").Append(nasty.Replace("\"", "\"\"")).Append("\",\"r1999\"").Append("\r\n");
                csv.Append("\"2026-09-25 20:05:00\",\"blocked\",\"\",\"ark\"").Append("\r\n");
                File.WriteAllText(tmp, csv.ToString(), new UTF8Encoding(false));
                historyPathOverride = tmp;

                List<string[]> recs = ParseCsv(File.ReadAllText(tmp));
                Write("表头 + 3 条记录都解析出来(末尾无换行也不丢一行)", recs.Count == 4);
                Write("打乱列序后按表头取到 game 列", recs[2][3] == "r1999");
                Write("CSV 里的 \"\" 还原成一个引号，逗号/'|' 没被当分隔符", ParseCsv("a,b\r\n\"x,\"\"y\",z|w\r\n")[1][0] == "x,\"y");
                if (recs.Count != 4 || recs[2][3] != "r1999") fail++;

                ParseSummary(SyntheticDump());
                Row ark = FindLast("ark"), m9 = FindLast("r1999");
                Write("红行(r1999)挂上了完整原因", m9 != null && m9.Detail == nasty);
                Write("同一路后来的空 detail 覆盖掉旧原因，不挂(ark)", ark != null && ark.Detail.Length == 0);
                Write("没 detail 的结论行本身不受影响", ark != null && ark.Text.StartsWith("20:05"));
                if (m9 == null || m9.Detail != nasty) fail++;
                if (ark == null || ark.Detail.Length != 0) fail++;

                int before = logLines.Count;
                ShowRowDetail(m9);
                StringBuilder joined = new StringBuilder();
                for (int i = before + 1; i < logLines.Count; i++) joined.Append(logLines[i][1]);
                Write("点这一行 → 完整原因进了日志区，折行后一个字没丢",
                      logLines.Count > before + 1 && NoSpace(joined.ToString()) == NoSpace(nasty));
                if (logLines.Count <= before + 1 || NoSpace(joined.ToString()) != NoSpace(nasty)) fail++;

                List<string> wr = WrapUnits(nasty, 40);
                // 断点处的空格是"本来就该被吃掉"的(在词边界换行)，所以比的是"除空格以外一个字不丢"
                bool joined2 = NoSpace(string.Join("", wr.ToArray())) == NoSpace(nasty);
                bool fits = true;
                foreach (string l in wr) if (Units(l) > 40) fits = false;
                Write("折行不丢字、每行不超宽", joined2 && fits && wr.Count > 1);
                if (!joined2 || !fits || wr.Count <= 1) fail++;

                // 真出过事的那句：断点不能落在 "-" 后面(把 -a 拆成 "-" + "a 实测无效")，也不能留个空格尾巴。
                // 反过来，断在"短参数␣-a"的空格处是可以的，所以这里只禁真正的坏断点。
                string tricky = "命令行 --autostart 没生效(短参数 -a 实测无效)，或资源/程序有新版本要更新；日志：log-20260925.log(游戏进程出现过)";
                List<string> wr2 = WrapUnits(tricky, 40);
                bool cleanBreaks = true;
                foreach (string l in wr2)
                {
                    if (Units(l) > 40) cleanBreaks = false;
                    if (l.EndsWith("-") || l.EndsWith(" ") || l.EndsWith("　")) cleanBreaks = false;
                }
                Write("断点不会把 -a 这种词拆两半(回看到最近的标点/空格再断)",
                      cleanBreaks && NoSpace(string.Join("", wr2.ToArray())) == NoSpace(tricky) && wr2.Count > 1);
                if (!cleanBreaks) fail++;

                // 上面那步是直接调函数，测不到"渲染那一行时到底有没有把点击挂上去"。
                // Label 没有 PerformClick，只能反射拿 Control 的 Click 挂接表再 DynamicInvoke ——
                // 丑，但这条链路一旦退化成"能看见、点不动"，只有这种测法能发现。
                pump.Stop(); beat.Stop();
                Location = new Point(-16000, -16000);
                ShowInTaskbar = false;
                Show();
                SelectPage("all");
                Label rowLbl = FindLabel("19:36");
                int beforeClick = logLines.Count;
                bool invoked = false;
                if (rowLbl != null) invoked = RaiseClick(rowLbl);
                bool grew = invoked && logLines.Count > beforeClick &&
                            NoSpace(JoinLog(beforeClick)).Contains(NoSpace(nasty));
                Write("卡片上那一行确实挂了点击(反射点火后日志区打出全文)", grew);
                if (!grew) fail++;
                Label arkLbl = FindLabel("20:05");
                bool arkPlain = arkLbl != null && !HasClick(arkLbl);
                Write("没原因的结论行不挂点击(ark 那行点不动，也不是点了没反应)", arkPlain);
                if (!arkPlain) fail++;
                Hide();

                historyPathOverride = Path.Combine(Path.GetTempPath(), "gd-whytest-不存在-" + Process.GetCurrentProcess().Id + ".csv");
                LoadLastDetails();
                Write("历史文件不存在时静默(不抛，详情为空)", detailByGame.Count == 0);
                if (detailByGame.Count != 0) fail++;

                string old = Path.Combine(Path.GetTempPath(), "gd-whytest-old-" + Process.GetCurrentProcess().Id + ".csv");
                File.WriteAllText(old, "\"time\",\"game\",\"status\"\r\n\"2026-09-25 19:30:00\",\"ark\",\"ok\"\r\n", new UTF8Encoding(false));
                historyPathOverride = old;
                LoadLastDetails();
                Write("老格式(没有 detail 列)按表头判缺列，不按位置硬读", detailByGame.Count == 0);
                if (detailByGame.Count != 0) fail++;
                try { File.Delete(old); } catch { }
            }
            catch (Exception ex) { Console.WriteLine("  FAIL 自测中途抛了：" + ex.Message); fail++; }
            finally
            {
                historyPathOverride = keepOverride;
                try { File.Delete(tmp); } catch { }
            }

            // 真历史只读一眼，作信息不作断言：新克隆的仓里 history.csv 可能压根没有内容
            try
            {
                LoadLastDetails();
                int with = 0;
                foreach (KeyValuePair<string, string> kv in detailByGame) if (kv.Value.Length > 0) with++;
                Console.WriteLine("  INFO 真历史 " + HistoryPath + " 里有 " + detailByGame.Count + " 路、" + with + " 路带原因");
            }
            catch (Exception ex) { Console.WriteLine("  INFO 真历史读不了：" + ex.Message); }

            Console.WriteLine("结论: " + (fail == 0 ? "PASS(原因链路只转达判定层原文，列序/空覆盖/缺列都按表头判)" : "FAIL(" + fail + " 条不对)"));
            return fail == 0 ? 0 : 1;
        }

        static string SyntheticDump()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("PROTO|1").Append('\n');
            sb.Append("HEADER|ark r1999|daily-clear.ps1|pwsh.exe|pwsh|" + "root").Append('\n');
            sb.Append("GAME|ark|方舟").Append('\n');
            sb.Append("GAME|r1999|1999").Append('\n');
            sb.Append("LAST|err|ark|20:05 ark → blocked -").Append('\n');
            sb.Append("LAST|err|r1999|19:36 r1999 blocked→blocked 662s").Append('\n');
            return sb.ToString();
        }

        Row FindLast(string game)
        {
            foreach (Row r in rowsLast) if (r.Game == game) return r;
            return null;
        }

        static int Units(string s)
        {
            int n = 0;
            foreach (char c in s) n += (c > 0x1FFF) ? 2 : 1;
            return n;
        }

        // 折行本身可能正好落在空格上(行首空格会被 Trim 掉)，所以比对"丢没丢字"时要先把空格摘干净
        static string NoSpace(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s) if (c != ' ' && c != '\t' && c != '\r' && c != '\n') sb.Append(c);
            return sb.ToString();
        }

        // ---- 只在 --whytest 里用到的反射点火：Label 没有 PerformClick(那是 ButtonBase 才有的)，
        // 而控件树里"这一行到底挂没挂 Click"是这次功能真正交付的东西，只能从 Control 内部的
        // 事件表(EventClick 键)把挂接取出来自己点一次。取不到就返回 false，让自测红。 ----
        static Delegate ClickHandlerOf(Control c)
        {
            try
            {
                FieldInfo key = typeof(Control).GetField("EventClick", BindingFlags.Static | BindingFlags.NonPublic);
                PropertyInfo events = typeof(Control).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic);
                if (key == null || events == null) return null;
                object list = events.GetValue(c, null);
                if (list == null) return null;
                PropertyInfo idx = list.GetType().GetProperty("Item");
                if (idx == null) return null;
                return idx.GetValue(list, new object[] { key.GetValue(null) }) as Delegate;
            }
            catch { return null; }
        }

        static bool HasClick(Control c) { return ClickHandlerOf(c) != null; }

        static bool RaiseClick(Control c)
        {
            Delegate d = ClickHandlerOf(c);
            if (d == null) return false;
            try { d.DynamicInvoke(c, EventArgs.Empty); return true; } catch { return false; }
        }

        Label FindLabel(string textStart)
        {
            foreach (Control c in cardLast.Controls)
            {
                Label l = c as Label;
                if (l != null && l.Text.StartsWith(textStart)) return l;
            }
            return null;
        }

        string JoinLog(int from)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = from; i < logLines.Count; i++) sb.Append(logLines[i][1]);
            return sb.ToString();
        }

        // --exttest：外部轮次标记的读写回环自测。不碰游戏、不起助手，只验四件事：
        // 没标记判成空闲、活 PID 判成在跑、已退出的 PID 判成残留(否则一轮崩溃会把面板永久锁死)、
        // 以及日志路径里 JSON 的双重转义能还原成 File.Exists 认得的路径。
        public int ExtTest()
        {
            Console.WriteLine("---- panel-cs 外部轮次标记自测 ----");
            string path = ExtLockPath;
            // 真有一轮在跑时绝不碰这个文件：覆盖了就把跑着那轮的活体标记抹了，撞车保护当场失效。
            int livePid; string x1, x2, x3;
            if (ReadExtRun(out livePid, out x1, out x2, out x3))
            {
                Console.WriteLine("现在有一轮正在跑(PID " + livePid + ")，自测会覆盖它的活体标记 —— 等它结束再跑 --exttest。");
                return 2;
            }
            string backup = null;
            try { if (File.Exists(path)) backup = File.ReadAllText(path); } catch { }

            int mine = Process.GetCurrentProcess().Id;
            // 转义这一条要拿真实存在的文件来验：还原对了 File.Exists 才为 true，
            // 用假路径测不出"还原成了另一个能碰巧存在的怪路径"这种错。
            string realFile = Path.Combine(root, @"state\panel-theme.txt");
            int fail = 0;

            Write("基线：没有活标记", !ReadExtRun(out livePid, out x1, out x2, out x3));
            try
            {
                WriteLock(path, mine, "2026-09-25 19:11:35", "ark + r1999", realFile);
                bool alive = ReadExtRun(out livePid, out x1, out x2, out x3);
                Write("活 PID(" + mine + ")判成在跑", alive && livePid == mine);
                Write("games 读回 ark + r1999", x3 == "ark + r1999");
                Write("started 读回 2026-09-25 19:11:35", x2 == "2026-09-25 19:11:35");
                Write("日志路径反斜杠转义已还原(读回的就是那个真文件)", x1 == realFile && File.Exists(x1));
                if (!alive || x1 != realFile) fail++;

                int dead = DeadPid();
                if (dead > 0)
                {
                    WriteLock(path, dead, "2026-09-25 19:11:35", "ark", realFile);
                    Write("已退出的 PID(" + dead + ")判成残留=没在跑", !ReadExtRun(out livePid, out x1, out x2, out x3));
                }
                else Console.WriteLine("  SKIP 拿不到一个已退出的 PID，残留这条没测到");
            }
            catch (Exception ex) { Console.WriteLine("  FAIL 自测中途抛了：" + ex.Message); fail++; }

            try
            {
                if (backup != null) File.WriteAllText(path, backup, new UTF8Encoding(false));
                else File.Delete(path);
            }
            catch (Exception ex) { Console.WriteLine("  WARN 没能还原 " + path + "：" + ex.Message); }
            Write("跑完已还原(不留标记)", !File.Exists(path) || backup != null);
            if (fail > 0) { Console.WriteLine("结论: FAIL(" + fail + " 条不对)"); return 1; }
            Console.WriteLine("结论: PASS(判活/判残留/转义都对，面板不会把崩溃残留当成在跑)");
            return 0;
        }

        static void Write(string what, bool ok) { Console.WriteLine((ok ? "  PASS " : "  FAIL ") + what); }

        static void WriteLock(string path, int pid, string started, string games, string log)
        {
            string dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string esc = log.Replace("\\", "\\\\");
            string json = "{\"pid\":" + pid + ",\"started\":\"" + started +
                          "\",\"games\":\"" + games + "\",\"log\":\"" + esc + "\"}";
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        // 起一个立刻退出的 cmd，拿它的 PID 用 —— 比瞎猜一个"应该不存在"的数字可靠，
        // 那个数字可能刚好被系统复用，测出来的结论就是假的。
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

    }
}