using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BaiduAI.Ui
{
    /// <summary>
    /// 全局控件工厂与主题应用辅助。
    /// </summary>
    internal static class DshControls
    {
        /// <summary>当前主题（应用级，切换时由窗体统一刷新）。</summary>
        public static DshTheme Theme = DshTheme.Dark();

        // ==================================================================
        //  系统级外观：滚动条主题
        // ==================================================================

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        /// <summary>
        /// 让原生滚动条跟随暗色主题（Windows 10 1809+ 支持 "DarkMode_Explorer"）。
        /// 亮色主题下传 null 恢复默认外观。
        /// </summary>
        public static void ApplyScrollBarTheme(Control control, bool dark)
        {
            if (control == null || control.IsDisposed || !control.IsHandleCreated) return;
            try
            {
                SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : null, null);
            }
            catch
            {
                // 老系统上该主题名不存在，忽略即可，不影响功能
            }
        }

        public static void ApplyScrollBarThemeRecursive(Control root, bool dark)
        {
            if (root == null) return;
            ApplyScrollBarTheme(root, dark);
            foreach (Control c in root.Controls) ApplyScrollBarThemeRecursive(c, dark);
        }

        // ==================================================================
        //  控件工厂
        // ==================================================================

        public static DshButton Button(string text, int width, int height,
                                       DshButtonKind kind = DshButtonKind.Primary)
        {
            return new DshButton
            {
                ButtonText = text,
                Width = width,
                Height = height,
                Kind = kind
            };
        }

        /// <summary>与 DSH Button md 同尺寸（高 36px）的按钮。</summary>
        public static DshButton ButtonMd(string text, int width, DshButtonKind kind = DshButtonKind.Primary)
        {
            return Button(text, width, DshTheme.ControlHeight, kind);
        }

        /// <summary>与 DSH Button sm 同尺寸（高 28px、12px 字号）的按钮。</summary>
        public static DshButton ButtonSm(string text, int width, DshButtonKind kind = DshButtonKind.Outline)
        {
            return Button(text, width, DshTheme.ControlHeightSm, kind);
        }

        public static DshTextField Field(int width, int height, bool readOnly = false, bool multiline = false)
        {
            return new DshTextField
            {
                Width = width,
                Height = height,
                ReadOnlyField = readOnly,
                MultilineField = multiline
            };
        }

        public static Label Label(string text, Font font, Color color, int width, int height)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                AutoSize = false,
                Width = width,
                Height = height,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                UseCompatibleTextRendering = false
            };
        }

        public static DshCard Card(string title, int x, int y, int width, int height, int radius = DshTheme.RadiusXl)
        {
            return new DshCard
            {
                CardTitle = title,
                Location = new Point(x, y),
                Size = new Size(width, height),
                Radius = radius
            };
        }

        public static DshPill Pill(string text, DshPillKind kind = DshPillKind.Neutral)
        {
            return new DshPill { PillText = text, Kind = kind };
        }
    }

    // ======================================================================
    //  卡片：DSH settings-card = 0.5px 描边 + radius-xl(20px)，填色 bg-layer-2
    // ======================================================================

    internal sealed class DshCard : Panel
    {
        private string _title = string.Empty;
        private string _statusText;

        public int Radius { get; set; }
        public string CardTitle { get { return _title; } set { _title = value ?? string.Empty; Invalidate(); } }
        /// <summary>右上角状态文字（为空则不显示）。</summary>
        public string StatusText { get { return _statusText; } set { _statusText = value; Invalidate(); } }
        /// <summary>右上角状态圆点颜色（为空则不显示）。</summary>
        public Color? StatusColor { get; set; }
        /// <summary>标题下方是否需要分隔线。</summary>
        public bool ShowHeader { get; set; }

        /// <summary>标题占据的高度（供外部在标题下摆放控件）。</summary>
        public const int HeaderHeight = 46;

        public DshCard()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            ShowHeader = true;
            Radius = DshTheme.RadiusXl;
        }

        /// <summary>按当前主题刷新配色。</summary>
        public void ApplyTheme(DshTheme theme)
        {
            Invalidate();
            foreach (Control c in Controls)
            {
                var themed = c as IDshThemed;
                if (themed != null) themed.ApplyTheme(theme);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = DshControls.Theme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 卡片本体：bg-layer-2 + border-l4 描边
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            DshPaint.FillRounded(g, rect, Radius, theme.BgLayer2);
            DshPaint.DrawRoundedBorder(g, rect, Radius, theme.BorderL4, 1f);

            if (ShowHeader && !string.IsNullOrEmpty(_title))
            {
                var titleRect = new Rectangle(DshTheme.CardPadding, 0, Width - DshTheme.CardPadding * 2, HeaderHeight);
                TextRenderer.DrawText(g, _title, theme.FontBaseStrong, titleRect,
                    theme.LabelPrimary, DshPaint.TextFlagsLeft);

                // 右上角状态：圆点 + 文字
                if (StatusColor.HasValue || !string.IsNullOrEmpty(_statusText))
                {
                    int right = Width - DshTheme.CardPadding;
                    string text = _statusText ?? string.Empty;
                    var textSize = string.IsNullOrEmpty(text)
                        ? Size.Empty
                        : TextRenderer.MeasureText(g, text, theme.FontCaption, Size.Empty, DshPaint.TextFlags);

                    if (textSize.Width > 0)
                    {
                        var stRect = new Rectangle(right - textSize.Width, 0, textSize.Width, HeaderHeight);
                        TextRenderer.DrawText(g, text, theme.FontCaption, stRect,
                            theme.LabelTertiary, DshPaint.TextFlagsLeft);
                        right -= textSize.Width + 6;
                    }

                    if (StatusColor.HasValue)
                    {
                        using (var brush = new SolidBrush(StatusColor.Value))
                        {
                            g.FillEllipse(brush, right - 7, HeaderHeight / 2 - 3, 7, 7);
                        }
                    }
                }

                // 标题分隔线：border-l1
                using (var pen = new Pen(theme.BorderL1))
                {
                    g.DrawLine(pen, 1, HeaderHeight - 1, Width - 2, HeaderHeight - 1);
                }
            }

            base.OnPaint(e);
        }
    }

    // ======================================================================
    //  按钮：DSH Button —— primary / outline / ghost / info
    // ======================================================================

    internal enum DshButtonKind
    {
        /// <summary>主按钮：填充 button-primary-fill，文字 label-primary-foreground</summary>
        Primary,
        /// <summary>次按钮：0.5px border-l3 描边，透明底</summary>
        Outline,
        /// <summary>幽灵按钮：无描边，悬停出现 interactive-bg-hover</summary>
        Ghost,
        /// <summary>强调按钮：button-info-fill（deepseek 蓝）</summary>
        Info
    }

    internal interface IDshThemed
    {
        void ApplyTheme(DshTheme theme);
    }

    /// <summary>
    /// DSH 自绘控件的公共基类：统一开启双缓冲与自绘，消除闪烁。
    /// （SetStyle 是 Control 的 protected 成员，必须由控件自身调用，故放在构造函数里。）
    /// </summary>
    internal abstract class DshControlBase : Control
    {
        protected DshControlBase(bool selectable)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, selectable);
        }
    }

    internal sealed class DshButton : DshControlBase, IDshThemed
    {
        private string _buttonText = string.Empty;
        private bool _hover;
        private bool _pressed;

        public DshButtonKind Kind { get; set; }
        public int Radius { get; set; }
        /// <summary>字号（DSH: md=14px、sm=12px）。</summary>
        public float FontSize { get; set; }

        public string ButtonText
        {
            get { return _buttonText; }
            set { _buttonText = value ?? string.Empty; Invalidate(); }
        }

        public DshButton() : base(true)
        {
            Kind = DshButtonKind.Primary;
            Radius = DshTheme.RadiusMd;   // DSH Button: radius-md = 12px
            FontSize = DshTheme.FontSizeBase;
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = DshTheme.ControlHeight;
        }

        public void ApplyTheme(DshTheme theme)
        {
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = DshControls.Theme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Kind == DshButtonKind.Primary || Kind == DshButtonKind.Info ? Radius : Radius;

            Color fill;
            Color textColor;
            Color? stroke = null;

            if (!Enabled)
            {
                fill = theme.ButtonDisabledFill;
                textColor = theme.ButtonDisabledText;
                if (Kind == DshButtonKind.Outline) stroke = theme.BorderL2;
            }
            else
            {
                switch (Kind)
                {
                    case DshButtonKind.Primary:
                        fill = _pressed ? theme.ButtonPrimaryPressed
                             : _hover ? theme.ButtonPrimaryHover
                             : theme.ButtonPrimaryFill;
                        textColor = theme.LabelPrimaryForeground;
                        break;

                    case DshButtonKind.Info:
                        fill = _hover ? theme.ButtonInfoHover : theme.ButtonInfoFill;
                        textColor = Color.White;
                        break;

                    case DshButtonKind.Ghost:
                        fill = _pressed ? theme.InteractiveActive
                             : _hover ? theme.InteractiveHover
                             : theme.BgLayer2;   // 与卡片同色，视觉上"透明"
                        textColor = theme.LabelPrimary;
                        break;

                    default: // Outline
                        fill = _pressed ? theme.InteractiveActive
                             : _hover ? theme.InteractiveHover
                             : theme.ButtonOutlineFill;
                        textColor = theme.LabelPrimary;
                        stroke = theme.BorderL3;
                        break;
                }
            }

            DshPaint.FillRounded(g, rect, radius, fill);
            if (stroke.HasValue) DshPaint.DrawRoundedBorder(g, rect, radius, stroke.Value, 1f);

            // 键盘焦点环：DSH --dsw-focus-ring-width: 2px，颜色 state-business-primary
            if (Focused && Enabled)
                DshPaint.DrawRoundedBorder(g, rect, radius, theme.StateBusiness, 2f);

            using (var font = DshTheme.Ui(FontSize, FontStyle.Regular))
            {
                TextRenderer.DrawText(g, _buttonText, font, rect, textColor, DshPaint.TextFlagsCenter);
            }
        }
    }

    // ======================================================================
    //  输入框：DSH Input —— 0.5px border-l4 描边、radius-md、聚焦变 business 蓝
    // ======================================================================

    internal sealed class DshTextField : DshControlBase, IDshThemed
    {
        private readonly TextBox _inner;
        private bool _focused;
        private string _placeholder = string.Empty;

        public int Radius { get; set; }
        /// <summary>只读（不可编辑，文字用次要色）。</summary>
        public bool ReadOnlyField
        {
            get { return _inner.ReadOnly; }
            set { _inner.ReadOnly = value; _inner.BackColor = DshControls.Theme.BgInput; }
        }
        public bool MultilineField
        {
            get { return _inner.Multiline; }
            set { _inner.Multiline = value; LayoutInner(); }
        }
        public string Placeholder
        {
            get { return _placeholder; }
            set { _placeholder = value ?? string.Empty; _inner.Invalidate(); }
        }

        /// <summary>内部文本框，便于挂事件。</summary>
        public TextBox Inner { get { return _inner; } }

        public override string Text
        {
            get { return _inner.Text; }
            set { _inner.Text = value; }
        }

        public DshTextField() : base(true)
        {
            Radius = DshTheme.RadiusMd;

            _inner = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = DshTheme.Ui(DshTheme.FontSizeBase),
                BackColor = DshControls.Theme.BgInput,
                ForeColor = DshControls.Theme.LabelPrimary,
                ShortcutsEnabled = true
            };
            _inner.GotFocus += (s, e) => { _focused = true; Invalidate(); };
            _inner.LostFocus += (s, e) => { _focused = false; Invalidate(); };
            _inner.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);
            Controls.Add(_inner);
        }

        /// <summary>把外部对 Inner 的点击转成对内部文本框的聚焦。</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            _inner.Focus();
            base.OnMouseDown(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutInner();
        }

        private void LayoutInner()
        {
            const int padX = 10;
            int padY = _inner.Multiline ? 8 : Math.Max(0, (Height - _inner.PreferredHeight) / 2);
            _inner.SetBounds(padX, padY, Math.Max(10, Width - padX * 2),
                             _inner.Multiline ? Math.Max(10, Height - padY * 2) : _inner.PreferredHeight);
            if (_inner.Multiline) _inner.ScrollBars = ScrollBars.Vertical;
        }

        public void ApplyTheme(DshTheme theme)
        {
            _inner.BackColor = ReadOnlyField ? theme.BgLayer3 : theme.BgInput;
            _inner.ForeColor = theme.LabelPrimary;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = DshControls.Theme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            DshPaint.FillRounded(g, rect, Radius, ReadOnlyField ? theme.BgLayer3 : theme.BgInput);

            // 聚焦时描边切到 state-business-primary（DSH Input :focus-within）
            var border = _focused && !ReadOnlyField ? theme.StateBusiness : theme.BorderL4;
            DshPaint.DrawRoundedBorder(g, rect, Radius, border, _focused && !ReadOnlyField ? 2f : 1f);

            // 占位提示：仅在无内容且未聚焦时显示（DSH placeholder 用 label-dimmed）
            if (string.IsNullOrEmpty(_inner.Text) && !_focused && !string.IsNullOrEmpty(_placeholder)
                && !_inner.Multiline)
            {
                var pr = new Rectangle(11, 0, Width - 22, Height);
                TextRenderer.DrawText(g, _placeholder, _inner.Font, pr, theme.LabelDimmed, DshPaint.TextFlagsLeft);
            }

            base.OnPaint(e);
        }
    }

    // ======================================================================
    //  胶囊标签：DSH Pill —— radius 999px、height 24px、12px/18px
    // ======================================================================

    internal enum DshPillKind { Neutral, Business, Success, Warn, Error }

    internal sealed class DshPill : DshControlBase, IDshThemed
    {
        private string _text = string.Empty;

        public DshPillKind Kind { get; set; }

        public string PillText
        {
            get { return _text; }
            set
            {
                _text = value ?? string.Empty;

                // 用静态 TextRenderer.MeasureText：不依赖控件句柄，
                // 因此在控件构造阶段、还没显示之前也能正确算出宽度。
                // 注意必须带 TextFormatFlags.NoPadding，否则与 OnPaint 的绘制宽度不一致。
                using (var font = DshTheme.Ui(12f))
                {
                    int w = TextRenderer.MeasureText(_text, font, Size.Empty, DshPaint.TextFlags).Width;
                    Width = w + 18;   // 左右各 9px 内边距
                }
                Invalidate();
            }
        }

        public DshPill() : base(false)
        {
            Kind = DshPillKind.Neutral;
            Height = 24;
            Width = 60;
        }

        public void ApplyTheme(DshTheme theme) { Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = DshControls.Theme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color fill, text;
            switch (Kind)
            {
                case DshPillKind.Business:
                    fill = theme.StateBusinessTertiary; text = theme.StateBusiness; break;
                case DshPillKind.Success:
                    fill = theme.IsDark ? Color.FromArgb(0x23, 0x3C, 0x2C) : Color.FromArgb(0xE6, 0xFA, 0xED);
                    text = theme.StateSuccess; break;
                case DshPillKind.Warn:
                    fill = theme.IsDark ? Color.FromArgb(0x27, 0x24, 0x1F) : Color.FromArgb(0xFE, 0xF5, 0xE7);
                    text = theme.StateWarn; break;
                case DshPillKind.Error:
                    fill = theme.StateErrorTertiary; text = theme.StateError; break;
                default:
                    fill = theme.BgLayer3; text = theme.LabelSecondary; break;
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            DshPaint.FillRounded(g, rect, Height / 2, fill);
            if (Kind == DshPillKind.Neutral) DshPaint.DrawRoundedBorder(g, rect, Height / 2, theme.BorderL3, 1f);

            using (var font = DshTheme.Ui(12f))
            {
                TextRenderer.DrawText(g, _text, font, rect, text, DshPaint.TextFlagsCenter);
            }
        }
    }

    // ======================================================================
    //  标题栏：自绘深色条 + 可拖动 + 最小化/最大化/关闭
    // ======================================================================

    internal sealed class DshTitleBar : DshControlBase, IDshThemed
    {
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary>标题文字。</summary>
        public string TitleText { get; set; }
        /// <summary>副标题（弱化显示）。</summary>
        public string SubtitleText { get; set; }

        public bool ShowMinimize { get; set; }
        public bool ShowMaximize { get; set; }
        public bool ShowClose { get; set; }

        private int _hoverButton = -1;   // 0 最小化 1 最大化 2 关闭

        private const int ButtonWidth = 44;

        public DshTitleBar() : base(false)
        {
            ShowMinimize = true;
            ShowMaximize = true;
            ShowClose = true;
            Height = 56;
            BackColor = DshControls.Theme.BgLayer3;
        }

        public void ApplyTheme(DshTheme theme)
        {
            BackColor = theme.BgLayer3;
            Invalidate();
        }

        private Rectangle ButtonRect(int index)
        {
            return new Rectangle(Width - ButtonWidth * (3 - index), 0, ButtonWidth, Height);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < 3; i++)
            {
                bool visible = i == 0 ? ShowMinimize : i == 1 ? ShowMaximize : ShowClose;
                if (visible && ButtonRect(i).Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitTest(e.Location);
            if (h != _hoverButton) { _hoverButton = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverButton = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var form = FindForm();
                if (form != null && form.WindowState != FormWindowState.Maximized)
                {
                    ReleaseCapture();
                    SendMessage(form.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            var form = FindForm();
            if (form != null && e.Button == MouseButtons.Left)
            {
                switch (HitTest(e.Location))
                {
                    case 0: form.WindowState = FormWindowState.Minimized; break;
                    case 1:
                        form.WindowState = form.WindowState == FormWindowState.Maximized
                            ? FormWindowState.Normal : FormWindowState.Maximized;
                        break;
                    case 2: form.Close(); break;
                }
            }
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var theme = DshControls.Theme;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var brush = new SolidBrush(theme.BgLayer3))
                g.FillRectangle(brush, ClientRectangle);

            // 底部 1px 分隔：border-l1
            using (var pen = new Pen(theme.BorderL1))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            // 左侧品牌标记：一个 deepseek 蓝的圆角方块
            var markRect = new Rectangle(20, Height / 2 - 8, 16, 16);
            DshPaint.FillRounded(g, markRect, 5, theme.ButtonPrimaryFill);
            using (var brush = new SolidBrush(theme.LabelPrimaryForeground))
            {
                // 方块内画一个小圆点，作为极简品牌符号
                g.FillEllipse(brush, markRect.X + 5, markRect.Y + 5, 6, 6);
            }

            // 标题 + 副标题
            int textLeft = markRect.Right + 10;
            int textWidth = Math.Max(10, Width - textLeft - ButtonWidth * 3 - 12);

            if (!string.IsNullOrEmpty(TitleText))
            {
                var tRect = string.IsNullOrEmpty(SubtitleText)
                    ? new Rectangle(textLeft, 0, textWidth, Height)
                    : new Rectangle(textLeft, Height / 2 - 20, textWidth, 20);
                TextRenderer.DrawText(g, TitleText, theme.FontBaseStrong, tRect,
                    theme.LabelPrimary, DshPaint.TextFlagsLeft);
            }
            if (!string.IsNullOrEmpty(SubtitleText))
            {
                var sRect = new Rectangle(textLeft, Height / 2 + 1, textWidth, 18);
                TextRenderer.DrawText(g, SubtitleText, theme.FontTiny, sRect,
                    theme.LabelTertiary, DshPaint.TextFlagsLeft);
            }

            // 右上角按钮
            for (int i = 0; i < 3; i++)
            {
                bool visible = i == 0 ? ShowMinimize : i == 1 ? ShowMaximize : ShowClose;
                if (!visible) continue;

                var r = ButtonRect(i);
                if (_hoverButton == i)
                {
                    // 关闭按钮悬停用错误色，其余用常规悬停填充
                    var fill = i == 2 ? theme.StateError : theme.InteractiveHover;
                    using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, r);
                }

                var glyphColor = (_hoverButton == i && i == 2) ? Color.White : theme.LabelSecondary;
                using (var pen = new Pen(glyphColor, 1.4f))
                {
                    int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                    const int s = 5;
                    if (i == 0) g.DrawLine(pen, cx - s, cy, cx + s, cy);
                    else if (i == 1) g.DrawRectangle(pen, cx - s, cy - s, s * 2, s * 2);
                    else
                    {
                        g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
                        g.DrawLine(pen, cx - s, cy + s, cx + s, cy - s);
                    }
                }
            }
        }
    }
}
