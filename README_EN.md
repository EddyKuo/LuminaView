# LuminaView - High-Performance Image Viewer

LuminaView is a modern, high-performance image viewer built with .NET 8, WPF, and SkiaSharp. Designed for handling large collections of images, it combines hardware-accelerated rendering, virtualization technology, and an intelligent caching system to provide a smooth user experience.

## 🌟 Highlights

*   **Extreme Performance**: Uses **SkiaSharp** for hardware-accelerated rendering, replacing traditional WPF BitmapSource to achieve smooth 60FPS zooming and panning.
*   **Massive Browsing**: Implements a custom **VirtualizingWrapPanel**, supporting virtualized scrolling for tens of thousands of images with minimal memory usage.
*   **Smart Caching**: Built-in **SQLite** database and LRU caching mechanism, supporting **WebP disk caching** to significantly accelerate subsequent loads.
*   **Modern Architecture**: Adopts **MVVM** architecture and Dependency Injection (DI) to ensure code maintainability and testability.
*   **Beautiful UI**: Supports **Dark/Light Theme** switching and features a modern UI design.

## ✨ Features

### 1. Image Browsing
*   **Multi-Format Support**: Supports common formats like JPG, PNG, BMP, WebP, **GIF (Animation)**, etc.
*   **Folder Navigation**: Left-side Folder Tree for quick switching between drives and directories.
*   **Async Loading**: Image scanning and decoding are performed in the background, ensuring the UI never freezes.
*   **Real-time Monitoring**: Automatically detects file changes (add/delete) in folders and updates the interface in real-time.

### 2. Image Viewer
*   **Smooth Operation**: Supports mouse wheel zooming and left-click drag panning.
*   **Free Rotation**: Supports 90-degree rotation (R/Shift+R), keeping the rotation center in the middle of the window.
*   **Fit Modes**: Supports quick switching between "Fit to Window" and "Actual Size (100%)".
*   **Slideshow**: Supports automatic playback (F5) with a default interval of 3 seconds.
*   **Detailed Info**: Integrates **MetadataExtractor** to display complete EXIF information (Camera Model, ISO, Shutter Speed, etc.).
*   **Keyboard Control**: Full keyboard shortcut support (Arrow keys to move, PageUp/PageDown to switch).

### 3. System Integration
*   **Single Executable**: Supports packaging as a standalone EXE, runnable without installing .NET Runtime.
*   **Error Handling**: Automatically ignores protected system folders (like Recovery) to prevent crashes.

## 📂 Source Structure

This project follows a typical layered architecture, separating core logic from UI:

```text
d:\code\LuminaView
├── src
│   ├── PhotoViewer.App          # [UI Layer] WPF Application Main Body
│   │   ├── Controls             # Custom Controls
│   │   │   ├── SkiaCanvasControl.cs   # SkiaSharp Canvas (Core Rendering)
│   │   │   └── VirtualizingWrapPanel.cs # Virtualizing Layout Panel
│   │   ├── Views                # XAML Windows
│   │   │   ├── MainWindow.xaml        # Main Window (Folder Tree + Thumbnail Wall)
│   │   │   └── ViewerView.xaml        # Large Image Viewer Window
│   │   ├── Utilities            # UI Related Utilities
│   │   │   └── ImageLoaderBehavior.cs # Image Loading Behavior (Attached Property)
│   │   ├── Themes               # Theme Resources (Light/Dark)
│   │   └── MainWindow.xaml.cs   # Main Window Logic
│   │
│   └── PhotoViewer.Core         # [Core Layer] Business Logic and Data Models
│       ├── Models               # Data Models
│       │   ├── FolderNode.cs    # Folder Node
│       │   ├── ImageItem.cs     # Image Item
│       │   └── AnimatedImage.cs # Animated Image Model
│       ├── Services             # Core Services
│       │   ├── ImageLoaderService.cs    # Image Loading and Cache Control
│       │   ├── FileWatcherService.cs    # File System Monitoring
│       │   ├── ThumbnailCacheService.cs # SQLite Cache Service
│       │   ├── ImageDecoderService.cs   # Image Decoding (SkiaSharp/MetadataExtractor)
│       │   └── ThemeService.cs          # Theme Switching Service
│       └── Utilities            # General Utilities
│           ├── ImageUtils.cs    # Image Format Detection and Hash Calculation
│           └── LruCache.cs      # LRU Cache Implementation
│
├── build_debug.bat              # Build Script (Debug)
├── build_release.bat            # Build Script (Release)
├── publish_self_contained.bat   # Publish Script (Standalone EXE)
└── publish_framework_dependent.bat # Publish Script (Framework Dependent)
```

## 🛠️ Tech Stack

*   **Framework**: .NET 8.0 (Windows Desktop)
*   **UI Framework**: WPF (Windows Presentation Foundation)
*   **Rendering**: SkiaSharp (Google Skia Graphics Engine binding)
*   **Metadata**: MetadataExtractor (Comprehensive EXIF support)
*   **Database**: SQLite-net-pcl (Local Caching)
*   **Architecture**: MVVM (Model-View-ViewModel)

## 🚀 Build and Publish

### Build
*   **Development Debug**: Run `build_debug.bat`
*   **Performance Optimization**: Run `build_release.bat`

### Publish
*   **Standalone EXE (Recommended)**: Run `publish_self_contained.bat`
    *   Produces a single EXE containing the Runtime, plug-and-play.
*   **Optimized Single File (Smaller)**: Run `publish_optimized.bat`
    *   Produces a compressed single EXE, smaller size, suitable for final distribution.
*   **Framework Dependent**: Run `publish_framework_dependent.bat`
    *   Smaller file size, requires .NET 8 Runtime installed.

## 🎮 Shortcuts Guide

| Function | Shortcut | Description |
|---|---|---|
| **Previous** | `←` / `PageUp` | Switch to previous image |
| **Next** | `→` / `PageDown` | Switch to next image |
| **Zoom** | Wheel / `+` / `-` | Zoom in or out |
| **Pan** | Drag / Arrow Keys | Move image view |
| **Rotate (CW)** | `R` | Rotate 90 degrees clockwise |
| **Rotate (CCW)** | `Shift + R` | Rotate 90 degrees counter-clockwise |
| **Fit Window** | `F` | Zoom image to fit window |
| **Actual Size** | `Ctrl + 1` | Display at 100% scale |
| **Slideshow** | `F5` | Start/Stop slideshow |
| **Info Panel** | `I` | Show/Hide EXIF info |
| **Close** | `Esc` | Close window / Stop playback |

## 📝 License

MIT License
