// 小工具：控件工厂、字体(含等宽怎么挑)、内嵌图标与它的释放时机。
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
        // ---------- 小工具 ----------
        Label MakeLabel(Control parent, string text, int x, int y, int w, int h, ContentAlignment? align, Font font, bool autoSize)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            if (autoSize || w <= 0 || h <= 0) l.AutoSize = true;
            else
            {
                l.AutoSize = false;
                l.AutoEllipsis = true;
                l.Size = new Size(w, h);
                if (align.HasValue) l.TextAlign = align.Value;
            }
            if (font != null) l.Font = font;
            // Label 默认 BackColor=Transparent，但 WinForms 是"画父容器背景"——父底色之后被主题改掉，
            // 而 Label 的 ForeColor 还停在控件默认值上(没显式设过的就是系统深字)：黑底上直接隐形。
            // 所以建控件就显式给一个和"这个父容器在主题里的角色"相称的字色，ApplyOne 再按 Tag 精修。
            l.BackColor = parent.BackColor;
            l.ForeColor = th == null ? Color.FromKnownColor(KnownColor.ControlText) : th.Text;
            parent.Controls.Add(l);
            return l;
        }

        Button MakeButton(Control parent, string text, int x, int y, int w, int h, string kind)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.Tag = kind;                                  // ApplyOne 靠这个认"这是什么按钮"
            b.FlatAppearance.BorderSize = 0;
            b.ForeColor = kind == "primary" ? th.PrimaryFore : th.Text;   // 建控件时就给主题字色(ApplyOne 之后还会精修)
            if (kind == "primary") b.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            else if (kind == "chip") b.Font = SmallFont();
            parent.Controls.Add(b);
            // 圆角只给不横向拉伸的按钮(主动作/导航)：secondary 跟着动作区变宽时 Region 会过期，右侧被裁掉
            if (kind == "primary" || kind == "nav") Round(b, 8);
            return b;
        }

        static void themeTag(Control c, string tag) { c.Tag = tag; }

        // 勾选框：实验功能卡上那三个"各写一个布尔键"的开关用它。
        // 只借主题里的两个字色(正文色 + 父容器底色)，方框本体交给系统按当前主题画 —— 黑白两档之外不加第三种颜色。
        // Click 而不是 CheckedChanged：程序回填现状态(RefreshCards 每 45 秒一次)不该被当成"玩家改主意"写进配置。
        CheckBox MakeCheck(Control parent, string text, int x, int y, int w)
        {
            CheckBox cb = new CheckBox();
            cb.Text = text;
            cb.Location = new Point(x, y);
            cb.Size = new Size(w, 18);
            cb.Font = SmallFont();
            cb.ForeColor = th == null ? Color.FromKnownColor(KnownColor.ControlText) : th.Text;
            cb.BackColor = parent.BackColor;
            cb.TabStop = false;
            cb.Tag = "expcheck";
            parent.Controls.Add(cb);
            return cb;
        }

        void MakeCard(Panel card, int x, int y, int w, int h, string title, AnchorStyles anchor)
        {
            card.Location = new Point(x, y);
            card.Size = new Size(w, h);
            card.Anchor = anchor;
            Controls.Add(card);
            themeTag(card, "card");
            Borderless(card);

            Panel sq = new Panel();
            sq.Location = new Point(14, 13);
            sq.Size = new Size(3, 13);
            sq.Tag = "accent_sq";
            card.Controls.Add(sq);

            Label t = MakeLabel(card, title.ToUpper(), 24, 10, w - 38, 17, null, SectionFont(), false);
            t.Tag = "cardtitle";
            t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        }

        void Borderless(Control c)
        {
            // 深/浅底上卡片只靠 1px 描边分层；Paint 里读当前主题，切主题 Invalidate 即可
            c.Paint += delegate (object s, PaintEventArgs pe)
            {
                using (Pen pen = new Pen(th.Line))
                    pe.Graphics.DrawRectangle(pen, 0, 0, ((Control)s).Width - 1, ((Control)s).Height - 1);
            };
        }

        static void Round(Control c, int radius)
        {
            int w = c.Width, h = c.Height, d = radius * 2;
            if (w <= d || h <= d) { c.Region = null; return; }
            using (System.Drawing.Drawing2D.GraphicsPath gp = new System.Drawing.Drawing2D.GraphicsPath())
            {
                gp.AddArc(0, 0, d, d, 180, 90);
                gp.AddArc(w - d, 0, d, d, 270, 90);
                gp.AddArc(w - d, h - d, d, d, 0, 90);
                gp.AddArc(0, h - d, d, d, 90, 90);
                gp.CloseFigure();
                Region old = c.Region;
                c.Region = new Region(gp);
                if (old != null) { try { old.Dispose(); } catch { } }
            }
        }

        Font SmallFont() { return new Font("Microsoft YaHei UI", 8.5f); }
        Font SectionFont() { return new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold); }

        // 历史表那几列靠 Pad() 数"格子"对齐(中文算两格)，只有等宽字体才数得准。
        // 挑字体的办法不靠"量一个字母一个空格" —— MeasureText 对短串有个跟长度有关的底补
        // (这台机器上量单个空格 14px、量 8 个才 63px，按单字算出来的比例全是假的)。
        // 直接量**真会画出来的那两行**：哪套候选字体下"方舟行"和"1999 行"一样宽，就用哪套；
        // 一套都找不到就退回雅黑(宁可没对齐也别缺字)，那时 --consoletest 的列对齐断言会当场红给你看。
        Font monoFont;
        static string MonoProbeA { get { return Pad("2026-09-25 19:30:00", 20) + "  " + Pad("方舟", 8) + "  " + Pad("662s", 8); } }
        static string MonoProbeB { get { return Pad("2026-09-25 19:36:15", 20) + "  " + Pad("1999", 8) + "  " + Pad("-", 8); } }

        Font MonoFont()
        {
            if (monoFont == null)
            {
                Font best = null;
                foreach (string cand in new string[] { "NSimSun", "SimSun", "Microsoft YaHei UI" })
                {
                    Font f = null;
                    try
                    {
                        f = new Font(cand, 9.5f);
                        if (Math.Abs(TextRenderer.MeasureText(MonoProbeA, f).Width - TextRenderer.MeasureText(MonoProbeB, f).Width) <= 1)
                        { best = f; break; }
                        f.Dispose();
                    }
                    catch { if (f != null) f.Dispose(); }
                }
                monoFont = best != null ? best : SmallFont();
            }
            return monoFont;
        }

        static string Pad(string text, int width)
        {
            int cols = 0;
            foreach (char ch in text) cols += ch >= 0x2E80 ? 2 : 1;
            if (cols >= width) return text + " ";
            return text + new string(' ', width - cols);
        }

        Image embeddedImg;
        Image LoadEmbeddedImage()
        {
            if (embeddedImg != null) return embeddedImg;
            // 优先用旁边那张 assets\icon.png(build.ps1 的产物目录会带上)；没有就用内嵌资源
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                foreach (string cand in new[] {
                    Path.Combine(exeDir, @"tools\assets\icon.png"),
                    Path.Combine(root, @"tools\assets\icon.png") })
                    if (File.Exists(cand)) { embeddedImg = Image.FromFile(cand); return embeddedImg; }
                string beside = Path.Combine(exeDir, "icon.png");
                if (File.Exists(beside)) { embeddedImg = Image.FromFile(beside); return embeddedImg; }
                foreach (string n in GetType().Assembly.GetManifestResourceNames())
                    if (n.EndsWith("icon.png"))
                        using (Stream st = GetType().Assembly.GetManifestResourceStream(n))
                        { if (st != null) { embeddedImg = Image.FromStream(st); return embeddedImg; } }
            }
            catch { }
            return null;
        }

        Icon LoadEmbeddedIcon()
        {
            Image img = LoadEmbeddedImage();
            if (img == null) return null;
            Bitmap bmp = new Bitmap(img, new Size(32, 32));
            IntPtr h = bmp.GetHicon();
            Icon ic = Icon.FromHandle(h);
            // FromHandle 不复制句柄，但 Form.Icon 内部会再拷一份用于窗口消息；这里在设置完成后销毁原始句柄。
            // 保险起见留到 OnFormClosed 再 DestroyIcon(见下)，避免绘制时机不同导致图标变白块。
            iconHandleToFree = h;
            return ic;
        }

        IntPtr iconHandleToFree = IntPtr.Zero;
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pump.Stop();
            StopChild();
            try { foreach (string f in new[] { outPath, errPath }) if (f != null && File.Exists(f)) File.Delete(f); } catch { }
            if (iconHandleToFree != IntPtr.Zero) { try { DestroyIcon(iconHandleToFree); } catch { } }
            base.OnFormClosed(e);
        }

    }
}