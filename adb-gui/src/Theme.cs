using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AdbGui
{
    /// <summary>深色配色与自绘控件</summary>
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(23, 25, 31);
        public static readonly Color Surface = Color.FromArgb(33, 36, 44);
        public static readonly Color SurfaceAlt = Color.FromArgb(28, 31, 38);
        public static readonly Color Border = Color.FromArgb(52, 57, 70);
        public static readonly Color Text = Color.FromArgb(233, 237, 244);
        public static readonly Color TextDim = Color.FromArgb(142, 150, 167);
        public static readonly Color Green = Color.FromArgb(46, 204, 113);
        public static readonly Color Red = Color.FromArgb(231, 76, 60);
        public static readonly Color Amber = Color.FromArgb(241, 196, 15);
        public static readonly Color Accent = Color.FromArgb(90, 150, 255);

        public static readonly Font UiFont = new Font("Segoe UI", 9f);
        public static readonly Font UiFontBold = new Font("Segoe UI Semibold", 9f);
        public static readonly Font MonoFont = new Font("Consolas", 9.5f);
        public static readonly Font TitleFont = new Font("Segoe UI Semibold", 11f);

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Mix(Color a, Color b, double amount)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * amount),
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }
    }

    /// <summary>自绘圆角按钮</summary>
    internal class RoundedButton : Button
    {
        private int _radius = 6;
        private Color _baseColor = Theme.Accent;
        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            Font = Theme.UiFontBold;
            Cursor = Cursors.Hand;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BaseColor
        {
            get { return _baseColor; }
            set { _baseColor = value; Invalidate(); }
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _baseColor;
            if (_hover) fill = Theme.Mix(fill, Color.White, 0.14);
            if (_pressed) fill = Theme.Mix(fill, Color.Black, 0.18);
            if (!Enabled) fill = Color.FromArgb(70, 74, 86);
            using (GraphicsPath path = Theme.RoundedRect(rect, _radius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, rect,
                Enabled ? ForeColor : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    }

    /// <summary>工具条上的浅色描边按钮</summary>
    internal class GhostButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private bool _active;

        public GhostButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            Cursor = Cursors.Hand;
            Height = 30;
        }

        /// <summary>被选中（例如正在实时日志中）时高亮</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Active
        {
            get { return _active; }
            set { _active = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = Theme.SurfaceAlt;
            if (_active) fill = Theme.Mix(Theme.Accent, Theme.Bg, 0.55);
            if (_hover) fill = Theme.Mix(fill, Color.White, 0.10);
            if (_pressed) fill = Theme.Mix(fill, Color.Black, 0.15);

            using (GraphicsPath path = Theme.RoundedRect(rect, 6))
            {
                using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillPath(brush, path);
                using (Pen pen = new Pen(_active ? Theme.Accent : Theme.Border, 1f)) e.Graphics.DrawPath(pen, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, rect,
                Enabled ? ForeColor : Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    }

    /// <summary>自绘圆角面板（卡片容器）</summary>
    internal class RoundedPanel : Panel
    {
        private int _radius = 10;
        private Color _borderColor = Theme.Border;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Surface;
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor
        {
            get { return _borderColor; }
            set { _borderColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(rect, _radius))
            {
                using (SolidBrush brush = new SolidBrush(BackColor)) e.Graphics.FillPath(brush, path);
                if (_borderColor.A > 0 && _borderColor != BackColor)
                {
                    using (Pen pen = new Pen(_borderColor, 1.2f)) e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }

    /// <summary>状态圆点</summary>
    internal class StatusDot : Control
    {
        private Color _dotColor = Theme.TextDim;

        public StatusDot()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(14, 14);
            BackColor = Color.Transparent;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color DotColor
        {
            get { return _dotColor; }
            set { _dotColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Bg);
            Rectangle r = new Rectangle(2, 2, Width - 5, Height - 5);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(60, _dotColor)))
                e.Graphics.FillEllipse(glow, new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4));
            using (SolidBrush brush = new SolidBrush(_dotColor))
                e.Graphics.FillEllipse(brush, r);
        }
    }

    /// <summary>带占位提示的输入框</summary>
    internal class CueTextBox : TextBox
    {
        private const int WM_PAINT = 0x000F;
        private string _cue = "";

        public CueTextBox()
        {
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Theme.SurfaceAlt;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
        }

        public string Cue
        {
            get { return _cue; }
            set { _cue = value ?? ""; Invalidate(); }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && !Focused && Text.Length == 0 && _cue.Length > 0 && !DesignMode)
            {
                using (Graphics g = CreateGraphics())
                {
                    TextRenderer.DrawText(g, _cue, Font, ClientRectangle, Theme.TextDim,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }

    /// <summary>深色下拉菜单渲染器</summary>
    internal class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextDim;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(Theme.Border))
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle r = new Rectangle(2, 1, e.Item.Width - 4, e.Item.Height - 2);
            Color fill = e.Item.Selected ? Theme.Mix(Theme.Surface, Color.White, 0.12) : Theme.Surface;
            using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, r);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            // 菜单不使用分隔线：整体统一，仅靠间距区分
            using (SolidBrush brush = new SolidBrush(Theme.Surface))
                e.Graphics.FillRectangle(brush, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
        }
    }

    internal class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color MenuItemSelected { get { return Theme.SurfaceAlt; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Border; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color SeparatorDark { get { return Theme.Surface; } }
        public override Color SeparatorLight { get { return Theme.Surface; } }
    }

    /// <summary>无边框窗口用的右上角控制按钮（最小 / 最大 / 还原 / 关闭）</summary>
    internal class SysButton : Button
    {
        public enum Kind { Minimize, Maximize, Restore, Close }

        private Kind _kind = Kind.Close;
        private bool _hover;
        private bool _pressed;

        public SysButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Theme.Text;
            Font = Theme.UiFont;
            Cursor = Cursors.Hand;
            Size = new Size(36, 28);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Kind Symbol
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color bg = Theme.SurfaceAlt;
            if (_kind == Kind.Close)
            {
                if (_hover) bg = Color.FromArgb(232, 76, 60);
            }
            else if (_hover)
            {
                bg = Theme.Mix(Theme.SurfaceAlt, Color.White, 0.12);
            }
            if (_pressed) bg = Theme.Mix(bg, Color.Black, 0.22);
            using (SolidBrush b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, new Rectangle(0, 0, Width, Height));

            Color c = (_kind == Kind.Close && _hover) ? Color.White : Theme.Text;
            using (Pen pen = new Pen(c, 1.6f))
            {
                int cx = Width / 2, cy = Height / 2;
                if (_kind == Kind.Minimize)
                {
                    e.Graphics.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                }
                else if (_kind == Kind.Maximize)
                {
                    e.Graphics.DrawRectangle(pen, cx - 6, cy - 6, 12, 12);
                }
                else if (_kind == Kind.Restore)
                {
                    e.Graphics.DrawRectangle(pen, cx - 6, cy - 2, 9, 9);
                    e.Graphics.DrawLine(pen, cx - 6, cy - 2, cx - 6, cy - 6);
                    e.Graphics.DrawLine(pen, cx - 6, cy - 6, cx + 2, cy - 6);
                    e.Graphics.DrawLine(pen, cx + 2, cy - 6, cx + 2, cy + 2);
                    e.Graphics.DrawLine(pen, cx + 2, cy + 2, cx + 3, cy + 2);
                }
                else // Close
                {
                    e.Graphics.DrawLine(pen, cx - 6, cy - 6, cx + 6, cy + 6);
                    e.Graphics.DrawLine(pen, cx + 6, cy - 6, cx - 6, cy + 6);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    }

    /// <summary>运行时绘制的小图标（窗口 / 托盘用），与 build.ps1 里的 exe 图标同款</summary>
    internal static class IconFactory
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon Create(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                float k = size / 64f;
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (GraphicsPath bg = Theme.RoundedRect(new Rectangle(2, 2, size - 4, size - 4), (int)(14 * k)))
                    {
                        using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 30, 33, 41))) g.FillPath(brush, bg);
                        using (Pen pen = new Pen(Theme.Accent, Math.Max(1.5f, 2.5f * k))) g.DrawPath(pen, bg);
                    }
                    using (GraphicsPath phone = Theme.RoundedRect(new Rectangle((int)(22 * k), (int)(11 * k), (int)(20 * k), (int)(42 * k)), (int)(5 * k)))
                    using (SolidBrush brush = new SolidBrush(Theme.Text)) g.FillPath(brush, phone);
                    using (GraphicsPath screen = Theme.RoundedRect(new Rectangle((int)(25 * k), (int)(16 * k), (int)(14 * k), (int)(27 * k)), (int)(2 * k)))
                    using (SolidBrush brush = new SolidBrush(Theme.Accent)) g.FillPath(brush, screen);
                    using (SolidBrush dot = new SolidBrush(Theme.Green))
                        g.FillEllipse(dot, (int)(29 * k), (int)(46 * k), Math.Max(4, (int)(6 * k)), Math.Max(4, (int)(6 * k)));
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(handle))
                        return (Icon)tmp.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }
    }
}
