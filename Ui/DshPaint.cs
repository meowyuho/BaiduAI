using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace BaiduAI.Ui
{
    /// <summary>
    /// GDI+ 绘制辅助：DSH 的所有圆角都是超椭圆（corner-shape: superellipse），
    /// WinForms 无法实现超椭圆，这里用普通圆角近似。
    /// </summary>
    internal static class DshPaint
    {
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) { path.AddRectangle(r); return path; }

            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>以清晰抗锯齿方式绘制圆角描边（内缩半个像素，避免边缘发虚）。</summary>
        public static void DrawRoundedBorder(Graphics g, Rectangle r, int radius, Color color, float width = 1f)
        {
            var rect = new RectangleF(r.X + width / 2f, r.Y + width / 2f,
                                      r.Width - width, r.Height - width);
            using (var path = RoundedRect(Rectangle.Round(rect), radius))
            using (var pen = new Pen(color, width))
            {
                pen.Alignment = PenAlignment.Center;
                g.DrawPath(pen, path);
            }
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color color)
        {
            using (var path = RoundedRect(r, radius))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        /// <summary>按 DSH 的默认文本渲染参数绘制居中/左对齐文字。</summary>
        public static readonly TextFormatFlags TextFlags =
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;

        public static readonly TextFormatFlags TextFlagsLeft =
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine |
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter;

        public static readonly TextFormatFlags TextFlagsCenter =
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine |
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
    }
}
