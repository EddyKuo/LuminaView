# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**LuminaView** is a high-performance WPF image viewer built with SkiaSharp, designed to handle large image libraries (10,000+ images) with smooth browsing, intelligent caching, and efficient rendering.

## Project Status

**Phase 2 已完成** - SkiaSharp 整合和單張圖片檢視功能已實現。專案包含完整的縮圖系統、圖片瀏覽器和基本的圖片操作功能（縮放、旋轉、平移）。

## Tech Stack

### Core Frameworks
- **WPF**: UI framework (.NET 8.0-windows)
- **SkiaSharp**: High-performance image decoding and rendering
- **SQLite**: Thumbnail metadata caching (SQLite-net-pcl)

### Key NuGet Packages (已安裝)
- SkiaSharp (2.88.8)
- SkiaSharp.Views.WPF (2.88.8)
- SQLite-net-pcl (1.9.172)
- SQLitePCLRaw.bundle_green (2.1.10)

## Architecture

### Three-Layer Design (已實現)

```
PhotoViewer.App (WPF UI Layer) ✓
  ├── Views: MainWindow.xaml, ViewerView.xaml ✓
  ├── Controls: SkiaCanvasControl ✓
  └── App.xaml.cs (SQLite 初始化) ✓

PhotoViewer.Core (Business Logic Layer) ✓
  ├── Services: ✓
  │   ├── ImageLoaderService (雙層快取) ✓
  │   ├── ThumbnailCacheService (SQLite + WebP) ✓
  │   ├── ImageDecoderService (SkiaSharp) ✓
  │   └── FileWatcherService (檔案監控) ✓
  ├── Models: ImageItem, CacheEntry ✓
  └── Utilities: LruCache, ImageUtils ✓

Base Libraries: SkiaSharp | SQLite | .NET Task
```

### Core Workflows

**Folder Opening**:
1. User selects folder → FileWatcherService starts monitoring
2. Background scan for image files → Query SQLite cache
3. Cache hit: Load from WebP quickly | Cache miss: Generate new thumbnail
4. Update UI grid display

**Thumbnail Loading Strategy** (3-level):
1. Placeholder (gray box) - immediate
2. Small thumbnail (128x128px) - from cache
3. Full image - decode on user click

**Virtualization**: Only render visible items, dynamically load/unload on scroll for 10,000+ image support.

## Development Commands

### Setup
```bash
# Restore NuGet packages
dotnet restore

# Build 專案
dotnet build src/PhotoViewer.App/PhotoViewer.App.csproj

# Run application
dotnet run --project src/PhotoViewer.App/PhotoViewer.App.csproj

# 或直接執行編譯後的 exe
src\PhotoViewer.App\bin\Debug\net8.0-windows\PhotoViewer.App.exe

# 附帶控制台輸出執行（用於除錯）
RunWithConsole.bat
```

### Build and Publish
```bash
# Build in Release mode
dotnet build -c Release

# Publish as standalone executable
dotnet publish -c Release -r win-x64 --self-contained
```

### 已實現的功能
- ✓ 開啟資料夾並掃描圖片
- ✓ 顯示縮圖網格（128x128px）
- ✓ 記憶體快取（LRU，200MB 上限）
- ✓ 點擊縮圖開啟單張圖片檢視器
- ✓ 圖片縮放（滑鼠滾輪）
- ✓ 圖片平移（拖曳）
- ✓ 圖片旋轉（左/右轉 90°）
- ✓ 適應視窗大小
- ✓ 實際大小顯示
- ✓ 鍵盤快捷鍵（←/→/Home/End/F/R/Ctrl+1/Esc）
- ✓ 檔案監控（即時更新）
- ✓ 深色主題 UI

## Coding Conventions

### Async Services Pattern
All services must implement async methods:
```csharp
public async Task<List<ImageItem>> LoadImagesAsync(string path, CancellationToken ct)
{
    return await Task.Run(() => /* background logic */, ct);
}
```

### MVVM Toolkit Usage
Use CommunityToolkit.Mvvm attributes:
```csharp
public class GalleryViewModel : ObservableObject
{
    [ObservableProperty]
    private string folderPath;

    [RelayCommand]
    private async Task LoadFolderAsync() { }
}
```

### Cache Access Pattern
Use LRU cache for bitmap management:
```csharp
private readonly LruCache<string, SKBitmap> _cache;
var bitmap = _cache.GetOrCreate(key, () => DecodeImage(path));
```

### Thumbnail Generation (實際實現)
簡化版本 - 載入後縮放（更穩定）:
```csharp
public SKBitmap? DecodeThumbnail(string filePath, int maxSize = 128)
{
    // 載入完整圖片
    using var original = DecodeBitmap(filePath);
    if (original == null) return null;

    // 計算縮放比例
    var scale = Math.Min((float)maxSize / original.Width, (float)maxSize / original.Height);
    var targetWidth = (int)(original.Width * scale);
    var targetHeight = (int)(original.Height * scale);

    // 使用 Resize 方法
    return original.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
}
```

## Performance Targets

| Metric | Target | Implementation |
|--------|--------|----------------|
| Folder initialization | < 2s | Background scan + incremental update |
| First thumbnail display | < 500ms | Cache preheating |
| Grid scrolling framerate | >= 50 FPS | Virtualized panel |
| Single image load | < 100ms | Async decode + LRU cache |
| Memory usage | < 250MB | Limited cache size |
| Large file handling | Support 4K+ | Line-by-line decoding |

## Commit Message Format

Follow this pattern:
```
[Phase-N] Feature: Brief description
[Fix] Bug description
[Refactor] Module name optimization
[Docs] Documentation update
[Test] New test coverage
```

## 重要實現細節與已知問題

### 記憶體管理
- **不要手動 Dispose SKBitmap**：bitmap 由 `ImageLoaderService` 的 LRU 快取管理
- `SkiaCanvasControl.CurrentBitmap` setter 不應該 Dispose 舊 bitmap，因為可能被快取引用
- 快取系統會自動管理記憶體，達到上限時會自動清除最少使用的項目

### Async/Await 模式
- **避免在建構子中使用 `.Wait()`**：會導致 UI 執行緒死鎖
- 使用延遲初始化模式 (Lazy Initialization)：
```csharp
private bool _isInitialized = false;
private async Task EnsureInitializedAsync()
{
    if (_isInitialized) return;
    await _initLock.WaitAsync();
    try
    {
        if (_isInitialized) return;
        await InitializeDatabaseAsync();
        _isInitialized = true;
    }
    finally { _initLock.Release(); }
}
```

### SQLite 初始化
- **必須在 App.xaml.cs 中初始化**：`SQLitePCL.Batteries.Init();`
- 在 `OnStartup` 方法中呼叫，否則 SQLite 無法正常運作

### 並發載入保護
- ViewerView 使用 `_isLoading` 旗標防止多次同時載入
- 避免快速切換圖片時的競態條件

### SkiaSharp 矩陣變換順序
正確的順序：平移 → 縮放 → 旋轉
```csharp
_matrix = SKMatrix.Identity;
_matrix = _matrix.PostConcat(SKMatrix.CreateTranslation(_translate.X, _translate.Y));
_matrix = _matrix.PostConcat(SKMatrix.CreateScale(_scale, _scale));
if (_rotation != 0)
{
    var centerX = _currentBitmap.Width * _scale / 2f;
    var centerY = _currentBitmap.Height * _scale / 2f;
    _matrix = _matrix.PostConcat(SKMatrix.CreateRotationDegrees(_rotation, _translate.X + centerX, _translate.Y + centerY));
}
```

## File Structure (已實現)

```
LuminaView/
├── src/
│   ├── PhotoViewer.Core/                    # Business logic ✓
│   │   ├── Models/
│   │   │   ├── ImageItem.cs                 # 圖片元數據模型 ✓
│   │   │   └── CacheEntry.cs                # SQLite 快取項目 ✓
│   │   ├── Services/
│   │   │   ├── ImageLoaderService.cs        # 統一載入服務 ✓
│   │   │   ├── ThumbnailCacheService.cs     # SQLite + WebP 快取 ✓
│   │   │   ├── ImageDecoderService.cs       # SkiaSharp 解碼 ✓
│   │   │   └── FileWatcherService.cs        # 檔案監控 ✓
│   │   └── Utilities/
│   │       ├── LruCache.cs                  # LRU 記憶體快取 ✓
│   │       └── ImageUtils.cs                # 工具函數 ✓
│   └── PhotoViewer.App/                     # WPF UI ✓
│       ├── Views/
│       │   ├── MainWindow.xaml              # 主視窗 (縮圖網格) ✓
│       │   └── ViewerView.xaml              # 單張圖片檢視 ✓
│       ├── Controls/
│       │   └── SkiaCanvasControl.cs         # SkiaSharp 畫布 ✓
│       ├── App.xaml                         # 應用程式進入點 ✓
│       └── Styles.xaml                      # 深色主題樣式 ✓
├── CLAUDE.md                                # AI 助手指引 ✓
├── plan.md                                  # 專案計畫
├── Task.md                                  # 任務清單
└── RunWithConsole.bat                       # 除錯用批次檔 ✓
```

## Cache System

- **Location**: `%APPDATA%\LuminaView\Cache`
- **Database**: `cache.db` (SQLite)
- **Thumbnails**: Stored as WebP format (85% quality)
- **Hash validation**: SHA-256 (based on first 1MB)
- **Cleanup**: LRU eviction when > 1GB, expire after 30 days

## Key Implementation Notes

### Virtualization Panel
Must only measure and arrange visible items to support 10,000+ images without performance degradation.

### Concurrent Loading
Use SemaphoreSlim to limit concurrent image decoding (max 4 simultaneous):
```csharp
private SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(4);
```

### Cancellation on Fast Scroll
Cancel previous load operations when user scrolls quickly:
```csharp
private CancellationTokenSource _cts;

public void OnScroll()
{
    _cts?.Cancel();
    _cts = new CancellationTokenSource();
    LoadVisibleItemsAsync(_cts.Token);
}
```

### File Watching
Monitor folder changes in real-time for automatic list updates when files are added/modified/deleted.

## System Requirements

- Visual Studio 2022+
- .NET 6.0 or higher
- Windows 10 21H2+

## Supported Image Formats

- JPEG (lossy compression)
- PNG (lossless + alpha)
- WebP (modern format)
- BMP (bitmap)
- GIF (basic support, animation in Phase 6)

## Phase Implementation Status

| Phase | Description | Status |
|-------|-------------|--------|
| Phase 1 | 基本框架 | ✓ 已完成 |
| Phase 2 | SkiaSharp 整合 | ✓ 已完成 |
| Phase 3 | 快取系統 | ✓ 已完成 (包含在 Phase 2) |
| Phase 4 | 虛擬化 | 未開始 |
| Phase 5 | 單張圖片編輯 | 部分完成 (縮放/旋轉/平移) |
| Phase 6 | 進階功能 | 未開始 |

## Future Features (Phase 6)

- EXIF information reading and display
- Image filtering (by type, size, date)
- Slideshow playback
- Customizable keyboard shortcuts
- Theme switching (light/dark)
- Image comparison (side-by-side)
- Batch operations

## Known Limitations

- GIF animation support: Not yet implemented
- RAW format support: Requires third-party library (LibRaw or Windows Imaging Component)
- Cloud sync: Not planned for initial release
