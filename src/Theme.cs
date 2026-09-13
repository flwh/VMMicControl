using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VMMicControl
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(23, 25, 31);
        public static readonly Color Surface = Color.FromArgb(33, 36, 44);
        public static readonly Color SurfaceAlt = Color.FromArgb(28, 31, 38);
        public static readonly Color Border = Color.FromArgb(52, 57, 70);
        public static readonly Color Text = Color.FromArgb(233, 237, 244);
        public static readonly Color TextDim = Color.FromArgb(142, 150, 167);
        public static readonly Color On = Color.FromArgb(46, 204, 113);
        public static readonly Color Off = Color.FromArgb(231, 76, 60);
        public static readonly Color Accent = Color.FromArgb(90, 150, 255);

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
    }

    /// <summary>自绘圆角按钮</summary>
    internal class RoundedButton : Button
    {
        private int _radius = 6;
        private Color _baseColor = Theme.Accent;
        private Color _hoverColor = Theme.Accent;
        private bool _hover;
        private bool _pressed;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Color.White;
            Font = new Font("Segoe UI Semibold", 9.5f);
            Cursor = Cursors.Hand;
            _hoverColor = Lighten(_baseColor, 0.12);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BaseColor
        {
            get { return _baseColor; }
            set
            {
                _baseColor = value;
                _hoverColor = Lighten(value, 0.12);
                Invalidate();
            }
        }

        public int Radius
        {
            get { return _radius; }
            set { _radius = value; Invalidate(); }
        }

        public static Color Lighten(Color color, double amount)
        {
            int r = (int)Math.Min(255, color.R + (255 - color.R) * amount);
            int g = (int)Math.Min(255, color.G + (255 - color.G) * amount);
            int b = (int)Math.Min(255, color.B + (255 - color.B) * amount);
            return Color.FromArgb(color.A, r, g, b);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundedRect(rect, _radius))
            {
                Color fill = _baseColor;
                if (_hover) fill = _hoverColor;
                if (_pressed) fill = Color.FromArgb(fill.A, (int)(fill.R * 0.85), (int)(fill.G * 0.85), (int)(fill.B * 0.85));
                if (!Enabled) fill = Color.FromArgb(90, 90, 100);
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }
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
                using (SolidBrush brush = new SolidBrush(BackColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
                if (_borderColor.A > 0 && _borderColor != BackColor)
                {
                    using (Pen pen = new Pen(_borderColor, 1.2f))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            }
        }
    }
}
