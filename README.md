# LuminaView - 高性能 WPF 圖片瀏覽器

[English] | [繁體中文]

## 📸 專案簡介

**LuminaView** 是一款基於 WPF 和 SkiaSharp 構建的現代化圖片瀏覽器，旨在為用戶提供極快的性能和流暢的瀏覽體驗。

### ✨ 核心特性

- 🚀 **超高性能**: 支持數萬張圖片的流暢瀏覽（虛擬化渲染）
- 💾 **智能緩存**: SQLite 元數據 + WebP 緩存，快速重複打開
- 🎨 **SkiaSharp 渲染**: 跨平台圖像處理，支持多種格式
- 📂 **實時監控**: 自動監聽檔案夾變化，動態更新列表
- 🔍 **高級編輯**: 縮放、平移、旋轉、EXIF 查看
- ⚡ **異步加載**: 背景執行緒加載，不阻塞 UI

---

## 🏗️ 系統架構

### 三層架構設計

```
┌──────────────────────────────────────────────┐
│         PhotoViewer.App (WPF UI)             │
│  ├─ MainWindow      ├─ GalleryView          │
│  ├─ ViewerView      └─ Controls             │
└──────────────────────────────────────────────┘
                    ▼
┌──────────────────────────────────────────────┐
│      PhotoViewer.Core (業務邏輯層)            │
│  ├─ ImageLoaderService                      │
│  ├─ ThumbnailCacheService                   │
│  ├─ ImageDecoderService                     │
│  └─ FileWatcherService                      │
└──────────────────────────────────────────────┘
                    ▼
┌──────────────────────────────────────────────┐
│  基礎庫: SkiaSharp | SQLite | .NET Task     │
└──────────────────────────────────────────────┘
```

---

## 📦 專案結構

```
PhotoViewer/
├── src/
│   ├── PhotoViewer.Core/
│   │   ├── Models/
│   │   │   ├── ImageItem.cs           # 圖片資料模型
│   │   │   ├── FolderNode.cs          # 檔案夾樹結構
│   │   │   └── CacheEntry.cs          # 快取項目
│   │   │
│   │   ├── Services/
│   │   │   ├── ImageLoaderService.cs       # 異步加載器
│   │   │   ├── ThumbnailCacheService.cs    # 快取管理
│   │   │   ├── ImageDecoderService.cs      # SkiaSharp 解碼
│   │   │   └── FileWatcherService.cs       # 檔案監控
│   │   │
│   │   ├── Interfaces/
│   │   │   └── IImageService.cs
│   │   │
│   │   └── Utilities/
│   │       ├── LruCache.cs
│   │       └── ImageUtils.cs
│   │
│   └── PhotoViewer.App/
│       ├── Views/
│       │   ├── MainWindow.xaml
│       │   ├── GalleryView.xaml
│       │   └── ViewerView.xaml
│       │
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   ├── GalleryViewModel.cs
│       │   └── ViewerViewModel.cs
│       │
│       └── Controls/
│           ├── SkiaCanvasControl.cs
│           └── VirtualizingWrapPanel.cs
│
├── docs/
│   ├── plan.md
│   ├── README.md
│   └── ARCHITECTURE.md
│
└── tests/
```

---

## 🚀 快速開始

### 系統要求
- Visual Studio 2022+
- .NET 6.0 或更高版本
- Windows 10 21H2+

### 安裝步驟

```bash
# 1. 複製專案
git clone https://github.com/username/luminaview.git
cd LuminaView

# 2. 恢復依賴
dotnet restore

# 3. 執行應用
dotnet run --project src/PhotoViewer.App
```

### 編譯發佈

```bash
# 發佈為獨立可執行檔案
dotnet publish -c Release -r win-x64 --self-contained
```

---

## 💡 核心工作流

### 1️⃣ 打開檔案夾

```
用戶選擇檔案夾
    ↓
FileWatcherService 啟動監控
    ↓
背景掃描圖片檔案
    ↓
查詢 SQLite 快取資料庫
    ↓
  ├─ 快取命中 → 從 WebP 快速加載
  └─ 快取未命中 → 生成新縮圖
    ↓
更新 UI 顯示網格
```

### 2️⃣ 縮圖加載策略

```
Level 1: 佔位符 (灰色方塊) → 立即顯示
    ↓
Level 2: 小縮圖 (128×128px) → 從快取加載
    ↓
Level 3: 完整圖片 → 用戶點擊時解碼
```

### 3️⃣ 虛擬化滾動

只渲染可見區域的圖片，滾動時動態加載/卸載。支持 10000+ 張圖片流暢瀏覽。

---

## ⚙️ NuGet 依賴

```xml
<!-- 圖像處理 -->
<PackageReference Include="SkiaSharp" Version="2.88.0" />
<PackageReference Include="SkiaSharp.Views.WPF" Version="2.88.0" />

<!-- 資料快取 -->
<PackageReference Include="SQLite-net-pcl" Version="1.8.0" />

<!-- MVVM 框架 -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.0" />

<!-- 異步工具 -->
<PackageReference Include="System.Reactive" Version="5.4.0" />
```

---

## 🎯 使用指南

### 基本操作

| 功能 | 操作 |
|------|------|
| 打開檔案夾 | `File → Open Folder` 或 `Ctrl+O` |
| 查看圖片 | 雙擊縮圖 |
| 放大/縮小 | `滑鼠滾輪` |
| 平移 | `拖拽` 或 `方向鍵` |
| 旋轉 | `R` 順時針 / `Shift+R` 逆時針 |
| 上一張 | `←` 或 `Page Up` |
| 下一張 | `→` 或 `Page Down` |
| 全螢幕 | `F` |
| 幻燈片 | `Space` |

### 快捷鍵列表

```
[通用]
Ctrl+O      打開檔案夾
Ctrl+W      關閉目前
Ctrl+Q      結束應用
Ctrl+S      保存編輯

[編輯]
Ctrl+Z      復原
Ctrl+Y      重做
Ctrl+R      重設縮放
Ctrl+L      逆時針旋轉 90°
Ctrl+R      順時針旋轉 90°

[瀏覽]
←/→         上/下一張
Page Up/Down 上/下一張
Home         第一張
End          最後一張
Space        播放/暫停幻燈片
```

---

## 📊 性能指標

| 指標 | 目標 | 實現方案 |
|------|------|--------|
| 檔案夾初始化 | < 2s | 背景掃描 + 增量更新 |
| 首張縮圖顯示 | < 500ms | 快取預熱 |
| 網格滾動幀率 | ≥ 50 FPS | 虛擬化面板 |
| 單張圖加載 | < 100ms | 異步解碼 + LRU Cache |
| 記憶體占用 | < 250MB | 受限快取大小 |
| 大檔案處理 | 支持 4K+ | 逐行解碼 |

---

## 🔧 開發指南

### 程式碼結構約定

```csharp
// Services 必須實現異步
public async Task<List<ImageItem>> LoadImagesAsync(string path, CancellationToken ct)
{
    return await Task.Run(() => /* 背景邏輯 */, ct);
}

// 使用 MVVM Toolkit
public class GalleryViewModel : ObservableObject
{
    [ObservableProperty]
    private string folderPath;
    
    [RelayCommand]
    private async Task LoadFolderAsync() { }
}

// 快取存取模式
private readonly LruCache<string, SKBitmap> _cache;
var bitmap = _cache.GetOrCreate(key, () => DecodeImage(path));
```

### 提交訊息格式

```
[Phase-1] Feature: 實現基礎檔案夾瀏覽
[Fix] 修復虛擬化面板滾動卡頓問題
[Refactor] 最佳化快取管理器架構
[Docs] 更新 API 文件
[Test] 新增快取一致性測試
```

### 單元測試

```bash
# 執行所有測試
dotnet test

# 執行特定測試類
dotnet test --filter NameFilter=ThumbnailCacheServiceTests

# 生成覆蓋率報告
dotnet test /p:CollectCoverage=true
```

---

## 🐛 常見問題

### Q: 如何支持 GIF 動畫？
**A**: SkiaSharp 原生支持 GIF 逐幀解碼。在 `ImageDecoderService` 中實現幀序列加載即可。

### Q: 快取檔案存儲在哪裡？
**A**: 預設在 `%APPDATA%\LuminaView\Cache` 目錄，SQLite 資料庫為 `cache.db`。

### Q: 可以修改縮圖大小嗎？
**A**: 在 `ThumbnailCacheService` 中配置 `THUMBNAIL_SIZE` 常數。建議 128-256px。

### Q: 怎樣處理 RAW 格式照片？
**A**: SkiaSharp 不原生支持 RAW，需集成第三方庫如 `LibRaw` 或使用 Windows Imaging Component。

---

## 📝 許可證

MIT License - 詳見 [LICENSE](LICENSE)

---

## 🤝 貢獻指南

歡迎提交 Issue 和 Pull Request！請遵循以下流程：

1. Fork 專案
2. 建立特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m '[Feature] Add AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打開 Pull Request

---

## 📞 聯絡方式

- 📧 郵件: [your-email@example.com]
- 🐦 GitHub Issues: [Link]
- 💬 討論區: [Link]

---

## 🙏 致謝

感謝以下開源專案的支持：
- [SkiaSharp](https://github.com/mono/SkiaSharp)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [SQLite-net-pcl](https://github.com/praeclarum/sqlite-net)

---

**最後更新**: 2025年12月3日  
**版本**: 0.1.0 (早期開發版)
