using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AdbGui
{
    internal static class Ui
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>统一深色窗口外观（含 Win10/11 深色标题栏）</summary>
        public static void Dark(Form form, string title, int width, int height, bool resizable)
        {
            form.Text = title;
            form.BackColor = Theme.Bg;
            form.ForeColor = Theme.Text;
            form.Font = Theme.UiFont;
            form.Icon = IconFactory.Create(32);
            form.ClientSize = new Size(width, height);
            form.StartPosition = FormStartPosition.CenterParent;
            form.FormBorderStyle = resizable ? FormBorderStyle.Sizable : FormBorderStyle.FixedDialog;
            form.MaximizeBox = resizable;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            try
            {
                int on = 1;
                DwmSetWindowAttribute(form.Handle, 20, ref on, 4);
                DwmSetWindowAttribute(form.Handle, 19, ref on, 4);
            }
            catch { }
        }

        public static RoundedButton Button(string text, Color color, int width, EventHandler onClick)
        {
            RoundedButton b = new RoundedButton();
            b.Text = text;
            b.BaseColor = color;
            b.Size = new Size(width, 30);
            if (onClick != null) b.Click += onClick;
            return b;
        }

        public static Label Label(string text, Color color, Font font, Point location)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = color;
            l.Font = font;
            l.AutoSize = true;
            l.Location = location;
            l.BackColor = Color.Transparent;
            return l;
        }
    }

    /// <summary>单行 / 多行文本输入窗</summary>
    internal sealed class InputDialog : Form
    {
        private readonly CueTextBox _box;
        public string Value { get; private set; }

        private InputDialog(string title, string prompt, string defaultValue, bool multiline, string hint, string cue)
        {
            int height = multiline ? 268 : 200;
            Ui.Dark(this, title, 430, height, false);

            Label lblPrompt = Ui.Label(prompt, Theme.Text, Theme.UiFontBold, new Point(18, 16));
            Controls.Add(lblPrompt);

            _box = new CueTextBox();
            _box.Cue = cue ?? "";
            _box.Text = defaultValue ?? "";
            _box.Location = new Point(18, 44);
            _box.Size = new Size(392, multiline ? 140 : 26);
            if (multiline)
            {
                _box.Multiline = true;
                _box.ScrollBars = ScrollBars.Vertical;
                _box.AcceptsReturn = true;
                _box.Font = Theme.MonoFont;
            }
            Controls.Add(_box);

            int y = multiline ? 196 : 80;
            if (!string.IsNullOrEmpty(hint))
            {
                Label lblHint = Ui.Label(hint, Theme.TextDim, new Font("Segoe UI", 8.25f), new Point(18, y));
                Controls.Add(lblHint);
                y += 26;
            }

            RoundedButton ok = Ui.Button("确定", Theme.Accent, 96, delegate { Accept(); });
            ok.Location = new Point(218, y);
            Controls.Add(ok);
            RoundedButton cancel = Ui.Button("取消", Theme.Mix(Theme.Surface, Theme.Border, 0.55), 96, delegate { DialogResult = DialogResult.Cancel; Close(); });
            cancel.Location = new Point(320, y);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate { _box.Focus(); _box.SelectAll(); };
        }

        private void Accept()
        {
            Value = _box.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }

        public static string Ask(IWin32Window owner, string title, string prompt, string defaultValue,
                                 bool multiline, string hint, string cue)
        {
            using (InputDialog dlg = new InputDialog(title, prompt, defaultValue, multiline, hint, cue))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Value : null;
            }
        }

        public static string Ask(IWin32Window owner, string title, string prompt, string defaultValue)
        {
            return Ask(owner, title, prompt, defaultValue, false, null, null);
        }
    }

    /// <summary>可搜索的深色列表窗（用于应用列表等），带若干操作按钮</summary>
    internal sealed class ListDialog : Form
    {
        private readonly ListBox _list;
        private readonly Label _hint;
        private readonly List<string> _items = new List<string>();
        private readonly Func<string, bool> _canAction;
        private readonly string _emptyText;

        /// <summary>参数：(选中的条目, 操作名)</summary>
        public event Action<string, string> ActionChosen;

        public ListDialog(string title, string hint, string emptyText, string[] actions, Func<string, bool> canAction)
        {
            _emptyText = emptyText;
            _canAction = canAction;

            Ui.Dark(this, title, 520, 560, true);
            MinimumSize = new Size(420, 420);

            _hint = Ui.Label(hint, Theme.TextDim, Theme.UiFont, new Point(18, 16));
            Controls.Add(_hint);

            CueTextBox filter = new CueTextBox();
            filter.Cue = "输入关键字过滤…";
            filter.Location = new Point(18, 44);
            filter.Size = new Size(484, 26);
            filter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            filter.TextChanged += delegate { FilterBox(filter.Text); };
            Controls.Add(filter);

            _list = new ListBox();
            _list.Location = new Point(18, 80);
            _list.Size = new Size(484, 400);
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _list.BackColor = Theme.SurfaceAlt;
            _list.ForeColor = Theme.Text;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Font = Theme.MonoFont;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = 22;
            _list.IntegralHeight = false;
            _list.DoubleClick += delegate { CopySelected(); };
            _list.DrawItem += ListDrawItem;
            _list.SelectedIndexChanged += delegate { UpdateButtons(); };
            Controls.Add(_list);

            int x = 18;
            int y = 492;
            foreach (string action in actions)
            {
                string a = action;
                int w = 96;
                Controls.Add(PlaceButton(Ui.Button(a, ActionColor(a), w, delegate { Fire(a); }), ref x, ref y));
            }

            RoundedButton copy = Ui.Button("复制包名", Theme.Mix(Theme.Surface, Theme.Border, 0.55), 90, delegate { CopySelected(); });
            copy.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            copy.Location = new Point(18, 526);
            Controls.Add(copy);

            Text = title;
            UpdateButtons();
        }

        private static Control PlaceButton(Control c, ref int x, ref int y)
        {
            c.Location = new Point(x, y);
            c.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            x += c.Width + 8;
            return c;
        }

        private static Color ActionColor(string action)
        {
            if (action == "卸载") return Theme.Red;
            if (action == "停止") return Theme.Amber;
            if (action == "刷新") return Theme.Mix(Theme.Surface, Theme.Border, 0.55);
            return Theme.Accent;
        }

        private void ListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush back = new SolidBrush(selected ? Theme.Mix(Theme.SurfaceAlt, Theme.Accent, 0.35) : Theme.SurfaceAlt))
                e.Graphics.FillRectangle(back, e.Bounds);
            string text = _list.Items[e.Index].ToString();
            TextRenderer.DrawText(e.Graphics, text, Theme.MonoFont, e.Bounds, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        public void SetItemsHint(string text)
        {
            if (IsDisposed) return;
            _hint.Text = text;
        }

        public void SetItems(List<string> items)
        {
            if (IsDisposed) return;
            _items.Clear();
            _items.AddRange(items);
            FilterBox(GetFilterText());
            SetItemsHint(string.Format("共 {0} 项", _items.Count));
        }

        private string GetFilterText()
        {
            foreach (Control c in Controls)
            {
                CueTextBox tb = c as CueTextBox;
                if (tb != null && tb.Cue.Length > 0) return tb.Text;
            }
            return "";
        }

        private void FilterBox(string keyword)
        {
            string key = (keyword ?? "").Trim();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (string item in _items)
            {
                if (key.Length == 0 || item.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    _list.Items.Add(item);
            }
            _list.EndUpdate();
            if (_list.Items.Count == 0 && _items.Count == 0)
                SetItemsHint(_emptyText);
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            string selected = Current();
            foreach (Control c in Controls)
            {
                Button b = c as Button;
                if (b == null) continue;
                bool needsItem = b.Text != "刷新" && b.Text != "复制包名";
                if (needsItem) b.Enabled = selected != null && (_canAction == null || _canAction(b.Text));
            }
        }

        private string Current()
        {
            return _list.SelectedItem == null ? null : _list.SelectedItem.ToString();
        }

        private void Fire(string action)
        {
            string item = Current();
            if (item == null)
            {
                MessageBox.Show(this, "请先在列表中选择一项。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ActionChosen != null) ActionChosen(item, action);
        }

        private void CopySelected()
        {
            string item = Current();
            if (item == null || item.Length == 0) return;
            try { Clipboard.SetText(item); } catch { }
            SetItemsHint("已复制: " + item);
        }
    }

    /// <summary>查看设备截图：在工具内展示图片，关闭窗口后自动删除临时文件</summary>
    internal sealed class ScreenshotForm : Form
    {
        private readonly string _file;
        private readonly PictureBox _pic;

        public ScreenshotForm(string file)
        {
            _file = file;
            // 无边框直接展示截图：去掉标题栏/边框/面板背景
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;

            _pic = new PictureBox();
            _pic.Location = new Point(0, 0);
            _pic.SizeMode = PictureBoxSizeMode.Zoom;
            try
            {
                _pic.Image = Image.FromFile(file);
                int w = _pic.Image.Width;
                int h = _pic.Image.Height;
                // 始终按主屏工作区缩放，保证整张截图完整可见（构造期窗口尚未关联屏幕，
                // 不能用 Screen.GetWorkingArea(this)，否则可能取到异常大的区域而不缩放）
                Rectangle wa = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.WorkingArea : Screen.GetWorkingArea(this);
                int maxW = Math.Max(320, wa.Width - 40);
                int maxH = Math.Max(240, wa.Height - 40);
                double scale = (w > maxW || h > maxH) ? Math.Min((double)maxW / w, (double)maxH / h) : 1.0;
                int dispW = (int)Math.Round(w * scale);
                int dispH = (int)Math.Round(h * scale);
                _pic.Width = dispW;
                _pic.Height = dispH;
                ClientSize = new Size(dispW, dispH);
            }
            catch (Exception ex)
            {
                Label err = Ui.Label("无法显示截图：" + ex.Message, Theme.Red, Theme.UiFont, new Point(16, 16));
                Controls.Add(err);
                ClientSize = new Size(360, 60);
            }
            Controls.Add(_pic);

            // 无标题栏，点击任意处或按 Esc 关闭
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            MouseClick += delegate { Close(); };
            FormClosing += delegate { Cleanup(); };
        }

        private void Cleanup()
        {
            if (_pic != null && _pic.Image != null)
            {
                _pic.Image.Dispose();
                _pic.Image = null;
            }
            try { if (File.Exists(_file)) File.Delete(_file); }
            catch { }
        }
    }

    /// <summary>独立的设备按键面板：点击按钮模拟手机按键（需已连接设备）</summary>
    internal sealed class KeyForm : Form
    {
        public event Action<string, string> KeyPressed;

        private static readonly string[][] KeyItems = new string[][]
        {
            new string[] { "主页", "KEYCODE_HOME" },
            new string[] { "返回", "KEYCODE_BACK" },
            new string[] { "最近任务", "KEYCODE_APP_SWITCH" },
            new string[] { "菜单", "KEYCODE_MENU" },
            new string[] { "电源键", "KEYCODE_POWER" },
            new string[] { "唤醒屏幕", "KEYCODE_WAKEUP" },
            new string[] { "息屏", "KEYCODE_SLEEP" },
            new string[] { "音量 +", "KEYCODE_VOLUME_UP" },
            new string[] { "音量 -", "KEYCODE_VOLUME_DOWN" },
            new string[] { "静音", "KEYCODE_VOLUME_MUTE" },
        };

        public KeyForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Border;
            ShowInTaskbar = false;
            Font = Theme.UiFont;

            RoundedPanel frame = new RoundedPanel();
            frame.Dock = DockStyle.Fill;
            frame.BackColor = Theme.Bg;
            frame.BorderColor = Theme.Border;
            frame.Radius = 12;
            frame.Padding = new Padding(1);
            Controls.Add(frame);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Theme.Bg;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Padding = new Padding(0);
            frame.Controls.Add(root);

            // 标题栏（可拖动）
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Theme.SurfaceAlt;
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.Border))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Label title = Ui.Label("按键", Theme.Text, Theme.TitleFont, new Point(14, 9));
            header.Controls.Add(title);

            SysButton closeBtn = new SysButton();
            closeBtn.Symbol = SysButton.Kind.Close;
            closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            closeBtn.Location = new Point(0, 6);
            closeBtn.Click += delegate { Close(); };
            header.Controls.Add(closeBtn);

            header.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); }
            };
            root.Controls.Add(header, 0, 0);

            Label hint = Ui.Label("点击按钮模拟设备按键（需已连接设备）", Theme.TextDim, Theme.UiFont, new Point(14, 6));
            root.Controls.Add(hint, 0, 1);

            // 按键网格（两列）
            int rows = (KeyItems.Length + 1) / 2;
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.BackColor = Theme.Bg;
            grid.Padding = new Padding(14, 10, 14, 14);
            grid.ColumnCount = 2;
            grid.RowCount = rows;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

            for (int i = 0; i < KeyItems.Length; i++)
            {
                string label = KeyItems[i][0];
                string code = KeyItems[i][1];
                GhostButton b = new GhostButton();
                b.Text = label;
                b.Height = 32;
                b.Margin = new Padding(4);
                b.Click += delegate { if (KeyPressed != null) KeyPressed(label, code); };
                grid.Controls.Add(b, i % 2, i / 2);
            }
            root.Controls.Add(grid, 0, 2);

            ClientSize = new Size(320, 40 + 26 + rows * 38 + 28);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            FormClosed += delegate { Region = null; };

            Region = new Region(Theme.RoundedRect(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), 12));
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    }
}
