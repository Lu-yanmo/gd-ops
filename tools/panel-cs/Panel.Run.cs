// 这一轮跑没跑：读外部轮次在 state\running.json 留的活体标记(按 PID 判活)，和自己起的子进程输出泵。
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
        // ---------- 外部轮次：跑的那一轮往 state\running.json 留活体标记，面板只读不写 ----------
        // 为什么按 PID 判活，而不是"日志最近有没有新行"：实机量过，MAA 的 Infrast 那一段能连着
        // 10 分 41 秒一行都不写(2026-09-25 18:50:44 Recruit → 19:01:25 Infrast)。
        // 按日志新鲜度判会把"正在跑基建"看成"空闲"，照样放行走二次点火 —— 判据必须挂在进程活没活上。
        //
        // B-3②-a 起这一段的**解析与判活委托给 Round.LockAt**(tools\panel-cs\Round.cs)：写侧新立了
        // start-run-lock/stop-run-lock 两枚 verb，同一把文件的两侧就是同一条契约，读侧不许再留第二份正则
        // (§\rust-core-plan.md §11.50 ⑤-2 那句"不许分两批动"的兑现处；随行自证 = --exttest 那五格一字未改)。
        // 面板这一路只保留它自己那两条规矩：①读不到进程细节时宁可当成还在跑(防二次点火，与 PS 的
        // "残留算没在跑"由 unhandledMeansAlive 这一个布尔分开)；②自己起的那一轮不算"外部"。
        string ExtLockPath { get { return Path.Combine(root, @"state\running.json"); } }

        // 返回 true = 有别的进程正在跑这一轮。pid 已不在的残留标记按"没在跑"处理，
        // 免得一轮崩溃把面板永久锁死；那种残留由写方下一轮起跑时自己覆盖。
        // ⚠ 这里必须写全装配限定名 GdPanel.Round：本窗体自己有一枚 Round(Control,int)(Panel.Widgets.cs:115
        //    的圆角助手)，简单名在那个作用域里按 C# 的成员查找先命中方法，编译期当场报 CS0119。
        bool ReadExtRun(out int pid, out string log, out string started, out string games)
        {
            GdPanel.Round.Lock lk = GdPanel.Round.LockAt(ExtLockPath, true);
            pid = lk.Pid; log = lk.Log; started = lk.Started; games = lk.Games;
            if (!lk.Found || !lk.Alive) return false;
            // 自己起的那一轮走 busy 那条线：它已经有退出码和跟随逻辑，别当成"外部"重复报一次
            if (child != null && !child.HasExited && child.Id == pid) return false;
            return true;
        }

        // ---------- 子进程(和 panel.ps1 同一套：重定向到 TEMP 文件 + 定时器增量读) ----------
        void StartTask(string script, List<string> args, string title, bool tailLog)
        {
            if (busy && child != null && !child.HasExited)
            {
                AppendLog("上一个动作还在跑：等它结束，或点\"开始跑一轮\"那个按钮停掉跟随。");
                return;
            }
            if (busy) CompleteTask();
            if (!File.Exists(script)) { AppendLog("找不到要跑的脚本：" + script); return; }
            if (engine.Length == 0) { AppendLog(NoEngineHint); return; }

            StringBuilder sb = new StringBuilder();
            sb.Append("-NoProfile -ExecutionPolicy Bypass -File \"").Append(script).Append('"');
            foreach (string a in args) sb.Append(' ').Append(a);
            AppendLog("──── " + DateTime.Now.ToString("HH:mm:ss") + "  " + title + " ────");
            AppendLog("  " + Path.GetFileName(engine) + " " + sb);

            outPath = Path.Combine(Path.GetTempPath(), "game-daily-cs-panel-stdout-" + Process.GetCurrentProcess().Id + ".txt");
            errPath = Path.Combine(Path.GetTempPath(), "game-daily-cs-panel-error-" + Process.GetCurrentProcess().Id + ".txt");
            tailPos = 0; tailPath = null;
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = engine;
            psi.Arguments = sb.ToString();
            psi.WorkingDirectory = root;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            try
            {
                child = Process.Start(psi);
                child.OutputDataReceived += delegate (object s, DataReceivedEventArgs e2)
                { if (e2.Data != null) lock (lineLock) childOut.Add(e2.Data); };
                child.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e2)
                { if (e2.Data != null) lock (lineLock) childOut.Add("[stderr] " + e2.Data); };
                child.BeginOutputReadLine();
                child.BeginErrorReadLine();
                busy = true;
                if (tailLog) tailSince = DateTime.Now; else tailSince = DateTime.MinValue;
                SetBusyUi(true);
            }
            catch (Exception ex)
            {
                AppendLog("启动失败：" + ex.Message);
                child = null;
                SetBusyUi(false);
            }
        }

        readonly object lineLock = new object();
        readonly List<string> childOut = new List<string>();

        DateTime lastExtCheck = DateTime.MinValue;

        void OnPumpTick(object sender, EventArgs e)
        {
            lock (lineLock)
            {
                foreach (string l in childOut) AppendLog(l);
                childOut.Clear();
            }
            PumpTail();
            if (busy && child != null && child.HasExited) CompleteTask();
            // 外部轮次 3 秒一查：挡点火这件事要够用，但没到每拍(800ms)都去读盘和查 PID 的程度。
            // 自己那轮在跑时不查 —— 锁文件本来就是我写的，读到的 PID 就是自家子进程。
            if (!busy && (DateTime.Now - lastExtCheck).TotalSeconds >= 3)
            {
                lastExtCheck = DateTime.Now;
                RefreshExternal();
            }
            if (!busy && extBusy) PumpExtTail();
        }

        void PumpTail()
        {
            if (tailSince == DateTime.MinValue) return;
            if (tailPath == null)
            {
                try
                {
                    DirectoryInfo di = new DirectoryInfo(Path.Combine(root, "logs"));
                    if (di.Exists)
                    {
                        FileInfo best = null;
                        foreach (FileInfo f in di.GetFiles("run-*.log"))
                        {
                            if (f.LastWriteTime < tailSince) continue;
                            if (best == null || f.LastWriteTime > best.LastWriteTime) best = f;
                        }
                        if (best != null) { tailPath = best.FullName; tailPos = 0; AppendLog("  跟随日志：" + tailPath); }
                    }
                }
                catch { }
            }
            if (tailPath == null) return;
            foreach (string l in ReadNew(tailPath, ref tailPos)) AppendLog("  | " + l);
        }

        static IEnumerable<string> ReadNew(string path, ref long pos)
        {
            List<string> lines = new List<string>();
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < pos) pos = 0;
                    fs.Seek(pos, SeekOrigin.Begin);
                    byte[] buf = new byte[(int)(fs.Length - pos)];
                    int got = fs.Read(buf, 0, buf.Length);
                    pos = fs.Length;
                    string txt = Encoding.UTF8.GetString(buf, 0, got);
                    foreach (string l in txt.Replace("\r\n", "\n").Split('\n'))
                        if (l.Length > 0) lines.Add(l.TrimEnd('\r'));
                }
            }
            catch { }
            return lines;
        }

        void CompleteTask()
        {
            if (child != null)
            {
                try
                {
                    lastExit = child.ExitCode;
                    AppendLog("退出码 " + lastExit + "(0=正常；「无可刷活动关卡 / 无进行中活动」也是正常结论)");
                }
                catch { AppendLog("子进程已结束(没取到退出码)。"); }
                try { child.Dispose(); } catch { }
                child = null;
            }
            tailSince = DateTime.MinValue; tailPath = null;
            // 自家这轮刚结束：先把外部标记重读一遍再定状态灯，否则"自己那轮"和"外部那轮"
            // 在锁文件被写方删掉的这一瞬间会接错位。
            RefreshExternal();
            SetBusyUi(false);
            // 卡片先重读一次再念原因：这一轮的结论是子进程退出前才写进 history.csv 的，
            // 不重读就会拿"开跑前那批行"去公告，念出来的还是上一轮为什么红。
            RefreshNow("跑完后的自动刷新");
            AnnounceDetails("这一轮跑完了");
        }

        // 状态卡的一次重读(核内取一次摘要 + 分页渲染；刀② 2026-10-23 起这里不再有子进程)。三处收尾都要用，
        // 集中一处写错误处理：读不到时卡片保留上一次的内容，只把原因说清楚。
        bool RefreshNow(string when)
        {
            try { ParseSummary(BuildDumpSummary()); }
            catch (Exception ex)
            {
                AppendLog("[WARN] " + when + "没读到新状态(" + ex.Message + ")，卡片还是上一次的内容。");
                return false;
            }
            RefreshCards();
            return true;
        }

        // 一轮收尾时主动把红/黄行的完整原因念一遍：人最不看日志的时刻恰恰是"跑完了、红了、为什么"那一刻，
        // 而卡片那一行只放得下一个 not-started。同一个状态只念一次，靠 lastAnnounced 去重。
        string lastAnnounced = null;
        void AnnounceDetails(string when)
        {
            StringBuilder key = new StringBuilder();
            for (int i = 0; i < rowsLast.Count; i++)
            {
                Row r = rowsLast[i];
                if (r.Detail.Length == 0) continue;
                key.Append(r.Game).Append(':').Append(r.Kind).Append(':').Append(r.Detail.Length).Append(';');
            }
            if (key.Length == 0) return;
            if (key.ToString() == lastAnnounced) return;
            lastAnnounced = key.ToString();
            AppendLog(when + "，有几路没给好脸色 —— 完整原因(点「上一轮」里那几行随时能再看一次)：");
            for (int i = 0; i < rowsLast.Count; i++)
            {
                Row r = rowsLast[i];
                if (r.Detail.Length == 0) continue;
                AppendLog("  " + GameName(r.Game) + "(" + r.Text + ")：");
                foreach (string l in WrapUnits(r.Detail, 96)) AppendLog("      " + l);
            }
        }

        void SetBusyUi(bool b)
        {
            busy = b;
            UpdateRunSurface();
        }

        // 状态灯三态：自己的子进程 > 外部那一轮 > 空闲。顺序不能反 ——
        // 自己那轮的退出码还没读到手时，宁可显示"正在跑"，也别被外部标记抢过去把退出码吞了。
        void UpdateRunSurface()
        {
            if (busy) { runBtn.Text = "正在跑…(点这个按钮停跟随)"; SetChip("正在跑", true); return; }
            if (extBusy) { runBtn.Text = "外部有一轮在跑(再点=停掉它)"; SetChip("外部在跑", true); return; }
            // 空闲态也要看有没有在等二次确认：外部标记每 3 秒查一次，一刷就把
            // "再点一次就真的开跑"冲回默认文案的话，人根本看不见确认提示、只会以为按钮坏了。
            if (confirmAt == DateTime.MinValue || (DateTime.Now - confirmAt).TotalSeconds >= 12) runBtn.Text = idleRunText;
            SetChip("空闲", false);
        }

        // 单游戏仓直接藏掉它(只有一路时它和"开始跑一轮"是同一件事，多一个按钮只会让人猜差别在哪)。
        void UpdateOnlyBtn()
        {
            if (btnOnly == null) return;
            bool many = flagsGames && games.Count > 1;
            btnOnly.Visible = many;
            if (!many || busy) return;
            if (page == "all" || page.Length == 0) { btnOnly.Text = "只跑这一路(先选一路)"; return; }
            if (onlyAt != DateTime.MinValue && (DateTime.Now - onlyAt).TotalSeconds < 12) return;   // 等确认时别冲掉"再点一次="
            btnOnly.Text = "只跑" + GameName(page);
        }

        // 每拍打一次"有没有别处在跑"。它刻意不走 DumpSummary 那条线：读一个 json + 查一个 PID，
        // 不起 pwsh 子进程，所以能按 pump 的节奏跑，撞车提示才不会慢半拍。
        bool RefreshExternal()
        {
            int pid; string log, started, g;
            bool now = ReadExtRun(out pid, out log, out started, out g);
            List<string> bits = new List<string>();
            if (now)
            {
                if (pid > 0) bits.Add("PID " + pid);
                if (g.Length > 0) bits.Add(g);
                if (started.Length > 0) bits.Add("起于 " + started);
            }
            string note = now ? string.Join(" · ", bits.ToArray()) : "";
            bool changed = (now != extBusy) || (note != extNote);
            extBusy = now;
            extNote = note;
            string newLog = (now && log.Length > 0 && File.Exists(log)) ? log : null;
            if (newLog != extLog) { extLog = newLog; extPos = 0; }
            if (changed)
            {
                UpdateRunSurface();
                if (now) AppendLog("检测到外部有一轮在跑(" + note + ") —— 面板不再接受点火，避免两轮抢同一个助手/端游。");
                else AppendLog("外部那一轮已经不在跑了。");
            }
            return changed;
        }

        void PumpExtTail()
        {
            if (!extBusy || extLog == null) return;
            foreach (string l in ReadNew(extLog, ref extPos)) AppendLog("  | " + l);
        }

        void OnBeatTick(object sender, EventArgs e)
        {
            RefreshExternal();
            // 自己那轮还在跑时不去抢：DumpSummary 会读同一批状态文件，而且此刻刷出来的卡片必然是过时的
            if (busy) return;
            RefreshNow("自动刷新");
        }

        // 停掉外部那一轮：只有走计划任务那条路才停得干净 —— 面板是普通完整性，
        // 直接 Kill 提权进程会被拒；任务自己的 Stop-ScheduledTask 才会连它派生的进程树一起收。
        void StopExternal()
        {
            string script = Path.Combine(root, "run-via-task.ps1");
            if (!File.Exists(script)) { AppendLog("没有 run-via-task.ps1，面板停不了外部那一轮 —— 去任务计划程序里结束它。"); return; }
            StartTask(script, new List<string> { "-Stop" }, "停掉正在跑的这一轮(走计划任务)", false);
        }

        string chipText = "空闲";
        void SetChip(string text, bool isBusy)
        {
            chipText = text;
            runChip.Text = text;
            runChip.BackColor = isBusy ? th.BusyBack : th.ChipBack;
            runChip.ForeColor = isBusy ? th.BusyFore : th.ChipFore;
        }

        void StopChild()
        {
            if (child != null)
            {
                try { if (!child.HasExited) child.Kill(); } catch { }
                try { child.Dispose(); } catch { }
                child = null;
            }
            tailSince = DateTime.MinValue; tailPath = null;
            SetBusyUi(false);
        }

    }
}