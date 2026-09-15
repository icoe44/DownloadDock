using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace DownloadDock
{
    internal class TrayHost : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly FloatingWindow _window;

        public TrayHost(FloatingWindow window)
        {
            _window = window;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("设置...", null, delegate
            {
                ((App)System.Windows.Application.Current).OpenSettings();
            });
            menu.Items.Add("重置位置", null, delegate { _window.ResetPosition(); });
            menu.Items.Add("打开下载文件夹", null,
                delegate { DownloadsService.OpenFolder(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate
            {
                System.Windows.Application.Current.Shutdown();
            });

            _icon = new NotifyIcon();
            _icon.Icon = LoadIcon();
            _icon.Text = "DownloadDock · 下载浮窗";
            _icon.ContextMenuStrip = menu;
            _icon.Visible = true;
            _icon.DoubleClick += delegate { DownloadsService.OpenFolder(); };
        }

        private static Icon LoadIcon()
        {
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exe))
                {
                    Icon ic = Icon.ExtractAssociatedIcon(exe);
                    if (ic != null) return ic;
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            try { _icon.Visible = false; } catch { }
            try { _icon.Dispose(); } catch { }
        }
    }
}
