using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GjBridge
{
    internal sealed class MainForm : Form
    {
        private readonly BridgeHost _bridge = new BridgeHost();
        private readonly bool _openAboutOnStart;
        private NotifyIcon _tray;
        private Label _statusTitle;
        private Label _statusDetail;
        private Label _adbLabel;
        private Label _urlLabel;
        private Panel _dot;
        private System.Windows.Forms.Timer _timer;
        private bool _reallyExit;
        private Icon _appIcon;
        private Image _avatar;

        public MainForm() : this(false) { }

        public MainForm(bool openAboutOnStart)
        {
            _openAboutOnStart = openAboutOnStart;
            _appIcon = LoadAppIcon();
            _avatar = LoadAvatar();
            Text = Program.AppName;
            Icon = _appIcon;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 520);
            BackColor = Color.FromArgb(241, 245, 249);
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
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("GjBridge.app.ico"))
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
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("GjBridge.avatar.png"))
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
            header.Height = 108;
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                Rectangle r = header.ClientRectangle;
                using (LinearGradientBrush br = new LinearGradientBrush(r,
                    Color.FromArgb(15, 118, 110), Color.FromArgb(20, 184, 166), 18f))
                {
                    e.Graphics.FillRectangle(br, r);
                }
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                {
                    e.Graphics.FillEllipse(glow, r.Width - 120, -40, 180, 180);
                }
            };
            Controls.Add(header);

            if (_avatar != null)
            {
                PictureBox avatarBox = new PictureBox();
                avatarBox.Image = _avatar;
                avatarBox.SizeMode = PictureBoxSizeMode.Zoom;
                avatarBox.Size = new Size(52, 52);
                avatarBox.Location = new Point(18, 28);
                avatarBox.BackColor = Color.Transparent;
                avatarBox.Cursor = Cursors.Hand;
                avatarBox.Click += delegate { OpenUrl(Program.AuthorGithubUrl); };
                header.Controls.Add(avatarBox);
            }

            Label brand = new Label();
            brand.AutoSize = false;
            brand.Text = Program.AppName;
            brand.ForeColor = Color.White;
            brand.Font = new Font("Microsoft YaHei UI", 13.5F, FontStyle.Bold);
            brand.Location = new Point(_avatar != null ? 80 : 20, 24);
            brand.Size = new Size(360, 28);
            brand.BackColor = Color.Transparent;
            header.Controls.Add(brand);

            Label sub = new Label();
            sub.AutoSize = false;
            sub.Text = "本站维护 · " + Program.Brand + "  ·  v" + Program.Version;
            sub.ForeColor = Color.FromArgb(204, 251, 241);
            sub.Font = new Font("Microsoft YaHei UI", 9F);
            sub.Location = new Point(_avatar != null ? 80 : 20, 56);
            sub.Size = new Size(360, 22);
            sub.BackColor = Color.Transparent;
            header.Controls.Add(sub);

            Label author = new Label();
            author.AutoSize = false;
            author.Text = "开源作者 @" + Program.AuthorGithub + " · 点头像进 GitHub";
            author.ForeColor = Color.FromArgb(167, 243, 208);
            author.Font = new Font("Microsoft YaHei UI", 8.5F);
            author.Location = new Point(_avatar != null ? 80 : 20, 78);
            author.Size = new Size(360, 20);
            author.BackColor = Color.Transparent;
            author.Cursor = Cursors.Hand;
            author.Click += delegate { OpenUrl(Program.AuthorGithubUrl); };
            header.Controls.Add(author);

            Panel card = RoundedCard(20, 124, 420, 168);
            Controls.Add(card);

            _dot = new Panel();
            _dot.Size = new Size(12, 12);
            _dot.Location = new Point(20, 24);
            _dot.BackColor = Color.FromArgb(148, 163, 184);
            card.Controls.Add(_dot);

            _statusTitle = new Label();
            _statusTitle.Text = "正在启动…";
            _statusTitle.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            _statusTitle.ForeColor = Color.FromArgb(15, 23, 42);
            _statusTitle.Location = new Point(42, 18);
            _statusTitle.Size = new Size(350, 26);
            card.Controls.Add(_statusTitle);

            _statusDetail = new Label();
            _statusDetail.Text = "启动本机 ADB 桥接服务";
            _statusDetail.ForeColor = Color.FromArgb(100, 116, 139);
            _statusDetail.Location = new Point(20, 52);
            _statusDetail.Size = new Size(380, 22);
            card.Controls.Add(_statusDetail);

            _adbLabel = new Label();
            _adbLabel.Text = "ADB：检测中";
            _adbLabel.ForeColor = Color.FromArgb(71, 85, 105);
            _adbLabel.Location = new Point(20, 84);
            _adbLabel.Size = new Size(380, 22);
            card.Controls.Add(_adbLabel);

            _urlLabel = new Label();
            _urlLabel.Text = "地址：—";
            _urlLabel.ForeColor = Color.FromArgb(71, 85, 105);
            _urlLabel.Location = new Point(20, 114);
            _urlLabel.Size = new Size(380, 36);
            _urlLabel.AutoEllipsis = true;
            card.Controls.Add(_urlLabel);

            Panel tip = RoundedCard(20, 304, 420, 58);
            Controls.Add(tip);
            Label tipText = new Label();
            tipText.Text = "保持本窗口/托盘运行，回到网站点「无线连接」即可。\nUSB 调试不需要本程序。";
            tipText.ForeColor = Color.FromArgb(51, 65, 85);
            tipText.Location = new Point(16, 12);
            tipText.Size = new Size(388, 40);
            tip.Controls.Add(tipText);

            Button openSite = MakeButton("打开搞机工具箱网站", 20, 380, 250, true);
            openSite.Click += delegate { OpenUrl(Program.SiteUrl); };
            Controls.Add(openSite);

            Button copyBtn = MakeButton("复制桥接地址", 280, 380, 160, false);
            copyBtn.Click += delegate
            {
                try
                {
                    Clipboard.SetText(_bridge.PublicUrl);
                    ShowBalloon("已复制", "桥接地址已复制。网站一般会自动探测，通常不用粘贴。");
                }
                catch { }
            };
            Controls.Add(copyBtn);

            Button aboutBtn = MakeButton("关于 / 免责声明", 20, 426, 160, false);
            aboutBtn.Click += delegate { ShowAbout(); };
            Controls.Add(aboutBtn);

            Button hideBtn = MakeButton("隐藏到托盘", 190, 426, 120, false);
            hideBtn.Click += delegate { HideToTray(); };
            Controls.Add(hideBtn);

            Button exitBtn = MakeButton("退出", 320, 426, 120, false);
            exitBtn.Click += delegate { Quit(); };
            Controls.Add(exitBtn);

            LinkLabel oss = new LinkLabel();
            oss.Text = "开源仓库 " + Program.RepoUrl;
            oss.Location = new Point(20, 478);
            oss.Size = new Size(420, 22);
            oss.LinkColor = Color.FromArgb(13, 148, 136);
            oss.ActiveLinkColor = Color.FromArgb(15, 118, 110);
            oss.LinkClicked += delegate { OpenUrl(Program.RepoUrl); };
            Controls.Add(oss);
        }

        private Panel RoundedCard(int x, int y, int w, int h)
        {
            Panel card = new Panel();
            card.Location = new Point(x, y);
            card.Size = new Size(w, h);
            card.BackColor = Color.White;
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240)))
                using (GraphicsPath path = RoundRect(0, 0, card.Width - 1, card.Height - 1, 12))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };
            return card;
        }

        private static GraphicsPath RoundRect(int x, int y, int w, int h, int r)
        {
            GraphicsPath path = new GraphicsPath();
            int d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Button MakeButton(string text, int x, int y, int w, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 36);
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Microsoft YaHei UI", 9F);
            if (primary)
            {
                b.BackColor = Color.FromArgb(13, 148, 136);
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 118, 110);
            }
            else
            {
                b.BackColor = Color.White;
                b.ForeColor = Color.FromArgb(15, 118, 110);
                b.FlatAppearance.BorderColor = Color.FromArgb(153, 246, 228);
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 253, 250);
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
            menu.Items.Add("打开搞机工具箱网站", null, delegate { OpenUrl(Program.SiteUrl); });
            menu.Items.Add("打开 GitHub", null, delegate { OpenUrl(Program.AuthorGithubUrl); });
            menu.Items.Add("显示主窗口", null, delegate
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            });
            menu.Items.Add("关于 / 免责声明", null, delegate { Show(); ShowAbout(); });
            menu.Items.Add("复制桥接地址", null, delegate
            {
                try { Clipboard.SetText(_bridge.PublicUrl); } catch { }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Quit(); });
            _tray.ContextMenuStrip = menu;
        }

        private void ShowAbout()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "关于 · 免责声明 · 开源";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ClientSize = new Size(460, 520);
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowIcon = true;
                dlg.Icon = _appIcon;
                dlg.BackColor = Color.White;
                dlg.Font = Font;

                Panel top = new Panel();
                top.Dock = DockStyle.Top;
                top.Height = 96;
                top.Paint += delegate(object s, PaintEventArgs e)
                {
                    using (LinearGradientBrush br = new LinearGradientBrush(top.ClientRectangle,
                        Color.FromArgb(15, 118, 110), Color.FromArgb(20, 184, 166), 20f))
                        e.Graphics.FillRectangle(br, top.ClientRectangle);
                };
                dlg.Controls.Add(top);

                if (_avatar != null)
                {
                    PictureBox pic = new PictureBox();
                    pic.Image = _avatar;
                    pic.SizeMode = PictureBoxSizeMode.Zoom;
                    pic.Size = new Size(56, 56);
                    pic.Location = new Point(18, 20);
                    pic.Cursor = Cursors.Hand;
                    pic.Click += delegate { OpenUrl(Program.AuthorGithubUrl); };
                    top.Controls.Add(pic);
                }

                Label t1 = new Label();
                t1.Text = Program.AppName;
                t1.ForeColor = Color.White;
                t1.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
                t1.Location = new Point(88, 22);
                t1.Size = new Size(350, 24);
                t1.BackColor = Color.Transparent;
                top.Controls.Add(t1);

                Label t2 = new Label();
                t2.Text = Program.AuthorName + "  @" + Program.AuthorGithub + "  ·  v" + Program.Version;
                t2.ForeColor = Color.FromArgb(204, 251, 241);
                t2.Location = new Point(88, 52);
                t2.Size = new Size(350, 22);
                t2.BackColor = Color.Transparent;
                top.Controls.Add(t2);

                TextBox box = new TextBox();
                box.Multiline = true;
                box.ReadOnly = true;
                box.BorderStyle = BorderStyle.None;
                box.BackColor = Color.White;
                box.Location = new Point(18, 112);
                box.Size = new Size(424, 340);
                box.ScrollBars = ScrollBars.Vertical;
                box.Text =
                    "【这是什么】\r\n" +
                    "本程序是「搞机工具箱 WebADB」的本机桥接器，只在 127.0.0.1 监听，\r\n" +
                    "把网页的无线调试请求转给本机 adb，让浏览器能连局域网手机。\r\n" +
                    "网站：" + Program.SiteUrl + "\r\n\r\n" +
                    "【开源】\r\n" +
                    "许可证：" + Program.LicenseName + "\r\n" +
                    "本仓库：" + Program.RepoUrl + "\r\n" +
                    "作者 GitHub：@" + Program.AuthorGithub + "  " + Program.AuthorGithubUrl + "\r\n" +
                    "联系邮箱：" + Program.AuthorEmail + "\r\n" +
                    "上游项目：" + Program.UpstreamUrl + "\r\n" +
                    "欢迎 Star / Issue / PR。\r\n\r\n" +
                    "【免责声明】\r\n" +
                    "1. 本软件按「现状」提供，作者不对使用后果承担任何责任。\r\n" +
                    "2. 请仅在您拥有或已获授权的设备上使用；勿用于未授权访问。\r\n" +
                    "3. 无线调试、卸载、清数据、刷机相关操作可能导致数据丢失或系统异常，\r\n" +
                    "   请自行备份并确认风险后再操作。\r\n" +
                    "4. 本程序仅监听本机回环地址，不会把 ADB 流量发到云端；\r\n" +
                    "   但您仍需自行保证电脑与网络安全。\r\n" +
                    "5. 第三方杀软可能误报，属正常现象；源码已开源可供审计。\r\n" +
                    "6. 继续使用即表示您已阅读并同意上述说明。\r\n\r\n" +
                    "【隐私】\r\n" +
                    "不收集账号密码；桥接 token 仅存本机本地目录。\r\n";
                dlg.Controls.Add(box);

                Button github = MakeButton("打开我的 GitHub", 18, 462, 150, true);
                github.Click += delegate { OpenUrl(Program.AuthorGithubUrl); };
                dlg.Controls.Add(github);

                Button repo = MakeButton("打开开源仓库", 178, 462, 140, false);
                repo.Click += delegate { OpenUrl(Program.RepoUrl); };
                dlg.Controls.Add(repo);

                Button ok = MakeButton("我知道了", 328, 462, 114, false);
                ok.Click += delegate { dlg.Close(); };
                dlg.Controls.Add(ok);

                dlg.ShowDialog(this);
            }
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            try
            {
                _bridge.Start();
                RefreshStatus(true);
                ShowBalloon("桥接已启动", "可以打开 " + Program.Brand + " 使用无线调试了");
                OpenUrl(Program.SiteUrl);
                bool first = IsFirstRun();
                if (_openAboutOnStart || first) ShowAbout();
            }
            catch (Exception ex)
            {
                _statusTitle.Text = "启动失败";
                _statusDetail.Text = ex.Message;
                _dot.BackColor = Color.FromArgb(239, 68, 68);
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
                _statusDetail.Text = "监听 127.0.0.1:" + _bridge.Port + " · 仅本机可访问";
                _dot.BackColor = Color.FromArgb(16, 185, 129);
                _tray.Text = Program.AppName + " · 运行中";
            }
            else
            {
                _statusTitle.Text = "未运行";
                _statusDetail.Text = _bridge.LastError ?? "桥接未启动";
                _dot.BackColor = Color.FromArgb(239, 68, 68);
            }

            if (AdbRunner.AdbOk)
                _adbLabel.Text = "ADB：已找到  " + ShortPath(AdbRunner.AdbPath);
            else
                _adbLabel.Text = "ADB：未找到（无线会失败，请装 platform-tools）";

            _urlLabel.Text = "地址：" + _bridge.PublicUrl;
        }

        private static string ShortPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            if (p.Length <= 46) return p;
            return "…" + p.Substring(p.Length - 44);
        }

        private static string FirstRunFlagPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "gj-toolbox-bridge");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "seen-about-v15.txt");
        }

        private static bool IsFirstRun()
        {
            try
            {
                string path = FirstRunFlagPath();
                if (File.Exists(path)) return false;
                File.WriteAllText(path, DateTime.Now.ToString("o"));
                return true;
            }
            catch { return false; }
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(url); }
            catch
            {
                try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
                catch { }
            }
        }

        private void HideToTray()
        {
            Hide();
            ShowBalloon("仍在后台运行", "右下角托盘可打开窗口或退出。网站可继续无线连接。");
        }

        private void ShowBalloon(string title, string text)
        {
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = text;
                _tray.BalloonTipIcon = ToolTipIcon.Info;
                _tray.ShowBalloonTip(3500);
            }
            catch { }
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
                HideToTray();
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
