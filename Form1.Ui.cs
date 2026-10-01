using AxWMPLib;
using BaiduAI.Common;
using BaiduAI.Ui;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace BaiduAI
{
    /// <summary>
    /// 窗体界面构建（全部由代码创建，不使用 Windows 窗体设计器）。
    ///
    /// 视觉规范来自 DSH 设计系统：暗色基座、卡片采用 radius-xl(20px) + 1px 描边、
    /// 按钮 radius-md(12px) 且 md 高 36px、输入框高 32px、字体 Microsoft YaHei UI 14px。
    /// </summary>
    public partial class Form1
    {
        // ---------------- 设计器替代：容器与资源 ----------------
        private IContainer components = null;

        // ---------------- 自定义窗口层 ----------------
        private DshTitleBar titleBar;
        private DshPill pillCredential;
        private DshButton btnTheme;
        private Label lblStatus;

        // ---------------- 图片检测卡片 ----------------
        private DshCard cardImage;
        private DshButton btnPickImage;
        private DshButton btnCompare;
        private DshTextField txtFaceA;
        private DshTextField txtFaceB;

        // ---------------- 摄像头预览卡片 ----------------
        private DshCard cardPreview;
        private AForge.Controls.VideoSourcePlayer videoSourcePlayer1;
        private ComboBox comboBox1;
        private DshButton btnConnect;
        private DshButton btnRefreshDevices;
        private DshButton btnCapture;
        private DshButton btnStop;

        // ---------------- 人脸库卡片 ----------------
        private DshCard cardFaceDb;
        private DshTextField txtGroupId;
        private DshTextField txtUserName;
        private DshButton btnRegister;
        private DshButton btnLogin;
        private Label lblGroupHint;

        // ---------------- 识别结果卡片 ----------------
        private DshCard cardResult;
        private DshPill pillAge;
        private DshTextField txtUserId;
        private DshTextField txtQuality;
        private AxWindowsMediaPlayer axWindowsMediaPlayer1;

        // ---------------- 原始返回卡片 ----------------
        private DshCard cardRaw;
        private DshTextField txtRawResult;

        /// <summary>界面构建入口，替代设计器生成的 InitializeComponent。</summary>
        private void InitializeComponent()
        {
            SuspendLayout();

            // ========== 窗体本体 ==========
            Text = "BaiduAI · 人脸识别工作台";
            ClientSize = new Size(1436, 800);
            MinimumSize = new Size(1160, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = DshControls.Theme.BgBase;
            ForeColor = DshControls.Theme.LabelPrimary;
            Font = DshTheme.Ui(DshTheme.FontSizeBase);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            DoubleBuffered = true;

            BuildTitleBar();
            BuildImageCard();
            BuildPreviewCard();
            BuildFaceDbCard();
            BuildResultCard();
            BuildRawCard();
            BuildStatusBar();
            BuildWmp();

            Load += Form1_Load;
            FormClosed += Form1_FormClosed;

            ResumeLayout(false);
        }

        // ==================================================================
        //  标题栏
        // ==================================================================

        private void BuildTitleBar()
        {
            titleBar = new DshTitleBar
            {
                Dock = DockStyle.Top,
                TitleText = "BaiduAI",
                SubtitleText = "百度人脸识别 · 摄像头实时检测"
            };

            pillCredential = DshControls.Pill("未配置密钥");
            pillCredential.Top = (titleBar.Height - pillCredential.Height) / 2;
            pillCredential.Left = titleBar.Width - 300;
            pillCredential.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            btnTheme = DshControls.Button("切换主题", 96, 28, DshButtonKind.Outline);
            btnTheme.FontSize = 12f;
            btnTheme.Top = (titleBar.Height - btnTheme.Height) / 2;
            btnTheme.Left = titleBar.Width - 180;
            btnTheme.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnTheme.Click += btnTheme_Click;

            titleBar.Controls.Add(pillCredential);
            titleBar.Controls.Add(btnTheme);

            // Dock 顺序：先加底部状态栏、再加标题栏，最后加填充区，保证层叠正确
            Controls.Add(titleBar);
        }

        // ==================================================================
        //  图片检测卡片（左栏）
        // ==================================================================

        private void BuildImageCard()
        {
            cardImage = DshControls.Card("图片检测", 16, 68, 380, 226);
            cardImage.ShowHeader = true;

            const int pad = DshTheme.CardPadding;
            int y = DshCard.HeaderHeight + 12;
            int w = cardImage.Width - pad * 2;

            btnPickImage = DshControls.ButtonMd("选择人脸图片", 150, DshButtonKind.Outline);
            btnPickImage.Location = new Point(pad, y);
            btnPickImage.Click += btnPickImage_Click;

            btnCompare = DshControls.ButtonMd("开始对比", 150, DshButtonKind.Primary);
            btnCompare.Location = new Point(pad + 150 + 10, y);
            btnCompare.Click += btnCompare_Click;

            y += DshTheme.ControlHeight + 14;
            txtFaceA = DshControls.Field(w, DshTheme.InputHeight, readOnly: true);
            txtFaceA.Placeholder = "图片 A：尚未选择";
            txtFaceA.Location = new Point(pad, y);

            y += DshTheme.InputHeight + 8;
            txtFaceB = DshControls.Field(w, DshTheme.InputHeight, readOnly: true);
            txtFaceB.Placeholder = "图片 B：尚未选择";
            txtFaceB.Location = new Point(pad, y);

            cardImage.Controls.Add(btnPickImage);
            cardImage.Controls.Add(btnCompare);
            cardImage.Controls.Add(txtFaceA);
            cardImage.Controls.Add(txtFaceB);
            Controls.Add(cardImage);
        }

        // ==================================================================
        //  摄像头预览卡片（中栏）
        // ==================================================================

        private void BuildPreviewCard()
        {
            cardPreview = DshControls.Card("摄像头预览", 408, 68, 656, 612);
            cardPreview.StatusText = "未连接";

            const int pad = DshTheme.CardPadding;
            int w = cardPreview.Width - pad * 2;

            int y = DshCard.HeaderHeight + 12;

            // 设备选择 + 操作按钮一排
            comboBox1 = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = DshTheme.Ui(DshTheme.FontSizeBase),
                BackColor = DshControls.Theme.BgInput,
                ForeColor = DshControls.Theme.LabelPrimary,
                Location = new Point(pad, y),
                Width = 176,
                Height = DshTheme.InputHeight
            };

            btnConnect = DshControls.ButtonMd("连接", 72, DshButtonKind.Primary);
            btnConnect.Location = new Point(pad + 184, y);
            btnConnect.Click += btnConnect_Click;

            btnRefreshDevices = DshControls.ButtonMd("重新检测", 88, DshButtonKind.Outline);
            btnRefreshDevices.Location = new Point(pad + 262, y);
            btnRefreshDevices.Click += btnRefreshDevices_Click;

            // 右侧两个主操作按钮
            btnStop = DshControls.ButtonMd("停止", 72, DshButtonKind.Ghost);
            btnStop.Location = new Point(pad + w - 156, y);
            btnStop.Click += btnStop_Click;

            btnCapture = DshControls.ButtonMd("拍照", 74, DshButtonKind.Info);
            btnCapture.Location = new Point(pad + w - 74, y);
            btnCapture.Click += btnCapture_Click;

            // 视频预览区：深色衬底 + radius-lg
            y += DshTheme.ControlHeight + 13;
            int videoH = cardPreview.Height - y - pad;

            videoSourcePlayer1 = new AForge.Controls.VideoSourcePlayer
            {
                Location = new Point(pad, y),
                Size = new Size(w, videoH),
                BackColor = DshControls.Theme.BgMedia,
                ForeColor = DshControls.Theme.LabelTertiary,
                BorderColor = DshControls.Theme.BorderL4,
                AutoSizeControl = false
            };

            cardPreview.Controls.Add(comboBox1);
            cardPreview.Controls.Add(btnConnect);
            cardPreview.Controls.Add(btnRefreshDevices);
            cardPreview.Controls.Add(btnStop);
            cardPreview.Controls.Add(btnCapture);
            cardPreview.Controls.Add(videoSourcePlayer1);
            Controls.Add(cardPreview);
        }

        // ==================================================================
        //  人脸库卡片（右栏）
        // ==================================================================

        private void BuildFaceDbCard()
        {
            cardFaceDb = DshControls.Card("人脸库", 1076, 68, 344, 276);

            const int pad = DshTheme.CardPadding;
            int y = DshCard.HeaderHeight + 12;
            int w = cardFaceDb.Width - pad * 2;

            var lblGroup = DshControls.Label("用户分组 ID", DshControls.Theme.FontSmall,
                DshControls.Theme.LabelSecondary, w, 18);
            lblGroup.Location = new Point(pad, y);

            y += 20;
            txtGroupId = DshControls.Field(w, DshTheme.InputHeight);
            txtGroupId.Placeholder = "例如：students";
            txtGroupId.Location = new Point(pad, y);

            y += DshTheme.InputHeight + 14;
            var lblName = DshControls.Label("用户姓名", DshControls.Theme.FontSmall,
                DshControls.Theme.LabelSecondary, w, 18);
            lblName.Location = new Point(pad, y);

            y += 20;
            txtUserName = DshControls.Field(w, DshTheme.InputHeight);
            txtUserName.Placeholder = "例如：张三";
            txtUserName.Location = new Point(pad, y);

            y += DshTheme.InputHeight + 16;
            int half = (w - 10) / 2;

            btnRegister = DshControls.ButtonMd("人脸注册", half, DshButtonKind.Outline);
            btnRegister.Location = new Point(pad, y);
            btnRegister.Click += btnRegister_Click;

            btnLogin = DshControls.ButtonMd("人脸登录", half, DshButtonKind.Primary);
            btnLogin.Location = new Point(pad + half + 10, y);
            btnLogin.Click += btnLogin_Click;

            y += DshTheme.ControlHeight + 8;
            lblGroupHint = DshControls.Label("登录时需填写与注册一致的「用户分组 ID」",
                DshControls.Theme.FontTiny, DshControls.Theme.LabelCaption, w, 16);
            lblGroupHint.Location = new Point(pad, y);

            cardFaceDb.Controls.Add(lblGroup);
            cardFaceDb.Controls.Add(txtGroupId);
            cardFaceDb.Controls.Add(lblName);
            cardFaceDb.Controls.Add(txtUserName);
            cardFaceDb.Controls.Add(btnRegister);
            cardFaceDb.Controls.Add(btnLogin);
            cardFaceDb.Controls.Add(lblGroupHint);
            Controls.Add(cardFaceDb);
        }

        // ==================================================================
        //  识别结果卡片（右栏）
        // ==================================================================

        private void BuildResultCard()
        {
            cardResult = DshControls.Card("识别结果", 1076, 356, 344, 200);

            const int pad = DshTheme.CardPadding;
            int y = DshCard.HeaderHeight + 10;
            int w = cardResult.Width - pad * 2;

            var lblAge = DshControls.Label("年龄", DshControls.Theme.FontSmall,
                DshControls.Theme.LabelSecondary, 40, 24);
            lblAge.Location = new Point(pad, y);

            pillAge = DshControls.Pill("--", DshPillKind.Business);
            pillAge.Location = new Point(pad + 42, y);

            y += 30;
            var lblUser = DshControls.Label("当前用户", DshControls.Theme.FontSmall,
                DshControls.Theme.LabelSecondary, w, 18);
            lblUser.Location = new Point(pad, y);

            y += 20;
            txtUserId = DshControls.Field(w, DshTheme.InputHeight, readOnly: true);
            txtUserId.Placeholder = "尚未识别";
            txtUserId.Location = new Point(pad, y);

            y += DshTheme.InputHeight + 12;
            var lblQuality = DshControls.Label("人脸质量", DshControls.Theme.FontSmall,
                DshControls.Theme.LabelSecondary, w, 18);
            lblQuality.Location = new Point(pad, y);

            y += 20;
            txtQuality = DshControls.Field(w, cardResult.Height - y - pad - 4,
                readOnly: true, multiline: true);
            txtQuality.Location = new Point(pad, y);

            cardResult.Controls.Add(lblAge);
            cardResult.Controls.Add(pillAge);
            cardResult.Controls.Add(lblUser);
            cardResult.Controls.Add(txtUserId);
            cardResult.Controls.Add(lblQuality);
            cardResult.Controls.Add(txtQuality);
            Controls.Add(cardResult);
        }

        // ==================================================================
        //  原始返回卡片（左栏下方，横跨到中栏）
        // ==================================================================

        private void BuildRawCard()
        {
            cardRaw = DshControls.Card("接口原始返回", 16, 306, 1048 - 0, 374);
            cardRaw.StatusText = "JSON";

            const int pad = DshTheme.CardPadding;
            int y = DshCard.HeaderHeight + 10;

            txtRawResult = DshControls.Field(cardRaw.Width - pad * 2, cardRaw.Height - y - pad,
                readOnly: true, multiline: true);
            txtRawResult.Location = new Point(pad, y);
            txtRawResult.Inner.Font = DshTheme.Mono(12f);
            txtRawResult.Inner.ScrollBars = ScrollBars.Both;
            txtRawResult.Inner.WordWrap = false;

            cardRaw.Controls.Add(txtRawResult);
            Controls.Add(cardRaw);
        }

        // ==================================================================
        //  底部状态栏
        // ==================================================================

        private void BuildStatusBar()
        {
            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = DshControls.Theme.BgLayer2
            };

            lblStatus = DshControls.Label("就绪", DshControls.Theme.FontCaption,
                DshControls.Theme.LabelTertiary, 1200, 32);
            lblStatus.Location = new Point(16, 0);
            lblStatus.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            var right = DshControls.Label(".NET Framework 4.8 · AForge · Baidu AIP SDK",
                DshControls.Theme.FontTiny, DshControls.Theme.LabelCaption, 320, 32);
            right.Location = new Point(bar.Width - 336, 0);
            right.TextAlign = ContentAlignment.MiddleRight;
            right.Anchor = AnchorStyles.Right | AnchorStyles.Top;

            bar.Controls.Add(lblStatus);
            bar.Controls.Add(right);
            Controls.Add(bar);
        }

        // ==================================================================
        //  欢迎语音播放器（不可见的 COM 组件，仅用于播放 mp3）
        // ==================================================================

        /// <summary>
        /// 尝试从嵌入资源装载 Windows Media Player 的 ActiveX 设计期状态。
        ///
        /// 必要性：AxHost 必须持有 OcxState 才能访问 ActiveX 属性，否则访问 uiMode
        /// 会抛 InvalidActiveXStateException。这与 Windows 窗体设计器生成代码里
        /// resources.GetObject("axWindowsMediaPlayer1.OcxState") 的做法一致，
        /// 只是控件改为代码创建，资源仍从 Form1.resx 读取。
        /// </summary>
        /// <returns>成功装载返回 true；资源缺失或类型不符时记录日志并返回 false。</returns>
        private static bool TryApplyWmpOcxState(AxWindowsMediaPlayer player)
        {
            try
            {
                var manager = new ComponentResourceManager(typeof(Form1));
                object raw = manager.GetObject("axWindowsMediaPlayer1.OcxState");

                if (raw == null)
                {
                    ClassLoger.Fail("TryApplyWmpOcxState", "Form1.resx 中不存在 axWindowsMediaPlayer1.OcxState 资源");
                    return false;
                }

                var state = raw as System.Windows.Forms.AxHost.State;
                if (state == null)
                {
                    ClassLoger.Fail("TryApplyWmpOcxState", "OcxState 资源类型不符：" + raw.GetType().FullName);
                    return false;
                }

                player.OcxState = state;
                return true;
            }
            catch (Exception ex)
            {
                ClassLoger.Error("TryApplyWmpOcxState", ex);
                return false;
            }
        }

        private void BuildWmp()
        {
            var player = new AxWindowsMediaPlayer();
            ((ISupportInitialize)player).BeginInit();

            // 顺序与设计器生成代码一致：BeginInit → 普通属性 → OcxState → EndInit → ActiveX 属性
            player.Enabled = true;
            player.Visible = false;
            player.Left = 0;
            player.Top = 0;
            player.Width = 1;
            player.Height = 1;

            bool stateLoaded = TryApplyWmpOcxState(player);

            ((ISupportInitialize)player).EndInit();

            // uiMode 是 ActiveX 属性：只有成功装载 OcxState 之后访问才安全，
            // 否则会抛 InvalidActiveXStateException。拿不到状态就宁可不设置，
            // 播放器仍可正常播放 mp3，只是会显示自身界面（反正它是 1x1 且不可见）。
            if (stateLoaded)
            {
                try
                {
                    player.uiMode = "Invisible";
                }
                catch (Exception ex)
                {
                    ClassLoger.Error("BuildWmp/uiMode", ex);
                }
            }
            else
            {
                ClassLoger.Fail("BuildWmp", "未取到 Windows Media Player 的 OcxState，已跳过 uiMode 设置");
            }

            Controls.Add(player);
            axWindowsMediaPlayer1 = player;
        }

        // ==================================================================
        //  主题
        // ==================================================================

        private void btnTheme_Click(object sender, EventArgs e)
        {
            _darkTheme = !_darkTheme;
            ApplyTheme();
        }

        /// <summary>
        /// 把当前主题套用到所有自绘控件与原生控件上。
        /// </summary>
        private void ApplyTheme()
        {
            DshControls.Theme = _darkTheme ? DshTheme.Dark() : DshTheme.Light();
            var theme = DshControls.Theme;

            BackColor = theme.BgBase;
            ForeColor = theme.LabelPrimary;

            // 自绘控件
            if (titleBar != null) titleBar.ApplyTheme(theme);
            foreach (Control c in Controls)
            {
                var themed = c as IDshThemed;
                if (themed != null) themed.ApplyTheme(theme);

                var card = c as DshCard;
                if (card != null) card.ApplyTheme(theme);
            }

            // 药丸与状态文字
            if (pillAge != null)
            {
                pillAge.Kind = DshPillKind.Business;
                pillAge.PillText = pillAge.PillText;   // 触发按当前文字重新计算宽度
                pillAge.Invalidate();
            }

            UpdateCredentialPill();

            // 原生控件：ComboBox 与预览控件无法自绘，只能设置配色
            if (comboBox1 != null)
            {
                comboBox1.BackColor = theme.BgInput;
                comboBox1.ForeColor = theme.LabelPrimary;
            }
            if (lblStatus != null) lblStatus.ForeColor = theme.LabelTertiary;
            if (videoSourcePlayer1 != null)
            {
                videoSourcePlayer1.BackColor = theme.BgMedia;
                videoSourcePlayer1.ForeColor = theme.LabelTertiary;
                videoSourcePlayer1.BorderColor = theme.BorderL4;
            }

            if (txtRawResult != null) txtRawResult.ApplyTheme(theme);

            // 滚动条跟随暗色主题
            DshControls.ApplyScrollBarThemeRecursive(this, theme.IsDark);

            if (btnTheme != null) btnTheme.ButtonText = _darkTheme ? "切换亮色" : "切换暗色";

            Invalidate(true);
        }

        /// <summary>更新标题栏上的密钥配置状态标记。</summary>
        private void UpdateCredentialPill()
        {
            if (pillCredential == null) return;

            bool configured = CredentialsConfigured();
            pillCredential.PillText = configured ? "密钥已配置" : "未配置密钥";
            pillCredential.Kind = configured ? DshPillKind.Success : DshPillKind.Warn;
            pillCredential.Invalidate();
        }

        /// <summary>底部状态栏提示。</summary>
        private void SetStatus(string text, bool error = false, bool success = false)
        {
            if (lblStatus == null) return;
            lblStatus.Text = text;
            var theme = DshControls.Theme;
            lblStatus.ForeColor = error ? theme.StateError
                              : success ? theme.StateSuccess
                              : theme.LabelTertiary;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
