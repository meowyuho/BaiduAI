using System.Drawing;

namespace BaiduAI.Ui
{
    /// <summary>
    /// DSH 视觉设计令牌（design tokens）。
    ///
    /// 取值来源：DSH 前端打包档案中的设计变量表
    ///   --dsw-alias-* / --dsw-static-* / --dsw-radius-* / --dsw-font-* / --dsw-shadow-* / --dsw-elevation-*
    /// 命名沿用 DSH 的语义（bg-layer / label / border-l / button / state-*），便于与上游对照。
    ///
    /// 说明：WinForms 使用 GDI+ 绘制，无法使用 CSS 的 color-mix() 与 8 位 #RRGGBBAA 半透明叠加，
    /// 因此 DSH 中带透明度的色值在这里折算为等效的实色（折算依据见各字段注释）。
    /// </summary>
    internal sealed class DshTheme
    {
        // ---------------- 标识 ----------------
        public string Name { get; private set; }
        public bool IsDark { get; private set; }

        // ---------------- 背景层级（--dsw-alias-bg-*） ----------------
        /// <summary>窗口最底色。亮 #fff / 暗 #151517</summary>
        public Color BgBase { get; private set; }
        /// <summary>一级表面（卡片）。亮 #fff / 暗 #232324</summary>
        public Color BgLayer1 { get; private set; }
        /// <summary>二级表面。亮 #fff / 暗 #2c2c2e</summary>
        public Color BgLayer2 { get; private set; }
        /// <summary>三级表面（标题栏等）。亮 #fff / 暗 #353638</summary>
        public Color BgLayer3 { get; private set; }
        /// <summary>侧栏底。亮 #f9fafb / 暗 #1b1b1c</summary>
        public Color BgSidebar { get; private set; }
        /// <summary>模块底（居中列）。亮 #f5f6f7 / 暗 #353638</summary>
        public Color BgModule { get; private set; }
        /// <summary>输入控件底。亮 #fff / 暗 #2c2c2e</summary>
        public Color BgInput { get; private set; }
        /// <summary>用户气泡底。亮 #edf3fe / 暗 #2c2c2e</summary>
        public Color BgBubble { get; private set; }
        /// <summary>视频/预览衬底，两主题一致，取近黑。</summary>
        public Color BgMedia = Color.FromArgb(0x0B, 0x0B, 0x0C);

        // ---------------- 文字（--dsw-alias-label-*） ----------------
        /// <summary>主要文字。亮 #0f1115 / 暗 #f9fafb</summary>
        public Color LabelPrimary { get; private set; }
        /// <summary>次要文字。亮 #61666b / 暗 #cfd3d6</summary>
        public Color LabelSecondary { get; private set; }
        /// <summary>三级文字。亮 #81858c / 暗 #adb2b8</summary>
        public Color LabelTertiary { get; private set; }
        /// <summary>说明文字。亮 #adb2b8 / 暗 #81858c</summary>
        public Color LabelCaption { get; private set; }
        /// <summary>占位/极弱文字。亮 #e1e5ee / 暗 #43454a</summary>
        public Color LabelDimmed { get; private set; }
        /// <summary>反色文字（用在主按钮上）。亮 #fff / 暗 #0f1115</summary>
        public Color LabelPrimaryForeground { get; private set; }

        // ---------------- 边框（--dsw-alias-border-l*） ----------------
        /// <summary>最弱描边。亮 #0000000a / 暗 #ffffff0f</summary>
        public Color BorderL1 { get; private set; }
        /// <summary>弱描边。亮 #0000001a / 暗 #ffffff1f</summary>
        public Color BorderL2 { get; private set; }
        /// <summary>常规描边。亮 #0000001f / 暗 #ffffff29</summary>
        public Color BorderL3 { get; private set; }
        /// <summary>强调描边（输入框）。亮 #00000029 / 暗 #ffffff33</summary>
        public Color BorderL4 { get; private set; }

        // ---------------- 交互填充（--dsw-alias-interactive-*） ----------------
        /// <summary>悬停填充（等效实色）。亮 #2631480f→#eef1f6 / 暗 #ffffff14→#2f2f31</summary>
        public Color InteractiveHover { get; private set; }
        /// <summary>按下填充。亮 #2631481a→#e3e7ee / 暗 #ffffff24→#3d3d40</summary>
        public Color InteractiveActive { get; private set; }

        // ---------------- 按钮（--dsw-alias-button-*） ----------------
        /// <summary>主按钮填充。亮 #0f1115 / 暗 #f9fafb</summary>
        public Color ButtonPrimaryFill { get; private set; }
        /// <summary>主按钮悬停。亮 #43454a / 暗 #ebeef2</summary>
        public Color ButtonPrimaryHover { get; private set; }
        /// <summary>主按钮按下（DSH 未定义 active，此处取 hover 再压暗一档）。</summary>
        public Color ButtonPrimaryPressed { get; private set; }
        /// <summary>次按钮底（描边式）。亮 #fff / 暗 #2c2c2e</summary>
        public Color ButtonOutlineFill { get; private set; }
        /// <summary>信息按钮（发送/强调）。亮 #4176e6 / 暗 #7aaaff</summary>
        public Color ButtonInfoFill { get; private set; }
        public Color ButtonInfoHover { get; private set; }
        /// <summary>禁用态按钮底（等效于 40% 不透明的填充压到背景上）。</summary>
        public Color ButtonDisabledFill { get; private set; }
        public Color ButtonDisabledText { get; private set; }

        // ---------------- 状态色（--dsw-alias-state-*） ----------------
        /// <summary>业务主色 / 聚焦色。亮 #4176e6 / 暗 #7aaaff</summary>
        public Color StateBusiness { get; private set; }
        /// <summary>业务色浅底。亮 #e4edfd / 暗 #34415b</summary>
        public Color StateBusinessTertiary { get; private set; }
        /// <summary>成功（在线指示）。两主题一致 #22c55e</summary>
        public Color StateSuccess = Color.FromArgb(0x22, 0xC5, 0x5E);
        /// <summary>警告。两主题一致 #f59e0b</summary>
        public Color StateWarn = Color.FromArgb(0xF5, 0x9E, 0x0B);
        /// <summary>错误/危险。亮 #ec1313 / 暗 #f25a5a</summary>
        public Color StateError { get; private set; }
        /// <summary>错误浅底。亮 #fee2e2 / 暗 #3c1f1b</summary>
        public Color StateErrorTertiary { get; private set; }

        // ---------------- 滚动条（--dsw-alias-scrollbar-*，宽 5px） ----------------
        /// <summary>滚动条滑块。亮 #e5e5e5 / 暗 #3c3c3d</summary>
        public Color ScrollThumb { get; private set; }
        /// <summary>滚动条滑块悬停。亮 #d4d4d4 / 暗 #545557</summary>
        public Color ScrollThumbHover { get; private set; }
        /// <summary>滚动条宽度（DSH: --dsh-scrollbar-width: 5px）</summary>
        public const int ScrollBarWidth = 5;

        // ---------------- 圆角（--dsw-radius-*） ----------------
        public const int RadiusXs = 4;
        public const int RadiusSm = 8;
        public const int RadiusMd = 12;
        public const int RadiusLg = 16;
        public const int RadiusXl = 20;
        public const int RadiusPanel = 28;

        // ---------------- 尺寸与间距 ----------------
        /// <summary>标准控件高度：DSH Button md = 36px</summary>
        public const int ControlHeight = 36;
        /// <summary>小号控件高度：DSH Button sm = 28px</summary>
        public const int ControlHeightSm = 28;
        /// <summary>输入框高度：DSH Input = 32px</summary>
        public const int InputHeight = 32;
        /// <summary>基础字号：DSH 正文 14px</summary>
        public const float FontSizeBase = 14f;
        /// <summary>卡片内边距</summary>
        public const int CardPadding = 16;
        /// <summary>卡片之间的间距</summary>
        public const int CardGap = 12;

        // ---------------- 字体（--dsw-font-family / --ds-font-family-code） ----------------
        // DSH: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Hiragino Sans GB",
        //      "Microsoft YaHei", "Helvetica Neue", Helvetica, Arial, sans-serif
        // 在 Windows 上取 "Microsoft YaHei UI"（对应"Microsoft YaHei"的 UI 变体）。
        private const string UiFamily = "Microsoft YaHei UI";
        private const string MonoFamily = "Consolas";

        public static Font Ui(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(UiFamily, size, style, GraphicsUnit.Point);
        }

        public static Font Mono(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(MonoFamily, size, style, GraphicsUnit.Point);
        }

        // 常用字号：DSH 阶梯 s-14 / xs-13 / xxs-12 / xxxs-11、m-18、l-20
        public Font FontBase { get { return Ui(14f); } }
        public Font FontBaseStrong { get { return Ui(14f, FontStyle.Bold); } }
        public Font FontSmall { get { return Ui(13f); } }
        public Font FontSmallStrong { get { return Ui(13f, FontStyle.Bold); } }
        public Font FontCaption { get { return Ui(12f); } }
        public Font FontCaptionStrong { get { return Ui(12f, FontStyle.Bold); } }
        public Font FontTiny { get { return Ui(11f); } }
        public Font FontTinyStrong { get { return Ui(11f, FontStyle.Bold); } }
        public Font FontTitle { get { return Ui(16f, FontStyle.Bold); } }
        public Font FontHeading { get { return Ui(18f, FontStyle.Bold); } }
        public Font FontDisplay { get { return Ui(24f, FontStyle.Bold); } }
        public Font FontCode { get { return Mono(12f); } }
        public Font FontCodeSmall { get { return Mono(11f); } }

        // ==================================================================
        //  两套主题
        // ==================================================================

        /// <summary>亮色主题（DSH 亮色基座取值）。</summary>
        public static DshTheme Light()
        {
            var t = new DshTheme
            {
                Name = "亮色",
                IsDark = false,

                BgBase = Hex(0xFFFFFF),
                BgLayer1 = Hex(0xFFFFFF),
                BgLayer2 = Hex(0xFFFFFF),
                BgLayer3 = Hex(0xFFFFFF),
                BgSidebar = Hex(0xF9FAFB),
                BgModule = Hex(0xF5F6F7),
                BgInput = Hex(0xFFFFFF),
                BgBubble = Hex(0xEDF3FE),

                LabelPrimary = Hex(0x0F1115),
                LabelSecondary = Hex(0x61666B),
                LabelTertiary = Hex(0x81858C),
                LabelCaption = Hex(0xADB2B8),
                LabelDimmed = Hex(0xE1E5EE),
                LabelPrimaryForeground = Hex(0xFFFFFF),

                BorderL1 = Hex(0xF2F3F5),   // #0000000a 折算
                BorderL2 = Hex(0xE6E8EB),   // #0000001a 折算
                BorderL3 = Hex(0xDDE0E3),   // #0000001f 折算
                BorderL4 = Hex(0xD3D7DB),   // #00000029 折算

                InteractiveHover = Hex(0xEEF1F6),   // #2631480f 折算
                InteractiveActive = Hex(0xE3E7EE),  // #2631481a 折算

                ButtonPrimaryFill = Hex(0x0F1115),
                ButtonPrimaryHover = Hex(0x43454A),
                ButtonPrimaryPressed = Hex(0x2C2C2E),
                ButtonOutlineFill = Hex(0xFFFFFF),
                ButtonInfoFill = Hex(0x4176E6),
                ButtonInfoHover = Hex(0x7AAAFF),
                ButtonDisabledFill = Hex(0xF0F1F3),
                ButtonDisabledText = Hex(0xADB2B8),

                StateBusiness = Hex(0x4176E6),
                StateBusinessTertiary = Hex(0xE4EDFD),
                StateError = Hex(0xEC1313),
                StateErrorTertiary = Hex(0xFEE2E2),

                ScrollThumb = Hex(0xE5E5E5),
                ScrollThumbHover = Hex(0xD4D4D4)
            };
            return t;
        }

        /// <summary>暗色主题（DSH 暗色取值）。</summary>
        public static DshTheme Dark()
        {
            var t = new DshTheme
            {
                Name = "暗色",
                IsDark = true,

                BgBase = Hex(0x151517),
                BgLayer1 = Hex(0x232324),
                BgLayer2 = Hex(0x2C2C2E),
                BgLayer3 = Hex(0x353638),
                BgSidebar = Hex(0x1B1B1C),
                BgModule = Hex(0x353638),
                BgInput = Hex(0x2C2C2E),
                BgBubble = Hex(0x2C2C2E),

                LabelPrimary = Hex(0xF9FAFB),
                LabelSecondary = Hex(0xCFD3D6),
                LabelTertiary = Hex(0xADB2B8),
                LabelCaption = Hex(0x81858C),
                LabelDimmed = Hex(0x43454A),
                LabelPrimaryForeground = Hex(0x0F1115),

                BorderL1 = Hex(0x262628),   // #ffffff0f 折算
                BorderL2 = Hex(0x303032),   // #ffffff1f 折算
                BorderL3 = Hex(0x3A3B3E),   // #ffffff29 折算
                BorderL4 = Hex(0x454649),   // #ffffff33 折算

                InteractiveHover = Hex(0x2F2F31),   // #ffffff14 折算
                InteractiveActive = Hex(0x3D3D40),  // #ffffff24 折算

                ButtonPrimaryFill = Hex(0xF9FAFB),
                ButtonPrimaryHover = Hex(0xEBEEF2),
                ButtonPrimaryPressed = Hex(0xCFD3D6),
                ButtonOutlineFill = Hex(0x2C2C2E),
                ButtonInfoFill = Hex(0x7AAAFF),
                ButtonInfoHover = Hex(0x4176E6),
                ButtonDisabledFill = Hex(0x242426),
                ButtonDisabledText = Hex(0x545557),

                StateBusiness = Hex(0x7AAAFF),
                StateBusinessTertiary = Hex(0x34415B),
                StateError = Hex(0xF25A5A),
                StateErrorTertiary = Hex(0x3C1F1B),

                ScrollThumb = Hex(0x3C3C3D),
                ScrollThumbHover = Hex(0x545557)
            };
            return t;
        }

        private static Color Hex(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
    }
}
