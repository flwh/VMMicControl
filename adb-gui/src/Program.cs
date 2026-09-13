using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AdbGui
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "AdbGui_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("ADB 工具箱已经在运行了。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    Report("界面线程", e.Exception);
                };

                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    Report("启动", ex);
                }
            }
        }

        /// <summary>出问题时把堆栈写到 %APPDATA%\AdbGui\crash.log，方便排查</summary>
        private static void Report(string stage, Exception ex)
        {
            string text = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + stage + "失败\r\n" +
                          ex.GetType().FullName + ": " + ex.Message + "\r\n" + ex.StackTrace + "\r\n\r\n";
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdbGui");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"), text, new UTF8Encoding(true));
            }
            catch { }
            MessageBox.Show(ex.Message + "\r\n\r\n详细信息已写入 %APPDATA%\\AdbGui\\crash.log",
                stage + "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
