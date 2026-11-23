using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GjBridge
{
    internal sealed class MainForm : Form
    {
        private readonly BridgeHost _bridge = new BridgeHost();
        private NotifyIcon _tray;
        private Label _statusTitle;
        private Label _statusDetail;
        private Label _adbLabel;
        private Panel _dot;
        private System.Windows.Forms.Timer _timer;
        private bool _reallyExit;
        private Icon _appIcon;
        private Image _avatar;

        public MainForm()
        {
            _appIcon = LoadAppIcon();
            _avatar = LoadAvatar();
            Text = Program.AppName;
            Icon = _appIcon;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(400, 420);
            BackColor = Theme.Bg;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            DoubleBuffered = true;

            BuildUi();
            BuildTray();

            Load += OnFormLoad;
            FormClosing += OnFormClosing;
            Resize += OnResize;
        }

        private Icon LoadAppIcon()
        {
            try
            {
                string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(beside)) return new Icon(beside);
            }
            catch { }
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("GjBridge.app.ico"))
                {
                    if (s != null) return new Icon(s);
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        private Image LoadAvatar()
        {
            try
            {
                string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "avatar-round.png");
                if (File.Exists(beside)) return Image.FromFile(beside);
            }
            catch { }
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("GjBridge.avatar.png"))
                {
                    if (s != null) return Image.FromStream(s);
                }
            }
            catch { }
            return null;
        }

        private void BuildUi()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 88;
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (SolidBrush br = new SolidBrush(Theme.Brand))
                    e.Graphics.FillRectangle(br, header.ClientRectangle);
            };
            Controls.Add(header);

            if (_avatar != null)
            {
                PictureBox pic = new PictureBox();
                pic.Image = _avatar;
                pic.SizeMode = PictureBoxSizeMode.Zoom;
                pic.Size = new Size(44, 44);
                pic.Location = new Point(18, 22);
                pic.Cursor = Cursors.Hand;
                pic.Click += delegate { OpenUrl(Program.AuthorGithubUrl); };
                header.Controls.Add(pic);
            }

            int left = _avatar != null ? 74 : 18;
            Label brand = new Label();
            brand.Text = "搞机工具箱";
            brand.ForeColor = Color.White;
            brand.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            brand.Location = new Point(left, 18);
            brand.Size = new Size(300, 28);
            brand.BackColor = Color.Transparent;
            header.Controls.Add(brand);

            Label sub = new Label();
            sub.Text = "本机桥接 · " + Program.Brand + "  v" + Program.Version;
            sub.ForeColor = Color.FromArgb(220, 226, 255);
            sub.Location = new Point(left, 48);
            sub.Size = new Size(300, 22);
            sub.BackColor = Color.Transparent;
            header.Controls.Add(sub);

            Panel card = new Panel();
            card.Location = new Point(18, 106);
            card.Size = new Size(364, 140);
            card.BackColor = Theme.Surface;
            card.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Line))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            Controls.Add(card);

            _dot = new Panel();
            _dot.Size = new Size(10, 10);
            _dot.Location = new Point(18, 22);
            _dot.BackColor = Theme.Muted;
            card.Controls.Add(_dot);

            _statusTitle = new Label();
            _statusTitle.Text = "正在启动…";
            _statusTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            _statusTitle.ForeColor = Theme.Ink;
            _statusTitle.Location = new Point(36, 16);
            _statusTitle.Size = new Size(310, 26);
            card.Controls.Add(_statusTitle);

            _statusDetail = new Label();
            _statusDetail.Text = "启动桥接服务";
            _statusDetail.ForeColor = Theme.Muted;
            _statusDetail.Location = new Point(18, 52);
            _statusDetail.Size = new Size(328, 22);
            card.Controls.Add(_statusDetail);

            _adbLabel = new Label();
            _adbLabel.Text = "ADB：检测中";
            _adbLabel.ForeColor = Theme.Muted;
            _adbLabel.Location = new Point(18, 84);
            _adbLabel.Size = new Size(328, 40);
            card.Controls.Add(_adbLabel);

            Label tip = new Label();
            tip.Text = "保持运行即可。回到网站点「无线连接」。\nUSB 调试不需要本程序。";
            tip.ForeColor = Theme.Muted;
            tip.Location = new Point(18, 258);
            tip.Size = new Size(364, 40);
            Controls.Add(tip);

            Button openSite = MakeBtn("打开搞机工具箱网站", 18, 310, 364, true);
            openSite.Click += delegate { OpenUrl(Program.SiteUrl); };
            Controls.Add(openSite);

            Button hideBtn = MakeBtn("隐藏到托盘", 18, 356, 176, false);
            hideBtn.Click += delegate { HideToTray(); };
            Controls.Add(hideBtn);

            Button aboutBtn = MakeBtn("关于 / 开源", 206, 356, 176, false);
            aboutBtn.Click += delegate { ShowAboutLite(); };
            Controls.Add(aboutBtn);

            LinkLabel foot = new LinkLabel();
            foot.Text = "@" + Program.AuthorGithub + " · 开源仓库";
            foot.LinkColor = Theme.Brand;
            foot.ActiveLinkColor = Theme.BrandDark;
            foot.Location = new Point(18, 398);
            foot.Size = new Size(364, 18);
            foot.LinkClicked += delegate { OpenUrl(Program.RepoUrl); };
            Controls.Add(foot);
        }

        private Button MakeBtn(string text, int x, int y, int w, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 38);
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            if (primary)
            {
                b.BackColor = Theme.Brand;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = Theme.BrandDark;
            }
            else
            {
                b.BackColor = Theme.Surface;
                b.ForeColor = Theme.Brand;
                b.FlatAppearance.BorderColor = Theme.BrandSoft;
            }
            return b;
        }

        private void BuildTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = _appIcon;
            _tray.Text = Program.AppName;
            _tray.Visible = true;
            _tray.DoubleClick += delegate
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开网站", null, delegate { OpenUrl(Program.SiteUrl); });
            menu.Items.Add("显示窗口", null, delegate
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            });
            menu.Items.Add("关于 / 开源", null, delegate { Show(); ShowAboutLite(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Quit(); });
            _tray.ContextMenuStrip = menu;
        }

        private void ShowAboutLite()
        {
            MessageBox.Show(
                Program.AppName + "  v" + Program.Version + "\n" +
                "作者：" + Program.AuthorName + "  @" + Program.AuthorGithub + "\n" +
                "邮箱：" + Program.AuthorEmail + "\n" +
                "开源：" + Program.RepoUrl + "\n" +
                "网站：" + Program.SiteUrl + "\n" +
                "许可：" + Program.LicenseName + "\n\n" +
                "按「现状」提供，请仅在已授权设备上使用。",
                "关于 / 开源",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            try
            {
                _bridge.Start();
                RefreshStatus(true);
                ShowBalloon("桥接已启动", "可打开网站使用无线调试");
                OpenUrl(Program.SiteUrl);
            }
            catch (Exception ex)
            {
                _statusTitle.Text = "启动失败";
                _statusDetail.Text = ex.Message;
                _dot.BackColor = Theme.Err;
                MessageBox.Show(ex.Message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 3000;
            _timer.Tick += delegate { RefreshStatus(false); };
            _timer.Start();
        }

        private void RefreshStatus(bool forceAdb)
        {
            if (forceAdb) AdbRunner.Refresh();
            if (_bridge.IsRunning)
            {
                _statusTitle.Text = "桥接运行中";
                _statusDetail.Text = "127.0.0.1:" + _bridge.Port + " · 仅本机";
                _dot.BackColor = Theme.Ok;
                _tray.Text = Program.AppName + " · 运行中";
            }
            else
            {
                _statusTitle.Text = "未运行";
                _statusDetail.Text = _bridge.LastError ?? "桥接未启动";
                _dot.BackColor = Theme.Err;
            }

            if (AdbRunner.AdbOk)
                _adbLabel.Text = "ADB 已就绪";
            else
                _adbLabel.Text = "未找到 adb.exe（请安装 platform-tools）";
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch
            {
                try { Process.Start(url); } catch { }
            }
        }

        private void HideToTray()
        {
            Hide();
            ShowBalloon("后台运行中", "托盘图标可重新打开窗口");
        }

        private void ShowBalloon(string title, string text)
        {
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = text;
                _tray.BalloonTipIcon = ToolTipIcon.Info;
                _tray.ShowBalloonTip(2800);
            }
            catch { }
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized) HideToTray();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }
            try { if (_timer != null) _timer.Stop(); } catch { }
            try { _bridge.Stop(); } catch { }
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
        }

        private void Quit()
        {
            _reallyExit = true;
            Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_appIcon != null) _appIcon.Dispose(); } catch { }
                try { if (_avatar != null) _avatar.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
