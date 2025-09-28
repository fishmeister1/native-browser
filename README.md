# Native Browser

A custom native Windows application with modern dark gray theme, built using WPF (XAML and C#). Features a completely custom window chrome with custom title bar controls and a sleek, modern interface.

## Features

### Custom Window Controls
- **Frameless Window**: Complete removal of default Windows chrome
- **Custom Title Bar**: Dark gray theme with app icon, title, and control buttons
- **Window Controls**: Custom minimize, maximize/restore, and close buttons with hover effects
- **Window Management**: Full support for dragging, resizing, minimizing, and maximizing
- **Responsive Design**: Adapts to different window sizes

### Modern Dark Gray Theme
- **Consistent Color Scheme**: Professional dark gray color palette throughout
- **Custom Styling**: All controls styled to match the dark theme
- **Smooth Animations**: Hover and interaction effects on all interactive elements
- **High Contrast**: Optimized for readability and accessibility

### Browser-like Interface
- **Navigation Bar**: Back, forward, refresh buttons with modern icons
- **Address Bar**: Styled text input with focus effects and keyboard navigation
- **Content Area**: Placeholder for web content (ready for WebView integration)
- **Status Bar**: Information display at the bottom
- **Menu Button**: Extensible menu system

## Technologies Used

- **C#**: Backend logic and window behavior
- **XAML**: UI layout and structure
- **WPF**: Windows Presentation Foundation framework
- **CSS**: Design reference and styling concepts
- **.NET 6**: Target framework with Windows-specific features

## Project Structure

```
NativeBrowser/
├── App.xaml                    # Application entry point and resource references
├── App.xaml.cs                # Application code-behind
├── MainWindow.xaml             # Main window layout and controls
├── MainWindow.xaml.cs          # Window behavior and event handling
├── NativeBrowser.csproj        # Project configuration
├── Styles/
│   └── DarkTheme.xaml         # Dark theme color definitions and styles
├── Assets/
│   ├── styles.css             # CSS reference for styling concepts
│   └── README_Icon.md         # Icon placement instructions
└── README.md                  # This file
```

## Building and Running

### Prerequisites
- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **.NET 6.0 SDK** or later
- **Windows 10/11** (WPF is Windows-specific)

### Build Instructions

1. **Clone the repository**:
   ```bash
   git clone https://github.com/fishmeister1/native-browser.git
   cd native-browser
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build the application**:
   ```bash
   dotnet build
   ```

4. **Run the application**:
   ```bash
   dotnet run
   ```

### Alternative: Visual Studio
1. Open `NativeBrowser.csproj` in Visual Studio
2. Press F5 or click "Start Debugging"

## Customization

### Changing Colors
Edit `Styles/DarkTheme.xaml` to modify the color scheme:
- `WindowBackgroundBrush`: Main window background
- `TitleBarBackgroundBrush`: Title bar color
- `ContentBackgroundBrush`: Content area background
- `ControlButtonForegroundBrush`: Button text/icon color

### Adding Features
The application is structured for easy extension:
- **Navigation Logic**: Implement web navigation in `MainWindow.xaml.cs`
- **Menu System**: Extend the menu button functionality
- **Additional Controls**: Add new UI elements to `MainWindow.xaml`
- **Styling**: Create new styles in `DarkTheme.xaml`

## Key Features Implemented

### Window Management
- ✅ Custom window chrome removal
- ✅ Draggable title bar
- ✅ Custom minimize/maximize/close buttons
- ✅ Window resize support
- ✅ Proper maximize/restore behavior
- ✅ Window state management

### UI Components
- ✅ Modern navigation bar with icon buttons
- ✅ Styled address bar with focus effects
- ✅ Responsive layout system
- ✅ Status bar for information display
- ✅ Custom scrollbar styling

### Theme System
- ✅ Complete dark gray theme
- ✅ Consistent color palette
- ✅ Hover and active states
- ✅ Resource-based styling system
- ✅ Easy theme customization

## Future Enhancements

- [ ] WebView2 integration for actual web browsing
- [ ] Tab system for multiple pages
- [ ] Bookmark management
- [ ] Settings and preferences panel
- [ ] Plugin/extension system
- [ ] Multiple theme support
- [ ] Keyboard shortcuts
- [ ] Context menus

## Architecture

The application follows WPF best practices:
- **MVVM-ready**: Structured for Model-View-ViewModel pattern
- **Resource-based styling**: Centralized theme management
- **Event-driven**: Clean separation of UI and logic
- **Extensible**: Modular design for easy feature additions

## License

This project is open source. Feel free to use, modify, and distribute according to your needs.

## Contributing

Contributions are welcome! Please feel free to submit issues and enhancement requests.
