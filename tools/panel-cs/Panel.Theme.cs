// 主题：黑白两套配色的定义(Theme)，和套用/读写主题的 ApplyTheme / LoadTheme / SaveTheme。
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
    // ---------- 主题：只有黑、白两套；语义色(绿/黄/红)各自一档，别的颜色一律中性 ----------
    sealed class Theme
    {
        public string Name;
        public Color Window, Rail, Card, CardTitle, NavSub, Line, Accent, NavOn, Hover, Down;
        public Color Text, Ok, Warn, Err, Dim;
        public Color LogBack, LogOk, LogWarn, LogErr, LogStep, LogInfo, LogDim, LogStamp;
        public Color ChipBack, ChipFore, BusyBack, BusyFore;
        public Color PrimaryFore;

        public static Theme Dark()
        {
            Theme t = new Theme();
            t.Name = "dark";
            // 2026-09-25 用户反馈"黑得太过了"：背景整体抬一档灰、正文白字收到 228，
            // 层次仍靠 窗<栏<卡 三级灰度拉开；语义色(绿/黄/红)不动。
            t.Window    = C(27, 27, 27);
            t.Rail      = C(21, 21, 21);
            t.Card      = C(37, 37, 37);
            t.CardTitle = C(190, 190, 190);
            t.NavSub    = C(130, 130, 130);
            t.Line      = C(64, 64, 64);
            t.Accent    = C(255, 255, 255);   // 黑底上唯一的"强调"就是白
            t.NavOn     = C(52, 52, 52);
            t.Hover     = C(48, 48, 48);
            t.Down      = C(64, 64, 64);
            t.Text      = C(228, 228, 228);
            t.Ok        = C(96, 214, 120);
            t.Warn      = C(245, 185, 60);
            t.Err       = C(244, 110, 110);
            t.Dim       = C(140, 140, 140);
            t.LogBack   = C(22, 22, 22);
            t.LogOk     = C(126, 231, 135);
            t.LogWarn   = C(227, 179, 65);
            t.LogErr    = C(255, 123, 114);
            t.LogStep   = C(230, 230, 230);
            t.LogInfo   = C(205, 205, 205);
            t.LogDim    = C(120, 120, 120);
            t.LogStamp  = C(125, 125, 125);
            t.ChipBack  = C(50, 50, 50);
            t.ChipFore  = C(150, 150, 150);
            t.BusyBack  = C(58, 46, 16);
            t.BusyFore  = C(245, 185, 60);
            t.PrimaryFore = C(0, 0, 0);        // 白底按钮配黑字
            return t;
        }

        public static Theme Light()
        {
            Theme t = new Theme();
            t.Name = "light";
            t.Window    = C(246, 246, 246);
            t.Rail      = C(255, 255, 255);
            t.Card      = C(255, 255, 255);
            t.CardTitle = C(60, 60, 60);
            t.NavSub    = C(120, 120, 120);
            // 2026-09-26 视觉第二轮：描边和 chip 底色原来离白卡太近(210/238 对 255)，
            // 浅色主题下交互件的可辨性明显弱于深色 —— 两档灰各自加深一点，仍是纯灰阶。
            t.Line      = C(198, 198, 198);
            t.Accent    = C(20, 20, 20);      // 白底上唯一的"强调"就是黑
            t.NavOn     = C(228, 228, 228);
            t.Hover     = C(238, 238, 238);
            t.Down      = C(222, 222, 222);
            t.Text      = C(20, 20, 20);
            t.Ok        = C(24, 128, 56);
            t.Warn      = C(158, 106, 0);
            t.Err       = C(178, 34, 34);
            t.Dim       = C(120, 120, 120);
            t.LogBack   = C(255, 255, 255);
            t.LogOk     = C(24, 128, 56);
            t.LogWarn   = C(158, 106, 0);
            t.LogErr    = C(178, 34, 34);
            t.LogStep   = C(20, 20, 20);
            t.LogInfo   = C(45, 45, 45);
            t.LogDim    = C(140, 140, 140);
            t.LogStamp  = C(140, 140, 140);
            t.ChipBack  = C(232, 232, 232);
            t.ChipFore  = C(110, 110, 110);
            t.BusyBack  = C(255, 242, 204);
            t.BusyFore  = C(158, 106, 0);
            t.PrimaryFore = C(255, 255, 255);  // 黑底按钮配白字
            return t;
        }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
    }

    // ---------- 主窗体(这一份装的成员见本文件头) ----------
    sealed partial class PanelForm : Form
    {
        // ---------- 主题 ----------
        void OnThemeClick(object sender, EventArgs e)
        {
            th = th.Name == "dark" ? Theme.Light() : Theme.Dark();
            ApplyTheme();
            btnTheme.Text = th.Name == "dark" ? "切到白底" : "切到黑底";
            RefreshCards();
            SetChip(busy ? "正在跑" : "空闲", busy);
            SaveTheme();
            SyncAgentTheme();   // 对话小窗不自己存主题：面板切了它就得跟着切，否则两扇窗一黑一白
            AppendLog("已切到" + (th.Name == "dark" ? "黑" : "白") + "底主题(记在 state\\panel-theme.txt，下次打开还在)。");
        }

        void ApplyTheme()
        {
            BackColor = th.Window;
            foreach (Control c in AllControls()) ApplyOne(c);
            foreach (Panel p in new[] { cardTimes, cardLast, cardGates, cardEnv, cardExp, act, logCard, consoleHost }) p.Invalidate();
            hdr.Invalidate();
            logBox.Invalidate();
        }

        void ApplyOne(Control c)
        {
            object tagObj = c.Tag;
            string tag = tagObj == null ? "" : tagObj.ToString();
            // runBtn 不再单开分支：它就是 kind="primary"，走下面 Button 分支的填充样式。
            if (c == rail) { rail.BackColor = th.Rail; return; }
            if (c == avatar) { avatar.BackColor = th.Rail; return; }
            if (c == brandTitle) { brandTitle.ForeColor = th.Text; brandTitle.BackColor = rail.BackColor; return; }
            if (c == brandSub) { brandSub.ForeColor = th.NavSub; brandSub.BackColor = rail.BackColor; return; }
            if (c == logBox) { logBox.BackColor = th.LogBack; logBox.ForeColor = th.LogInfo; RepaintLog(); return; }
            // "chip" 这个 Tag 被两类控件共用：runChip 那颗状态灯(Label)和一排小按钮(Button)。
            // 早先不区分，chip 按钮全被 SetChip 分支提前 return，切主题时字色不跟着换 —— 这里只留 Label 走 SetChip。
            if (tag == "chip" && !(c is Button)) { SetChip(chipText, busy); return; }
            if (tag.StartsWith("navstripe:")) { /* RefreshCards 统一刷 */ return; }
            if (tag.StartsWith("navdot:")) { return; }
            // Label 的 BackColor 默认是"控件默认浅色"而不是透明 —— 建控件时抓的父底色在
            // ApplyTheme 之前还是系统值，所以每个字色分支都要顺手把底色重新继承一次父容器。
            if (tag.StartsWith("line:")) { c.ForeColor = KindColor(tag.Substring(5)); c.BackColor = th.Card; return; }
            if (tag.StartsWith("cardtitle")) { c.ForeColor = th.CardTitle; c.BackColor = ParentBg(c); return; }
            if (tag == "title_big") { c.ForeColor = th.Text; c.BackColor = ParentBg(c); return; }
            if (tag == "navsub") { c.ForeColor = th.NavSub; c.BackColor = ParentBg(c); return; }
            if (tag == "dim") { c.ForeColor = th.Dim; c.BackColor = ParentBg(c); return; }
            if (tag == "line") { c.BackColor = th.Line; return; }
            if (tag == "accent_sq") { c.BackColor = th.Accent; return; }   // 卡片标题前那 3px 小块
            if (tag == "rail") { c.BackColor = th.Rail; return; }
            if (tag == "card") { c.BackColor = th.Card; return; }
            // 输入框：黑白双主题里"能敲的地方"只靠一档底色差体现，不加第三种颜色
            if (tag == "input" && c is TextBox)
            {
                c.BackColor = th.Name == "dark" ? th.NavOn : th.Window;
                c.ForeColor = th.Text;
                return;
            }
            if (c is Button)
            {
                Button b = (Button)c;
                b.ForeColor = th.Text;
                b.FlatAppearance.MouseOverBackColor = th.Hover;
                b.FlatAppearance.MouseDownBackColor = th.Down;
                if (tag == "capbtn") { b.BackColor = th.Rail; return; }   // 贴在 hdr(同为 rail 色)上，看不出是控件
                if (tag == "nav") { b.BackColor = th.Rail; return; }      // 选中态由 RefreshCards 覆盖
                // 交互件的可辨性分两层：primary 实心块(Accent 底)，secondary/chip 一档底色差 + 1px 描边。
                // 之前所有按钮都是"和本容器同色 + BorderSize=0"，看起来就是一排纯文本 —— 全面板最掉质感的地方。
                // 描边由 FlatAppearance 画在控件内侧，不改 Bounds，自检的越界/叠压断言不受影响。
                if (tag == "primary")
                {
                    b.BackColor = th.Accent;
                    b.ForeColor = th.PrimaryFore;
                    b.FlatAppearance.MouseOverBackColor = th.Name == "dark" ? ControlPaint.Light(th.Accent, 0.6f) : ControlPaint.Dark(th.Accent, 0.15f);
                    b.FlatAppearance.MouseDownBackColor = th.Name == "dark" ? ControlPaint.Dark(th.Accent, 0.15f) : ControlPaint.Light(th.Accent, 0.6f);
                    return;
                }
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = th.Line;
                b.BackColor = th.ChipBack;
                // chip/secondary 的反馈色从自己的底色推出来，黑白两档都成立，不引入第三种颜色
                b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(th.ChipBack, 0.15f);
                b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(th.ChipBack, 0.25f);
                return;
            }
            // 勾选框(实验功能卡那三个)：只换字色和底色这两档，方框本体交给系统画 —— 不加第三种颜色
            if (c is CheckBox)
            {
                c.ForeColor = th.Text;
                c.BackColor = ParentBg(c);
                return;
            }
        }

        static Color ParentBg(Control c)
        {
            return c.Parent != null ? c.Parent.BackColor : SystemColors.Window;
        }

        IEnumerable<Control> AllControls()
        {
            return Walk(this);
        }

        static IEnumerable<Control> Walk(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                yield return c;
                foreach (Control d in Walk(c)) yield return d;
            }
        }

        static Theme LoadTheme()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                foreach (string cand in new[] { Path.Combine(exeDir, "panel-theme.txt"), Path.Combine(Directory.GetCurrentDirectory(), "state", "panel-theme.txt") })
                    if (File.Exists(cand) && File.ReadAllText(cand).Trim() == "light") return Theme.Light();
            }
            catch { }
            return Theme.Dark();
        }

        void SaveTheme()
        {
            try
            {
                string stateDir = Path.Combine(root, "state");
                if (!Directory.Exists(stateDir)) return;   // 没配置就跑不出 state，静默跳过：主题只是外观
                File.WriteAllText(Path.Combine(stateDir, "panel-theme.txt"), th.Name);
            }
            catch { }
        }

    }
}