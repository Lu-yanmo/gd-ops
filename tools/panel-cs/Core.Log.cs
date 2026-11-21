// tools/panel-cs/Core.Log.cs —— 批次 0 的 Core 层之三：日志行格式 + 可选落盘。
// 格式和 Write-GdLog 逐字节一致：控制台/管道里是 "[HH:mm:ss][LEVEL] 消息"，
// 落盘那行前面再多带一个 "[yyyy-MM-dd HH:mm:ss]" —— 面板日志区和人肉 grep 都按这两层读，改一个字符都算协议变更。

using System;
using System.IO;
using System.Text;

namespace GdPanel
{
    static class GdLog
    {
        public static string LogFile;      // 空 = 不落盘(同 PS 的 $global:GD.LogFile)
        public static bool ToConsole = true;
        static readonly object gate = new object();

        public static void Line(string msg) { Line(msg, "INFO"); }

        public static void Line(string msg, string level)
        {
            lock (gate)
            {
                DateTime now = DateTime.Now;
                string line = "[" + now.ToString("HH:mm:ss") + "][" + level + "] " + msg;
                if (!string.IsNullOrEmpty(LogFile))
                {
                    try
                    {
                        File.AppendAllText(LogFile,
                            "[" + now.ToString("yyyy-MM-dd HH:mm:ss") + "]" + line + Environment.NewLine,
                            new UTF8Encoding(false));
                    }
                    catch { }
                }
                if (!ToConsole) return;
                int prev = -1;
                try
                {
                    ConsoleColor c = ConsoleColor.Gray;
                    if (level == "ERROR") c = ConsoleColor.Red;
                    else if (level == "WARN") c = ConsoleColor.Yellow;
                    else if (level == "OK") c = ConsoleColor.Green;
                    else if (level == "STEP") c = ConsoleColor.Cyan;
                    if (level != "INFO") { prev = (int)Console.ForegroundColor; Console.ForegroundColor = c; }
                }
                catch { prev = -1; }
                Console.WriteLine(line);
                if (prev >= 0) { try { Console.ForegroundColor = (ConsoleColor)prev; } catch { } }
            }
        }

        // Start-GdTranscript 的同型款：verb 调用默认**不**落盘 —— 转发壳(PS)自己的 transcript 已经把
        // 子进程 stdout 一起收进 run-*.log 里了，C# 再落一份就是双份日志；要独立留档时传 --transcript 显式开。
        public static void StartCli(string verb)
        {
            GdEnv.Init(null);
            LogFile = Path.Combine(GdEnv.Logs, "cli-" + verb + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            Line("日志文件：" + LogFile);
        }
    }
}
