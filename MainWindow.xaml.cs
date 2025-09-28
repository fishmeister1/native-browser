using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace NativeBrowser
{
    /// &lt;summary&gt;
    /// Interaction logic for MainWindow.xaml
    /// &lt;/summary&gt;
    public partial class MainWindow : Window
    {
        private bool _isMaximized = false;
        private Rect _restoreBounds;
        private List<TabItem> _tabs = new List<TabItem>();
        private TabItem _currentTab = null;

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
            
            // Initialize WebView2 and first tab
            InitializeWebView();
            InitializeFirstTab();
            
            // Set up keyboard shortcuts
            this.KeyDown += MainWindow_KeyDown;
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
            if (WebView.CanGoBack)
            {
                WebView.GoBack();
                StatusText.Text = "Navigating back";
            }
            else
            {
                StatusText.Text = "Cannot go back";
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebView.CanGoForward)
            {
                WebView.GoForward();
                StatusText.Text = "Navigating forward";
            }
            else
            {
                StatusText.Text = "Cannot go forward";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            WebView.Reload();
            StatusText.Text = "Refreshing page";
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToUrl(AddressBar.Text);
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Show menu
            StatusText.Text = "Menu button clicked";
        }

        #endregion

        #region WebView2 Methods

        private async void InitializeWebView()
        {
            try
            {
                await WebView.EnsureCoreWebView2Async();
                
                // Set user agent to identify as a modern browser
                WebView.CoreWebView2.Settings.UserAgent = 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 NativeBrowser/1.0";
                
                // Enable developer tools for debugging (optional)
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                
                // Enable password autosave
                WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = true;
                
                // Enable general autofill
                WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = true;
                
                StatusText.Text = "WebView2 initialized successfully";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"WebView2 initialization failed: {ex.Message}";
            }
        }

        private void NavigateToUrl(string url)
        {
            try
            {
                // Ensure URL has a protocol
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                WebView.Source = new Uri(url);
                StatusText.Text = $"Navigating to: {url}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Navigation failed: {ex.Message}";
            }
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            StatusText.Text = $"Loading: {e.Uri}";
            AddressBar.Text = e.Uri;
            
            // Update navigation button states
            Dispatcher.BeginInvoke(() =>
            {
                BackButton.IsEnabled = WebView.CanGoBack;
                ForwardButton.IsEnabled = WebView.CanGoForward;
            });
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                StatusText.Text = "Page loaded successfully";
            }
            else
            {
                StatusText.Text = $"Navigation failed: {e.WebErrorStatus}";
            }
            
            // Update navigation button states
            BackButton.IsEnabled = WebView.CanGoBack;
            ForwardButton.IsEnabled = WebView.CanGoForward;
            
            // Update address bar with final URL
            if (WebView.Source != null)
            {
                AddressBar.Text = WebView.Source.ToString();
            }
        }

        #endregion

        #region Tab Management

        private void InitializeFirstTab()
        {
            var firstTab = new TabItem
            {
                Title = "OpticaAI",
                Url = "https://opticaai.org/",
                WebView = WebView,
                TabButton = DefaultTab,
                IsActive = true
            };

            _tabs.Add(firstTab);
            _currentTab = firstTab;
            
            // Set up close button click handler for the default tab
            SetupTabCloseHandler(DefaultTab, firstTab);
        }

        private void NewTab_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab("New Tab", "https://opticaai.org/");
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            // Only handle clicks if the sender is the tab button itself
            if (sender is Button clickedButton)
            {
                // Check if the click came from the close button
                if (e.OriginalSource is Button closeBtn && closeBtn.Name == "CloseTabButton")
                {
                    return; // Let the close button handler deal with it
                }
                
                var tab = _tabs.FirstOrDefault(t => t.TabButton == clickedButton);
                if (tab != null)
                {
                    SwitchToTab(tab);
                }
            }
        }

        private TabItem CreateNewTab(string title, string url)
        {
            // Create new WebView2 control
            var newWebView = new WebView2();
            
            // Create new tab button
            var tabButton = new Button
            {
                Content = title,
                Style = (Style)FindResource("TabItemStyle"),
                Tag = "Inactive"
            };

            // Create tab item
            var newTab = new TabItem
            {
                Title = title,
                Url = url,
                WebView = newWebView,
                TabButton = tabButton,
                IsActive = false
            };

            // Set up event handlers
            tabButton.Click += Tab_Click;
            SetupTabCloseHandler(tabButton, newTab);
            
            // Set up WebView event handlers
            newWebView.NavigationStarting += WebView_NavigationStarting;
            newWebView.NavigationCompleted += WebView_NavigationCompleted;

            // Add tab button to container (before the new tab button)
            int newTabButtonIndex = TabContainer.Children.IndexOf(NewTabButton);
            TabContainer.Children.Insert(newTabButtonIndex, tabButton);

            // Add to tabs collection
            _tabs.Add(newTab);

            // Switch to the new tab
            SwitchToTab(newTab);

            // Initialize the WebView and navigate
            Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    await newWebView.EnsureCoreWebView2Async();
                    newWebView.Source = new Uri(url);
                });
            });

            return newTab;
        }

        private void SwitchToTab(TabItem tab)
        {
            if (_currentTab == tab) return;

            // Hide current WebView
            if (_currentTab != null)
            {
                _currentTab.IsActive = false;
                _currentTab.TabButton.Tag = "Inactive";
                _currentTab.WebView.Visibility = Visibility.Hidden;
            }

            // Show new tab's WebView
            _currentTab = tab;
            tab.IsActive = true;
            tab.TabButton.Tag = "Active";
            
            // Replace WebView in the content area
            var contentBorder = WebViewContainer;
            if (contentBorder != null)
            {
                contentBorder.Child = tab.WebView;
                tab.WebView.Visibility = Visibility.Visible;
            }

            // Update address bar
            if (tab.WebView.Source != null)
            {
                AddressBar.Text = tab.WebView.Source.ToString();
            }

            // Update navigation buttons
            BackButton.IsEnabled = tab.WebView.CanGoBack;
            ForwardButton.IsEnabled = tab.WebView.CanGoForward;

            StatusText.Text = $"Switched to tab: {tab.Title}";
        }

        private Border GetWebViewContainer()
        {
            // This method finds the border that contains the WebView
            // We'll need to access the content area border
            return FindVisualChild<Border>(this);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childItem = FindVisualChild<T>(child);
                    if (childItem != null)
                        return childItem;
                }
            }
            return null;
        }

        private void SetupTabCloseHandler(Button tabButton, TabItem tab)
        {
            // Find the close button within the tab button template
            tabButton.Loaded += (s, e) =>
            {
                tabButton.ApplyTemplate();
                var closeButton = FindVisualChildByName<Button>(tabButton, "CloseTabButton");
                if (closeButton != null)
                {
                    closeButton.PreviewMouseLeftButtonDown += (sender, args) =>
                    {
                        args.Handled = true; // Prevent event bubbling to tab button
                        CloseTab(tab);
                    };
                }
            };
        }

        private static T FindVisualChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T element && element.Name == name)
                    return element;
                else
                {
                    T childItem = FindVisualChildByName<T>(child, name);
                    if (childItem != null)
                        return childItem;
                }
            }
            return null;
        }

        private void CloseTab(TabItem tab)
        {
            if (_tabs.Count <= 1)
            {
                // Don't close the last tab, instead create a new one
                tab.Title = "New Tab";
                tab.Url = "about:blank";
                tab.TabButton.Content = "New Tab";
                tab.WebView.Source = null;
                return;
            }

            // Remove from UI
            TabContainer.Children.Remove(tab.TabButton);
            
            // Remove from collection
            _tabs.Remove(tab);

            // If this was the active tab, switch to another
            if (tab.IsActive)
            {
                var nextTab = _tabs.FirstOrDefault();
                if (nextTab != null)
                {
                    SwitchToTab(nextTab);
                }
            }

            // Dispose of WebView
            tab.WebView?.Dispose();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+T for new tab
            if (e.Key == Key.T && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                CreateNewTab("New Tab", "https://opticaai.org/");
                e.Handled = true;
            }
            // Ctrl+W for close tab
            else if (e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (_currentTab != null)
                {
                    CloseTab(_currentTab);
                }
                e.Handled = true;
            }
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