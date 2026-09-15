using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DownloadDock
{
    internal class App : Application
    {
        private FloatingWindow _window;
        private TrayHost _tray;

        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DownloadDock");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(DataDir, "log.txt"),
                    string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}{2}",
                        DateTime.Now, message, Environment.NewLine));
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherException;

            bool selftest = false;
            string[] args = Environment.GetCommandLineArgs();
            foreach (string a in args)
            {
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                    selftest = true;
            }

            Log("startup" + (selftest ? " (selftest)" : ""));

            try
            {
                _window = new FloatingWindow();
                _window.Show();
                _tray = new TrayHost(_window);
                if (selftest) RunSelftest();
            }
            catch (Exception ex)
            {
                Log("startup failed: " + ex);
                MessageBox.Show("DownloadDock 启动失败:\r\n" + ex.Message,
                    "DownloadDock", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void RunSelftest()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1500);
            timer.Tick += delegate
            {
                timer.Stop();
                try
                {
                    Log("SELFTEST OK: " + _window.TestProbe());
                }
                catch (Exception ex)
                {
                    Log("SELFTEST FAIL: " + ex);
                }
                Shutdown(0);
            };
            timer.Start();
        }

        private SettingsWindow _settings;

        public void OpenSettings()
        {
            if (_settings != null)
            {
                try { _settings.Activate(); } catch { }
                return;
            }
            if (_window == null) return;
            _settings = new SettingsWindow(_window);
            _settings.Closed += delegate { _settings = null; };
            _settings.Show();
        }

        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("UI exception: " + e.Exception);
            try
            {
                MessageBox.Show("发生错误（已记录到日志）:\r\n" + e.Exception.Message,
                    "DownloadDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { }
            e.Handled = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { if (_tray != null) _tray.Dispose(); } catch { }
            Log("exit code=" + e.ApplicationExitCode);
            base.OnExit(e);
        }
    }
}
