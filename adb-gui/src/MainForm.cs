using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AdbGui
{
    internal sealed partial class MainForm : Form
    {
        private const int HeaderHeight = 58;
        private const int StatusHeight = 30;

        private string _adbPath;
        private string _serial;
        private List<AdbDevice> _devices = new List<AdbDevice>();

        private TableLayoutPanel _root;
        private RoundedPanel _frame;
        private Panel _header;
        private SysButton _btnMin;
        private SysButton _btnMax;
        private SysButton _btnClose;
        private bool _maximized;
        private Rectangle _savedBounds;
        private Panel _sidebarHost;
        private FlowLayoutPanel _sidebar;
        private List<GhostButton> _sideButtons = new List<GhostButton>();
        private RoundedPanel _console;
        private Panel _consoleBar;
        private Panel _statusBar;

        private Label _title;
        private ComboBox _deviceCombo;
        private GhostButton _refreshBtn;
        private CheckBox _autoCheck;
        private StatusDot _statusDot;
        private Label _statusLabel;
        private Label _adbLabel;
        private GhostButton _browseBtn;

        private RichTextBox _log;
        private CueTextBox _cmdBox;
        private CueTextBox _filterBox;
        private GhostButton _runBtn;
        private GhostButton _stopBtn;
        private GhostButton _logToggle;
        private Label _jobLabel;
        private Label _statusLeft;
        private Label _statusRight;
        private GhostButton _fpsToggle;
        private Label _fpsLabel;
        private CheckBox _autoScroll;
        private KeyForm _keyForm;

        private System.Windows.Forms.Timer _deviceTimer;
        private bool _layoutReady;

        public MainForm()
        {
            Ui.Dark(this, "ADB 工具箱", 920, 760, true);
            // 无边框整体化：去掉系统标题栏与边框，改用自定义标题栏与控制按钮
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Padding = new Padding(1);
            BackColor = Theme.Border;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 560);
            ShowInTaskbar = true;
            Icon = IconFactory.Create(32);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);

            BuildLayout();
            InitQueue();
            _layoutReady = true;
            LayoutChrome();
            UpdateRgn();

            _deviceTimer = new System.Windows.Forms.Timer();
            _deviceTimer.Interval = 4000;
            _deviceTimer.Tick += delegate { if (_autoCheck.Checked) RefreshDevices(false, "自动"); };
        }

        #region 界面构建

        private void BuildLayout()
        {
            _root = new TableLayoutPanel();
            _root.Dock = DockStyle.Fill;
            _root.BackColor = Theme.Bg;
            _root.ColumnCount = 1;
            _root.RowCount = 3;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
            _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _root.RowStyles.Add(new RowStyle(SizeType.Absolute, StatusHeight));
            _root.Padding = new Padding(0);

            // 圆角外壳：1px 边 + 内部圆角面板，让整个软件像一张整体卡片
            _frame = new RoundedPanel();
            _frame.Dock = DockStyle.Fill;
            _frame.BackColor = Theme.Bg;
            _frame.BorderColor = Theme.Border;
            _frame.Radius = 13;
            _frame.Padding = new Padding(0);
            _frame.Controls.Add(_root);
            Controls.Add(_frame);

            // 主体：左侧竖向工具栏 + 右侧输出区
            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(0);
            body.BackColor = Theme.Bg;
            body.ColumnCount = 2;
            body.RowCount = 1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _sidebarHost = new Panel();
            _sidebarHost.Dock = DockStyle.Fill;
            _sidebarHost.Margin = new Padding(0);
            _sidebarHost.BackColor = Theme.Bg;

            _sidebar = new FlowLayoutPanel();
            _sidebar.Dock = DockStyle.Fill;
            _sidebar.Margin = new Padding(0);
            _sidebar.FlowDirection = FlowDirection.TopDown;
            _sidebar.WrapContents = false;
            _sidebar.AutoScroll = true;
            _sidebar.BackColor = Theme.Bg;
            _sidebar.Padding = new Padding(12, 12, 12, 12);
            _sidebarHost.Controls.Add(_sidebar);

            BuildHeader();
            BuildSidebar();
            BuildConsole();
            BuildStatusBar();

            body.Controls.Add(_sidebarHost, 0, 0);
            body.Controls.Add(_console, 1, 0);

            _root.Controls.Add(_header, 0, 0);
            _root.Controls.Add(body, 0, 1);
            _root.Controls.Add(_statusBar, 0, 2);
        }

        private void BuildHeader()
        {
            _header = new Panel();
            _header.Dock = DockStyle.Fill;
            _header.Margin = new Padding(0);
            _header.BackColor = Theme.Bg;

            _title = Ui.Label("ADB 工具箱", Theme.Text, Theme.TitleFont, new Point(16, 8));
            _header.Controls.Add(_title);

            _statusDot = new StatusDot();
            _statusDot.Location = new Point(18, 36);
            _header.Controls.Add(_statusDot);

            _statusLabel = Ui.Label("正在查找 adb…", Theme.TextDim, Theme.UiFontBold, new Point(40, 34));
            _header.Controls.Add(_statusLabel);

            _adbLabel = Ui.Label("", Theme.TextDim, new Font("Segoe UI", 8.25f), new Point(0, 8));
            _adbLabel.AutoSize = false;
            _adbLabel.Size = new Size(320, 18);
            _adbLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            _adbLabel.AutoEllipsis = true;
            _adbLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _header.Controls.Add(_adbLabel);

            _browseBtn = Tool("选择 adb", delegate { BrowseAdb(); });
            _browseBtn.Width = 92;
            _browseBtn.Location = new Point(0, 32);
            _browseBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _header.Controls.Add(_browseBtn);

            // 右上角窗口控制按钮（无边框后由我们自定义）
            _btnMin = new SysButton();
            _btnMin.Symbol = SysButton.Kind.Minimize;
            _btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };

            _btnMax = new SysButton();
            _btnMax.Symbol = SysButton.Kind.Maximize;
            _btnMax.Click += delegate { ToggleMaximize(); };

            _btnClose = new SysButton();
            _btnClose.Symbol = SysButton.Kind.Close;
            _btnClose.Click += delegate { Close(); };

            foreach (SysButton b in new SysButton[] { _btnMin, _btnMax, _btnClose })
            {
                b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _header.Controls.Add(b);
            }

            // 标题栏拖动窗口；双击在最大化 / 还原间切换
            _header.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (_maximized) RestoreWindow();
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
            _header.MouseDoubleClick += delegate(object s, MouseEventArgs e) { ToggleMaximize(); };
        }

        private void BuildSidebar()
        {
            _deviceCombo = new ComboBox();
            _deviceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _deviceCombo.FlatStyle = FlatStyle.Flat;
            _deviceCombo.BackColor = Theme.Surface;
            _deviceCombo.ForeColor = Theme.Text;
            _deviceCombo.Font = Theme.UiFont;
            _deviceCombo.DrawMode = DrawMode.OwnerDrawFixed;
            _deviceCombo.ItemHeight = 22;
            _deviceCombo.Margin = new Padding(0, 0, 0, 8);
            _deviceCombo.DrawItem += DeviceComboDrawItem;
            _deviceCombo.SelectedIndexChanged += DeviceComboChanged;
            _sidebar.Controls.Add(_deviceCombo);

            _refreshBtn = SidebarButton("刷新设备", delegate { RefreshDevices(true, null); });
            _autoCheck = new CheckBox();
            _autoCheck.Text = "自动刷新设备列表";
            _autoCheck.ForeColor = Theme.TextDim;
            _autoCheck.Font = Theme.UiFont;
            _autoCheck.AutoSize = true;
            _autoCheck.Margin = new Padding(0, 0, 0, 4);
            _autoCheck.Checked = Settings.GetBool("autoRefresh", true);
            _autoCheck.CheckedChanged += delegate
            {
                Settings.SetBool("autoRefresh", _autoCheck.Checked);
                Settings.Save();
                if (_autoCheck.Checked) _deviceTimer.Start(); else _deviceTimer.Stop();
            };
            _sidebar.Controls.Add(_autoCheck);

            ContextMenuStrip rebootMenu = NewMenu();
            rebootMenu.Items.Add("软重启（重启框架）", null, delegate { SoftReboot(); });
            rebootMenu.Items.Add("重启系统", null, delegate { RunAdbLogged("reboot", "reboot", 20000); });
            rebootMenu.Items.Add("重启到 Bootloader", null, delegate { RunAdbLogged("reboot bootloader", "reboot bootloader", 20000); });
            rebootMenu.Items.Add("重启到 Recovery", null, delegate { RunAdbLogged("reboot recovery", "reboot recovery", 20000); });
            rebootMenu.Items.Add("关机", null, delegate { RunAdbLogged("reboot -p", "reboot -p", 20000); });
            _sidebar.Controls.Add(SidebarMenu("重启 ▾", rebootMenu));

            ContextMenuStrip wifiMenu = NewMenu();
            wifiMenu.Items.Add("开启无线调试 (tcpip 5555)", null, delegate { RunAdbLogged("tcpip 5555", "tcpip 5555", 20000, delegate(ProcResult r)
            {
                if (r.Ok) Info("无线调试已开启，拔掉数据线后点「连接设备…」输入手机 IP 即可。");
            }); });
            wifiMenu.Items.Add("连接设备…", null, delegate { ConnectWifi(); });
            wifiMenu.Items.Add("断开无线设备", null, delegate { RunAdbLogged("disconnect", "disconnect", 20000); });
            wifiMenu.Items.Add("查看本机 IP", null, delegate { ShowWifiIp(); });
            _sidebar.Controls.Add(SidebarMenu("无线连接 ▾", wifiMenu));

            _sidebar.Controls.Add(SidebarButton("按键", delegate { OpenKeyPad(); }));

            _sidebar.Controls.Add(SidebarButton("输入文字", delegate { InputText(); }));

            _sidebar.Controls.Add(SidebarButton("安装应用", delegate { InstallApk(false); }));
            _sidebar.Controls.Add(SidebarButton("软件管理", delegate { OpenAppManager(); }));

            _sidebar.Controls.Add(SidebarButton("设备信息", delegate { DeviceInfo(); }));
            _sidebar.Controls.Add(SidebarButton("激活 Shizuku", delegate { ActivateShizuku(); }));
            _sidebar.Controls.Add(SidebarButton("截屏", delegate { Screenshot(); }));
            _sidebar.Controls.Add(SidebarButton("电池状态", delegate { BatteryStatus(); }));
            _sidebar.Controls.Add(SidebarButton("CPU / 内存", delegate { RunShellLogged("CPU / 内存", "dumpsys meminfo"); }));
            _logToggle = SidebarButton("实时日志", delegate { ToggleLogcat(); });
            _fpsToggle = SidebarButton("实时 FPS", delegate { ToggleFps(); });
            _sidebar.Controls.Add(_fpsToggle);
            _sidebar.Controls.Add(SidebarButton("清空日志", delegate { RunShellLogged("清空设备日志", "logcat -c"); }));

            ContextMenuStrip moreMenu = NewMenu();
            moreMenu.Items.Add("安装 APK（自动授权 -g）", null, delegate { InstallApk(true); });
            moreMenu.Items.Add("获取 root 权限", null, delegate { RunAdbLogged("root", "root", 20000); });
            moreMenu.Items.Add("重新挂载 /system 可写", null, delegate { RunAdbLogged("remount", "remount", 20000); });
            moreMenu.Items.Add("重启 ADB 服务", null, delegate { RestartAdbServer(); });
            moreMenu.Items.Add("当前前台界面", null, delegate
            {
                RunShellLogged("当前前台界面", "dumpsys activity activities", 30000, null,
                    delegate(string line) { return line.IndexOf("ResumedActivity", StringComparison.OrdinalIgnoreCase) >= 0; });
            });
            moreMenu.Items.Add("打开 adb.exe 所在目录", null, delegate { OpenAdbFolder(); });
            _sidebar.Controls.Add(SidebarMenu("更多 ▾", moreMenu));
        }

        private GhostButton SidebarButton(string text, EventHandler onClick)
        {
            GhostButton b = new GhostButton();
            b.Text = text;
            b.Margin = new Padding(0, 0, 0, 8);
            b.Click += onClick;
            _sideButtons.Add(b);
            return b;
        }

        private GhostButton SidebarMenu(string text, ContextMenuStrip menu)
        {
            GhostButton b = SidebarButton(text, null);
            b.Click += delegate { menu.Show(b, new Point(0, b.Height + 2)); };
            return b;
        }

        private void BuildConsole()
        {
            _console = new RoundedPanel();
            _console.Dock = DockStyle.Fill;
            _console.Margin = new Padding(12, 0, 12, 8);
            _console.Padding = new Padding(1);
            _console.BackColor = Theme.Surface;
            _console.BorderColor = Theme.Border;

            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 1;
            grid.RowCount = 3;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            grid.BackColor = Theme.Surface;
            _console.Controls.Add(grid);

            // 输出区标题栏
            _consoleBar = new Panel();
            _consoleBar.Dock = DockStyle.Fill;
            _consoleBar.Margin = new Padding(0);
            _consoleBar.BackColor = Theme.Surface;
            grid.Controls.Add(_consoleBar, 0, 0);

            Label lblOut = Ui.Label("输出", Theme.Text, Theme.UiFontBold, new Point(14, 10));
            _consoleBar.Controls.Add(lblOut);

            _jobLabel = Ui.Label("就绪", Theme.TextDim, Theme.UiFont, new Point(58, 11));
            _consoleBar.Controls.Add(_jobLabel);

            _autoScroll = new CheckBox();
            _autoScroll.Text = "自动滚动";
            _autoScroll.ForeColor = Theme.TextDim;
            _autoScroll.Font = Theme.UiFont;
            _autoScroll.AutoSize = true;
            _autoScroll.Checked = true;
            _autoScroll.Location = new Point(430, 11);
            _consoleBar.Controls.Add(_autoScroll);

            _filterBox = new CueTextBox();
            _filterBox.Cue = "日志过滤关键字（实时日志用）";
            _filterBox.Location = new Point(520, 8);
            _filterBox.Size = new Size(200, 24);
            _consoleBar.Controls.Add(_filterBox);

            _stopBtn = Tool("停止", delegate { CancelCurrent(); });
            _stopBtn.Width = 68;
            _stopBtn.Location = new Point(730, 5);
            _stopBtn.Enabled = false;
            _consoleBar.Controls.Add(_stopBtn);

            GhostButton clear = Tool("清空", delegate { _log.Clear(); });
            clear.Width = 68;
            clear.Location = new Point(804, 5);
            _consoleBar.Controls.Add(clear);

            GhostButton save = Tool("保存日志", delegate { SaveLog(); });
            save.Width = 84;
            save.Location = new Point(878, 5);
            _consoleBar.Controls.Add(save);

            // 输出窗口
            _log = new RichTextBox();
            _log.Dock = DockStyle.Fill;
            _log.Margin = new Padding(14, 2, 14, 2);
            _log.BackColor = Theme.SurfaceAlt;
            _log.ForeColor = Theme.Text;
            _log.BorderStyle = BorderStyle.None;
            _log.Font = Theme.MonoFont;
            _log.ReadOnly = true;
            _log.WordWrap = false;
            _log.DetectUrls = false;
            _log.ScrollBars = RichTextBoxScrollBars.Both;
            _log.ShortcutsEnabled = true;
            grid.Controls.Add(_log, 0, 1);

            // 命令输入行
            Panel inputRow = new Panel();
            inputRow.Dock = DockStyle.Fill;
            inputRow.Margin = new Padding(0);
            inputRow.BackColor = Theme.Surface;
            grid.Controls.Add(inputRow, 0, 2);

            _cmdBox = new CueTextBox();
            _cmdBox.Cue = "输入 adb 命令后回车执行，例如 shell pm list packages -3";
            _cmdBox.Location = new Point(14, 8);
            _cmdBox.Size = new Size(960, 26);
            _cmdBox.Font = Theme.MonoFont;
            _cmdBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunCustomCommand(); }
            };
            inputRow.Controls.Add(_cmdBox);

            _runBtn = Tool("执行", delegate { RunCustomCommand(); });
            _runBtn.Width = 88;
            _runBtn.Location = new Point(990, 7);
            inputRow.Controls.Add(_runBtn);
        }

        private void BuildStatusBar()
        {
            _statusBar = new Panel();
            _statusBar.Dock = DockStyle.Fill;
            _statusBar.Margin = new Padding(0);
            _statusBar.BackColor = Theme.Bg;

            _statusLeft = Ui.Label("就绪", Theme.TextDim, Theme.UiFont, new Point(14, 7));
            _statusBar.Controls.Add(_statusLeft);

            _statusRight = Ui.Label("", Theme.TextDim, new Font("Segoe UI", 8.25f), new Point(700, 8));
            _statusRight.AutoSize = false;
            _statusRight.Size = new Size(300, 16);
            _statusRight.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            _statusBar.Controls.Add(_statusRight);

            _fpsLabel = Ui.Label("实时 FPS：未启动", Theme.TextDim, Theme.UiFont, new Point(300, 7));
            _fpsLabel.AutoSize = false;
            _fpsLabel.Size = new Size(380, 16);
            _fpsLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            _fpsLabel.AutoEllipsis = true;
            _statusBar.Controls.Add(_fpsLabel);
        }

        private GhostButton Tool(string text, EventHandler onClick)
        {
            GhostButton b = new GhostButton();
            b.Text = text;
            b.Width = TextRenderer.MeasureText(text, b.Font).Width + 24;
            b.Click += onClick;
            b.Margin = new Padding(0, 2, 8, 2);
            return b;
        }

        private GhostButton ToolMenu(string text, ContextMenuStrip menu)
        {
            GhostButton b = Tool(text, null);
            b.Click += delegate { menu.Show(b, new Point(0, b.Height + 2)); };
            return b;
        }

        private static ContextMenuStrip NewMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenuRenderer();
            menu.BackColor = Theme.Surface;
            menu.ForeColor = Theme.Text;
            menu.Font = Theme.UiFont;
            menu.ShowImageMargin = false;
            menu.Padding = new Padding(4);
            return menu;
        }

        /// <summary>打开独立的设备按键面板（仅保留一个实例）</summary>
        private void OpenKeyPad()
        {
            if (_keyForm != null && !_keyForm.IsDisposed) { _keyForm.Focus(); return; }
            _keyForm = new KeyForm();
            _keyForm.KeyPressed += delegate(string label, string code)
            {
                RunShellLogged("按键 " + label, "input keyevent " + code, 15000);
            };
            _keyForm.FormClosed += delegate { _keyForm = null; };
            _keyForm.Show(this);
        }

        #endregion

        #region 布局微调

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutChrome();
            UpdateRgn();
            Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_layoutReady) { LayoutChrome(); UpdateRgn(); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _fpsRunning = false;
            base.OnFormClosing(e);
        }

        /// <summary>把右侧按钮/标签摆到该有的位置（避免 Docking 顺序坑）</summary>
        private void LayoutChrome()
        {
            int headerWidth = _header.ClientSize.Width;
            if (headerWidth > 200)
            {
                // 右上角窗口控制按钮（最右为关闭）
                int right = headerWidth - 16;
                _btnClose.Location = new Point(right - 36, 6);
                _btnMax.Location = new Point(right - 72, 6);
                _btnMin.Location = new Point(right - 108, 6);

                // 选择 adb 与 adb 路径显示在按钮左侧
                int leftOfCluster = right - 108 - 12;
                _browseBtn.Location = new Point(leftOfCluster - _browseBtn.Width, 14);
                _browseBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _adbLabel.Location = new Point(_browseBtn.Left - 12 - _adbLabel.Width, 8);
                _adbLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            }

            int sideWidth = _sidebar.ClientSize.Width - _sidebar.Padding.Left - _sidebar.Padding.Right;
            if (sideWidth > 80)
            {
                _deviceCombo.Width = sideWidth;
                foreach (GhostButton b in _sideButtons) b.Width = sideWidth;
            }

            int barWidth = _consoleBar.ClientSize.Width;
            if (barWidth > 300)
            {
                int x = barWidth - 16;
                GhostButton[] right = new GhostButton[] { _stopBtn, FindTool(_consoleBar, "清空"), FindTool(_consoleBar, "保存日志") };
                foreach (GhostButton b in right)
                {
                    if (b == null) continue;
                    b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                    x -= b.Width;
                    b.Location = new Point(x, 5);
                    x -= 8;
                }
                _filterBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _filterBox.Location = new Point(x - _filterBox.Width, 8);
                _autoScroll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _autoScroll.Location = new Point(_filterBox.Left - _autoScroll.Width - 12, 11);
            }

            int inputWidth = _cmdBox.Parent != null ? _cmdBox.Parent.ClientSize.Width : 0;
            if (inputWidth > 300)
            {
                _runBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _runBtn.Location = new Point(inputWidth - _runBtn.Width - 14, 7);
                _cmdBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                _cmdBox.Width = Math.Max(200, _runBtn.Left - _cmdBox.Left - 10);
            }

            int statusWidth = _statusBar.ClientSize.Width;
            if (statusWidth > 300)
            {
                _statusRight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _statusRight.Location = new Point(statusWidth - _statusRight.Width - 14, 8);
            }
        }

        private static GhostButton FindTool(Control parent, string text)
        {
            foreach (Control c in parent.Controls)
            {
                GhostButton b = c as GhostButton;
                if (b != null && b.Text == text) return b;
            }
            return null;
        }

        #endregion

        #region 无边框窗口

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                          HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETMINMAXINFO)
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                mmi.ptMinTrackSize = new POINT { X = MinimumSize.Width, Y = MinimumSize.Height };
                Marshal.StructureToPtr(mmi, m.LParam, true);
            }
            else if (m.Msg == WM_NCHITTEST && !_maximized)
            {
                // 边缘命中测试，让无边框窗口仍可拖拽缩放
                Point pt = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
                int grip = 6;
                int w = ClientSize.Width, h = ClientSize.Height;
                if (pt.X <= grip)
                {
                    if (pt.Y <= grip) { m.Result = (IntPtr)HTTOPLEFT; return; }
                    if (pt.Y >= h - grip) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                    m.Result = (IntPtr)HTLEFT; return;
                }
                if (pt.X >= w - grip)
                {
                    if (pt.Y <= grip) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                    if (pt.Y >= h - grip) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                    m.Result = (IntPtr)HTRIGHT; return;
                }
                if (pt.Y <= grip) { m.Result = (IntPtr)HTTOP; return; }
                if (pt.Y >= h - grip) { m.Result = (IntPtr)HTBOTTOM; return; }
            }
            base.WndProc(ref m);
        }

        /// <summary>把窗口与外壳面板裁成圆角，营造整体卡片感</summary>
        private void UpdateRgn()
        {
            if (_frame == null) return;
            if (_maximized) { Region = null; _frame.Region = null; return; }
            int r = 12;
            using (GraphicsPath p = Theme.RoundedRect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), r))
                Region = new Region(p);
            using (GraphicsPath p2 = Theme.RoundedRect(new Rectangle(0, 0, _frame.Width, _frame.Height), r - 1))
                _frame.Region = new Region(p2);
        }

        private void ToggleMaximize() { if (_maximized) RestoreWindow(); else MaximizeWindow(); }

        private void MaximizeWindow()
        {
            _savedBounds = Bounds;
            _maximized = true;
            Padding = new Padding(0);
            if (_btnMax != null) _btnMax.Symbol = SysButton.Kind.Restore;
            Bounds = Screen.GetWorkingArea(this);
            UpdateRgn();
        }

        private void RestoreWindow()
        {
            _maximized = false;
            Padding = new Padding(1);
            if (_btnMax != null) _btnMax.Symbol = SysButton.Kind.Maximize;
            if (!_savedBounds.IsEmpty) Bounds = _savedBounds;
            UpdateRgn();
        }

        #endregion

        #region 设备列表

        private void DeviceComboDrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            using (SolidBrush back = new SolidBrush(Theme.Surface)) e.Graphics.FillRectangle(back, e.Bounds);
            if (e.Index >= 0 && e.Index < _deviceCombo.Items.Count)
            {
                AdbDevice device = _deviceCombo.Items[e.Index] as AdbDevice;
                if (device != null)
                {
                    bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                    if (selected)
                    {
                        using (SolidBrush hl = new SolidBrush(Theme.Mix(Theme.Surface, Theme.Accent, 0.35)))
                            e.Graphics.FillRectangle(hl, e.Bounds);
                    }
                    using (SolidBrush dot = new SolidBrush(device.Online ? Theme.Green : Theme.Amber))
                        e.Graphics.FillEllipse(dot, e.Bounds.X + 4, e.Bounds.Y + e.Bounds.Height / 2 - 4, 8, 8);
                    Rectangle text = new Rectangle(e.Bounds.X + 18, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height);
                    TextRenderer.DrawText(e.Graphics, device.Title, Theme.UiFont, text,
                        device.Online ? Theme.Text : Theme.TextDim,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private void DeviceComboChanged(object sender, EventArgs e)
        {
            AdbDevice device = _deviceCombo.SelectedItem as AdbDevice;
            _serial = device == null ? null : device.Serial;
            if (_serial != null)
            {
                Settings.Set("lastSerial", _serial);
                Settings.Save();
            }
            UpdateStatusText();
            UpdateStatusBar();
        }

        private void UpdateDevices(List<AdbDevice> devices)
        {
            bool same = devices.Count == _devices.Count;
            if (same)
            {
                for (int i = 0; i < devices.Count && same; i++)
                {
                    same = devices[i].Serial == _devices[i].Serial && devices[i].State == _devices[i].State &&
                           devices[i].Title == _devices[i].Title;
                }
            }
            if (same) { _devices = devices; UpdateStatusText(); return; }

            string keep = _serial;
            if (keep == null) keep = Settings.Get("lastSerial", null);

            _deviceCombo.BeginUpdate();
            _deviceCombo.Items.Clear();
            foreach (AdbDevice d in devices) _deviceCombo.Items.Add(d);
            _deviceCombo.EndUpdate();

            _devices = devices;
            int index = -1;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i].Serial == keep) { index = i; break; }
            }
            if (index < 0)
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    if (devices[i].Online) { index = i; break; }
                }
            }
            if (index < 0 && devices.Count > 0) index = 0;

            if (index >= 0) { _deviceCombo.SelectedIndex = index; }
            else { _serial = null; }

            UpdateStatusText();
            UpdateStatusBar();
        }

        private void UpdateStatusText()
        {
            if (_adbPath == null)
            {
                _statusDot.DotColor = Theme.Red;
                _statusLabel.ForeColor = Theme.Red;
                _statusLabel.Text = "未找到 adb.exe，请点「选择 adb」指定";
                return;
            }
            int online = 0;
            foreach (AdbDevice d in _devices) if (d.Online) online++;

            if (_devices.Count == 0)
            {
                _statusDot.DotColor = Theme.Amber;
                _statusLabel.ForeColor = Theme.Amber;
                _statusLabel.Text = "未检测到设备（请插好数据线并打开 USB 调试）";
            }
            else if (online == 0)
            {
                _statusDot.DotColor = Theme.Amber;
                _statusLabel.ForeColor = Theme.Amber;
                _statusLabel.Text = "检测到 " + _devices.Count + " 台设备，但没有可用的（可能未在手机上确认授权）";
            }
            else
            {
                _statusDot.DotColor = Theme.Green;
                _statusLabel.ForeColor = Theme.Green;
                _statusLabel.Text = "已连接 " + online + " 台设备" + (_devices.Count > online ? "（共 " + _devices.Count + " 台）" : "");
            }
        }

        private void UpdateStatusBar()
        {
            _statusRight.Text = string.Format("{0} 台设备 · 当前 {1}",
                _devices.Count, _serial == null ? "未选择" : _serial);
            if (_adbPath != null) _adbLabel.Text = "adb: " + _adbPath;
            else _adbLabel.Text = "adb: 未找到";
        }

        #endregion

        #region 设置与启动

        private void BrowseAdb()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "选择 adb.exe";
                dlg.Filter = "adb.exe|adb.exe|可执行文件 (*.exe)|*.exe";
                if (_adbPath != null)
                {
                    try { dlg.InitialDirectory = Path.GetDirectoryName(_adbPath); } catch { }
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                UseAdb(dlg.FileName, true);
            }
        }

        private void UseAdb(string path, bool log)
        {
            _adbPath = path;
            Settings.Set("adbPath", path);
            Settings.Save();
            UpdateStatusBar();
            if (log) Info("使用 adb: " + path);
            RunAdbLogged("adb 版本", "version", 15000);
            RefreshDevices(true, null);
        }

        private void Start()
        {
            string saved = Settings.Get("adbPath", null);
            if (saved != null && File.Exists(saved))
            {
                _adbPath = saved;
            }
            else
            {
                _adbPath = AdbLocator.Find();
                if (_adbPath != null) { Settings.Set("adbPath", _adbPath); Settings.Save(); }
            }

            Info("ADB 工具箱已启动");
            if (_adbPath == null)
            {
                Warn("没有找到 adb.exe。请点右上角「选择 adb」手动指定（通常在 platform-tools 目录里）。");
                UpdateStatusText();
                UpdateStatusBar();
                return;
            }

            UpdateStatusBar();
            Info("使用 adb: " + _adbPath);
            RunAdbLogged("启动 adb 服务", "start-server", 30000, delegate(ProcResult r)
            {
                RefreshDevices(true, null);
            });
            if (_autoCheck.Checked) _deviceTimer.Start();
        }

        #endregion
    }
}
