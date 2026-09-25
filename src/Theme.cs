using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CodexNetFix
{
    // 辅助色预设（淡蓝 / 淡紫 / 淡黄 / 淡粉）
    public class AccentPreset
    {
        public string Key = "blue";
        public string Name = "淡蓝";
        public Color Main = Color.FromArgb(111, 168, 245);
        public Color Soft = Color.FromArgb(233, 242, 255);
        public Color Text = Color.White;

        public static AccentPreset[] All()
        {
            AccentPreset[] a = new AccentPreset[4];
            a[0] = new AccentPreset(); a[0].Key = "blue"; a[0].Name = "淡蓝"; a[0].Main = Color.FromArgb(96, 160, 248); a[0].Soft = Color.FromArgb(232, 242, 255); a[0].Text = Color.White;
            a[1] = new AccentPreset(); a[1].Key = "purple"; a[1].Name = "淡紫"; a[1].Main = Color.FromArgb(160, 128, 238); a[1].Soft = Color.FromArgb(240, 234, 254); a[1].Text = Color.White;
            a[2] = new AccentPreset(); a[2].Key = "yellow"; a[2].Name = "淡黄"; a[2].Main = Color.FromArgb(242, 198, 84); a[2].Soft = Color.FromArgb(255, 247, 226); a[2].Text = Color.FromArgb(74, 55, 8);
            a[3] = new AccentPreset(); a[3].Key = "pink"; a[3].Name = "淡粉"; a[3].Main = Color.FromArgb(244, 138, 176); a[3].Soft = Color.FromArgb(255, 236, 244); a[3].Text = Color.White;
            return a;
        }

        public static AccentPreset Get(string key)
        {
            foreach (AccentPreset p in All()) if (p.Key == key) return p;
            return All()[0];
        }
    }

    // 主题调色板：浅色（默认，白色主色）/ 深色
    public class Palette
    {
        public bool DarkMode = false;
        public Color Bg = Color.FromArgb(255, 255, 255);
        public Color Card = Color.FromArgb(255, 255, 255);
        public Color CardAlt = Color.FromArgb(246, 247, 249);
        public Color Edge = Color.FromArgb(230, 232, 236);
        public Color Text = Color.FromArgb(28, 30, 34);
        public Color TextSub = Color.FromArgb(130, 136, 148);
        public Color TextFaint = Color.FromArgb(168, 174, 186);
        public Color Accent = Color.FromArgb(111, 168, 245);
        public Color AccentDark = Color.FromArgb(78, 124, 200);   // 深色辅助色（选中/分层用）
        public Color AccentSoft = Color.FromArgb(233, 242, 255);
        public Color AccentText = Color.White;
        public Color Ok = Color.FromArgb(52, 168, 110);
        public Color OkSoft = Color.FromArgb(232, 246, 238);
        public Color Warn = Color.FromArgb(214, 148, 30);
        public Color WarnSoft = Color.FromArgb(255, 246, 224);
        public Color Fail = Color.FromArgb(214, 82, 82);
        public Color FailSoft = Color.FromArgb(255, 238, 238);
        public Color Field = Color.FromArgb(247, 248, 250);
        public Color Sidebar = Color.FromArgb(241, 243, 246);
        public Color ContentBg = Color.FromArgb(247, 249, 252);

        public static Palette Create(bool dark, AccentPreset accent)
        {
            Palette p = new Palette();
            p.DarkMode = dark;
            p.Accent = accent.Main;
            p.AccentDark = ColorUtil.Darken(accent.Main, 0.22f);
            p.AccentSoft = accent.Soft;
            p.AccentText = accent.Text;
            if (dark)
            {
                p.Bg = Color.FromArgb(18, 20, 24);
                p.Card = Color.FromArgb(28, 31, 37);
                p.CardAlt = Color.FromArgb(34, 38, 45);
                p.Edge = Color.FromArgb(52, 57, 66);
                p.Text = Color.FromArgb(238, 240, 244);
                p.TextSub = Color.FromArgb(160, 167, 178);
                p.TextFaint = Color.FromArgb(120, 127, 138);
                p.Field = Color.FromArgb(24, 27, 32);
                p.AccentSoft = Color.FromArgb(38, 46, 60);
                p.Ok = Color.FromArgb(88, 200, 140);
                p.OkSoft = Color.FromArgb(28, 46, 38);
                p.Warn = Color.FromArgb(226, 178, 74);
                p.WarnSoft = Color.FromArgb(48, 42, 26);
                p.Fail = Color.FromArgb(240, 120, 120);
                p.FailSoft = Color.FromArgb(52, 32, 34);
            }
            return p;
        }
    }

    public static class Draw
    {
        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            if (r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        // 向上寻找第一个不透明的父背景色（避免 Transparent 被清成黑色 —— 黑边问题根因）
        public static Color EffectiveBack(Control c)
        {
            Control p = c == null ? null : c.Parent;
            while (p != null)
            {
                if (p.BackColor.A == 255) return p.BackColor;
                p = p.Parent;
            }
            return Color.White;
        }
        public static Font Ui(float size, FontStyle style)
        {
            try { return new Font("Microsoft YaHei UI", size, style); }
            catch { return new Font("Segoe UI", size, style); }
        }
    }

    public static class ColorUtil
    {
        public static Color Blend(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // 颜色加深（用于"深色辅助色"：选中态 / 分层按钮）
        public static Color Darken(Color c, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return Color.FromArgb(c.A, (int)(c.R * (1 - t)), (int)(c.G * (1 - t)), (int)(c.B * (1 - t)));
        }
    }
    // ---------- 轻量动画驱动（60fps，仅在动画进行时运行） ----------
    public static class Anim
    {
        public static bool Enabled = true;
        class Job { public Control Target; public long Start; public int Duration; public Action<float> Apply; public Action Done; }
        static readonly List<Job> jobs = new List<Job>();
        static System.Windows.Forms.Timer timer;

        public static void Start(Control target, int durationMs, Action<float> apply) { Start(target, durationMs, apply, null); }

        public static void Start(Control target, int durationMs, Action<float> apply, Action done)
        {
            if (!Enabled) { try { apply(1f); } catch { } if (done != null) { try { done(); } catch { } } return; }
            Job j = new Job();
            j.Target = target; j.Start = Environment.TickCount; j.Duration = Math.Max(16, durationMs); j.Apply = apply; j.Done = done;
            jobs.Add(j);
            if (timer == null) { timer = new System.Windows.Forms.Timer(); timer.Interval = 16; timer.Tick += delegate(object s, EventArgs e) { Tick(); }; }
            if (!timer.Enabled) timer.Start();
        }

        static void Tick()
        {
            long now = Environment.TickCount;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                Job j = jobs[i];
                float t = (float)(now - j.Start) / j.Duration;
                if (t > 1f) t = 1f;
                float e = t < 0.5f ? (4f * t * t * t) : (1f - (float)Math.Pow(-2f * t + 2f, 3f) / 2f);   // ease-in-out cubic
                try { j.Apply(e); j.Target.Invalidate(); } catch { }
                if (t >= 1f)
                {
                    jobs.RemoveAt(i);
                    if (j.Done != null) { try { j.Done(); } catch { } }
                }
            }
            if (jobs.Count == 0 && timer != null) timer.Stop();
        }
    }
    // 圆角卡片
    public class RoundPanel : Panel
    {
        public int Radius = 16;
        public Color Fill = Color.White;
        public Color Edge = Color.FromArgb(230, 232, 236);
        public bool ShowEdge = true;
        public string Caption = "";
        public bool NoFill = false;
        public Color CaptionColor = Color.White;
        public Font CaptionFont = null;

        public RoundPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (NoFill) return;   // 完全透明面板：不绘制，避免擦掉父级内容（导航滑块/文字）
            e.Graphics.Clear(Draw.EffectiveBack(this));
            Draw.Smooth(e.Graphics);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Draw.Rounded(r, Radius))
            {
                if (!NoFill) using (SolidBrush b = new SolidBrush(Fill)) e.Graphics.FillPath(b, p);
                if (ShowEdge) using (Pen pen = new Pen(Edge)) e.Graphics.DrawPath(pen, p);

                if (Caption != null && Caption.Length > 0)
                {
                    Font f = CaptionFont != null ? CaptionFont : Draw.Ui(9.5f, FontStyle.Regular);
                    TextRenderer.DrawText(e.Graphics, Caption, f, r, CaptionColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }            }
            base.OnPaint(e);
        }
    }

    // 圆角按钮：Primary / Secondary / Ghost 三种风格
    public class RoundButton : Button
    {
        public int Radius = 10;
        public bool Primary = false;
        public bool Ghost = false;
        public Color SwatchColor = Color.Empty;
        public bool SwatchSelected = false;
        public Color TextOverride = Color.Empty;
        public bool TransparentPaint = false;
        public Color CustomFill = Color.Empty;
        public Palette Theme;
        bool hover = false;
        bool down = false;
        float hoverT = 0f;
        public Color HoverFillColor = Color.Empty;   // 指定则用该颜色的半透明悬停
        public bool NoPaint = false;   // 只绘制文字（背景由父级负责）
        public int HoverAlpha = 120;                 // 悬停不透明度 0-255
        float pressT = 0f;

        public RoundButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Font = Draw.Ui(10f, FontStyle.Regular);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; float f = hoverT; Anim.Start(this, 120, delegate(float p) { hoverT = f + (1f - f) * p; }); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; float f = hoverT; Anim.Start(this, 120, delegate(float p) { hoverT = f * (1f - p); }); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Anim.Start(this, 80, delegate(float p) { pressT = p; }); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; float f = pressT; Anim.Start(this, 110, delegate(float p) { pressT = f * (1f - p); }); base.OnMouseUp(e); }

        static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            if (NoPaint)
            {
                Draw.Smooth(pevent.Graphics);
                Rectangle rr0 = new Rectangle(0, 0, Width - 1, Height - 1);
                Color tc0 = TextOverride != Color.Empty ? TextOverride : ForeColor;
                TextRenderer.DrawText(pevent.Graphics, Text, Font, rr0, tc0, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            Graphics g = pevent.Graphics;
            Draw.Smooth(g);
            if (!TransparentPaint) g.Clear(Draw.EffectiveBack(this));
            Palette t = Theme != null ? Theme : Palette.Create(false, AccentPreset.All()[0]);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (CustomFill != Color.Empty)
            {
                Rectangle cr = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath cp2 = Draw.Rounded(cr, Radius))
                using (SolidBrush cb2 = new SolidBrush(hover ? Blend(CustomFill, Color.Black, 0.12) : CustomFill))
                    g.FillPath(cb2, cp2);
                Color ct = TextOverride != Color.Empty ? TextOverride : Color.White;
                TextRenderer.DrawText(g, Text, Font, cr, ct, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            if (SwatchColor != Color.Empty)
            {
                Rectangle rr = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath sp = Draw.Rounded(rr, Radius))
                using (SolidBrush sb = new SolidBrush(SwatchColor))
                    g.FillPath(sb, sp);
                if (SwatchSelected)
                    using (Pen pen = new Pen(t.DarkMode ? Color.White : Color.FromArgb(70, 70, 78), 2f))
                        g.DrawEllipse(pen, 1, 1, Width - 3, Height - 3);
                return;
            }
            Color fill, txt, edge = Color.Transparent;
            if (Primary)
            {
                fill = t.Accent; txt = t.AccentText;
                if (hoverT > 0f) fill = Blend(fill, Color.White, 0.12 * hoverT);
                if (pressT > 0f) fill = Blend(fill, Color.Black, 0.08 * pressT);
            }
            else if (Ghost)
            {
                // 悬停高亮：半透明（用 hover 直接判断，避免依赖动画状态）
                if (HoverFillColor != Color.Empty) fill = Color.FromArgb(hover ? HoverAlpha : 0, HoverFillColor);
                else fill = Color.FromArgb(hover ? 200 : 0, t.CardAlt);
                txt = t.TextSub;
            }
            else
            {
                fill = t.Field; txt = t.Text; edge = t.Edge;
                fill = Blend(fill, t.Accent, 0.10f * hoverT + 0.16f * pressT);
            }
            using (GraphicsPath p = Draw.Rounded(r, Radius))
            {
                if (fill.A > 0) using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (edge.A > 0) using (Pen pen = new Pen(edge)) g.DrawPath(pen, p);
            }
            if (TextOverride != Color.Empty) txt = TextOverride;
            TextRenderer.DrawText(g, Text, Font, r, txt,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // 圆角开关
    public class SwitchBox : Control
    {
        public bool Checked = false;
        public Palette Theme;
        public bool TransparentPaint = false;
        public event EventHandler CheckedChanged;
        float pos = -1f;   // 0=关 1=开（用于滑行动画）

        public SwitchBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(44, 24);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            float from = pos < 0f ? (Checked ? 1f : 0f) : pos;
            Checked = !Checked;
            float to = Checked ? 1f : 0f;
            Anim.Start(this, 150, delegate(float p) { pos = from + (to - from) * p; });
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Smooth(g);
            if (!TransparentPaint) g.Clear(Draw.EffectiveBack(this));
            Palette t = Theme != null ? Theme : Palette.Create(false, AccentPreset.All()[0]);
            Rectangle track = new Rectangle(0, (Height - 22) / 2, Math.Min(Width - 1, 42), 22);
            using (GraphicsPath p = Draw.Rounded(track, 11))
            using (SolidBrush b = new SolidBrush(ColorUtil.Blend(t.Edge, t.Accent, (pos < 0f ? (Checked ? 1f : 0f) : pos))))
                g.FillPath(b, p);
            int kd = 18;
            float sp = pos < 0f ? (Checked ? 1f : 0f) : pos;
            int kx = track.X + 2 + (int)Math.Round((track.Right - kd - 2 - (track.X + 2)) * sp);
            using (SolidBrush b = new SolidBrush(Color.White))
                g.FillEllipse(b, kx, track.Y + 2, kd, kd);
            using (Pen pen = new Pen(Color.FromArgb(40, 0, 0, 0)))
                g.DrawEllipse(pen, kx, track.Y + 2, kd, kd);
        }
    }

    // 状态胶囊标签
    public class StatusPill : Control
    {
        public string Status = "OK";
        public Palette Theme;
        public bool TransparentPaint = false;

        public StatusPill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(58, 24);
            BackColor = Color.Transparent;
            Font = Draw.Ui(9f, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Smooth(g);
            if (!TransparentPaint) g.Clear(Draw.EffectiveBack(this));
            Palette t = Theme != null ? Theme : Palette.Create(false, AccentPreset.All()[0]);
            Color fg = t.TextSub, bg = t.CardAlt;
            if (Status == "OK") { fg = t.Ok; bg = t.OkSoft; }
            else if (Status == "WARN") { fg = t.Warn; bg = t.WarnSoft; }
            else if (Status == "FAIL") { fg = t.Fail; bg = t.FailSoft; }
            else { fg = t.TextSub; bg = t.CardAlt; }
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Draw.Rounded(r, Height / 2))
            using (SolidBrush b = new SolidBrush(bg))
                g.FillPath(b, p);
            TextRenderer.DrawText(g, Status, Font, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
