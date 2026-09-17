using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DownloadDock
{
    // 缩略图/悬浮预览的解码缓存：key = 路径 + mtime + 长边上限。
    // OnLoad 全量读入后立即释放文件句柄（不阻塞下载文件夹的删改/重命名）；
    // 解码失败（非图片 / 缺编解码器 / 文件已消失）返回 null，由调用方退化为占位块。
    // downscale-only：仅当源图长边超过上限时才等比缩小解码，小图保持原尺寸不放大。
    internal static class ThumbCache
    {
        private const int CacheLimit = 96;
        private static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>();
        private static readonly object Gate = new object();

        public static BitmapSource Get(string path, DateTime modified, int maxEdge)
        {
            if (string.IsNullOrEmpty(path) || maxEdge <= 0) return null;
            if (!IsImageExtension(Path.GetExtension(path))) return null;

            string key = path.ToLowerInvariant() + "|" + modified.Ticks + "|" + maxEdge;
            lock (Gate)
            {
                BitmapSource hit;
                if (Cache.TryGetValue(key, out hit)) return hit;
            }

            BitmapSource bmp = Decode(path, maxEdge);
            if (bmp == null) return null;
            lock (Gate)
            {
                if (Cache.Count >= CacheLimit) Cache.Clear();
                Cache[key] = bmp;
            }
            return bmp;
        }

        private static bool IsImageExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            switch (ext.ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                case ".tif":
                case ".tiff":
                case ".webp": // 系统装了 WebP 编解码器才能解；解不了按失败占位
                    return true;
                default:
                    return false;
            }
        }

        private static BitmapSource Decode(string path, int maxEdge)
        {
            try
            {
                if (!File.Exists(path)) return null;

                // 先只读文件头拿像素尺寸（DelayCreation 不做全图解码）
                BitmapFrame probe = BitmapFrame.Create(new Uri(path),
                    BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                int w = probe.PixelWidth;
                int h = probe.PixelHeight;
                if (w <= 0 || h <= 0) return null;

                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path);
                if (w >= h) { if (w > maxEdge) img.DecodePixelWidth = maxEdge; }
                else { if (h > maxEdge) img.DecodePixelHeight = maxEdge; }
                img.EndInit();

                // 归一化到 96dpi：让显示尺寸(dip) == 解码像素，长边上限才严格成立
                // （否则带 72dpi 元数据的图会被放大渲染，超出 350px 预期）
                BitmapSource result;
                if (Math.Abs(img.DpiX - 96.0) > 0.5 || Math.Abs(img.DpiY - 96.0) > 0.5)
                {
                    TransformedBitmap normalized = new TransformedBitmap(img,
                        new ScaleTransform(96.0 / img.DpiX, 96.0 / img.DpiY));
                    normalized.Freeze();
                    result = normalized;
                }
                else
                {
                    img.Freeze();
                    result = img;
                }
                return result;
            }
            catch (Exception ex)
            {
                App.Log("thumb decode failed " + path + ": " + ex.Message);
                return null;
            }
        }
    }
}
