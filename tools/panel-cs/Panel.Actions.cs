// 按钮动作：全部转调现成脚本，和 panel.ps1 逐条对齐。
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
        // ---------- 按钮动作(全部转调现成脚本，和 panel.ps1 逐条对齐) ----------
        void OnRunClick(object sender, EventArgs e)
        {
            if (busy && child != null && !child.HasExited)
            {
                StopChild();
                AppendLog("面板不再跟随这一轮。若这一轮是走计划任务起的，本体不受影响，到点还会自己跑完并收尾。");
                return;
            }
            if (busy) CompleteTask();
            DateTime now = DateTime.Now;
            // 真要点火前再实时查一次：3 秒一查的缓存挡不住"别人刚点下去"这种毫秒级撞车。
            // 这里不新增"能不能跑"的判定 —— 有没有在跑只看那一轮自己留的活体标记。
            RefreshExternal();
            if (extBusy)
            {
                if ((now - confirmAt).TotalSeconds < 12)
                {
                    confirmAt = DateTime.MinValue;
                    StopExternal();
                    return;
                }
                confirmAt = now;
                runBtn.Text = "再点一次=停掉正在跑的那一轮";
                AppendLog("外部已有一轮在跑(" + extNote + ")。再点一次是去停它；想换别的跑法，先等这一轮自己结束。");
                return;
            }
            if ((now - confirmAt).TotalSeconds < 12)
            {
                confirmAt = DateTime.MinValue;
                runBtn.Text = idleRunText;
                if (flagsViaTask)
                {
                    List<string> a = new List<string>();
                    if (flagsGames && games.Count > 1) { a.Add("-Games"); a.Add("all"); }
                    StartTask(Path.Combine(root, "run-via-task.ps1"), a, "跑一轮(走计划任务，免 UAC)", true);
                }
                else
                {
                    List<string> a = new List<string> { "-Tag", "manual" };
                    if (flagsGames && games.Count > 1) { a.Insert(0, "-Games"); a.Insert(1, "all"); }
                    StartTask(EntryPath, a, "跑一轮(本机直跑" + ElevNote() + ")", true);
                }
                return;
            }
            confirmAt = now;
            runBtn.Text = "再点一次就真的开跑(12 秒内)";
            AppendLog("再点一次这个按钮就开跑。跑完会自动关掉助手、端游和模拟器；中途想停就再点它。");
        }

        string GameName(string id)
        {
            foreach (string[] g in games) if (g[0] == id) return g[1];
            return id;
        }

        // 只跑左边当前页那一路。刻意不做下拉框：跑哪一路已经由页面表达了，
        // 面板上再多一个和页面状态重复的选择器，就多一个"看着选的是 A、实际跑的是 B"的机会。
        void OnOnlyClick(object sender, EventArgs e)
        {
            if (!flagsGames || games.Count < 2) { AppendLog("这一仓只有一路，「只跑这一路」和「开始跑一轮」是同一件事。"); return; }
            if (page == "all" || page.Length == 0) { AppendLog("先在左边点进某一款游戏，再回来点「只跑这一路」。"); return; }
            if (busy && child != null && !child.HasExited) { AppendLog("面板里已经有一轮在跑了，先等它结束(或点大按钮停跟随)。"); return; }
            if (busy) CompleteTask();
            RefreshExternal();
            if (extBusy) { AppendLog("外部已有一轮在跑(" + extNote + ") —— 只跑一路也会和它抢同一个助手，先停掉那一轮。"); return; }
            string id = page;
            string shown = GameName(id);
            DateTime now = DateTime.Now;
            if ((now - onlyAt).TotalSeconds < 12)
            {
                onlyAt = DateTime.MinValue;
                btnOnly.Text = "只跑这一路";
                // 走任务时 -Games 靠 run-via-task.ps1 覆盖任务参数实现免 UAC；
                // 它要是拼不出来会自己回落直跑并在日志里说清楚(那时可能弹一次 UAC)。
                if (flagsViaTask)
                    StartTask(Path.Combine(root, "run-via-task.ps1"), new List<string> { "-Games", id }, "只跑" + shown + "(走计划任务，免 UAC)", true);
                else
                    StartTask(EntryPath, new List<string> { "-Games", id, "-Tag", "manual" }, "只跑" + shown + "(本机直跑" + ElevNote() + ")", true);
                return;
            }
            onlyAt = now;
            btnOnly.Text = "再点一次=只跑" + shown;
            AppendLog("只跑" + shown + "。再点一次这个按钮开跑(12 秒内)；要跑全部就切回「总览」用左边那个大按钮。");
        }

        void OnAckClick(object sender, EventArgs e)
        {
            // 空名有两种来路，都不等于"按钮坏了"(2026-10-12 第 1 刀)：方舟这一路的 -Ack 确认门已经退场
            // (每轮拿活动最后一关当探针自证，进得去就刷)，而 1999 在合体入口里叫 -Ack1999、不在这枚候选表里。
            // 所以这里只把"这门现在怎么算"说清楚，绝不写成"这个按钮不做事"让人以为功能坏了。
            if (ackFlagName.Length == 0)
            {
                AppendLog("这个入口没有\"活动确认\"参数：方舟一路已于 2026-10-12 改为每轮自证(拿活动最后一关当探针，进得去就按活动图刷，被 MAA 剪掉就判还没打通、当日改刷长草)，不需要人点；门卡片里显示的就是最新一条自证结论。1999 那一路仍要人确认，合体入口里是 -Ack1999。");
                return;
            }
            DateTime now = DateTime.Now;
            if ((now - ackAt).TotalSeconds < 12)
            {
                ackAt = DateTime.MinValue;
                ackBtn.Text = "我打过活动了";
                StartTask(EntryPath, new List<string> { "-" + ackFlagName, "-DryRun" }, "标记本轮活动已打过(不再刷活动图)", false);
                return;
            }
            ackAt = now;
            ackBtn.Text = "再点一次=确认我已打过活动";
            AppendLog("作用：版本更新后你先手动打活动，然后点它，之后到点就只刷日常、不碰活动图。再点一次确认。");
        }

        void OnRefreshClick(object sender, EventArgs e)
        {
            // 手点刷新顺带把外部轮次也认一次：自动刷新被 --no-beat 关掉时，这条路就是唯一的感知时机
            RefreshExternal();
            if (RefreshNow("手动刷新")) AppendLog("已刷新上面的状态。");
        }

        // ---------- 「问需求管家」：开/唤那一只独立小窗 ----------
        // 面板这边只给三样东西：仓库根、当前主题、"现在有没有一轮在跑"的判据。
        // 能跑什么/要跑几步/跑没跑成，全部由 tools\gdagent 的机读口回答(契约：stdout 那一行信封)，
        // 面板一个字都不判定 —— 和其余按钮转调脚本是同一套分工。
        void OpenAgentWindow()
        {
            if (agentWin != null && !agentWin.IsDisposed) { agentWin.Activate(); return; }
            agentWin = new AgentChatForm(root, th,
                delegate { return busy || extBusy; },
                delegate(string msg) { AppendLog(msg); });
            agentWin.FormClosed += delegate { agentWin = null; };
            agentWin.Show(this);
            AppendLog("开了对话小窗：第一击只列计划，真跑要你在小窗上再点一次「确认执行」。");
        }

        // 主面板换主题时通知它(小窗不自己存主题，跟着面板走)
        void SyncAgentTheme()
        {
            if (agentWin != null && !agentWin.IsDisposed) agentWin.ApplyThemeNow(th);
        }
    }
}