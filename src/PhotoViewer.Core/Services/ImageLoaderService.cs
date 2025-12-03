using PhotoViewer.Core.Models;
using PhotoViewer.Core.Utilities;
using SkiaSharp;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 統一的圖片載入服務
/// 整合快取、解碼、記憶體管理
/// </summary>
public class ImageLoaderService : IDisposable
{
    private readonly ThumbnailCacheService _cacheService;
    private readonly ImageDecoderService _decoderService;
    private readonly LruCache<string, SKBitmap> _memoryCache;
    private readonly SemaphoreSlim _loadingSemaphore;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="maxConcurrentLoads">最大並發載入數量</param>
    /// <param name="memoryCacheSizeMb">記憶體快取大小（MB）</param>
    public ImageLoaderService(int maxConcurrentLoads = 4, int memoryCacheSizeMb = 200)
    {
        _cacheService = new ThumbnailCacheService();
        _decoderService = new ImageDecoderService();
        _memoryCache = new LruCache<string, SKBitmap>(maxCapacity: 500, maxMemoryMb: memoryCacheSizeMb);
        _loadingSemaphore = new SemaphoreSlim(maxConcurrentLoads);
    }

    /// <summary>
    /// 載入縮圖（優先從快取）
    /// </summary>
    public async Task<SKBitmap?> LoadThumbnailAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine($"[LoadThumbnail] File not found or empty path: {filePath}");
            return null;
        }

        Console.WriteLine($"[LoadThumbnail] START loading: {Path.GetFileName(filePath)}");

        // 檢查記憶體快取
        if (_memoryCache.TryGet(filePath, out var cachedBitmap))
        {
            Console.WriteLine($"[LoadThumbnail] Memory cache HIT: {Path.GetFileName(filePath)}");
            return cachedBitmap;
        }

        Console.WriteLine($"[LoadThumbnail] Memory cache MISS: {Path.GetFileName(filePath)}");

        // 控制並發數量
        await _loadingSemaphore.WaitAsync(ct);

        try
        {
            // 直接嘗試解碼縮圖（跳過快取系統來測試）
            Console.WriteLine($"[LoadThumbnail] Decoding thumbnail directly from file...");
            var bitmap = await _decoderService.DecodeThumbnailAsync(filePath, 128, ct);

            if (bitmap == null)
            {
                Console.WriteLine($"[LoadThumbnail] FAILED to decode thumbnail for: {Path.GetFileName(filePath)}");
                return null;
            }

            Console.WriteLine($"[LoadThumbnail] SUCCESS! Bitmap decoded: {bitmap.Width}x{bitmap.Height}");

            // 估算 bitmap 大小
            long size = bitmap.Width * bitmap.Height * 4; // RGBA

            // 加入記憶體快取
            _memoryCache.GetOrCreate(filePath, () => bitmap, size);
            Console.WriteLine($"[LoadThumbnail] Added to memory cache: {Path.GetFileName(filePath)}");

            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadThumbnail] EXCEPTION: {ex.Message}");
            Console.WriteLine($"[LoadThumbnail] Stack trace: {ex.StackTrace}");
            return null;
        }
        finally
        {
            _loadingSemaphore.Release();
        }
    }

    /// <summary>
    /// 載入完整圖片
    /// </summary>
    public async Task<SKBitmap?> LoadFullImageAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        var cacheKey = $"full_{filePath}";

        // 檢查記憶體快取
        if (_memoryCache.TryGet(cacheKey, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        await _loadingSemaphore.WaitAsync(ct);

        try
        {
            var bitmap = await _decoderService.DecodeBitmapAsync(filePath, ct);
            if (bitmap == null)
                return null;

            long bitmapSize = bitmap.Width * bitmap.Height * 4;
            _memoryCache.GetOrCreate(cacheKey, () => bitmap, bitmapSize);

            return bitmap;
        }
        finally
        {
            _loadingSemaphore.Release();
        }
    }

    /// <summary>
    /// 批次載入縮圖
    /// </summary>
    public async Task<List<(string FilePath, SKBitmap? Bitmap)>> LoadThumbnailBatchAsync(
        IEnumerable<string> filePaths,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<(string, SKBitmap?)>();
        var filePathList = filePaths.ToList();
        int current = 0;
        int total = filePathList.Count;

        // 分批處理，避免一次性載入太多
        const int batchSize = 20;

        for (int i = 0; i < total; i += batchSize)
        {
            if (ct.IsCancellationRequested)
                break;

            var batch = filePathList.Skip(i).Take(batchSize);
            var tasks = batch.Select(async path =>
            {
                var bitmap = await LoadThumbnailAsync(path, ct);
                return (path, bitmap);
            });

            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults);

            current += batchResults.Length;
            progress?.Report((current, total));
        }

        return results;
    }

    /// <summary>
    /// 預載入圖片（預熱快取）
    /// </summary>
    public async Task PreloadThumbnailsAsync(
        IEnumerable<string> filePaths,
        CancellationToken ct = default)
    {
        var filePathList = filePaths.ToList();

        // 使用快取服務批次處理
        await _cacheService.GetOrCreateBatchAsync(filePathList, null, ct);
    }

    /// <summary>
    /// 取得快取統計資訊
    /// </summary>
    public async Task<CacheStatistics> GetCacheStatisticsAsync()
    {
        var (diskCount, diskSize) = await _cacheService.GetCacheStatsAsync();

        return new CacheStatistics
        {
            DiskCacheCount = diskCount,
            DiskCacheSizeBytes = diskSize,
            MemoryCacheCount = _memoryCache.Count,
            MemoryCacheSizeBytes = _memoryCache.CurrentMemoryUsage
        };
    }

    /// <summary>
    /// 清理快取
    /// </summary>
    public async Task CleanupCacheAsync()
    {
        await _cacheService.ClearExpiredAsync();
        await _cacheService.ClearOversizedCacheAsync();
    }

    /// <summary>
    /// 清空記憶體快取
    /// </summary>
    public void ClearMemoryCache()
    {
        _memoryCache.Clear();
    }

    /// <summary>
    /// 從記憶體快取移除特定項目
    /// </summary>
    public void RemoveFromMemoryCache(string filePath)
    {
        _memoryCache.Remove(filePath);
        _memoryCache.Remove($"full_{filePath}");
    }

    public void Dispose()
    {
        _cacheService?.Dispose();
        _memoryCache?.Clear();
        _loadingSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 快取統計資訊
/// </summary>
public class CacheStatistics
{
    public int DiskCacheCount { get; set; }
    public long DiskCacheSizeBytes { get; set; }
    public int MemoryCacheCount { get; set; }
    public long MemoryCacheSizeBytes { get; set; }

    public string DiskCacheSizeFormatted => ImageUtils.FormatFileSize(DiskCacheSizeBytes);
    public string MemoryCacheSizeFormatted => ImageUtils.FormatFileSize(MemoryCacheSizeBytes);
}
