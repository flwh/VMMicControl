using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AdbGui
{
    internal sealed partial class MainForm
    {
        #region 应用安装 / 管理

        private void InstallApk(bool grant)
        {
            if (!RequireDevice()) return;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = grant ? "选择 APK（安装后自动授予运行时权限）" : "选择 APK";
                dlg.Filter = "Android 安装包 (*.apk)|*.apk|所有文件 (*.*)|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string[] files = dlg.FileNames;
                Info("准备安装 " + files.Length + " 个 APK…");
                foreach (string file in files) InstallOne(file, grant);
            }
        }

        private void InstallOne(string file, bool grant)
        {
            string name = Path.GetFileName(file);
            string args = "install " + (grant ? "-r -g " : "-r ") + Quote(file);
            RunAdbLogged("安装 " + name, args, 600000, delegate(ProcResult r)
            {
                string text = (r.StdOut + " " + r.StdErr).ToLowerInvariant();
                if (text.IndexOf("success", StringComparison.Ordinal) >= 0)
                    Ok(name + " 安装成功");
                else if (text.IndexOf("already exists", StringComparison.Ordinal) >= 0)
                    Err(name + "：设备上已存在该应用的更高版本");
                else if (text.IndexOf("insufficient storage", StringComparison.Ordinal) >= 0)
                    Err(name + "：设备存储空间不足");
                else if (text.IndexOf("install_failed_incompatible", StringComparison.Ordinal) >= 0)
                    Err(name + "：与设备不兼容（CPU 架构或系统版本不符）");
                else if (!r.Ok)
                    Err(name + " 安装失败，请看上方输出");
            });
        }

        private void OpenAppManager()
        {
            if (!RequireDevice()) return;

            ListDialog dlg = new ListDialog("应用管理",
                "正在读取已安装的第三方应用…", "没有读取到第三方应用",
                new string[] { "卸载", "停止", "清除数据", "启动" }, null);

            dlg.ActionChosen += delegate(string pkg, string action)
            {
                if (action == "卸载")
                {
                    if (MessageBox.Show(this, "确定卸载 " + pkg + " 吗？", "卸载应用",
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    RunAdbLogged("卸载 " + pkg, "uninstall " + Quote(pkg), 180000, delegate(ProcResult r)
                    {
                        if (r.Ok && (r.StdOut + r.StdErr).IndexOf("Success", StringComparison.OrdinalIgnoreCase) >= 0)
                            Ok(pkg + " 已卸载");
                        else Err(pkg + " 卸载失败（系统应用或正在使用中）");
                        LoadPackages(dlg);
                    });
                }
                else if (action == "停止")
                {
                    RunShellLogged("强制停止 " + pkg, "am force-stop " + Quote(pkg), 30000);
                }
                else if (action == "清除数据")
                {
                    if (MessageBox.Show(this, "清除 " + pkg + " 的全部数据？此操作不可撤销。", "清除应用数据",
                            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    RunShellLogged("清除数据 " + pkg, "pm clear " + Quote(pkg), 60000);
                }
                else if (action == "启动")
                {
                    RunShellLogged("启动 " + pkg, "monkey -p " + Quote(pkg) + " -c android.intent.category.LAUNCHER 1", 40000);
                }
            };

            dlg.Show(this);
            LoadPackages(dlg);
        }

        private void LoadPackages(ListDialog dlg)
        {
            RunShellLogged("列出第三方应用", "pm list packages -3", 90000, delegate(ProcResult r)
            {
                if (dlg == null || dlg.IsDisposed) return;
                List<string> items = new List<string>();
                foreach (string raw in r.StdOut.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("package:")) items.Add(line.Substring("package:".Length));
                }
                items.Sort(StringComparer.OrdinalIgnoreCase);
                dlg.SetItems(items);
                if (items.Count == 0) Warn("没有读取到第三方应用（可能需要先解锁手机）。");
            });
        }

        #endregion



        #region 截屏

        /// <summary>抓取设备截图，在工具内展示；窗口关闭后自动删除本机临时文件</summary>
        private void Screenshot()
        {
            if (!RequireDevice()) return;
            string stamp = Stamp();
            string remote = "/sdcard/adbgui_" + stamp + ".png";
            string dir = Path.Combine(Path.GetTempPath(), "AdbGui");
            try { Directory.CreateDirectory(dir); } catch { }
            string local = Path.Combine(dir, "screenshot_" + stamp + ".png");

            Info("正在截屏…");
            RunShellLogged("截屏", "screencap -p " + remote, 60000, delegate(ProcResult r)
            {
                if (!r.Ok) { Err("截屏失败，设备可能未解锁。"); return; }
                RunAdbLogged("导出截图", "pull " + remote + " " + Quote(local), 300000, delegate(ProcResult r2)
                {
                    // 立即清理设备端临时文件
                    RunShellLogged("清理设备端临时文件", "rm -f " + remote, 20000, null, delegate { return false; });
                    if (!r2.Ok) { Err("导出截图失败。"); return; }
                    if (!File.Exists(local)) { Err("截图文件未生成。"); return; }
                    SafeInvoke(delegate
                    {
                        ScreenshotForm f = new ScreenshotForm(local);
                        f.Show(this);
                    });
                });
            });
        }

        #endregion


        #region 无线调试 / 输入 / 信息

        private void ConnectWifi()
        {
            if (!EnsureAdb()) return;
            string input = InputDialog.Ask(this, "连接无线设备", "设备 IP 与端口",
                Settings.Get("wifiTarget", ""), false,
                "手机与电脑需在同一网络。先用数据线连接并点「开启无线调试 (tcpip 5555)」，然后拔线输入手机 IP。",
                "192.168.1.23:5555");
            if (string.IsNullOrEmpty(input)) return;

            Settings.Set("wifiTarget", input);
            Settings.Save();

            RunAdbLogged("连接 " + input, "connect " + Quote(input), 40000, delegate(ProcResult r)
            {
                if (r.Ok && (r.StdOut + r.StdErr).IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Ok(input + " 连接成功");
                    RefreshDevices(true, null);
                }
                else Err("连接 " + input + " 失败，请检查 IP、端口和 Wi-Fi 是否同一网络。");
            });
        }

        private void ShowWifiIp()
        {
            if (!RequireDevice()) return;
            Info("读取设备网络地址…");
            RunShellLogged("设备 IP", "ip -f inet addr show wlan0", 30000, null,
                delegate(string line)
                {
                    return line.IndexOf("inet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           line.IndexOf("wlan0", StringComparison.OrdinalIgnoreCase) >= 0;
                });
        }

        private void InputText()
        {
            if (!RequireDevice()) return;
            string text = InputDialog.Ask(this, "输入文字",
                "要输入到手机的文字（先把光标放到手机的输入框里）", "", true,
                "空格会自动转义；中文需要手机端输入法支持，不一定生效。", "hello world");
            if (string.IsNullOrEmpty(text)) return;

            text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace(" ", "%s");
            RunShellLogged("输入文字", "input text " + Quote(text), 30000, null, delegate { return false; });
        }

        private void DeviceInfo()
        {
            if (!RequireDevice()) return;

            // 一次 getprop 取全部属性（含定制 UI 判定），避免逐条命令刷屏
            RunShellLogged("读取设备信息", "getprop", 20000, delegate(ProcResult r)
            {
                string raw = r.StdOut ?? "";
                Dictionary<string, string> p = ParseProps(raw);
                ShowProp(p, "品牌", "ro.product.brand");
                ShowProp(p, "型号", "ro.product.model");
                ShowProp(p, "设备代号", "ro.product.device");
                ShowProp(p, "Android 版本", "ro.build.version.release");
                ShowProp(p, "API 级别", "ro.build.version.sdk");
                ShowCpuModel(p);
                ShowProp(p, "CPU 架构", "ro.product.cpu.abi");
                ShowProp(p, "构建版本", "ro.build.display.id");
                ShowProp(p, "序列号", "ro.serialno");
                string rom = DetectRomName(p, raw);
                if (rom == null)
                {
                    Dim("  定制 UI：未识别（可能是原生 Android / AOSP）");
                }
                else
                {
                    Info("  定制 UI：" + rom);
                    string ver = DetectRomVersion(p, rom);
                    if (!string.IsNullOrEmpty(ver)) Info("  定制 UI 版本：" + ver);
                }
            }, delegate { return false; }, false);

            RunShellLogged("内存信息", "cat /proc/meminfo", 20000, delegate(ProcResult r)
            {
                long total = 0, free = 0, avail = 0;
                using (StringReader sr = new StringReader(r.StdOut ?? ""))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("MemTotal:")) total = MemKb(line);
                        else if (line.StartsWith("MemFree:")) free = MemKb(line);
                        else if (line.StartsWith("MemAvailable:")) avail = MemKb(line);
                    }
                }
                if (total > 0) Info("  总内存：" + FormatKb(total));
                if (avail > 0) Info("  可用内存：" + FormatKb(avail));
                if (free > 0) Info("  空闲内存：" + FormatKb(free));
            }, delegate { return false; }, false);

            RunShellLogged("电池信息", "dumpsys battery", 20000, delegate(ProcResult r)
            {
                BatteryInfo(r.StdOut);
            }, delegate { return false; }, false);

            // 应用数量：-s 系统应用，-3 第三方（用户安装）应用
            RunShellLogged("系统应用数量", "pm list packages -s", 30000, delegate(ProcResult r)
            {
                Info("  系统应用：" + CountPackages(r.StdOut) + " 个");
            }, delegate { return false; }, false);

            RunShellLogged("用户应用数量", "pm list packages -3", 30000, delegate(ProcResult r)
            {
                Info("  用户应用：" + CountPackages(r.StdOut) + " 个");
            }, delegate { return false; }, false);

            ScreenInfo();
            RefreshRateInfo();
        }

        /// <summary>通过 ADB 激活 Shizuku：调用 Shizuku 应用内置的 start.sh 启动其特权服务</summary>
        private void ActivateShizuku()
        {
            if (!RequireDevice()) return;
            RunShellLogged("检查 Shizuku", "pm path moe.shizuku.privileged.api", 15000, delegate(ProcResult r)
            {
                if ((r.StdOut ?? "").Trim().Length == 0)
                {
                    Err("未检测到 Shizuku 应用，请先安装 Shizuku（包名 moe.shizuku.privileged.api）。");
                    return;
                }
                Info("正在通过 ADB 启动 Shizuku 服务…");
                string script = "/storage/emulated/0/Android/data/moe.shizuku.privileged.api/start.sh";
                RunShellLogged("激活 Shizuku", "sh " + script, 30000, delegate(ProcResult r2)
                {
                    if (r2.Ok)
                    {
                        Ok("Shizuku 服务已启动，请在手机上打开 Shizuku 应用查看运行状态。");
                    }
                    else
                    {
                        Warn("Shizuku 启动未成功。请先打开一次 Shizuku 应用并按提示生成启动脚本，然后重试。");
                        string se = (r2.StdErr ?? "").Trim();
                        if (se.Length > 0) Dim(se);
                    }
                }, delegate { return false; });
            }, delegate { return false; });
        }

        private static Dictionary<string, string> ParseProps(string text)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            using (StringReader sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    int i = line.IndexOf("]: [");
                    if (i > 0 && line.StartsWith("["))
                    {
                        string key = line.Substring(1, i - 1);
                        string val = line.Substring(i + 4);
                        if (val.EndsWith("]")) val = val.Substring(0, val.Length - 1);
                        d[key] = val;
                    }
                }
            }
            return d;
        }

        /// <summary>解析 /proc/meminfo 中形如 "MemTotal:  5847560 kB" 的数值（单位 kB）</summary>
        private static long MemKb(string line)
        {
            int i = line.IndexOf(':');
            if (i < 0) return 0;
            string rest = line.Substring(i + 1).Trim();
            int j = 0;
            while (j < rest.Length && char.IsDigit(rest[j])) j++;
            long v;
            return long.TryParse(rest.Substring(0, j), out v) ? v : 0;
        }

        /// <summary>把 kB 数值格式化为易读的 MB / GB</summary>
        private static string FormatKb(long kb)
        {
            double mb = kb / 1024.0;
            if (mb >= 1024) return (mb / 1024.0).ToString("0.00") + " GB";
            return mb.ToString("0") + " MB";
        }

        /// <summary>从 MIUI / HyperOS 版本字符串中提取主版本号（如 "V816.0.1.0" -> 816，"V14" -> 14）</summary>
        private static int MiuiVersionNum(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int i = s.IndexOf('V');
            if (i < 0) i = s.IndexOf('v');
            if (i < 0) i = 0; else i++;
            int n = 0;
            while (i < s.Length && char.IsDigit(s[i]))
            {
                n = n * 10 + (s[i] - '0');
                i++;
            }
            return n;
        }

        /// <summary>根据属性表与原始文本判断设备运行的定制 UI 名称</summary>
        private static string DetectRomName(Dictionary<string, string> p, string raw)
        {
            string low = raw.ToLowerInvariant();
            // 小米：增量版本号（如 V816.x；MIUI 14 为 V14.x）≥ V816 视为 HyperOS
            string mv, incX;
            int miuiNum = 0;
            if (p.TryGetValue("ro.miui.ui.version.name", out mv)) miuiNum = MiuiVersionNum(mv);
            else if (p.TryGetValue("ro.build.version.incremental", out incX)) miuiNum = MiuiVersionNum(incX);
            if (miuiNum >= 816) return "小米 HyperOS";
            if (low.Contains("hyperos")) return "小米 HyperOS";
            if (p.ContainsKey("ro.miui.ui.version.name") || low.Contains("miui")) return "小米 MIUI";
            if (low.Contains("harmonyos")) return "华为 HarmonyOS";
            if (p.ContainsKey("ro.build.version.emui") || p.ContainsKey("ro.hw.emui.version") || low.Contains("emui")) return "华为 EMUI";
            if (p.ContainsKey("ro.build.version.coloros") || p.ContainsKey("ro.coloros.version") ||
                p.ContainsKey("ro.build.version.opporom") || low.Contains("coloros") || low.Contains("opporom")) return "OPPO / 一加 ColorOS";
            if (p.ContainsKey("ro.vivo.os.name") || p.ContainsKey("ro.vivo.os.version") ||
                low.Contains("originos") || (low.Contains("vivo") && low.Contains("os"))) return "vivo Funtouch / OriginOS";
            if (p.ContainsKey("ro.build.version.samsung") || low.Contains("oneui") || low.Contains("one ui")) return "三星 One UI";
            if (p.ContainsKey("ro.oxygen.version") || p.ContainsKey("ro.oxygen.version.name") || low.Contains("oxygenos")) return "一加 OxygenOS";
            if (p.ContainsKey("ro.realme.ui.version") || low.Contains("realmeui") || low.Contains("realme ui")) return "realme UI";
            if (p.ContainsKey("ro.build.version.flyme") || p.ContainsKey("ro.flyme.version") || low.Contains("flyme")) return "魅族 Flyme";
            if (p.ContainsKey("ro.asus.ui.version") || low.Contains("zenui")) return "华硕 ZenUI";
            return null;
        }

        private static string DetectRomVersion(Dictionary<string, string> p, string rom)
        {
            string v, e, c, vo, s, o, r, f, a;
            switch (rom)
            {
                case "小米 HyperOS":
                    if (p.TryGetValue("ro.build.version.incremental", out v) && !string.IsNullOrEmpty(v)) return v;
                    if (p.TryGetValue("ro.miui.ui.version.name", out v) && !string.IsNullOrEmpty(v)) return v;
                    return null;
                case "小米 MIUI":
                    if (p.TryGetValue("ro.miui.ui.version.name", out v) && !string.IsNullOrEmpty(v)) return v;
                    if (p.TryGetValue("ro.build.version.incremental", out v) && !string.IsNullOrEmpty(v)) return v;
                    return null;
                case "华为 EMUI":
                case "华为 HarmonyOS":
                    if (p.TryGetValue("ro.build.version.emui", out e) && !string.IsNullOrEmpty(e)) return e;
                    if (p.TryGetValue("ro.hw.emui.version", out e) && !string.IsNullOrEmpty(e)) return e;
                    return null;
                case "OPPO / 一加 ColorOS":
                    if (p.TryGetValue("ro.build.version.coloros", out c) && !string.IsNullOrEmpty(c)) return c;
                    if (p.TryGetValue("ro.coloros.version", out c) && !string.IsNullOrEmpty(c)) return c;
                    return null;
                case "vivo Funtouch / OriginOS":
                    if (p.TryGetValue("ro.vivo.os.version", out vo) && !string.IsNullOrEmpty(vo)) return vo;
                    if (p.TryGetValue("ro.vivo.os.name", out vo) && !string.IsNullOrEmpty(vo)) return vo;
                    return null;
                case "三星 One UI":
                    return p.TryGetValue("ro.build.version.samsung", out s) ? s : null;
                case "一加 OxygenOS":
                    return p.TryGetValue("ro.oxygen.version.name", out o) ? o : null;
                case "realme UI":
                    return p.TryGetValue("ro.realme.ui.version", out r) ? r : null;
                case "魅族 Flyme":
                    return p.TryGetValue("ro.build.version.flyme", out f) ? f : null;
                case "华硕 ZenUI":
                    return p.TryGetValue("ro.asus.ui.version", out a) ? a : null;
            }
            return null;
        }

        private void ShowProp(Dictionary<string, string> p, string label, string key)
        {
            string v;
            if (p.TryGetValue(key, out v) && !string.IsNullOrEmpty(v)) Info("  " + label + "：" + v);
            else Dim("  " + label + "：未知");
        }

        /// <summary>
        /// 展示 CPU（SoC）通用型号：优先 ro.soc.model，依次回退平台代号 / 主板代号 / 硬件代号，
        /// 并尽量带上 ro.soc.manufacturer 厂商名。全部复用同一次 getprop 的结果，不额外执行命令。
        /// </summary>
        private void ShowCpuModel(Dictionary<string, string> p)
        {
            string[] modelKeys = { "ro.soc.model", "ro.board.platform", "ro.product.board", "ro.soc.platform", "ro.hardware" };
            string model = null;
            foreach (string key in modelKeys)
            {
                string v;
                if (p.TryGetValue(key, out v) && v != null && v.Trim().Length > 0)
                {
                    model = v.Trim();
                    break;
                }
            }

            string maker = null;
            string m;
            if (p.TryGetValue("ro.soc.manufacturer", out m) && m != null && m.Trim().Length > 0) maker = m.Trim();

            if (model == null)
            {
                Dim("  CPU 型号：未知");
                return;
            }

            // 优先显示市场通用型号（SM7325 → 骁龙 778G），未收录时原样显示代号
            string name = SocNames.Translate(model);
            if (name != null)
            {
                Info("  CPU 型号：" + name);
                return;
            }

            string prettyMaker = SocNames.TranslateMaker(maker);
            Info("  CPU 型号：" + (prettyMaker == null ? model : prettyMaker + " " + model));
        }

        /// <summary>统计 pm list packages 输出中的包数量（每行形如 package:com.xxx）</summary>
        private static int CountPackages(string output)
        {
            int count = 0;
            using (StringReader sr = new StringReader(output ?? ""))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("package:", StringComparison.Ordinal)) count++;
                }
            }
            return count;
        }

        /// <summary>在多行文本中查找以 prefix 开头的行并返回其后的内容</summary>
        private static string FindLineValue(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text)) return null;
            using (StringReader sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (t.StartsWith(prefix)) return t.Substring(prefix.Length).Trim();
                }
            }
            return null;
        }

        /// <summary>解析形如 "  key: value" 的行为字典（用于 dumpsys 等输出）</summary>
        private static Dictionary<string, string> ParseColonProps(string text)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return d;
            using (StringReader sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string t = line.Trim();
                    int i = t.IndexOf(':');
                    if (i <= 0) continue;
                    string key = t.Substring(0, i).Trim();
                    if (!d.ContainsKey(key)) d[key] = t.Substring(i + 1).Trim();
                }
            }
            return d;
        }

        /// <summary>解析 dumpsys battery 输出并以中文展示关键项</summary>
        private void BatteryInfo(string dump)
        {
            Dictionary<string, string> b = ParseColonProps(dump);
            string v;
            if (b.TryGetValue("level", out v) && v.Length > 0) Info("  电量：" + v + "%");
            if (b.TryGetValue("status", out v)) Info("  充电状态：" + BatteryStatusText(v));
            if (b.TryGetValue("health", out v)) Info("  电池健康：" + BatteryHealthText(v));
            if (b.TryGetValue("temperature", out v))
            {
                double t;
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out t))
                    Info("  温度：" + (t / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + " °C");
            }
        }

        private static string BatteryStatusText(string code)
        {
            switch (code)
            {
                case "2": return "充电中";
                case "3": return "放电中";
                case "4": return "未充电";
                case "5": return "已充满";
                default: return "未知";
            }
        }

        private static string BatteryHealthText(string code)
        {
            switch (code)
            {
                case "2": return "良好";
                case "3": return "过热";
                case "4": return "损坏";
                case "5": return "过压";
                case "6": return "故障";
                case "7": return "过冷";
                default: return "未知";
            }
        }

        /// <summary>电池状态：复用 dumpsys battery 解析，并附带 HyperOS / 小米电池健康度</summary>
        private void BatteryStatus()
        {
            if (!RequireDevice()) return;
            RunShellLogged("电池状态", "dumpsys battery", 20000, delegate(ProcResult r)
            {
                BatteryInfo(r.StdOut);
            }, delegate { return false; }, false);
            BatteryHealthInfo();
        }

        /// <summary>
        /// 读取 HyperOS / 小米电池健康度：由 sysfs 的设计容量与当前满充容量折算百分比，并附带循环次数。
        /// 路径优先 /sys/class/power_supply/battery，取不到时回退 bms。
        /// </summary>
        private void BatteryHealthInfo()
        {
            string cmd =
                "echo D=$(cat /sys/class/power_supply/battery/charge_full_design 2>/dev/null || cat /sys/class/power_supply/bms/charge_full_design 2>/dev/null); " +
                "echo F=$(cat /sys/class/power_supply/battery/charge_full 2>/dev/null || cat /sys/class/power_supply/bms/charge_full 2>/dev/null); " +
                "echo C=$(cat /sys/class/power_supply/battery/cycle_count 2>/dev/null || cat /sys/class/power_supply/bms/cycle_count 2>/dev/null)";

            RunShellLogged("电池健康度", cmd, 20000, delegate(ProcResult r)
            {
                string text = r.StdOut ?? "";
                long design, full, cycles;

                if (long.TryParse((FindLineValue(text, "D=") ?? "").Trim(), out design) && design > 0 &&
                    long.TryParse((FindLineValue(text, "F=") ?? "").Trim(), out full) && full > 0)
                {
                    double health = full * 100.0 / design;
                    if (health > 100) health = 100;
                    Info("  电池健康度：" + health.ToString("0.#", CultureInfo.InvariantCulture) + "%");
                    Info("  设计容量：" + (design / 1000) + " mAh ／ 当前满充容量：" + (full / 1000) + " mAh");
                }
                else
                {
                    Dim("  电池健康度：无法读取（该机型可能未开放 sysfs 容量节点）");
                }

                if (long.TryParse((FindLineValue(text, "C=") ?? "").Trim(), out cycles) && cycles > 0)
                    Info("  循环次数：" + cycles + " 次");
            }, delegate { return false; }, false);
        }

        private void ScreenInfo()
        {
            RunShellLogged("屏幕分辨率", "wm size", 20000, delegate(ProcResult r)
            {
                string v = FindLineValue(r.StdOut, "Override size:");
                if (v == null) v = FindLineValue(r.StdOut, "Physical size:");
                Info("  分辨率：" + (v ?? "未知"));
            }, delegate { return false; }, false);

            RunShellLogged("屏幕密度", "wm density", 20000, delegate(ProcResult r)
            {
                string v = FindLineValue(r.StdOut, "Override density:");
                if (v == null) v = FindLineValue(r.StdOut, "Physical density:");
                Info("  屏幕密度：" + (v == null ? "未知" : v + " dpi"));
            }, delegate { return false; }, false);
        }

        /// <summary>读取刷新率设置与屏幕实际档位（峰值设置 + dumpsys display 解析）</summary>
        private void RefreshRateInfo()
        {
            // 系统设置的峰值 / 最低刷新率（MIUI 等会在某些场景限帧）
            RunShellLogged("峰值刷新率设置", "settings get system peak_refresh_rate", 15000, delegate(ProcResult r)
            {
                string v = (r.StdOut ?? "").Trim();
                if (v.Length == 0 || v == "null") Dim("  峰值刷新率设置：未设置（默认）");
                else Info("  峰值刷新率设置：" + FormatHz(v) + " Hz");
            }, delegate { return false; }, false);

            RunShellLogged("最低刷新率设置", "settings get system min_refresh_rate", 15000, delegate(ProcResult r)
            {
                string v = (r.StdOut ?? "").Trim();
                if (v.Length == 0 || v == "null") Dim("  最低刷新率设置：未设置（默认）");
                else Info("  最低刷新率设置：" + FormatHz(v) + " Hz");
            }, delegate { return false; }, false);

            // 实际帧率与可选档位：解析 dumpsys display（不打印原始大段输出，只解析后展示）
            RunShellLogged("刷新率详情", "dumpsys display", 20000, delegate(ProcResult r)
            {
                ParseRefresh(r.StdOut);
            }, delegate { return false; }, false);
        }

        private static string FormatHz(string raw)
        {
            double d;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return Math.Round(d).ToString("0", CultureInfo.InvariantCulture);
            return raw;
        }

        /// <summary>从 dumpsys display 输出中解析当前帧率与支持档位</summary>
        private void ParseRefresh(string dump)
        {
            if (string.IsNullOrEmpty(dump)) { Dim("  无法读取刷新率详情。"); return; }

            double current = 0;
            Match m = Regex.Match(dump, @"renderFrameRate\s+([0-9]+(?:\.[0-9]+)?)");
            if (m.Success) double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out current);

            List<double> rates = new List<double>();
            int idx = dump.IndexOf("supportedModes", StringComparison.Ordinal);
            if (idx >= 0)
            {
                int start = dump.IndexOf('[', idx);
                int end = (start >= 0) ? dump.IndexOf(']', start) : -1;
                if (start >= 0 && end > start)
                {
                    string block = dump.Substring(start, end - start + 1);
                    foreach (Match fm in Regex.Matches(block, @"fps=([0-9]+(?:\.[0-9]+)?)"))
                    {
                        double f;
                        if (double.TryParse(fm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                        {
                            double r = Math.Round(f);
                            if (!rates.Contains(r)) rates.Add(r);
                        }
                    }
                }
            }

            if (current > 0)
                Info("  当前渲染帧率：" + Math.Round(current).ToString("0", CultureInfo.InvariantCulture) + " Hz");
            else
                Dim("  当前渲染帧率：未知");

            if (rates.Count > 0)
            {
                rates.Sort();
                Info("  支持刷新率档位：" + string.Join(" / ", rates.ConvertAll(x => x.ToString("0", CultureInfo.InvariantCulture))) + " Hz");
            }
            else
                Dim("  支持刷新率档位：未知");
        }

        #endregion

        #region 软重启（重启 Android 框架）

        /// <summary>软重启：仅重启 Android 框架（stop + start），不断电、不重启内核</summary>
        private void SoftReboot()
        {
            if (!RequireDevice()) return;
            if (MessageBox.Show(this,
                    "软重启会重启 Android 框架（所有应用都会关闭并重新加载，但手机不断电、不重启内核，比普通重启快）。\n确定继续吗？",
                    "软重启（重启框架）", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return;

            Info("开始软重启（重启 Android 框架）…");
            Thread worker = new Thread(delegate()
            {
                Proc.Run(_adbPath, PrefixSerial("shell stop"), 20000, null, null, null, null);
                SafeInvoke(delegate { Dim("  已停止框架，等待重启…"); });
                Thread.Sleep(4000);
                Proc.Run(_adbPath, PrefixSerial("shell start"), 20000, null, null, null, null);
                SafeInvoke(delegate
                {
                    Ok("已发送启动框架指令，设备即将恢复，请稍候刷新设备。");
                    RefreshDevices(true, null);
                });
            });
            worker.IsBackground = true;
            worker.Start();
        }

        #endregion

        #region 实时 FPS 监控

        private bool _fpsRunning;
        private Thread _fpsThread;
        private long _fpsPrevFrames = -1;
        private long _fpsPrevTick = 0;
        private string _fpsPrevPkg = "";

        /// <summary>开关实时 FPS 监控（在状态栏持续显示前台应用的渲染帧率）</summary>
        private void ToggleFps()
        {
            if (_fpsRunning)
            {
                _fpsRunning = false;
                if (_fpsToggle != null) { _fpsToggle.Text = "实时 FPS"; _fpsToggle.Active = false; }
                SafeInvoke(delegate { _fpsLabel.Text = "实时 FPS：已停止"; _fpsLabel.ForeColor = Theme.TextDim; });
                return;
            }
            if (!RequireDevice()) return;

            _fpsRunning = true;
            _fpsPrevFrames = -1;
            _fpsPrevPkg = "";
            if (_fpsToggle != null) { _fpsToggle.Text = "停止 FPS"; _fpsToggle.Active = true; }
            _fpsThread = new Thread(FpsMonitorLoop);
            _fpsThread.IsBackground = true;
            _fpsThread.Name = "fps-monitor";
            _fpsThread.Start();
            Dim("实时 FPS 监控已开启（状态栏查看）。让手机前台运行会动态刷新的应用（如视频 / 游戏 / 滚动列表）即可看到帧率。");
        }

        private void FpsMonitorLoop()
        {
            while (_fpsRunning)
            {
                if (_adbPath == null || string.IsNullOrEmpty(_serial))
                {
                    SafeInvoke(delegate { _fpsLabel.Text = "实时 FPS：未连接设备"; _fpsLabel.ForeColor = Theme.TextDim; });
                    for (int i = 0; i < 10 && _fpsRunning; i++) Thread.Sleep(100);
                    continue;
                }

                // 取前台应用包名：优先 activity activities（最可靠），失败再退回 window
                string pkg = "";
                ProcResult actR = Proc.Run(_adbPath,
                    PrefixSerial("shell dumpsys activity activities 2>/dev/null | grep ResumedActivity"),
                    10000, null, null, null, delegate { return !_fpsRunning; });
                pkg = ParseFocusedPackage(actR.StdOut);
                if (pkg.Length == 0)
                {
                    ProcResult winR = Proc.Run(_adbPath,
                        PrefixSerial("shell dumpsys window 2>/dev/null | grep mCurrentFocus"),
                        10000, null, null, null, delegate { return !_fpsRunning; });
                    pkg = ParseFocusedPackage(winR.StdOut);
                }
                if (pkg.Length == 0)
                {
                    SafeInvoke(delegate { _fpsLabel.Text = "实时 FPS：无法获取前台应用"; _fpsLabel.ForeColor = Theme.Amber; });
                    for (int i = 0; i < 10 && _fpsRunning; i++) Thread.Sleep(100);
                    continue;
                }

                // 切换应用后重置基准，避免跨应用帧数差分
                if (pkg != _fpsPrevPkg) { _fpsPrevFrames = -1; _fpsPrevPkg = pkg; }

                // 取该应用累计渲染帧数（取全量输出在本地解析，避免依赖设备端 grep 对引号的怪异处理）
                ProcResult fr = Proc.Run(_adbPath,
                    PrefixSerial("shell dumpsys gfxinfo " + pkg + " 2>/dev/null"),
                    10000, null, null, null, delegate { return !_fpsRunning; });
                long frames = ParseTotalFrames(fr.StdOut);

                long now = DateTime.UtcNow.Ticks;
                double fps = 0;
                if (_fpsPrevFrames >= 0 && frames >= _fpsPrevFrames)
                {
                    double sec = (now - _fpsPrevTick) / 1e7;
                    if (sec > 0.2) fps = (frames - _fpsPrevFrames) / sec;
                }
                _fpsPrevFrames = frames;
                _fpsPrevTick = now;

                long shown = (long)Math.Round(fps);
                string shortPkg = pkg.Length > 20 ? pkg.Substring(0, 19) + "…" : pkg;
                SafeInvoke(delegate
                {
                    if (frames < 0)
                    {
                        _fpsLabel.Text = "实时 FPS：--（" + shortPkg + "）";
                        _fpsLabel.ForeColor = Theme.TextDim;
                    }
                    else if (shown > 0)
                    {
                        _fpsLabel.Text = "实时 FPS：" + shown + "  （" + shortPkg + "）";
                        _fpsLabel.ForeColor = Theme.Green;
                    }
                    else
                    {
                        _fpsLabel.Text = "实时 FPS：0  （" + shortPkg + " 静止）";
                        _fpsLabel.ForeColor = Theme.Amber;
                    }
                });

                // 约 1 秒采样一次
                for (int i = 0; i < 10 && _fpsRunning; i++) Thread.Sleep(100);
            }
        }

        // 兼容 mCurrentFocus / mFocusedApp / ResumedActivity / topResumedActivity 等多种格式
        private static readonly Regex _reFocusPkg = new Regex(@"u0\s+([A-Za-z0-9._]+)/", RegexOptions.Compiled);

        private static string ParseFocusedPackage(string output)
        {
            if (string.IsNullOrEmpty(output)) return "";
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                Match m = _reFocusPkg.Match(line);
                if (m.Success) return m.Groups[1].Value;
            }
            return "";
        }

        private static long ParseTotalFrames(string output)
        {
            if (string.IsNullOrEmpty(output)) return -1;
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.Trim();
                if (line.IndexOf("Total frames rendered") < 0) continue;
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string num = line.Substring(colon + 1).Trim().Replace(",", "");
                long v;
                if (long.TryParse(num, out v)) return v;
            }
            return -1;
        }

        #endregion

        #region 实时日志

        private void ToggleLogcat()
        {
            if (_logcatRunning)
            {
                CancelCurrent();
                return;
            }
            if (!RequireDevice()) return;

            _logcatRunning = true;
            _logToggle.Text = "停止日志";
            _logToggle.Active = true;

            string args = PrefixSerial("logcat -v time");
            Cmd(args);
            Dim("  正在实时输出日志：点「停止日志」结束；右上「日志过滤」框可只保留包含关键字的行。");

            Enqueue(new Job
            {
                Title = "实时日志",
                Args = args,
                TimeoutMs = 0,
                Filter = LogcatFilter,
                Done = delegate(ProcResult r)
                {
                    _logcatRunning = false;
                    _logToggle.Text = "实时日志";
                    _logToggle.Active = false;
                    UpdateQueueStatus();
                }
            });
        }

        private bool LogcatFilter(string line)
        {
            string key = _filterText;
            if (key.Length == 0) return true;
            return line.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion
    }
}
