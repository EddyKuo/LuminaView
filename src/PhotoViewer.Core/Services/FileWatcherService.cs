namespace PhotoViewer.Core.Services;

using System.IO;

/// <summary>
/// 檔案系統監控服務
/// 監控檔案夾變化，觸發事件通知
/// </summary>
public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private string _watchedPath = string.Empty;

    /// <summary>
    /// 檔案建立事件
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? FileCreated;

    /// <summary>
    /// 檔案修改事件
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? FileModified;

    /// <summary>
    /// 檔案刪除事件
    /// </summary>
    public event EventHandler<FileSystemEventArgs>? FileDeleted;

    /// <summary>
    /// 檔案重新命名事件
    /// </summary>
    public event EventHandler<RenamedEventArgs>? FileRenamed;

    /// <summary>
    /// 錯誤事件
    /// </summary>
    public event EventHandler<ErrorEventArgs>? Error;

    /// <summary>
    /// 是否正在監控
    /// </summary>
    public bool IsWatching => _watcher?.EnableRaisingEvents ?? false;

    /// <summary>
    /// 目前監控的路徑
    /// </summary>
    public string WatchedPath => _watchedPath;

    /// <summary>
    /// 開始監控指定檔案夾
    /// </summary>
    public void WatchFolder(string folderPath, bool includeSubdirectories = true)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            throw new ArgumentException("Invalid folder path", nameof(folderPath));
        }

        // 停止現有監控
        StopWatching();

        _watchedPath = folderPath;
        _watcher = new FileSystemWatcher(folderPath)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName |
                          NotifyFilters.DirectoryName |
                          NotifyFilters.LastWrite |
                          NotifyFilters.Size,
            // 只監控圖片檔案
            Filter = "*.*"
        };

        // 註冊事件處理器
        _watcher.Created += OnCreated;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;

        // 開始監控
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// 停止監控
    /// </summary>
    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnCreated;
            _watcher.Changed -= OnChanged;
            _watcher.Deleted -= OnDeleted;
            _watcher.Renamed -= OnRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }

        _watchedPath = string.Empty;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsImageFile(e.FullPath))
        {
            FileCreated?.Invoke(this, e);
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (IsImageFile(e.FullPath))
        {
            FileModified?.Invoke(this, e);
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsImageFile(e.FullPath))
        {
            FileDeleted?.Invoke(this, e);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsImageFile(e.FullPath) || IsImageFile(e.OldFullPath))
        {
            FileRenamed?.Invoke(this, e);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Error?.Invoke(this, e);
    }

    private bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".gif";
    }

    public void Dispose()
    {
        StopWatching();
        GC.SuppressFinalize(this);
    }
}
