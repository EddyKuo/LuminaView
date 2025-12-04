using SQLite;
using System.IO;
using PhotoViewer.Core.Models;
using PhotoViewer.Core.Utilities;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 縮圖快取服務
/// 使用 SQLite 管理元數據，WebP 儲存縮圖
/// </summary>
public class ThumbnailCacheService : IDisposable
{
    private const int THUMBNAIL_SIZE = 128;
    private const int WEBP_QUALITY = 85;
    private const long MAX_CACHE_SIZE_BYTES = 1024L * 1024 * 1024; // 1GB
    private const int CACHE_EXPIRY_DAYS = 30;

    private readonly string _cacheDirectory;
    private readonly string _thumbnailDirectory;
    private readonly string _databasePath;
    private readonly SQLiteAsyncConnection _database;
    private readonly ImageDecoderService _imageDecoder;

    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    public ThumbnailCacheService(string? cacheDirectory = null)
    {
        // 預設快取目錄：%APPDATA%\LuminaView\Cache
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LuminaView",
            "Cache"
        );

        _thumbnailDirectory = Path.Combine(_cacheDirectory, "thumbnails");
        _databasePath = Path.Combine(_cacheDirectory, "cache.db");

        // 確保目錄存在
        Directory.CreateDirectory(_cacheDirectory);
        Directory.CreateDirectory(_thumbnailDirectory);

        // 初始化資料庫
        _database = new SQLiteAsyncConnection(_databasePath);
        _imageDecoder = new ImageDecoderService();

        // 不在建構子中 Wait，改為延遲初始化
    }

    /// <summary>
    /// 確保資料庫已初始化
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized)
                return;

            await InitializeDatabaseAsync();
            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 初始化資料庫表格
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        await _database.CreateTableAsync<CacheEntry>();
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_modified ON cache_entries(Modified)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_hash ON cache_entries(Hash)");
    }

    /// <summary>
    /// 取得或建立縮圖快取
    /// </summary>
    public async Task<CacheEntry?> GetOrCreateAsync(string filePath, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (!File.Exists(filePath))
            return null;

        try
        {
            var fileInfo = new FileInfo(filePath);
            var modified = fileInfo.LastWriteTime;
            var fileSize = fileInfo.Length;

            // 檢查快取是否存在且有效
            var existingEntry = await _database.Table<CacheEntry>()
                .Where(e => e.FilePath == filePath)
                .FirstOrDefaultAsync();

            if (existingEntry != null && existingEntry.Modified == modified)
            {
                // 快取命中，更新最後存取時間
                existingEntry.LastAccessed = DateTime.Now;
                await _database.UpdateAsync(existingEntry);

                // 驗證縮圖檔案是否存在
                if (File.Exists(existingEntry.ThumbnailPath))
                {
                    return existingEntry;
                }
            }

            // 快取未命中或無效，生成新縮圖
            return await CreateThumbnailAsync(filePath, fileSize, modified, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetOrCreateAsync for {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 建立縮圖快取
    /// </summary>
    private async Task<CacheEntry?> CreateThumbnailAsync(string filePath, long fileSize, DateTime modified, CancellationToken ct)
    {
        try
        {
            // 解碼縮圖
            var bitmap = await _imageDecoder.DecodeThumbnailAsync(filePath, THUMBNAIL_SIZE, ct);
            if (bitmap == null)
                return null;

            // 計算 Hash
            var hash = await ImageUtils.ComputeFileHashAsync(filePath, ct);

            // 生成縮圖檔案路徑
            var thumbnailFileName = $"{hash}.webp";
            var thumbnailPath = Path.Combine(_thumbnailDirectory, thumbnailFileName);

            // 儲存為 WebP
            var saved = _imageDecoder.SaveAsWebP(bitmap, thumbnailPath, WEBP_QUALITY);
            if (!saved)
            {
                bitmap.Dispose();
                return null;
            }

            // 建立快取項目
            var cacheEntry = new CacheEntry
            {
                FilePath = filePath,
                Hash = hash,
                Modified = modified,
                ThumbnailPath = thumbnailPath,
                Width = bitmap.Width,
                Height = bitmap.Height,
                FileSize = fileSize,
                Format = ImageUtils.GetImageFormat(filePath),
                CachedAt = DateTime.Now,
                LastAccessed = DateTime.Now
            };

            bitmap.Dispose();

            // 儲存到資料庫
            await _database.InsertOrReplaceAsync(cacheEntry);

            return cacheEntry;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating thumbnail for {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 檢查檔案是否已快取
    /// </summary>
    public async Task<bool> IsCachedAsync(string filePath, string hash)
    {
        await EnsureInitializedAsync();

        var entry = await _database.Table<CacheEntry>()
            .Where(e => e.FilePath == filePath && e.Hash == hash)
            .FirstOrDefaultAsync();

        return entry != null && File.Exists(entry.ThumbnailPath);
    }

    /// <summary>
    /// 取得快取統計資訊
    /// </summary>
    public async Task<(int Count, long TotalSize)> GetCacheStatsAsync()
    {
        await EnsureInitializedAsync();

        var entries = await _database.Table<CacheEntry>().ToListAsync();
        long totalSize = 0;

        foreach (var entry in entries)
        {
            if (File.Exists(entry.ThumbnailPath))
            {
                totalSize += new FileInfo(entry.ThumbnailPath).Length;
            }
        }

        return (entries.Count, totalSize);
    }

    /// <summary>
    /// 清理過期的快取
    /// </summary>
    public async Task<int> ClearExpiredAsync()
    {
        await EnsureInitializedAsync();

        var expiryDate = DateTime.Now.AddDays(-CACHE_EXPIRY_DAYS);
        var expiredEntries = await _database.Table<CacheEntry>()
            .Where(e => e.LastAccessed < expiryDate)
            .ToListAsync();

        int removedCount = 0;

        foreach (var entry in expiredEntries)
        {
            try
            {
                // 刪除縮圖檔案
                if (File.Exists(entry.ThumbnailPath))
                {
                    File.Delete(entry.ThumbnailPath);
                }

                // 從資料庫刪除
                await _database.DeleteAsync(entry);
                removedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing expired cache entry: {ex.Message}");
            }
        }

        return removedCount;
    }

    /// <summary>
    /// 清理超過大小限制的快取（LRU）
    /// </summary>
    public async Task<int> ClearOversizedCacheAsync()
    {
        await EnsureInitializedAsync();

        var stats = await GetCacheStatsAsync();

        if (stats.TotalSize <= MAX_CACHE_SIZE_BYTES)
            return 0;

        // 依最後存取時間排序，刪除最舊的
        var entries = await _database.Table<CacheEntry>()
            .OrderBy(e => e.LastAccessed)
            .ToListAsync();

        int removedCount = 0;
        long currentSize = stats.TotalSize;

        foreach (var entry in entries)
        {
            if (currentSize <= MAX_CACHE_SIZE_BYTES * 0.8) // 清理到 80%
                break;

            try
            {
                if (File.Exists(entry.ThumbnailPath))
                {
                    var fileSize = new FileInfo(entry.ThumbnailPath).Length;
                    File.Delete(entry.ThumbnailPath);
                    currentSize -= fileSize;
                }

                await _database.DeleteAsync(entry);
                removedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing cache entry: {ex.Message}");
            }
        }

        return removedCount;
    }

    /// <summary>
    /// 清空所有快取
    /// </summary>
    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync();

        // 刪除所有縮圖檔案
        if (Directory.Exists(_thumbnailDirectory))
        {
            foreach (var file in Directory.GetFiles(_thumbnailDirectory))
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }

        // 清空資料庫
        await _database.DeleteAllAsync<CacheEntry>();
    }

    /// <summary>
    /// 載入縮圖圖片
    /// </summary>
    public async Task<SkiaSharp.SKBitmap?> LoadThumbnailAsync(CacheEntry entry, CancellationToken ct = default)
    {
        if (!File.Exists(entry.ThumbnailPath))
            return null;

        try
        {
            return await Task.Run(() =>
            {
                using var stream = File.OpenRead(entry.ThumbnailPath);
                return SkiaSharp.SKBitmap.Decode(stream);
            }, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading thumbnail: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 批次處理多個圖片
    /// </summary>
    public async Task<List<CacheEntry>> GetOrCreateBatchAsync(
        IEnumerable<string> filePaths,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        var results = new List<CacheEntry>();
        var filePathList = filePaths.ToList();
        int current = 0;
        int total = filePathList.Count;

        foreach (var filePath in filePathList)
        {
            if (ct.IsCancellationRequested)
                break;

            var entry = await GetOrCreateAsync(filePath, ct);
            if (entry != null)
            {
                results.Add(entry);
            }

            current++;
            progress?.Report((current, total));
        }

        return results;
    }

    public void Dispose()
    {
        _database?.CloseAsync().Wait();
        GC.SuppressFinalize(this);
    }
}
