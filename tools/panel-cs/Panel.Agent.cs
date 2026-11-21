// tools/panel-cs/Panel.Agent.cs —— 「问需求管家」独立对话小窗(第二笔：GUI 接线)。
//
// 这个窗子只做四件事：把人话递给 tools\gdagent 的机读口、把信封里的人话和计划原样画出来、
// 等玩家当面点「确认执行」、把执行结果如实报回来。它**不算任何结论**：
//   哪一步能真跑 = Python 侧的白名单闸(engine.check_verb_args)说的；
//   要跑什么 = 第一击落在 state\agent\plans\ 里的那份计划；
//   跑没跑成 = 第二击信封里逐条的 status/exit。
// 两击之间不重问模型(模型第二轮再答一遍是不确定的)，所以确认按钮认的是**代号**，不是屏上那几行字。
//
// 为什么不阻塞：一次问答要过模型(几秒到几十秒)、第二击会真起 MAA/M9A(几分钟)。
// 沿用主面板那套"子进程事件 → 缓冲区 → UI 定时器泵"的做法，UI 线程一次 WaitForExit 都不做。
//
// 只在主面板开着的时候存在(Owner 是主面板)；换主题由主面板调 ApplyThemeNow 通知它。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace GdPanel
{
    sealed class AgentChatForm : Form
    {
        const int W0 = 700, H0 = 560;
        const int X0 = 14, CW = W0 - 28;      // 内容区左右各留 14

        readonly string root;
        readonly Func<bool> otherBusyFn;      // 主面板给"这台机器此刻有没有一轮在跑"的判据，窗子不自创
        readonly Action<string> noteFn;       // 有事要进主面板日志时说给它(小窗关了也要看得见)
        Theme th;

        readonly Panel hdr = new Panel();
        readonly Label title = new Label();
        readonly Label sub = new Label();
        readonly Button btnClose = new Button();
        readonly RichTextBox box = new RichTextBox();
        readonly TextBox input = new TextBox();
        Button askBtn, runBtn, dropBtn, cancelBtn;
        readonly Label status = new Label();
        readonly ToolTip tip = new ToolTip();
        readonly System.Windows.Forms.Timer pump = new System.Windows.Forms.Timer();

        // 子进程三样：stdout 拼整串等信封、stderr 只拿来打"在做什么"的那一行、退出码只作旁证
        Process child;
        readonly object bufLock = new object();
        readonly StringBuilder outBuf = new StringBuilder();
        readonly List<string> errTail = new List<string>();
        DateTime since = DateTime.MinValue;
        string pendingId = "";        // 第一击回的计划代号；空 = 没有等确认的东西
        DateTime closeAskAt = DateTime.MinValue;

        public AgentChatForm(string rootDir, Theme theme, Func<bool> otherBusy, Action<string> note)
        {
            root = rootDir;
            th = theme;
            otherBusyFn = otherBusy;
            noteFn = note;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(W0, H0);
            MinimumSize = new Size(560, 420);
            Font = new Font("Microsoft YaHei UI", 9.5f);
            ShowInTaskbar = false;
            try { Icon = LoadIconFrom(root); } catch { }
            BuildUi();
            ApplyThemeNow(th);
            Say("dim", "要什么直接说，比如「周末只刷 CE-5，别碰活动图」。");
            Say("dim", "真跑的那几步不会自己动手：先把要做的那几行列给你看，你点「确认执行」才跑。");

            pump.Interval = 250;
            pump.Tick += OnPumpTick;
            pump.Start();
        }

        void BuildUi()
        {
            SuspendLayout();

            hdr.Location = new Point(0, 0);
            hdr.Size = new Size(W0, 52);
            hdr.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            hdr.MouseDown += BeginDrag;
            Controls.Add(hdr);
            hdr.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen pen = new Pen(th.Line)) pe.Graphics.DrawLine(pen, 0, hdr.Height - 1, hdr.Width - 1, hdr.Height - 1);
            };

            title.Text = "问需求管家";
            title.Location = new Point(X0, 8);
            title.AutoSize = true;
            title.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            hdr.Controls.Add(title);

            sub.Text = "两击确认 · 第一击只出计划，真跑要你再点一次";
            sub.Location = new Point(X0 + 118, 15);
            sub.AutoSize = true;
            sub.Font = new Font("Microsoft YaHei UI", 8.5f);
            hdr.Controls.Add(sub);

            btnClose.Text = "×";
            btnClose.Location = new Point(W0 - 32, 0);
            btnClose.Size = new Size(32, 24);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Font = new Font("Microsoft YaHei UI", 10f);
            btnClose.TabStop = false;
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Click += delegate { RequestClose(); };
            hdr.Controls.Add(btnClose);

            box.Location = new Point(X0, 62);
            box.Size = new Size(CW, 372);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            box.BorderStyle = BorderStyle.None;
            box.ReadOnly = true;
            box.DetectUrls = false;
            box.Font = new Font("Consolas", 9.5f);
            Controls.Add(box);

            input.Location = new Point(X0, 444);
            input.Size = new Size(CW - 118, 26);
            input.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(input);

            askBtn = Chip("问一句", W0 - X0 - 104, 442, 104, 30, true);
            askBtn.Click += OnAskClick;
            runBtn = Chip("确认执行(真跑)", X0, 480, 156, 30, false);
            runBtn.Click += OnRunClick;
            dropBtn = Chip("不要这份", X0 + 164, 480, 104, 30, false);
            dropBtn.Click += OnDropClick;
            cancelBtn = Chip("取消这一问", X0 + 276, 480, 104, 30, false);
            cancelBtn.Click += OnCancelClick;
            // 开局只有「问一句」能点：确认按钮认的是第一击回来的代号，没有计划就没有可确认的东西(按钮不是装饰)
            runBtn.Enabled = false;
            dropBtn.Enabled = false;
            cancelBtn.Enabled = false;

            status.Location = new Point(X0 + 388, 486);
            status.Size = new Size(CW - 388, 18);
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            status.TextAlign = ContentAlignment.TopRight;
            status.Font = new Font("Microsoft YaHei UI", 8.5f);
            status.Text = "空闲";
            Controls.Add(status);

            tip.SetToolTip(askBtn, "把这句话递给 tools\\gdagent 的机读口(agent-plan)：预览、查表这类不改东西的动作当场就办，" +
                                  "要真跑一轮的那几步只列计划、一次都不执行。");
            tip.SetToolTip(runBtn, "第二击：按第一击落在 state\\agent\\plans\\ 的那份代号取回计划，逐条重新过白名单闸再跑。" +
                                   "屏上显示什么，跑的就是什么；计划里没有的一步都不多做。");
            tip.SetToolTip(dropBtn, "作废这份计划(文件仍留在盘上，窗子不替你删)。作废后才能再问下一句。");
            tip.SetToolTip(cancelBtn, "结束等待。已经在跑的那一步不会被这扇窗硬杀 —— 它要么还没开始，要么让它自己跑完。");
            input.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OnAskClick(s, EventArgs.Empty); }
            };

            ResumeLayout(true);
        }

        Button Chip(string text, int x, int y, int w, int h, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.Anchor = primary ? (AnchorStyles.Bottom | AnchorStyles.Right) : (AnchorStyles.Bottom | AnchorStyles.Left);
            b.Tag = primary ? "primary" : "chip";
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.Font = primary ? new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold) : new Font("Microsoft YaHei UI", 9f);
            Controls.Add(b);
            return b;
        }

        void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        // ---------- 主题：只用传进来的那套黑白值，一色都不自创 ----------
        public void ApplyThemeNow(Theme t)
        {
            th = t;
            BackColor = t.Window;
            hdr.BackColor = t.Rail;
            title.ForeColor = t.Text; title.BackColor = t.Rail;
            sub.ForeColor = t.NavSub; sub.BackColor = t.Rail;
            btnClose.ForeColor = t.Text; btnClose.BackColor = t.Rail;
            btnClose.FlatAppearance.MouseOverBackColor = t.Hover;
            btnClose.FlatAppearance.MouseDownBackColor = t.Down;
            box.BackColor = t.LogBack;
            input.BackColor = t.Name == "dark" ? t.NavOn : t.Window;
            input.ForeColor = t.Text;
            status.ForeColor = t.Dim; status.BackColor = t.Window;
            PaintButtons(t);
            Repaint();
        }

        void PaintButtons(Theme t)
        {
            PaintOne(askBtn, t); PaintOne(runBtn, t); PaintOne(dropBtn, t); PaintOne(cancelBtn, t);
        }

        void PaintOne(Button b, Theme t)
        {
            if (b == null) return;
            // Tag 是 object：先当字符串取再比，别写 (object)"primary" == b.Tag —— 那是引用比较，看着对其实没比内容
            string tag = b.Tag as string;
            bool primary = tag == "primary";
            b.ForeColor = primary ? t.PrimaryFore : t.Text;
            b.BackColor = primary ? t.Accent : t.ChipBack;
            b.FlatAppearance.BorderColor = t.Line;
            b.FlatAppearance.MouseOverBackColor = primary
                ? (t.Name == "dark" ? ControlPaint.Light(t.Accent, 0.6f) : ControlPaint.Dark(t.Accent, 0.15f))
                : ControlPaint.Light(t.ChipBack, 0.15f);
            b.FlatAppearance.MouseDownBackColor = primary
                ? (t.Name == "dark" ? ControlPaint.Dark(t.Accent, 0.15f) : ControlPaint.Light(t.Accent, 0.6f))
                : ControlPaint.Dark(t.ChipBack, 0.25f);
        }

        // ---------- 对话区：按角色上色(黑白双档里只有 正文/说明/成/败 四种脸色，全从主题取) ----------
        readonly List<string[]> lines = new List<string[]>();   // [kind, text]，换主题时整框重画

        void Say(string kind, string text)
        {
            if (text == null) return;
            foreach (string l in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (l.Length == 0) continue;
                lines.Add(new[] { kind, l });
                if (lines.Count > 4000) lines.RemoveRange(0, lines.Count - 3000);
                AddColored(l, kind);
            }
        }

        void AddColored(string text, string kind)
        {
            box.SuspendLayout();
            box.Select(box.TextLength, 0);
            box.SelectionColor = ColorFor(kind);
            box.AppendText(text + "\n");
            box.ResumeLayout();
            try { box.SelectionStart = box.TextLength; box.ScrollToCaret(); } catch { }
        }

        void Repaint()
        {
            box.Clear();
            foreach (string[] e in lines) AddColored(e[1], e[0]);
        }

        Color ColorFor(string kind)
        {
            switch (kind)
            {
                case "me": return th.LogStep;
                case "dim": return th.LogDim;
                case "ok": return th.LogOk;
                case "warn": return th.LogWarn;
                case "err": return th.LogErr;
                default: return th.LogInfo;
            }
        }

        // ---------- 第一击：问一句 ----------
        void OnAskClick(object sender, EventArgs e)
        {
            if (busy) { Say("warn", "上一问还在跑，先等它(或点「取消这一问」)。"); return; }
            if (pendingId.Length > 0)
            {
                Say("warn", "上一份计划还挂在「确认执行」上(代号 " + pendingId + ")。");
                Say("warn", "要么点「确认执行」把它办了，要么点「不要这份」作废，再问下一句 —— 两个都堵着的话，屏上就可能出现两份计划，" +
                            "而第二击认的只有一份。");
                return;
            }
            string args = GdAgentLink.PlanArguments(input.Text);
            if (args.Length == 0) { Say("warn", "这句是空的，不递过去。"); return; }
            Say("me", "我：" + input.Text.Trim());
            StartChild(args, "问一句(第一击：只出计划)");
            input.Clear();
        }

        // ---------- 第二击：确认执行那份计划 ----------
        void OnRunClick(object sender, EventArgs e)
        {
            if (busy) { Say("warn", "正在等一件事回来，先不要点火第二件。"); return; }
            if (pendingId.Length == 0) { Say("warn", "现在没有等确认的计划 —— 先问一句，让它把要跑的那几步列出来。"); return; }
            if (GdAgentLink.RunArguments(pendingId).Length == 0)
            {
                Say("err", "这个代号不成形状，不拼进命令行：" + pendingId + "(要的是 20260929-221254-4cf4 这种)。");
                return;
            }
            if (otherBusyFn != null && otherBusyFn())
            {
                Say("err", "这一轮还在跑(面板上或计划任务里)，现在点火会和它抢同一个助手/端游 —— 等它结束再来确认。");
                return;
            }
            Say("dim", "确认执行：按代号 " + pendingId + " 取回那份计划，逐条重新过白名单闸。");
            StartChild(GdAgentLink.RunArguments(pendingId), "确认执行(第二击：按计划真跑)");
        }

        void OnDropClick(object sender, EventArgs e)
        {
            if (pendingId.Length == 0) { Say("dim", "现在没有等确认的计划。"); return; }
            Say("dim", "作废这一份(代号 " + pendingId + ")。文件还在 state\\agent\\plans\\ 里，窗子不替你删。");
            ClearPending();
        }

        void OnCancelClick(object sender, EventArgs e)
        {
            if (!busy) { Say("dim", "没有要取消的事。"); return; }
            Say("dim", "不再跟随这一问。已经在跑的那一步不去硬杀它 —— 真跑起来的东西由它自己收尾。");
            Detach();
            Finish(false, "这一问被你不跟了(子进程还在它自己的路上，结果不再显示到这里)");
        }

        void RequestClose()
        {
            if (busy && closeAskAt == DateTime.MinValue)
            {
                closeAskAt = DateTime.Now;
                Say("warn", "这一问还在跑。再点一次 × 就是「不跟了、关窗」—— 子进程不会被这扇窗硬杀，" +
                            "它要是正在执行计划里的某一步，会自己跑完；那时的结果只进主面板日志和盘上的台账，不再显示在这里。");
                return;
            }
            Close();
        }

        void ClearPending()
        {
            pendingId = "";
            runBtn.Enabled = false;
            dropBtn.Enabled = false;
            askBtn.Enabled = true;
        }

        // ---------- 起子进程(机读口：stdout 只有一行信封，人话进 stderr) ----------
        bool busy;

        void StartChild(string args, string title)
        {
            string toolsDir = Path.Combine(root, "tools");
            string settingsText = "";
            try
            {
                string sp = Path.Combine(root, @"config\settings.json");
                if (File.Exists(sp)) settingsText = File.ReadAllText(sp);
            }
            catch { }
            string py = GdAgentLink.FindPython(settingsText, Environment.GetEnvironmentVariable("GD_PYTHON"),
                                               root, Environment.GetEnvironmentVariable("PATH"), GdAgentLink.PythonUsable);
            if (py.Length == 0)
            {
                Say("err", "没找到 Python 3：需求管家的引擎真身在 tools\\gdagent(Python)。");
                Say("err", "先跑 setup.ps1 -WithPython 装一份，或在 settings.json 的 runtime.pythonExe 里指一条绝对路径。");
                if (noteFn != null) noteFn("[WARN] 对话小窗这次没跑：这台机器找不到能用的 Python 3。");
                return;
            }

            Say("dim", "──── " + DateTime.Now.ToString("HH:mm:ss") + "  " + title + " ────");
            lock (bufLock) { outBuf.Length = 0; errTail.Clear(); }
            ProcessStartInfo psi = new ProcessStartInfo(py);
            psi.Arguments = args;
            psi.WorkingDirectory = root;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            // gdagent 这个包在 <root>\tools 下面；PYTHONPATH 指过去就不动工作目录(和 tools\agent.ps1 同一做法)。
            // 同时把子进程的三个流钉成 UTF-8：这台机器的 ANSI 码页是 GBK，重定向后 Python 默认按它写，
            // 而面板这边按 UTF-8 读 —— 不钉这一句，stderr 上的人话会读成乱码。
            psi.EnvironmentVariables["PYTHONPATH"] = toolsDir + Path.PathSeparator + (psi.EnvironmentVariables["PYTHONPATH"] ?? "");
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            try
            {
                child = Process.Start(psi);
                child.OutputDataReceived += delegate(object s, DataReceivedEventArgs e2)
                {
                    if (e2.Data == null) return;
                    lock (bufLock) { outBuf.Append(e2.Data).Append('\n'); }
                };
                child.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e2)
                {
                    if (e2.Data == null) return;
                    lock (bufLock) { errTail.Add(e2.Data); if (errTail.Count > 40) errTail.RemoveAt(0); }
                };
                child.BeginOutputReadLine();
                child.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                child = null;
                Say("err", "起不来：" + ex.Message);
                ClearBusy();
                return;
            }
            busy = true;
            since = DateTime.Now;
            askBtn.Enabled = false;
            runBtn.Enabled = false;
            dropBtn.Enabled = false;
            cancelBtn.Enabled = true;
            status.Text = "在想…";
        }

        void OnPumpTick(object sender, EventArgs e)
        {
            if (!busy) return;
            string last = "";
            lock (bufLock) if (errTail.Count > 0) last = errTail[errTail.Count - 1];
            if (last.Length > 76) last = last.Substring(0, 76) + "…";
            status.Text = "在想… " + (int)(DateTime.Now - since).TotalSeconds + "s" + (last.Length > 0 ? " · " + last : "");
            bool gone = false;
            int code = 0;
            try { if (child != null && child.HasExited) { gone = true; code = child.ExitCode; } }
            catch { gone = true; }
            if (!gone) return;
            string stdout;
            lock (bufLock) stdout = outBuf.ToString();
            Detach();           // 读取管线拆掉、句柄放掉(进程已经自己退出，这里不涉及杀)
            Finish(true, null);
            Render(stdout, code);
        }

        void ClearBusy()
        {
            busy = false;
            status.Text = "空闲";
            cancelBtn.Enabled = false;
            if (pendingId.Length == 0) { askBtn.Enabled = true; runBtn.Enabled = false; dropBtn.Enabled = false; }
        }

        // "不跟了"≠"杀掉"：这一步可能正在 MAA 里跑，硬杀这扇窗不该把那一轮拦腰打断。
        // 这里只把异步读取的管线拆掉、句柄放掉，操作系统那边的进程照自己的路走完。
        void Detach()
        {
            if (child == null) return;
            try { child.CancelOutputRead(); } catch { }
            try { child.CancelErrorRead(); } catch { }
            try { child.Dispose(); } catch { }
            child = null;
        }

        void Finish(bool followed, string why)
        {
            ClearBusy();
            if (!followed && why != null && noteFn != null) noteFn("[STEP] 对话小窗：" + why);
        }

        // ---------- 把信封画出来 ----------
        void Render(string stdout, int exitCode)
        {
            string why;
            Dictionary<string, object> env = GdAgentLink.ParseEnvelope(stdout, out why);
            if (env == null)
            {
                Say("err", why + "(退出码 " + exitCode + ")");
                DumpErr();
                return;
            }
            bool ok = GdAgentLink.IsOk(env);
            string reply = GdAgentLink.Str(env, "reply");
            if (reply.Length > 0) Say(ok ? "info" : "warn", "管家：" + reply);
            string whyEnv = GdAgentLink.Str(env, "why");
            if (whyEnv.Length > 0) Say(ok ? "dim" : "err", "为什么：" + whyEnv);

            List<Dictionary<string, object>> items = GdAgentLink.Rows(env, "items");
            string pid = GdAgentLink.Str(env, "plan_id");
            if (items.Count > 0 && pid.Length > 0)
            {
                Say("dim", "等你当面确认的那几步(第 2 击才会真跑，逐条还要再过一次闸)：");
                for (int i = 0; i < items.Count; i++)
                    Say("warn", "  〔" + (i + 1) + "〕" + GdAgentLink.Str(items[i], "desc") +
                        (Arr(items[i], "args").Length > 0 ? "   ← " + string.Join(" ", Arr(items[i], "args")) : ""));
                Say("dim", "代号 " + pid + (GdAgentLink.Str(env, "created").Length > 0 ? " · 落盘于 " + GdAgentLink.Str(env, "created") : ""));
                pendingId = pid;
                runBtn.Enabled = true;
                dropBtn.Enabled = true;
                askBtn.Enabled = true;
                runBtn.Text = "确认执行(共 " + items.Count + " 步)";
            }
            else if (items.Count > 0)
            {
                // 有计划但没代号 = 那份计划没落盘(盘上没这份事实)，第二击就没有依据
                Say("err", "这几步列出来了却没落盘，所以「确认执行」点不动 —— 上面那句「为什么」就是原因。");
                for (int i = 0; i < items.Count; i++) Say("dim", "  〔" + (i + 1) + "〕" + GdAgentLink.Str(items[i], "desc"));
            }

            List<Dictionary<string, object>> results = GdAgentLink.Rows(env, "results");
            if (results.Count > 0)
            {
                Say("dim", "这一击的逐条结果(屏上显示什么就跑的什么，计划里没有的一步都不多做)：");
                for (int i = 0; i < results.Count; i++)
                {
                    Dictionary<string, object> r = results[i];
                    string st = GdAgentLink.Str(r, "status");
                    string kind = st == "已执行" ? "ok" : (st == "被拒" || st == "没成" ? "err" : "warn");
                    Say(kind, "  〔" + (i + 1) + "〕" + st + " · " + GdAgentLink.Str(r, "desc") +
                        " · 退出码 " + GdAgentLink.ExitOf(r));
                    foreach (string l in Lines(GdAgentLink.Str(r, "detail"))) Say("dim", "        " + l);
                }
                ClearPendingAfterRun();
            }
            if (reply.Length == 0 && results.Count == 0) Say("dim", "(这一轮没有要办的事)");
            if (!ok && whyEnv.Length == 0) DumpErr();
        }

        // 执行过就作废：那份计划在盘上还是同一份，但同一份跑两遍就是多烧一次理智
        void ClearPendingAfterRun()
        {
            if (pendingId.Length == 0) return;
            Say("dim", "代号 " + pendingId + " 这一份已经用过，窗子把它从确认按钮上摘下来了(要再来一遍就重新问一句)。");
            ClearPending();
        }

        void DumpErr()
        {
            lock (bufLock)
                foreach (string l in errTail) Say("dim", "  [stderr] " + l);
        }

        static string[] Arr(Dictionary<string, object> row, string key)
        {
            if (row == null) return new string[0];
            object v;
            if (!row.TryGetValue(key, out v)) return new string[0];
            object[] arr = v as object[];
            if (arr == null) return new string[0];
            List<string> list = new List<string>();
            foreach (object o in arr) list.Add(o == null ? "" : o.ToString());
            return list.ToArray();
        }

        static string[] Lines(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            List<string> list = new List<string>();
            foreach (string l in text.Replace("\r\n", "\n").Split('\n'))
                if (l.Trim().Length > 0) list.Add(l.TrimEnd());
            return list.ToArray();
        }

        static Icon LoadIconFrom(string rootDir)
        {
            try
            {
                string png = Path.Combine(rootDir, @"tools\assets\icon.png");
                if (!File.Exists(png)) return null;
                using (Bitmap bmp = new Bitmap(Image.FromFile(png), new Size(32, 32)))
                    return Icon.FromHandle(bmp.GetHicon());
            }
            catch { return null; }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pump.Stop();
            try { tip.Dispose(); } catch { }
            // 这里**不杀**子进程：第二击可能正在跑 MAA 的某一步，硬杀这扇窗不该把那一轮拦腰打断。
            // 它要是还在跑，跑完的结果进盘上台账；窗子这边只把句柄放掉。
            base.OnFormClosed(e);
        }
    }
}
