// tools/panel-cs/Agent.SelfTest.cs —— --agenttest：对话小窗那条链里"会算错就得背锅"的三段纯函数。
//
// 纪律(和批次 0 起同一套)：
//   * 断言计入退出码；
//   * **不起进程、不读真配置、不要求这台机器装了 Python** —— 找解释器喂的是假尺子和假 PATH，
//     命令行拼出来的串是金标准字面量，信封喂的是手写 JSON。真接通由人工在窗子上问一句验(见 kb-plan §7)。
//   * 期望串是**手算的 CRT 拆词结果**，不是"跑一遍实现把它现在的输出抄下来"—— 后者只会把 bug 钉成基线。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GdPanel
{
    static class GdAgentSelfTest
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

        // 期望串的两个零件：QQ = 一个引号字符，Bs(n) = n 个反斜杠。
        // 命令行转义这种东西，用字面量硬写等于"把 bug 的形状抄进基线"；拆成零件后每条断言都在数概念。
        const string QQ = "\"";
        static string Bs(int n) { return new string('\\', n); }

        public static int AgentTest()
        {
            Box b = new Box();
            Console.WriteLine("---- panel-cs --agenttest(对话小窗的纯规则段，不起进程/不联网) ----");

            // ---------- 1) 计划代号：它要拼成盘上的文件名，形状不合一律拒 ----------
            b.Chk("正常代号过形状闸", delegate { return GdAgentLink.PlanIdOk("20260929-221254-4cf4"); });
            b.Chk("带空格/引号/路径穿越的都不算代号", delegate
            {
                return !GdAgentLink.PlanIdOk("20260929-221254-4cf4 --plan x") &&
                       !GdAgentLink.PlanIdOk("a\"b") &&
                       !GdAgentLink.PlanIdOk("../../etc/passwd") &&
                       !GdAgentLink.PlanIdOk("") &&
                       !GdAgentLink.PlanIdOk(null);
            });
            b.Chk("大写十六进制不认(Python 侧的盐是小写 4 位)", delegate { return !GdAgentLink.PlanIdOk("20260929-221254-4CF4"); });
            b.Chk("代号不合法时第二击的命令行拼不出来(调用方据此不跑)", delegate { return GdAgentLink.RunArguments("../x").Length == 0; });

            // ---------- 2) CRT 拆词的反面：拼出来的整串要能被 python 逐字还原成原话 ----------
            // 期望串一律用 QQ/Bs 拼出来写，不用 `@"..."` 里塞反斜杠+引号那种"看着像、数错一位就假绿"的字面量。
            b.Chk("普通中文：只加外层引号", delegate
            {
                return GdAgentLink.QuoteArg("周末只刷 CE-5") == QQ + "周末只刷 CE-5" + QQ;
            });
            b.Chk("串里的反斜杠不翻倍(没紧跟引号就是字面量)", delegate
            {
                return GdAgentLink.QuoteArg(@"C:\Tools\py") == QQ + @"C:\Tools\py" + QQ;
            });
            b.Chk("反斜杠紧跟引号：那串要翻倍、引号要前置一个反斜杠(a\\\" 出成 a\\\\\\\")", delegate
            {
                string input = "a" + Bs(1) + QQ;                       // a \ "
                string expect = QQ + "a" + Bs(3) + QQ + QQ;            // " a \ \ \ " 然后闭口
                return GdAgentLink.QuoteArg(input) == expect;
            });
            b.Chk("收尾的反斜杠翻倍(不然吃掉闭口引号)", delegate
            {
                return GdAgentLink.QuoteArg(@"a\") == QQ + "a" + Bs(2) + QQ;
            });
            b.Chk("裸引号前置一个反斜杠", delegate
            {
                string expect = QQ + "他说" + Bs(1) + QQ + "停" + Bs(1) + QQ + QQ;
                return GdAgentLink.QuoteArg("他说" + QQ + "停" + QQ) == expect;
            });
            b.Chk("空串拼成一对手引号，不是空(空参数会让 argparse 报缺值)", delegate
            {
                return GdAgentLink.QuoteArg("") == QQ + QQ;
            });
            b.Chk("null 一样当空串处理", delegate { return GdAgentLink.QuoteArg(null) == QQ + QQ; });
            // 反向验：按 Windows CRT 的拆词规则把拼出来的整串再拆回参数，必须逐字等于原话。
            // 这个复刻只为自检存在(见文件末 Argv)，真拆词的是 python 那边的 libc —— 两侧规则同源，
            // 所以它和上面那几条手算期望合起来看才算数：单靠往返会把"两边同错"验成绿。
            b.Chk("往返：转义后再拆回原话(纯中文)", delegate { return Back("周末只刷 CE-5，别碰活动图"); });
            b.Chk("往返：含引号(玩家真的会打引号，比如关卡名 CE-\"5\")", delegate { return Back("CE-\"5\" 与 \"活动\""); });
            b.Chk("往返：含反斜杠路径 + 结尾反斜杠", delegate { return Back(@"D:\Tools\MAA\ 与 C:\x\"); });
            b.Chk("往返：反斜杠紧跟引号(最难的一位)", delegate { return Back("a\\\"b\" c\\\\\""); });
            b.Chk("往返：制表符和多余空格不被拆丢", delegate { return Back("  两\t个  词  "); });

            // ---------- 3) 两条机读口的命令行 ----------
            b.Chk("第一击：-m gdagent agent-plan --ask <引号包住的原话>", delegate
            {
                return GdAgentLink.PlanArguments("  刷 10 局 CE-5  ") == "-m gdagent agent-plan --ask \"刷 10 局 CE-5\"";
            });
            b.Chk("空话不递(回空串 = 这句别发)", delegate
            {
                return GdAgentLink.PlanArguments("").Length == 0 && GdAgentLink.PlanArguments("   ").Length == 0;
            });
            b.Chk("第二击：代号原样上命令行，不再套引号(它已过形状闸)", delegate
            {
                return GdAgentLink.RunArguments("20260929-221254-4cf4") == "-m gdagent agent-run --plan 20260929-221254-4cf4";
            });

            // ---------- 4) 找解释器：顺序照 lib\common.ps1 的 Get-GdPythonExe ----------
            string settings = "{ \"runtime\": { \"pythonExe\": \"D:\\\\Tools\\\\py\\\\python.exe\" } }";
            b.Chk("settings 的 runtime.pythonExe 取出来是解过转义的绝对路径", delegate
            {
                return GdAgentLink.RuntimePythonExe(settings) == @"D:\Tools\py\python.exe";
            });
            b.Chk("settings 没这一键时回空串(不猜)", delegate { return GdAgentLink.RuntimePythonExe("{\"runtime\":{}}") == ""; });
            b.Chk("顺序：settings 指定 > 本仓库下的 > PATH(GD_PYTHON 只在 settings 空时才进队)", delegate
            {
                List<string> c = GdAgentLink.PythonCandidates(settings, @"E:\env\python.exe", @"R:\game", @"C:\Windows;D:\bin");
                return c[0] == @"D:\Tools\py\python.exe" &&
                       c[1] == Path.Combine(@"R:\game", @"state\python\python.exe") &&
                       c[2] == @"C:\Windows\python.exe" &&
                       c[c.Count - 1] == @"D:\bin\py.exe" &&
                       !c.Contains(@"E:\env\python.exe");
            });
            b.Chk("settings 没指定时 GD_PYTHON 补第一格", delegate
            {
                List<string> c = GdAgentLink.PythonCandidates("", @" E:\env\python.exe ", "", "");
                return c[0] == @"E:\env\python.exe" && c.Count == 1;
            });
            b.Chk("PATH 先 python.exe 后 py.exe，WindowsApps 那个占位程序连试都不试", delegate
            {
                // 那条假 PATH 刻意不写成真实的用户目录形状：判据只看 `\WindowsApps\` 这一段，
                // 而仓库里除这一个字面量外没有第二处出现那个前缀 —— 别为了举例给自己埋一次泄漏扫描误报。
                List<string> c = GdAgentLink.PythonCandidates("", "", "",
                    @"C:\Windows;D:\AppData\Local\Microsoft\WindowsApps;E:\bin");
                return c[0] == @"C:\Windows\python.exe" && c[1] == @"E:\bin\python.exe" &&
                       c[2] == @"C:\Windows\py.exe" && c[3] == @"E:\bin\py.exe" &&
                       c.Count == 4;
            });
            b.Chk("同一格重复出现只留一条(大小写不算重复两次机会)", delegate
            {
                List<string> c = GdAgentLink.PythonCandidates("", "", @"r", @"D:\bin;d:\BIN");
                int n = 0;
                foreach (string s in c) if (string.Equals(s, @"D:\bin\python.exe", StringComparison.OrdinalIgnoreCase)) n++;
                return n == 1;
            });
            b.Chk("FindPython 按顺序问尺子，第一个说\"用得住\"的就定", delegate
            {
                List<string> asked = new List<string>();
                Func<string, bool> usable = delegate(string e)
                {
                    asked.Add(e);
                    return e.EndsWith(@"D:\bin\python.exe");
                };
                string got = GdAgentLink.FindPython("", "", "", @"C:\Windows;D:\bin", usable);
                return got == @"D:\bin\python.exe" && asked.Count == 2 && asked[0] == @"C:\Windows\python.exe";
            });
            b.Chk("全不可用时回空串(调用方负责说人话，窗子不许拿空串当解释器起进程)", delegate
            {
                return GdAgentLink.FindPython("", "", "", @"C:\Windows", delegate(string e) { return false; }).Length == 0;
            });

            // ---------- 5) 信封：stdout 那一行 JSON 是唯一事实 ----------
            b.Chk("一行信封照单全收(中文是 \\uXXXX，解出来要是人话)", delegate
            {
                string why;
                Dictionary<string, object> env = GdAgentLink.ParseEnvelope(
                    "{\"ok\": true, \"why\": \"\", \"reply\": \"\\u7ba1\\u5bb6\\u597d\", \"plan_id\": \"20260929-221254-4cf4\"}", out why);
                return GdAgentLink.IsOk(env) && GdAgentLink.Str(env, "reply") == "管家好" &&
                       GdAgentLink.Str(env, "plan_id") == "20260929-221254-4cf4" && why == "";
            });
            b.Chk("前面混进别的行也认最后一行信封(契约是只有一行，但失灵时不该整窗变白)", delegate
            {
                string why;
                Dictionary<string, object> env = GdAgentLink.ParseEnvelope(
                    "一些噪音\n{\"ok\": false, \"why\": \"没给 --ask\"}\n", out why);
                return !GdAgentLink.IsOk(env) && GdAgentLink.Str(env, "why") == "没给 --ask";
            });
            b.Chk("stdout 没信封时报的是\"机读通道违约\"，不是空回复", delegate
            {
                string why;
                Dictionary<string, object> env = GdAgentLink.ParseEnvelope("", out why);
                return env == null && why.Contains("机读通道违约");
            });
            b.Chk("那行不是对象 / 没有 ok 键 = 不是 agent 的信封，两种都要说出来", delegate
            {
                string why;
                GdAgentLink.ParseEnvelope("[1,2]", out why);
                string w1 = why;
                GdAgentLink.ParseEnvelope("{\"no\": 1}", out why);
                return w1.Length > 0 && why.Contains("ok");
            });
            b.Chk("items 逐条投影出 desc 和 args", delegate
            {
                string why;
                Dictionary<string, object> env = GdAgentLink.ParseEnvelope(
                    "{\"ok\": true, \"items\": [{\"desc\": \"真跑方舟\", \"args\": [\"-ArkShopDone\", \"-Games\", \"ark\"]}], \"plan_id\": \"x\"}", out why);
                List<Dictionary<string, object>> rows = GdAgentLink.Rows(env, "items");
                return rows.Count == 1 && GdAgentLink.Str(rows[0], "desc") == "真跑方舟";
            });
            b.Chk("缺键/形状不对一律回空列表，不抛(面板只转达，不替子进程编内容)", delegate
            {
                string why;
                Dictionary<string, object> env = GdAgentLink.ParseEnvelope("{\"ok\": true, \"items\": 3}", out why);
                return GdAgentLink.Rows(env, "items").Count == 0 && GdAgentLink.Rows(env, "nothere").Count == 0 &&
                       GdAgentLink.Str(env, "reply") == "" && GdAgentLink.Rows(null, "items").Count == 0;
            });
            b.Chk("退出码：JSON null 显示成\"-\"，整数按整数显示(JSS 的 int/Decimal 混发在这儿被抹平)", delegate
            {
                string why;
                Dictionary<string, object> env = GdAgentLink.ParseEnvelope(
                    "{\"ok\": true, \"results\": [{\"exit\": null}, {\"exit\": 0}, {\"exit\": 2}]}", out why);
                List<Dictionary<string, object>> rows = GdAgentLink.Rows(env, "results");
                return rows.Count == 3 && GdAgentLink.ExitOf(rows[0]) == "-" &&
                       GdAgentLink.ExitOf(rows[1]) == "0" && GdAgentLink.ExitOf(rows[2]) == "2";
            });

            return b.Result("--agenttest");
        }

        // ---------- Windows CRT 拆词的只读复刻 ----------
        // 只为上面那几条"往返"断言存在：真拆词的是 python 那边链接的 MSVC CRT，规则同源 ——
        //   引号外的空格/制表符分参；一串反斜杠只在**紧跟引号**时特殊：2k 个 + 引号 => k 个字面反斜杠 + 引号改状态，
        //   2k+1 个 => k 个字面反斜杠 + 一个字面引号(状态不变)；不跟引号的反斜杠全是字面量。
        // 它是 QuoteArg 的反面：QuoteArg 把"人话 → 整串"，这里把"整串 → argv"。
        static List<string> Argv(string commandLine)
        {
            List<string> args = new List<string>();
            int i = 0;
            while (i < commandLine.Length)
            {
                while (i < commandLine.Length && (commandLine[i] == ' ' || commandLine[i] == '\t')) i++;
                if (i >= commandLine.Length) break;
                StringBuilder cur = new StringBuilder();
                bool inQuote = false;
                while (i < commandLine.Length)
                {
                    char c = commandLine[i];
                    if (c == '\\')
                    {
                        int n = 0;
                        while (i + n < commandLine.Length && commandLine[i + n] == '\\') n++;
                        if (i + n < commandLine.Length && commandLine[i + n] == '"')
                        {
                            cur.Append('\\', n / 2);
                            if (n % 2 == 1) cur.Append('"');
                            else inQuote = !inQuote;
                            i += n + 1;
                        }
                        else { cur.Append('\\', n); i += n; }
                        continue;
                    }
                    if (c == '"') { inQuote = !inQuote; i++; continue; }
                    if ((c == ' ' || c == '\t') && !inQuote) break;
                    cur.Append(c);
                    i++;
                }
                args.Add(cur.ToString());
            }
            return args;
        }

        // 一句话单独成参时，转义 → 拆词必须逐字回到原话，而且不许被拆成两段(拆两段 argparse 就收到别的东西了)。
        static bool Back(string s)
        {
            List<string> argv = Argv(GdAgentLink.QuoteArg(s));
            return argv.Count == 1 && argv[0] == s;
        }
    }
}
