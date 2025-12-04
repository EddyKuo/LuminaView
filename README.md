# LuminaView - 高效能圖片瀏覽器

LuminaView 是一款基於 .NET 8、WPF 和 SkiaSharp 打造的現代化高效能圖片瀏覽器。它專為處理大量圖片而設計，結合了硬體加速渲染、虛擬化技術與智慧快取系統，提供流暢的使用者體驗。

## 🌟 專案亮點

*   **極致效能**: 使用 **SkiaSharp** 進行硬體加速繪圖，取代傳統 WPF BitmapSource，實現 60FPS 的流暢縮放與平移。
*   **海量瀏覽**: 實作自定義 **VirtualizingWrapPanel**，支援數萬張圖片的虛擬化滾動，記憶體占用極低。
*   **智慧快取**: 內建 **SQLite** 資料庫與 LRU 快取機制，支援 **WebP 磁碟快取**，大幅加速二次載入。
    *   快取位置: `%APPDATA%\LuminaView\Cache`
*   **現代架構**: 採用 **MVVM** 架構與依賴注入 (DI)，確保程式碼的可維護性與測試性。
*   **美觀介面**: 支援 **深色/淺色主題** 切換，並採用現代化 UI 設計。

## ✨ 詳細功能列表

### 1. 圖片瀏覽
*   **多格式支援**: 支援 JPG, PNG, BMP, WebP, **GIF (動畫播放)** 等常見格式。
*   **資料夾導航**: 左側樹狀結構 (Folder Tree) 快速切換磁碟與目錄。
*   **非同步載入**: 圖片掃描與解碼全在背景執行，介面永不卡頓。
*   **即時監控**: 自動偵測資料夾內的檔案變動 (新增/刪除) 並即時更新介面。

### 2. 圖片檢視器
*   **操作流暢**: 支援滑鼠滾輪縮放、左鍵拖曳平移。
*   **自由旋轉**: 支援 90 度旋轉 (R/Shift+R)，且旋轉中心保持在視窗中央。
*   **適應模式**: 支援「適應視窗」與「實際大小 (100%)」快速切換。
*   **幻燈片播放**: 支援自動播放 (F5)，預設間隔 3 秒。
*   **詳細資訊**: 整合 **MetadataExtractor**，顯示完整的 EXIF 資訊 (相機型號、ISO、快門等)。
*   **鍵盤控制**: 完整的鍵盤快捷鍵支援 (方向鍵移動、PageUp/Down 切換)。

### 3. 系統整合
*   **單一執行檔**: 支援打包成獨立 EXE，無需安裝 .NET Runtime 即可執行。
*   **錯誤處理**: 自動忽略受保護的系統資料夾 (如 Recovery)，避免程式崩潰。

## 📂 專案結構 (Source Structure)

本專案採用典型的分層架構，將核心邏輯與 UI 分離：

```text
d:\code\LuminaView
├── src
│   ├── PhotoViewer.App          # [UI 層] WPF 應用程式主體
│   │   ├── Controls             # 自定義控制項
│   │   │   ├── SkiaCanvasControl.cs   # SkiaSharp 畫布 (核心渲染)
│   │   │   └── VirtualizingWrapPanel.cs # 虛擬化排版面板
│   │   ├── Views                # XAML 視窗
│   │   │   ├── MainWindow.xaml        # 主視窗 (檔案夾樹 + 縮圖牆)
│   │   │   └── ViewerView.xaml        # 大圖檢視視窗
│   │   ├── Utilities            # UI 相關工具
│   │   │   └── ImageLoaderBehavior.cs # 圖片載入行為 (附加屬性)
│   │   ├── Themes               # 主題資源 (Light/Dark)
│   │   └── MainWindow.xaml.cs   # 主視窗邏輯
│   │
│   └── PhotoViewer.Core         # [核心層] 業務邏輯與資料模型
│       ├── Models               # 資料模型
│       │   ├── FolderNode.cs    # 資料夾節點
│       │   ├── ImageItem.cs     # 圖片項目
│       │   └── AnimatedImage.cs # 動畫圖片模型
│       ├── Services             # 核心服務
│       │   ├── ImageLoaderService.cs    # 圖片載入與快取控制
│       │   ├── FileWatcherService.cs    # 檔案系統監控
│       │   ├── ThumbnailCacheService.cs # SQLite 快取服務
│       │   ├── ImageDecoderService.cs   # 圖片解碼 (SkiaSharp/MetadataExtractor)
│       │   └── ThemeService.cs          # 主題切換服務
│       └── Utilities            # 通用工具
│           ├── ImageUtils.cs    # 圖片格式判斷與 Hash 計算
│           └── LruCache.cs      # LRU 快取實作
│
├── build_debug.bat              # 建置腳本 (Debug)
├── build_release.bat            # 建置腳本 (Release)
├── publish_self_contained.bat   # 發布腳本 (獨立執行檔)
└── publish_framework_dependent.bat # 發布腳本 (依賴框架)
```

## 🛠️ 技術棧 (Tech Stack)

*   **Framework**: .NET 8.0 (Windows Desktop)
*   **UI Framework**: WPF (Windows Presentation Foundation)
*   **Rendering**: SkiaSharp (Google Skia Graphics Engine binding)
*   **Metadata**: MetadataExtractor (Comprehensive EXIF support)
*   **Database**: SQLite-net-pcl (Local Caching)
*   **Architecture**: MVVM (Model-View-ViewModel)

## 🚀 如何建置與發布

### 建置 (Build)
*   **開發除錯**: 執行 `build_debug.bat`
*   **效能最佳化**: 執行 `build_release.bat`

### 發布 (Publish)
*   **獨立執行檔 (推薦)**: 執行 `publish_self_contained.bat`
    *   產出單一 EXE，內含 Runtime，隨插即用。
*   **最佳化單一執行檔 (更小)**: 執行 `publish_optimized.bat`
    *   產出壓縮過的單一 EXE，體積更小，適合最終發布。
*   **依賴框架**: 執行 `publish_framework_dependent.bat`
    *   檔案較小，需安裝 .NET 8 Runtime。

## 🎮 快捷鍵指南

| 功能 | 快捷鍵 | 說明 |
|------|--------|------|
| **上一張** | `←` / `PageUp` | 切換至上一張圖片 |
| **下一張** | `→` / `PageDown` | 切換至下一張圖片 |
| **縮放** | 滾輪 / `+` / `-` | 放大或縮小圖片 |
| **平移** | 拖曳 / 方向鍵 | 移動圖片視角 |
| **旋轉 (順)** | `R` | 順時針旋轉 90 度 |
| **旋轉 (逆)** | `Shift + R` | 逆時針旋轉 90 度 |
| **適應視窗** | `F` | 將圖片縮放至視窗大小 |
| **實際大小** | `Ctrl + 1` | 以 100% 比例顯示 |
| **幻燈片** | `F5` | 開始/停止幻燈片播放 |
| **資訊面板** | `I` | 顯示/隱藏 EXIF 資訊 |
| **關閉** | `Esc` | 關閉視窗 / 停止播放 |

## 📝 授權

MIT License
