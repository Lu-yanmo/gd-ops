// 日志区：逐行按 Write-GdLog 的级别上色，判据和 panel.ps1 用同一条正则。
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
        // ---------- 日志区(逐行按 Write-GdLog 的级别上色；判据和 panel.ps1 的同一条正则) ----------
        readonly List<string[]> logLines = new List<string[]>();   // [kind, text]，换主题时整框重画

        void AppendLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            logLines.Add(new[] { LogKind(text), text });
            if (logLines.Count > 2000) logLines.RemoveRange(0, logLines.Count - 1500);
            AppendColored(text, LogKind(text));
        }

        void AppendColored(string text, string kind)
        {
            Match m = Regex.Match(text, @"^(?:  \| )?(?:(?:\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]|\[\d{2}:\d{2}:\d{2}\])+)(?:\[[A-Z]+\])? (?<rest>.*)$");
            Color stamp = th.LogStamp;
            Color body = LogColor(kind);
            SuspendLayoutLog();
            if (m.Success)
            {
                AddText(text.Substring(0, text.Length - m.Groups["rest"].Length), stamp);
                AddText(m.Groups["rest"].Value, body);
            }
            else AddText(text, body);
            AddText("\n", body);
            ResumeLayoutLog();
        }

        void RepaintLog()
        {
            logBox.Clear();
            foreach (string[] e in logLines) AddRaw(e[1], e[0]);
            ScrollEnd();
        }

        void AddRaw(string text, string kind)
        {
            Match m = Regex.Match(text, @"^(?:  \| )?(?:(?:\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]|\[\d{2}:\d{2}:\d{2}\])+)(?:\[[A-Z]+\])? (?<rest>.*)$");
            SuspendLayoutLog();
            if (m.Success)
            {
                AddText(text.Substring(0, text.Length - m.Groups["rest"].Length), th.LogStamp);
                AddText(m.Groups["rest"].Value, LogColor(kind));
            }
            else AddText(text, LogColor(kind));
            AddText("\n", LogColor(kind));
            ResumeLayoutLog();
        }

        void SuspendLayoutLog() { logBox.SuspendLayout(); }
        void ResumeLayoutLog() { logBox.ResumeLayout(); ScrollEnd(); }
        void ScrollEnd() { try { logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret(); } catch { } }

        void AddText(string text, Color c)
        {
            if (text.Length == 0) return;
            logBox.Select(logBox.TextLength, 0);
            logBox.SelectionColor = c;
            logBox.AppendText(text);
        }

        Color LogColor(string kind)
        {
            switch (kind)
            {
                case "ok": return th.LogOk;
                case "warn": return th.LogWarn;
                case "err": return th.LogErr;
                case "step": return th.LogStep;
                case "dim": return th.LogDim;
                default: return th.LogInfo;
            }
        }

        static string LogKind(string text)
        {
            if (text.StartsWith("[stderr")) return "err";
            if (Regex.IsMatch(text, @"^\s*[=─]{4,}")) return "dim";
            if (text.StartsWith("────")) return "dim";
            if (text.Contains("[ERROR]")) return "err";
            if (text.Contains("[WARN]")) return "warn";
            if (text.Contains("[OK]")) return "ok";
            if (text.Contains("[STEP]")) return "step";
            if (Regex.IsMatch(text, @"^退出码 0(\(|$)")) return "ok";
            if (Regex.IsMatch(text, @"^退出码 [1-9]")) return "err";
            return "info";
        }

    }
}