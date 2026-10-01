using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace BaiduAI
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 界面自检开关：把窗体渲染成 PNG 后退出，便于在没有摄像头/密钥的环境下核对视觉稿。
            // 正常双击运行不会命中该分支，不影响任何功能。
            if (args != null && args.Length >= 2 && args[0] == "--render-test")
            {
                Environment.Exit(RenderSnapshot(args[1]));
                return;
            }

            Application.Run(new Form1());
        }

        /// <summary>
        /// 创建主窗体、完成布局并渲染为位图文件。返回进程退出码（0 = 成功）。
        /// </summary>
        private static int RenderSnapshot(string outputPath)
        {
            string logPath = outputPath + ".log";
            var log = new System.Text.StringBuilder();

            void Step(string s)
            {
                log.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s);
                File.WriteAllText(logPath, log.ToString());
            }

            try
            {
                Step("start");
                using (var form = new Form1())
                {
                    Step("form constructed, size=" + form.Width + "x" + form.Height);
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-4000, -4000);   // 放到屏幕外，避免打扰
                    form.Show();
                    Application.DoEvents();
                    Step("form shown, controls=" + form.Controls.Count);

                    for (int i = 0; i < 12; i++)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(60);
                    }
                    form.Refresh();
                    Application.DoEvents();
                    Step("layout settled");

                    using (var bmp = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                        Step("DrawToBitmap done");
                        bmp.Save(outputPath, ImageFormat.Png);
                        Step("saved " + outputPath);
                    }

                    form.Hide();
                }
                Step("RENDER_OK " + outputPath);
                return 0;
            }
            catch (Exception ex)
            {
                Step("RENDER_FAIL " + ex.GetType().Name + ": " + ex.Message);
                return 2;
            }
        }
    }
}
