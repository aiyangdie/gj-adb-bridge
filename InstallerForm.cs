using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace GjBridge
{
    /// <summary>简易安装向导：复制便携 exe、创建开始菜单/桌面快捷方式。</summary>
    internal sealed class InstallerForm : Form
    {
        private TextBox _pathBox;
        private CheckBox _deskCheck;
        private CheckBox _runCheck;
        private Label _log;

        public InstallerForm()
        {
            Text = "安装 · " + Program.AppName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 360);
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Theme.Bg;
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);

            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 64;
            head.BackColor = Theme.Brand;
            Controls.Add(head);

            Label t = new Label();
            t.Text = "安装本机桥接";
            t.ForeColor = Color.White;
            t.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            t.Location = new Point(20, 18);
            t.Size = new Size(400, 28);
            t.BackColor = Color.Transparent;
            head.Controls.Add(t);

            Label hint = new Label();
            hint.Text = "安装后可从开始菜单随时打开；每次启动会打开网站并运行桥接。";
            hint.ForeColor = Theme.Muted;
            hint.Location = new Point(20, 80);
            hint.Size = new Size(420, 40);
            Controls.Add(hint);

            Label pathLbl = new Label();
            pathLbl.Text = "安装位置";
            pathLbl.ForeColor = Theme.Ink;
            pathLbl.Location = new Point(20, 128);
            pathLbl.Size = new Size(100, 20);
            Controls.Add(pathLbl);

            _pathBox = new TextBox();
            _pathBox.Location = new Point(20, 152);
            _pathBox.Size = new Size(420, 28);
            _pathBox.Text = DefaultInstallDir();
            Controls.Add(_pathBox);

            _deskCheck = new CheckBox();
            _deskCheck.Text = "创建桌面快捷方式";
            _deskCheck.Checked = true;
            _deskCheck.Location = new Point(20, 196);
            _deskCheck.Size = new Size(200, 24);
            Controls.Add(_deskCheck);

            _runCheck = new CheckBox();
            _runCheck.Text = "安装完成后立即运行";
            _runCheck.Checked = true;
            _runCheck.Location = new Point(20, 224);
            _runCheck.Size = new Size(200, 24);
            Controls.Add(_runCheck);

            _log = new Label();
            _log.ForeColor = Theme.Muted;
            _log.Location = new Point(20, 256);
            _log.Size = new Size(420, 36);
            Controls.Add(_log);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Location = new Point(220, 304);
            cancel.Size = new Size(100, 36);
            cancel.FlatStyle = FlatStyle.Flat;
            cancel.BackColor = Theme.Surface;
            cancel.ForeColor = Theme.Brand;
            cancel.FlatAppearance.BorderColor = Theme.BrandSoft;
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            Button ok = new Button();
            ok.Text = "安装";
            ok.Location = new Point(340, 304);
            ok.Size = new Size(100, 36);
            ok.FlatStyle = FlatStyle.Flat;
            ok.BackColor = Theme.Brand;
            ok.ForeColor = Color.White;
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += delegate { DoInstall(); };
            Controls.Add(ok);
        }

        private static string DefaultInstallDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GjToolboxBridge");
        }

        private void DoInstall()
        {
            try
            {
                string dir = (_pathBox.Text ?? "").Trim();
                if (dir.Length == 0) throw new Exception("请填写安装目录");
                Directory.CreateDirectory(dir);

                string srcExe = Assembly.GetExecutingAssembly().Location;
                // 安装器与便携版分开时：优先旁边的便携 exe
                string portable = Path.Combine(Path.GetDirectoryName(srcExe) ?? "", "搞机工具箱-本机桥接.exe");
                if (!File.Exists(portable))
                    portable = Path.Combine(Path.GetDirectoryName(srcExe) ?? "", "gj-adb-bridge.exe");
                if (!File.Exists(portable))
                    portable = srcExe; // 退化：若用同一文件带 --install

                string dstExe = Path.Combine(dir, "搞机工具箱-本机桥接.exe");
                File.Copy(portable, dstExe, true);

                string icoSrc = Path.Combine(Path.GetDirectoryName(portable) ?? "", "app.ico");
                string icoDst = Path.Combine(dir, "app.ico");
                if (File.Exists(icoSrc)) File.Copy(icoSrc, icoDst, true);

                string avatarSrc = Path.Combine(Path.GetDirectoryName(portable) ?? "", "avatar-round.png");
                if (File.Exists(avatarSrc))
                    File.Copy(avatarSrc, Path.Combine(dir, "avatar-round.png"), true);

                string startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs",
                    "搞机工具箱本机桥接");
                Directory.CreateDirectory(startMenu);
                CreateShortcut(Path.Combine(startMenu, "搞机工具箱本机桥接.lnk"), dstExe, dir, "搞机工具箱 · 本机桥接");
                CreateShortcut(Path.Combine(startMenu, "卸载本机桥接.lnk"), dstExe, dir, "卸载", "--uninstall");

                if (_deskCheck.Checked)
                {
                    string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desk, "搞机工具箱本机桥接.lnk"), dstExe, dir, "搞机工具箱 · 本机桥接");
                }

                // 简易卸载脚本
                File.WriteAllText(Path.Combine(dir, "uninstall.cmd"),
                    "@echo off\r\n" +
                    "taskkill /IM \"搞机工具箱-本机桥接.exe\" /F >nul 2>nul\r\n" +
                    "taskkill /IM gj-adb-bridge.exe /F >nul 2>nul\r\n" +
                    "cd /d \"%~dp0\"\r\n" +
                    "start \"\" /wait cmd /c \"timeout /t 1 >nul & rmdir /s /q \"\"%~dp0\"\"\"\r\n",
                    Encoding.Default);

                _log.ForeColor = Theme.Ok;
                _log.Text = "安装完成：" + dir;

                if (_runCheck.Checked)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = dstExe,
                        WorkingDirectory = dir,
                        UseShellExecute = true
                    });
                }

                MessageBox.Show("安装成功！\n可从开始菜单打开「搞机工具箱本机桥接」。", Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                _log.ForeColor = Theme.Err;
                _log.Text = "安装失败：" + ex.Message;
                MessageBox.Show(ex.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void CreateShortcut(string lnkPath, string target, string workDir, string desc)
        {
            CreateShortcut(lnkPath, target, workDir, desc, null);
        }

        private static void CreateShortcut(string lnkPath, string target, string workDir, string desc, string args)
        {
            // 用 PowerShell 创建 .lnk，避免依赖额外 COM 互操作包
            string ps =
                "$ws = New-Object -ComObject WScript.Shell; " +
                "$s = $ws.CreateShortcut([string]'" + EscapePs(lnkPath) + "'); " +
                "$s.TargetPath = [string]'" + EscapePs(target) + "'; " +
                "$s.WorkingDirectory = [string]'" + EscapePs(workDir) + "'; " +
                "$s.Description = [string]'" + EscapePs(desc) + "'; ";
            if (!string.IsNullOrEmpty(args))
                ps += "$s.Arguments = [string]'" + EscapePs(args) + "'; ";
            string ico = Path.Combine(workDir, "app.ico");
            if (File.Exists(ico))
                ps += "$s.IconLocation = [string]'" + EscapePs(ico) + "'; ";
            ps += "$s.Save();";

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " + Quote(ps);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
            {
                if (p != null) p.WaitForExit(15000);
            }
        }

        private static string EscapePs(string s)
        {
            return (s ?? "").Replace("'", "''");
        }

        private static string Quote(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";
        }

        public static void RunUninstall()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            DialogResult r = MessageBox.Show(
                "确定卸载本机桥接？\n将删除：\n" + dir,
                "卸载",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            try
            {
                string desk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "搞机工具箱本机桥接.lnk");
                if (File.Exists(desk)) File.Delete(desk);
                string sm = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs",
                    "搞机工具箱本机桥接");
                if (Directory.Exists(sm)) Directory.Delete(sm, true);
            }
            catch { }

            string bat = Path.Combine(Path.GetTempPath(), "gj-bridge-uninstall.cmd");
            File.WriteAllText(bat,
                "@echo off\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                "rmdir /s /q \"" + dir.TrimEnd('\\') + "\"\r\n" +
                "del \"%~f0\"\r\n", Encoding.Default);
            Process.Start(new ProcessStartInfo { FileName = bat, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            Application.Exit();
        }
    }
}
