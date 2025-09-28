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
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Globalization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NativeBrowser.Services;
using Newtonsoft.Json;
using System.Windows.Documents;

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
        private bool _isSidePanelOpen = false;
        private GroqAIService _aiService;
        private string _pendingJavaScript = null;

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
            
            // Initialize AI service
            try
            {
                _aiService = new GroqAIService();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"AI Service initialization failed: {ex.Message}";
            }
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
            if (_currentTab?.WebView != null && _currentTab.WebView.CanGoBack)
            {
                _currentTab.WebView.GoBack();
                StatusText.Text = "Navigating back";
            }
            else
            {
                StatusText.Text = "Cannot go back";
            }
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab?.WebView != null && _currentTab.WebView.CanGoForward)
            {
                _currentTab.WebView.GoForward();
                StatusText.Text = "Navigating forward";
            }
            else
            {
                StatusText.Text = "Cannot go forward";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab?.WebView != null)
            {
                _currentTab.WebView.Reload();
                StatusText.Text = "Refreshing page";
            }
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
            ToggleSidePanel();
        }
        
        private void ToggleSidePanel()
        {
            _isSidePanelOpen = !_isSidePanelOpen;
            
            if (_isSidePanelOpen)
            {
                OpenSidePanel();
            }
            else
            {
                CloseSidePanel();
            }
        }
        
        private void OpenSidePanel()
        {
            // Animate side panel opening
            var widthAnimation = new DoubleAnimation
            {
                From = 0,
                To = 300,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            SidePanel.BeginAnimation(Border.WidthProperty, widthAnimation);
            
            // Transform menu icon to chevron
            HamburgerIcon.Visibility = Visibility.Collapsed;
            ChevronIcon.Visibility = Visibility.Visible;
            
            StatusText.Text = "Side panel opened";
        }
        
        private void CloseSidePanel()
        {
            // Animate side panel closing
            var widthAnimation = new DoubleAnimation
            {
                From = 300,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            SidePanel.BeginAnimation(Border.WidthProperty, widthAnimation);
            
            // Transform chevron back to hamburger icon
            ChevronIcon.Visibility = Visibility.Collapsed;
            HamburgerIcon.Visibility = Visibility.Visible;
            
            StatusText.Text = "Side panel closed";
        }

        #endregion
        
        #region Message Input Handlers
        
        private void MessageInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (MessageInput.Text == "Message Optica...")
            {
                MessageInput.Text = "";
                MessageInput.Foreground = new SolidColorBrush(Colors.White);
                MessageInput.Height = 34; // Reset to minimum height
                MessageInput.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
        }
        
        private void MessageInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MessageInput.Text))
            {
                MessageInput.Text = "Message Optica...";
                MessageInput.Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                MessageInput.Height = 34; // Reset to minimum height
                MessageInput.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
        }
        
        private void MessageInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                e.Handled = true;
                SendMessage();
            }
        }
        
        private void MessageInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MessageInput.Text != "Message Optica...")
            {
                AutoResizeMessageInput();
            }
        }
        
        private void AutoResizeMessageInput()
        {
            var textBox = MessageInput;
            
            // Calculate line height (font size + some padding)
            double lineHeight = textBox.FontSize * 1.3; // Standard line height multiplier
            
            // Count the number of lines in the text
            int lineCount = Math.Max(1, textBox.Text.Split('\n').Length);
            
            // Calculate height for the content
            double contentHeight = lineCount * lineHeight;
            double totalHeight = contentHeight + textBox.Padding.Top + textBox.Padding.Bottom;
            
            // Maximum height for 10 lines
            double maxHeightFor10Lines = (10 * lineHeight) + textBox.Padding.Top + textBox.Padding.Bottom;
            
            // Determine final height and scrollbar visibility
            double newHeight;
            if (lineCount <= 10)
            {
                // Show full content, no scrollbar
                newHeight = Math.Max(34, totalHeight);
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
            else
            {
                // Cap at 10 lines height, show scrollbar
                newHeight = maxHeightFor10Lines;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            
            // Animate the height change
            var heightAnimation = new DoubleAnimation
            {
                From = textBox.ActualHeight,
                To = newHeight,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            textBox.BeginAnimation(TextBox.HeightProperty, heightAnimation);
        }
        
        private void AttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Attachment button clicked";
            // TODO: Implement file attachment functionality
        }
        
        private void MicrophoneButton_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Microphone button clicked";
            // TODO: Implement voice recording functionality
        }
        
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }
        
        private async void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(MessageInput.Text) && MessageInput.Text != "Message Optica...")
            {
                string userMessage = MessageInput.Text;
                
                // Add user message to chat
                AddUserMessage(userMessage);
                
                // Clear input
                MessageInput.Text = "";
                MessageInput.Height = 34;
                MessageInput.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                MessageInput_LostFocus(null, null);
                
                // Process with AI
                await ProcessAIMessage(userMessage);
            }
        }

        #endregion
        
        #region AI Integration
        
        private readonly string[] _browserControlKeywords = { "show", "play", "research", "find" };
        
        private bool IsBrowserControlRequest(string message)
        {
            var lowerMessage = message.ToLower();
            return Array.Exists(_browserControlKeywords, keyword => lowerMessage.Contains(keyword));
        }
        
        private async Task ProcessAIMessage(string userMessage)
        {
            if (_aiService == null)
            {
                AddAIMessage("AI service not available", "Error");
                return;
            }
            
            try
            {
                if (IsBrowserControlRequest(userMessage))
                {
                    // Highlight keywords in user message
                    HighlightBrowserKeywords(userMessage);
                    
                    // Process as browser control request
                    StatusText.Text = "AI is thinking...";
                    string response = await _aiService.GetBrowserActionResponse(userMessage);
                    
                    // Clean the response and check if it's valid JSON
                    string cleanedResponse = response.Trim();
                    if (cleanedResponse.StartsWith("```json"))
                    {
                        cleanedResponse = cleanedResponse.Substring(7);
                    }
                    if (cleanedResponse.EndsWith("```"))
                    {
                        cleanedResponse = cleanedResponse.Substring(0, cleanedResponse.Length - 3);
                    }
                    cleanedResponse = cleanedResponse.Trim();
                    
                    // Add cleaned response for debugging
                    AddAIMessage($"AI Response: {cleanedResponse}", "Debug");
                    
                    // Check if response starts with valid JSON
                    if (!cleanedResponse.StartsWith("{"))
                    {
                        AddAIMessage("AI did not return valid JSON format. Creating fallback action.", "Error");
                        
                        // Create fallback action based on user message
                        var fallbackAction = CreateFallbackAction(userMessage);
                        ExecuteFallbackAction(fallbackAction);
                        return;
                    }
                    
                    // Parse JSON response
                    try
                    {
                        var actionResponse = JsonConvert.DeserializeObject<BrowserActionResponse>(cleanedResponse);
                        
                        if (actionResponse == null)
                        {
                            AddAIMessage("Failed to parse AI response as JSON", "Error");
                            AddAIMessage(response, "Response");
                            return;
                        }
                        
                        // Display AI thinking, action, and explanation
                        if (!string.IsNullOrEmpty(actionResponse.Thinking))
                            AddAIMessage(actionResponse.Thinking, "Thinking");
                        
                        if (!string.IsNullOrEmpty(actionResponse.Action))
                            AddAIMessage(actionResponse.Action, "Action");
                        
                        if (!string.IsNullOrEmpty(actionResponse.Explanation))
                            AddAIMessage(actionResponse.Explanation, "Reasoning");
                        
                        // Navigate to URL if provided
                        if (!string.IsNullOrEmpty(actionResponse.Url))
                        {
                            AddAIMessage($"Attempting navigation to: {actionResponse.Url}", "Debug");
                            NavigateToUrl(actionResponse.Url);
                            
                            // Queue JavaScript for execution after navigation
                            if (!string.IsNullOrEmpty(actionResponse.Javascript))
                            {
                                AddAIMessage($"Will execute JavaScript after page loads: {actionResponse.Javascript}", "Debug");
                                _pendingJavaScript = actionResponse.Javascript;
                            }
                            
                            StatusText.Text = $"Navigating to: {actionResponse.Url}";
                        }
                        else
                        {
                            AddAIMessage("No URL provided in AI response", "Error");
                        }
                    }
                    catch (Exception parseEx)
                    {
                        // Fallback if JSON parsing fails
                        AddAIMessage($"JSON Parse Error: {parseEx.Message}", "Error");
                        AddAIMessage(response, "Action");
                    }
                }
                else
                {
                    // Process as Q&A request
                    StatusText.Text = "AI is responding...";
                    string response = await _aiService.GetQAResponse(userMessage);
                    AddAIMessage(response, "Response");
                    StatusText.Text = "AI response complete";
                }
            }
            catch (Exception ex)
            {
                AddAIMessage($"Error: {ex.Message}", "Error");
                StatusText.Text = "AI request failed";
            }
        }
        
        private void AddUserMessage(string message)
        {
            var messageBlock = new TextBlock
            {
                Text = $"You: {message}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            
            ChatMessagesPanel.Children.Add(messageBlock);
            ScrollToBottom();
        }
        
        private void AddAIMessage(string message, string messageType)
        {
            var headerColor = messageType switch
            {
                "Thinking" => Color.FromRgb(0x4A, 0x9E, 0xFF), // Blue
                "Action" => Color.FromRgb(0x4C, 0xAF, 0x50),   // Green
                "Reasoning" => Color.FromRgb(0xFF, 0x98, 0x00), // Orange
                "Response" => Color.FromRgb(0xFF, 0xFF, 0xFF),  // White
                "Error" => Color.FromRgb(0xF4, 0x43, 0x36),     // Red
                "Debug" => Color.FromRgb(0x9C, 0x27, 0xB0),     // Purple
                _ => Color.FromRgb(0xFF, 0xFF, 0xFF)
            };
            
            var headerBlock = new TextBlock
            {
                Text = $"AI {messageType}:",
                Foreground = new SolidColorBrush(headerColor),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 8, 0, 2)
            };
            
            var messageBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            
            ChatMessagesPanel.Children.Add(headerBlock);
            ChatMessagesPanel.Children.Add(messageBlock);
            ScrollToBottom();
        }
        
        private void HighlightBrowserKeywords(string message)
        {
            // Find the last user message and update it with highlighted keywords
            if (ChatMessagesPanel.Children.Count > 0)
            {
                var lastMessage = ChatMessagesPanel.Children[ChatMessagesPanel.Children.Count - 1] as TextBlock;
                if (lastMessage != null && lastMessage.Text.StartsWith("You:"))
                {
                    // Create new TextBlock with highlighted keywords
                    var highlightedBlock = new TextBlock
                    {
                        FontSize = 13,
                        FontFamily = new FontFamily("Segoe UI"),
                        Margin = new Thickness(0, 0, 0, 8),
                        TextWrapping = TextWrapping.Wrap
                    };
                    
                    var run1 = new Run("You: ") { Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)) };
                    highlightedBlock.Inlines.Add(run1);
                    
                    string[] words = message.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        var word = words[i];
                        var isKeyword = _browserControlKeywords.Any(k => word.ToLower().Contains(k.ToLower()));
                        
                        var run = new Run(word)
                        {
                            Foreground = isKeyword ? 
                                new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)) : // Blue for keywords
                                new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA))   // Gray for normal text
                        };
                        
                        highlightedBlock.Inlines.Add(run);
                        
                        if (i < words.Length - 1)
                            highlightedBlock.Inlines.Add(new Run(" "));
                    }
                    
                    // Replace the last message
                    ChatMessagesPanel.Children.RemoveAt(ChatMessagesPanel.Children.Count - 1);
                    ChatMessagesPanel.Children.Add(highlightedBlock);
                }
            }
        }
        
        private void ScrollToBottom()
        {
            ChatScrollViewer.ScrollToEnd();
        }
        
        private BrowserActionResponse CreateFallbackAction(string userMessage)
        {
            var lowerMessage = userMessage.ToLower();
            
            // Determine action based on keywords
            if (lowerMessage.Contains("video") || lowerMessage.Contains("play"))
            {
                var searchTerm = ExtractSearchTerm(userMessage, new[] { "play", "video", "show" });
                return new BrowserActionResponse
                {
                    Thinking = $"User wants to watch a video about '{searchTerm}'. I'll search YouTube for this.",
                    Action = $"Searching YouTube for '{searchTerm}' videos",
                    Url = $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(searchTerm)}",
                    Javascript = "",
                    Explanation = $"YouTube is the best platform for finding videos about '{searchTerm}'"
                };
            }
            else if (lowerMessage.Contains("research") || lowerMessage.Contains("learn") || lowerMessage.Contains("information"))
            {
                var searchTerm = ExtractSearchTerm(userMessage, new[] { "research", "learn", "information", "about" });
                return new BrowserActionResponse
                {
                    Thinking = $"User wants to research '{searchTerm}'. I'll use Wikipedia for reliable information.",
                    Action = $"Looking up '{searchTerm}' on Wikipedia",
                    Url = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(searchTerm.Replace(" ", "_"))}",
                    Javascript = "",
                    Explanation = $"Wikipedia provides comprehensive information about '{searchTerm}'"
                };
            }
            else if (lowerMessage.Contains("find") || lowerMessage.Contains("search"))
            {
                var searchTerm = ExtractSearchTerm(userMessage, new[] { "find", "search", "look", "for" });
                return new BrowserActionResponse
                {
                    Thinking = $"User wants to find information about '{searchTerm}'. I'll use Google search.",
                    Action = $"Searching Google for '{searchTerm}'",
                    Url = $"https://www.google.com/search?q={Uri.EscapeDataString(searchTerm)}",
                    Javascript = "",
                    Explanation = $"Google search will provide comprehensive results for '{searchTerm}'"
                };
            }
            else if (lowerMessage.Contains("show") || lowerMessage.Contains("display"))
            {
                var searchTerm = ExtractSearchTerm(userMessage, new[] { "show", "display", "me" });
                return new BrowserActionResponse
                {
                    Thinking = $"User wants to see '{searchTerm}'. I'll search Google Images for visual content.",
                    Action = $"Showing images of '{searchTerm}'",
                    Url = $"https://www.google.com/search?tbm=isch&q={Uri.EscapeDataString(searchTerm)}",
                    Javascript = "",
                    Explanation = $"Google Images will show visual content related to '{searchTerm}'"
                };
            }
            else
            {
                // Default to Google search
                return new BrowserActionResponse
                {
                    Thinking = $"User asked: '{userMessage}'. I'll search Google for this query.",
                    Action = $"Searching Google for '{userMessage}'",
                    Url = $"https://www.google.com/search?q={Uri.EscapeDataString(userMessage)}",
                    Javascript = "",
                    Explanation = "Using Google search as the default action for this request"
                };
            }
        }
        
        private string ExtractSearchTerm(string message, string[] keywords)
        {
            var lowerMessage = message.ToLower();
            
            // Find the keyword and extract text after it
            foreach (var keyword in keywords)
            {
                var keywordIndex = lowerMessage.IndexOf(keyword);
                if (keywordIndex >= 0)
                {
                    var afterKeyword = message.Substring(keywordIndex + keyword.Length).Trim();
                    
                    // Remove common words from the beginning
                    var commonWords = new[] { "a", "an", "the", "me", "about", "of", "for", "on" };
                    var words = afterKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    
                    var filteredWords = words.Where(w => !commonWords.Contains(w.ToLower())).ToArray();
                    
                    if (filteredWords.Length > 0)
                    {
                        return string.Join(" ", filteredWords);
                    }
                }
            }
            
            // If no keyword found, use the whole message (cleaned)
            var allWords = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commonStopWords = new[] { "can", "you", "please", "i", "want", "to", "would", "like" };
            var cleanedWords = allWords.Where(w => !commonStopWords.Contains(w.ToLower())).ToArray();
            
            return cleanedWords.Length > 0 ? string.Join(" ", cleanedWords) : message;
        }
        
        private void ExecuteFallbackAction(BrowserActionResponse action)
        {
            // Display the fallback action parts
            if (!string.IsNullOrEmpty(action.Thinking))
                AddAIMessage(action.Thinking, "Thinking");
            
            if (!string.IsNullOrEmpty(action.Action))
                AddAIMessage(action.Action, "Action");
            
            if (!string.IsNullOrEmpty(action.Explanation))
                AddAIMessage(action.Explanation, "Reasoning");
            
            // Navigate to URL
            if (!string.IsNullOrEmpty(action.Url))
            {
                AddAIMessage($"Navigating to: {action.Url}", "Debug");
                NavigateToUrl(action.Url);
                StatusText.Text = $"Navigating to: {action.Url}";
            }
        }
        
        private async Task ExecutePendingJavaScript()
        {
            if (string.IsNullOrEmpty(_pendingJavaScript))
                return;
                
            var javascript = _pendingJavaScript;
            _pendingJavaScript = null; // Clear pending JavaScript
            
            var webView = _currentTab?.WebView ?? WebView;
            if (webView?.CoreWebView2 != null)
            {
                try
                {
                    // Add a delay to ensure page content is fully loaded
                    await Task.Delay(3000);
                    
                    AddAIMessage($"Executing JavaScript: {javascript}", "Debug");
                    var result = await webView.CoreWebView2.ExecuteScriptAsync(javascript);
                    AddAIMessage($"JavaScript executed successfully. Result: {result}", "Debug");
                }
                catch (Exception ex)
                {
                    AddAIMessage($"JavaScript execution failed: {ex.Message}", "Error");
                }
            }
            else
            {
                AddAIMessage("WebView2 not ready for JavaScript execution", "Error");
            }
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
                if (_currentTab?.WebView != null)
                {
                    // Ensure URL has a protocol
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        url = "https://" + url;
                    }

                    _currentTab.WebView.Source = new Uri(url);
                    _currentTab.Url = url;
                    StatusText.Text = $"Navigating to: {url}";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Navigation failed: {ex.Message}";
            }
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (_currentTab?.WebView == sender)
            {
                StatusText.Text = $"Loading: {e.Uri}";
                AddressBar.Text = e.Uri;
                
                // Update navigation button states
                Dispatcher.BeginInvoke(() =>
                {
                    BackButton.IsEnabled = _currentTab.WebView.CanGoBack;
                    ForwardButton.IsEnabled = _currentTab.WebView.CanGoForward;
                });
            }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_currentTab?.WebView == sender)
            {
                if (e.IsSuccess)
                {
                    StatusText.Text = "Page loaded successfully";
                    
                    // Execute pending JavaScript if any
                    if (!string.IsNullOrEmpty(_pendingJavaScript))
                    {
                        await ExecutePendingJavaScript();
                    }
                }
                else
                {
                    StatusText.Text = $"Navigation failed: {e.WebErrorStatus}";
                }
                
                // Update navigation button states
                BackButton.IsEnabled = _currentTab.WebView.CanGoBack;
                ForwardButton.IsEnabled = _currentTab.WebView.CanGoForward;
                
                // Update address bar with final URL
                if (_currentTab.WebView.Source != null)
                {
                    AddressBar.Text = _currentTab.WebView.Source.ToString();
                }
            }
        }

        #endregion

        #region Tab Management

        private void InitializeFirstTab()
        {
            var firstTab = new TabItem
            {
                Title = "Google",
                Url = "https://google.com/",
                WebView = WebView,
                TabButton = DefaultTab,
                IsActive = true
            };

            _tabs.Add(firstTab);
            _currentTab = firstTab;
            
            // Ensure the default tab shows as active
            DefaultTab.Tag = "Active";
            
            // Set up close button click handler for the default tab
            SetupTabCloseHandler(DefaultTab, firstTab);
        }

        private void NewTab_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab("New Tab", "https://google.com/");
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            // Only handle clicks if the sender is the tab button itself
            if (sender is Button clickedButton)
            {
                // Check if the click came from the close button - don't switch tabs
                if (e.OriginalSource is Button closeBtn && closeBtn.Name == "CloseTabButton")
                {
                    StatusText.Text = "Close button clicked - not switching tabs";
                    return; // Let the close button handler deal with it
                }
                
                // Check if click came from somewhere inside close button
                var element = e.OriginalSource as FrameworkElement;
                while (element != null)
                {
                    if (element is Button btn && btn.Name == "CloseTabButton")
                    {
                        StatusText.Text = "Close button area clicked - not switching tabs";
                        return;
                    }
                    element = element.Parent as FrameworkElement;
                }
                
                var tab = _tabs.FirstOrDefault(t => t.TabButton == clickedButton);
                if (tab != null)
                {
                    StatusText.Text = $"Switching to tab: {tab.Title} (Button: {clickedButton.Content})";
                    SwitchToTab(tab);
                }
                else
                {
                    StatusText.Text = $"Tab not found for button: {clickedButton.Content}";
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
            newWebView.NavigationStarting += (s, e) => 
            {
                if (_currentTab?.WebView == s)
                {
                    WebView_NavigationStarting(s, e);
                }
            };
            newWebView.NavigationCompleted += (s, e) => 
            {
                if (_currentTab?.WebView == s)
                {
                    WebView_NavigationCompleted(s, e);
                }
            };

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
            if (_currentTab == tab) 
            {
                StatusText.Text = $"Already on tab: {tab.Title}";
                return;
            }

            StatusText.Text = $"Switching from '{_currentTab?.Title ?? "none"}' to '{tab.Title}'";

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
            else
            {
                AddressBar.Text = tab.Url;
            }

            // Update navigation buttons
            BackButton.IsEnabled = tab.WebView.CanGoBack;
            ForwardButton.IsEnabled = tab.WebView.CanGoForward;

            StatusText.Text = $"Now on tab: {tab.Title}";
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
                    closeButton.Click += (sender, args) =>
                    {
                        args.Handled = true; // Prevent event bubbling to tab button
                        StatusText.Text = $"Closing tab: {tab.Title}";
                        CloseTab(tab);
                    };
                }
                else
                {
                    StatusText.Text = $"Close button not found for tab: {tab.Title}";
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

        protected override void OnClosed(EventArgs e)
        {
            _aiService?.Dispose();
            base.OnClosed(e);
        }
    }
}