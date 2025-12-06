# LuminaView 任務清單 (Task Board)

**專案**: LuminaView - WPF + SkiaSharp 圖片瀏覽器  
**狀態**: Phase 4 - 虛擬化渲染  
**最後更新**: 2025-12-03

---

## 📋 Phase 1: 基礎框架 (第1-2週)

### ✅ P1.1 專案結構搭建
- [x] **P1.1.1** 建立 Visual Studio 解決方案
  - 工作項: 建立 PhotoViewer.sln
  - 優先級: 🔴 Critical
  - 分配: Backend Team
  - 預估: 0.5 天
  - 驗收: sln 檔案可編譯通過

- [x] **P1.1.2** 建立 Core 類別庫專案
  - 工作項: PhotoViewer.Core (.NET 6.0 Class Library)
  - 優先級: 🔴 Critical
  - 分配: Backend Team
  - 預估: 0.5 天
  - 驗收: Core 專案建立成功

- [x] **P1.1.3** 建立 WPF 應用專案
  - 工作項: PhotoViewer.App (.NET 6.0 WPF)
  - 優先級: 🔴 Critical
  - 分配: Frontend Team
  - 預估: 0.5 天
  - 驗收: WPF 應用可執行

### ✅ P1.2 NuGet 依賴安裝
- [x] **P1.2.1** 安裝 SkiaSharp
  - 工作項: `dotnet add package SkiaSharp`
  - 優先級: 🔴 Critical
  - 版本: 2.88.*
  - 預估: 0.3 天
  - 驗收: using SkiaSharp 無錯誤

- [x] **P1.2.2** 安裝 SkiaSharp.Views.WPF
  - 工作項: `dotnet add package SkiaSharp.Views.WPF`
  - 版本: 2.88.*
  - 預估: 0.3 天

- [x] **P1.2.3** 安裝 SQLite-net-pcl
  - 工作項: `dotnet add package SQLite-net-pcl`
  - 版本: 1.8.*
  - 預估: 0.2 天

- [x] **P1.2.4** 安裝 CommunityToolkit.Mvvm
  - 工作項: `dotnet add package CommunityToolkit.Mvvm`
  - 版本: 8.*
  - 預估: 0.2 天

### ✅ P1.3 資料模型
- [x] **P1.3.1** 實現 ImageItem 模型
  ```csharp
  public class ImageItem
  {
      public string FilePath { get; set; }
      public string FileName { get; set; }
      public DateTime Modified { get; set; }
      public long FileSize { get; set; }
      public (int Width, int Height) Dimensions { get; set; }
      public string Hash { get; set; }  // 用於快取驗證
  }
  ```
  - 位置: `PhotoViewer.Core/Models/ImageItem.cs`
  - 優先級: 🔴 Critical
  - 預估: 1 天
  - 驗收: 單元測試通過

- [x] **P1.3.2** 實現 FolderNode 模型
  ```csharp
  public class FolderNode
  {
      public string Path { get; set; }
      public string Name { get; set; }
      public List<FolderNode> SubFolders { get; set; }
      public List<ImageItem> Images { get; set; }
      public DateTime LastScanned { get; set; }
  }
  ```
  - 位置: `PhotoViewer.Core/Models/FolderNode.cs`
  - 優先級: 🟡 High
  - 預估: 1 天

- [x] **P1.3.3** 實現 CacheEntry 模型
  ```csharp
  [Table("cache_entries")]
  public class CacheEntry
  {
      [PrimaryKey]
      public string FilePath { get; set; }
      public string Hash { get; set; }
      public DateTime Modified { get; set; }
      public string ThumbnailPath { get; set; }
      public int Width { get; set; }
      public int Height { get; set; }
      public DateTime CachedAt { get; set; }
  }
  ```
  - 優先級: 🟡 High
  - 預估: 0.5 天

### ✅ P1.4 檔案系統服務
- [x] **P1.4.1** 實現 FileWatcherService
  ```csharp
  public class FileWatcherService
  {
      public event EventHandler<FileSystemEventArgs> FileCreated;
      public event EventHandler<FileSystemEventArgs> FileModified;
      public event EventHandler<FileSystemEventArgs> FileDeleted;
      
      public void WatchFolder(string folderPath);
      public void StopWatching();
  }
  ```
  - 位置: `PhotoViewer.Core/Services/FileWatcherService.cs`
  - 優先級: 🟡 High
  - 預估: 1.5 天
  - 驗收: 檔案變化被正確捕獲

- [x] **P1.4.2** 實現檔案夾掃描
  ```csharp
  public async Task<FolderNode> ScanFolderAsync(string path, CancellationToken ct)
  {
      // 遞迴掃描，支持取消
  }
  ```
  - 預估: 1 天
  - 驗收: 支持大檔案夾掃描

### ✅ P1.5 主UI框架
- [x] **P1.5.1** 實現 MainWindow.xaml
  - 工作項: 建立主視窗配置
  - 配置: 左側檔案夾樹 + 右側內容區
  - 預估: 1.5 天
  - 驗收: 配置美觀，響應式

- [x] **P1.5.2** 實現 MainWindowViewModel
  ```csharp
  public partial class MainWindowViewModel : ObservableObject
  {
      [ObservableProperty]
      private string selectedPath;
      
      [RelayCommand]
      private async Task OpenFolderAsync() { }
  }
  ```
  - 預估: 1 天

- [x] **P1.5.3** 建立 GalleryView 框架
  - 工作項: 縮圖網格介面
  - 預估: 1 天

- [x] **P1.5.4** 建立 ViewerView 框架
  - 工作項: 單張圖片查看介面
  - 預估: 0.5 天

**Phase 1 總預估**: 14 天

---

## 📋 Phase 2: SkiaSharp 集成 (第3-4週)

### ✅ P2.1 圖像解碼
- [x] **P2.1.1** 實現 ImageDecoderService
  ```csharp
  public class ImageDecoderService
  {
      public SKBitmap DecodeBitmap(string path);
      public SKBitmap DecodeThumbnail(string path, int maxSize);
      public (int Width, int Height) GetImageDimensions(string path);
  }
  ```
  - 位置: `PhotoViewer.Core/Services/ImageDecoderService.cs`
  - 優先級: 🔴 Critical
  - 預估: 2 天
  - 驗收: 支持 JPG, PNG, WebP, BMP

- [x] **P2.1.2** 支持多種圖片格式
  - JPEG (有損)
  - PNG (無損 + Alpha)
  - WebP (現代格式)
  - BMP (位圖)
  - 預估: 1 天

- [x] **P2.1.3** 圖像資訊提取
  - 寬度、高度、色彩空間
  - 檔案大小、修改時間
  - 預估: 0.5 天

### ✅ P2.2 縮圖生成
- [x] **P2.2.1** 實現縮圖生成演算法
  ```csharp
  public static SKBitmap GenerateThumbnail(string path, int maxSize = 128)
  {
      using var stream = File.OpenRead(path);
      using var codec = SKCodec.Create(stream);
      
      var info = codec.Info;
      var scale = Math.Min((float)maxSize / info.Width, (float)maxSize / info.Height);
      var targetInfo = new SKImageInfo((int)(info.Width * scale), (int)(info.Height * scale));
      
      var bitmap = new SKBitmap(targetInfo);
      codec.GetPixels(targetInfo, bitmap.GetPixels());
      return bitmap;
  }
  ```
  - 位置: `PhotoViewer.Core/Services/ImageDecoderService.cs`
  - 優先級: 🔴 Critical
  - 預估: 1.5 天
  - 關鍵: 直接縮放解碼，不加載完整圖像

- [x] **P2.2.2** 保存縮圖為 WebP
  - 格式: WebP (壓縮率最佳化)
  - 存儲: `%APPDATA%\LuminaView\Cache\thumbnails\`
  - 預估: 1 天

### ✅ P2.3 SkiaSharp 畫布控制項
- [x] **P2.3.1** 實現 SkiaCanvasControl
  ```csharp
  public partial class SkiaCanvasControl : SKCanvasView
  {
      protected override void OnPaintSurface(SKPaintSurfaceEventArgs args)
      {
          var canvas = args.Surface.Canvas;
          canvas.Clear(SKColors.Black);
          // 繪製圖像
      }
  }
  ```
  - 位置: `PhotoViewer.App/Controls/SkiaCanvasControl.cs`
  - 優先級: 🔴 Critical
  - 預估: 1.5 天
  - 驗收: 可繪製SKBitmap 並顯示

- [x] **P2.3.2** 集成到 ViewerView
  - 位置: `PhotoViewer.App/Views/ViewerView.xaml`
  - 預估: 0.5 天

### ✅ P2.4 圖片網格顯示
- [x] **P2.4.1** 建立縮圖項目範本
  - XAML DataTemplate 設計
  - 每個項目顯示: 縮圖 + 檔案名稱
  - 預估: 1 天

- [x] **P2.4.2** 實現 GalleryViewModel
  ```csharp
  public partial class GalleryViewModel : ObservableObject
  {
      [ObservableProperty]
      private ObservableCollection<ImageItem> images;
      
      [RelayCommand]
      private async Task LoadGalleryAsync(string folderPath) { }
  }
  ```
  - 預估: 1.5 天

- [x] **P2.4.3** 配置 GalleryView 網格
  - Grid/WrapPanel 配置
  - 響應式列數計算
  - 預估: 1 天

### ✅ P2.5 單張查看功能
- [x] **P2.5.1** 實現 ViewerViewModel
  ```csharp
  public partial class ViewerViewModel : ObservableObject
  {
      [ObservableProperty]
      private SKBitmap currentImage;
      
      [RelayCommand]
      private async Task LoadImageAsync(ImageItem item) { }
      
      [RelayCommand]
      private void NextImage() { }
      
      [RelayCommand]
      private void PreviousImage() { }
  }
  ```
  - 預估: 1.5 天

- [x] **P2.5.2** 顯示圖片資訊
  - 檔案名稱、尺寸、大小
  - 修改日期、格式
  - 預估: 0.5 天

**Phase 2 總預估**: 14 天

---

## 📋 Phase 3: 快取系統 (第5-6週)

### ✅ P3.1 SQLite 快取資料庫
- [x] **P3.1.1** 設計資料庫模式
  - 表: cache_entries (FilePath, Hash, Modified, ThumbnailPath, etc.)
  - 索引: FilePath, Modified
  - 預估: 0.5 天

- [x] **P3.1.2** 實現 ThumbnailCacheService
  ```csharp
  public class ThumbnailCacheService
  {
      private const int THUMBNAIL_SIZE = 128;
      private readonly SQLiteAsyncConnection _db;
      
      public async Task<CacheEntry> GetOrCreateAsync(string filePath, CancellationToken ct);
      public async Task<bool> IsCachedAsync(string filePath, string hash);
      public async Task ClearExpiredAsync();
  }
  ```
  - 位置: `PhotoViewer.Core/Services/ThumbnailCacheService.cs`
  - 優先級: 🔴 Critical
  - 預估: 2 天
  - 驗收: 快取命中率 > 90%

- [x] **P3.1.3** 實現增量更新邏輯
  ```csharp
  public async Task<List<CacheEntry>> FindUpdatedFilesAsync(
      string folderPath, 
      CancellationToken ct)
  {
      // 比對修改時間，只返回變動檔案
  }
  ```
  - 預估: 1.5 天
  - 驗收: 支持新增/刪除/修改偵測

### ✅ P3.2 快取檔案管理
- [x] **P3.2.1** 實現快取目錄管理
  - 建立 `%APPDATA%\LuminaView\Cache`
  - 子目錄: `thumbnails`, `temp`
  - 預估: 0.5 天

- [x] **P3.2.2** 實現 WebP 快取存儲
  ```csharp
  private async Task SaveThumbnailAsync(SKBitmap bitmap, string cachePath)
  {
      using var data = bitmap.Encode(SKEncodedImageFormat.Webp, 85);
      using var stream = File.Create(cachePath);
      await data.AsStream().CopyToAsync(stream);
  }
  ```
  - 壓縮率: 85% (平衡品質和大小)
  - 預估: 1 天

- [x] **P3.2.3** 實現快取清理機制
  - LRU 清理 (最多 1GB)
  - 過期清理 (> 30 天)
  - 預估: 1 day

### ✅ P3.3 Hash 驗證
- [x] **P3.3.1** 實現檔案 Hash 計算
  ```csharp
  public static string ComputeHash(string filePath)
  {
      using var sha = System.Security.Cryptography.SHA256.Create();
      using var stream = File.OpenRead(filePath);
      var hash = sha.ComputeHash(stream);
      return Convert.ToHexString(hash);
  }
  ```
  - 演算法: SHA-256 (基於前1MB)
  - 預估: 0.5 day

- [x] **P3.3.2** 整合到快取驗證流程
  - 讀取時比對 Hash
  - 不匹配則重新生成
  - 預估: 0.5 day

**Phase 3 總預估**: 7 天

---

## 📋 Phase 4: 虛擬化渲染 (第7-8週)

### ✅ P4.1 虛擬化面板
- [x] **P4.1.1** 實現 VirtualizingWrapPanel
  ```csharp
  public class VirtualizingWrapPanel : Panel
  {
      protected override Size MeasureOverride(Size constraint);
      protected override Size ArrangeOverride(Size arrangeSize);
      
      // 僅測量和排列可見區域的項目
  }
  ```
  - 位置: `PhotoViewer.App/Controls/VirtualizingWrapPanel.cs`
  - 優先級: 🔴 Critical
  - 預估: 2 天
  - 驗收: 1000+ 項目滾動無卡頓

- [x] **P4.1.2** ItemsControl 集成
  ```xaml
  <ItemsControl ItemsSource="{Binding Images}"
                VirtualizingPanel.IsVirtualizing="True"
                VirtualizingPanel.VirtualizationMode="Recycling">
      <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
              <local:VirtualizingWrapPanel />
          </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
  </ItemsControl>
  ```
  - 預估: 0.5 day

### ✅ P4.2 延遲加載
- [x] **P4.2.1** 實現延遲加載機制
  ```csharp
  private SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(4); // 最多並發4個
  
  public async Task<SKBitmap> LoadImageAsync(
      string path, 
      CancellationToken ct)
  {
      await _loadingSemaphore.WaitAsync(ct);
      try
      {
          return await Task.Run(() => DecodeImage(path), ct);
      }
      finally
      {
          _loadingSemaphore.Release();
      }
  }
  ```
  - 並發限制: 4 個
  - 預估: 1.5 day

- [x] **P4.2.2** 快速滾動時取消加載
  ```csharp
  private CancellationTokenSource _cts;
  
  public void OnScroll()
  {
      _cts?.Cancel();  // 取消之前的加載
      _cts = new CancellationTokenSource();
      LoadVisibleItemsAsync(_cts.Token);
  }
  ```
  - 預估: 1 day

### ✅ P4.3 LRU 快取
- [x] **P4.3.1** 實現 LRU Cache 類別
  ```csharp
  public class LruCache<TKey, TValue> where TValue : class
  {
      private const long MAX_SIZE = 200 * 1024 * 1024; // 200MB
      
      public TValue GetOrCreate(TKey key, Func<TValue> factory);
      public void Remove(TKey key);
      public void Clear();
  }
  ```
  - 位置: `PhotoViewer.Core/Utilities/LruCache.cs`
  - 優先級: 🔴 Critical
  - 預估: 1.5 day
  - 驗收: 記憶體占用穩定

- [x] **P4.3.2** 集成到 ImageLoaderService
  - 限制記憶體中的 SKBitmap 數量
  - 超過 200MB 自動清理
  - 預估: 0.5 day

### ✅ P4.4 性能最佳化
- [x] **P4.4.1** 實現進度指示
  - 加載進度條
  - 正在處理數量顯示
  - 預估: 0.5 day

- [x] **P4.4.2** 記憶體監控
  - 顯示目前記憶體占用
  - 監控 GC 活動
  - 預估: 0.5 day

**Phase 4 總預估**: 8 天

---

## 📋 Phase 5: 單張圖片編輯 (第9-10週)

### ✅ P5.1 縮放功能
- [x] **P5.1.1** 滑鼠滾輪縮放
  ```csharp
  protected override void OnMouseWheel(MouseWheelEventArgs e)
  {
      var zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
      _zoomLevel *= zoomFactor;
      _zoomLevel = Math.Clamp(_zoomLevel, 0.1f, 10f);
      InvalidateVisual();
  }
  ```
  - 預估: 1 day

- [x] **P5.1.2** 快捷鍵縮放
  - `+` / `-` 縮放
  - `Ctrl+0` 重設
  - 預估: 0.5 day

- [x] **P5.1.3** 適應視窗
  - `Ctrl+Shift+F` 適應寬度
  - `Ctrl+Shift+H` 適應高度
  - 預估: 0.5 day

### ✅ P5.2 平移功能
- [x] **P5.2.1** 拖拽平移
  ```csharp
  private Point _dragStart;
  
  protected override void OnMouseDown(MouseButtonEventArgs e)
  {
      _dragStart = e.GetPosition(this);
      CaptureMouse();
  }
  
  protected override void OnMouseMove(MouseEventArgs e)
  {
      if (IsMouseCaptured)
      {
          var offset = e.GetPosition(this) - _dragStart;
          _panX += offset.X;
          _panY += offset.Y;
          _dragStart = e.GetPosition(this);
          InvalidateVisual();
      }
  }
  ```
  - 預估: 1 day

- [x] **P5.2.2** 方向鍵平移
  - `← → ↑ ↓` 移動
  - `Shift + 方向鍵` 快速移動
  - 預估: 0.5 day

### ✅ P5.3 旋轉功能
- [x] **P5.3.1** 旋轉變換
  ```csharp
  private float _rotation = 0;  // degrees
  
  public void Rotate(float degrees)
  {
      _rotation = (_rotation + degrees) % 360;
      InvalidateVisual();
  }
  ```
  - 快捷鍵: `R` (順時針) / `Shift+R` (逆時針)
  - 預估: 1 day

- [x] **P5.3.2** 翻轉功能
  - `H` 水平翻轉
  - `V` 垂直翻轉
  - 預估: 0.5 day

### ✅ P5.4 導航功能
- [x] **P5.4.1** 上下張切換
  ```csharp
  [RelayCommand]
  private void NextImage()
  {
      if (CurrentIndex < Images.Count - 1)
          CurrentIndex++;
  }
  ```
  - 快捷鍵: `→ / Page Down` 下一張
  - 快捷鍵: `← / Page Up` 上一張
  - 預估: 0.5 day

- [x] **P5.4.2** 首尾跳轉
  - `Home` 第一張
  - `End` 最後一張
  - 預估: 0.5 day

### ✅ P5.5 資訊顯示
- [x] **P5.5.1** 實現資訊欄
  - 檔案名稱、尺寸、大小
  - 目前位置 (X / Total)
  - 格式、色彩空間
  - 預估: 1 day

- [ ] **P5.5.2** 直方圖顯示
  - RGB 直方圖 (可選)
  - 預估: 1.5 day

**Phase 5 總預估**: 8 天

---

## 📋 Phase 7: 部署與腳本 (新增)

### ✅ P7.1 建置腳本
- [x] **P7.1.1** 建立 build_debug.bat
- [x] **P7.1.2** 建立 build_release.bat

### ✅ P7.2 發布腳本
- [x] **P7.2.1** 建立 publish_self_contained.bat
- [x] **P7.2.2** 建立 publish_framework_dependent.bat

---

## 📋 Phase 6: 進階功能 (第11-12週)

### ✅ P6.1 EXIF 處理
- [ ] **P6.1.1** EXIF 資訊讀取
  - 拍攝時間、相機型號
  - ISO、快門、光圈
  - 位置資訊
  - 預估: 1.5 day

- [ ] **P6.1.2** EXIF 顯示面板
  - 詳細 EXIF 瀏覽器
  - 地圖顯示 (可選)
  - 預估: 1 day

### ✅ P6.2 圖片篩選
- [ ] **P6.2.1** 按類型篩選
  - JPG / PNG / WebP / BMP / GIF
  - 預估: 1 day

- [ ] **P6.2.2** 按大小篩選
  - 小於 1MB / 1-10MB / > 10MB
  - 預估: 0.5 day

- [ ] **P6.2.3** 按日期篩選
  - 今天 / 本週 / 本月 / 自訂
  - 預估: 0.5 day

### ✅ P6.3 幻燈片
- [ ] **P6.3.1** 幻燈片播放
  ```csharp
  [RelayCommand]
  private async Task PlaySlideShowAsync()
  {
      while (IsPlayingSlideShow)
      {
          await Task.Delay(3000); // 3秒間隔
          NextImage();
      }
  }
  ```
  - 預設間隔: 3 秒
  - 支持自訂間隔
  - 預估: 1 day

- [ ] **P6.3.2** 循環播放
  - 播放結束自動跳回第一張
  - 預估: 0.5 day

### ✅ P6.4 快捷鍵自訂
- [ ] **P6.4.1** 快捷鍵配置檔案
  - JSON / XML 格式
  - 用戶可編輯
  - 預估: 1 day

- [ ] **P6.4.2** 快捷鍵管理 UI
  - 配置對話框
  - 衝突偵測
  - 預估: 1.5 day

### ✅ P6.5 主題/皮膚
- [ ] **P6.5.1** 淺色/深色主題
  - WPF ResourceDictionary
  - 預估: 1 day

- [ ] **P6.5.2** 自訂主題
  - 主題編輯器
  - 預估: 1 day

### ✅ P6.6 高級功能
- [ ] **P6.6.1** 圖片對比功能
  - 並排顯示兩張圖
  - 預估: 1 day

- [ ] **P6.6.2** 批量操作
  - 批量刪除/移動/複製
  - 預估: 1.5 day

**Phase 6 總預估**: 11 天

---

## 📊 專案時間線

```
週 1-2   | Phase 1 (基礎框架)        | ████████████████████ 100%
週 3-4   | Phase 2 (SkiaSharp 集成)  | ████████████████████ 100%
週 5-6   | Phase 3 (快取系統)        | ████████████████████ 100%
週 7-8   | Phase 4 (虛擬化)          | ████████████████████ 100%
週 9-10  | Phase 5 (單張編輯)        | ████████████████████ 100%
週 11-12 | Phase 6 (進階功能)        | ░░░░░░░░░░░░░░░░░░░░ 0%
```

---

## 🔍 優先級說明

| 符號 | 優先級 | 說明 |
|------|--------|------|
| 🔴 | Critical | 阻塞其他任務，必須完成 |
| 🟡 | High | 重要，應盡快完成 |
| 🟢 | Medium | 標準優先級 |
| 🔵 | Low | 可選或延後 |

---

## 📈 驗收標準

### Phase 1 完成標準
- [x] 專案結構完整可編譯
- [x] 可打開本機檔案夾
- [x] 顯示圖片清單
- [x] 基礎 UI 回應正常

### Phase 2 完成標準
- [x] SkiaSharp 成功解碼多種格式
- [x] 縮圖生成正確
- [x] 單張圖片可顯示和編輯基本屬性

### Phase 3 完成標準
- [x] SQLite 快取建立成功
- [x] 首次打開 < 2 秒
- [x] 重複打開同一檔案夾 < 500ms
- [x] 快取驗證無誤

### Phase 4 完成標準
- [x] 10000+ 圖片流暢滾動 (FPS ≥ 50)
- [x] 記憶體占用穩定 < 250MB
- [x] 快速滾動不出現卡頓

### Phase 5 完成標準
- [x] 縮放/平移/旋轉功能完整
- [x] 快捷鍵回應迅速
- [x] 圖片資訊顯示準確

### Phase 6 完成標準
- [ ] EXIF 資訊完整顯示
- [ ] 幻燈片正常播放
- [ ] 快捷鍵自訂成功
- [ ] 主題切換流暢

---

## 🐛 已知問題

| 問題 | 狀態 | 優先級 |
|------|------|--------|
| GIF 動畫支持 | 🟢 已實現 | 🟢 Medium |
| RAW 格式支持 | 🟢 已實現 | 🔵 Low |
| 雲同步功能 | 🔴 未實現 | 🔵 Low |

---

## 📝 更新日誌

### v0.3.0 (2025-12-03) - Phase 4 & 5 完成
- 完成虛擬化渲染 (VirtualizingWrapPanel)
- 完成檔案夾樹狀導航
- 完成圖片檢視器 (縮放/平移/旋轉)
- 新增建置與發布腳本

### v0.2.0 (2025-12-03) - Phase 2 & 3 完成
- 完成 SkiaSharp 集成
- 完成 SQLite 快取系統
- 完成單張圖片檢視器 (縮放/平移/旋轉)
- 準備開始 Phase 4 虛擬化開發

### v0.1.0 (2025-12-03) - 初始規劃
- 完成專案計劃和任務分解
- 確定技術棧和架構
- 制定時間線和驗收標準

---

**維護人**: Backend Team  
**最後更新**: 2025-12-03  
**下次評審**: 2025-12-10
