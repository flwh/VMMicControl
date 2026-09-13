using System;
using System.Threading;
using System.Windows.Forms;

namespace VMMicControl
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "VMMicControl_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("Voicemeeter 麦克风控制已经在运行了。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
