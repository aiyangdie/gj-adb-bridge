using System;
using System.Threading;
using System.Windows.Forms;

namespace GjBridge
{
    internal static class Program
    {
        public const string AppName = "搞机工具箱 · 本机桥接";
        public const string Brand = "sj.aikex.ink";
        public const string SiteUrl = "https://sj.aikex.ink/";
        public const string Version = "1.7.1";
        public const int DefaultPort = 17855;

        public const string AuthorName = "艾阳";
        public const string AuthorGithub = "aiyangdie";
        public const string AuthorGithubUrl = "https://github.com/aiyangdie";
        public const string AuthorEmail = "aike1015@qq.com";
        public const string RepoUrl = "https://github.com/aiyangdie/gj-adb-bridge";
        public const string UpstreamUrl = "https://github.com/CzJam/gjgjxweb";
        public const string LicenseName = "GPL-3.0";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = ParseMode(args);
            if (mode == "uninstall")
            {
                InstallerForm.RunUninstall();
                return;
            }
            if (mode == "install")
            {
                Application.Run(new InstallerForm());
                return;
            }

            // 强制免责声明：不同意则直接退出，不启动桥接
            if (!DisclaimerForm.EnsureAccepted())
                return;

            bool created;
            using (var mutex = new Mutex(true, @"Local\GjToolboxAdbBridge_sj_aikex_ink", out created))
            {
                if (!created)
                {
                    MessageBox.Show(
                        "本机桥接已经在运行。\n请看右下角托盘图标。\n\n网站：" + SiteUrl,
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }
                Application.Run(new MainForm());
            }
        }

        private static string ParseMode(string[] args)
        {
            if (args == null) return "";
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i] == null ? "" : args[i].Trim().ToLowerInvariant();
                if (a == "--install" || a == "/install") return "install";
                if (a == "--uninstall" || a == "/uninstall") return "uninstall";
            }
            // 文件名含「安装」时走安装向导（安装包 exe）
            try
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                if (name.IndexOf("安装") >= 0 || name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "install";
            }
            catch { }
            return "";
        }
    }
}
