# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LuminaView is a high-performance image viewer built with .NET 8 and WPF, designed to handle large image collections efficiently. It uses SkiaSharp for hardware-accelerated rendering, custom virtualization for smooth scrolling through thousands of images, and a SQLite-based caching system with WebP compression.

**Key Technologies:**
- .NET 8.0 (Windows Desktop)
- WPF with MVVM architecture (CommunityToolkit.Mvvm)
- SkiaSharp for rendering (replaces traditional WPF BitmapSource for 60fps performance)
- SQLite-net-pcl for thumbnail cache management
- MetadataExtractor for EXIF data
- Magick.NET for RAW image format support

## Build and Development Commands

### Building
```bash
# Debug build
dotnet build -c Debug

# Release build
dotnet build -c Release
```

Alternatively, use the provided scripts:
- `build_debug.bat` - Debug configuration
- `build_release.bat` - Release configuration

### Publishing
```bash
# Self-contained single file (recommended for distribution)
dotnet publish src\PhotoViewer.App\PhotoViewer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\self-contained

# Optimized single file (compressed, smaller size)
dotnet publish src\PhotoViewer.App\PhotoViewer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\optimized

# Framework-dependent (requires .NET 8 Runtime)
dotnet publish src\PhotoViewer.App\PhotoViewer.App.csproj -c Release -r win-x64 --self-contained false -o publish\framework-dependent
```

Alternatively, use the provided scripts:
- `publish_self_contained.bat` - Single EXE with embedded runtime
- `publish_optimized.bat` - Compressed single EXE (smallest size)
- `publish_framework_dependent.bat` - Requires .NET 8 Runtime

## Architecture

### Two-Layer Design

**PhotoViewer.Core** (Business Logic Layer)
- **Models**: Core data structures (`ImageItem`, `FolderNode`, `AnimatedImage`, `CacheEntry`)
- **Services**:
  - `ImageLoaderService` - Central orchestration for image loading, memory/disk cache coordination
  - `ThumbnailCacheService` - SQLite database + WebP file management
  - `ImageDecoderService` - SkiaSharp-based decoding, EXIF extraction, GIF animation support
  - `FileWatcherService` - Real-time folder monitoring for file changes
- **Utilities**: `LruCache` (memory cache), `ImageUtils` (format detection, hash calculation)

**PhotoViewer.App** (UI Layer)
- **Controls**:
  - `SkiaCanvasControl` - Custom SkiaSharp rendering surface for high-performance image display with zoom/pan/rotate
  - `VirtualizingWrapPanel` - Custom virtualizing panel that renders only visible thumbnails (critical for performance with 10,000+ images)
- **Views**: `MainWindow.xaml` (folder tree + thumbnails), `ViewerView.xaml` (full-screen image viewer)
- **Services**: `ThemeService` - Dark/Light theme switching

### Critical Performance Components

**1. Three-Tier Caching Strategy**
- **Memory Cache (LruCache)**: Hot images in RAM, 200MB limit by default
- **Disk Cache (ThumbnailCacheService)**: WebP thumbnails in `%APPDATA%\LuminaView\Cache`, 1GB limit
- **SQLite Metadata**: Fast lookup by file hash, tracks last access time for LRU eviction

**2. Virtualization Pipeline**
- `VirtualizingWrapPanel` calculates visible range based on scroll position
- Only generates UI containers for visible items ± 2 buffer rows
- Automatically cleans up off-screen containers to minimize memory
- Critical for handling 10,000+ images without UI lag

**3. SkiaSharp Rendering**
- `SkiaCanvasControl` uses SKElement (WPF-SkiaSharp bridge)
- All transformations (zoom/pan/rotate) use hardware-accelerated matrix operations
- 60fps rendering via `OnPaintSurface` override
- GIF animation support via DispatcherTimer frame switching

### Image Loading Flow

1. User scrolls → `VirtualizingWrapPanel` calculates visible range
2. UI requests thumbnail via `ImageLoaderBehavior` (attached property)
3. `ImageLoaderService.LoadThumbnailAsync`:
   - Check memory cache (instant hit)
   - Check SQLite + load WebP from disk (~10ms)
   - Decode original image + save WebP (~50-200ms first time)
4. Thumbnail displayed, SQLite entry updated with LastAccessed timestamp

### File Watching Architecture

`FileWatcherService` monitors active folders:
- Detects file additions/deletions
- Automatically updates `ObservableCollection` in UI thread
- Handles system folder errors gracefully (e.g., Recovery partition)

## Important Implementation Details

### SQLite Initialization
Must call `SQLitePCL.Batteries.Init()` in `App.OnStartup` before any database access (see `App.xaml.cs:15`).

### Cache Management
- Default cache location: `%APPDATA%\LuminaView\Cache\thumbnails\`
- Thumbnails stored as `{SHA256Hash}.webp`
- Auto-cleanup: 30-day expiry, 1GB size limit (LRU eviction at 80%)
- Call `ImageLoaderService.CleanupCacheAsync()` periodically

### SkiaSharp Transform Matrix
Transform order matters (see `SkiaCanvasControl.cs:245-269`):
1. Translate to image center (make origin)
2. Apply scale
3. Apply rotation
4. Translate to canvas position

### Virtualization Performance
`VirtualizingWrapPanel` uses `BufferRows = 2` to preload off-screen rows. Increasing this improves scroll smoothness but increases memory usage.

### Concurrent Loading
**Auto-scaling Thread Pool** (ImageLoaderService.cs:28-42):
- Default: Automatically uses **all CPU cores** (`Environment.ProcessorCount`)
- Previous: Limited to 4 threads (now removed)
- Uses `SemaphoreSlim` to control concurrent image decoding
- Console output shows active thread count on startup

**Batch Processing Optimization** (ImageLoaderService.cs:163-199):
- Dynamic batch size: `CPU cores × 3` (minimum 50)
- Example: 16-core CPU processes 48 images simultaneously
- Uses `Task.WhenAll` for maximum parallelism
- Progress reporting via `Interlocked.Increment` for thread-safety

**Performance Impact**:
- 4-core CPU: 12 concurrent loads (3x improvement from 4)
- 8-core CPU: 24 concurrent loads (6x improvement)
- 16-core CPU: 48 concurrent loads (12x improvement)

To customize: Pass `maxConcurrentLoads` parameter to constructor (0 = auto, >0 = manual limit)

### RAW Image Support and Performance Optimizations
**Supported Formats**: CR2, NEF, ARW, DNG, ORF, RW2, RAF, PEF, SRW (see `ImageUtils.RawExtensions`)

**Three-Tier Decoding Strategy** (ImageDecoderService.cs:74-177):
1. **Fast Path - Embedded Thumbnail Extraction** (`ExtractRawEmbeddedThumbnail`):
   - Uses Magick.NET's `thumbnail:` prefix to extract embedded JPEG previews
   - **10-100x faster** than full RAW decode for thumbnails
   - Falls back to full decode if embedded thumbnail unavailable

2. **Optimized Decode with Settings** (for thumbnails):
   - Sets `MagickReadSettings.Width/Height` to target size
   - Magick.NET performs subsampled decode (saves 50-75% time)

3. **Full Resolution Decode** (for viewer):
   - Uses high-quality JPEG intermediary (faster than PNG)
   - Quality=95 to minimize artifacts

**Magick.NET Resource Limits** (ImageDecoderService.cs:14-21):
- `ResourceLimits.Thread`: Set to CPU core count for parallel decode
- `ResourceLimits.Memory`: 2GB limit to prevent OOM with large RAW files

**Performance Tips**:
- First load of RAW thumbnail: ~100-500ms (depending on file size and embedded thumbnail availability)
- Cached loads: ~10-50ms (WebP from disk cache)
- Memory cached: <1ms
- For folders with many RAW files, initial scan will be slower but subsequent access is fast due to caching

## Common Patterns

### Adding New Image Format Support
1. Update `ImageUtils.GetImageFormat()` to detect extension
2. Add decoding logic in `ImageDecoderService.DecodeBitmapAsync()`
3. Consider thumbnail extraction optimization (like EXIF thumbnail for JPEG)

### Adding New Viewer Keyboard Shortcuts
1. Add `KeyBinding` in `ViewerView.xaml`
2. Implement command in ViewModel or code-behind
3. Update README.md shortcuts table

### Theme Customization
Themes defined in `PhotoViewer.App\Themes\` as ResourceDictionaries. `ThemeService` switches merged dictionaries at runtime.

## Testing Notes

No formal test suite exists yet. Manual testing focuses on:
- Loading folders with 10,000+ images (virtualization stress test)
- Cache hit rate (check SQLite row count vs file loads)
- Memory usage over time (LruCache eviction)
- GIF animation frame timing accuracy

## Known Constraints

- Windows-only (WPF dependency)
- No cross-platform support planned
- Single-folder view (no multi-folder merge)
- Protected system folders (e.g., `C:\Recovery`) intentionally skipped to prevent UnauthorizedAccessException
