using System;
using Microsoft.Win32;
using System.Reflection;

namespace DownloadDock
{
    internal static class StartupManager
    {
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string ValueName = "DownloadDock";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    object v = key.GetValue(ValueName);
                    string s = v as string;
                    return !string.IsNullOrEmpty(s);
                }
            }
            catch (Exception ex)
            {
                App.Log("startup IsEnabled failed: " + ex.Message);
                return false;
            }
        }

        public static void SetEnabled(bool enable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enable)
                {
                    string exe = Assembly.GetExecutingAssembly().Location;
                    if (string.IsNullOrEmpty(exe)) throw new InvalidOperationException("无法获取 exe 路径");
                    key.SetValue(ValueName, "\"" + exe + "\"");
                }
                else
                {
                    if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
