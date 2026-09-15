using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;

namespace DownloadDock
{
    internal class FloatingWindow : Window
    {
        private const double PillHeight = 40;
        private const double PanelWidth = 330;
        private const double RowHeight = 34;
        private const int MaxRows = 5;
        private const double Gap = 8;
        private const double DefaultMargin = 24;
        private const double DefaultTopOffset = 120;

        private static readonly SolidColorBrush NormalPillBrush =
            new SolidColorBrush(Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1D));
        private static readonly SolidColorBrush HoverPillBrush =
            new SolidColorBrush(Color.FromArgb(0xF0, 0x24, 0x24, 0x28));
        private static readonly SolidColorBrush RowHoverBrush =
            new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));

        private Border _pill;
        private Border _panel;
        private StackPanel _rowsHost;
        private TextBlock _countText;
        private readonly DispatcherTimer _collapseTimer;
        private readonly DispatcherTimer _saveTimer;
        private readonly DownloadWatcher _watcher;

        private ContextMenu _openMenu;
        private DownloadItem _dragItem;
        private Point _dragStart;
        private bool _dragging;
        private DateTime _lastClickTime;
        private string _lastClickPath = "";
        private bool _positionValidated;
        private double _savedX;
        private double _savedY;
        private bool _hasSavedPos;

        public FloatingWindow()
        {
            LoadSettingsEarly();
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            Width = PanelWidth;
            Height = PillHeight;
            Title = "DownloadDock";
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 12.5;

            Grid root = new Grid();
            RowDefinition r0 = new RowDefinition(); r0.Height = GridLength.Auto;
            RowDefinition r1 = new RowDefinition(); r1.Height = GridLength.Auto;
            root.RowDefinitions.Add(r0);
            root.RowDefinitions.Add(r1);
            Content = root;

            _pill = BuildPill();
            Grid.SetRow(_pill, 0);
            root.Children.Add(_pill);

            _panel = BuildPanel();
            Grid.SetRow(_panel, 1);
            root.Children.Add(_panel);

            _collapseTimer = new DispatcherTimer();
            _collapseTimer.Interval = TimeSpan.FromMilliseconds(220);
            _collapseTimer.Tick += delegate
            {
                _collapseTimer.Stop();
                MaybeCollapse();
            };

            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _saveTimer.Tick += delegate
            {
                _saveTimer.Stop();
                SavePositionNow();
            };

            MouseEnter += delegate { CancelCollapse(); };
            MouseLeave += delegate { ScheduleCollapse(); };
            LocationChanged += delegate
            {
                if (_positionValidated)
                {
                    _saveTimer.Stop();
                    _saveTimer.Start();
                }
            };

            _watcher = new DownloadWatcher(delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RefreshList();
                }));
            });
            RefreshList();

            SourceInitialized += delegate { LoadPosition(); };
            Loaded += delegate { ValidatePosition(); };
            Closed += delegate
            {
                _saveTimer.Stop();
                SavePositionNow();
                _watcher.Dispose();
            };
        }

        // ---------- UI 构建 ----------

        private Border BuildPill()
        {
            Border b = new Border();
            b.Height = PillHeight;
            b.HorizontalAlignment = HorizontalAlignment.Right;
            b.CornerRadius = new CornerRadius(PillHeight / 2);
            b.Background = NormalPillBrush;
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            b.Padding = new Thickness(14, 0, 12, 0);
            b.ToolTip = "DownloadDock 下载浮窗\n左键拖动 · 悬停展开 · 右键菜单";

            StackPanel host = new StackPanel();
            host.Orientation = Orientation.Horizontal;
            host.VerticalAlignment = VerticalAlignment.Center;

            Path arrow = new Path();
            arrow.Data = Geometry.Parse("M 7 1.5 L 7 10 M 3 6.5 L 7 10.5 L 11 6.5");
            arrow.Stroke = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
            arrow.StrokeThickness = 1.8;
            arrow.StrokeStartLineCap = PenLineCap.Round;
            arrow.StrokeEndLineCap = PenLineCap.Round;
            arrow.StrokeLineJoin = PenLineJoin.Round;
            arrow.Width = 15;
            arrow.Height = 15;
            arrow.Stretch = Stretch.Uniform;
            arrow.VerticalAlignment = VerticalAlignment.Center;
            arrow.Margin = new Thickness(0, 0, 7, 0);
            host.Children.Add(arrow);

            TextBlock label = new TextBlock();
            label.Text = "下载";
            label.FontSize = 13;
            label.Foreground = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(0, 0, 8, 0);
            host.Children.Add(label);

            _countText = new TextBlock();
            _countText.Text = "";
            _countText.FontSize = 12;
            _countText.Foreground = new SolidColorBrush(Color.FromArgb(0x96, 0xFF, 0xFF, 0xFF));
            _countText.VerticalAlignment = VerticalAlignment.Center;
            host.Children.Add(_countText);

            b.Child = host;

            b.MouseEnter += delegate
            {
                CancelCollapse();
                b.Background = HoverPillBrush;
                Expand();
            };
            b.MouseLeave += delegate { b.Background = NormalPillBrush; };

            b.MouseLeftButtonDown += delegate
            {
                try
                {
                    Collapse();
                    DragMove();
                    SavePositionNow();
                }
                catch (InvalidOperationException) { }
            };
            b.MouseRightButtonUp += delegate { OpenBallMenu(); };

            return b;
        }

        private Border BuildPanel()
        {
            Border p = new Border();
            p.Margin = new Thickness(0, Gap, 0, 0);
            p.CornerRadius = new CornerRadius(10);
            p.Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x1C, 0x1C, 0x1E));
            p.BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            p.BorderThickness = new Thickness(1);
            p.Padding = new Thickness(6);
            p.Visibility = Visibility.Collapsed;

            _rowsHost = new StackPanel();
            p.Child = _rowsHost;

            p.MouseEnter += delegate { CancelCollapse(); };
            p.MouseLeave += delegate { ScheduleCollapse(); };
            return p;
        }

        private void OpenBallMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(MakeMenuItem("设置...", delegate
            {
                ((App)Application.Current).OpenSettings();
            }));
            menu.Items.Add(MakeMenuItem("重置位置", delegate { ResetPosition(); }));
            menu.Items.Add(MakeMenuItem("打开下载文件夹",
                delegate { DownloadsService.OpenFolder(); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeMenuItem("退出", delegate
            {
                Application.Current.Shutdown();
            }));
            ShowMenu(menu);
        }

        private ContextMenu BuildRowMenu(DownloadItem item)
        {
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(MakeMenuItem("打开文件",
                delegate { DownloadsService.OpenFile(item.FullPath); }));
            menu.Items.Add(MakeMenuItem("打开所在文件夹",
                delegate { DownloadsService.RevealFile(item.FullPath); }));
            menu.Items.Add(MakeMenuItem("复制完整路径",
                delegate { DownloadsService.CopyPath(item.FullPath); }));
            return menu;
        }

        private static MenuItem MakeMenuItem(string header, Action onClick)
        {
            MenuItem mi = new MenuItem();
            mi.Header = header;
            mi.Click += delegate { onClick(); };
            return mi;
        }

        private void ShowMenu(ContextMenu menu)
        {
            CancelCollapse();
            _openMenu = menu;
            menu.Closed += delegate
            {
                _openMenu = null;
                ScheduleCollapse();
            };
            menu.Placement = PlacementMode.MousePoint;
            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        // ---------- 列表 ----------

        public void RefreshList()
        {
            _rowsHost.Children.Clear();
            List<DownloadItem> items;
            try
            {
                items = DownloadsService.GetRecentFiles(MaxRows);
            }
            catch (Exception ex)
            {
                App.Log("refresh list failed: " + ex.Message);
                _rowsHost.Children.Add(MakeNoteRow("无法读取下载文件夹 · 请在设置中检查路径"));
                UpdateCount(0);
                return;
            }
            if (items.Count == 0)
            {
                _rowsHost.Children.Add(MakeNoteRow("（下载文件夹暂无文件）"));
                UpdateCount(0);
                return;
            }
            foreach (DownloadItem item in items)
            {
                _rowsHost.Children.Add(MakeFileRow(item));
            }
            UpdateCount(items.Count);
        }

        private void UpdateCount(int n)
        {
            _countText.Text = n > 0 ? n.ToString() : "";
        }

        private static TextBlock MakeNoteRow(string text)
        {
            TextBlock tb = new TextBlock();
            tb.Text = text;
            tb.Margin = new Thickness(10, 8, 10, 8);
            tb.FontSize = 12;
            tb.Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
            return tb;
        }

        private Border MakeFileRow(DownloadItem item)
        {
            Border row = new Border();
            row.Height = RowHeight;
            row.CornerRadius = new CornerRadius(6);
            row.Padding = new Thickness(10, 0, 10, 0);
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;
            row.Tag = item;

            Grid g = new Grid();
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(86);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock name = new TextBlock();
            name.Text = item.Name;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.Foreground = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
            Grid.SetColumn(name, 0);

            TextBlock date = new TextBlock();
            date.Text = item.ModifiedText;
            date.VerticalAlignment = VerticalAlignment.Center;
            date.HorizontalAlignment = HorizontalAlignment.Right;
            date.FontSize = 11;
            date.Foreground = new SolidColorBrush(Color.FromArgb(0x9E, 0xFF, 0xFF, 0xFF));
            Grid.SetColumn(date, 1);

            g.Children.Add(name);
            g.Children.Add(date);
            row.Child = g;
            row.ToolTip = item.Name + "\n修改时间: " +
                item.Modified.ToString("yyyy-MM-dd HH:mm:ss");
            row.ContextMenu = BuildRowMenu(item);

            row.MouseEnter += delegate
            {
                CancelCollapse();
                row.Background = RowHoverBrush;
            };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            row.MouseLeftButtonDown += RowMouseDown;
            row.MouseMove += RowMouseMove;
            row.MouseLeftButtonUp += RowMouseUp;
            return row;
        }

        // ---------- 拖拽（OLE 文件拖放到 PS/AI） ----------

        private void RowMouseDown(object sender, MouseButtonEventArgs e)
        {
            Border row = (Border)sender;
            _dragItem = (DownloadItem)row.Tag;
            _dragStart = e.GetPosition(this);
            _dragging = false;
            row.CaptureMouse();
        }

        private void RowMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (_dragItem == null || _dragging) return;

            Point pos = e.GetPosition(this);
            Vector delta = pos - _dragStart;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _dragging = true;
            DownloadItem item = _dragItem;
            try
            {
                StringCollection files = new StringCollection();
                files.Add(item.FullPath);
                DataObject data = new DataObject();
                data.SetFileDropList(files);
                data.SetText(item.FullPath, TextDataFormat.UnicodeText);
                DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                App.Log("drag failed: " + ex.Message);
            }
            finally
            {
                _dragging = false;
                _dragItem = null;
                if (sender is IInputElement) ((IInputElement)sender).ReleaseMouseCapture();
                MaybeCollapse();
            }
        }

        private void RowMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragging) return;
            Border row = (Border)sender;
            DownloadItem item = (DownloadItem)row.Tag;

            DateTime now = DateTime.Now;
            if (_lastClickPath == item.FullPath &&
                (now - _lastClickTime).TotalMilliseconds < 500)
            {
                _lastClickPath = "";
                DownloadsService.OpenFile(item.FullPath);
            }
            else
            {
                _lastClickPath = item.FullPath;
                _lastClickTime = now;
            }
        }

        // ---------- 展开 / 收起 ----------

        private void Expand()
        {
            if (_panel.Visibility == Visibility.Visible) return;
            RefreshList();
            _panel.Visibility = Visibility.Visible;
            UpdateLayout();
            double h = PillHeight + Gap + _panel.ActualHeight;
            if (h < PillHeight + Gap + RowHeight) h = PillHeight + Gap + RowHeight;
            Height = h;
        }

        private void Collapse()
        {
            _panel.Visibility = Visibility.Collapsed;
            Height = PillHeight;
            UpdateLayout();
        }

        private void ScheduleCollapse()
        {
            if (_panel.Visibility != Visibility.Visible) return;
            if (_openMenu != null) return;
            _collapseTimer.Stop();
            _collapseTimer.Start();
        }

        private void CancelCollapse()
        {
            _collapseTimer.Stop();
        }

        private void MaybeCollapse()
        {
            if (_openMenu != null) return;
            if (_panel.Visibility != Visibility.Visible) return;
            try
            {
                NativePoint pt;
                if (GetCursorPos(out pt))
                {
                    Point local = PointFromScreen(new Point(pt.X, pt.Y));
                    if (local.X >= 0 && local.Y >= 0 &&
                        local.X <= ActualWidth && local.Y <= ActualHeight)
                        return; // 仍在悬浮球+面板范围内
                }
            }
            catch { }
            Collapse();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint p);

        // ---------- 位置记忆 ----------

        private void LoadSettingsEarly()
        {
            string folder;
            _hasSavedPos = SettingsStore.TryLoad(out _savedX, out _savedY, out folder);
            DownloadsService.SetCustomFolder(folder);
        }

        private void LoadPosition()
        {
            if (_hasSavedPos)
            {
                Left = _savedX;
                Top = _savedY;
            }
            else
            {
                SetDefaultPosition();
            }
        }

        private void ValidatePosition()
        {
            try
            {
                if (!IsBallOnAnyScreen())
                {
                    App.Log("saved position off-screen -> reset");
                    ResetPosition();
                }
            }
            catch (Exception ex)
            {
                App.Log("validate position failed: " + ex.Message);
            }
            _positionValidated = true;
        }

        private bool IsBallOnAnyScreen()
        {
            // 胶囊按钮贴窗口右缘：窗口相对坐标即可（PointToScreen 会自动加上窗口原点）
            double pillW = _pill.ActualWidth > 0 ? _pill.ActualWidth : 132;
            Point center = new Point(Width - pillW / 2, PillHeight / 2);
            Point screen = PointToScreen(center);
            foreach (System.Windows.Forms.Screen s in
                System.Windows.Forms.Screen.AllScreens)
            {
                if (s.WorkingArea.Contains((int)screen.X, (int)screen.Y)) return true;
            }
            return false;
        }

        private void SetDefaultPosition()
        {
            // 球贴窗口右缘：窗口右缘 = 球右缘
            Rect wa = SystemParameters.WorkArea;
            Left = wa.Right - DefaultMargin - Width;
            Top = wa.Top + DefaultTopOffset;
        }

        public void ResetPosition()
        {
            SetDefaultPosition();
            SavePositionNow();
            App.Log("position reset to default");
        }

        private void SavePositionNow()
        {
            SettingsStore.Save(Left, Top, DownloadsService.GetCustomFolder());
        }

        public void ApplyFolder(string folder)
        {
            DownloadsService.SetCustomFolder(folder);
            SettingsStore.SaveFolderOnly(DownloadsService.GetCustomFolder());
            _watcher.Retarget(DownloadsService.GetDownloadsPath());
            RefreshList();
        }

        // ---------- 自检 ----------

        public string TestProbe()
        {
            int count = 0;
            string path = "?";
            try
            {
                path = DownloadsService.GetDownloadsPath();
                count = DownloadsService.GetRecentFiles(MaxRows).Count;
            }
            catch { }
            return string.Format(CultureInfo.InvariantCulture,
                "downloads={0}; items={1}; pos=({2},{3}); pill=({4}x{5})",
                path, count,
                Math.Round(Left, 1), Math.Round(Top, 1),
                Math.Round(_pill.ActualWidth, 1), Math.Round(PillHeight, 1));
        }
    }

    internal class DownloadWatcher : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action _onChange;
        private readonly DispatcherTimer _debounce;
        private FileSystemWatcher _watcher;

        public DownloadWatcher(Action onChange)
        {
            _onChange = onChange;
            _dispatcher = Dispatcher.CurrentDispatcher;

            _debounce = new DispatcherTimer();
            _debounce.Interval = TimeSpan.FromMilliseconds(300);
            _debounce.Tick += delegate
            {
                _debounce.Stop();
                _onChange();
            };

            CreateWatcher(DownloadsService.GetDownloadsPath());
        }

        public void Retarget(string newPath)
        {
            CreateWatcher(newPath);
        }

        private void CreateWatcher(string path)
        {
            DisposeWatcher();
            try
            {
                _watcher = new FileSystemWatcher();
                _watcher.Path = path;
                _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                                        NotifyFilters.Size | NotifyFilters.CreationTime;
                _watcher.IncludeSubdirectories = false;
                _watcher.InternalBufferSize = 8192;
                _watcher.Created += delegate { Schedule(); };
                _watcher.Deleted += delegate { Schedule(); };
                _watcher.Renamed += delegate { Schedule(); };
                _watcher.Changed += delegate { Schedule(); };
                _watcher.Error += delegate { };
                _watcher.EnableRaisingEvents = true;
                App.Log("watcher started on " + path);
            }
            catch (Exception ex)
            {
                App.Log("watcher init failed on " + path + ": " + ex.Message);
                if (_watcher != null) { try { _watcher.Dispose(); } catch { } }
                _watcher = null;
            }
        }

        private void DisposeWatcher()
        {
            if (_watcher != null)
            {
                try { _watcher.EnableRaisingEvents = false; } catch { }
                try { _watcher.Dispose(); } catch { }
                _watcher = null;
            }
        }

        public void Dispose()
        {
            DisposeWatcher();
        }

        private void Schedule()
        {
            try
            {
                _dispatcher.BeginInvoke(new Action(delegate
                {
                    _debounce.Stop();
                    _debounce.Start();
                }));
            }
            catch { }
        }
    }
}
