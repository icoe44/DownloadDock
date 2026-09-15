using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DownloadDock
{
    internal class SettingsWindow : Window
    {
        private static readonly SolidColorBrush TextBrush =
            new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        private static readonly SolidColorBrush HintBrush =
            new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
        private static readonly SolidColorBrush OkBrush =
            new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
        private static readonly SolidColorBrush ErrBrush =
            new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28));

        private readonly FloatingWindow _floating;
        private TextBox _folderBox;
        private TextBlock _status;
        private CheckBox _autoStart;

        public SettingsWindow(FloatingWindow floating)
        {
            _floating = floating;

            Title = "DownloadDock 设置";
            Width = 480;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 12.5;

            StackPanel root = new StackPanel();
            root.Margin = new Thickness(20);
            Content = root;

            root.Children.Add(MakeText("下载文件夹", TextBrush, 0, 0, 0, 0));

            DockPanel row = new DockPanel();
            row.Margin = new Thickness(0, 6, 0, 0);

            Button browse = MakeButton("浏览...", 0, 0, 0, 0);
            browse.Click += delegate { BrowseFolder(); };

            Button useDefault = MakeButton("系统默认", 8, 0, 0, 0);
            useDefault.Click += delegate
            {
                _folderBox.Text = "";
                ApplyFolder();
            };

            DockPanel.SetDock(browse, Dock.Right);
            DockPanel.SetDock(useDefault, Dock.Right);
            row.Children.Add(browse);
            row.Children.Add(useDefault);

            _folderBox = new TextBox();
            _folderBox.Height = 28;
            _folderBox.VerticalContentAlignment = VerticalAlignment.Center;
            _folderBox.Text = DownloadsService.GetCustomFolder();
            row.Children.Add(_folderBox);
            root.Children.Add(row);

            TextBlock hint = MakeText(
                "留空 = 跟随系统默认下载文件夹（系统默认：" +
                DownloadsService.GetSystemDownloadsPath() + "）",
                HintBrush, 0, 6, 0, 0);
            hint.TextTrimming = TextTrimming.CharacterEllipsis;
            root.Children.Add(hint);

            root.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 0) });

            _autoStart = new CheckBox();
            _autoStart.Content = "开机自动启动";
            _autoStart.Margin = new Thickness(0, 14, 0, 0);
            _autoStart.IsChecked = StartupManager.IsEnabled();
            _autoStart.Checked += delegate { SetAutoStart(true); };
            _autoStart.Unchecked += delegate { SetAutoStart(false); };
            root.Children.Add(_autoStart);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 18, 0, 0);

            Button resetPos = MakeButton("重置悬浮按钮位置", 0, 0, 10, 0);
            resetPos.Click += delegate
            {
                _floating.ResetPosition();
                ShowStatus("悬浮按钮已回到默认位置", false);
            };
            buttons.Children.Add(resetPos);

            Button apply = MakeButton("应用路径", 0, 0, 10, 0);
            apply.Click += delegate { ApplyFolder(); };
            buttons.Children.Add(apply);

            Button close = MakeButton("关闭", 0, 0, 0, 0);
            close.Click += delegate { Close(); };
            buttons.Children.Add(close);

            root.Children.Add(buttons);

            _status = new TextBlock();
            _status.Margin = new Thickness(0, 10, 0, 0);
            _status.FontSize = 11.5;
            root.Children.Add(_status);
        }

        private void BrowseFolder()
        {
            using (System.Windows.Forms.FolderBrowserDialog dlg =
                new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择下载文件夹";
                dlg.ShowNewFolderButton = false;
                string current = _folderBox.Text;
                if (current != null && current.Trim().Length > 0 &&
                    System.IO.Directory.Exists(current.Trim().Trim('"')))
                {
                    dlg.SelectedPath = current.Trim().Trim('"');
                }
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _folderBox.Text = dlg.SelectedPath;
                    ApplyFolder();
                }
            }
        }

        private void ApplyFolder()
        {
            string path = _folderBox.Text == null ? "" : _folderBox.Text.Trim().Trim('"');
            _folderBox.Text = path;
            try
            {
                _floating.ApplyFolder(path);
            }
            catch (Exception ex)
            {
                ShowStatus("应用失败: " + ex.Message, true);
                return;
            }
            if (path.Length > 0 && !System.IO.Directory.Exists(path))
                ShowStatus("已应用（注意：该文件夹当前不存在，列表会显示为空）", true);
            else
                ShowStatus("已应用", false);
        }

        private void SetAutoStart(bool on)
        {
            try
            {
                StartupManager.SetEnabled(on);
                ShowStatus(on ? "已开启开机自启" : "已关闭开机自启", false);
            }
            catch (Exception ex)
            {
                ShowStatus("设置开机自启失败: " + ex.Message, true);
            }
        }

        private void ShowStatus(string text, bool isError)
        {
            _status.Text = text;
            _status.Foreground = isError ? ErrBrush : OkBrush;
        }

        private static TextBlock MakeText(string text, Brush brush,
            double l, double t, double r, double b)
        {
            TextBlock tb = new TextBlock();
            tb.Text = text;
            tb.Foreground = brush;
            tb.Margin = new Thickness(l, t, r, b);
            return tb;
        }

        private static Button MakeButton(string text,
            double l, double t, double r, double b)
        {
            Button btn = new Button();
            btn.Content = text;
            btn.Height = 28;
            btn.Padding = new Thickness(12, 0, 12, 0);
            btn.Margin = new Thickness(l, t, r, b);
            return btn;
        }
    }
}
