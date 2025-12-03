# 快速測試指南

## 立即測試

請嘗試以下步驟：

### 1. 確認檔案存在
```bash
dir src\PhotoViewer.App\bin\Debug\net8.0-windows\PhotoViewer.App.exe
```

### 2. 嘗試使用 Visual Studio 啟動
如果有安裝 Visual Studio：
1. 開啟 `PhotoViewer.sln`
2. 右鍵點擊 `PhotoViewer.App` 專案
3. 選擇「設定為啟動專案」
4. 按 `F5` 或點擊「開始偵錯」

這樣可以在 Visual Studio 的輸出視窗中看到任何錯誤訊息。

### 3. 檢查 .NET Runtime
```bash
dotnet --list-runtimes
```

確認有安裝：
- Microsoft.WindowsDesktop.App 8.0.x

如果沒有，請下載安裝：
https://dotnet.microsoft.com/download/dotnet/8.0
選擇「Desktop Runtime」

### 4. 如果有錯誤訊息
現在應用程式已經加入錯誤處理，如果啟動失敗會彈出訊息框。

請回報看到的錯誤訊息內容。
