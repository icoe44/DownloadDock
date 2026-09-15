using System;
using System.Globalization;
using System.IO;

namespace DownloadDock
{
    internal static class SettingsStore
    {
        private static string FilePath
        {
            get { return Path.Combine(App.DataDir, "settings.txt"); }
        }

        // 返回值表示是否读到了有效坐标；folder 无论是否读到坐标都会输出
        public static bool TryLoad(out double x, out double y, out string folder)
        {
            x = 0; y = 0; folder = "";
            try
            {
                if (!File.Exists(FilePath)) return false;
                string[] lines = File.ReadAllLines(FilePath);
                double lx = 0, ly = 0;
                bool okx = false, oky = false;
                foreach (string line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "x")
                        okx = double.TryParse(val, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out lx);
                    else if (key == "y")
                        oky = double.TryParse(val, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out ly);
                    else if (key == "folder")
                        folder = val;
                }
                if (okx && oky) { x = lx; y = ly; return true; }
            }
            catch (Exception ex) { App.Log("settings load failed: " + ex.Message); }
            return false;
        }

        public static void Save(double x, double y, string folder)
        {
            try
            {
                File.WriteAllText(FilePath,
                    string.Format(CultureInfo.InvariantCulture, "x={0}\r\ny={1}\r\n", x, y) +
                    "folder=" + (folder ?? "") + "\r\n");
            }
            catch (Exception ex) { App.Log("settings save failed: " + ex.Message); }
        }

        public static void SaveFolderOnly(string folder)
        {
            double x, y; string f;
            bool hasPos = TryLoad(out x, out y, out f);
            try
            {
                if (hasPos)
                {
                    Save(x, y, folder);
                }
                else
                {
                    File.WriteAllText(FilePath, "folder=" + (folder ?? "") + "\r\n");
                }
            }
            catch (Exception ex) { App.Log("settings save failed: " + ex.Message); }
        }
    }
}
