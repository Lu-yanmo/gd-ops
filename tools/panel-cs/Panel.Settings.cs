// 配置读写：settings.json 的行内替换(只动那一个键所在的一行)、改前备份、每文件留 20 份。
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
        // ---------- 实验功能：面板唯一直接读写 settings.json 里"布尔开关"的地方 ----------
        // 为什么敢让 C# 直接碰配置：这不是判定 —— 用不用视觉兜底、准不准它给领取点位，
        // 仍由核内那一路读同一批键决定(Live.cs 的 ArkVisionOn/MumuVisionOn 与 resolve-rescue-context 闸②；11-06 刀⑮ 笔(d) 前写在 lib\vision-rescue.ps1，那件已整件出仓)，面板只是替玩家拧开关。
        // 写法用"整文件行内替换"：只换 true/false 那几个字节，
        // 别的内容(含中文说明、注释性键)一个字节不动，改前照 PS 那边的规矩备份 + 每文件只留 20 份。
        // 现在管两个键：arkVisionRescue(端游兜底总开关) / visionAllowReward(领奖白名单，默认关)。
        static readonly string ExpKey = "arkVisionRescue";
        static readonly string MumuKey = "mumuVisionRescue";
        static readonly string RewardKey = "visionAllowReward";
        static Regex BoolKeyRe(string key) { return new Regex("(\"" + Regex.Escape(key) + "\"\\s*:\\s*)(true|false)"); }
        string settingsPathOverride;       // 只有 --consoletest/--exptest 用它：把读写指到一份临时副本上
        string SettingsPath { get { return string.IsNullOrEmpty(settingsPathOverride) ? Path.Combine(root, @"config\settings.json") : settingsPathOverride; } }
        // 11-06 刀⑮ 笔(d)：实现库的判断从 lib\vision-rescue.ps1 的在位性换到 tools\vision\ask.py 的在位性——兜底判据全在 C#/核内，
        // 唯一还在树外的依赖就是"问模型"那一层的 python 本体；它在不在，与"这张卡片有没有东西可拧"是同一件事。
        bool ExpImplemented() { return File.Exists(Path.Combine(root, @"tools\vision\ask.py")); }

        // 这棵树上到底有没有"能拍画面的后端"：⚠ 11-06 刀⑮ 笔(d)：lib\vision-*.ps1 三件整件出仓，"后端在场"自此不由 PS 件在位性供数——
        // PrintWindow(ark)与 adb(mumu)两段直身是编译进这棵 exe 的 C#(Live.cs／Gate.Host.cs)，闸①的 hasBuilder 默认按 kind 认路(核内 resolve-rescue-context)，
        // 面板这一层只剩一个真问题：这台机器上"问模型"那一层的本体(ask.py)带没带上树。带上了，这一路的两枚后端就都在场。
        // 名单规矩不变：面板不自己记 kind 名单，不认识的第 X 路照旧由核内闸①回那句 skip 话术，不在这儿长第二套判据。
        bool HasVisionBackend() { return ExpImplemented(); }

        bool? ReadExpFlag() { return ReadBoolFlag(ExpKey); }
        string WriteExpFlag(bool on) { return WriteBoolFlag(ExpKey, on, "AI 辅助(视觉兜底)", "已打开", "已关掉"); }
        bool? ReadAllowReward() { return ReadBoolFlag(RewardKey); }
        string WriteAllowReward(bool on) { return WriteBoolFlag(RewardKey, on, "允许领日常奖励", "已勾上", "已取消"); }

        // 勾选框点一下之后做的事：写下去 → 再读回来核对 → 才说话。
        // 为什么要多读这一次：领奖白名单这一档"界面显示勾上了、文件里其实没写进去"比"显示没勾"更糟 ——
        // 那会让人以为兜底现在准点领取了，实际提示词里还是硬禁(反过来也一样，说了不算最坑)。
        // 写没成，就当着人的面把勾退回去，别留一个假状态在界面上。
        void ToggleExpFlag(CheckBox cb, string key, string subject, string onWord, string offWord)
        {
            bool want = cb.Checked;
            string msg = WriteBoolFlag(key, want, subject, onWord, offWord);
            bool? now = ReadBoolFlag(key);
            if (!now.HasValue || now.Value != want)
            {
                cb.Checked = !want;      // 赋值不会再触发 Click，所以不会绕回来写第二遍
                AppendLog(subject + "：没改成" + (want ? "勾上" : "取消") + "，勾已退回 —— " + msg);
                return;
            }
            AppendLog(subject + "：" + msg);
            RefreshCards();
        }

        bool? ReadBoolFlag(string key)
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return null;
                Match m = BoolKeyRe(key).Match(File.ReadAllText(path));
                if (!m.Success) return null;
                return m.Groups[2].Value == "true";
            }
            catch { return null; }
        }

        string WriteBoolFlag(string key, bool on, string subject, string onWord, string offWord)
        {
            if (!ExpImplemented()) return "这一仓没有实验功能的实现库，这个按钮不该出现。";
            string path = SettingsPath;
            if (!File.Exists(path)) return "还没有 config\\settings.json —— 先跑 setup.ps1 生成配置，再回来开/关。";
            try
            {
                string text = File.ReadAllText(path);
                string bakNote = "";
                string next;
                Match m = BoolKeyRe(key).Match(text);
                if (m.Success)
                {
                    next = BoolKeyRe(key).Replace(text, "$1" + (on ? "true" : "false"));
                }
                else
                {
                    // 这一行还不存在(老配置里常见：setup.ps1 不带 -Force 时只往现有 settings.json 里补路径，
                    // 新键不会自己冒出来)。这里在 runtime 段的开头补**一行**，其余字节照原样搬过去。
                    if (!TryInsertBoolLine(text, key, on, out next))
                        return "settings.json 里没有 " + key + " 这一行，也补不进去(runtime 段没找到或形状不认识) —— 重跑 setup.ps1 或手动补一行再试。";
                    // 插行改的是文件**形状**，"读回来是 true"看不出括号有没有被弄坏 —— 落盘前先自己配平核对一次。
                    if (!JsonShapeOk(next))
                        return "补出 " + key + " 这一行之后，settings.json 的形状就不配平了(引号/括号对不上) —— 面板不敢写这一笔，" +
                               "这一项请手动加(改前的 config\\settings.example.json 里有现成的那一行可以抄)。";
                    bakNote = "，并在 runtime 段补出了这一行";
                }
                string bak = BackupSettings(path);
                File.WriteAllText(path, next, new UTF8Encoding(false));
                return subject + (on ? onWord : offWord) + "(写进 config\\settings.json" + bakNote +
                       (bak != null ? "，旧配置已备份到 backups\\" : "，注意：备份没成") + ")。";
            }
            catch (Exception ex) { return "写配置失败：" + ex.Message; }
        }

        // 够用的 JSON 形状检查：引号配对、大括号/方括号配平、不出现 ", }" 或 ", ]" 这种收尾逗号。
        // 为什么自己写而不用 JavaScriptSerializer：build.ps1 只引用 WinForms/Drawing，为一处自检给编译链加引用不值当；
        // 而"补一行"真正会弄坏的就是这三样形状(多余逗号 / 引号没配平)，不是语义。
        static bool JsonShapeOk(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            int depth = 0;
            bool inStr = false, esc = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inStr)
                {
                    if (esc) { esc = false; continue; }
                    if (ch == '\\') { esc = true; continue; }
                    if (ch == '"') inStr = false;
                    continue;
                }
                if (ch == '"') { inStr = true; continue; }
                if (ch == '{' || ch == '[') depth++;
                else if (ch == '}' || ch == ']') { depth--; if (depth < 0) return false; }
                else if (ch == ',')
                {
                    for (int j = i + 1; j < text.Length; j++)
                    {
                        char n = text[j];
                        if (n == ' ' || n == '\t' || n == '\r' || n == '\n') continue;
                        if (n == '}' || n == ']') return false;     // 收尾逗号：JSON 不认
                        break;
                    }
                }
            }
            return depth == 0 && !inStr;
        }

        // 在 `"runtime": {` 那一行后面插一行 `"key": true|false,`，缩进抄下一行键的缩进、换行抄这一行原本的换行。
        // 两处认不出来就回 false 让调用方说人话：① runtime 那一行本身就带了收尾花括号(整段一行写完，插不进去)；
        // ② 紧跟的第一行是 '}' (那是个空对象，插进去会多出一个逗号，JSON 直接坏)。
        // 换行必须**连同 head 一起吃掉**：早先按"这一行末尾"切，切出来的是 { 后面那个 \r，
        // 插完变成 `{` + 裸 \r + 新行 + 原本的 \n —— 文件里凭空多一个空行，而且下一档再想插就找不到 runtime 那行了
        // (--exptest 的"只有这一行动过"就是这么抓出来的)。
        static bool TryInsertBoolLine(string text, string key, bool on, out string next)
        {
            next = null;
            Match head = Regex.Match(text, @"(?m)^([ \t]*)(?:""[^""]+""\s*:\s*)?""runtime""\s*:\s*\{([ \t]*\r?\n)");
            if (!head.Success) return false;
            int after = head.Index + head.Length;          // 指下一行行首
            string nl = head.Groups[2].Value;              // 这一行原本怎么换行，新行就怎么换
            string rest = text.Substring(after);
            Match nxt = Regex.Match(rest, @"(?m)^([ \t]*)(\S.*)$");
            if (!nxt.Success) return false;
            // 紧跟的第一个非空白字符必须是一个键名的引号：'}' 是空对象(插进去多一个逗号)，
            // 别的形状(整段一行写完、被注释性写法弄脏)一律认不出来 —— 认不出来就不动，比"试试看"好。
            if (!nxt.Groups[2].Value.TrimStart().StartsWith("\"")) return false;
            string line = nxt.Groups[1].Value + "\"" + key + "\": " + (on ? "true" : "false") + ",";
            next = text.Substring(0, after) + line + nl + rest;
            return true;
        }

        string BackupSettings(string path)
        {
            try
            {
                string dir = Path.Combine(root, "backups");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string dest = Path.Combine(dir, "settings.json." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");
                File.Copy(path, dest, true);
                List<FileInfo> olds = new List<FileInfo>();
                foreach (string f in Directory.GetFiles(dir, "settings.json.*.bak"))
                { try { olds.Add(new FileInfo(f)); } catch { } }
                olds.Sort(delegate(FileInfo a, FileInfo b) { return b.LastWriteTime.CompareTo(a.LastWriteTime); });
                for (int i = 20; i < olds.Count; i++) { try { olds[i].Delete(); } catch { } }
                return dest;
            }
            catch { return null; }
        }

        // ===================== 主控台四页：配置 / 定时 / 历史 / 诊断 =====================
        // 这一段的定位和「实验功能」卡一样：面板是"把手"，不是"判定"。
        //   读 —— 路径/时间点/视觉项的现值 = 行内读 settings.json(只取那一个键)；
        //          「这一路到底就绪没有」「下一个时间点是几点」这类结论一律转达 -DumpSummary 已有的
        //          PROBE / TIME 行原文，面板不自己算；历史表 = 直读 state\history.csv(判定层写的那几列)。
        //   写 —— 路径改动代跑 setup.ps1(合并、备份、校验的规矩都留在那儿，面板不复制一套)；
        //          视觉模型/地址/Webhook/vmIndex 这类标量走行内替换(和 arkVisionRescue 同一套：备份 + 每文件留 20 份)；
        //          装卸任务、换时间点代跑 install-task.ps1 / reschedule-daily.ps1，那两步会弹一次 UAC，是脚本本来的行为。
        // 例外一条：**视觉 Key 永远不进命令行参数**(进程命令行在别的用户/工具面前是明文)，只在面板里行内写。
        static readonly string[] ConsoleIds = new[] { "cfg", "sched", "hist", "diag" };
        static bool IsConsolePage(string id)
        {
            if (id == null) return false;
            foreach (string s in ConsoleIds) if (s == id) return true;
            return false;
        }

        Panel consoleHost;                 // 盖住"四卡 + 动作区"那块地，只在主控台四页可见
        string consoleBuilt = null;        // consoleHost 里现在装的是哪一页(同页不重建，免得把人家正敲的字抹掉)
        Label consoleNote;                 // 右上角一句话说明
        readonly Dictionary<string, TextBox> boxByKey = new Dictionary<string, TextBox>();
        readonly Dictionary<string, string> loadedVal = new Dictionary<string, string>();
        Label cfgProbeArk, cfgProbeR1999;
        Label schedReadout;
        Panel histHost;
        Label histHint;
        int histRows;                      // 这次真的铺了几行，版式自检要用它区分"没历史"和"铺不出来"
        string lastDump = null;            // 最近一次 -DumpSummary 原文，诊断页直接把它当"当前读数"存档
        bool diagBusy = false;
        string lastPackError = "";         // 打包失败的人话原因，自检要拿它说话

        sealed class CfgField
        {
            public string Key = "";
            public string Title = "";
            public bool Dir;             // true = 目录(浏览用 FolderBrowserDialog)
            public bool NoPick;          // true = 不给浏览按钮(数字序号这类，选不了文件)
            public bool Secret;          // true = 不进命令行、框里打码
            public string SetupArg = ""; // 非空 = 改动通过 setup.ps1 的这个参数下发
            public string Game = "";     // 归属哪一路(""=两路都用)
            public string Help = "";
        }

        // 按"这棵树上真的有这一路"给字段：单游戏仓库里不该出现另一路的路径框
        List<CfgField> CfgFields()
        {
            List<CfgField> f = new List<CfgField>();
            bool ark = HasGame("ark"), r1999 = HasGame("r1999");
            if (ark)
            {
                f.Add(new CfgField { Key = "maaDir", Title = "方舟助手(MAA)目录", Dir = true, SetupArg = "-MaaDir", Game = "ark", Help = "整个程序目录，不是 MAA.exe 本身" });
                f.Add(new CfgField { Key = "arknightsExe", Title = "方舟端游程序", Dir = false, SetupArg = "-ArkExe", Game = "ark", Help = "指到 Arknights.exe" });
            }
            if (r1999)
            {
                f.Add(new CfgField { Key = "m9aDir", Title = "1999 助手(M9A)目录", Dir = true, SetupArg = "-M9aDir", Game = "r1999", Help = "M9A-win-x86_64-… 那一层" });
                f.Add(new CfgField { Key = "mumuDir", Title = "MuMu 管理器目录", Dir = true, SetupArg = "-MuMuDir", Game = "r1999", Help = "下面要有 nx_main\\MuMuManager.exe" });
                f.Add(new CfgField { Key = "vmIndex", Title = "MuMu 虚拟机序号", Dir = false, NoPick = true, SetupArg = "-VmIndex", Game = "r1999", Help = "只有一台时 setup.ps1 会自动填；多台必须指对" });
            }
            // 视觉兜底按"这棵树上有没有能拍画面的后端"给这两行，不按 ark 给：
            // 1999 那一路(adb 拍模拟器屏)用的是同一个模型/同一把 key，只看 HasGame("ark") 会让 1999 单仓没地方填 key。
            if (HasVisionBackend())
            {
                f.Add(new CfgField { Key = "visionModel", Title = "视觉模型", Dir = false, Game = "", Help = "只在助手判到卡住时用来认弹窗，正常一轮一个请求都不发" });
                f.Add(new CfgField { Key = "visionApiKey", Title = "视觉 Key", Dir = false, Secret = true, Game = "", Help = "留空 = 这一项不改；面板不把它带上命令行" });
            }
            f.Add(new CfgField { Key = "webhook", Title = "通知 Webhook", Dir = false, Game = "", Help = "留空 = 不发通知" });
            return f;
        }

        bool HasGame(string id)
        {
            foreach (string[] g in games) if (g[0] == id) return true;
            return false;
        }

        // ---------- settings.json 的标量读写：只换那一个键所在的一行，别的一个字节不动 ----------
        static string JsonEscape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '"') sb.Append("\\\"");
                else if (ch == '\t') sb.Append("\\t");
                else if (ch == '\r' || ch == '\n') sb.Append(' ');
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        static string JsonUnescape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch != '\\' || i + 1 >= s.Length) { sb.Append(ch); continue; }
                char n = s[++i];
                if (n == '\\') sb.Append('\\');
                else if (n == '"') sb.Append('"');
                else if (n == '/') sb.Append('/');
                else if (n == 't') sb.Append('\t');
                else if (n == 'n') sb.Append('\n');
                else if (n == 'r') sb.Append('\r');
                else sb.Append(n);          // \u 之类这台机器的 settings.json 里没有，原样留着别乱猜
            }
            return sb.ToString();
        }

        string ReadSettingString(string key)
        {
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return null;
                Match m = new Regex("\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"").Match(File.ReadAllText(path));
                if (!m.Success) return null;
                return JsonUnescape(m.Groups[1].Value);
            }
            catch { return null; }
        }

        // 返回 null = 改好了；否则是人话版的没改成
        string WriteSettingString(string key, string value)
        {
            string path = SettingsPath;
            if (!File.Exists(path)) return "还没有 config\\settings.json —— 先点「自动扫描」或跑 setup.ps1 生成配置。";
            try
            {
                string text = File.ReadAllText(path);
                Regex re = new Regex("(\"" + Regex.Escape(key) + "\"\\s*:\\s*\")((?:[^\"\\\\]|\\\\.)*)(\")");
                if (!re.IsMatch(text))
                    return "settings.json 里没有 " + key + " 这一行(多半是老模板)，面板改不动 —— 重跑 setup.ps1 补出这一键再改。";
                string bak = BackupSettings(path);
                string repl = re.Replace(text, "$1" + MatcherEscape(JsonEscape(value)) + "$3");
                File.WriteAllText(path, repl, new UTF8Encoding(false));
                return bak != null ? "" : "写好了，但备份没成(backups\\ 没写进去) —— 这一项改动没法回退，留意一下。";
            }
            catch (Exception ex) { return "写 " + key + " 失败：" + ex.Message; }
        }

        static string MatcherEscape(string s)
        {
            // Regex.Replace 的替换串里 $ 和 \ 有特殊含义，路径里的反斜杠已经转义过了，$ 还得再挡一道
            return s.Replace("$", "$$");
        }

        string[] ReadTimes()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new string[0];
                Match m = new Regex("\"times\"\\s*:\\s*\\[([^\\]]*)\\]").Match(File.ReadAllText(SettingsPath));
                if (!m.Success) return new string[0];
                List<string> outList = new List<string>();
                foreach (Match q in Regex.Matches(m.Groups[1].Value, "\"([^\"]*)\"")) outList.Add(q.Groups[1].Value);
                return outList.ToArray();
            }
            catch { return new string[0]; }
        }

    }
}