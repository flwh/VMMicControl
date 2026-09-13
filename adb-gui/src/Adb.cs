using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AdbGui
{
    #region 数据模型

    /// <summary>一台 adb 设备</summary>
    internal sealed class AdbDevice
    {
        public string Serial = "";
        public string State = "";
        public string Model = "";
        public string Product = "";
        public string TransportId = "";

        public bool Online
        {
            get { return string.Equals(State, "device", StringComparison.OrdinalIgnoreCase); }
        }

        public string StateLabel
        {
            get
            {
                if (State.Length == 0) return "未知";
                if (string.Equals(State, "device", StringComparison.OrdinalIgnoreCase)) return "已连接";
                if (string.Equals(State, "offline", StringComparison.OrdinalIgnoreCase)) return "离线";
                if (string.Equals(State, "unauthorized", StringComparison.OrdinalIgnoreCase)) return "未授权";
                if (State.StartsWith("no permissions")) return "无权限";
                return State;
            }
        }

        public string Title
        {
            get
            {
                string name = Model.Length > 0 ? Model : Serial;
                if (name != Serial) name = name + "  (" + Serial + ")";
                if (!Online) name = name + "  -  " + StateLabel;
                return name;
            }
        }

        public override string ToString() { return Title; }
    }

    /// <summary>一次进程调用的结果</summary>
    internal sealed class ProcResult
    {
        public int ExitCode = -1;
        public bool Canceled;
        public bool TimedOut;
        public string StdOut = "";
        public string StdErr = "";
        public string Failure;
        public long ElapsedMs;

        public bool Ok
        {
            get { return Failure == null && !Canceled && !TimedOut && ExitCode == 0; }
        }

        public string Message
        {
            get
            {
                if (Failure != null) return Failure;
                if (Canceled) return "已取消";
                if (TimedOut) return "超时";
                return "退出码 " + ExitCode;
            }
        }
    }

    #endregion

    #region 进程执行

    /// <summary>启动子进程并抓取输出（无窗口、UTF-8、可取消 / 超时）</summary>
    internal static class Proc
    {
        public static ProcResult Run(string exe, string arguments, int timeoutMs,
                                     Action<string> onStdOut, Action<string> onStdErr,
                                     Action<Process> onStarted, Func<bool> cancelRequested)
        {
            ProcResult result = new ProcResult();
            ProcessStartInfo psi = new ProcessStartInfo(exe, arguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);
            try { psi.WorkingDirectory = Path.GetDirectoryName(exe); } catch { }

            Process p = new Process();
            p.StartInfo = psi;

            StringBuilder so = new StringBuilder();
            StringBuilder se = new StringBuilder();
            // 始终注册处理器以捕获输出；onStdOut/onStdErr 仅作为可选回调
            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (so) so.AppendLine(e.Data);
                if (onStdOut != null) onStdOut(e.Data);
            };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (se) se.AppendLine(e.Data);
                if (onStdErr != null) onStdErr(e.Data);
            };

            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (onStarted != null) onStarted(p);

                while (!p.WaitForExit(40))
                {
                    if (cancelRequested != null && cancelRequested())
                    {
                        result.Canceled = true;
                        Kill(p);
                        break;
                    }
                    if (timeoutMs > 0 && watch.ElapsedMilliseconds > timeoutMs)
                    {
                        result.TimedOut = true;
                        Kill(p);
                        break;
                    }
                }

                try { p.WaitForExit(); } catch { }                 // 等输出流读完
                if (result.Canceled || result.TimedOut) { try { p.WaitForExit(1500); } catch { } }
                try { result.ExitCode = p.ExitCode; } catch { result.ExitCode = -1; }
            }
            catch (Exception ex)
            {
                result.Failure = ex.Message;
            }
            finally
            {
                watch.Stop();
                result.ElapsedMs = watch.ElapsedMilliseconds;
                lock (so) result.StdOut = so.ToString();
                lock (se) result.StdErr = se.ToString();
                try { p.Close(); } catch { }
            }
            return result;
        }

        private static void Kill(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }
    }

    #endregion

    #region adb.exe 定位与设置

    /// <summary>在常见位置寻找 adb.exe（不需要环境变量也能找到）</summary>
    internal static class AdbLocator
    {
        public static List<string> Candidates()
        {
            List<string> list = new List<string>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            list.Add(Path.Combine(baseDir, "adb.exe"));
            list.Add(Path.Combine(Path.Combine(baseDir, "platform-tools"), "adb.exe"));

            AddEnv(list, "ADB_PATH");
            AddEnv(list, "ADB_EXE");

            string[] sdkEnvs = { "ANDROID_HOME", "ANDROID_SDK_ROOT", "ANDROID_SDK" };
            foreach (string key in sdkEnvs)
            {
                string v = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(v))
                    list.Add(Path.Combine(Path.Combine(v, "platform-tools"), "adb.exe"));
            }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            list.Add(Path.Combine(local, @"Android\Sdk\platform-tools\adb.exe"));
            list.Add(Path.Combine(user, @"AppData\Local\Android\Sdk\platform-tools\adb.exe"));
            list.Add(Path.Combine(user, @"scoop\apps\adb\current\adb.exe"));
            list.Add(Path.Combine(user, @"platform-tools\adb.exe"));
            list.Add(Path.Combine(local, @"Programs\platform-tools\adb.exe"));
            list.Add(Path.Combine(x86, @"Android\android-sdk\platform-tools\adb.exe"));

            string[] roots = { @"C:\", @"D:\", @"E:\", @"F:\" };
            foreach (string root in roots)
            {
                list.Add(root + @"platform-tools\adb.exe");
                list.Add(root + @"adb\adb.exe");
                list.Add(root + @"tools\platform-tools\adb.exe");
                list.Add(root + @"Program\platform-tools\adb.exe");
            }

            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                foreach (string raw in path.Split(';'))
                {
                    string dir = raw.Trim().Trim('"');
                    if (dir.Length == 0) continue;
                    try { list.Add(Path.Combine(dir, "adb.exe")); } catch { }
                }
            }
            return list;
        }

        private static void AddEnv(List<string> list, string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(v)) list.Add(v);
        }

        public static string Find()
        {
            foreach (string c in Candidates())
            {
                try { if (File.Exists(c)) return Path.GetFullPath(c); } catch { }
            }
            return null;
        }
    }

    /// <summary>极简设置存储：%APPDATA%\AdbGui\settings.txt</summary>
    internal static class Settings
    {
        private static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdbGui");
        private static readonly string FilePath = Path.Combine(Dir, "settings.txt");
        private static readonly Dictionary<string, string> Map = Load();

        private static Dictionary<string, string> Load()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return map;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    map[line.Substring(0, i).Trim()] = line.Substring(i + 1);
                }
            }
            catch { }
            return map;
        }

        public static string Get(string key, string fallback)
        {
            string v;
            return Map.TryGetValue(key, out v) ? v : fallback;
        }

        public static bool GetBool(string key, bool fallback)
        {
            string v = Get(key, fallback ? "1" : "0");
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static void Set(string key, string value) { Map[key] = value ?? ""; }

        public static void SetBool(string key, bool value) { Map[key] = value ? "1" : "0"; }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in Map) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    #endregion
}
