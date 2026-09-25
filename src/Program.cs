using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CodexNetFix
{
    public class MainForm : Form
    {
        AppSettings cfg = AppSettings.Load();
        AccentPreset accent;
        Palette pal;
        NotifyIcon tray;
        System.Windows.Forms.Timer monitorTimer;
        bool lastOnline = true;
        bool busyMonitor = false;

        Panel content;
        NavItem navRepair, navCheck, navAbout, navSettings;
        string currentPage = "repair";
        float pillX = -1f, pillW = 0f;   // 导航胶囊滑动
        NavItem navPressItem = null;
        bool dragMoved = false;
        Panel pageRepair, pageCheck, pageAbout, pageSettings;
        RoundPanel cardPort, cardOptions, cardActions, cardLog, cardCheckList, cardLook, cardBehave, cardStore, cardLogNow, cardLogPrev, cardHistory;
        TextBox txtPort, logBox;
        Label lblPortHint, lblCheckSum, lblStorePath, lblVersion, lblIntervalValue, lblAccentName;
        RoundButton btnDetect, btnFix, btnRestart, btnRollback, btnClearEnv, btnCheck, btnExport, btnCopy, btnDoc, btnSave, btnOpenDir, btnIntervalMinus, btnIntervalPlus, btnCleanup;
        RoundButton[] swatches;
        SwitchBox swStartCheck, swMonitor, swTray, swDeep, swCodex, swUserEnv, swGit, swGitExec, swAnim;
        FlowLayoutPanel checkList;
        Label titleLabel, subLabel;
        Label lblSideStatus, lblSideSub, lblSideHome;
        RoundPanel cardSideStatus;
        Panel topBar, sidebar, contentHost, bodyPanel, bottomBar;
        List<string> runLog = new List<string>();

        public MainForm()
        {
            accent = AccentPreset.Get(cfg.AccentKey);
            pal = Palette.Create(false, accent);
            FormBorderStyle = FormBorderStyle.None;
            Text = "Codex 网络修复工具";
            Size = new Size(1120, 780);
            MinimumSize = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = Draw.Ui(9.5f, FontStyle.Regular);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildHeader();
            BuildTabs();
            BuildPages();
            BuildBottom();
            BuildTray();
            BindSettings();
            ApplyTheme();

            Resize += delegate { LayoutShell(); ApplyRoundRegion(); };
            ApplyRoundRegion();
            Load += delegate { OnLoaded(); };
            Shown += delegate { BeginInvoke(new Action(delegate { ShowStartupDialogs(); })); };
            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (cfg.TrayResident && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true; Hide();
                    if (tray != null) tray.ShowBalloonTip(2000, "Codex 网络修复工具", "已最小化到托盘，仍在后台运行。", ToolTipIcon.Info);
                }
            };
        }

        void LayoutShell()
        {
            if (topBar == null || bodyPanel == null || bottomBar == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            int topH = 52, botH = 32;
            topBar.SetBounds(0, 0, w, topH);
            bodyPanel.SetBounds(0, topH, w, h - topH - botH);
            bottomBar.SetBounds(0, h - botH, w, botH);
        }

        void ApplyRoundRegion()
        {
            try
            {
                if (WindowState == FormWindowState.Maximized) { Region = null; return; }
                using (System.Drawing.Drawing2D.GraphicsPath gp = Draw.Rounded(new Rectangle(0, 0, Width, Height), 16))
                    Region = new Region(gp);
            }
            catch { }
        }

        void BuildHeader()
        {
            topBar = new Panel();
            topBar.Height = 52; topBar.Tag = "topbar"; topBar.BackColor = pal.Accent;

            PictureBox pic = new PictureBox();
            pic.Left = 16; pic.Top = 15; pic.Width = 26; pic.Height = 26; pic.SizeMode = PictureBoxSizeMode.Zoom;
            try { pic.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch { }
            titleLabel = new Label();
            titleLabel.Text = "Codex 网络修复工具";
            titleLabel.Font = Draw.Ui(11.5f, FontStyle.Bold);
            titleLabel.ForeColor = Color.White; titleLabel.BackColor = pal.Accent;
            titleLabel.AutoSize = true; titleLabel.Left = 50; titleLabel.Top = 17; titleLabel.Tag = "ontopbar";
            topBar.Controls.Add(pic); topBar.Controls.Add(titleLabel);

            navRepair = MakeNav("修复", 300, "repair");
            navCheck = MakeNav("自检", 396, "check");
            navAbout = MakeNav("更新日志", 492, "about");
            navSettings = MakeNav("设置", 588, "settings");
            // 导航项不加入控件树：由 topBar 父级统一绘制 + 命中测试（子控件会擦除父级绘制）

            RoundButton mn = WinBtn("—", delegate { WindowState = FormWindowState.Minimized; });
            mn.Tag = "winbtn";
            RoundButton cl = WinBtn("✕", delegate { Close(); });
            cl.Tag = "winclose"; cl.HoverFillColor = Color.FromArgb(255, 232, 82, 82); cl.HoverAlpha = 60;
            winButtonsCache = new RoundButton[] { mn, cl };
            topBar.Controls.Add(mn); topBar.Controls.Add(cl);
            EventHandler place = delegate
            {
                cl.Left = topBar.Width - 14 - cl.Width;
                mn.Left = cl.Left - mn.Width;
            };
            topBar.Resize += delegate { place(null, EventArgs.Empty); };
            place(null, EventArgs.Empty);

            topBar.Paint += delegate(object s, PaintEventArgs e) { DrawNavPills(e.Graphics); };
            topBar.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) DragWindow(); };
            titleLabel.MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) DragWindow(); };
            AttachDrag(pic); AttachDrag(titleLabel);
            topBar.MouseDown += delegate(object s, MouseEventArgs e)
            {
                navPressItem = HitNav(e.Location);
                if (navPressItem == null && e.Button == MouseButtons.Left)
                {
                    dragOn = true; dragMoved = false; dragCursorStart = Cursor.Position; dragFormStart = Location;
                }
            };
            topBar.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (dragOn)
                {
                    Point cur = Cursor.Position;
                    if (Math.Abs(cur.X - dragCursorStart.X) + Math.Abs(cur.Y - dragCursorStart.Y) > 3) dragMoved = true;
                    Location = new Point(dragFormStart.X + (cur.X - dragCursorStart.X), dragFormStart.Y + (cur.Y - dragCursorStart.Y));
                }
                NavItem h = HitNav(e.Location);
                bool changed = false;
                foreach (NavItem n in Navs()) { bool hv = (n == h); if (n.Hover != hv) { n.Hover = hv; changed = true; } }
                if (changed) topBar.Invalidate();
                topBar.Cursor = h != null ? Cursors.Hand : Cursors.SizeAll;
            };
            topBar.MouseUp += delegate(object s, MouseEventArgs e)
            {
                NavItem h = HitNav(e.Location);
                if (navPressItem != null && h == navPressItem && !dragMoved) ShowPage(navPressItem.Key);
                navPressItem = null; dragOn = false; dragMoved = false;
            };
            topBar.MouseLeave += delegate(object s, EventArgs e)
            {
                bool changed = false;
                foreach (NavItem n in Navs()) if (n.Hover) { n.Hover = false; changed = true; }
                if (changed) topBar.Invalidate();
            };
            foreach (NavItem n in new NavItem[] { navRepair, navCheck, navAbout, navSettings }) { AttachDrag(n.Box); AttachDrag(n.Text); }
            AttachDrag(mn); AttachDrag(cl);
            Controls.Add(topBar);
        }

        public class NavItem
        {
            public RoundPanel Box;
            public Label Text;
            public string Key = "";
            public string Caption = "";
            public bool Hover = false;
        }

        RoundButton[] winButtonsCache;
        RoundButton[] WinButtons() { return winButtonsCache; }

        NavItem[] Navs() { return new NavItem[] { navRepair, navCheck, navAbout, navSettings }; }

        NavItem HitNav(Point pt)
        {
            foreach (NavItem n in Navs())
                if (n != null && n.Box != null && new Rectangle(n.Box.Left, n.Box.Top, n.Box.Width, n.Box.Height).Contains(pt)) return n;
            return null;
        }

        void AnimatePill()
        {
            NavItem target = null;
            foreach (NavItem n in new NavItem[] { navRepair, navCheck, navAbout, navSettings })
                if (n.Key == currentPage) target = n;
            if (target == null || topBar == null) return;
            float toX = target.Box.Left, toW = target.Box.Width;
            if (pillX < 0f) { pillX = toX; pillW = toW; return; }
            float fromX = pillX, fromW = pillW;
            Anim.Start(topBar, 180, delegate(float e) { pillX = fromX + (toX - fromX) * e; pillW = fromW + (toW - fromW) * e; });
        }

        NavItem MakeNav(string text, int left, string key)
        {
            NavItem n = new NavItem();
            n.Key = key; n.Caption = text;
            n.Box = new RoundPanel();
            n.Box.Left = left; n.Box.Top = 10; n.Box.Width = 86; n.Box.Height = 32;
            n.Box.NoFill = true;
            n.Box.ShowEdge = false; n.Box.Tag = "navbox";
            n.Box.BackColor = pal.Accent;
            n.Box.CaptionFont = Draw.Ui(9.5f, FontStyle.Regular);
            n.Box.Cursor = Cursors.Hand;
            EventHandler go = delegate { ShowPage(key); };
            EventHandler enter = delegate { n.Hover = true; if (topBar != null) topBar.Invalidate(); };
            EventHandler leave = delegate { n.Hover = false; if (topBar != null) topBar.Invalidate(); };
            n.Box.Click += go; n.Box.MouseEnter += enter; n.Box.MouseLeave += leave;
            return n;
        }

        void StyleNav()
        {
            if (navRepair == null) return;
            if (topBar != null) topBar.Invalidate();
        }        void DrawNavPills(Graphics g)
        {
            if (navRepair == null) return;
            Draw.Smooth(g);
            NavItem[] items = new NavItem[] { navRepair, navCheck, navAbout, navSettings };

            // 悬停项：浅色圆角矩形
            foreach (NavItem n in items)
            {
                if (n.Key == currentPage || !n.Hover) continue;
                Rectangle hr = new Rectangle(n.Box.Left, n.Box.Top, n.Box.Width - 1, n.Box.Height - 1);
                using (System.Drawing.Drawing2D.GraphicsPath gp = Draw.Rounded(hr, 8))
                using (SolidBrush sb = new SolidBrush(Mix(pal.Accent, Color.White, 0.20)))
                    g.FillPath(sb, gp);
            }

            // 滑块：圆角矩形（radius 8，非两端半圆）
            int px = pillX < 0f ? navRepair.Box.Left : (int)Math.Round(pillX);
            int pw = pillW <= 0f ? navRepair.Box.Width : (int)Math.Round(pillW);
            Rectangle pr = new Rectangle(px, navRepair.Box.Top, Math.Max(8, pw - 1), navRepair.Box.Height - 1);
            using (System.Drawing.Drawing2D.GraphicsPath gp = Draw.Rounded(pr, 8))
            using (SolidBrush sb = new SolidBrush(Color.White))
                g.FillPath(sb, gp);

            // 文字：按滑块覆盖该导航项的比例，从白色渐变到强调色（避免“露馅”）
            foreach (NavItem n in items)
            {
                Rectangle tr = new Rectangle(n.Box.Left, n.Box.Top, n.Box.Width, n.Box.Height);
                int x1 = Math.Max(tr.Left, pr.Left), x2 = Math.Min(tr.Right, pr.Right);
                float cov = x2 > x1 ? (float)(x2 - x1) / tr.Width : 0f;
                Color col = ColorUtil.Blend(Color.White, pal.Accent, cov);
                Font f = n.Box.CaptionFont != null ? n.Box.CaptionFont : Draw.Ui(9.5f, FontStyle.Regular);
                TextRenderer.DrawText(g, n.Caption, f, tr, col,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
        static Color Lighten(Color c, double t) { return Color.FromArgb(c.A, (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t)); }
        static Color Darken(Color c, double t) { return Color.FromArgb(c.A, (int)(c.R * (1 - t)), (int)(c.G * (1 - t)), (int)(c.B * (1 - t))); }
        RoundButton WinBtn(string glyph, EventHandler h)
        {
            RoundButton b = new RoundButton();
            b.Text = glyph; b.Width = 36; b.Height = 30; b.Top = 12; b.Radius = 15;
            b.Font = Draw.Ui(9f, FontStyle.Regular);
            // 纯图标按钮：默认无背景，悬停时半透明高亮
            b.Ghost = true;
            b.CustomFill = Color.Empty;
            b.TextOverride = Color.White;
            b.HoverFillColor = Color.White;
            b.HoverAlpha = 24;   // ≈9% 白，非常柔和
            b.NoPaint = true;           // 背景完全交给顶栏绘制
            b.Click += h;
            return b;
        }
        void FixWinButtons(Panel head, RoundButton mn, RoundButton mx, RoundButton cl)
        {
            EventHandler place = delegate
            {
                int right = head.Width - 16;
                cl.Left = right - cl.Width;
                mx.Left = cl.Left - mx.Width - 2;
                mn.Left = mx.Left - mn.Width - 2;
            };
            head.Resize += delegate { place(null, EventArgs.Empty); };
            place(null, EventArgs.Empty);
        }

        void ToggleMax()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            ApplyRoundRegion();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        bool dragOn = false;
        Point dragCursorStart, dragFormStart;

        void AttachDrag(Control c)
        {
            if (c == null) return;
            c.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                dragOn = true; dragCursorStart = Cursor.Position; dragFormStart = Location;
            };
            c.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (!dragOn) return;
                Point cur = Cursor.Position;
                Location = new Point(dragFormStart.X + (cur.X - dragCursorStart.X), dragFormStart.Y + (cur.Y - dragCursorStart.Y));
            };
            c.MouseUp += delegate(object s, MouseEventArgs e) { dragOn = false; };
        }

        static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        void DragWindow()
        {
            History.Compact();
            AppendLog(AppVersion.Current + "  已启动");
            AppendLog("CODEX_HOME = " + Core.CodexHome());
            AppendLog("使用步骤：① 启动代理软件 → ②【一键修复并部署】→ ③【重启 Codex】→ ④【全面自检】");
            AppendLog("");
            string warn = Core.ProbeEnvironment();
            if (warn.Length > 0) { AppendLog("⚠ " + warn); SetStatus(warn, pal.Fail); }
            if (Program.StartPage != "repair") ShowPage(Program.StartPage);
            if (txtPort.Text.Trim().Length == 0) DoDetect();
            StartMonitorIfNeeded();
            if (cfg.CheckOnStart) DoCheck(false);
        }

        Panel NewPage()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill; p.Visible = false; p.Tag = "page";
            return p;
        }

        Label MkLabel(string text, float size, FontStyle style, int x, int y, string role)
        {
            Label l = new Label();
            l.Text = text; l.Font = Draw.Ui(size, style); l.AutoSize = true; l.Left = x; l.Top = y; l.Tag = role;
            return l;
        }

        RoundPanel MkCard(string title, int x, int y, int w, int h)
        {
            RoundPanel c = new RoundPanel();
            c.Left = x; c.Top = y; c.Width = w; c.Height = h; c.Radius = 14;
            c.Fill = pal.Card; c.Edge = pal.Edge;
            if (title.Length > 0)
            {
                c.Controls.Add(MkLabel(title, 10f, FontStyle.Bold, 18, 14, "cardtitle"));
                Panel sep = new Panel();
                sep.Left = 18; sep.Top = 42; sep.Height = 1; sep.Width = w - 36; sep.Tag = "sep";
                sep.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                c.Controls.Add(sep);
            }
            return c;
        }

        SwitchBox MkSwitch(string text, int x, int y, Control parent, int labelWidth)
        {
            Label l = MkLabel(text, 9.5f, FontStyle.Regular, x, y + 1, "opt");
            l.AutoSize = false; l.Width = labelWidth; l.Height = 22;
            SwitchBox s = new SwitchBox();
            s.Left = x + labelWidth + 8; s.Top = y - 2;
            parent.Controls.Add(l); parent.Controls.Add(s);
            return s;
        }

        RoundButton MkAction(string text, int x, int y, int w, EventHandler h)
        {
            RoundButton b = new RoundButton();
            b.Text = text; b.Left = x; b.Top = y; b.Width = w; b.Height = 36;
            b.Click += h;
            return b;
        }

        void BuildTabs()
        {
            Panel body = new Panel();
            body.Tag = "body"; bodyPanel = body; body.Dock = DockStyle.None;

            contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill; contentHost.Padding = new Padding(18, 16, 18, 16); contentHost.Tag = "content";

            sidebar = new Panel();
            sidebar.Dock = DockStyle.Left; sidebar.Width = 268; sidebar.Tag = "sidebar";

            StatusPill pill = new StatusPill();
            pill.Name = "sidePill"; pill.Status = "INFO"; pill.Left = 22; pill.Top = 22; pill.Width = 62;
            Label cap = MkLabel("代理状态", 9f, FontStyle.Regular, 94, 26, "hint");
            sidebar.Controls.Add(pill); sidebar.Controls.Add(cap);

            redPill = new RoundPanel();
            redPill.Tag = "redpill"; redPill.Radius = 13; redPill.ShowEdge = false;
            redPill.Fill = Color.FromArgb(232, 82, 82); redPill.BackColor = redPill.Fill;
            redPill.Left = 168; redPill.Top = 20; redPill.Width = 82; redPill.Height = 26;
            redPill.Cursor = Cursors.Hand;
            redPill.Caption = "更新提示"; redPill.CaptionColor = Color.White;
            redPill.CaptionFont = Draw.Ui(8.5f, FontStyle.Bold);
            redPill.Click += delegate { ShowPage("about"); };
            redPill.Visible = cfg.SuppressUpdateTip;
            sidebar.Controls.Add(redPill);

            lblSideStatus = MkLabel("未检测", 21f, FontStyle.Bold, 22, 74, "opt");
            lblSideSub = MkLabel("点【一键修复并部署】开始", 9f, FontStyle.Regular, 24, 120, "hint");
            lblSideHome = MkLabel(Core.CodexHome(), 8f, FontStyle.Regular, 24, 146, "hint");
            lblSideHome.AutoSize = false; lblSideHome.Width = 220; lblSideHome.Height = 40;
            sidebar.Controls.Add(lblSideStatus); sidebar.Controls.Add(lblSideSub); sidebar.Controls.Add(lblSideHome);

            Panel btns = new Panel();
            btns.Dock = DockStyle.Bottom; btns.Height = 190; btns.Tag = "sidebar";
            btnFix = new RoundButton();
            btnFix.Text = "一键修复并部署"; btnFix.Primary = true; btnFix.Radius = 10;
            btnFix.Left = 18; btnFix.Top = 8; btnFix.Width = 232; btnFix.Height = 44;
            btnFix.Font = Draw.Ui(10.5f, FontStyle.Bold);
            btnFix.Click += delegate { DoRepair(); };
            btnRestart = MkAction("重启 Codex", 18, 60, 112, delegate { DoRestart(); });
            btnRollback = MkAction("还原备份", 138, 60, 112, delegate { DoRollback(); });
            btnCheck = MkAction("全面自检", 18, 102, 112, delegate { ShowPage("check"); DoCheck(true); });
            btnClearEnv = MkAction("清除变量", 138, 102, 112, delegate { DoClearEnv(); });
            btnCleanup = new RoundButton();
            btnCleanup.Text = "清理旧版本"; btnCleanup.Ghost = true;
            btnCleanup.Left = 18; btnCleanup.Top = 146; btnCleanup.Width = 112; btnCleanup.Height = 30;
            btnCleanup.Click += delegate { DoCleanupOld(); };
            btnDoc = new RoundButton();
            btnDoc.Text = "使用说明"; btnDoc.Ghost = true;
            btnDoc.Left = 138; btnDoc.Top = 146; btnDoc.Width = 112; btnDoc.Height = 30;
            btnDoc.Click += delegate { OpenDoc(); };
            btns.Controls.Add(btnFix); btns.Controls.Add(btnRestart); btns.Controls.Add(btnRollback);
            btns.Controls.Add(btnCheck); btns.Controls.Add(btnClearEnv); btns.Controls.Add(btnCleanup); btns.Controls.Add(btnDoc);
            AttachDrag(sidebar); AttachDrag(cap); AttachDrag(lblSideStatus); AttachDrag(lblSideSub);
            sidebar.Controls.Add(btns);

            body.Controls.Add(contentHost);
            body.Controls.Add(sidebar);
            Controls.Add(body);
        }

        void BuildPages()
        {
            content = contentHost;
            pageRepair = NewPage(); pageCheck = NewPage(); pageAbout = NewPage(); pageSettings = NewPage();
            content.Controls.Add(pageRepair); content.Controls.Add(pageCheck); content.Controls.Add(pageAbout); content.Controls.Add(pageSettings);
            content.BringToFront();
            BuildRepairPage(); BuildCheckPage(); BuildSettingsPage(); BuildAboutPage();
            ShowPage("repair");
        }

        void BuildRepairPage()
        {
            cardPort = MkCard("代理端口", 0, 0, 380, 108);
            RoundPanel wrap = new RoundPanel();
            wrap.Left = 16; wrap.Top = 44; wrap.Width = 140; wrap.Height = 36; wrap.Radius = 9; wrap.Tag = "fieldwrap";
            txtPort = new TextBox();
            txtPort.BorderStyle = BorderStyle.None; txtPort.Font = new Font("Consolas", 12.5f);
            txtPort.Left = 12; txtPort.Top = 9; txtPort.Width = 116;
            wrap.Controls.Add(txtPort);
            btnDetect = new RoundButton();
            btnDetect.Text = "自动探测"; btnDetect.Left = 168; btnDetect.Top = 44; btnDetect.Width = 100; btnDetect.Height = 36;
            btnDetect.Click += delegate { DoDetect(); };
            lblPortHint = MkLabel("留空即自动探测", 8.5f, FontStyle.Regular, 16, 86, "hint");
            cardPort.Controls.Add(wrap); cardPort.Controls.Add(btnDetect); cardPort.Controls.Add(lblPortHint);

            cardOptions = MkCard("修复选项", 0, 124, 380, 186);
            swCodex = MkSwitch("修复 Codex 配置", 16, 54, cardOptions, 300);
            swUserEnv = MkSwitch("写入用户环境变量", 16, 84, cardOptions, 300);
            swGit = MkSwitch("修复 git TLS 后端", 16, 114, cardOptions, 300);
            swGitExec = MkSwitch("注入 GIT_EXEC_PATH", 16, 144, cardOptions, 300);

            RoundPanel cardSteps = MkCard("使用步骤", 0, 326, 380, 272);
            string[] steps = new string[] { "① 启动代理软件（Clash / iKuuu 等）", "② 点左侧【一键修复并部署】", "③ 点【重启 Codex】", "④ 点【全面自检】确认全部 OK", "", "提示：自检失败多是代理节点掉线，", "换个节点重新自检即可。" };
            int sy = 48;
            foreach (string s in steps) { cardSteps.Controls.Add(MkLabel(s, 9f, FontStyle.Regular, 18, sy, "opt")); sy += 26; }

            cardLog = MkCard("运行日志", 396, 0, 394, 598);
            logBox = new TextBox();
            logBox.Multiline = true; logBox.ScrollBars = ScrollBars.Vertical; logBox.ReadOnly = true; logBox.BorderStyle = BorderStyle.None;
            logBox.Font = new Font("Consolas", 8.5f);
            logBox.Left = 18; logBox.Top = 46; logBox.Width = 358; logBox.Height = 532;
            cardLog.Controls.Add(logBox);
            cardLog.Resize += delegate { logBox.Width = cardLog.Width - 36; logBox.Height = cardLog.Height - 66; };

            pageRepair.Controls.Add(cardPort); pageRepair.Controls.Add(cardOptions);
            pageRepair.Controls.Add(cardSteps); pageRepair.Controls.Add(cardLog);
        }

        void BuildCheckPage()
        {
            cardCheckList = MkCard("自检结果", 0, 0, 790, 598);
            RoundButton run = new RoundButton();
            run.Text = "开始自检"; run.Primary = true;
            run.Left = 18; run.Top = 46; run.Width = 110; run.Height = 34;
            run.Click += delegate { DoCheck(true); };
            btnExport = new RoundButton();
            btnExport.Text = "导出报告"; btnExport.Left = 138; btnExport.Top = 46; btnExport.Width = 100; btnExport.Height = 34;
            btnExport.Click += delegate { DoExport(); };
            btnCopy = new RoundButton();
            btnCopy.Text = "复制报告"; btnCopy.Left = 246; btnCopy.Top = 46; btnCopy.Width = 100; btnCopy.Height = 34;
            btnCopy.Click += delegate { DoCopyReport(); };
            lblCheckSum = MkLabel("尚未自检", 9f, FontStyle.Regular, 362, 54, "hint");
            checkList = new FlowLayoutPanel();
            checkList.Left = 18; checkList.Top = 94; checkList.Width = 754; checkList.Height = 486;
            checkList.AutoScroll = true; checkList.FlowDirection = FlowDirection.TopDown; checkList.WrapContents = false;
            checkList.Tag = "flow";
            cardCheckList.Controls.Add(run); cardCheckList.Controls.Add(btnExport); cardCheckList.Controls.Add(btnCopy);
            cardCheckList.Controls.Add(lblCheckSum); cardCheckList.Controls.Add(checkList);
            cardCheckList.Resize += delegate { checkList.Width = cardCheckList.Width - 36; checkList.Height = cardCheckList.Height - 114; };
            pageCheck.Controls.Add(cardCheckList);
        }

        RoundButton MkSwatch(string key, int x, int y)
        {
            AccentPreset p = AccentPreset.Get(key);
            RoundButton b = new RoundButton();
            b.Text = ""; b.Left = x; b.Top = y; b.Width = 42; b.Height = 42; b.Radius = 21;
            b.Tag = "swatch:" + key;
            b.SwatchColor = p.Main;
            b.SwatchSelected = (cfg.AccentKey == key);
            b.Click += delegate { SetAccent(key); };
            return b;
        }

        void BuildSettingsPage()
        {
            cardLook = MkCard("辅助色", 0, 0, 386, 162);
            string[] keys = new string[] { "blue", "purple", "yellow", "pink" };
            swatches = new RoundButton[4];
            for (int i = 0; i < 4; i++) swatches[i] = MkSwatch(keys[i], 22 + i * 54, 62);
            lblAccentName = MkLabel("当前：" + accent.Name, 9f, FontStyle.Regular, 22, 118, "hint");
            cardLook.Controls.Add(lblAccentName);
            foreach (RoundButton b in swatches) cardLook.Controls.Add(b);

            cardBehave = MkCard("行为", 0, 178, 386, 420);
            swStartCheck = MkSwitch("启动时自动自检", 18, 56, cardBehave, 250);
            swMonitor = MkSwitch("后台监控代理健康", 18, 92, cardBehave, 250);
            swTray = MkSwitch("关闭窗口时最小化到托盘", 18, 128, cardBehave, 250);
            swAnim = MkSwitch("界面动画（滑块 / 胶囊滑动）", 18, 164, cardBehave, 250);
            cardBehave.Controls.Add(MkLabel("监控间隔", 9.5f, FontStyle.Regular, 18, 214, "opt"));
            btnIntervalMinus = new RoundButton();
            btnIntervalMinus.Text = "-"; btnIntervalMinus.Left = 120; btnIntervalMinus.Top = 208; btnIntervalMinus.Width = 34; btnIntervalMinus.Height = 30;
            btnIntervalMinus.Click += delegate { cfg.MonitorInterval = Math.Max(15, cfg.MonitorInterval - 15); RefreshInterval(); };
            lblIntervalValue = MkLabel(cfg.MonitorInterval + " 秒", 9.5f, FontStyle.Regular, 164, 214, "opt");
            btnIntervalPlus = new RoundButton();
            btnIntervalPlus.Text = "+"; btnIntervalPlus.Left = 228; btnIntervalPlus.Top = 208; btnIntervalPlus.Width = 34; btnIntervalPlus.Height = 30;
            btnIntervalPlus.Click += delegate { cfg.MonitorInterval = Math.Min(1800, cfg.MonitorInterval + 15); RefreshInterval(); };
            cardBehave.Controls.Add(btnIntervalMinus); cardBehave.Controls.Add(lblIntervalValue); cardBehave.Controls.Add(btnIntervalPlus);

            cardStore = MkCard("升级与存储", 402, 0, 388, 320);
            lblStorePath = MkLabel(AppSettings.FilePath(), 8.5f, FontStyle.Regular, 18, 50, "hint");
            lblStorePath.AutoSize = false; lblStorePath.Width = 350; lblStorePath.Height = 38;
            btnOpenDir = new RoundButton();
            btnOpenDir.Text = "打开配置目录"; btnOpenDir.Left = 18; btnOpenDir.Top = 98; btnOpenDir.Width = 122; btnOpenDir.Height = 34;
            btnOpenDir.Click += delegate { try { Process.Start("explorer.exe", AppSettings.Dir()); } catch { } };
            btnSave = new RoundButton();
            btnSave.Text = "保存设置"; btnSave.Primary = true;
            btnSave.Left = 150; btnSave.Top = 98; btnSave.Width = 122; btnSave.Height = 34;
            btnSave.Click += delegate { SaveSettings(true); };
            btnCleanup = new RoundButton();
            btnCleanup.Text = "清理旧版本"; btnCleanup.Left = 18; btnCleanup.Top = 142; btnCleanup.Width = 122; btnCleanup.Height = 34;
            btnCleanup.Click += delegate { DoCleanupOld(); };
            cardStore.Controls.Add(lblStorePath); cardStore.Controls.Add(btnOpenDir);
            cardStore.Controls.Add(btnSave); cardStore.Controls.Add(btnCleanup);

            RoundPanel cardUpgrade = MkCard("如何升级到新版本", 402, 336, 388, 262);
            string[] ups = new string[] {
                "1. 下载新版压缩包并解压到新文件夹",
                "2. 直接用新版 exe 覆盖旧版（配置保留）",
                "3. 或点【清理旧版本】删除历史版本",
                "4. 覆盖后打开新版，设置自动沿用",
                "",
                "本工具只改用户级配置，升级不丢修复结果。"
            };
            int uy = 50;
            foreach (string s in ups) { cardUpgrade.Controls.Add(MkLabel(s, 9f, FontStyle.Regular, 18, uy, "opt")); uy += 30; }

            pageSettings.Controls.Add(cardLook); pageSettings.Controls.Add(cardBehave);
            pageSettings.Controls.Add(cardStore); pageSettings.Controls.Add(cardUpgrade);
        }

        void FillBullets(RoundPanel card, string[] lines, int width, int step)
        {
            int y = 46;
            foreach (string line in lines)
            {
                Label l = MkLabel("•  " + line, 8.5f, FontStyle.Regular, 18, y, "opt");
                l.AutoSize = false; l.Width = width; l.Height = step;
                card.Controls.Add(l);
                y += step + 2;
            }
        }

        void BuildAboutPage()
        {
            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Left = 0; flow.Top = 150; flow.Width = 790; flow.Height = 292;
            flow.AutoScroll = true; flow.FlowDirection = FlowDirection.LeftToRight; flow.WrapContents = true;
            flow.Tag = "flowbg"; flow.Name = "versionFlow";
            foreach (VersionLog v in AppVersion.All())

            {
                RoundPanel c = MkCard((v.Bate ? "测试版 · " : "正式版 · ") + v.Version, 0, 0, 768, 150);
                c.Margin = new Padding(0, 0, 0, 12);
                FillBullets(c, v.Items, 726, 21);
                flow.Controls.Add(c);
            }
            // 作者有话说：固定钉在更新日志页顶部（不随版本列表滚动）
            RoundPanel authorFixed = MkCard("作者有话说", 0, 0, 790, 138);
            Label noteFixed = MkLabel(AppVersion.AuthorNote(), 9f, FontStyle.Regular, 20, 50, "opt");
            noteFixed.AutoSize = false; noteFixed.Width = 748; noteFixed.Height = 80;
            authorFixed.Controls.Add(noteFixed);
            pageAbout.Controls.Add(authorFixed);
            pageAbout.Controls.Add(flow);

            cardHistory = MkCard("最近修复记录", 0, 452, 790, 190);
            RoundButton clr = new RoundButton();
            clr.Text = "清空记录"; clr.Ghost = true;
            clr.Width = 86; clr.Height = 28; clr.Left = cardHistory.Width - 104; clr.Top = 10;
            clr.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clr.Click += delegate
            {
                if (MessageBox.Show("确定清空全部修复记录吗？", "清空记录", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                History.Clear();
                RefreshHistory();
                SetStatus("修复记录已清空", pal.Ok);
            };
            cardHistory.Controls.Add(clr);

            FlowLayoutPanel h = new FlowLayoutPanel();
            h.Left = 18; h.Top = 46; h.Width = 754; h.Height = 128;
            h.AutoScroll = true; h.FlowDirection = FlowDirection.TopDown; h.WrapContents = false;
            h.Tag = "flowbg"; h.Name = "historyList";
            h.Resize += delegate { foreach (Control c in h.Controls) c.Width = h.ClientSize.Width - 8; };
            cardHistory.Controls.Add(h);
            pageAbout.Controls.Add(cardHistory);
        }        void BuildBottom()
        {
            Panel bar = new Panel();
            bar.Height = 32; bar.Tag = "bottom"; bottomBar = bar;
            Label st = new Label();
            st.Name = "statusLabel"; st.AutoSize = false; st.Dock = DockStyle.Fill;
            st.TextAlign = ContentAlignment.MiddleLeft; st.Padding = new Padding(28, 0, 0, 0);
            st.Font = Draw.Ui(9f, FontStyle.Regular); st.Tag = "hint"; st.Text = "就绪";
            lblVersion = new Label();
            lblVersion.Text = AppVersion.Current;
            lblVersion.AutoSize = false; lblVersion.Dock = DockStyle.Right; lblVersion.Width = 160;
            lblVersion.TextAlign = ContentAlignment.MiddleRight; lblVersion.Padding = new Padding(0, 0, 26, 0);
            lblVersion.Font = new Font("Consolas", 9.5f); lblVersion.Tag = "version";
            AttachDrag(bar); AttachDrag(st);
            bar.Controls.Add(st); bar.Controls.Add(lblVersion);
            Controls.Add(bar);
        }

        void BindSettings()
        {
            swCodex.Checked = cfg.OptCodex; swUserEnv.Checked = cfg.OptUserEnv;
            swGit.Checked = cfg.OptGit; swGitExec.Checked = cfg.OptGitExec;
            swStartCheck.Checked = cfg.CheckOnStart; swMonitor.Checked = cfg.Monitor; swTray.Checked = cfg.TrayResident;
            if (swAnim != null) swAnim.Checked = cfg.EnableAnim;
            Anim.Enabled = cfg.EnableAnim;
            if (txtPort != null && cfg.LastPort > 0) txtPort.Text = cfg.LastPort.ToString();
            RefreshInterval();
        }

        void RefreshInterval()
        {
            if (lblIntervalValue != null) lblIntervalValue.Text = cfg.MonitorInterval + " 秒";
        }

        void OnLoaded()
        {
            History.Compact();
            AppendLog(AppVersion.Current + "  已启动");
            AppendLog("CODEX_HOME = " + Core.CodexHome());
            AppendLog("使用步骤：① 启动代理软件 → ②【一键修复并部署】→ ③【重启 Codex】→ ④【全面自检】");
            AppendLog("");
            string warn = Core.ProbeEnvironment();
            if (warn.Length > 0) { AppendLog("⚠ " + warn); SetStatus(warn, pal.Fail); }
            if (Program.StartPage != "repair") ShowPage(Program.StartPage);
            if (txtPort.Text.Trim().Length == 0) DoDetect();
            StartMonitorIfNeeded();
            if (cfg.CheckOnStart) DoCheck(false);
            StyleNav();
            if (topBar != null) topBar.Invalidate();
            ShowFirstRunBanner();
        }
        RoundPanel banner;
        RoundPanel redPill;

        void ShowFirstRunBanner()
        {
            if (cfg.SuppressUpdateTip) { if (redPill != null) redPill.Visible = true; return; }
            if (cfg.LastSeenVersion == AppVersion.Num) return;
            banner = new RoundPanel();
            banner.Radius = 12; banner.Tag = "banner"; banner.Fill = pal.AccentSoft; banner.Edge = pal.Accent;
            banner.Height = 58; banner.Width = contentHost.Width - 36; banner.Left = 18; banner.Top = 10;
            banner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Label t = new Label();
            t.Text = "已更新到 " + AppVersion.Current + "：" + AppVersion.CurrentChanges()[0];
            t.Font = Draw.Ui(9.5f, FontStyle.Bold); t.ForeColor = pal.Text; t.BackColor = pal.AccentSoft;
            t.AutoSize = false; t.Left = 16; t.Top = 9; t.Width = banner.Width - 250; t.Height = 20;
            Label s = new Label();
            s.Text = "升级方式：用新版 exe 覆盖旧版即可；更多改进见【更新日志】";
            s.Font = Draw.Ui(8.5f, FontStyle.Regular); s.ForeColor = pal.TextSub; s.BackColor = pal.AccentSoft;
            s.AutoSize = false; s.Left = 16; s.Top = 30; s.Width = banner.Width - 250; s.Height = 18;
            RoundButton go = new RoundButton();
            go.Text = "查看日志"; go.Ghost = true; go.Width = 92; go.Height = 30;
            go.Left = banner.Width - 318; go.Top = 14; go.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            go.Click += delegate { ShowPage("about"); };
            RoundButton never = new RoundButton();
            never.Text = "不再提醒"; never.Ghost = true; never.Width = 92; never.Height = 30;
            never.Left = banner.Width - 116; never.Top = 14; never.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            never.Click += delegate
            {
                cfg.LastSeenVersion = AppVersion.Num; cfg.SuppressUpdateTip = true; cfg.Save();
                if (banner != null) { banner.Visible = false; banner.Dispose(); banner = null; }
                if (redPill != null) redPill.Visible = true;
                SetStatus("已关闭更新提醒；可点左侧红色【更新提示】查看", pal.TextSub);
                AdjustPageTop(0);
            };            RoundButton ok = new RoundButton();
            ok.Text = "我知道了"; ok.Primary = true; ok.Width = 108; ok.Height = 30;
            ok.Left = banner.Width - 222; ok.Top = 14; ok.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            ok.Click += delegate
            {
                cfg.LastSeenVersion = AppVersion.Num; cfg.Save();
                if (banner != null) { banner.Visible = false; banner.Dispose(); banner = null; }
                AdjustPageTop(0);
            };
            banner.Controls.Add(t); banner.Controls.Add(s); banner.Controls.Add(go); banner.Controls.Add(ok); banner.Controls.Add(never);
            contentHost.Controls.Add(banner);
            banner.BringToFront();
            StyleTree(banner);
        }
        void SaveSettings(bool notify)
        {
            cfg.OptCodex = swCodex.Checked; cfg.OptUserEnv = swUserEnv.Checked;
            cfg.OptGit = swGit.Checked; cfg.OptGitExec = swGitExec.Checked;
            cfg.CheckOnStart = swStartCheck.Checked; cfg.Monitor = swMonitor.Checked; cfg.TrayResident = swTray.Checked;
            if (swAnim != null) { cfg.EnableAnim = swAnim.Checked; Anim.Enabled = cfg.EnableAnim; }
            int p; if (int.TryParse(txtPort.Text.Trim(), out p)) cfg.LastPort = p;
            cfg.ThemeMode = "light";
            cfg.AccentKey = accent.Key;
            cfg.Save();
            StartMonitorIfNeeded();
            if (notify) { SetStatus("设置已保存到 " + AppSettings.FilePath(), pal.Ok); History.Add("保存设置", "成功", accent.Name + " / " + cfg.ThemeMode); }
        }


        void SetAccent(string key)
        {
            accent = AccentPreset.Get(key);
            cfg.AccentKey = key;
            pal = Palette.Create(false, accent);
            if (lblAccentName != null) lblAccentName.Text = "当前：" + accent.Name;
            if (swatches != null)
                foreach (RoundButton b in swatches)
                    if (b != null)
                    {
                        string k = b.Tag.ToString().Replace("swatch:", "");
                        b.SwatchSelected = (k == key);
                        b.Invalidate();
                    }
            ApplyTheme();
            SaveSettings(false);
        }

        void ApplyTheme()
        {
            BackColor = pal.Bg;
            if (logBox != null) { logBox.BackColor = pal.Card; logBox.ForeColor = pal.TextSub; }
            if (checkList != null) checkList.BackColor = pal.Card;
            StyleTree(this);
            foreach (Control c in Controls) if (c is Panel && c.Tag != null && c.Tag.ToString() == "bottom") c.BackColor = pal.Bg;
            Invalidate(true);
        }

        void StyleTree(Control root)
        {
            foreach (Control c in root.Controls)
            {
                string tag = c.Tag == null ? "" : c.Tag.ToString();
                if (c is RoundPanel)
                {
                    RoundPanel rp = (RoundPanel)c;
                    if (tag == "navbox" || tag == "redpill") { /* 由 StyleNav / 固定色负责，勿覆盖 */ }
                    else if (tag == "row") { rp.Fill = pal.CardAlt; rp.Edge = pal.Edge; rp.BackColor = rp.Fill; }
                    else if (tag == "banner") { rp.Fill = pal.AccentSoft; rp.Edge = pal.Accent; rp.BackColor = rp.Fill; }
                    else if (tag == "fieldwrap") { rp.Fill = pal.Field; rp.Edge = pal.Edge; rp.BackColor = rp.Fill; }
                    else { rp.Fill = pal.Card; rp.Edge = pal.Edge; rp.BackColor = rp.Fill; }
                }
                else if (c is RoundButton)
                {
                    RoundButton rb = (RoundButton)c;
                    rb.Theme = pal;
                    if (tag == "nav")
                    {
                        bool active = IsActiveTab(rb);
                        rb.Primary = active;
                        rb.Ghost = !active;
                        rb.TransparentPaint = !active;
                        rb.TextOverride = active ? pal.AccentText : Color.White;
                    }
                    else if (tag == "winbtn" || tag == "winclose")
                    {
                        rb.Ghost = true; rb.CustomFill = Color.Empty; rb.TextOverride = Color.White;
                        // 悬停：半透明高亮（最小化=极淡白，关闭=淡红）
                        if (rb.Tag != null && rb.Tag.ToString() == "winclose") { rb.HoverFillColor = Color.FromArgb(255, 232, 82, 82); rb.HoverAlpha = 60; }
                        else { rb.HoverFillColor = Color.White; rb.HoverAlpha = 24; }
                    }
                    else rb.TextOverride = Color.Empty;
                }
                else if (c is SwitchBox) ((SwitchBox)c).Theme = pal;
                else if (c is StatusPill) ((StatusPill)c).Theme = pal;
                else if (c is Label)
                {
                    Label lb = (Label)c;
                    if (tag == "ontopbar") { lb.ForeColor = Color.White; lb.BackColor = Color.Transparent; }
                    else
                    {
                        if (tag == "title") lb.ForeColor = pal.Text;
                        else if (tag == "sub" || tag == "hint") lb.ForeColor = pal.TextSub;
                        else if (tag == "version") lb.ForeColor = pal.TextFaint;
                        else lb.ForeColor = pal.Text;
                        lb.BackColor = Draw.EffectiveBack(lb);
                    }
                }
                else if (c is TextBox)
                {
                    TextBox tb = (TextBox)c;
                    if (c == logBox) { tb.BackColor = pal.Card; tb.ForeColor = pal.TextSub; }
                    else { tb.BackColor = pal.Field; tb.ForeColor = pal.Text; }
                }
                else if (c is FlowLayoutPanel) c.BackColor = (tag == "flowbg") ? pal.ContentBg : pal.Card;
                else if (c is Panel)
                {
                    Panel pn = (Panel)c;
                    if (tag == "sep") pn.BackColor = pal.Edge;
                    else if (tag == "sidebar") pn.BackColor = pal.Sidebar;
                    else if (tag == "content") pn.BackColor = pal.ContentBg;
                    else if (tag == "page") pn.BackColor = pal.ContentBg;
                    else if (tag == "topbar") { pn.BackColor = pal.Accent; }
                    else pn.BackColor = pal.Bg;
                }
                if (c.HasChildren) StyleTree(c);
            }
        }
        bool IsActiveTab(RoundButton b)
        {
            return false;
        }
        void ShowPage(string key)
        {
            currentPage = key;
            pageRepair.Visible = key == "repair";
            pageCheck.Visible = key == "check";
            pageAbout.Visible = key == "about";
            pageSettings.Visible = key == "settings";
            if (key == "about") RefreshHistory();
            StyleTree(content);
            if (topBar != null) StyleTree(topBar);
            StyleNav();
            AnimatePill();
            Invalidate(true);
        }
        void AdjustPageTop(int offset)
        {
            foreach (Panel pg in new Panel[] { pageRepair, pageCheck, pageAbout, pageSettings })
                if (pg != null) pg.Padding = new Padding(0, offset, 0, 0);
        }

        void RefreshHistory()
        {
            if (cardHistory == null) return;
            Control h = null;
            foreach (Control c in cardHistory.Controls) if (c.Name == "historyList") h = c;
            if (h == null) return;
            h.Controls.Clear();
            List<string> items = History.Recent(40);
            if (items.Count == 0)
            {
                Label e0 = MkLabel("暂无记录（执行修复 / 还原 / 清理等操作后会记录在这里）", 9f, FontStyle.Regular, 4, 4, "hint");
                e0.AutoSize = false; e0.Width = h.ClientSize.Width - 8; e0.Height = 20;
                h.Controls.Add(e0);
                return;
            }
            foreach (string s in items)
            {
                Label l = MkLabel(s, 9f, FontStyle.Regular, 4, 4, "hint");
                l.AutoSize = false;
                l.Width = h.ClientSize.Width - 8; l.Height = 20;
                h.Controls.Add(l);
            }
        }
        // ---------- 状态与日志 ----------
        void UpdateSide(string status, string sub, string pill)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { UpdateSide(status, sub, pill); })); return; }
            if (lblSideStatus != null) lblSideStatus.Text = status;
            if (lblSideSub != null) lblSideSub.Text = sub;
            if (sidebar != null)
                foreach (Control c in sidebar.Controls)
                    if (c is StatusPill && c.Name == "sidePill") { ((StatusPill)c).Status = pill; c.Invalidate(); }
        }

        void SetStatus(string s, Color c)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { SetStatus(s, c); })); return; }
            foreach (Control ctl in Controls)
                if (ctl is Panel && ctl.Tag != null && ctl.Tag.ToString() == "bottom")
                    foreach (Control inner in ctl.Controls)
                        if (inner is Label && inner.Name == "statusLabel") { inner.Text = s; inner.ForeColor = c; }
        }

        void AppendLog(string line)
        {
            if (InvokeRequired) { BeginInvoke(new Action(delegate { AppendLog(line); })); return; }
            runLog.Add(line);
            if (logBox != null) logBox.AppendText(line + "\r\n");
        }

        int SelectedPort()
        {
            int p;
            if (txtPort != null && txtPort.Text.Trim().Length > 0 && int.TryParse(txtPort.Text.Trim(), out p)) return p;
            return 0;
        }

        // ---------- 动作 ----------
        void DoDetect()
        {
            SetStatus("正在探测本机代理端口…", pal.Warn);
            Thread t = new Thread(delegate ()
            {
                List<string> lg = new List<string>();
                int p = Core.DetectProxyPort(lg);
                BeginInvoke(new Action(delegate
                {
                    foreach (string l in lg) AppendLog(l);
                    if (p > 0)
                    {
                        txtPort.Text = p.ToString(); cfg.LastPort = p; cfg.Save();
                        SetStatus("已探测到可用代理端口 127.0.0.1:" + p, pal.Ok);
                        UpdateSide("已就绪", "代理端口 127.0.0.1:" + p, "OK");
                        AppendLog("✓ 自动探测结果: 127.0.0.1:" + p);
                        History.Add("端口探测", "成功", "127.0.0.1:" + p);
                    }
                    else
                    {
                        SetStatus("未探测到可用代理：请先启动代理软件，或手动填写端口", pal.Fail);
                        UpdateSide("未就绪", "未发现可用代理端口", "FAIL");
                        AppendLog("✗ 未探测到可用代理端口。");
                        History.Add("端口探测", "失败", "未发现可用端口");
                    }
                }));
            });
            t.IsBackground = true; t.Start();
        }

        void DoRepair()
        {
            SaveSettings(false);
            RepairOptions o = new RepairOptions();
            o.Port = SelectedPort();
            o.PatchCodexHome = swCodex.Checked;
            o.WriteUserEnvVars = swUserEnv.Checked;
            o.PatchGitTls = swGit.Checked;
            o.PatchGitExecPath = swGitExec.Checked;
            runLog.Clear(); if (logBox != null) logBox.Clear();
            SetStatus("正在修复…", pal.Warn);
            btnFix.Enabled = false;
            Thread t = new Thread(delegate ()
            {
                List<string> lg = new List<string>();
                int used = 0;
                try { used = Core.Repair(o, lg); }
                catch (Exception ex) { lg.Add("异常: " + ex.Message); }
                BeginInvoke(new Action(delegate
                {
                    foreach (string l in lg) AppendLog(l);
                    btnFix.Enabled = true;
                    if (used > 0)
                    {
                        txtPort.Text = used.ToString(); cfg.LastPort = used; cfg.Save();
                        SetStatus("修复完成（端口 " + used + "）。建议点【重启 Codex】后再【全面自检】。", pal.Ok);
                        UpdateSide("已修复", "端口 " + used + " · 已写入配置", "OK");
                        History.Add("一键修复", "成功", "端口 " + used);
                    }
                    else
                    {
                        SetStatus("修复未完成：请确认代理软件已启动", pal.Fail);
                        History.Add("一键修复", "失败", "未找到可用代理");
                    }
                }));
            });
            t.IsBackground = true; t.Start();
        }

        void DoCheck(bool deep)
        {
            bool d = deep || (swDeep != null && swDeep.Checked);
            SetStatus("正在全面自检…", pal.Warn);
            Thread t = new Thread(delegate ()
            {
                List<CheckItem> items = new List<CheckItem>();
                try { items = Core.SelfCheck(SelectedPort(), d, null); }
                catch (Exception ex) { items.Add(CheckItem.Make("自检异常", "FAIL", ex.Message, "")); }
                BeginInvoke(new Action(delegate
                {
                    RenderChecks(items);
                    int okc = 0, wn = 0, fl = 0;
                    foreach (CheckItem c in items)
                    {
                        if (c.Status == "OK") okc++; else if (c.Status == "WARN") wn++; else if (c.Status == "FAIL") fl++;
                    }
                    lblCheckSum.Text = "OK " + okc + " · 警告 " + wn + " · 失败 " + fl;
                    SetStatus(fl == 0 ? ("自检通过：OK " + okc + "，警告 " + wn) : ("自检发现 " + fl + " 项失败，请按建议处理"), fl == 0 ? pal.Ok : pal.Fail);
                    AppendLog("—— 自检完成：OK " + okc + " / 警告 " + wn + " / 失败 " + fl + " ——");
                    History.Add("全面自检", fl == 0 ? "通过" : "有失败项", "OK=" + okc + " WARN=" + wn + " FAIL=" + fl);
                }));
            });
            t.IsBackground = true; t.Start();
        }

        void RenderChecks(List<CheckItem> items)
        {
            checkList.Controls.Clear();
            int idx = 0;
            foreach (CheckItem c in items)
            {
                RoundPanel row = new RoundPanel();
                row.Width = checkList.Width - 26; row.Height = 44; row.Radius = 10;
                row.Fill = (idx % 2 == 0) ? pal.CardAlt : pal.Card; row.Edge = pal.Edge;
                row.BackColor = row.Fill; row.Tag = "row"; row.Margin = new Padding(0, 0, 0, 6);
                StatusPill pill = new StatusPill();
                pill.Status = c.Status; pill.Theme = pal; pill.Left = 14; pill.Top = 10;
                Label nm = MkLabel(c.Name, 9.5f, FontStyle.Bold, 86, 13, "opt");
                nm.AutoSize = false; nm.Width = 140; nm.Height = 20;
                Label dt = MkLabel(c.Detail + (c.Hint.Length > 0 ? "   ⇒ " + c.Hint : ""), 9f, FontStyle.Regular, 236, 14, "hint");
                dt.AutoSize = false; dt.Width = Math.Max(200, row.Width - 256); dt.Height = 20;
                row.Controls.Add(pill); row.Controls.Add(nm); row.Controls.Add(dt);
                checkList.Controls.Add(row);
                idx++;
            }
        }        void DoExport()
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = "Codex网络诊断报告-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            sfd.Filter = "文本文件|*.txt";
            if (sfd.ShowDialog() != DialogResult.OK) return;
            List<CheckItem> items = Core.SelfCheck(SelectedPort(), false, null);
            File.WriteAllText(sfd.FileName, Core.ReportText(SelectedPort(), items, runLog), new UTF8Encoding(true));
            SetStatus("报告已保存: " + sfd.FileName, pal.Ok);
            History.Add("导出报告", "成功", sfd.FileName);
        }

        void DoCopyReport()
        {
            try
            {
                List<CheckItem> items = Core.SelfCheck(SelectedPort(), false, null);
                string txt = Core.ReportText(SelectedPort(), items, runLog);
                if (txt.Length > 0) Clipboard.SetText(txt);
                SetStatus("诊断报告已复制到剪贴板", pal.Ok);
                History.Add("复制报告", "成功", "已复制到剪贴板");
            }
            catch (Exception ex) { SetStatus("复制失败: " + ex.Message, pal.Fail); }
        }

        void DoRollback()
        {
            if (MessageBox.Show("将把 config.toml / .env / .gitconfig 还原为最近一次修复前的备份（工具新建的文件会被删除）。确定继续吗？",
                "还原备份", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            runLog.Clear(); if (logBox != null) logBox.Clear();
            List<string> lg = new List<string>();
            Core.Rollback(lg);
            foreach (string l in lg) AppendLog(l);
            SetStatus("还原完成，建议重启 Codex。", pal.Ok);
            History.Add("还原备份", "完成", lg.Count + " 条操作");
        }

        void DoClearEnv()
        {
            if (MessageBox.Show("将删除用户级环境变量中的代理设置。确定继续吗？", "清除环境变量",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            List<string> lg = new List<string>();
            Core.RollbackUserEnv(lg);
            foreach (string l in lg) AppendLog(l);
            SetStatus("已清除用户级代理环境变量。", pal.Ok);
            History.Add("清除环境变量", "完成", "");
        }

        void DoRestart()
        {
            if (MessageBox.Show("将关闭所有 Codex / ChatGPT 进程并重新启动桌面应用。\r\n\r\n请先确认没有未保存的工作。确定继续吗？",
                "重启 Codex", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            SetStatus("正在重启 Codex…", pal.Warn);
            Thread t = new Thread(delegate ()
            {
                List<string> lg = new List<string>();
                string r = Core.RestartCodex(lg, false);
                BeginInvoke(new Action(delegate
                {
                    foreach (string l in lg) AppendLog(l);
                    SetStatus(r == "OK" ? "Codex 已重启" : "Codex 已关闭，请手动启动", r == "OK" ? pal.Ok : pal.Warn);
                    History.Add("重启 Codex", r == "OK" ? "成功" : "部分完成", "");
                }));
            });
            t.IsBackground = true; t.Start();
        }

        void DoCleanupOld()
        {
            List<Core.OldCopy> items = Core.FindOldCopies();
            if (items.Count == 0)
            {
                MessageBox.Show("没有发现旧版本文件，当前已是唯一版本。", "清理旧版本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StringBuilder sb = new StringBuilder();
            foreach (Core.OldCopy o in items) sb.AppendLine((o.IsDir ? "[文件夹] " : "[文件]   ") + o.Path);
            string msg = "发现以下旧版本，与新版共存不会自动消失：\r\n\r\n" + sb.ToString() + "\r\n是否立即删除？（不可恢复）";
            if (MessageBox.Show(msg, "清理旧版本", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            List<string> lg = new List<string>();
            int n = Core.DeleteOldCopies(items, lg);
            foreach (string l in lg) AppendLog(l);
            SetStatus("已清理 " + n + " 项旧版本", pal.Ok);
            History.Add("清理旧版本", n > 0 ? "成功" : "未删除", n + " 项");
        }

        void ShowStartupDialogs()
        {
            try
            {
                if (cfg.LastSeenVersion != AppVersion.Num)
                {
                    using (ChangelogDialog dlg = new ChangelogDialog(pal, AppVersion.Current, AppVersion.CurrentChanges()))
                        dlg.ShowDialog(this);
                    cfg.LastSeenVersion = AppVersion.Num;
                    cfg.Save();
                    return;
                }
                List<Core.OldCopy> olds = Core.FindOldCopies();
                if (olds.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (Core.OldCopy o in olds) sb.AppendLine((o.IsDir ? "[文件夹] " : "[文件]   ") + o.Path);
                    string msg = "检测到 " + olds.Count + " 个旧版本，与新版共存时容易混淆：\r\n\r\n" + sb.ToString()
                        + "\r\n是否立即删除这些旧版本？（不可恢复；当前版本不会被删除）\r\n\r\n"
                        + "升级方式：以后下载新版后，直接用新版 exe 覆盖旧版，或点【设置 → 清理旧版本】。";
                    if (MessageBox.Show(msg, "发现旧版本", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        List<string> cl = new List<string>();
                        int dn = Core.DeleteOldCopies(olds, cl);
                        foreach (string l in cl) AppendLog(l);
                        History.Add("清理旧版本", "成功", dn + " 项");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("启动提示失败: " + ex.Message);
                try { File.AppendAllText(Path.Combine(AppSettings.Dir(), "startup.log"), DateTime.Now.ToString() + " " + ex.ToString() + "\r\n", new UTF8Encoding(false)); } catch { }
            }
        }

        void OpenDoc()
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "使用说明.md");
                if (File.Exists(p)) Process.Start("notepad.exe", "\"" + p + "\"");
                else MessageBox.Show("未找到 使用说明.md", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch { }
        }

        // ---------- 托盘与后台监控 ----------
        void BuildTray()
        {
            tray = new NotifyIcon();
            try { tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            tray.Text = "Codex 网络修复工具 " + AppVersion.Current;
            tray.Visible = false;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            menu.Items.Add("立即自检", null, delegate { Show(); ShowPage("check"); DoCheck(true); });
            menu.Items.Add("退出", null, delegate { cfg.TrayResident = false; SaveSettings(false); tray.Visible = false; Close(); Application.Exit(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };

            monitorTimer = new System.Windows.Forms.Timer();
            monitorTimer.Interval = Math.Max(15, cfg.MonitorInterval) * 1000;
            monitorTimer.Tick += delegate { MonitorTick(); };
        }

        void StartMonitorIfNeeded()
        {
            if (tray == null || monitorTimer == null) return;
            tray.Visible = cfg.TrayResident || cfg.Monitor;
            monitorTimer.Interval = Math.Max(15, cfg.MonitorInterval) * 1000;
            if (cfg.Monitor) monitorTimer.Start(); else monitorTimer.Stop();
        }

        void MonitorTick()
        {
            if (busyMonitor) return;
            busyMonitor = true;
            int port = SelectedPort();
            if (port <= 0) port = cfg.LastPort;
            Thread t = new Thread(delegate ()
            {
                string detail;
                bool ok = Core.QuickProbe(port, out detail);
                BeginInvoke(new Action(delegate
                {
                    busyMonitor = false;
                    if (ok != lastOnline)
                    {
                        lastOnline = ok;
                        string msg = ok ? ("代理已恢复：127.0.0.1:" + port + " 可正常出网") : ("代理不可用：" + detail);
                        SetStatus(msg, ok ? pal.Ok : pal.Fail);
                        if (tray != null && tray.Visible)
                            tray.ShowBalloonTip(3000, "Codex 网络修复工具", msg, ok ? ToolTipIcon.Info : ToolTipIcon.Warning);
                        History.Add("后台监控", ok ? "恢复" : "掉线", detail);
                    }
                }));
            });
            t.IsBackground = true; t.Start();
        }
    }

    // 首次启动/版本更新时的更新日志弹窗（无边框圆角）
    public class ChangelogDialog : Form
    {
        public ChangelogDialog(Palette pal, string version, string[] lines)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1120, 780);
            BackColor = pal.Card;
            Font = Draw.Ui(9.5f, FontStyle.Regular);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label t = new Label();
            t.Text = "本次更新 · " + version;
            t.Font = Draw.Ui(13f, FontStyle.Bold); t.ForeColor = pal.Text;
            t.AutoSize = true; t.Left = 28; t.Top = 26;
            Label sub = new Label();
            sub.Text = "感谢使用！以下是本次新增与修复内容：";
            sub.Font = Draw.Ui(9f, FontStyle.Regular); sub.ForeColor = pal.TextSub;
            sub.AutoSize = true; sub.Left = 30; sub.Top = 56;
            Controls.Add(t); Controls.Add(sub);

            int y = 92;
            foreach (string line in lines)
            {
                Label l = new Label();
                l.Text = "•  " + line;
                l.Font = Draw.Ui(9.5f, FontStyle.Regular); l.ForeColor = pal.Text;
                l.AutoSize = false; l.Left = 30; l.Top = y; l.Width = 500; l.Height = 24;
                Controls.Add(l);
                y += 26;
            }

            RoundButton ok = new RoundButton();
            ok.Text = "我知道了"; ok.Primary = true; ok.Theme = pal;
            ok.Width = 120; ok.Height = 38; ok.Left = Size.Width - 150; ok.Top = Size.Height - 62;
            ok.Click += delegate { Close(); };
            Controls.Add(ok);

            MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };
            Shown += delegate
            {
                using (System.Drawing.Drawing2D.GraphicsPath gp = Draw.Rounded(new Rectangle(0, 0, Width, Height), 16))
                    Region = new Region(gp);
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Draw.Smooth(e.Graphics);
            using (System.Drawing.Drawing2D.GraphicsPath gp = Draw.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 16))
            using (Pen pen = new Pen(Color.FromArgb(40, 0, 0, 0)))
                e.Graphics.DrawPath(pen, gp);
        }
    }
    public static class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint GetConsoleOutputCP();

        public static string StartPage = "repair";

        [STAThread]
        public static void Main(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--page") StartPage = args[i + 1];
            bool cli = args.Length > 0 && args[0] == "--cli";
            if (!cli)
            {
                try { IntPtr h = GetConsoleWindow(); if (h != IntPtr.Zero) ShowWindow(h, 0); } catch { }
            }
            if (cli) { Cli(args); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { LogError(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject as Exception); };
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogError(ex);
                MessageBox.Show("程序启动失败，详情已写入:\r\n" + Path.Combine(AppSettings.Dir(), "error.log") + "\r\n\r\n" + ex.Message,
                    "Codex 网络修复工具", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void LogError(Exception ex)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====");
                sb.AppendLine(ex == null ? "(null)" : ex.ToString());
                sb.AppendLine();
                File.AppendAllText(Path.Combine(AppSettings.Dir(), "error.log"), sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
        static void Cli(string[] args)
        {
            try
            {
                uint cp = GetConsoleOutputCP();
                if (cp != 0) Console.OutputEncoding = Encoding.GetEncoding((int)cp);
            }
            catch { }
            string action = args.Length > 1 ? args[1] : "check";
            List<string> lg = new List<string>();

            if (action == "version") { Console.WriteLine("VERSION=" + AppVersion.Current); return; }

            if (action == "detect")
            {
                int p = Core.DetectProxyPort(lg);
                foreach (string l in lg) Console.WriteLine(l);
                Console.WriteLine(p > 0 ? ("PORT=" + p) : "PORT=NOT_FOUND");
                return;
            }
            if (action == "repair")
            {
                RepairOptions o = new RepairOptions();
                if (args.Length > 2) { int p; if (int.TryParse(args[2], out p)) o.Port = p; }
                int used = Core.Repair(o, lg);
                foreach (string l in lg) Console.WriteLine(l);
                Console.WriteLine("RESULT=" + (used > 0 ? "OK:" + used : "FAILED"));
                return;
            }
            if (action == "rollback")
            {
                Core.Rollback(lg);
                foreach (string l in lg) Console.WriteLine(l);
                Console.WriteLine("RESULT=ROLLED_BACK");
                return;
            }
            if (action == "e2e")
            {
                int ep = Core.DetectProxyPort(null);
                string cx = Core.FindCodexExe();
                if (cx.Length == 0) { Console.WriteLine("E2E=NO_CODEX"); return; }
                string outp2 = Core.RunSandboxProbe(cx, lg);
                foreach (string l in lg) Console.WriteLine(l);
                Console.WriteLine(outp2);
                bool hasProxy = outp2.IndexOf("HTTP_PROXY=http://127.0.0.1:") >= 0;
                bool netOk = outp2.IndexOf("GIT=OK") >= 0 || System.Text.RegularExpressions.Regex.IsMatch(outp2, "NODE=[0-9]{3}");
                bool relayOk = outp2.IndexOf("RELAY=200") >= 0;
                Console.WriteLine("RESULT=" + ((hasProxy && netOk && relayOk) ? "OK" : "FAIL") + " PORT=" + ep);
                return;
            }
            if (action == "restart-list")
            {
                List<Core.ProcInfo> ps = Core.CodexProcesses();
                foreach (Core.ProcInfo p in ps) Console.WriteLine("PROC=" + p.Name + " PID=" + p.Id);
                Console.WriteLine("COUNT=" + ps.Count);
                return;
            }
            if (action == "restart")
            {
                bool yes = false;
                foreach (string a in args) if (a == "--yes") yes = true;
                if (!yes) { Console.WriteLine("需要 --yes 才会真正执行重启（安全保护）"); return; }
                string r = Core.RestartCodex(lg, false);
                foreach (string l in lg) Console.WriteLine(l);
                Console.WriteLine("RESULT=" + r);
                return;
            }
            if (action == "report")
            {
                string outPath = args.Length > 2 ? args[2] : Path.Combine(Environment.CurrentDirectory, "Codex网络诊断报告-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                int dp = Core.DetectProxyPort(null);
                List<CheckItem> its = Core.SelfCheck(dp, true, lg);
                File.WriteAllText(outPath, Core.ReportText(dp, its, lg), new UTF8Encoding(true));
                Console.WriteLine("REPORT=" + Path.GetFullPath(outPath));
                return;
            }
            if (action == "icon")
            {
                string outPath = args.Length > 2 ? args[2] : "app.ico";
                AppIcon.WriteIco(outPath, new int[] { 16, 24, 32, 48, 64, 128, 256 });
                Console.WriteLine("ICON=" + Path.GetFullPath(outPath));
                return;
            }
            if (action == "preview")
            {
                string outPath = args.Length > 2 ? args[2] : "icon-preview.png";
                AppIcon.SavePreview(outPath, 512);
                Console.WriteLine("PNG=" + Path.GetFullPath(outPath));
                return;
            }
            int port = args.Length > 2 ? int.Parse(args[2]) : 0;
            List<CheckItem> items = Core.SelfCheck(port, true, null);
            foreach (CheckItem c in items)
                Console.WriteLine("[" + c.Status + "] " + c.Name + " : " + c.Detail + (c.Hint.Length > 0 ? "  => " + c.Hint : ""));
        }
    }
}