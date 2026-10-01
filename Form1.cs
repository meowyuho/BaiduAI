using AForge.Video;
using AForge.Video.DirectShow;
using Baidu.Aip.Face;
using BaiduAI.Common;
using BaiduAI.Ui;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace BaiduAI
{
    public partial class Form1 : Form
    {
        // ==================== 认证信息 ====================
        // 密钥统一存放在 app.config 的 appSettings 中，避免硬编码进源码。
        private readonly string API_KEY;
        private readonly string SECRET_KEY;

        // ==================== 主题 ====================
        /// <summary>当前是否为暗色主题（DSH 默认暗色）。</summary>
        private bool _darkTheme = true;

        // ==================== 运行状态 ====================
        private Face client = null;                             // 百度人脸识别客户端
        private FilterInfoCollection videoDevices = null;       // 视频设备集合
        private VideoCaptureDevice videoSource;                 // 视频捕获设备
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>实时人脸框位置（相对于预览帧的像素坐标），由后台检测线程写入</summary>
        private volatile FaceLocation location = null;

        /// <summary>人脸框的显示有效期（毫秒）；超出后不再绘制，避免残留旧框</summary>
        private const int FaceBoxTtlMs = 3000;

        /// <summary>人脸框最后更新时刻（Environment.TickCount）</summary>
        private int _locationStamp = 0;

        /// <summary>绘制人脸框用的画笔；整个生命周期复用，避免每帧新建导致 GDI 句柄泄漏</summary>
        private readonly Pen _facePen = new Pen(Color.FromArgb(0x22, 0xC5, 0x5E), 2f);

        // ==================== 实时检测节流 ====================
        /// <summary>两次人脸检测之间的最小间隔（毫秒），防止把摄像头每一帧都送去调用接口</summary>
        private const int DetectIntervalMs = 700;

        /// <summary>是否已有一帧正在检测中（0 = 空闲，1 = 忙碌）</summary>
        private int _liveBusy = 0;

        /// <summary>上一帧检测的发起时刻</summary>
        private int _lastDetectTick = 0;

        /// <summary>检测失败的累计次数，用于连续失败时自动降级</summary>
        private int _liveFailCount = 0;

        /// <summary>连续失败的最大次数，超过后暂停实时检测以避免刷爆接口配额</summary>
        private const int MaxLiveFailCount = 5;

        /// <summary>窗体是否正在关闭，用于阻止关闭过程中的帧回调</summary>
        private volatile bool _closing = false;

        /// <summary>欢迎语音的绝对路径，惰性解析并缓存</summary>
        private string _welcomeSoundPath;

        public Form1()
        {
            InitializeComponent();

            API_KEY = ReadConfig("ApiKey");
            SECRET_KEY = ReadConfig("SecretKey");

            // 界面创建完成后再套用主题，确保所有自绘控件都能收到配色
            ApplyTheme();

            client = new Face(API_KEY, SECRET_KEY);         // 初始化人脸识别客户端
        }

        /// <summary>
        /// 读取 app.config 中的配置项；未配置时返回空字符串。
        /// </summary>
        private static string ReadConfig(string key)
        {
            try
            {
                return (ConfigurationManager.AppSettings[key] ?? string.Empty).Trim();
            }
            catch (Exception ex)
            {
                ClassLoger.Error("Form1/ReadConfig", key, ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 认证信息是否已填写（占位符视为未填写）。
        /// </summary>
        private bool CredentialsConfigured()
        {
            return !API_KEY.IsNull() && !SECRET_KEY.IsNull()
                   && !API_KEY.StartsWith("请替换") && !SECRET_KEY.StartsWith("请替换");
        }

        /// <summary>
        /// 校验认证信息是否已正确配置，未配置时提示用户并返回 false。
        /// </summary>
        private bool EnsureCredentials()
        {
            if (!CredentialsConfigured())
            {
                MessageBox.Show(
                    "尚未配置百度 AI 认证信息。\n\n请在 BaiduAI.exe.config 的 <appSettings> 中填写 ApiKey 与 SecretKey 后重新启动程序。",
                    "缺少认证信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("未配置认证信息，接口调用不可用", error: true);
                return false;
            }
            return true;
        }

        // ==================================================================
        //  通用的接口结果处理
        // ==================================================================

        /// <summary>
        /// 统一判断接口返回是否成功；失败时把百度返回的错误信息友好地显示出来。
        /// 百度接口约定：error_code == 0 表示成功，非 0 表示失败且 error_msg 给出原因。
        /// </summary>
        private bool CheckApiResult(JObject result, string action)
        {
            if (result == null)
            {
                SetStatus(action + "失败：接口无返回", error: true);
                MessageBox.Show(action + "失败：接口未返回任何内容，请检查网络连接。",
                    "接口异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            JToken codeToken = result["error_code"];
            int errorCode = codeToken == null ? 0 : codeToken.Value<int>();
            if (errorCode == 0) return true;

            string errorMsg = result["error_msg"] == null ? "未知错误" : result["error_msg"].ToString();
            // 配额 / 限流类错误与业务错误分别给出可操作的建议
            string hint = (errorCode == 4 || errorCode == 17 || errorCode == 18)
                ? "\n\n可能原因：接口调用量超限或配额不足，请稍后重试或检查百度控制台配额。"
                : "\n\n可能原因：认证信息无效、应用未开通对应接口，或参数不符合要求。";

            ClassLoger.Error("Form1/CheckApiResult", action + " error_code=" + errorCode, errorMsg);
            SetStatus(string.Format("{0}失败（错误码 {1}）", action, errorCode), error: true);
            MessageBox.Show(string.Format("{0}失败（错误码 {1}）：{2}{3}", action, errorCode, errorMsg, hint),
                "接口返回错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        /// <summary>
        /// 把当前画面编码为 JPEG 的 Base64 字符串。
        /// </summary>
        private string EncodeFrameToBase64(Bitmap frame)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                frame.Save(ms, ImageFormat.Jpeg);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// 从摄像头抓取一帧、并缩放到不超过 maxWidth x maxHeight 的位图。调用方负责释放返回值。
        /// </summary>
        private Bitmap CaptureScaledFrame(int maxWidth, int maxHeight)
        {
            Bitmap currentFrame = videoSourcePlayer1.GetCurrentVideoFrame();
            if (currentFrame == null) return null;

            try
            {
                double ratio = Math.Min((double)maxWidth / currentFrame.Width,
                                        (double)maxHeight / currentFrame.Height);
                if (ratio >= 1.0) return new Bitmap(currentFrame);

                int newWidth = Math.Max(1, (int)(currentFrame.Width * ratio));
                int newHeight = Math.Max(1, (int)(currentFrame.Height * ratio));
                Bitmap resized = new Bitmap(newWidth, newHeight);
                using (Graphics g = Graphics.FromImage(resized))
                {
                    g.DrawImage(currentFrame, 0, 0, newWidth, newHeight);
                }
                return resized;
            }
            finally
            {
                currentFrame.Dispose();
            }
        }

        /// <summary>
        /// 判断摄像头是否已就绪并给出提示。
        /// </summary>
        private bool EnsureCameraReady()
        {
            if (comboBox1.Items.Count <= 0)
            {
                MessageBox.Show("请插入视频设备", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (videoSourcePlayer1 == null || !videoSourcePlayer1.IsRunning)
            {
                MessageBox.Show("摄像头尚未启动，请先点击「连接」。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        // ==================================================================
        //  图片模式
        // ==================================================================

        /// <summary>
        /// 将图片转换为Base64字符串（用于API调用）
        /// </summary>
        public string ConvertImageToBase64(Image file)
        {
            if (file == null) return null;
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    file.Save(ms, ImageFormat.Jpeg);
                    byte[] bytes = ms.ToArray();
                    return Convert.ToBase64String(bytes);
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("ConvertImageToBase64", ex);
                return null;
            }
        }

        /// <summary>
        /// 读取图片文件并转换为Base64
        /// </summary>
        public string ReadImg(string img)
        {
            return Convert.ToBase64String(File.ReadAllBytes(img));
        }

        // 选择两张人脸图片（按“先 A 后 B”的顺序填入）
        private void btnPickImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog
            {
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*",
                RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                if (txtFaceA.Inner.Text.IsNull())
                {
                    txtFaceA.Inner.Text = dialog.FileName;
                    SetStatus("已选择图片 A，请再选一张进行对比");
                }
                else if (txtFaceB.Inner.Text.IsNull())
                {
                    txtFaceB.Inner.Text = dialog.FileName;
                    SetStatus("已选择图片 B，可以点击「开始对比」");
                }
                else
                {
                    // 两张都已选择：整体左移，把新图放进 B
                    txtFaceA.Inner.Text = txtFaceB.Inner.Text;
                    txtFaceB.Inner.Text = dialog.FileName;
                    SetStatus("已替换图片，可以点击「开始对比」");
                }
            }
        }

        // 人脸对比
        private async void btnCompare_Click(object sender, EventArgs e)
        {
            if (!EnsureCredentials()) return;

            string path1 = txtFaceA.Inner.Text;
            string path2 = txtFaceB.Inner.Text;

            if (path1.IsNull() || path2.IsNull())
            {
                MessageBox.Show("请选择要对比的两张人脸图片", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 先确认两个文件都存在，避免 File.ReadAllBytes 抛出难以理解的异常
            foreach (string path in new[] { path1, path2 })
            {
                if (!File.Exists(path))
                {
                    MessageBox.Show("找不到图片文件：\n" + path, "文件不存在",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            btnCompare.Enabled = false;
            UseWaitCursor = true;
            SetStatus("正在进行人脸对比…");
            try
            {
                // 读文件与接口调用都在后台线程执行
                JObject result = await Task.Run(() =>
                {
                    JObject MakeFace(string path) => new JObject
                    {
                        {"image", ReadImg(path)},
                        {"image_type", "BASE64"},
                        {"face_type", "LIVE"},
                        {"quality_control", "LOW"},
                        {"liveness_control", "NONE"}
                    };

                    var faces = new JArray { MakeFace(path1), MakeFace(path2) };
                    return client.Match(faces);
                });

                txtRawResult.Text = result == null ? "接口未返回内容" : result.ToString();

                if (CheckApiResult(result, "人脸对比"))
                {
                    double score = result["result"] == null || result["result"]["score"] == null
                        ? 0 : result["result"].Value<double>("score");
                    SetStatus(string.Format("人脸对比完成，相似度 {0}", score), success: true);
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("btnCompare_Click", ex);
                SetStatus("人脸对比失败：" + ex.Message, error: true);
                MessageBox.Show("人脸对比失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCompare.Enabled = true;
                UseWaitCursor = false;
            }
        }

        // ==================================================================
        //  窗体加载 / 关闭
        // ==================================================================

        // 窗体加载初始化
        private void Form1_Load(object sender, EventArgs e)
        {
            // 枚举所有视频输入设备
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices != null && videoDevices.Count > 0)
            {
                foreach (FilterInfo device in videoDevices)
                    comboBox1.Items.Add(device.Name);
                comboBox1.SelectedIndex = 0;
                SetStatus(string.Format("已检测到 {0} 个视频设备", videoDevices.Count));
            }
            else
            {
                SetStatus("未检测到视频设备", error: true);
            }

            // 注册视频帧回调事件；实时人脸检测在该回调中按 DetectIntervalMs 节流触发
            videoSourcePlayer1.NewFrame += VideoSourcePlayer1_NewFrame;

            if (!CredentialsConfigured())
            {
                ClassLoger.Fail("Form1_Load", "认证信息未配置，接口调用将不可用");
                SetStatus("未配置认证信息，请填写 app.config 后重启", error: true);
            }
            else
            {
                SetStatus("就绪");
            }
        }

        // 窗体关闭事件：资源清理
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            _closing = true;

            // 取消后台任务
            try { _cancellationTokenSource.Cancel(); }
            catch (Exception ex) { ClassLoger.Error("Form1_FormClosed/cancel", ex); }

            // 停止音频播放
            try
            {
                if (axWindowsMediaPlayer1 != null) axWindowsMediaPlayer1.Ctlcontrols.stop();
            }
            catch (Exception ex) { ClassLoger.Error("Form1_FormClosed/audio", ex); }

            // 停止视频流与设备
            StopCamera();

            if (videoSourcePlayer1 != null)
                videoSourcePlayer1.NewFrame -= VideoSourcePlayer1_NewFrame;

            _facePen.Dispose();

            // 不再用 Environment.Exit 立即强杀进程，先给 AForge 采集线程留出收尾时间；
            // 若个别驱动导致进程残留，等待 500ms 后再强制结束。
            Task.Delay(500).ContinueWith(_ =>
            {
                try { Environment.Exit(0); }
                catch (Exception ex) { ClassLoger.Error("Form1_FormClosed/exit", ex); }
            });
        }

        // ==================================================================
        //  实时预览与人脸框绘制
        // ==================================================================

        // 视频帧处理回调（核心渲染逻辑）
        private void VideoSourcePlayer1_NewFrame(object sender, ref Bitmap image)
        {
            if (_closing) return;

            try
            {
                if (image == null) return;

                // 节流：距上次检测达到间隔、且没有正在进行的检测时，克隆一帧交给后台检测
                TryQueueLiveDetect(image);

                // 在检测到的人脸位置绘制矩形框（位置由后台线程更新，超过有效期不再绘制）
                FaceLocation box = location;
                if (box != null && unchecked(Environment.TickCount - _locationStamp) < FaceBoxTtlMs)
                    DrawFaceBox(image, box);
            }
            catch (Exception ex)
            {
                ClassLoger.Error("VideoSourcePlayer1_NewFrame", ex);
            }
        }

        /// <summary>
        /// 在帧上绘制人脸矩形框。颜色取 DSH 状态色 success（#22c55e）。
        /// </summary>
        private void DrawFaceBox(Bitmap image, FaceLocation box)
        {
            int left = Math.Max(0, box.left);
            int top = Math.Max(0, box.top);
            int right = Math.Min(image.Width - 1, box.left + box.width);
            int bottom = Math.Min(image.Height - 1, box.top + box.height);
            if (right <= left || bottom <= top) return;

            using (Graphics g = Graphics.FromImage(image))
            {
                g.DrawRectangle(_facePen, left, top, right - left, bottom - top);
            }
        }

        /// <summary>
        /// 按节流间隔把一个帧副本排入后台检测队列。
        /// </summary>
        private void TryQueueLiveDetect(Bitmap frame)
        {
            if (_liveFailCount >= MaxLiveFailCount) return;         // 连续失败过多，暂停实时检测
            if (Volatile.Read(ref _liveBusy) != 0) return;          // 上一帧还在检测

            int now = Environment.TickCount;
            if (unchecked(now - _lastDetectTick) < DetectIntervalMs) return;
            if (Interlocked.CompareExchange(ref _liveBusy, 1, 0) != 0) return;

            _lastDetectTick = now;

            // NewFrame 返回后该 Bitmap 会被复用，必须先克隆再交给后台线程
            Bitmap copy;
            try
            {
                copy = (Bitmap)frame.Clone();
            }
            catch (Exception ex)
            {
                ClassLoger.Error("TryQueueLiveDetect", ex);
                Volatile.Write(ref _liveBusy, 0);
                return;
            }

            Task.Run(() => LiveDetectWorker(copy));
        }

        /// <summary>
        /// 后台实时检测：调用接口、记录人脸位置，并回主线程更新年龄与质量提示。
        /// </summary>
        private void LiveDetectWorker(Bitmap copy)
        {
            Bitmap scaled = null;
            try
            {
                // 缩放到较小尺寸以减少上传耗时与流量
                double ratio = Math.Min(400.0 / copy.Width, 300.0 / copy.Height);
                if (ratio < 1.0)
                {
                    scaled = new Bitmap(Math.Max(1, (int)(copy.Width * ratio)),
                                        Math.Max(1, (int)(copy.Height * ratio)));
                    using (Graphics g = Graphics.FromImage(scaled))
                        g.DrawImage(copy, 0, 0, scaled.Width, scaled.Height);
                }
                else
                {
                    scaled = new Bitmap(copy);
                }

                string imageBase64 = EncodeFrameToBase64(scaled);
                var options = new Dictionary<string, object>
                {
                    {"max_face_num", 2},
                    {"face_field", "age,qualities,beauty"}
                };

                JObject result = client.Detect(imageBase64, "BASE64", options);

                if (result == null || result.Value<int?>("error_code") != 0)
                {
                    _liveFailCount++;
                    ClassLoger.Error("LiveDetectWorker",
                        result == null ? "接口无返回" : result.ToString());
                    return;
                }

                _liveFailCount = 0;

                FaceDetectInfo detect = JsonHelper.DeserializeObject<FaceDetectInfo>(result.ToString());
                if (detect == null || detect.result == null || detect.result.Count == 0)
                {
                    location = null;    // 画面中没有可解析的人脸
                    return;
                }

                FaceDetectInfoResult face = detect.result[0];
                if (face.location == null) return;
                if (face.location.width <= 0 || face.location.height <= 0) return;

                // 把「缩放图坐标」换算回「预览帧坐标」
                double scaleBack = (double)copy.Width / scaled.Width;
                location = new FaceLocation
                {
                    left = (int)(face.location.left * scaleBack),
                    top = (int)(face.location.top * scaleBack),
                    width = (int)(face.location.width * scaleBack),
                    height = (int)(face.location.height * scaleBack)
                };
                _locationStamp = Environment.TickCount;

                // 回主线程更新年龄与质量提示
                try
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (_closing) return;
                        pillAge.PillText = Math.Round(face.age).ToString();
                        txtQuality.Text = BuildQualityReport(face);
                    });
                }
                catch (InvalidOperationException)
                {
                    // 窗体正在关闭，句柄已销毁，丢弃这次界面更新即可
                }
            }
            catch (Exception ex)
            {
                _liveFailCount++;
                ClassLoger.Error("LiveDetectWorker", ex);
            }
            finally
            {
                if (scaled != null) scaled.Dispose();
                copy.Dispose();
                Volatile.Write(ref _liveBusy, 0);
            }
        }

        /// <summary>
        /// 依据返回的质量字段生成中文提示；质量良好时返回 "OK"。
        /// </summary>
        private static string BuildQualityReport(FaceDetectInfoResult face)
        {
            var sb = new System.Text.StringBuilder();
            Qualities q = face.qualities;

            if (q != null)
            {
                if (q.blur >= 0.7) sb.AppendLine("人脸过于模糊");
                if (q.completeness >= 0.4) sb.AppendLine("人脸不完整");
                if (q.illumination <= 40) sb.AppendLine("灯光光线质量不好");

                Occlusion o = q.occlusion;
                if (o != null)
                {
                    if (o.left_cheek >= 0.8) sb.AppendLine("左脸颊不清晰");
                    if (o.left_eye >= 0.6) sb.AppendLine("左眼不清晰");
                    if (o.mouth >= 0.7) sb.AppendLine("嘴巴不清晰");
                    if (o.nose >= 0.7) sb.AppendLine("鼻子不清晰");
                    if (o.right_cheek >= 0.8) sb.AppendLine("右脸颊不清晰");
                    if (o.right_eye >= 0.6) sb.AppendLine("右眼不清晰");
                    if (o.chin >= 0.6) sb.AppendLine("下巴不清晰");
                }
            }

            if (face.location != null && (face.location.height <= 100 || face.location.width <= 100))
                sb.AppendLine("人脸部分过小");

            string report = sb.ToString().Trim();
            return report.IsNull() ? "OK" : report;
        }

        // ==================================================================
        //  摄像头控制
        // ==================================================================

        // 连接摄像头
        private void CameraConn()
        {
            if (comboBox1.Items.Count <= 0)
            {
                MessageBox.Show("请插入视频设备");
                return;
            }

            StopCamera();   // 重复点击「连接」时先释放上一个设备，避免句柄泄漏

            // 配置摄像头参数
            videoSource = new VideoCaptureDevice(videoDevices[comboBox1.SelectedIndex].MonikerString);

            // AForge 2.2.5 将这两个属性标记为已过时，但在该版本中仍是设置采集分辨率与帧率的可用方式，
            // 保留并显式抑制警告（替代方案 VideoResolution / VideoCapabilities 的形态改为只读）。
#pragma warning disable 0612
            videoSource.DesiredFrameSize = new System.Drawing.Size(320, 240);  // 设置分辨率
            videoSource.DesiredFrameRate = 15;                  // 15fps：预览流畅，接口调用另有节流控制
#pragma warning restore 0612

            // 启动视频流
            videoSourcePlayer1.VideoSource = videoSource;
            videoSourcePlayer1.Start();

            _liveFailCount = 0;
            _lastDetectTick = 0;

            cardPreview.StatusText = "已连接";
            cardPreview.StatusColor = DshControls.Theme.StateSuccess;
            cardPreview.Invalidate();
            SetStatus(string.Format("摄像头已连接：{0}", comboBox1.Text), success: true);
        }

        /// <summary>
        /// 停止并释放摄像头资源。可安全重复调用，也可在从未连接过时调用。
        /// </summary>
        private void StopCamera()
        {
            try
            {
                if (videoSourcePlayer1 != null)
                {
                    if (videoSourcePlayer1.IsRunning) videoSourcePlayer1.Stop();
                    videoSourcePlayer1.VideoSource = null;
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("StopCamera/player", ex);
            }

            try
            {
                if (videoSource != null)
                {
                    if (videoSource.IsRunning) videoSource.Stop();
                    videoSource = null;
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("StopCamera/source", ex);
            }

            location = null;    // 清掉人脸框，避免下次启动时残留
        }

        // 连接按钮
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                CameraConn();
            }
            catch (Exception ex)
            {
                ClassLoger.Error("btnConnect_Click", ex);
                SetStatus("连接摄像头失败：" + ex.Message, error: true);
                MessageBox.Show("连接摄像头失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 重新检测设备按钮
        private void btnRefreshDevices_Click(object sender, EventArgs e)
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                comboBox1.Items.Clear();
                if (videoDevices != null && videoDevices.Count > 0)
                {
                    foreach (FilterInfo device in videoDevices)
                        comboBox1.Items.Add(device.Name);
                    comboBox1.SelectedIndex = 0;
                    SetStatus(string.Format("已重新检测到 {0} 个视频设备", videoDevices.Count));
                }
                else
                {
                    SetStatus("未检测到可用的视频设备", error: true);
                    MessageBox.Show("未检测到可用的视频设备。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("btnRefreshDevices_Click", ex);
                SetStatus("重新检测设备失败：" + ex.Message, error: true);
                MessageBox.Show("重新检测设备失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 停止按钮
        private void btnStop_Click(object sender, EventArgs e)
        {
            StopCamera();
            cardPreview.StatusText = "未连接";
            cardPreview.StatusColor = null;
            cardPreview.Invalidate();
            SetStatus("摄像头已停止");
        }

        // ==================================================================
        //  拍照分析
        // ==================================================================

        // 拍照并分析
        private async void btnCapture_Click(object sender, EventArgs e)
        {
            if (!EnsureCredentials()) return;
            if (!EnsureCameraReady()) return;

            btnCapture.Enabled = false;
            UseWaitCursor = true;
            SetStatus("正在拍照并分析…");

            try
            {
                using (Bitmap resizedFrame = CaptureScaledFrame(800, 600))
                {
                    if (resizedFrame == null)
                    {
                        MessageBox.Show("无法获取视频帧", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    string picName = Path.Combine(GetImagePath(), DateTime.Now.ToFileTime() + ".jpg");
                    resizedFrame.Save(picName, ImageFormat.Jpeg);

                    string imageBase64 = EncodeFrameToBase64(resizedFrame);

                    // 人脸检测在后台线程执行，界面保持响应
                    JObject result = await Task.Run(() =>
                    {
                        var options = new Dictionary<string, object> { { "face_field", "age,beauty" } };
                        return client.Detect(imageBase64, "BASE64", options);
                    });

                    txtRawResult.Text = result == null ? "接口未返回内容" : result.ToString();

                    if (result == null || result.Value<int?>("error_code") != 0)
                    {
                        string msg = result == null
                            ? "接口无返回"
                            : (result["error_msg"] == null ? "未知错误" : result["error_msg"].ToString());
                        SetStatus("人脸分析失败：" + msg, error: true);
                        MessageBox.Show("图片已保存至：\n" + picName + "\n\n人脸分析失败：" + msg,
                            "拍照完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    JToken faceList = result["result"] == null ? null : result["result"]["face_list"];
                    if (faceList == null || !faceList.HasValues)
                    {
                        SetStatus("拍照完成，但未检测到人脸", error: true);
                        MessageBox.Show("图片已保存至：\n" + picName + "\n\n未检测到人脸信息",
                            "拍照成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    JToken faceInfo = faceList[0];
                    JToken faceLoc = faceInfo["location"];
                    string age = faceInfo["age"] == null ? "-" : faceInfo["age"].ToString();
                    string beauty = faceInfo["beauty"] == null ? "-" : faceInfo["beauty"].ToString();

                    // 更新人脸位置信息（用于在预览画面绘制框）
                    if (faceLoc != null)
                    {
                        location = new FaceLocation
                        {
                            left = faceLoc.Value<int>("left"),
                            top = faceLoc.Value<int>("top"),
                            width = faceLoc.Value<int>("width"),
                            height = faceLoc.Value<int>("height")
                        };
                        _locationStamp = Environment.TickCount;
                    }

                    pillAge.PillText = age;
                    txtQuality.Text = "美颜度：" + beauty;
                    SetStatus(string.Format("拍照完成：年龄 {0}，美颜度 {1}", age, beauty), success: true);
                    MessageBox.Show("图片已成功保存至：\n" + picName + "\n\n人脸信息：\n年龄：" + age + "\n美颜度：" + beauty,
                        "拍照成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("btnCapture_Click", ex);
                SetStatus("拍照失败：" + ex.Message, error: true);
                MessageBox.Show("拍照过程中发生错误：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCapture.Enabled = true;
                UseWaitCursor = false;
            }
        }

        /// <summary>
        /// 获取图片存储路径（创建 PersonImg 目录）。
        /// 位置为可执行文件所在目录的上一级，便于清理编译产物后照片仍然保留。
        /// </summary>
        private string GetImagePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string parent = Path.GetDirectoryName(baseDir);
            string personImgPath = Path.Combine(parent.IsNull() ? baseDir : parent, "PersonImg");
            if (!Directory.Exists(personImgPath)) Directory.CreateDirectory(personImgPath);
            return personImgPath;
        }

        /// <summary>
        /// 位图转字节数组（用于API调用）
        /// </summary>
        public byte[] Bitmap2Byte(Bitmap bitmap)
        {
            if (bitmap == null) return null;
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Jpeg);
                    byte[] data = new byte[stream.Length];
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.Read(data, 0, Convert.ToInt32(stream.Length));
                    return data;
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("Bitmap2Byte", ex);
                return null;
            }
        }

        public byte[] BitmapSource2Byte(BitmapSource source)
        {
            if (source == null) return null;
            try
            {
                JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 100 };
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.Save(stream);
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("BitmapSource2Byte", ex);
                return null;
            }
        }

        /// <summary>
        /// 把预览控件的当前帧转换为 Base64，供人脸上传接口使用。
        /// </summary>
        private string CaptureFrameBase64()
        {
            using (Bitmap frame = videoSourcePlayer1.GetCurrentVideoFrame())
            {
                if (frame == null) return null;

                // GetHbitmap 申请的非托管句柄需要显式释放（BitmapSource 不是 IDisposable，交由 GC 回收）
                IntPtr hBitmap = frame.GetHbitmap();
                try
                {
                    BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    byte[] bytes = BitmapSource2Byte(bitmapSource);
                    return bytes == null ? null : Convert.ToBase64String(bytes);
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        /// <summary>
        /// 释放 GDI 位图句柄。
        /// </summary>
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        // ==================================================================
        //  人脸注册 / 登录
        // ==================================================================

        // 人脸注册
        private async void btnRegister_Click(object sender, EventArgs e)
        {
            if (!EnsureCredentials()) return;

            string uid = "1";                              // 用户ID
            string userInfo = txtUserName.Inner.Text.Trim(); // 用户信息
            string groupId = txtGroupId.Inner.Text.Trim();   // 用户组ID

            if (groupId.IsNull())
            {
                MessageBox.Show("请输入用户分组ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!EnsureCameraReady()) return;

            btnRegister.Enabled = false;
            UseWaitCursor = true;
            SetStatus("正在注册人脸…");
            try
            {
                string imageBase64 = CaptureFrameBase64();
                if (imageBase64.IsNull())
                {
                    MessageBox.Show("无法获取视频帧", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                JObject result = await Task.Run(() =>
                {
                    var options = new Dictionary<string, object>
                    {
                        { "action_type", "REPLACE" },
                        { "user_info", userInfo }
                    };
                    return client.UserAdd(imageBase64, "BASE64", groupId, uid, options);
                });

                txtRawResult.Text = result == null ? "接口未返回内容" : result.ToString();

                if (CheckApiResult(result, "人脸注册"))
                {
                    SetStatus(string.Format("注册成功：分组 {0}，用户 {1}", groupId, uid), success: true);
                    MessageBox.Show("注册成功。\n分组：" + groupId + "\n用户ID：" + uid, "注册成功",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ClassLoger.Error("btnRegister_Click", ex);
                SetStatus("人脸注册失败：" + ex.Message, error: true);
                MessageBox.Show("人脸注册失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRegister.Enabled = true;
                UseWaitCursor = false;
            }
        }

        // 人脸登录 / 识别
        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (!EnsureCredentials()) return;

            string groupId = txtGroupId.Inner.Text.Trim();  // 用户组ID
            if (groupId.IsNull())
            {
                MessageBox.Show("请输入用户组ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!EnsureCameraReady()) return;

            btnLogin.Enabled = false;
            UseWaitCursor = true;
            SetStatus("正在识别人脸…");
            try
            {
                string imageBase64 = CaptureFrameBase64();
                if (imageBase64.IsNull())
                {
                    MessageBox.Show("无法获取视频帧", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                JObject result = await Task.Run(() =>
                {
                    var options = new Dictionary<string, object>
                    {
                        {"match_threshold", 70},        // 相似度阈值(70%)
                        {"quality_control", "NORMAL"},  // 质量控制级别
                        {"liveness_control", "LOW"},    // 活体控制级别
                        {"max_user_num", 3}             // 最多返回3个匹配结果
                    };
                    return client.Search(imageBase64, "BASE64", groupId, options);
                });

                txtRawResult.Text = result == null ? "接口未返回内容" : result.ToString();

                if (!CheckApiResult(result, "人脸登录")) return;

                JToken userList = result["result"] == null ? null : result["result"]["user_list"];
                if (userList == null || !userList.HasValues)
                {
                    txtUserId.Text = string.Empty;
                    SetStatus("未找到匹配的用户", error: true);
                    MessageBox.Show("未找到匹配的用户，请先注册或检查用户组ID是否正确",
                        "未找到用户", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                JToken top = userList[0];
                txtUserId.Text = top["user_id"] == null ? "" : top["user_id"].ToString();
                double score = top["score"] == null ? 0 : top.Value<double>("score");

                PlayWelcomeSound();

                SetStatus(string.Format("登录成功：用户 {0}，相似度 {1}", txtUserId.Text, score), success: true);
                MessageBox.Show("登录成功！用户ID: " + txtUserId.Text + ", 相似度: " + score,
                    "登录成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ClassLoger.Error("btnLogin_Click", ex);
                SetStatus("人脸登录失败：" + ex.Message, error: true);
                MessageBox.Show("人脸登录失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLogin.Enabled = true;
                UseWaitCursor = false;
            }
        }

        /// <summary>
        /// 播放欢迎语音。找不到音频文件时记录日志并跳过，而不是静默失败。
        /// </summary>
        private void PlayWelcomeSound()
        {
            try
            {
                if (axWindowsMediaPlayer1 == null)
                {
                    ClassLoger.Fail("PlayWelcomeSound", "媒体播放器未初始化，跳过欢迎语音");
                    return;
                }

                if (_welcomeSoundPath == null)
                {
                    // 用可执行文件目录定位，避免从快捷方式启动时相对路径失效
                    string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "20230522_160638_1.mp3");
                    _welcomeSoundPath = File.Exists(candidate) ? candidate : string.Empty;
                }

                if (_welcomeSoundPath.IsNull())
                {
                    ClassLoger.Fail("PlayWelcomeSound", "未找到欢迎语音文件 20230522_160638_1.mp3");
                    return;
                }

                axWindowsMediaPlayer1.URL = _welcomeSoundPath;
                axWindowsMediaPlayer1.Ctlcontrols.play();
            }
            catch (Exception ex)
            {
                ClassLoger.Error("PlayWelcomeSound", ex);
            }
        }
    }
}
