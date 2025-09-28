using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NativeBrowser
{
    /// &lt;summary&gt;
    /// Interaction logic for MainWindow.xaml
    /// &lt;/summary&gt;
    public partial class MainWindow : Window
    {
        private bool _isMaximized = false;
        private Rect _restoreBounds;

        public MainWindow()
        {
            InitializeComponent();
            
            // Enable window dragging by mouse
            this.MouseLeftButtonDown += MainWindow_MouseLeftButtonDown;
            
            // Handle window state changes
            this.StateChanged += MainWindow_StateChanged;
            
            // Set initial window state
            UpdateMaximizeRestoreButton();
            
            // Handle source initialization for custom window behavior
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        #region Window Dragging and Resizing

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow window dragging when clicking on the title area
            var titleGrid = sender as FrameworkElement;
            if (titleGrid?.Name != "TitleText" && e.GetPosition(this).Y <= 40)
            {
                if (e.ClickCount == 2)
                {
                    // Double-click to maximize/restore
                    MaximizeRestoreWindow();
                }
                else
                {
                    // Single click to drag
                    this.DragMove();
                }
            }
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            // Get handle for this window
            IntPtr handle = new WindowInteropHelper(this).Handle;
            
            // Add hook for window messages
            HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTCAPTION = 2;

            if (msg == WM_NCHITTEST)
            {
                // Get cursor position relative to window
                var point = new Point();
                GetCursorPos(ref point);
                ScreenToClient(hwnd, ref point);

                // Check if cursor is in title bar area (for dragging)
                if (point.Y <= 40 && point.X < this.Width - 138) // 138 = width of 3 control buttons
                {
                    handled = true;
                    return new IntPtr(HTCAPTION);
                }
            }

            return IntPtr.Zero;
        }

        #endregion

        #region Window Control Button Events

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            MaximizeRestoreWindow();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MaximizeRestoreWindow()
        {
            if (_isMaximized)
            {
                // Restore window
                this.WindowState = WindowState.Normal;
                this.Left = _restoreBounds.Left;
                this.Top = _restoreBounds.Top;
                this.Width = _restoreBounds.Width;
                this.Height = _restoreBounds.Height;
                _isMaximized = false;
            }
            else
            {
                // Store current bounds
                _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                
                // Maximize window
                var workingArea = SystemParameters.WorkArea;
                this.Left = workingArea.Left;
                this.Top = workingArea.Top;
                this.Width = workingArea.Width;
                this.Height = workingArea.Height;
                _isMaximized = true;
            }
            
            UpdateMaximizeRestoreButton();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                _isMaximized = true;
            }
            else if (this.WindowState == WindowState.Normal)
            {
                _isMaximized = false;
            }
            
            UpdateMaximizeRestoreButton();
        }

        private void UpdateMaximizeRestoreButton()
        {
            if (MaximizeIcon != null)
            {
                if (_isMaximized)
                {
                    // Show restore icon (two overlapping rectangles)
                    MaximizeIcon.Data = Geometry.Parse("M2,0 L8,0 L8,6 L6,6 L6,2 L2,2 Z M0,2 L0,8 L6,8 L6,6 L2,6 L2,2 Z");
                    MaximizeButton.ToolTip = "Restore";
                }
                else
                {
                    // Show maximize icon (single rectangle)
                    MaximizeIcon.Data = Geometry.Parse("M0,2 L0,10 L8,10 L8,2 Z M0,2 L8,2");
                    MaximizeButton.ToolTip = "Maximize";
                }
            }
        }

        #endregion

        #region Navigation Events

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement back navigation
            StatusText.Text = "Back button clicked";
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement forward navigation
            StatusText.Text = "Forward button clicked";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement refresh
            StatusText.Text = "Refresh button clicked";
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // TODO: Navigate to URL
                StatusText.Text = $"Navigating to: {AddressBar.Text}";
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show menu
            StatusText.Text = "Menu button clicked";
        }

        #endregion

        #region Win32 API Declarations

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref Point lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref Point lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        #endregion
    }
}