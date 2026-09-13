using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VMMicControl
{
    /// <summary>Voicemeeter 版本类型</summary>
    public enum VoicemeeterKind
    {
        Unknown = 0,
        Standard = 1,
        Banana = 2,
        Potato = 3
    }

    internal static class Native
    {
        internal const string Dll = "VoicemeeterRemote64.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_Login();

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_Logout();

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_RunVoicemeeter(int vType);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_GetVoicemeeterType(ref int pType);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_IsParametersDirty();

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_GetParameterFloat(
            [MarshalAs(UnmanagedType.LPStr)] string szParamName, ref float pValue);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_SetParameterFloat(
            [MarshalAs(UnmanagedType.LPStr)] string szParamName, float value);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_SetParameters(
            [MarshalAs(UnmanagedType.LPStr)] string szScript);

        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int VBVMR_GetParameterStringA(
            [MarshalAs(UnmanagedType.LPStr)] string szParamName, byte[] szString);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetDllDirectory(string lpPathName);
    }

    /// <summary>Voicemeeter Remote API 的轻量封装</summary>
    public sealed class Voicemeeter : IDisposable
    {
        private VoicemeeterKind _kind = VoicemeeterKind.Unknown;
        private bool _loggedIn;

        public bool Connected
        {
            get { return _loggedIn; }
        }

        public VoicemeeterKind Kind
        {
            get { return _kind; }
        }

        /// <summary>物理（硬件）输入条数量：Standard 2 / Banana 3 / Potato 5</summary>
        public int HardwareInputs
        {
            get
            {
                switch (_kind)
                {
                    case VoicemeeterKind.Standard: return 2;
                    case VoicemeeterKind.Banana: return 3;
                    case VoicemeeterKind.Potato: return 5;
                    default: return 0;
                }
            }
        }

        /// <summary>输入条总数（含虚拟输入）：Standard 3 / Banana 5 / Potato 8</summary>
        public int TotalStrips
        {
            get
            {
                switch (_kind)
                {
                    case VoicemeeterKind.Standard: return 3;
                    case VoicemeeterKind.Banana: return 5;
                    case VoicemeeterKind.Potato: return 8;
                    default: return 0;
                }
            }
        }

        public string KindName
        {
            get
            {
                switch (_kind)
                {
                    case VoicemeeterKind.Standard: return "Voicemeeter";
                    case VoicemeeterKind.Banana: return "Voicemeeter Banana";
                    case VoicemeeterKind.Potato: return "Voicemeeter Potato";
                    default: return "未知";
                }
            }
        }

        /// <summary>定位 Voicemeeter 安装目录（优先从运行中的进程推断）</summary>
        public static string FindInstallDirectory()
        {
            Process[] procs = Process.GetProcesses();
            foreach (Process p in procs)
            {
                try
                {
                    if (p.MainModule == null) continue;
                    string name = p.MainModule.ModuleName.ToLowerInvariant();
                    if (!name.StartsWith("voicemeeter") && !name.StartsWith("voicemeeterpro")) continue;
                    string dir = Path.GetDirectoryName(p.MainModule.FileName);
                    if (dir != null && File.Exists(Path.Combine(dir, Native.Dll)))
                    {
                        p.Dispose();
                        return dir;
                    }
                }
                catch
                {
                    // 部分系统进程无法访问 MainModule，忽略
                }
                finally
                {
                    p.Dispose();
                }
            }

            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VB", "Voicemeeter"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VB", "Voicemeeter")
            };
            foreach (string dir in candidates)
            {
                if (File.Exists(Path.Combine(dir, Native.Dll))) return dir;
            }
            return candidates[0];
        }

        public static bool IsRunning
        {
            get
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        string n = p.ProcessName.ToLowerInvariant();
                        if (n.StartsWith("voicemeeter")) { p.Dispose(); return true; }
                    }
                    catch { /* ignore */ }
                    finally { p.Dispose(); }
                }
                return false;
            }
        }

        /// <summary>连接 Voicemeeter（未运行则失败，可配合 <see cref="Launch"/>）</summary>
        public bool Login()
        {
            string dir = FindInstallDirectory();
            if (!string.IsNullOrEmpty(dir)) Native.SetDllDirectory(dir);

            int r = Native.VBVMR_Login();
            _loggedIn = r >= 0;
            if (_loggedIn)
            {
                int type = 0;
                if (Native.VBVMR_GetVoicemeeterType(ref type) >= 0)
                    _kind = (VoicemeeterKind)type;
            }
            else
            {
                _kind = VoicemeeterKind.Unknown;
            }
            return _loggedIn;
        }

        public void Logout()
        {
            if (_loggedIn)
            {
                try { Native.VBVMR_Logout(); }
                catch { /* ignore */ }
                _loggedIn = false;
            }
        }

        /// <summary>启动 Voicemeeter（按 Potato → Banana → Standard 顺序尝试）</summary>
        public bool Launch()
        {
            int[] order = new int[] { 3, 2, 1 };
            foreach (int vType in order)
            {
                try
                {
                    if (Native.VBVMR_RunVoicemeeter(vType) >= 0) return true;
                }
                catch { /* ignore */ }
            }
            return false;
        }

        /// <summary>
        /// 查询参数是否有变化。注意：Remote API 的参数缓存正是在调用本函数时刷新的，
        /// 所以读取参数前必须先调用它，并且只有返回 false（已稳定）时读到的才是最新值。
        /// </summary>
        public bool IsDirty()
        {
            try { return Native.VBVMR_IsParametersDirty() > 0; }
            catch { return false; }
        }

        /// <summary>
        /// 等待参数同步完成。Remote API 在 dirty 标志清零之后才会返回最新值，
        /// 所以写入参数后必须等这一步，读回来才是新状态。
        /// </summary>
        public bool WaitForSync(int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (!IsDirty()) return true;
                Thread.Sleep(10);
                waited += 10;
            }
            return false;
        }

        public bool GetFloat(string param, out float value)
        {
            value = 0f;
            if (!_loggedIn) return false;
            try { return Native.VBVMR_GetParameterFloat(param, ref value) >= 0; }
            catch { return false; }
        }

        public bool SetFloat(string param, float value)
        {
            if (!_loggedIn) return false;
            try { return Native.VBVMR_SetParameterFloat(param, value) >= 0; }
            catch { return false; }
        }

        public string GetString(string param)
        {
            if (!_loggedIn) return null;
            try
            {
                byte[] buffer = new byte[512];
                if (Native.VBVMR_GetParameterStringA(param, buffer) < 0) return null;
                int len = Array.IndexOf(buffer, (byte)0);
                if (len < 0) len = buffer.Length;
                return Encoding.Default.GetString(buffer, 0, len).Trim();
            }
            catch { return null; }
        }

        // ---- 通道级封装 ----

        public bool GetMute(int strip)
        {
            float v;
            if (!GetFloat("Strip[" + strip + "].Mute", out v)) return false;
            return v > 0.5f;
        }

        public bool SetMute(int strip, bool muted)
        {
            return SetFloat("Strip[" + strip + "].Mute", muted ? 1f : 0f);
        }

        public bool ToggleMute(int strip)
        {
            return SetMute(strip, !GetMute(strip));
        }

        public float GetGain(int strip)
        {
            float v;
            return GetFloat("Strip[" + strip + "].Gain", out v) ? v : 0f;
        }

        public bool SetGain(int strip, float db)
        {
            if (db < -60f) db = -60f;
            if (db > 12f) db = 12f;
            return SetFloat("Strip[" + strip + "].Gain", db);
        }

        /// <summary>通道显示名：优先用户标签，其次设备名，最后回退默认名</summary>
        public string GetChannelName(int strip)
        {
            string[] probes = new string[]
            {
                "Strip[" + strip + "].Label",
                "Strip[" + strip + "].device.name",
                "Strip[" + strip + "].Device.name"
            };
            foreach (string p in probes)
            {
                string s = GetString(p);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
        }

        public void Dispose()
        {
            Logout();
        }
    }
}
