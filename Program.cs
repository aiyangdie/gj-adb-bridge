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
        public const string Version = "1.5.0";
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
            bool created;
            using (var mutex = new Mutex(true, @"Local\GjToolboxAdbBridge_sj_aikex_ink", out created))
            {
                if (!created)
                {
                    MessageBox.Show(
                        "本机桥接已经在运行。\n请看右下角托盘图标（青绿手机）。\n\n网站：" + SiteUrl,
                        AppName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool showAbout = false;
                if (args != null)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        string a = args[i] == null ? "" : args[i].Trim().ToLowerInvariant();
                        if (a == "--about" || a == "/about") showAbout = true;
                    }
                }
                Application.Run(new MainForm(showAbout));
            }
        }
    }
}
