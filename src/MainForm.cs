using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VMMicControl
{
    internal static class User32
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    internal sealed class ChannelEntry
    {
        private readonly int _index;
        private readonly string _name;

        public ChannelEntry(int index, string name)
        {
            _index = index;
            _name = name;
        }

        public int Index
        {
            get { return _index; }
        }

        public override string ToString()
        {
            return _name;
        }
    }

    internal sealed class MainForm : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyIdBase = 1000;

        private readonly Voicemeeter _vm = new Voicemeeter();
        private readonly List<ChannelCard> _cards = new List<ChannelCard>();
        private readonly List<HotkeyBinding> _bindings = new List<HotkeyBinding>();
        private readonly Dictionary<string, string> _rawHotkeys = new Dictionary<string, string>();
        private readonly List<int> _registeredHotkeyIds = new List<int>();
        private FlowLayoutPanel _host;
        private Panel _header;
        private Panel _footer;
        private Label _statusText;
        private Panel _statusDot;
        private RoundedButton _btnLaunch;
        private ComboBox _mainChannel;
        private CheckBox _chkHotkey;
        private CheckBox _chkVirtual;
        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private Timer _timer;
        private Icon _iconOn;
        private Icon _iconOff;
        private Bitmap _bmpOn;
        private Bitmap _bmpOff;
        private int _mainIndex;
        private bool _hotkeyOn = true;
        private int _reconnectTicks;
        private int _dirtyStreak;
        private bool _mainIndexLoaded;
        private bool _realExit;
        private bool _trayHintShown;
        private bool _defaultsApplied;
        private string _connectionText;
        private string _hotkeyWarning;
        private ToolStripMenuItem _trayToggleItem;

        public MainForm()
        {
            BuildUi();
            BuildTray();
            LoadSettings();
            Connect();
            _timer = new Timer();
            _timer.Interval = 200;
            _timer.Tick += OnTick;
            _timer.Start();
        }

        // ---------- 界面搭建 ----------

        private void BuildUi()
        {
            Text = "Voicemeeter 麦克风控制";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(620, 680);
            MinimumSize = new Size(470, 430);
            BackColor = Theme.Bg;
            Font = new Font("Segoe UI", 9f);
            Icon = LoadAppIcon();

            // 顶栏
            _header = new Panel();
            _header.Dock = DockStyle.Top;
            _header.Height = 66;
            _header.BackColor = Theme.SurfaceAlt;
            _header.Padding = new Padding(0);

            Label title = new Label();
            title.Text = "Voicemeeter 麦克风控制";
            title.Location = new Point(18, 12);
            title.Size = new Size(300, 24);
            title.ForeColor = Theme.Text;
            title.Font = new Font("Segoe UI Semibold", 13f);

            _statusDot = new Panel();
            _statusDot.Size = new Size(10, 10);
            _statusDot.Location = new Point(20, 45);
            _statusDot.BackColor = Theme.Off;
            _statusDot.Region = new Region(Theme.RoundedRect(new Rectangle(0, 0, 10, 10), 5));

            _statusText = new Label();
            _statusText.Text = "正在连接…";
            _statusText.Location = new Point(36, 41);
            _statusText.Size = new Size(320, 18);
            _statusText.ForeColor = Theme.TextDim;
            _statusText.Font = new Font("Segoe UI", 9f);

            _btnLaunch = new RoundedButton();
            _btnLaunch.Text = "启动 Voicemeeter";
            _btnLaunch.Size = new Size(150, 32);
            _btnLaunch.BaseColor = Theme.Accent;
            _btnLaunch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnLaunch.Visible = false;
            _btnLaunch.Click += OnLaunchClick;

            _header.Controls.Add(title);
            _header.Controls.Add(_statusDot);
            _header.Controls.Add(_statusText);
            _header.Controls.Add(_btnLaunch);
            _header.Resize += delegate { PlaceLaunchButton(); };
            PlaceLaunchButton();

            // 卡片区
            _host = new FlowLayoutPanel();
            _host.Dock = DockStyle.Fill;
            _host.FlowDirection = FlowDirection.TopDown;
            _host.WrapContents = false;
            _host.AutoScroll = true;
            _host.BackColor = Theme.Bg;
            _host.Padding = new Padding(14, 14, 14, 6);

            // 底栏
            _footer = new Panel();
            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 88;
            _footer.BackColor = Theme.SurfaceAlt;

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = true;
            bar.Padding = new Padding(14, 12, 14, 8);
            bar.BackColor = Theme.SurfaceAlt;

            RoundedButton btnMuteAll = new RoundedButton();
            btnMuteAll.Text = "全部静音";
            btnMuteAll.Size = new Size(96, 30);
            btnMuteAll.BaseColor = Theme.Off;
            btnMuteAll.Click += delegate { SetAllMuted(true); };

            RoundedButton btnUnmuteAll = new RoundedButton();
            btnUnmuteAll.Text = "取消全部静音";
            btnUnmuteAll.Size = new Size(120, 30);
            btnUnmuteAll.BaseColor = Theme.On;
            btnUnmuteAll.Click += delegate { SetAllMuted(false); };

            Label lblMain = new Label();
            lblMain.Text = "热键通道：";
            lblMain.ForeColor = Theme.TextDim;
            lblMain.AutoSize = true;
            lblMain.Padding = new Padding(10, 7, 0, 0);

            _mainChannel = new ComboBox();
            _mainChannel.DropDownStyle = ComboBoxStyle.DropDownList;
            _mainChannel.Size = new Size(160, 24);
            _mainChannel.FlatStyle = FlatStyle.Flat;
            _mainChannel.BackColor = Theme.Surface;
            _mainChannel.ForeColor = Theme.Text;
            _mainChannel.SelectedIndexChanged += OnMainChannelChanged;

            RoundedButton btnHotkeys = new RoundedButton();
            btnHotkeys.Text = "快捷键设置";
            btnHotkeys.Size = new Size(104, 30);
            btnHotkeys.BaseColor = Theme.Accent;
            btnHotkeys.Click += OnHotkeySettingsClick;

            _chkHotkey = new CheckBox();
            _chkHotkey.Text = "启用快捷键";
            _chkHotkey.Checked = true;
            _chkHotkey.ForeColor = Theme.TextDim;
            _chkHotkey.AutoSize = true;
            _chkHotkey.Padding = new Padding(10, 5, 0, 0);
            _chkHotkey.CheckedChanged += OnHotkeyToggle;

            _chkVirtual = new CheckBox();
            _chkVirtual.Text = "显示虚拟输入";
            _chkVirtual.ForeColor = Theme.TextDim;
            _chkVirtual.AutoSize = true;
            _chkVirtual.Padding = new Padding(10, 5, 0, 0);
            _chkVirtual.CheckedChanged += delegate { if (_vm.Connected) BuildCards(); SaveSettings(); };

            bar.Controls.Add(btnMuteAll);
            bar.Controls.Add(btnUnmuteAll);
            bar.Controls.Add(btnHotkeys);
            bar.Controls.Add(lblMain);
            bar.Controls.Add(_mainChannel);
            bar.Controls.Add(_chkHotkey);
            bar.Controls.Add(_chkVirtual);
            _footer.Controls.Add(bar);

            Controls.Add(_host);
            Controls.Add(_footer);
            Controls.Add(_header);
        }

        private void PlaceLaunchButton()
        {
            _btnLaunch.Location = new Point(_header.Width - 164, 17);
        }

        private void BuildTray()
        {
            _iconOn = BuildStateIcon(false);
            _iconOff = BuildStateIcon(true);

            _tray = new NotifyIcon();
            _tray.Icon = _iconOn;
            _tray.Text = "Voicemeeter 麦克风控制";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { RestoreFromTray(); };

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("显示主界面", null, delegate { RestoreFromTray(); });
            _trayToggleItem = new ToolStripMenuItem("切换麦克风", null, delegate { ToggleMainChannel(); });
            _trayMenu.Items.Add(_trayToggleItem);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("退出", null, delegate { ExitApp(); });
            _tray.ContextMenuStrip = _trayMenu;
        }

        private Icon BuildStateIcon(bool muted)
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(muted ? Theme.Off : Theme.On))
                {
                    g.FillEllipse(b, 1, 1, 30, 30);
                }
                using (SolidBrush w = new SolidBrush(Color.White))
                using (Pen p = new Pen(Color.White, 2f))
                {
                    g.FillEllipse(w, 13, 6, 6, 12);
                    g.DrawArc(p, 9, 9, 14, 15, 0, 180);
                    g.FillRectangle(w, 15, 19, 2, 5);
                    g.FillRectangle(w, 11, 24, 10, 2);
                    if (muted) g.DrawLine(p, 6, 26, 26, 6);
                }
            }
            Icon icon = Icon.FromHandle(bmp.GetHicon());
            if (muted) _bmpOff = bmp; else _bmpOn = bmp;
            return icon;
        }

        /// <summary>优先使用嵌入 exe 的图标，取不到时回退到运行时生成的状态图标</summary>
        private Icon LoadAppIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch
            {
                // 忽略，使用回退图标
            }
            return BuildStateIcon(false);
        }

        // ---------- 设置记忆 ----------

        private static string SettingsPath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMMicControl");
            return Path.Combine(dir, "settings.txt");
        }

        private void LoadSettings()
        {
            try
            {
                string path = SettingsPath();
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] kv = line.Split(new char[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim();
                    string value = kv[1].Trim();
                    if (key == "main")
                    {
                        int v;
                        if (int.TryParse(value, out v))
                        {
                            _mainIndex = v;
                            _mainIndexLoaded = true;
                        }
                    }
                    else if (key == "virtual") _chkVirtual.Checked = (value == "1");
                    else if (key == "hotkey") { _hotkeyOn = (value == "1"); _chkHotkey.Checked = _hotkeyOn; }
                    else if (key.StartsWith("hk.")) _rawHotkeys[key.Substring(3)] = value;
                }
                if (_rawHotkeys.Count > 0) _defaultsApplied = true;
            }
            catch
            {
                // 设置损坏时忽略，使用默认值
            }
        }

        private void SaveSettings()
        {
            try
            {
                string path = SettingsPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                List<string> lines = new List<string>();
                lines.Add("main=" + _mainIndex);
                lines.Add("virtual=" + (_chkVirtual.Checked ? "1" : "0"));
                lines.Add("hotkey=" + (_chkHotkey.Checked ? "1" : "0"));
                foreach (KeyValuePair<string, string> pair in _rawHotkeys)
                    lines.Add("hk." + pair.Key + "=" + pair.Value);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch
            {
                // 忽略写入失败
            }
        }

        // ---------- 连接与卡片 ----------

        private void Connect()
        {
            if (_vm.Login())
            {
                SetConnected(true, "已连接 · " + _vm.KindName + " · " + _vm.HardwareInputs + " 路硬件输入");
                BuildCards();
            }
            else
            {
                SetConnected(false, "未检测到运行中的 Voicemeeter");
                BuildOfflineCard();
            }
        }

        private void SetConnected(bool connected, string text)
        {
            _connectionText = text;
            _statusDot.BackColor = connected ? Theme.On : Theme.Off;
            _btnLaunch.Visible = !connected;
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            string text = _connectionText;
            if (string.IsNullOrEmpty(text)) text = "正在连接…";
            if (!string.IsNullOrEmpty(_hotkeyWarning)) text += " · " + _hotkeyWarning;
            _statusText.Text = text;
        }

        private int CardWidth()
        {
            int w = _host.ClientSize.Width - 28;
            if (w < 300) w = 300;
            return w;
        }

        private void BuildCards()
        {
            _vm.WaitForSync(800);
            _host.SuspendLayout();
            _host.Controls.Clear();
            _cards.Clear();
            _mainChannel.Items.Clear();

            int count = _chkVirtual.Checked ? _vm.TotalStrips : _vm.HardwareInputs;
            for (int i = 0; i < count; i++)
            {
                bool hardware = i < _vm.HardwareInputs;
                string name = _vm.GetChannelName(i);
                if (string.IsNullOrEmpty(name))
                    name = hardware ? "硬件输入 " + (i + 1) : "虚拟输入 " + (i - _vm.HardwareInputs + 1);

                ChannelCard card = new ChannelCard(_vm, i, name, hardware);
                card.Width = CardWidth();
                card.StateChanged += OnCardStateChanged;
                _host.Controls.Add(card);
                _cards.Add(card);
                _mainChannel.Items.Add(new ChannelEntry(i, (i + 1) + " · " + name));
            }

            if (!ApplySavedMainChannel()) SelectDefaultMainChannel();
            foreach (ChannelCard c in _cards) c.SyncFromDevice();
            UpdateMainHighlight();
            UpdateTrayState();
            RebuildHotkeys();
            _host.ResumeLayout(true);
        }

        private void BuildOfflineCard()
        {
            _host.Controls.Clear();
            RoundedPanel p = new RoundedPanel();
            p.Width = CardWidth();
            p.Height = 130;
            p.BackColor = Theme.Surface;

            Label tip = new Label();
            tip.Text = "未检测到运行中的 Voicemeeter。\r\n请先启动 Voicemeeter，程序会自动连接；\r\n也可以点下面的按钮直接启动它。";
            tip.Location = new Point(20, 18);
            tip.Size = new Size(p.Width - 40, 60);
            tip.ForeColor = Theme.TextDim;
            tip.Font = new Font("Segoe UI", 9.5f);

            RoundedButton btn = new RoundedButton();
            btn.Text = "启动 Voicemeeter";
            btn.Size = new Size(150, 32);
            btn.Location = new Point(20, 82);
            btn.BaseColor = Theme.Accent;
            btn.Click += OnLaunchClick;

            p.Controls.Add(tip);
            p.Controls.Add(btn);
            _host.Controls.Add(p);
        }

        private bool ApplySavedMainChannel()
        {
            if (!_mainIndexLoaded) return false;
            for (int i = 0; i < _mainChannel.Items.Count; i++)
            {
                ChannelEntry entry = _mainChannel.Items[i] as ChannelEntry;
                if (entry != null && entry.Index == _mainIndex)
                {
                    _mainChannel.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }

        private void SelectDefaultMainChannel()
        {
            int pick = 0;
            for (int i = 0; i < _cards.Count; i++)
            {
                string n = _cards[i].ChannelName.ToLowerInvariant();
                if (n.Contains("mic") || n.Contains("麦克风"))
                {
                    pick = i;
                    break;
                }
            }
            if (_cards.Count > 0)
            {
                _mainIndex = _cards[pick].StripIndex;
                if (_mainChannel.Items.Count > pick) _mainChannel.SelectedIndex = pick;
            }
        }

        private void UpdateMainHighlight()
        {
            foreach (ChannelCard c in _cards) c.IsMain = c.StripIndex == _mainIndex;
        }

        // ---------- 事件 ----------

        private void OnTick(object sender, EventArgs e)
        {
            if (!_vm.Connected)
            {
                _reconnectTicks++;
                if (_reconnectTicks < 7) return;
                _reconnectTicks = 0;
                if (_vm.Login())
                {
                    SetConnected(true, "已连接 · " + _vm.KindName + " · " + _vm.HardwareInputs + " 路硬件输入");
                    BuildCards();
                }
                return;
            }

            // Remote API 只有在 dirty 标志清零后读到的才是最新值，
            // 所以变更未落定时跳过本轮；若长时间一直 dirty 则强制刷新，避免界面卡住。
            if (_vm.IsDirty())
            {
                _dirtyStreak++;
                if (_dirtyStreak < 5) return;
            }
            _dirtyStreak = 0;
            foreach (ChannelCard c in _cards) c.SyncFromDevice();
            UpdateTrayState();
        }

        private void OnCardStateChanged(object sender, EventArgs e)
        {
            ChannelCard card = sender as ChannelCard;
            if (card == null) return;
            UpdateTrayState();
            if (card.StripIndex == _mainIndex)
                ShowBalloon(card.IsMuted ? "麦克风已静音" : "麦克风已开启");
        }

        private void OnMainChannelChanged(object sender, EventArgs e)
        {
            ChannelEntry entry = _mainChannel.SelectedItem as ChannelEntry;
            if (entry == null) return;
            _mainIndex = entry.Index;
            UpdateMainHighlight();
            UpdateTrayState();
            SaveSettings();
        }

        private void OnHotkeyToggle(object sender, EventArgs e)
        {
            _hotkeyOn = _chkHotkey.Checked;
            RegisterHotkeys();
            SaveSettings();
        }

        private void OnHotkeySettingsClick(object sender, EventArgs e)
        {
            List<HotkeyBinding> working = new List<HotkeyBinding>();
            foreach (HotkeyBinding b in _bindings) working.Add(b.Clone());

            using (HotkeyForm form = new HotkeyForm(working, _mainIndex))
            {
                // 传副本，取消时不会改动实际绑定
                if (form.ShowDialog(this) == DialogResult.OK && form.Changed)
                {
                    _bindings.Clear();
                    _bindings.AddRange(working);
                    SyncRawHotkeys();
                    RegisterHotkeys();
                    UpdateHotkeyHints();
                    SaveSettings();
                }
            }
        }

        // ---------- 快捷键 ----------

        private void RebuildHotkeys()
        {
            _bindings.Clear();
            foreach (ChannelCard c in _cards)
            {
                HotkeyBinding b = new HotkeyBinding(
                    HotkeyActionKind.ToggleChannel, c.StripIndex, "切换：" + c.ChannelName);
                ApplySavedHotkey(b);
                _bindings.Add(b);
            }
            AddGlobalHotkey(HotkeyActionKind.MuteAll, "全部静音");
            AddGlobalHotkey(HotkeyActionKind.UnmuteAll, "取消全部静音");
            AddGlobalHotkey(HotkeyActionKind.ToggleAll, "全部静音 / 取消（切换）");

            if (!_defaultsApplied)
            {
                _defaultsApplied = true;
                ApplyDefaultHotkeys();
                SyncRawHotkeys();
            }

            if (IsHandleCreated) RegisterHotkeys();
            else
            {
                // 首次访问 Handle 会触发 OnHandleCreated，在那里完成注册
                IntPtr unused = Handle;
            }
            UpdateHotkeyHints();
        }

        private void AddGlobalHotkey(HotkeyActionKind kind, string label)
        {
            HotkeyBinding b = new HotkeyBinding(kind, -1, label);
            ApplySavedHotkey(b);
            _bindings.Add(b);
        }

        private void ApplySavedHotkey(HotkeyBinding binding)
        {
            string raw;
            if (_rawHotkeys.TryGetValue(binding.SettingsKey, out raw)) binding.FromSettingValue(raw);
        }

        private void ApplyDefaultHotkeys()
        {
            foreach (HotkeyBinding b in _bindings)
            {
                if (b.Kind == HotkeyActionKind.ToggleChannel && b.ChannelIndex == _mainIndex)
                    b.Set(HotkeyBinding.ModControl | HotkeyBinding.ModAlt, (uint)Keys.M);
                else if (b.Kind == HotkeyActionKind.MuteAll)
                    b.Set(HotkeyBinding.ModControl | HotkeyBinding.ModAlt | HotkeyBinding.ModShift, (uint)Keys.M);
                else if (b.Kind == HotkeyActionKind.UnmuteAll)
                    b.Set(HotkeyBinding.ModControl | HotkeyBinding.ModAlt | HotkeyBinding.ModShift, (uint)Keys.N);
            }
        }

        private void SyncRawHotkeys()
        {
            foreach (HotkeyBinding b in _bindings) _rawHotkeys[b.SettingsKey] = b.ToSettingValue();
        }

        private void RegisterHotkeys()
        {
            if (!IsHandleCreated) return;   // 句柄创建后会由 OnHandleCreated 再来注册
            UnregisterHotkeys();
            int failed = 0;
            if (_hotkeyOn)
            {
                for (int i = 0; i < _bindings.Count; i++)
                {
                    HotkeyBinding b = _bindings[i];
                    b.Id = HotkeyIdBase + i;
                    if (!b.Enabled) continue;
                    if (User32.RegisterHotKey(Handle, b.Id, b.Modifiers, b.VirtualKey))
                        _registeredHotkeyIds.Add(b.Id);
                    else
                        failed++;
                }
            }
            _hotkeyWarning = failed > 0 ? (failed + " 个快捷键被占用") : null;
            UpdateStatusText();
            UpdateTrayMenuText();
        }

        private void UnregisterHotkeys()
        {
            foreach (int id in _registeredHotkeyIds) User32.UnregisterHotKey(Handle, id);
            _registeredHotkeyIds.Clear();
        }

        private void UpdateTrayMenuText()
        {
            if (_trayToggleItem == null) return;
            string hint = string.Empty;
            foreach (HotkeyBinding b in _bindings)
            {
                if (b.Kind == HotkeyActionKind.ToggleChannel && b.ChannelIndex == _mainIndex && b.Enabled)
                {
                    hint = "（" + b.DisplayText + "）";
                    break;
                }
            }
            _trayToggleItem.Text = "切换麦克风" + hint;
        }

        private void UpdateHotkeyHints()
        {
            foreach (ChannelCard c in _cards)
            {
                string hint = string.Empty;
                foreach (HotkeyBinding b in _bindings)
                {
                    if (b.Kind == HotkeyActionKind.ToggleChannel && b.ChannelIndex == c.StripIndex && b.Enabled)
                    {
                        hint = b.DisplayText;
                        break;
                    }
                }
                c.HotkeyHint = hint;
            }
        }

        private void RunHotkey(HotkeyBinding binding)
        {
            if (!_vm.Connected) return;
            switch (binding.Kind)
            {
                case HotkeyActionKind.ToggleChannel:
                    foreach (ChannelCard c in _cards)
                    {
                        if (c.StripIndex == binding.ChannelIndex)
                        {
                            c.Toggle();
                            return;
                        }
                    }
                    break;

                case HotkeyActionKind.MuteAll:
                    SetAllMuted(true);
                    break;

                case HotkeyActionKind.UnmuteAll:
                    SetAllMuted(false);
                    break;

                case HotkeyActionKind.ToggleAll:
                    SetAllMuted(!AllMuted());
                    break;
            }
        }

        private bool AllMuted()
        {
            if (_cards.Count == 0) return false;
            foreach (ChannelCard c in _cards)
            {
                if (!c.IsMuted) return false;
            }
            return true;
        }

        private void OnLaunchClick(object sender, EventArgs e)
        {
            _btnLaunch.Enabled = false;
            SetConnected(false, "正在启动 Voicemeeter…");
            if (_vm.Launch())
            {
                _reconnectTicks = 0;
            }
            else
            {
                _btnLaunch.Enabled = true;
                SetConnected(false, "启动失败，请手动打开 Voicemeeter");
            }
        }

        private void SetAllMuted(bool muted)
        {
            if (!_vm.Connected) return;
            foreach (ChannelCard c in _cards) c.SetMuted(muted, false);
            UpdateTrayState();
            ShowBalloon(muted ? "全部通道已静音" : "全部通道已取消静音");
        }

        private void ToggleMainChannel()
        {
            if (!_vm.Connected) return;
            foreach (ChannelCard c in _cards)
            {
                if (c.StripIndex == _mainIndex)
                {
                    c.Toggle();
                    return;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_host == null) return;
            int w = CardWidth();
            foreach (Control c in _host.Controls)
            {
                if (c is ChannelCard || c is RoundedPanel) c.Width = w;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeys();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                foreach (HotkeyBinding b in _bindings)
                {
                    if (b.Id == id && b.Enabled)
                    {
                        RunHotkey(b);
                        m.Result = IntPtr.Zero;
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            if (!_realExit)
            {
                e.Cancel = true;
                Hide();
                if (!_trayHintShown)
                {
                    _tray.ShowBalloonTip(2500, "Voicemeeter 麦克风控制", "已最小化到系统托盘，双击图标可重新打开。", ToolTipIcon.Info);
                    _trayHintShown = true;
                }
                return;
            }
            base.OnFormClosing(e);
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
        }

        private void ExitApp()
        {
            _realExit = true;
            Close();
        }

        // ---------- 托盘状态 ----------

        private void UpdateTrayState()
        {
            if (_tray == null) return;
            bool muted = false;
            foreach (ChannelCard c in _cards)
            {
                if (c.StripIndex == _mainIndex) muted = c.IsMuted;
            }
            _tray.Icon = muted ? _iconOff : _iconOn;
            _tray.Text = muted ? "麦克风已静音" : "麦克风工作中";
        }

        private void ShowBalloon(string message)
        {
            if (_tray == null) return;
            _tray.ShowBalloonTip(1200, "Voicemeeter", message, ToolTipIcon.Info);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { UnregisterHotkeys(); }
                catch { /* ignore */ }
                if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
                if (_trayMenu != null) _trayMenu.Dispose();
                if (_timer != null) _timer.Dispose();
                if (_iconOn != null) _iconOn.Dispose();
                if (_iconOff != null) _iconOff.Dispose();
                if (_bmpOn != null) _bmpOn.Dispose();
                if (_bmpOff != null) _bmpOff.Dispose();
                _vm.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
