// 主控台四页：配置 / 定时 / 历史 / 诊断打包，和四页共用的显示与刷新。
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
        // ---------- 页面切换：主控台四页把"四卡 + 动作区"整块换成一张操作台 ----------
        void RebuildConsole(string id)
        {
            consoleHost.Controls.Clear();
            boxByKey.Clear();
            consoleHost.Visible = true;
            consoleNote = MakeLabel(consoleHost, "", 300, 10, CW - 320, 17, ContentAlignment.TopRight, SmallFont(), false);
            themeTag(consoleNote, "dim");
            if (id == "cfg") BuildCfgPage();
            else if (id == "sched") BuildSchedPage();
            else if (id == "hist") BuildHistPage();
            else BuildDiagPage();
            ApplyTheme();
        }

        Label ConsoleCaption(string text, int y)
        {
            Label sq = new Label();
            sq.Text = "";
            sq.Location = new Point(16, y + 2);
            sq.Size = new Size(3, 13);
            sq.Tag = "accent_sq";
            consoleHost.Controls.Add(sq);
            // 第一行(y=10)右边那块位置被 consoleNote(右上角小字)占了：标题只留到 x=294，别和它压住
            int capW = (y <= 10 ? 268 : CW - 42);
            Label l = MakeLabel(consoleHost, text.ToUpper(), 26, y, capW, 17, null, SectionFont(), false);
            l.Tag = "cardtitle";
            return l;
        }

        TextBox MakeBox(Control parent, string key, int x, int y, int w, bool secret)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, 24);
            t.Font = new Font("Microsoft YaHei UI", 9.5f);
            t.BorderStyle = BorderStyle.FixedSingle;
            if (secret) t.UseSystemPasswordChar = true;
            t.Tag = "input";
            parent.Controls.Add(t);
            if (key != null) boxByKey[key] = t;
            return t;
        }

        // ---------- 页 1：配置 ----------
        void BuildCfgPage()
        {
            consoleNote.Text = "改路径 → 代跑 setup.ps1 · 视觉与通知 → 面板行内写(写前备份) · 不点保存不落盘";
            ConsoleCaption("助手路径", 10);
            int y = 32;
            List<CfgField> fields = CfgFields();
            foreach (CfgField f in fields)
            {
                if (f.SetupArg.Length == 0) break;     // 剩下的归下面那组
                AddCfgRow(f, ref y);
            }
            ConsoleCaption("视觉兜底与通知(可选)", y + 6);
            y += 30;   // 标题占 y+6..y+23，下一行输入框从 y+30 起，留出不叠压的空档
            foreach (CfgField f in fields)
            {
                if (f.SetupArg.Length != 0) continue;
                AddCfgRow(f, ref y);
            }

            cfgProbeArk = MakeLabel(consoleHost, "", 16, y + 6, CW - 32, 17, null, SmallFont(), false);
            themeTag(cfgProbeArk, "dim");
            cfgProbeR1999 = MakeLabel(consoleHost, "", 16, y + 25, CW - 32, 17, null, SmallFont(), false);
            themeTag(cfgProbeR1999, "dim");
            RefreshProbeLines();

            Button bScan = MakeButton(consoleHost, "自动扫描并保存", 16, 344, 168, 30, "primary");
            bScan.Click += delegate { StartTask(Path.Combine(root, "setup.ps1"), new List<string>(), "自动扫描助手位置并写配置(setup.ps1)", false); };
            Button bPaths = MakeButton(consoleHost, "保存路径改动", 192, 344, 148, 30, "secondary");
            bPaths.Click += delegate { SavePathEdits(); };
            Button bScalar = MakeButton(consoleHost, "保存视觉与通知", 348, 344, 148, 30, "secondary");
            bScalar.Click += delegate { SaveScalarEdits(); };
            Button bReload = MakeButton(consoleHost, "重读配置", 504, 344, 116, 30, "chip");
            bReload.Click += delegate { LoadCfgValues(); RefreshNow("重读配置"); RefreshProbeLines(); AppendLog("配置页已按磁盘上的 settings.json 重读。"); };
            Button bClear = MakeButton(consoleHost, "清空视觉 Key", 628, 344, 116, 30, "chip");
            bClear.Visible = HasVisionBackend();         // 这一路连拍画面的后端都没有，就没有"视觉 Key"可清
            bClear.Click += delegate
            {
                string r = WriteSettingString("visionApiKey", "");
                TextBox t; if (boxByKey.TryGetValue("visionApiKey", out t)) { t.Text = ""; loadedVal["visionApiKey"] = ""; }
                AppendLog(r.Length == 0 ? "视觉 Key 已从 settings.json 清掉(旧配置备份在 backups\\)。" : "清不掉：" + r);
            };
            Label warn = MakeLabel(consoleHost, "Key 不进命令行、不入仓", 752, 348, 156, 26, null, SmallFont(), false);
            warn.Visible = HasVisionBackend();
            themeTag(warn, "dim");
            LoadCfgValues();
        }

        void AddCfgRow(CfgField f, ref int y)
        {
            Label cap = MakeLabel(consoleHost, f.Title, 16, y + 3, 146, 18, null, null, false);
            cap.Tag = "dim";
            if (f.Help.Length > 0) rowTip.SetToolTip(cap, f.Help);   // 悬停看"这一项到底指哪儿"，不用把说明挤进标签
            TextBox t = MakeBox(consoleHost, f.Key, 168, y, 620, f.Secret);
            loadedVal[f.Key] = "";
            if (f.SetupArg.Length > 0 && !f.NoPick)
            {
                Button b = MakeButton(consoleHost, f.Dir ? "浏览…" : "选文件…", 796, y, 74, 24, "chip");
                bool dir = f.Dir;
                b.Click += delegate { PickPath(t, dir, f.Title); };
            }
            else if (f.SetupArg.Length > 0 && f.NoPick)
            {
                Label s = MakeLabel(consoleHost, "填数字", 796, y + 3, 100, 18, null, SmallFont(), false);
                themeTag(s, "dim");
            }
            else if (f.Secret)
            {
                Label s = MakeLabel(consoleHost, "留空=不改", 796, y + 3, 100, 18, null, SmallFont(), false);
                themeTag(s, "dim");
            }
            y += 26;
        }

        void PickPath(TextBox box, bool dir, string title)
        {
            try
            {
                if (dir)
                {
                    using (FolderBrowserDialog d = new FolderBrowserDialog())
                    {
                        d.Description = title;
                        try { if (box.Text.Length > 0 && Directory.Exists(box.Text)) d.SelectedPath = box.Text; } catch { }
                        if (d.ShowDialog(this) != DialogResult.OK) return;
                        box.Text = d.SelectedPath;
                    }
                }
                else
                {
                    using (OpenFileDialog d = new OpenFileDialog())
                    {
                        d.Title = title;
                        d.CheckFileExists = false;
                        try { if (box.Text.Length > 0 && File.Exists(box.Text)) d.FileName = box.Text; } catch { }
                        if (d.ShowDialog(this) != DialogResult.OK) return;
                        box.Text = d.FileName;
                    }
                }
                AppendLog("选好了一条：" + box.Text + " —— 还没写盘，点「保存路径改动」才交给 setup.ps1。");
            }
            catch (Exception ex) { AppendLog("浏览框没打开：" + ex.Message); }
        }

        void LoadCfgValues()
        {
            foreach (CfgField f in CfgFields())
            {
                TextBox t; if (!boxByKey.TryGetValue(f.Key, out t)) continue;
                string v = ReadSettingString(f.Key);
                string s = v == null ? "" : v;
                t.Text = s;
                loadedVal[f.Key] = s;
            }
        }

        void RefreshProbeLines()
        {
            if (cfgProbeArk == null) return;
            cfgProbeArk.Text = "判定层怎么说(转达 PROBE 原文)：" + ProbeLine("ark");
            cfgProbeR1999.Text = HasGame("r1999") ? "　└ 1999：" + ProbeLine("r1999") : "　└ 这一仓没有 1999 那一路，上面填了也用不上";
        }

        string ProbeLine(string game)
        {
            List<string> parts = new List<string>();
            List<Row> l;
            if (probesByGame.TryGetValue(game, out l))
                foreach (Row r in l) parts.Add(r.Text);
            return parts.Count == 0 ? "没读到探针(判定库不在？)" : string.Join("　", parts.ToArray());
        }

        // 把"哪些框被改过、该带哪几个参数给 setup.ps1"这段算法单拎出来：
        // 它决定"只下发改过的那几项"(其余沿用现有 settings.json)，也决定带引号的路径直接不交给命令行 —— 自检要钉的就是这两条。
        List<string> ChangedPathArgs(List<string> rejected)
        {
            List<string> args = new List<string>();
            foreach (CfgField f in CfgFields())
            {
                if (f.SetupArg.Length == 0 || f.Secret) continue;
                TextBox t; if (!boxByKey.TryGetValue(f.Key, out t)) continue;
                string now = t.Text.Trim();
                string was = loadedVal.ContainsKey(f.Key) ? loadedVal[f.Key] : "";
                if (now.Length == 0 || now == was) continue;
                if (now.IndexOf('"') >= 0)
                {
                    if (rejected != null) rejected.Add(f.Title + "：路径里有 \"，面板不把它交给命令行");
                    continue;
                }
                args.Add(f.SetupArg);
                args.Add("\"" + now + "\"");
            }
            return args;
        }

        void SavePathEdits()
        {
            List<string> rejected = new List<string>();
            List<string> args = ChangedPathArgs(rejected);
            foreach (string r in rejected) AppendLog("这一项没交给 setup.ps1 ——" + r);
            if (args.Count == 0) { AppendLog("路径没改动(或者上面那几项被挡下了)，这次不跑 setup.ps1。"); return; }
            foreach (string a in args) if (a.StartsWith("-")) AppendLog("要下发：" + a + " …");
            List<string> all = new List<string>();
            all.Add("-SkipScan");                 // 没点的那几项沿用现有 settings.json，不重新扫盘
            all.AddRange(args.ToArray());
            StartTask(Path.Combine(root, "setup.ps1"), all, "把改过的路径交给 setup.ps1(-SkipScan：其余沿用现有配置)", false);
        }

        void SaveScalarEdits()
        {
            int changed = 0;
            foreach (CfgField f in CfgFields())
            {
                if (f.SetupArg.Length != 0) continue;
                TextBox t; if (!boxByKey.TryGetValue(f.Key, out t)) continue;
                string now = t.Text.Trim();
                string was = loadedVal.ContainsKey(f.Key) ? loadedVal[f.Key] : "";
                if (f.Secret && now.Length == 0) continue;          // 留空 = 这一项不动(要清请用「清空视觉 Key」)
                if (now == was) continue;
                string r = WriteSettingString(f.Key, now);
                if (r.Length != 0 && r.IndexOf("写好了") < 0) { AppendLog(f.Title + " 没写成：" + r); continue; }
                loadedVal[f.Key] = now;
                changed++;
                AppendLog(f.Title + " 已写进 settings.json" + (f.Secret ? "(值本身不进日志)" : "：" + now) +
                          (r.Length == 0 ? "，旧配置备份在 backups\\" : "，" + r));
            }
            if (changed == 0) AppendLog("视觉/通知这几项没改动，什么都没写。");
            else RefreshNow("保存配置后");
        }

        // ---------- 页 2：定时 ----------
        void BuildSchedPage()
        {
            consoleNote.Text = "时间点存 settings.json 的 schedule.times，注册动作仍归 install-task.ps1";
            ConsoleCaption("机器上现在的定时任务(转达 TIME 原文)", 10);
            schedReadout = MakeLabel(consoleHost, "", 16, 34, CW - 32, 84, null, null, false);
            themeTag(schedReadout, "dim");
            schedReadout.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            int y = 128;
            MakeLabel(consoleHost, "时间点", 16, y + 3, 90, 18, null, null, false).Tag = "dim";
            TextBox t = MakeBox(consoleHost, "times", 108, y, 250, false);
            string[] cur = ReadTimes();
            t.Text = cur.Length == 0 ? "" : string.Join(",", cur);
            loadedVal["times"] = t.Text;
            Label hint = MakeLabel(consoleHost, "逗号分隔，HH:mm；一个时间点一个任务", 366, y + 3, 300, 18, null, SmallFont(), false);
            themeTag(hint, "dim");
            Label note2 = MakeLabel(consoleHost, "跑哪几路由脚本按这一仓有几路决定，面板不动它；换时间点会等正在跑的那一轮结束再动手。", 108, y + 32, 700, 18, null, SmallFont(), false);
            themeTag(note2, "dim");
            RefreshSchedReadout();

            Button bDry = MakeButton(consoleHost, "先看要改什么", 16, 344, 148, 30, "secondary");
            bDry.Click += delegate { RunReschedule(true); };
            Button bApply = MakeButton(consoleHost, "应用时间点(弹一次 UAC)", 172, 344, 190, 30, "primary");
            bApply.Click += delegate { RunReschedule(false); };
            Button bInstall = MakeButton(consoleHost, "按现在的时间点注册", 370, 344, 168, 30, "secondary");
            bInstall.Click += delegate { StartTask(Path.Combine(root, "install-task.ps1"), new List<string>(), "注册每日计划任务(install-task.ps1，要管理员)", false); };
            Button bUninstall = MakeButton(consoleHost, "卸掉这些任务", 546, 344, 130, 30, "chip");
            bUninstall.Click += delegate { StartTask(Path.Combine(root, "install-task.ps1"), new List<string> { "-Uninstall" }, "移除计划任务(install-task.ps1 -Uninstall)", false); };
            Button bReload2 = MakeButton(consoleHost, "重读", 684, 344, 90, 30, "chip");
            bReload2.Click += delegate { RebuildConsole("sched"); RefreshNow("重读定时"); };
        }

        void RunReschedule(bool dryRun)
        {
            TextBox t; if (!boxByKey.TryGetValue("times", out t)) return;
            string raw = t.Text.Trim();
            if (raw.Length == 0) { AppendLog("时间点框是空的 —— 不敢把任务改成「一个都没有」，先填上 HH:mm。"); return; }
            List<string> bad = new List<string>();
            foreach (string one in raw.Split(','))
            {
                string s = one.Trim();
                if (s.Length == 0) continue;
                if (!Regex.IsMatch(s, @"^\d{1,2}:\d{2}$")) bad.Add(s);
            }
            if (bad.Count > 0) { AppendLog("这几个不像时间点：" + string.Join(" ", bad.ToArray()) + "(要 06:00 这种)。"); return; }
            List<string> args = new List<string>();
            args.Add("-Times");
            args.Add("\"" + raw + "\"");
            if (dryRun) args.Add("-DryRun");
            StartTask(Path.Combine(root, @"tools\reschedule-daily.ps1"), args,
                      dryRun ? "预览要改的时间点(reschedule-daily.ps1 -DryRun，不写不注册)" : "改时间点(等当前轮跑完 → 写配置 → 重注册 → 删旧任务)", false);
        }

        void RefreshSchedReadout()
        {
            if (schedReadout == null) return;
            List<string> lines = new List<string>();
            List<Row> t;
            if (timesByPage.TryGetValue("all", out t))
                foreach (Row r in t) lines.Add(r.Kind + "  " + r.Text);
            schedReadout.Text = lines.Count == 0 ? "没读到定时行(判定库不在？)" : string.Join("   ｜   ", lines.ToArray());
        }

        // ---------- 页 3：历史 ----------
        void BuildHistPage()
        {
            consoleNote.Text = "state\\history.csv 是判定层每轮逐字写的，面板只铺表、不改一个字";
            ConsoleCaption("历  史", 10);
            histHost = new Panel();
            histHost.Location = new Point(16, 34);
            histHost.Size = new Size(CW - 32, 300);
            histHost.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            histHost.Tag = "card";
            consoleHost.Controls.Add(histHost);

            histHint = MakeLabel(consoleHost, "", 16, 340, 560, 18, null, SmallFont(), false);
            themeTag(histHint, "dim");
            Button bRef = MakeButton(consoleHost, "刷新历史", 592, 336, 116, 30, "secondary");
            bRef.Click += delegate { RebuildConsole("hist"); };
            Button bLog = MakeButton(consoleHost, "打开日志目录", 716, 336, 116, 30, "chip");
            bLog.Click += delegate
            {
                try { Process.Start("explorer.exe", Path.Combine(root, "logs")); }
                catch (Exception ex) { AppendLog("打不开日志目录：" + ex.Message); }
            };
            RenderHistory();
        }

        void RenderHistory()
        {
            if (histHost == null) return;
            histRows = 0;
            histHost.Controls.Clear();
            List<string[]> recs;
            try { recs = ParseCsv(ReadAllShared(HistoryPath)); }
            catch (Exception ex) { histHint.Text = "读 history.csv 失败：" + ex.Message; return; }
            if (recs.Count < 2) { histHint.Text = "还没有历史(跑过一轮才会有)。"; return; }
            string[] head = recs[0];
            int ti = CsvCol(head, "time"), gi = CsvCol(head, "game"), si = CsvCol(head, "status"),
                mi = CsvCol(head, "mode"), di = CsvCol(head, "duration_sec"), vi = CsvCol(head, "detail");
            if (ti < 0 || gi < 0 || si < 0) { histHint.Text = "history.csv 表头对不上(要 time/game/status)，面板不猜列位。"; return; }

            int total = recs.Count - 1;
            int cap = 12;
            int from = total > cap ? total - cap + 1 : 1;   // 记录从下标 1 开始：取最后 cap 条就是 [total-cap+1 .. total]
            int y = 4;
            int shown = 0;
            for (int i = recs.Count - 1; i >= from; i--)
            {
                string[] r = recs[i];
                if (r.Length <= ti || r.Length <= gi || r.Length <= si) continue;
                string game = r[gi].Trim();
                string status = r[si].Trim();
                string detail = vi >= 0 && r.Length > vi ? r[vi].Trim() : "";
                string kind = status == "ok" ? "ok" : (status == "blocked" || status == "error" ? "err" : "warn");
                string dur = di >= 0 && r.Length > di && r[di].Trim().Length > 0 ? r[di].Trim() + "s" : "-";
                string line = Pad(r[ti].Trim(), 20) + "  " + Pad(GameName(game), 8) + "  " + Pad(dur, 8) + "  " +
                              (detail.Length > 0 ? Cut(detail, 96) : "(这轮没写原因)");
                // 等宽字体：这几列是靠 Pad() 数格子对齐的，用雅黑那种比例字体"方舟"和"1999"宽度就不齐
                Label l = MakeLabel(histHost, line, 6, y, histHost.Width - 12, 18, null, MonoFont(), false);
                themeTag(l, "line:" + kind);
                if (detail.Length > 0)
                {
                    l.Cursor = Cursors.Hand;
                    Row captured = new Row(kind, game, status + " · " + r[ti].Trim(), detail);
                    ToolTipHintRow(l, captured);
                    l.Click += delegate { ShowRowDetail(captured); };
                }
                y += 22; shown++;
            }
            histRows = shown;
            histHint.Text = "共 " + total + " 条，这里显示最近 " + shown + " 条(新的在上) —— 带原因的行可以点，完整那句会打进日志区。";
        }

        void ToolTipHintRow(Label l, Row r)
        {
            rowTip.SetToolTip(l, ToolTipHint(r));
        }

        static string Cut(string s, int n)
        {
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        // ---------- 页 4：诊断打包 ----------
        void BuildDiagPage()
        {
            consoleNote.Text = "只放这台机器上给人看，默认落桌面；包里的 settings 摘要是去掉 key 之后的";
            ConsoleCaption("打  包", 10);
            string[] inc = new string[] {
                "读数.txt —— 最近一次 -DumpSummary 的协议原文(判定层说的那些行，一个字不改)",
                "体检.txt —— status.ps1 -Offline 的输出(只读，不会启动任何程序；含「屏保」那一行稳态读数)",
                "监督.txt / 监督流水.txt —— state\\supervision.json 现值 + 当天 jsonl 最近 200 拍(这一轮没监督过就不进包)",
                "配置摘要.txt —— settings.json 里 paths / schedule / runtime 这几段的现值，visionApiKey 与 webhook 已抹掉",
                "history.csv —— 每轮的状态与结论(判定层逐字写的)",
                "版本.txt —— 这一份是哪一版(读仓库根 VERSION) + 面板外壳/引擎/仓库根",
                "logs\\run-*.log 最近 5 个 —— 我们自己的那轮日志(单个超 4MB 的跳过)"
            };
            int y = 34;
            foreach (string s in inc)
            {
                Label b = MakeLabel(consoleHost, "· " + s, 16, y, CW - 40, 18, null, SmallFont(), false);
                themeTag(b, "dim");
                y += 20;
            }
            Label warn = MakeLabel(consoleHost, "里面有本机绝对路径和某一天的时间点：这类值只在这台机器上成立，别当成仓库里的东西发出去，也别忘了包里有日志里的那些路径。",
                                    16, y + 8, CW - 40, 34, null, SmallFont(), false);
            themeTag(warn, "warn");
            Button bPack = MakeButton(consoleHost, "打包到桌面", 16, 344, 168, 30, "primary");
            bPack.Click += delegate { AppendLog("开始打包：体检那一段要现跑 status.ps1 -Offline，十几秒到几十秒，界面会短暂无响应。"); PackDiag(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), true, true); };
            Button bTmp = MakeButton(consoleHost, "先打一份到临时目录看看", 192, 344, 200, 30, "secondary");
            bTmp.Click += delegate { PackDiag(Path.Combine(Path.GetTempPath(), "gd-diag-preview"), true, false); };
            Button bOpen = MakeButton(consoleHost, "打开桌面", 400, 344, 116, 30, "chip");
            bOpen.Click += delegate
            {
                try { Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); }
                catch (Exception ex) { AppendLog("打不开桌面：" + ex.Message); }
            };
        }

        // 打包本身不判定任何东西：只是把已经在盘上的读数/日志按清单抄一份压成 zip。
        // key 一律不进包(写摘要时按行抹掉)，zip 靠现成的 Compress-Archive 生成，不在 C# 里引新程序集。
        string PackDiag(string destDir, bool tell, bool withStatus)
        {
            if (diagBusy) { if (tell) AppendLog("上一次打包还没完，等它。"); return ""; }
            diagBusy = true;
            string stage = "";
            try
            {
                // 没引擎这台机器连子进程都起不来(见 PickEngine)：先报人话，别让玩家看 Process.Start 的裸错误。
                if (engine.Length == 0) { lastPackError = NoEngineHint; if (tell) AppendLog("打包失败：" + NoEngineHint); return ""; }
                stage = Path.Combine(Path.GetTempPath(), "gd-diag-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(Path.Combine(stage, "logs"));
                if (lastDump != null && lastDump.Length > 0) File.WriteAllText(Path.Combine(stage, "读数.txt"), lastDump, new UTF8Encoding(true));
                else File.WriteAllText(Path.Combine(stage, "读数.txt"), "(这次没读到 -DumpSummary 原文)", new UTF8Encoding(true));
                WriteSettingsDigest(Path.Combine(stage, "配置摘要.txt"));
                // 版本单独进包：递诊断包给人看的时候，"这是哪一版"必须是包自己说得清，而不是靠聊天记录里补一句。
                File.WriteAllText(Path.Combine(stage, "版本.txt"),
                    "版本 v" + version + "`r`n面板 C# 外壳 · 引擎 " + Path.GetFileName(engine) + "`r`n仓库根 " + root + "`r`n",
                    new UTF8Encoding(true));
                if (withStatus) File.WriteAllText(Path.Combine(stage, "体检.txt"), RunStatusOffline(), new UTF8Encoding(true));
                try { if (File.Exists(HistoryPath)) File.Copy(HistoryPath, Path.Combine(stage, "history.csv"), true); } catch { }
                int copied = CopyRecentLogs(Path.Combine(stage, "logs"));
                int svN = CopySupervision(stage);   // 没监督过就是 0，包里不会多出两个空文件

                // 压缩交给现成的 Compress-Archive。这里刻意走"写一个临时 .ps1 再 -File 调它"：
                // powershell.exe 5.1 的 -Command 会把整串当成一个单引号字符串字面量，不执行(实测静默什么都不压)。
                string dest = Path.Combine(destDir, "GD诊断-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
                string pack = Path.Combine(Path.GetTempPath(), "gd-diag-pack-" + Process.GetCurrentProcess().Id + ".ps1");
                StringBuilder ps = new StringBuilder();
                ps.AppendLine("$ErrorActionPreference = 'Stop'");
                ps.AppendLine("New-Item -ItemType Directory -Force -Path '" + QuoteSingle(destDir) + "' | Out-Null");
                ps.AppendLine("Compress-Archive -LiteralPath '" + QuoteSingle(stage) + "' -DestinationPath '" +
                              QuoteSingle(dest) + "' -Force");
                // 带 BOM：这台机器的 5.1 读无 BOM 的 UTF-8 中文 .ps1 会翻车(路径本身是 ASCII，但规矩按规矩来)
                File.WriteAllText(pack, ps.ToString(), new UTF8Encoding(true));

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = engine;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + pack + "\"";
                psi.WorkingDirectory = root;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                Process p = Process.Start(psi);
                string so = p.StandardOutput.ReadToEnd();
                string se = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(60000))
                {
                    try { p.Kill(); } catch { }
                    lastPackError = "打包超时(60 秒)";
                    if (tell) AppendLog("打包超时(60 秒)，这次没成。");
                    CleanupPack(pack, stage); return "";
                }
                if (tell)
                {
                    foreach (string l in (so + se).Replace("\r", "").Split('\n'))
                        if (l.Trim().Length > 0) AppendLog("  " + l);
                }
                bool ok = p.ExitCode == 0 && File.Exists(dest);
                lastPackError = ok ? "" : "退出码 " + p.ExitCode + (se.Trim().Length > 0 ? "；" + OneLine(se.Trim()) : (File.Exists(dest) ? "" : "；zip 没生成"));
                if (tell)
                {
                    AppendLog(ok ? "包好了：" + dest + "(日志 " + copied + " 个；监督件 " + svN + " 份；settings 摘要里没有 key)"
                                 : "打包没成 —— " + lastPackError);
                }
                CleanupPack(pack, stage);
                return ok ? dest : "";
            }
            catch (Exception ex) { lastPackError = ex.Message; if (tell) AppendLog("打包失败：" + ex.Message); return ""; }
            finally { diagBusy = false; }
        }

        static string QuoteSingle(string s)
        {
            return s == null ? "" : s.Replace("'", "''");
        }

        // 打包用的临时脚本和临时目录都得收掉：脚本留在 %TEMP% 里没人清，目录里是抄出来的日志
        void CleanupPack(string script, string stage)
        {
            try { if (script != null && File.Exists(script)) File.Delete(script); } catch { }
            try { if (stage != null && Directory.Exists(stage)) Directory.Delete(stage, true); } catch { }
        }

        static string OneLine(string s)
        {
            if (s == null) return "";
            string r = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return r.Length > 180 ? r.Substring(0, 180) + "…" : r;
        }

        void WriteSettingsDigest(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("settings.json 摘要(生成于 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")");
            sb.AppendLine("规则：_readme 那种长说明不抄；visionApiKey / webhook 两行按值抹成 <已抹掉>，其余原样。");
            try
            {
                if (!File.Exists(SettingsPath)) { sb.AppendLine("(这台机器还没有 config\\settings.json)"); }
                else
                {
                    foreach (string raw in File.ReadAllLines(SettingsPath))
                    {
                        string line = raw.TrimEnd();
                        if (line.Trim() == "{") continue;
                        if (line.IndexOf("\"_readme\"") >= 0) { sb.AppendLine("  \"_readme\": <模板说明，省略>"); continue; }
                        if (Regex.IsMatch(line, "\"(visionApiKey|webhook)\"\\s*:\\s*\"[^\"]+\""))
                            line = Regex.Replace(line, "(:\\s*)\"[^\"]+\"", "$1\"<已抹掉>\"");
                        sb.AppendLine(line);
                    }
                }
            }
            catch (Exception ex) { sb.AppendLine("(读 settings.json 失败：" + ex.Message + ")"); }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        // 体检那一段是"现跑一次只读脚本再把输出抄进包"，不自己算任何东西。
        // 同步等最多 60 秒：面板会短暂无响应，所以点按钮时先在日志里预告了。
        string RunStatusOffline()
        {
            if (engine.Length == 0) return "(" + NoEngineHint + ")";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = engine;
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + Path.Combine(root, "status.ps1") + "\" -Offline";
                psi.WorkingDirectory = root;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
                Process p = Process.Start(psi);
                string so = p.StandardOutput.ReadToEnd();
                string se = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } return "(status.ps1 -Offline 超 60 秒没跑完，这是已拿到的部分)\r\n" + so + se; }
                return "status.ps1 -Offline 退出码 " + p.ExitCode + "\r\n\r\n" + so + (se.Length > 0 ? "\r\n[stderr]\r\n" + se : "");
            }
            catch (Exception ex) { return "(体检没跑成：" + ex.Message + ")"; }
        }

        // 监督 agent 的两件现物：supervision.json 是"此刻"(整体重写)，supervision-YYYYMMDD.jsonl 是流水(逐拍追加)。
        // 为什么流水要裁尾而不是整本拷：它一行一拍、一天几百拍，而跑着的那一枚还在往里写 ——
        // 共享读 + 取末尾 200 行，既不会独占锁死，也不会把几 MB 搬进诊断包(同一条理由原写在退役前的 tools\supervise.ps1 采集契约里，那件 11-02 刀③ 出仓)。
        int CopySupervision(string stage)
        {
            int n = 0;
            try
            {
                string sj = Path.Combine(root, @"state\supervision.json");
                if (File.Exists(sj))
                {
                    try { File.WriteAllText(Path.Combine(stage, "监督.txt"), ReadAllShared(sj), new UTF8Encoding(true)); n++; } catch { }
                }
                string dir = Path.Combine(root, "logs");
                if (!Directory.Exists(dir)) return n;
                List<FileInfo> beats = new List<FileInfo>();
                foreach (string f in Directory.GetFiles(dir, "supervision-*.jsonl")) beats.Add(new FileInfo(f));
                beats.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                if (beats.Count == 0) return n;
                string[] lines = ReadAllShared(beats[0].FullName).Replace("\r\n", "\n").Split('\n');
                int from = lines.Length > 200 ? lines.Length - 200 : 0;
                StringBuilder sb = new StringBuilder();
                sb.Append("# 摘自 ").Append(beats[0].Name).Append("，只给最近 200 拍(全文还在 logs\\ 里，包里没塞整本)").Append('\n');
                for (int i = from; i < lines.Length; i++) { if (lines[i].Trim().Length > 0) sb.Append(lines[i]).Append('\n'); }
                File.WriteAllText(Path.Combine(stage, "监督流水.txt"), sb.ToString(), new UTF8Encoding(true));
                n++;
            }
            catch { }
            return n;
        }

        int CopyRecentLogs(string logStage)
        {
            int n = 0;
            try
            {
                string dir = Path.Combine(root, "logs");
                if (!Directory.Exists(dir)) return 0;
                List<FileInfo> files = new List<FileInfo>();
                foreach (string f in Directory.GetFiles(dir, "run-*.log")) files.Add(new FileInfo(f));
                files.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = 0; i < files.Count && n < 5; i++)
                {
                    if (files[i].Length > 4L * 1024 * 1024) continue;
                    try { File.Copy(files[i].FullName, Path.Combine(logStage, files[i].Name), true); n++; } catch { }
                }
            }
            catch { }
            return n;
        }

        // ---------- 四页共用的显示/刷新 ----------
        void RefreshConsoleInPlace()
        {
            if (page == "cfg") { RefreshProbeLines(); }
            else if (page == "sched") { RefreshSchedReadout(); }
            else if (page == "hist") { RenderHistory(); }
        }

        bool consoleUp;                    // 我们自己"要求显示"的状态：窗体没 Show 时控件的 Visible getter 是有效可见性，一律 false，自检不能拿它当依据
        bool cardsUp = true;               // 同上，卡片/动作区那一侧
        void ShowPageFor(string id)
        {
            bool console = IsConsolePage(id);
            consoleUp = console;
            cardsUp = !console;
            consoleHost.Visible = console;
            cardTimes.Visible = !console;
            cardLast.Visible = !console;
            cardGates.Visible = !console;
            cardEnv.Visible = !console;
            cardExp.Visible = !console;
            act.Visible = !console;
            if (!console) consoleBuilt = null;
        }

        string ConsoleTitle(string id)
        {
            if (id == "cfg") return "配置";
            if (id == "sched") return "定时";
            if (id == "hist") return "历史";
            if (id == "diag") return "诊断";
            return null;
        }

        void RenderRows(Panel card, List<Row> rows, int maxLines)
        {
            // 清掉上一批行(Tag 前缀 line:)，标题/强调块不动
            List<Control> dead = new List<Control>();
            foreach (Control c in card.Controls)
                if (c.Tag != null && c.Tag.ToString().StartsWith("line:")) dead.Add(c);
            foreach (Control c in dead) { card.Controls.Remove(c); c.Dispose(); }

            int w = card.ClientSize.Width - 28;
            int cap = (int)Math.Floor((card.ClientSize.Height - RowY0 - RowH) / (double)RowPitch) + 1;
            if (cap < 1) cap = 1;
            int n = Math.Min(rows.Count, maxLines);
            if (n > cap) n = cap;
            if (rows.Count > n && n >= cap) n--;      // 末尾留一行给"…另有 N 条"
            if (n < 0) n = 0;
            int y = RowY0;
            for (int i = 0; i < n; i++)
            {
                Row r = rows[i];
                Label l = new Label();
                l.AutoSize = false;
                l.AutoEllipsis = true;
                l.Location = new Point(14, y);
                l.Size = new Size(w, RowH);
                l.Text = r.Text;
                l.BackColor = th.Card;      // 同 MakeLabel：不显式给底色，主题换完就是"隐形行"
                l.ForeColor = KindColor(r.Kind);
                l.Tag = "line:" + r.Kind;
                l.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                // 带完整原因的行 = 可点。光标变手 + 悬停先给一句预告，点开才把整段打进日志区。
                // 不做"就地展开成两行"：卡片容量是按行算死的，展开会把同卡别的行挤掉，
                // 而"为什么"恰恰经常在两条以上(方舟和 1999 一起红) —— 那时日志区才是够宽的地方。
                if (r.Detail.Length > 0)
                {
                    Row captured = r;
                    l.Cursor = Cursors.Hand;
                    rowTip.SetToolTip(l, ToolTipHint(captured));
                    l.Click += delegate { ShowRowDetail(captured); };
                }
                card.Controls.Add(l);
                y += RowPitch;
            }
            if (rows.Count > n)
            {
                Label l = new Label();
                l.AutoSize = false;
                l.AutoEllipsis = true;
                l.Location = new Point(14, y);
                l.Size = new Size(w, RowH);
                l.Text = "…另有 " + (rows.Count - n) + " 条，命令行跑 status.ps1 看全部";
                l.BackColor = th.Card;
                l.ForeColor = th.Dim;
                l.Tag = "line:dim";
                l.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                card.Controls.Add(l);
            }
        }

        Color KindColor(string kind)
        {
            switch (kind)
            {
                case "ok": return th.Ok;
                case "warn": return th.Warn;
                case "err": return th.Err;
                case "dim": return th.Dim;
                default: return th.Text;
            }
        }

    }
}