using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VMMicControl
{
    /// <summary>快捷键设置窗口：点击右侧按钮后直接按下新的组合键</summary>
    internal sealed class HotkeyForm : Form
    {
        private readonly List<HotkeyBinding> _bindings;
        private readonly List<Button> _keyButtons = new List<Button>();
        private readonly int _defaultChannel;
        private HotkeyBinding _capturing;
        private int _captureIndex = -1;
        private bool _changed;

        public HotkeyForm(List<HotkeyBinding> bindings, int defaultChannel)
        {
            _bindings = bindings;
            _defaultChannel = defaultChannel;
            BuildUi();
        }

        public bool Changed
        {
            get { return _changed; }
        }

        private void BuildUi()
        {
            Text = "快捷键设置";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 540);
            MinimumSize = new Size(500, 320);
            BackColor = Theme.Bg;
            Font = new Font("Segoe UI", 9f);
            KeyPreview = true;
            MaximizeBox = false;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 58;
            header.BackColor = Theme.SurfaceAlt;

            Label title = new Label();
            title.Text = "快捷键设置";
            title.Location = new Point(16, 9);
            title.Size = new Size(300, 22);
            title.ForeColor = Theme.Text;
            title.Font = new Font("Segoe UI Semibold", 12f);

            Label tip = new Label();
            tip.Text = "点右侧按钮后按下新的组合键；Esc / Backspace 可清除该快捷键。";
            tip.Location = new Point(16, 33);
            tip.Size = new Size(500, 18);
            tip.ForeColor = Theme.TextDim;
            tip.Font = new Font("Segoe UI", 8.5f);

            header.Controls.Add(title);
            header.Controls.Add(tip);

            Panel list = new Panel();
            list.Dock = DockStyle.Fill;
            list.AutoScroll = true;
            list.BackColor = Theme.Bg;

            for (int i = 0; i < _bindings.Count; i++)
            {
                HotkeyBinding binding = _bindings[i];
                int y = 10 + i * 42;

                Label label = new Label();
                label.Text = binding.Label;
                label.Location = new Point(16, y + 7);
                label.Size = new Size(230, 20);
                label.ForeColor = Theme.Text;
                label.AutoEllipsis = true;

                Button keyButton = MakeButton(binding.DisplayText, 252, y, 168, 30);
                keyButton.Tag = i;
                keyButton.Click += OnKeyButtonClick;
                _keyButtons.Add(keyButton);

                Button clearButton = MakeButton("清除", 428, y, 56, 30);
                clearButton.Tag = i;
                clearButton.Click += OnClearClick;

                list.Controls.Add(label);
                list.Controls.Add(keyButton);
                list.Controls.Add(clearButton);
            }

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 54;
            footer.BackColor = Theme.SurfaceAlt;

            Button btnDefault = MakeButton("恢复默认", 16, 12, 100, 30);
            btnDefault.Click += OnRestoreDefaults;

            Button btnOk = MakeButton("确定", 370, 12, 80, 30);
            btnOk.DialogResult = DialogResult.OK;
            btnOk.BackColor = Theme.Accent;
            btnOk.ForeColor = Color.White;

            Button btnCancel = MakeButton("取消", 458, 12, 80, 30);
            btnCancel.DialogResult = DialogResult.Cancel;

            footer.Controls.Add(btnDefault);
            footer.Controls.Add(btnOk);
            footer.Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.Add(list);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private static Button MakeButton(string text, int x, int y, int width, int height)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(width, height);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Theme.Surface;
            b.ForeColor = Theme.Text;
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.Border;
            return b;
        }

        private void OnKeyButtonClick(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            if (_capturing != null) EndCapture();

            _captureIndex = (int)button.Tag;
            _capturing = _bindings[_captureIndex];
            button.Text = "请按下快捷键…";
            button.ForeColor = Theme.Accent;
            ActiveControl = null;
            Focus();
        }

        private void OnClearClick(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            int index = (int)button.Tag;
            if (_captureIndex == index) EndCapture();
            _bindings[index].Clear();
            _changed = true;
            _keyButtons[index].Text = _bindings[index].DisplayText;
        }

        private void OnRestoreDefaults(object sender, EventArgs e)
        {
            if (_capturing != null) EndCapture();
            foreach (HotkeyBinding b in _bindings) b.Clear();

            foreach (HotkeyBinding b in _bindings)
            {
                if (b.Kind == HotkeyActionKind.ToggleChannel && b.ChannelIndex == _defaultChannel)
                {
                    b.Set(HotkeyBinding.ModControl | HotkeyBinding.ModAlt, (uint)Keys.M);
                    break;
                }
            }
            foreach (HotkeyBinding b in _bindings)
            {
                if (b.Kind == HotkeyActionKind.MuteAll)
                    b.Set(HotkeyBinding.ModControl | HotkeyBinding.ModAlt | HotkeyBinding.ModShift, (uint)Keys.M);
                else if (b.Kind == HotkeyActionKind.UnmuteAll)
                    b.Set(HotkeyBinding.ModControl | HotkeyBinding.ModAlt | HotkeyBinding.ModShift, (uint)Keys.N);
            }

            for (int i = 0; i < _keyButtons.Count; i++) _keyButtons[i].Text = _bindings[i].DisplayText;
            _changed = true;
        }

        private void EndCapture()
        {
            if (_captureIndex >= 0 && _captureIndex < _keyButtons.Count)
            {
                _keyButtons[_captureIndex].Text = _bindings[_captureIndex].DisplayText;
                _keyButtons[_captureIndex].ForeColor = Theme.Text;
            }
            _capturing = null;
            _captureIndex = -1;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_capturing != null)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;

                if (HotkeyBinding.IsModifierKey(e.KeyCode)) return;

                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
                    _capturing.Clear();
                else
                    _capturing.Set(HotkeyBinding.ModifiersFromKeys(e.Modifiers), (uint)e.KeyCode);

                _changed = true;
                EndCapture();
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
