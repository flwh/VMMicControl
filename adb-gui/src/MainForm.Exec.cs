using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AdbGui
{
    internal sealed partial class MainForm
    {
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private sealed class Job
        {
            public string Title = "";
            public string Exe = "";
            public string Args = "";
            public int TimeoutMs;
            public Func<string, bool> Filter;
            public Action<ProcResult> Done;
            public bool Silent;
        }

        private struct LogLine
        {
            public string Text;
            public Color Color;
            public LogLine(string text, Color color) { Text = text; Color = color; }
        }

        private readonly Queue<Job> _jobs = new Queue<Job>();
        private readonly object _jobsLock = new object();
        private readonly List<LogLine> _pending = new List<LogLine>();
        private readonly object _pendingLock = new object();

        private Thread _worker;
        private Process _currentProc;
        private volatile bool _cancelCurrent;
        private string _runningTitle;
        private System.Windows.Forms.Timer _pumpTimer;
        private bool _logcatRunning;
        private string _filterText = "";

        private void InitQueue()
        {
            _pumpTimer = new System.Windows.Forms.Timer();
            _pumpTimer.Interval = 80;
            _pumpTimer.Tick += delegate { FlushPending(); };
            _pumpTimer.Start();

            _filterBox.TextChanged += delegate { _filterText = _filterBox.Text.Trim(); };
        }

        #region 日志输出

        internal void AddLine(string text, Color color)
        {
            lock (_pendingLock) _pending.Add(new LogLine(text, color));
        }

        internal void Info(string text) { AddLine(text, Theme.Text); }
        internal void Dim(string text) { AddLine(text, Theme.TextDim); }
        internal void Ok(string text) { AddLine("[OK] " + text, Theme.Green); }
        internal void Warn(string text) { AddLine("[!] " + text, Theme.Amber); }
        internal void Err(string text) { AddLine("[x] " + text, Theme.Red); }
        internal void Cmd(string text) { AddLine("$ adb " + text, Theme.Accent); }

        private void FlushPending()
        {
            List<LogLine> batch;
            lock (_pendingLock)
            {
                if (_pending.Count == 0) return;
                batch = new List<LogLine>(_pending);
                _pending.Clear();
            }

            bool scroll = _autoScroll.Checked;
            if (_log.TextLength > 500000)
            {
                _log.Select(0, 200000);
                _log.SelectedText = "";
            }

            SendMessage(_log.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                foreach (LogLine line in batch)
                {
                    _log.SelectionStart = _log.TextLength;
                    _log.SelectionLength = 0;
                    _log.SelectionColor = line.Color;
                    _log.AppendText(line.Text + "\r\n");
                }
                _log.SelectionStart = _log.TextLength;
                _log.SelectionLength = 0;
                _log.SelectionColor = Theme.Text;
            }
            finally
            {
                SendMessage(_log.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                _log.Invalidate();
            }

            if (scroll)
            {
                _log.SelectionStart = _log.TextLength;
                _log.ScrollToCaret();
            }
        }

        private void SaveLog()
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "保存输出日志";
                dlg.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dlg.FileName = "adb-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dlg.FileName, _log.Text, new UTF8Encoding(false));
                    Ok("日志已保存: " + dlg.FileName);
                }
                catch (Exception ex) { Err("保存失败: " + ex.Message); }
            }
        }

        #endregion

        #region 命令队列

        private void SafeInvoke(Action action)
        {
            if (IsDisposed || Disposing) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { }
        }

        private bool EnsureAdb()
        {
            if (_adbPath != null && File.Exists(_adbPath)) return true;
            Warn("还没有指定 adb.exe，请点右上角「选择 adb」指定。");
            return false;
        }

        /// <summary>不需要 -s 的命令（作用于 adb 服务本身）</summary>
        private static bool NeedsSerial(string args)
        {
            string[] server = { "start-server", "kill-server", "devices", "version", "connect", "disconnect", "wait-for-device", "get-state", "get-serialno" };
            foreach (string s in server)
            {
                if (args.StartsWith(s, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        private string PrefixSerial(string args)
        {
            if (_serial == null || !NeedsSerial(args)) return args;
            return "-s " + Quote(_serial) + " " + args;
        }

        /// <summary>Windows 命令行参数转义</summary>
        internal static string Quote(string s)
        {
            if (s.Length > 0 && s.IndexOfAny(new char[] { ' ', '\t', '"', '&', '^', '<', '>', '|', '(' }) < 0) return s;
            StringBuilder sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in s)
            {
                if (c == '\\') { backslashes++; sb.Append(c); continue; }
                if (c == '"') { sb.Append('\\', backslashes + 1).Append('"'); backslashes = 0; continue; }
                backslashes = 0;
                sb.Append(c);
            }
            sb.Append('\\', backslashes).Append('"');
            return sb.ToString();
        }

        private void Enqueue(Job job)
        {
            job.Exe = _adbPath;
            lock (_jobsLock) _jobs.Enqueue(job);
            EnsureWorker();
            UpdateQueueStatus();
        }

        private void EnsureWorker()
        {
            lock (_jobsLock)
            {
                if (_worker != null && _worker.IsAlive) return;
                _worker = new Thread(WorkerLoop);
                _worker.IsBackground = true;
                _worker.Name = "adb-worker";
                _worker.Start();
            }
        }

        private void WorkerLoop()
        {
            while (true)
            {
                Job job;
                lock (_jobsLock)
                {
                    if (_jobs.Count == 0)
                    {
                        _worker = null;
                        SafeInvoke(UpdateQueueStatus);
                        return;
                    }
                    job = _jobs.Dequeue();
                }

                _cancelCurrent = false;
                _runningTitle = job.Title;
                SafeInvoke(UpdateQueueStatus);

                ProcResult result = Proc.Run(job.Exe, job.Args, job.TimeoutMs,
                    delegate(string line) { if (!job.Silent && (job.Filter == null || job.Filter(line))) AddLine("  " + line, Theme.Text); },
                    delegate(string line) { if (!job.Silent && (job.Filter == null || job.Filter(line))) AddLine("  " + line, Theme.Amber); },
                    delegate(Process p) { _currentProc = p; },
                    delegate { return _cancelCurrent; });

                _currentProc = null;
                _runningTitle = null;

                if (result.Failure != null) Err(result.Message);
                else if (result.Canceled) Warn(job.Title + "：已停止");
                else if (result.TimedOut) Err(job.Title + "：超时（" + (job.TimeoutMs / 1000) + " 秒）");
                else if (result.ExitCode != 0) Err(job.Title + "：命令返回退出码 " + result.ExitCode);

                if (job.Done != null) SafeInvoke(delegate { job.Done(result); });

                SafeInvoke(delegate
                {
                    UpdateQueueStatus();
                    if (result.Ok && result.ElapsedMs > 2500 && job.TimeoutMs != 0)
                        Dim("  · 用时 " + (result.ElapsedMs / 1000.0).ToString("0.0") + " 秒");
                });
            }
        }

        private void UpdateQueueStatus()
        {
            int pending;
            lock (_jobsLock) pending = _jobs.Count;
            if (_runningTitle != null)
                _statusLeft.Text = "正在执行：" + _runningTitle + (pending > 0 ? "（队列 " + pending + "）" : "");
            else if (pending > 0)
                _statusLeft.Text = "队列中 " + pending + " 条命令";
            else
                _statusLeft.Text = "就绪";
            _stopBtn.Enabled = _runningTitle != null;
            if (_logcatRunning) _logToggle.Active = true;
        }

        private void CancelCurrent()
        {
            if (_runningTitle == null) { Warn("当前没有正在执行的命令。"); return; }
            _cancelCurrent = true;
            Warn("正在停止：" + _runningTitle);
        }

        #endregion

        #region 命令入口

        private void RunAdbLogged(string title, string args, int timeoutMs, Action<ProcResult> done = null, Func<string, bool> filter = null, bool echo = true)
        {
            if (!EnsureAdb()) return;
            string full = PrefixSerial(args);
            if (echo) Cmd(full);
            Enqueue(new Job { Title = title, Args = full, TimeoutMs = timeoutMs, Done = done, Filter = filter });
        }

        private void RunRawLogged(string title, string args, int timeoutMs)
        {
            if (!EnsureAdb()) return;
            Cmd(args);
            Enqueue(new Job { Title = title, Args = args, TimeoutMs = timeoutMs });
        }

        private void RunShellLogged(string title, string shellCmd, int timeoutMs = 0,
                                    Action<ProcResult> done = null, Func<string, bool> filter = null, bool echo = true)
        {
            if (!RequireDevice()) return;
            if (timeoutMs <= 0) timeoutMs = 120000;
            RunAdbLogged(title, "shell " + shellCmd, timeoutMs, done, filter, echo);
        }

        private void RefreshDevices(bool showOutput, string reason)
        {
            if (_adbPath == null) { UpdateStatusText(); return; }
            if (showOutput) Dim("正在刷新设备列表…");
            Enqueue(new Job
            {
                Title = showOutput ? "读取设备列表" : "自动检测设备",
                Args = "devices -l",
                TimeoutMs = 15000,
                Silent = true,
                Done = delegate(ProcResult r) { if (r.StdOut.Length > 0) UpdateDevices(ParseDevices(r.StdOut)); }
            });
        }

        private static bool NotDeviceHeader(string line)
        {
            return line.IndexOf("List of devices attached", StringComparison.OrdinalIgnoreCase) < 0;
        }

        internal static List<AdbDevice> ParseDevices(string output)
        {
            List<AdbDevice> list = new List<AdbDevice>();
            if (output == null) return list;
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("List of devices")) continue;
                if (line.StartsWith("*")) continue;

                string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                AdbDevice d = new AdbDevice();
                d.Serial = parts[0];
                int index = 1;
                if (parts[1] == "no" && parts.Length > 2 && parts[2] == "permissions")
                {
                    d.State = "no permissions";
                    index = 3;
                }
                else
                {
                    d.State = parts[1];
                    index = 2;
                }
                for (int i = index; i < parts.Length; i++)
                {
                    int colon = parts[i].IndexOf(':');
                    if (colon <= 0) continue;
                    string key = parts[i].Substring(0, colon);
                    string value = parts[i].Substring(colon + 1);
                    if (key == "model") d.Model = value;
                    else if (key == "product") d.Product = value;
                    else if (key == "transport_id") d.TransportId = value;
                }
                list.Add(d);
            }
            return list;
        }

        private bool RequireDevice()
        {
            if (!EnsureAdb()) return false;
            if (_serial == null || _serial.Length == 0)
            {
                Warn("请先在左上角下拉框里选择一台设备。");
                return false;
            }
            AdbDevice current = _deviceCombo.SelectedItem as AdbDevice;
            if (current != null && !current.Online)
                Warn("设备 " + _serial + " 当前状态：" + current.StateLabel + "，命令可能失败。");
            return true;
        }

        private void RunCustomCommand()
        {
            string cmd = _cmdBox.Text.Trim();
            if (cmd.Length == 0) return;
            _cmdBox.Clear();

            string lower = cmd.ToLowerInvariant();
            if (lower.StartsWith("adb ")) cmd = cmd.Substring(4).Trim();
            if (cmd.StartsWith("-s ") || cmd.StartsWith("-d ") || cmd.StartsWith("-e "))
            {
                RunRawLogged("自定义命令", cmd, 0);
                return;
            }
            RunAdbLogged("自定义命令", cmd, 0);
        }

        private void RestartAdbServer()
        {
            if (!EnsureAdb()) return;
            RunAdbLogged("停止 adb 服务", "kill-server", 20000, delegate
            {
                RunAdbLogged("启动 adb 服务", "start-server", 30000, delegate { RefreshDevices(true, null); });
            });
        }

        private void OpenAdbFolder()
        {
            if (_adbPath == null) { Warn("还没有指定 adb.exe。"); return; }
            try { Process.Start("explorer.exe", "/select," + Quote(_adbPath)); }
            catch (Exception ex) { Err("打不开目录: " + ex.Message); }
        }

        internal static void RevealInExplorer(string path)
        {
            try
            {
                if (Directory.Exists(path)) Process.Start("explorer.exe", Quote(path));
                else Process.Start("explorer.exe", "/select," + Quote(path));
            }
            catch { }
        }

        internal static void ShellOpen(string path)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(path);
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch { }
        }

        internal static string Stamp()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }

        #endregion
    }
}
