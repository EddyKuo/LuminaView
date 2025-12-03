# LuminaView 專案計劃

## 專案概述
**LuminaView** 是一款高性能 WPF + SkiaSharp 圖片瀏覽器，支持大規模圖片庫的流暢瀏覽、快速緩存和高效渲染。

---

## 核心目標
✅ 支持數萬張圖片的流暢瀏覽  
✅ 快速縮圖緩存策略（SQLite + WebP）  
✅ 虛擬化捲動顯示  
✅ 單張圖片高級編輯（縮放、平移、旋轉）  
✅ 即時檔案監控和動態更新  

---

## 技術堆疊

### 核心框架
- **WPF**: UI 框架
- **SkiaSharp**: 高性能圖像解碼和渲染
- **SQLite**: 縮圖元數據緩存
- **MVVM Toolkit**: UI 數據綁定

### NuGet 依賴
```xml
<!-- 圖像處理 -->
<PackageReference Include="SkiaSharp" Version="2.88.*" />
<PackageReference Include="SkiaSharp.Views.WPF" Version="2.88.*" />
<PackageReference Include="SkiaSharp.HarfBuzz" Version="2.88.*" />

<!-- 緩存 -->
<PackageReference Include="SQLite-net-pcl" Version="1.8.*" />

<!-- MVVM -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />

<!-- 可選：非同步庫 -->
<PackageReference Include="System.Reactive" Version="5.*" />
```

---

## 實施分階段

### Phase 1: 基礎框架 (第1週)
**目標**: 能打開檔案夾、顯示圖片清單

- [ ] 建立專案結構（PhotoViewer.Core / PhotoViewer.App）
- [ ] 實現 `ImageItem` 和 `FolderNode` 資料模型
- [ ] 實現 `FileWatcherService` 檔案監控
- [ ] 建立基礎 MainWindow UI
- [ ] 實現檔案夾瀏覽功能

**輸出**: 可以打開本機檔案夾並看到圖片清單

---

### Phase 2: SkiaSharp 集成 (第2週)
**目標**: 圖片解碼、縮圖生成、基礎渲染

- [ ] 實現 `ImageDecoderService` - SkiaSharp 圖像解碼
- [ ] 實現縮圖生成演算法（直接縮放解碼）
- [ ] 建立 `SkiaCanvasControl` 自訂控制項
- [ ] 實現 `GalleryView` 網格顯示
- [ ] 單圖顯示功能（ViewerView）

**輸出**: 可以顯示縮圖網格和單張圖片

---

### Phase 3: 緩存系統 (第3週)
**目標**: SQLite 緩存 + 高速緩存策略

- [ ] 實現 `ThumbnailCacheService`
- [ ] 設計緩存資料庫模式（路徑、修改時間、Hash、緩存路徑）
- [ ] 實現增量更新邏輯
- [ ] WebP 緩存檔案存儲
- [ ] 緩存失效和清理機制

**輸出**: 首次掃描快，再次打開同一檔案夾極快

---

### Phase 4: 虛擬化渲染 (第4週)
**目標**: 支持萬張圖片流暢捲動

- [ ] 實現虛擬化 WrapPanel
- [ ] 延遲載入機制
- [ ] 捲動最佳化（取消不可見項載入）
- [ ] 記憶體管理 LRU Cache

**輸出**: 10000+ 圖片可流暢捲動

---

### Phase 5: 單張圖片編輯 (第5週)
**目標**: 縮放、平移、上下張導航

- [ ] 實現縮放功能（滑鼠滾輪）
- [ ] 平移功能（拖拽）
- [ ] 旋轉功能（快捷鍵）
- [ ] 上下張切換快捷鍵
- [ ] 圖片資訊顯示（尺寸、格式、位置）

**輸出**: 完整的單圖查看體驗

---

### Phase 6: 進階功能 (第6週)
**目標**: EXIF、篩選、幻燈片

- [ ] EXIF 資訊讀取和顯示
- [ ] 圖片篩選（類型、大小、日期）
- [ ] 幻燈片播放
- [ ] 快捷鍵自訂
- [ ] 主題切換

**輸出**: 專業級圖片瀏覽器

---

## 架構圖

```
┌─────────────────────────────────────────────────┐
│         PhotoViewer.App (WPF主程式)              │
├─────────────────────────────────────────────────┤
│  Views                  ViewModels     Controls  │
│  ├─ MainWindow          ├─ GVM        ├─ Skia   │
│  ├─ GalleryView         ├─ VVM        └─ Custom │
│  └─ ViewerView          └─ FVM                   │
└─────────────────────────────────────────────────┘
           │                   △
           │                   │
           ▼                   │
┌─────────────────────────────────────────────────┐
│    PhotoViewer.Core (業務邏輯、服務層)           │
├─────────────────────────────────────────────────┤
│  Services              Models      Interfaces   │
│  ├─ ImageLoader       ├─ Image     └─ ISvc     │
│  ├─ ThumbnailCache    ├─ Folder                │
│  ├─ ImageDecoder      └─ CacheDB              │
│  └─ FileWatcher                               │
└─────────────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────────────┐
│  SkiaSharp  │  SQLite  │  .NET 非同步           │
└─────────────────────────────────────────────────┘
```

---

## 性能關鍵指標

| 指標 | 目標 | 實現方式 |
|------|------|--------|
| 檔案夾初始化 | < 2s | 背景掃描 + 增量更新 |
| 縮圖顯示 | 立即 | SQLite 緩存 + WebP |
| 1000張圖片捲動 | 60 FPS | 虛擬化面板 |
| 單張載入 | < 100ms | 非同步解碼 + LRU Cache |
| 記憶體占用 | < 200MB | 限制載入數量 |

---

## 檔案結構詳解

```
PhotoViewer/
├── PhotoViewer.sln
│
├── src/
│   ├── PhotoViewer.Core/
│   │   ├── Models/
│   │   │   ├── ImageItem.cs        # 圖片資料模型
│   │   │   ├── FolderNode.cs       # 檔案夾樹結構
│   │   │   └── CacheDbModel.cs     # SQLite ORM 模型
│   │   │
│   │   ├── Services/
│   │   │   ├── ImageLoaderService.cs      # 非同步圖片載入
│   │   │   ├── ThumbnailCacheService.cs   # 緩存管理
│   │   │   ├── ImageDecoderService.cs     # SkiaSharp 解碼
│   │   │   └── FileWatcherService.cs      # 檔案夾監控
│   │   │
│   │   ├── Interfaces/
│   │   │   ├── IImageLoader.cs
│   │   │   ├── IThumbnailCache.cs
│   │   │   └── IImageDecoder.cs
│   │   │
│   │   └── Utilities/
│   │       ├── LruCache.cs         # LRU 緩存實現
│   │       └── ImageUtils.cs       # 圖像工具類
│   │
│   └── PhotoViewer.App/
│       ├── Views/
│       │   ├── MainWindow.xaml
│       │   ├── GalleryView.xaml    # 網格縮圖
│       │   └── ViewerView.xaml     # 單張查看
│       │
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   ├── GalleryViewModel.cs
│       │   └── ViewerViewModel.cs
│       │
│       ├── Controls/
│       │   ├── SkiaCanvasControl.cs    # SkiaSharp 畫布
│       │   └── VirtualizingWrapPanel.cs # 虛擬化面板
│       │
│       └── Converters/
│           ├── BitmapToImageSourceConverter.cs
│           └── SizeToStringConverter.cs
│
├── docs/
│   ├── plan.md              # 專案計劃
│   ├── README.md            # 使用指南
│   └── ARCHITECTURE.md      # 詳細架構文件
│
└── tests/
    ├── PhotoViewer.Core.Tests/
    └── PhotoViewer.App.Tests/
```

---

## 開發指南

### 環境要求
- Visual Studio 2022 或更高版本
- .NET 6.0 / .NET 7.0 或更高版本
- Windows 10 21H2 或更高版本

### 快速開始
```bash
# 複製/打開專案
cd d:\code\LuminaView

# 恢復 NuGet 套件
dotnet restore

# 執行專案
dotnet run --project src/PhotoViewer.App
```

### 提交規範
```
[Phase-N] Feature: 簡要描述
[Fix] 修復問題描述
[Refactor] 重構模組名稱
[Docs] 文件更新
```

---

## 風險和緩解措施

| 風險 | 概率 | 影響 | 緩解措施 |
|------|------|------|--------|
| 大檔案夾卡頓 | 高 | 使用者體驗差 | 虛擬化 + 非同步載入 |
| 記憶體洩漏 | 中 | 長時間使用崩潰 | LRU Cache + GC 最佳化 |
| 緩存不同步 | 中 | 顯示過期資料 | 檔案監控 + Hash 驗證 |
| SkiaSharp 相容性 | 低 | 某些圖片不顯示 | 降級方案 + 錯誤處理 |

---

## 成功標準

✅ 打開 10000 張圖片的檔案夾 < 3 秒  
✅ 捲動 1000 張縮圖網格 FPS ≥ 50  
✅ 單張圖片載入 < 200ms  
✅ 記憶體占用穩定 < 250MB  
✅ 支持主流格式（JPG, PNG, WebP, BMP, GIF）  
✅ 完整 EXIF 資訊顯示  

---

## 參考資源

- [SkiaSharp 官方文件](https://docs.microsoft.com/zh-cn/xamarin/xamarin-forms/user-interface/graphics/skiasharp/)
- [WPF 虛擬化最佳實踐](https://docs.microsoft.com/zh-cn/dotnet/desktop/wpf/advanced/optimizing-performance-virtualizing-large-data-collections)
- [SQLite-net-pcl 文件](https://github.com/praeclarum/sqlite-net)
- [MVVM Toolkit 指南](https://learn.microsoft.com/zh-cn/windows/communitytoolkit/mvvm/)
