using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GjBridge
{
    /// <summary>强制同意免责声明：不同意则退出，不启动桥接。</summary>
    internal sealed class DisclaimerForm : Form
    {
        public bool Accepted { get; private set; }

        public DisclaimerForm()
        {
            Text = "使用前须知 · 免责声明";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 520);
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = true;
            BackColor = Theme.Surface;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            ShowInTaskbar = true;

            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 72;
            head.BackColor = Theme.Brand;
            Controls.Add(head);

            Label title = new Label();
            title.Text = Program.AppName;
            title.ForeColor = Color.White;
            title.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            title.Location = new Point(20, 14);
            title.Size = new Size(440, 26);
            title.BackColor = Color.Transparent;
            head.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "请阅读并同意后才能使用本机桥接";
            sub.ForeColor = Color.FromArgb(220, 226, 255);
            sub.Location = new Point(20, 42);
            sub.Size = new Size(440, 22);
            sub.BackColor = Color.Transparent;
            head.Controls.Add(sub);

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.FromArgb(247, 248, 249);
            box.ForeColor = Theme.Ink;
            box.Location = new Point(18, 88);
            box.Size = new Size(444, 340);
            box.ScrollBars = ScrollBars.Vertical;
            box.Text =
                "欢迎使用「搞机工具箱 · 本机桥接」\r\n" +
                "网站：" + Program.SiteUrl + "\r\n" +
                "开源：" + Program.RepoUrl + "\r\n" +
                "作者：@" + Program.AuthorGithub + "  " + Program.AuthorEmail + "\r\n" +
                "许可证：" + Program.LicenseName + "\r\n\r\n" +
                "【本软件做什么】\r\n" +
                "仅在本机 127.0.0.1 提供 ADB 桥接，让网页能进行无线调试。\r\n" +
                "USB 调试不需要本程序。\r\n\r\n" +
                "【免责声明 · 必须同意】\r\n" +
                "1. 本软件按「现状」提供，作者不对使用后果承担责任。\r\n" +
                "2. 仅可在您拥有或已获授权的设备上使用。\r\n" +
                "3. 无线调试、卸载、清数据等操作可能导致数据丢失或系统异常，请先备份。\r\n" +
                "4. 流量仅经本机回环，不会主动上传到云端；请自行保证电脑安全。\r\n" +
                "5. 继续使用即表示您已阅读并同意以上全部条款。\r\n\r\n" +
                "点击「不同意」将立即退出，不会启动桥接服务。";
            Controls.Add(box);

            Button disagree = new Button();
            disagree.Text = "不同意并退出";
            disagree.Location = new Point(18, 448);
            disagree.Size = new Size(150, 40);
            disagree.FlatStyle = FlatStyle.Flat;
            disagree.BackColor = Color.White;
            disagree.ForeColor = Theme.Err;
            disagree.FlatAppearance.BorderColor = Color.FromArgb(255, 176, 165);
            disagree.Cursor = Cursors.Hand;
            disagree.Click += delegate
            {
                Accepted = false;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(disagree);

            Button agree = new Button();
            agree.Text = "同意并开始使用";
            agree.Location = new Point(250, 448);
            agree.Size = new Size(212, 40);
            agree.FlatStyle = FlatStyle.Flat;
            agree.BackColor = Theme.Brand;
            agree.ForeColor = Color.White;
            agree.FlatAppearance.BorderSize = 0;
            agree.Cursor = Cursors.Hand;
            agree.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold);
            agree.Click += delegate
            {
                Accepted = true;
                SaveAccepted();
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(agree);

            AcceptButton = agree;
            CancelButton = disagree;
        }

        private static string FlagPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "gj-toolbox-bridge");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "disclaimer-accepted-v15.txt");
        }

        public static bool HasAcceptedBefore()
        {
            try { return File.Exists(FlagPath()); }
            catch { return false; }
        }

        private static void SaveAccepted()
        {
            try { File.WriteAllText(FlagPath(), DateTime.UtcNow.ToString("o")); }
            catch { }
        }

        /// <summary>返回 true 表示可继续启动；false 表示应退出进程。</summary>
        public static bool EnsureAccepted()
        {
            if (HasAcceptedBefore()) return true;
            using (DisclaimerForm dlg = new DisclaimerForm())
            {
                DialogResult r = dlg.ShowDialog();
                return r == DialogResult.OK && dlg.Accepted;
            }
        }
    }
}
