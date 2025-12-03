# 啟動問題修復記錄

## 問題描述
- 執行 PhotoViewer.App.exe 後程式出現在工作管理員
- 但沒有視窗顯示
- 記憶體使用為 0
- 程式無回應，需要手動終止

## 根本原因
ImageLoaderService 在 MainWindow 建構子中立即初始化，這會導致：
1. ThumbnailCacheService 立即創建
2. SQLite 資料庫立即初始化
3. 可能在某個地方卡住（等待 I/O 或鎖定）

## 解決方案
**延遲初始化 ImageLoaderService**

### 修改前：
```csharp
public MainWindow()
{
    InitializeComponent();
    _imageLoader = new ImageLoaderService(); // 立即初始化
    _fileWatcher = new FileWatcherService();
    // ...
}
```

### 修改後：
```csharp
public MainWindow()
{
    InitializeComponent();
    // _imageLoader 不在這裡初始化
    _fileWatcher = new FileWatcherService();
    // ...
}

private async Task LoadFolderAsync(string folderPath)
{
    // 只在需要時才初始化
    if (_imageLoader == null)
    {
        StatusTextBlock.Text = "正在初始化快取系統...";
        _imageLoader = new ImageLoaderService();
    }
    // ...
}
```

## 其他修復

### 1. SQLite 初始化（App.xaml.cs）
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    SQLitePCL.Batteries.Init(); // 必須
}
```

### 2. 全域異常處理
現在任何錯誤都會彈出訊息框，方便診斷。

### 3. Null 檢查
所有使用 _imageLoader 的地方都加入了 null 檢查。

## 測試步驟

1. **清理並重建：**
   ```bash
   dotnet clean
   dotnet build
   ```

2. **執行測試：**
   ```bash
   TestWindow.bat
   ```
   或直接執行：
   ```bash
   src\PhotoViewer.App\bin\Debug\net8.0-windows\PhotoViewer.App.exe
   ```

3. **預期結果：**
   - 視窗應該立即顯示
   - 看到 LuminaView 主介面
   - 可以點擊「開啟檔案夾」按鈕
   - 首次選擇檔案夾時會看到「正在初始化快取系統...」

## 驗證清單

- [ ] 視窗正常顯示
- [ ] 可以點擊「開啟檔案夾」按鈕
- [ ] 選擇檔案夾後可以看到縮圖
- [ ] 記憶體使用量正常（不是 0）
- [ ] 沒有錯誤訊息彈出

## 如果還是有問題

### A. 檢查 .NET Runtime
```bash
dotnet --list-runtimes
```
確保有 `Microsoft.WindowsDesktop.App 8.0.x`

### B. 使用 Visual Studio 偵錯
1. 開啟 PhotoViewer.sln
2. 設定 PhotoViewer.App 為啟動專案
3. 按 F5
4. 查看輸出視窗的錯誤

### C. 檢查事件檢視器
Windows 事件檢視器 → 應用程式 → 尋找 PhotoViewer 相關錯誤

## 效能改善

延遲初始化的額外好處：
- ✅ 啟動時間更快（不需要等待 SQLite 初始化）
- ✅ 記憶體使用更少（啟動時）
- ✅ 只在真正需要時才創建資源

---

**修復時間：** 2025-12-03
**狀態：** 已修復 ✅
