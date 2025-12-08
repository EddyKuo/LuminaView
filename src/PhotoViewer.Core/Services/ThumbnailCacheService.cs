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
    private const long MAX_CACHE_SIZE_BYTES = 5L * 1024 * 1024 * 1024; // 5GB
    private const int CACHE_EXPIRY_DAYS = 60;

    private readonly string _cacheDirectory;
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

        _databasePath = Path.Combine(_cacheDirectory, "cache.db");

        // 確保目錄存在
        Directory.CreateDirectory(_cacheDirectory);

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
        await _database.CreateTableAsync<ThumbnailBlob>();

        // 啟用 Write-Ahead Logging (WAL) 模式以提高並發性
        try
        {
            await _database.ExecuteAsync("PRAGMA journal_mode=WAL");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ThumbnailCache] Warning: Could not enable WAL mode: {ex.Message}");
        }

        // 增加頁面快取大小（10MB）
        try
        {
            await _database.ExecuteAsync("PRAGMA cache_size=-10000");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ThumbnailCache] Warning: Could not set cache size: {ex.Message}");
        }

        // Synchronous 模式 NORMAL（快取資料可接受）
        try
        {
            await _database.ExecuteAsync("PRAGMA synchronous=NORMAL");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ThumbnailCache] Warning: Could not set synchronous mode: {ex.Message}");
        }

        // 建立索引
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_modified ON cache_entries(Modified)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_hash ON cache_entries(Hash)");
        await _database.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_lastaccessed ON cache_entries(LastAccessed)");
        
        // 清理舊的 thumbnails 目錄 (如果存在)
        var oldThumbnailDir = Path.Combine(_cacheDirectory, "thumbnails");
        if (Directory.Exists(oldThumbnailDir))
        {
            try
            {
                Directory.Delete(oldThumbnailDir, true);
            }
            catch { /* 忽略刪除錯誤 */ }
        }
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

                // 檢查 Blob 是否真的存在
                var blobExists = await _database.Table<ThumbnailBlob>()
                    .Where(b => b.Hash == existingEntry.Hash)
                    .CountAsync() > 0;

                if (blobExists)
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

            int width = bitmap.Width;
            int height = bitmap.Height;

            // 計算 Hash
            var hash = await ImageUtils.ComputeFileHashAsync(filePath, ct);

            // 儲存為 WebP 到記憶體
            using var ms = new MemoryStream();
            var saved = _imageDecoder.SaveAsWebP(bitmap, ms, WEBP_QUALITY);
            
            bitmap.Dispose();

            if (!saved)
            {
                return null;
            }

            var thumbnailData = ms.ToArray();

            // 建立快取項目
            var cacheEntry = new CacheEntry
            {
                FilePath = filePath,
                Hash = hash,
                Modified = modified,
                Width = width,
                Height = height,
                FileSize = fileSize,
                Format = ImageUtils.GetImageFormat(filePath),
                CachedAt = DateTime.Now,
                LastAccessed = DateTime.Now
            };

            var thumbnailBlob = new ThumbnailBlob
            {
                Hash = hash,
                Data = thumbnailData
            };

            // 使用交易寫入資料庫
            await _database.RunInTransactionAsync(tran =>
            {
                tran.InsertOrReplace(thumbnailBlob);
                tran.InsertOrReplace(cacheEntry);
            });

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

        if (entry == null) return false;

        var blobExists = await _database.Table<ThumbnailBlob>()
            .Where(b => b.Hash == hash)
            .CountAsync() > 0;

        return blobExists;
    }

    /// <summary>
    /// 取得快取統計資訊
    /// </summary>
    public async Task<(int Count, long TotalSize)> GetCacheStatsAsync()
    {
        await EnsureInitializedAsync();

        var count = await _database.Table<CacheEntry>().CountAsync();
        
        // 這裡只能估算，因為計算所有 Blob 大小太慢
        // 假設平均每個縮圖 10KB
        long totalSize = count * 10240; 

        return (count, totalSize);
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
                await _database.RunInTransactionAsync(tran =>
                {
                    tran.Delete(entry);
                    // 注意：如果多個檔案共用同一個 Hash (內容相同)，這裡刪除 Blob 可能會影響其他檔案
                    // 但在這個應用場景中，通常 Hash 是唯一的，或者我們可以接受重新生成
                    // 更嚴謹的作法是檢查引用計數，但這裡簡化處理
                    tran.Execute("DELETE FROM thumbnail_blobs WHERE Hash = ?", entry.Hash);
                });
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

        // 簡單檢查：如果資料庫檔案超過限制
        var dbInfo = new FileInfo(_databasePath);
        if (dbInfo.Length <= MAX_CACHE_SIZE_BYTES)
            return 0;

        // 需要清理
        var targetSize = (long)(MAX_CACHE_SIZE_BYTES * 0.8);
        var toRemoveBytes = dbInfo.Length - targetSize;
        
        // 估算需要刪除的條目數 (假設平均 10KB)
        int estimatedCount = (int)(toRemoveBytes / 10240) + 100;

        var entries = await _database.QueryAsync<CacheEntry>(
            "SELECT * FROM cache_entries ORDER BY LastAccessed ASC LIMIT ?",
            estimatedCount
        );

        int removedCount = 0;

        foreach (var entry in entries)
        {
            try
            {
                await _database.RunInTransactionAsync(tran =>
                {
                    tran.Delete(entry);
                    tran.Execute("DELETE FROM thumbnail_blobs WHERE Hash = ?", entry.Hash);
                });
                removedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing cache entry: {ex.Message}");
            }
        }
        
        // 執行 VACUUM 以釋放磁碟空間 (這可能會花一點時間)
        await _database.ExecuteAsync("VACUUM");

        return removedCount;
    }

    /// <summary>
    /// 清空所有快取
    /// </summary>
    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync();

        await _database.DeleteAllAsync<CacheEntry>();
        await _database.DeleteAllAsync<ThumbnailBlob>();
        await _database.ExecuteAsync("VACUUM");
    }

    /// <summary>
    /// 載入縮圖圖片
    /// </summary>
    public async Task<SkiaSharp.SKBitmap?> LoadThumbnailAsync(CacheEntry entry, CancellationToken ct = default)
    {
        try
        {
            var blob = await _database.Table<ThumbnailBlob>()
                .Where(b => b.Hash == entry.Hash)
                .FirstOrDefaultAsync();

            if (blob == null || blob.Data == null)
                return null;

            return await Task.Run(() =>
            {
                using var stream = new MemoryStream(blob.Data);
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
