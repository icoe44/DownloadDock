using System;
using System.Threading;
using System.Windows.Forms;

namespace DownloadDock
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\DownloadDock.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("DownloadDock 已在运行中（可查看系统托盘）。",
                        "DownloadDock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GC.KeepAlive(mutex);

                App app = new App();
                app.Run();
            }
        }
    }
}
