using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GjBridge
{
    internal sealed class AdbResult
    {
        public int ExitCode;
        public string StdOut = "";
        public string StdErr = "";
        public string Combined
        {
            get
            {
                string s = (StdOut ?? "").Trim();
                if (s.Length == 0) s = (StdErr ?? "").Trim();
                return s;
            }
        }
    }

    internal static class AdbRunner
    {
        private static string _adbPath;
        private static bool _resolved;

        public static string AdbPath
        {
            get
            {
                EnsureResolved();
                return _adbPath;
            }
        }

        public static bool AdbOk
        {
            get
            {
                EnsureResolved();
                return _resolved;
            }
        }

        public static void EnsureResolved()
        {
            if (_adbPath != null) return;
            _adbPath = ResolveAdb(out _resolved);
        }

        public static void Refresh()
        {
            _adbPath = null;
            _resolved = false;
            EnsureResolved();
        }

        private static bool FileOk(string p)
        {
            try { return !string.IsNullOrEmpty(p) && File.Exists(p); }
            catch { return false; }
        }

        private static string ResolveAdb(out bool ok)
        {
            ok = false;
            string fromEnv = (Environment.GetEnvironmentVariable("ADB") ?? "").Trim();
            if (FileOk(fromEnv)) { ok = true; return fromEnv; }

            string which = Which("adb.exe");
            if (string.IsNullOrEmpty(which)) which = Which("adb");
            if (FileOk(which)) { ok = true; return which; }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
            string androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME") ?? "";
            string sdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? "";

            string[] candidates = new string[]
            {
                string.IsNullOrEmpty(androidHome) ? null : Path.Combine(androidHome, "platform-tools", "adb.exe"),
                string.IsNullOrEmpty(sdkRoot) ? null : Path.Combine(sdkRoot, "platform-tools", "adb.exe"),
                string.IsNullOrEmpty(local) ? null : Path.Combine(local, "Android", "Sdk", "platform-tools", "adb.exe"),
                string.IsNullOrEmpty(home) ? null : Path.Combine(home, "AppData", "Local", "Android", "Sdk", "platform-tools", "adb.exe"),
                @"C:\Android\platform-tools\adb.exe",
                @"C:\platform-tools\adb.exe"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (FileOk(candidates[i])) { ok = true; return candidates[i]; }
            }
            return "adb.exe";
        }

        private static string Which(string name)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "where";
                psi.Arguments = name;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    string[] lines = (output ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (FileOk(line)) return line;
                    }
                }
            }
            catch { }
            return "";
        }

        public static AdbResult Run(string[] args, int timeoutMs)
        {
            EnsureResolved();
            AdbResult result = new AdbResult();
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = _adbPath;
            psi.Arguments = JoinArgs(args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            try
            {
                psi.EnvironmentVariables["ADB_MDNS_OPENSCREEN"] = "1";
            }
            catch { }

            try
            {
                using (Process p = Process.Start(psi))
                {
                    StringBuilder so = new StringBuilder();
                    StringBuilder se = new StringBuilder();
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) so.AppendLine(e.Data);
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) se.AppendLine(e.Data);
                    };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { }
                        throw new Exception("adb 超时: " + psi.Arguments);
                    }
                    // ensure async readers finish
                    p.WaitForExit();
                    result.ExitCode = p.ExitCode;
                    result.StdOut = so.ToString();
                    result.StdErr = se.ToString();
                    if (result.ExitCode != 0)
                        throw new Exception(string.IsNullOrEmpty(result.Combined) ? ("adb exit " + result.ExitCode) : result.Combined);
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (!_resolved || (ex is System.ComponentModel.Win32Exception))
                {
                    throw new Exception("找不到 adb。可设环境变量 ADB=完整路径\\adb.exe，或安装 platform-tools。常见位置：%LOCALAPPDATA%\\Android\\Sdk\\platform-tools\\adb.exe");
                }
                throw;
            }
        }

        private static string JoinArgs(string[] args)
        {
            if (args == null || args.Length == 0) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                string a = args[i] ?? "";
                if (a.IndexOf(' ') >= 0 || a.IndexOf('"') >= 0)
                    sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
                else
                    sb.Append(a);
            }
            return sb.ToString();
        }

        public static List<Dictionary<string, string>> ParseDevices(string output)
        {
            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
            string[] lines = (output ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (Regex.IsMatch(line, @"^List of devices", RegexOptions.IgnoreCase)) continue;
                Match m = Regex.Match(line, @"^(\S+)\s+(\S+)(.*)$");
                if (!m.Success) continue;
                string serial = m.Groups[1].Value;
                string state = m.Groups[2].Value;
                string rest = m.Groups[3].Value;
                string model = MatchGroup(rest, @"model:(\S+)");
                string product = MatchGroup(rest, @"product:(\S+)");
                string transportId = MatchGroup(rest, @"transport_id:(\S+)");
                Dictionary<string, string> d = new Dictionary<string, string>();
                d["serial"] = serial;
                d["state"] = state;
                d["model"] = string.IsNullOrEmpty(model) ? "" : model.Replace('_', ' ');
                d["product"] = product ?? "";
                d["transportId"] = transportId ?? "";
                d["kind"] = Regex.IsMatch(serial, @":\d+$") ? "tcp" : "adb";
                list.Add(d);
            }
            return list;
        }

        public static List<Dictionary<string, string>> ParseMdns(string output)
        {
            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
            string[] lines = (output ?? "").Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0) continue;
                if (Regex.IsMatch(t, @"List of discovered", RegexOptions.IgnoreCase)) continue;
                string[] parts = Regex.Split(t, @"\s+");
                if (parts.Length < 3) continue;
                string name = parts[0].TrimEnd('.');
                string type = parts[1];
                string endpoint = parts[parts.Length - 1];
                Match m = Regex.Match(endpoint, @"^(\d{1,3}(?:\.\d{1,3}){3}):(\d{1,5})$");
                if (!m.Success) continue;
                Dictionary<string, string> d = new Dictionary<string, string>();
                d["name"] = name;
                d["type"] = type;
                d["host"] = m.Groups[1].Value;
                d["port"] = m.Groups[2].Value;
                list.Add(d);
            }
            return list;
        }

        private static string MatchGroup(string text, string pattern)
        {
            Match m = Regex.Match(text ?? "", pattern);
            return m.Success ? m.Groups[1].Value : "";
        }
    }
}
