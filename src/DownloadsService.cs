using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace DownloadDock
{
    internal class DownloadItem
    {
        public string FullPath;
        public string Name;
        public string ModifiedText;
        public DateTime Modified;
    }

    internal static class DownloadsService
    {
        private static readonly string[] SkipExtensions = new string[]
        {
            ".crdownload", ".part", ".partial", ".tmp", ".opdownload",
            ".download", ".dtl", ".jd", ".lnk", ".ini"
        };

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);

        private static Guid FolderDownloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        private static string _customFolder = "";

        public static void SetCustomFolder(string folder)
        {
            if (folder == null) folder = "";
            folder = folder.Trim().Trim('"').Trim();
            _customFolder = folder;
            App.Log("download folder = " +
                (_customFolder.Length == 0 ? "(system default)" : _customFolder));
        }

        public static string GetCustomFolder() { return _customFolder; }

        public static string GetSystemDownloadsPath()
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                Guid id = FolderDownloads;
                int hr = SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out ptr);
                if (hr == 0 && ptr != IntPtr.Zero)
                {
                    string p = Marshal.PtrToStringUni(ptr);
                    if (!string.IsNullOrEmpty(p)) return p;
                }
            }
            catch { }
            finally
            {
                if (ptr != IntPtr.Zero) Marshal.FreeCoTaskMem(ptr);
            }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        public static string GetDownloadsPath()
        {
            if (_customFolder.Length > 0) return _customFolder;
            return GetSystemDownloadsPath();
        }

        public static List<DownloadItem> GetRecentFiles(int maxCount)
        {
            List<DownloadItem> result = new List<DownloadItem>();
            string dir = GetDownloadsPath();
            DirectoryInfo info = new DirectoryInfo(dir);
            if (!info.Exists) return result;

            DateTime now = DateTime.Now;
            List<FileInfo> files = new List<FileInfo>();
            foreach (FileInfo f in info.GetFiles())
            {
                try
                {
                    FileAttributes attr = f.Attributes;
                    if ((attr & FileAttributes.Hidden) != 0) continue;
                    if ((attr & FileAttributes.System) != 0) continue;
                    if ((attr & FileAttributes.Directory) != 0) continue;
                    if (SkipExtensions.Contains(f.Extension.ToLowerInvariant())) continue;
                    files.Add(f);
                }
                catch { }
            }

            List<FileInfo> sorted = files.OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            foreach (FileInfo f in sorted)
            {
                if (result.Count >= maxCount) break;
                DownloadItem item = new DownloadItem();
                item.FullPath = f.FullName;
                item.Name = f.Name;
                item.Modified = f.LastWriteTime;
                item.ModifiedText = FormatDate(f.LastWriteTime, now);
                result.Add(item);
            }
            return result;
        }

        private static string FormatDate(DateTime d, DateTime now)
        {
            if (d.Year == now.Year) return d.ToString("MM-dd HH:mm");
            return d.ToString("yyyy-MM-dd");
        }

        public static void OpenFolder()
        {
            try { Process.Start(GetDownloadsPath()); }
            catch (Exception ex) { App.Log("open folder failed: " + ex.Message); }
        }

        public static void OpenFile(string path)
        {
            try { Process.Start(path); }
            catch (Exception ex) { App.Log("open file failed: " + ex.Message); }
        }

        public static void RevealFile(string path)
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { App.Log("reveal failed: " + ex.Message); }
        }

        public static void CopyPath(string path)
        {
            try { Clipboard.SetText(path); }
            catch (Exception ex) { App.Log("copy path failed: " + ex.Message); }
        }
    }
}
