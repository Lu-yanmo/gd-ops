// tools/panel-cs/Agent.cs —— 面板接「需求管家」的纯规则段：找 python、拼命令行、读信封。
//
// 为什么单独一份：这三件事是整条 GUI 接线里唯一"会算错就得背锅"的部分 ——
// 命令行拼错会让玩家看见一次莫名其妙的 Traceback，找错解释器会让小窗开局就是死的，
// 信封读歪了会把"管家已经办了的事"显示成没办。全部抽成静态纯函数，由 --agenttest 钉住，
// 窗体(Panel.Agent.cs)只负责画和起进程。
//
// 两条从 Python 侧抄来的事实(家不在这里，改了要对齐 tools\gdagent\)：
//   1) 机读通道 stdout **只有一行 JSON 信封**(ensure_ascii=True，中文是 \uXXXX)，人话/日志一律 stderr；
//      动词是 agent-plan(第一击，只出计划不执行) / agent-run(第二击，按计划逐条重闸再跑)。
//   2) 包位置决定工程根(Py 侧 parents[2])，但 `python -m gdagent` 要能 import 到包 ——
//      靠 PYTHONPATH=<root>\tools，和 tools\agent.ps1 同一做法，不改工作目录。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GdPanel
{
    static class GdAgentLink
    {
        // 计划代号的形状 = Python 侧 engine.PLAN_ID_SHAPE 的同一把尺(时间戳-时间-4 位盐)。
        // 它是**盘上的文件名**，要拼进路径，所以形状不合必须先拒，绝不拿人给的字符串当文件名。
        static readonly Regex PlanIdRe = new Regex(@"^\d{8}-\d{6}-[0-9a-f]{4}$");

        public static bool PlanIdOk(string id)
        {
            return id != null && PlanIdRe.IsMatch(id.Trim());
        }

        // ---------- 找解释器：顺序照 lib\common.ps1 的 Get-GdPythonExe，四条理由也照抄在那儿 ----------
        // 1) settings.runtime.pythonExe(人手工指定) 2) 环境变量 GD_PYTHON(不落盘的临时指定)
        // 3) 本仓库自己下的 state\python\python.exe 4) PATH 上的 python.exe / py.exe(商店占位程序跳过)
        public static string RuntimePythonExe(string settingsText)
        {
            if (string.IsNullOrEmpty(settingsText)) return "";
            Match m = new Regex("\"pythonExe\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"").Match(settingsText);
            if (!m.Success) return "";
            return UnescapeJson(m.Groups[1].Value).Trim().Trim('"');
        }

        public static List<string> PythonCandidates(string settingsText, string envGD, string root, string pathVar)
        {
            List<string> list = new List<string>();
            string manual = RuntimePythonExe(settingsText);
            if (manual.Length == 0) manual = (envGD ?? "").Trim().Trim('"');
            Add(list, manual);
            if (!string.IsNullOrEmpty(root)) Add(list, Path.Combine(root, @"state\python\python.exe"));
            // 先 python 后 py，和 PS 的 @('python','py') 外循环一致；WindowsApps 那个是商店占位程序，
            // 一定跑不成(还会白等一次进程)，连试都不试。
            foreach (string name in new string[] { "python.exe", "py.exe" })
                foreach (string dir in (pathVar ?? "").Split(';'))
                {
                    string d = dir.Trim().Trim('"');
                    if (d.Length == 0) continue;
                    string full;
                    try { full = Path.Combine(d, name); }
                    catch { continue; }        // PATH 里出现非法字符(比如就一个 "?")：跳过这一格，别把整次查找炸了
                    if (full.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    Add(list, full);
                }
            return list;
        }

        static void Add(List<string> list, string cand)
        {
            if (string.IsNullOrEmpty(cand)) return;
            foreach (string have in list) if (string.Equals(have, cand, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(cand);
        }

        // 候选顺序不变，逐个问"用得住吗"(委托可注入：自检喂假尺子，真跑用 PythonUsable)。全不行回空串。
        public static string FindPython(string settingsText, string envGD, string root, string pathVar, Func<string, bool> usable)
        {
            foreach (string c in PythonCandidates(settingsText, envGD, root, pathVar))
                if (usable != null && usable(c)) return c;
            return "";
        }

        // 实测尺子和 PS 的 Test-GdPythonUsable 逐字同一句探针：文件真在 + 退出码 0 + 输出正好是 "3"。
        // 起进程这件事只有窗体会碰，自检不许走到这里(见 Agent.SelfTest.cs 那条纪律)。
        public static bool PythonUsable(string exe)
        {
            try
            {
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.Arguments = "-c \"import sys,json,base64,ssl,urllib.request;print(sys.version_info[0])\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0 && outp.Trim() == "3";
                }
            }
            catch { return false; }
        }

        // ---------- 命令行 ----------
        // .NET Framework 的 ProcessStartInfo 没有 ArgumentList，只能自己拼整串 —— 那就必须按
        // Windows 的 CRT 拆词规矩反向转义(玩家的话里出现引号/反斜杠/空格都是常事，需求管家吃的正是人话)：
        //   一串反斜杠紧跟引号时要翻倍，那个引号本身要前置一个反斜杠；收尾的反斜杠串也要翻倍(不然吃掉闭口引号)。
        // 恒加外层引号：少一个分支就少一种"看着对、拼出来不一样"的地方。
        public static string QuoteArg(string s)
        {
            if (s == null) s = "";
            StringBuilder sb = new StringBuilder();
            sb.Append('"');
            int bs = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\\') { bs++; continue; }
                if (ch == '"') { sb.Append('\\', bs * 2 + 1).Append('"'); bs = 0; continue; }
                sb.Append('\\', bs).Append(ch); bs = 0;
            }
            sb.Append('\\', bs * 2).Append('"');
            return sb.ToString();
        }

        // 第一击。ask 空白时回空串 = "这句别发"(窗体据此不起进程，也不许把空 --ask 递下去)。
        public static string PlanArguments(string ask)
        {
            string text = (ask ?? "").Trim();
            if (text.Length == 0) return "";
            return "-m gdagent agent-plan --ask " + QuoteArg(text);
        }

        // 第二击。代号不过形状闸就回空串 —— 它要拼的是盘上的文件名，来历不明的字符串不当参数递出去。
        public static string RunArguments(string planId)
        {
            if (!PlanIdOk(planId)) return "";
            return "-m gdagent agent-run --plan " + planId.Trim();
        }

        // ---------- 信封 ----------
        // 认"最后一行能当 JSON 对象解析、且带 ok 键"的那一行。为什么取最后一行而不是第一行：
        // 铁律是 stdout 只有一行，但解释器版本提示/第三方库的 print 也可能落 stdout；
        // 拿最后一行既守得住契约，也不会因为前面多印一行就整个小窗失灵。
        public static Dictionary<string, object> ParseEnvelope(string stdout, out string why)
        {
            why = "";
            string last = null;
            if (!string.IsNullOrEmpty(stdout))
                foreach (string raw in stdout.Replace("\r\n", "\n").Split('\n'))
                {
                    string l = raw.Trim();
                    if (l.Length > 1 && l[0] == '{' && l[l.Length - 1] == '}') last = l;
                }
            if (last == null) { why = "子进程没在 stdout 给出一行 JSON 信封(机读通道违约)，面板不猜它做了什么"; return null; }
            try
            {
                Dictionary<string, object> d = GdJson.Deserialize(last) as Dictionary<string, object>;
                if (d == null) { why = "那行 JSON 不是对象，面板读不出信封"; return null; }
                if (!d.ContainsKey("ok")) { why = "那行 JSON 没有 ok 键，不是 agent 的信封"; return null; }
                return d;
            }
            catch (Exception ex) { why = "信封解析失败：" + ex.Message; return null; }
        }

        public static bool IsOk(Dictionary<string, object> env)
        {
            if (env == null) return false;
            object v;
            return env.TryGetValue("ok", out v) && v is bool && (bool)v;
        }

        // 取一个字符串字段：缺键/null 都回空串(面板只转达，不编内容)。
        // JavaScriptSerializer 的数字回读是 Int32/Decimal 混发(怪癖①在 Core.Json.cs 有案)，
        // 所以取数一律走 Convert，裸强转 (string)x 那种写法在这里会炸。
        public static string Str(Dictionary<string, object> env, string key)
        {
            if (env == null) return "";
            object v;
            if (!env.TryGetValue(key, out v) || v == null) return "";
            return v.ToString();
        }

        // 取一个对象数组字段(items / results)：形状不对一律回空列表，不抛。
        public static List<Dictionary<string, object>> Rows(Dictionary<string, object> env, string key)
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            if (env == null) return list;
            object v;
            if (!env.TryGetValue(key, out v)) return list;
            object[] arr = v as object[];
            if (arr == null) return list;
            foreach (object o in arr)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d != null) list.Add(d);
            }
            return list;
        }

        // 退出码：Python 侧给的是 null(JSON null)或整数；"没跑"和"跑了但非 0"要显示得不一样。
        public static string ExitOf(Dictionary<string, object> row)
        {
            if (row == null) return "-";
            object v;
            if (!row.TryGetValue("exit", out v) || v == null) return "-";
            try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture).ToString(); }
            catch { return v.ToString(); }
        }

        static string UnescapeJson(string s)
        {
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
                else sb.Append(n);
            }
            return sb.ToString();
        }
    }
}
