# LuminaView 故障排除指南

## 問題：執行應用程式沒有視窗顯示

### 症狀
- 執行 `PhotoViewer.App.exe` 或 `dotnet run` 後沒有視窗出現
- 在工作管理員中看到程式但記憶體使用為 0
- 程式似乎在執行但無回應

### 可能原因和解決方案

#### 1. SQLite 初始化問題
**解決方案：** 已在 `App.xaml.cs` 中添加 `SQLitePCL.Batteries.Init()`

檢查是否正確初始化：
```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    SQLitePCL.Batteries.Init(); // 必須在使用 SQLite 前呼叫
}
```

#### 2. 啟動時發生未捕獲的異常
**解決方案：** 已添加全域異常處理

現在任何啟動錯誤都會顯示在 MessageBox 中。

#### 3. 視窗在螢幕外
**嘗試：**
1. 按 `Alt + Space`，然後按 `M`（移動）
2. 使用方向鍵移動視窗
3. 或刪除應用程式設定（如果有保存視窗位置）

#### 4. .NET Runtime 問題
**檢查：**
```bash
dotnet --version
# 應該是 8.0 或更新版本
```

**安裝 .NET 8 Runtime：**
- 下載：https://dotnet.microsoft.com/download/dotnet/8.0
- 安裝：Desktop Runtime (包含 WPF)

#### 5. 缺少依賴庫
**檢查：**
確保所有 DLL 都在輸出目錄：
```
src/PhotoViewer.App/bin/Debug/net8.0-windows/
├── PhotoViewer.App.exe
├── PhotoViewer.Core.dll
├── SkiaSharp.dll
├── SkiaSharp.Views.WPF.dll
├── SQLitePCLRaw.*.dll
└── runtimes/ (native libraries)
```

#### 6. 使用批次檔啟動（推薦）
使用 `TestWindow.bat` 啟動，可以看到錯誤訊息：
```batch
cd D:\code\LuminaView
TestWindow.bat
```

### 手動測試步驟

#### 步驟 1：清理並重建
```bash
cd D:\code\LuminaView
dotnet clean
dotnet build
```

#### 步驟 2：檢查輸出
```bash
cd src\PhotoViewer.App\bin\Debug\net8.0-windows
dir
```

確認 `PhotoViewer.App.exe` 存在且有正確的大小（應該 > 0 KB）

#### 步驟 3：直接執行
```bash
cd src\PhotoViewer.App\bin\Debug\net8.0-windows
.\PhotoViewer.App.exe
```

#### 步驟 4：檢查事件檢視器
如果還是沒有視窗：
1. 開啟 Windows 事件檢視器
2. Windows 記錄 → 應用程式
3. 尋找與 PhotoViewer 相關的錯誤

### 已知問題

#### Issue 1: SQLite 未初始化
**錯誤訊息：** "You need to call SQLitePCL.Batteries.Init()"

**解決：** 已在 Phase 2 修復，確保更新到最新版本

#### Issue 2: SkiaSharp 平台不相容
**錯誤訊息：** "Unable to load DLL 'libSkiaSharp'"

**解決：**
1. 確認在 Windows x64 上執行
2. 檢查 `runtimes/win-x64/native/` 目錄是否存在

### 偵錯模式

如果需要詳細日誌，可以修改 `App.xaml.cs`：

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    try
    {
        Console.WriteLine("正在初始化 SQLite...");
        SQLitePCL.Batteries.Init();
        Console.WriteLine("SQLite 初始化成功");

        Console.WriteLine("正在設定異常處理...");
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        Console.WriteLine("異常處理設定完成");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"啟動失敗: {ex.Message}", "錯誤");
        throw;
    }
}
```

然後在命令提示字元中執行：
```bash
PhotoViewer.App.exe > log.txt 2>&1
```

檢查 `log.txt` 的內容。

### 建議的啟動方式

**方式 1：使用 Visual Studio**
1. 開啟 `PhotoViewer.sln`
2. 設定 `PhotoViewer.App` 為啟動專案
3. 按 F5 啟動偵錯

**方式 2：使用命令列**
```bash
cd D:\code\LuminaView
dotnet run --project src/PhotoViewer.App/PhotoViewer.App.csproj
```

**方式 3：直接執行 exe**
```bash
cd D:\code\LuminaView\src\PhotoViewer.App\bin\Debug\net8.0-windows
start PhotoViewer.App.exe
```

### 聯絡支援

如果以上方法都無法解決問題，請提供以下資訊：
1. Windows 版本
2. .NET 版本 (`dotnet --version`)
3. 錯誤訊息（如果有）
4. 事件檢視器的錯誤日誌
5. 是否能在 Visual Studio 中偵錯執行

### 緊急解決方案：最小化測試

如果需要測試是否是程式碼問題，可以建立一個最小化的測試視窗：

```csharp
// 在 MainWindow.xaml.cs 的建構子中
public MainWindow()
{
    InitializeComponent();
    MessageBox.Show("MainWindow 已初始化！", "測試");
    // 註解掉其他所有程式碼
}
```

如果這個測試視窗能顯示，說明問題在於初始化的某個服務。

### 系統需求確認

確保符合以下需求：
- ✅ Windows 10 1809 或更新版本
- ✅ .NET 8.0 Desktop Runtime
- ✅ 64-bit 作業系統
- ✅ 至少 4GB RAM
- ✅ 支援 DirectX 的顯示卡

---

**最後更新：** 2025-12-03
**版本：** Phase 2
