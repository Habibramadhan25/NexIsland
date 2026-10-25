using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using NexIsland.Services;
using NexIsland.ViewModels;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using Slider = System.Windows.Controls.Slider;

namespace NexIsland
{
    public partial class MainWindow : Window
    {
        private bool _hasBeenActivatedOnce = false;

        // Swipe & Gesture recognition state
        private Point _dragStartPoint;
        private bool _isMouseDown;
        private bool _swipedThisGesture;
        private DateTime _lastWheelTime = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            var s = Settings.SettingsService.Instance.Current;
            Width = ViewModel.Width > 0 ? ViewModel.Width : s.ExpandedWidth;
            Height = ViewModel.Height > 0 ? ViewModel.Height : s.ExpandedHeight;

            ViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ViewModel.Width) || args.PropertyName == nameof(ViewModel.Height))
                {
                    Dispatcher.Invoke(() =>
                    {
                        Width = ViewModel.Width;
                        Height = ViewModel.Height;
                        UpdatePosition();
                    });
                }
            };

            Activated += (sender, args) => _hasBeenActivatedOnce = true;
            Loaded += OnMainWindowLoaded;
        }

        private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            // Position at top center of current monitor
            UpdatePosition();

            // Set tool window style so NexIsland does not clutter the taskbar
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW);

            // Bring to topmost and force render in Win32
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, (int)Left, (int)Top, (int)Width, (int)Height, NativeMethods.SWP_SHOWWINDOW);

            // Initialize System Tray icon in notification area (bottom right)
            TrayService.Instance.Initialize(this);

            // Sync startup with Windows preference
            if (Settings.SettingsService.Instance.Current.StartWithWindows && !StartupService.IsStartupEnabled())
            {
                StartupService.SetStartup(true);
            }

            // Apply theme
            ThemeService.Instance.ApplyTheme(Settings.SettingsService.Instance.Current);

            // Focusable for keyboard navigation
            Focusable = true;

            // Initialize all core services asynchronously
            await ViewModel.InitializeServicesAsync(this);
        }

        public void UpdatePosition()
        {
            var s = Settings.SettingsService.Instance.Current;
            var (left, top) = ScreenHelper.CalculateTopCenterPosition(s.SelectedMonitorIndex, Width, s.YOffset, this);
            Left = left + s.XOffset;
            Top = top;
        }

        #region Swipe Navigation & Gesture Recognition
        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Ignore swipe initiation if clicking directly on interactive inputs (sliders, buttons, textboxes)
            if (IsInteractiveControl(e.OriginalSource as DependencyObject))
            {
                return;
            }

            _dragStartPoint = e.GetPosition(this);
            _isMouseDown = true;
            _swipedThisGesture = false;
        }

        private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || _swipedThisGesture) return;

            var currentPoint = e.GetPosition(this);
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            var s = Settings.SettingsService.Instance.Current;
            double threshold = s.SwipeThreshold > 10 ? s.SwipeThreshold : 40.0;

            // Horizontal swipe dominance check
            if (Math.Abs(deltaX) > threshold && Math.Abs(deltaX) > Math.Abs(deltaY) * 1.5)
            {
                _swipedThisGesture = true;

                if (deltaX < 0)
                {
                    // Swiped Left -> Next Module
                    ViewModel.NextModule();
                }
                else
                {
                    // Swiped Right -> Previous Module
                    ViewModel.PreviousModule();
                }

                e.Handled = true;
            }
        }

        private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMouseDown && !_swipedThisGesture)
            {
                var endPoint = e.GetPosition(this);
                double dist = (endPoint - _dragStartPoint).Length;

                // If it was a quick click without swipe:
                if (dist < 8.0)
                {
                    if (ViewModel.IsCompact)
                    {
                        // Expand island on click in compact mode
                        ViewModel.State = IslandVisualState.Expanded;
                        UpdatePosition();
                    }
                }
            }

            _isMouseDown = false;
            _swipedThisGesture = false;
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var s = Settings.SettingsService.Instance.Current;
            if (!s.EnableMouseWheelNavigation) return;

            // Debounce wheel scroll to prevent wild multiple page skips
            var now = DateTime.UtcNow;
            if ((now - _lastWheelTime).TotalMilliseconds < 220) return;

            if (e.Delta < 0)
            {
                // Wheel down / scroll forward -> Next Module
                _lastWheelTime = now;
                ViewModel.NextModule();
                e.Handled = true;
            }
            else if (e.Delta > 0)
            {
                // Wheel up / scroll back -> Previous Module
                _lastWheelTime = now;
                ViewModel.PreviousModule();
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel.IsUniversalSearchOpen)
            {
                if (e.Key == Key.Escape)
                {
                    ViewModel.CloseUniversalSearch();
                    UpdatePosition();
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.Enter)
                {
                    var item = ViewModel.SelectedUniversalResult ?? (ViewModel.UniversalSearchResults.Count > 0 ? ViewModel.UniversalSearchResults[0] : null);
                    if (item != null)
                    {
                        ViewModel.ExecuteUniversalSearchResultCommand.Execute(item);
                        e.Handled = true;
                        return;
                    }
                }
                else if (e.Key == Key.Down)
                {
                    var list = ViewModel.UniversalSearchResults;
                    if (list.Count > 0)
                    {
                        int idx = ViewModel.SelectedUniversalResult != null ? list.IndexOf(ViewModel.SelectedUniversalResult) : -1;
                        if (idx < list.Count - 1)
                        {
                            ViewModel.SelectedUniversalResult = list[idx + 1];
                            e.Handled = true;
                            return;
                        }
                    }
                }
                else if (e.Key == Key.Up)
                {
                    var list = ViewModel.UniversalSearchResults;
                    if (list.Count > 0)
                    {
                        int idx = ViewModel.SelectedUniversalResult != null ? list.IndexOf(ViewModel.SelectedUniversalResult) : 0;
                        if (idx > 0)
                        {
                            ViewModel.SelectedUniversalResult = list[idx - 1];
                            e.Handled = true;
                            return;
                        }
                    }
                }
                return;
            }

            // Left & Right arrow keys navigate modules when not typing in an input
            if (e.OriginalSource is not TextBox)
            {
                if (e.Key == Key.Right)
                {
                    ViewModel.NextModule();
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    ViewModel.PreviousModule();
                    e.Handled = true;
                }
            }

            if (e.Key == Key.Escape && ViewModel.IsExpanded)
            {
                ViewModel.State = IslandVisualState.Compact;
                UpdatePosition();
                e.Handled = true;
            }
        }

        private static bool IsInteractiveControl(DependencyObject? obj)
        {
            while (obj != null && !(obj is Window))
            {
                if (obj is Button || obj is Slider || obj is TextBox || obj is ScrollBar || obj is ProgressBar)
                {
                    return true;
                }
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            }
            return false;
        }
        #endregion

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Auto collapse when clicking outside only after it was interacted with
            if (_hasBeenActivatedOnce && ViewModel.IsExpanded)
            {
                ViewModel.State = IslandVisualState.Compact;
                UpdatePosition();
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    ViewModel.AddDroppedFiles(files);
                    NotificationService.Instance.PostNotification("Files Received",
                        $"Added {files.Length} file(s) to File Drop Zone.", "NexIsland File Drop", "📁");
                }
            }
        }

        #region Interactive Scrubber Seek
        private void MediaScrubber_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.CaptureMouse();
                ViewModel.BeginSeek();
                if (slider.ActualWidth > 0)
                {
                    double frac = Math.Clamp(e.GetPosition(slider).X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = frac;
                    ViewModel.UpdateSeekPreview(frac);
                }
                e.Handled = true;
            }
        }

        private void MediaScrubber_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Slider slider && slider.IsMouseCaptured)
            {
                if (slider.ActualWidth > 0)
                {
                    double frac = Math.Clamp(e.GetPosition(slider).X / slider.ActualWidth, 0.0, 1.0);
                    slider.Value = frac;
                    ViewModel.UpdateSeekPreview(frac);
                }
                e.Handled = true;
            }
        }

        private void MediaScrubber_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider && slider.IsMouseCaptured)
            {
                slider.ReleaseMouseCapture();
                if (slider.ActualWidth > 0)
                {
                    double frac = Math.Clamp(e.GetPosition(slider).X / slider.ActualWidth, 0.0, 1.0);
                    ViewModel.EndSeek(frac);
                }
                e.Handled = true;
            }
        }
        #endregion

        protected override void OnClosed(EventArgs e)
        {
            TrayService.Instance.Dispose();
            base.OnClosed(e);
        }
    }
}