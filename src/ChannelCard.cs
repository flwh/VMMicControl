using System;
using System.Drawing;
using System.Windows.Forms;

namespace VMMicControl
{
    /// <summary>单个输入通道的控制卡片：静音开关 + 增益</summary>
    internal sealed class ChannelCard : RoundedPanel
    {
        private readonly Voicemeeter _vm;
        private readonly int _index;
        private readonly bool _hardware;
        private readonly Label _badge;
        private readonly Label _name;
        private readonly Label _state;
        private readonly RoundedButton _toggle;
        private readonly TrackBar _gain;
        private readonly Label _gainText;
        private bool _updating;
        private bool _muted;
        private string _hotkeyHint;
        private DateTime _lastLocalChange = DateTime.MinValue;

        public event EventHandler StateChanged;

        public ChannelCard(Voicemeeter vm, int index, string name, bool hardware)
        {
            _vm = vm;
            _index = index;
            _hardware = hardware;

            SuspendLayout();
            Height = 96;
            BackColor = Theme.Surface;
            BorderColor = Theme.Border;
            Margin = new Padding(0, 0, 0, 10);

            _badge = new Label();
            _badge.Text = (index + 1).ToString();
            _badge.Size = new Size(36, 36);
            _badge.Location = new Point(14, 16);
            _badge.ForeColor = Color.White;
            _badge.Font = new Font("Segoe UI Semibold", 12f);
            _badge.TextAlign = ContentAlignment.MiddleCenter;
            _badge.BackColor = hardware ? Theme.Accent : Theme.Border;
            _badge.Region = new Region(Theme.RoundedRect(new Rectangle(0, 0, 36, 36), 10));

            _name = new Label();
            _name.Text = name;
            _name.Location = new Point(62, 14);
            _name.Size = new Size(200, 22);
            _name.ForeColor = Theme.Text;
            _name.Font = new Font("Segoe UI Semibold", 11f);
            _name.AutoEllipsis = true;

            _state = new Label();
            _state.Location = new Point(62, 38);
            _state.Size = new Size(200, 18);
            _state.ForeColor = Theme.TextDim;
            _state.Font = new Font("Segoe UI", 8.5f);
            _state.AutoEllipsis = true;
            _state.Text = hardware ? "硬件输入" : "虚拟输入";

            _toggle = new RoundedButton();
            _toggle.Size = new Size(104, 38);
            _toggle.BaseColor = Theme.On;
            _toggle.Text = "麦克风开";
            _toggle.Click += OnToggleClick;

            _gain = new TrackBar();
            _gain.Minimum = -600;
            _gain.Maximum = 120;
            _gain.TickFrequency = 60;
            _gain.SmallChange = 10;
            _gain.LargeChange = 60;
            _gain.BackColor = Theme.Surface;
            _gain.Size = new Size(160, 26);
            _gain.Location = new Point(62, 62);
            _gain.ValueChanged += OnGainChanged;

            _gainText = new Label();
            _gainText.Size = new Size(70, 20);
            _gainText.ForeColor = Theme.TextDim;
            _gainText.Font = new Font("Segoe UI", 8.5f);
            _gainText.TextAlign = ContentAlignment.MiddleLeft;

            Controls.Add(_badge);
            Controls.Add(_name);
            Controls.Add(_state);
            Controls.Add(_toggle);
            Controls.Add(_gain);
            Controls.Add(_gainText);
            ResumeLayout(false);
            LayoutControls();
        }

        public int StripIndex
        {
            get { return _index; }
        }

        public string ChannelName
        {
            get { return _name.Text; }
        }

        public bool IsMuted
        {
            get { return _muted; }
        }

        public bool IsMain
        {
            set { BorderColor = value ? Theme.Accent : Theme.Border; }
        }

        /// <summary>该通道绑定的快捷键提示（显示在状态行）</summary>
        public string HotkeyHint
        {
            set
            {
                _hotkeyHint = value;
                ApplyState(_muted);
            }
        }

        private void LayoutControls()
        {
            if (_name == null || _toggle == null || _gain == null || _gainText == null) return;
            int right = Width - 14;
            _toggle.Location = new Point(right - 104, 14);
            _name.Width = Math.Max(60, _toggle.Left - 62 - 10);
            _state.Width = _name.Width;
            _gain.Width = Math.Max(60, right - 62 - 80);
            _gainText.Location = new Point(_gain.Right + 8, 66);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        /// <summary>
        /// 从 Voicemeeter 同步状态。调用方需先通过 <see cref="Voicemeeter.IsDirty"/> 确认参数稳定；
        /// 用户刚操作过的卡片会短暂跳过，避免 API 缓存延迟把按钮弹回去。
        /// </summary>
        public void SyncFromDevice()
        {
            // 刚操作过的通道短暂不同步，避免 Remote API 的缓存延迟把按钮弹回去
            if ((DateTime.Now - _lastLocalChange).TotalMilliseconds < 1200) return;
            _updating = true;
            try
            {
                bool muted = _vm.GetMute(_index);
                float gain = _vm.GetGain(_index);
                ApplyState(muted);
                int v = (int)Math.Round(gain * 10);
                if (v < _gain.Minimum) v = _gain.Minimum;
                if (v > _gain.Maximum) v = _gain.Maximum;
                _gain.Value = v;
                _gainText.Text = (v / 10.0).ToString("0.0") + " dB";
            }
            finally
            {
                _updating = false;
            }
        }

        public void Toggle()
        {
            SetMuted(!_muted, true);
        }

        public void SetMuted(bool muted, bool notify)
        {
            _vm.SetMute(_index, muted);
            _lastLocalChange = DateTime.Now;
            ApplyState(muted);
            if (notify && StateChanged != null) StateChanged(this, EventArgs.Empty);
        }

        private void ApplyState(bool muted)
        {
            _muted = muted;
            _toggle.Text = muted ? "已静音" : "麦克风开";
            _toggle.BaseColor = muted ? Theme.Off : Theme.On;
            string suffix = _hardware ? "硬件输入" : "虚拟输入";
            if (!string.IsNullOrEmpty(_hotkeyHint)) suffix += " · " + _hotkeyHint;
            _state.Text = (muted ? "已静音 · " : "工作中 · ") + suffix;
            _state.ForeColor = muted ? Theme.Off : Theme.On;
            _badge.BackColor = muted ? Theme.Off : (_hardware ? Theme.Accent : Theme.Border);
        }

        private void OnToggleClick(object sender, EventArgs e)
        {
            if (_updating) return;
            Toggle();
        }

        private void OnGainChanged(object sender, EventArgs e)
        {
            if (_updating) return;
            _lastLocalChange = DateTime.Now;
            float db = _gain.Value / 10f;
            _gainText.Text = db.ToString("0.0") + " dB";
            _vm.SetGain(_index, db);
        }
    }
}
