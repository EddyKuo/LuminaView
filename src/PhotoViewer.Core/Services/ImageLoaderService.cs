using PhotoViewer.Core.Models;
using PhotoViewer.Core.Utilities;
using SkiaSharp;
using System.IO;

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
    private readonly SemaphoreSlim _regularImageSemaphore;  // 普通圖片信號量
    private readonly SemaphoreSlim _rawImageSemaphore;      // RAW 圖片信號量
    private int _activeLoadCount;

    // 智能預載入相關欄位
    private CancellationTokenSource? _preloadCts;
    private Task? _preloadTask;
    private readonly SemaphoreSlim _preloadLock = new SemaphoreSlim(1);

    public event EventHandler<int>? LoadingStatusChanged;
    public int ActiveLoadCount => _activeLoadCount;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="maxConcurrentLoads">最大並發載入數量（0 = 自動使用所有 CPU 核心）</param>
    /// <param name="memoryCacheSizeMb">記憶體快取大小（MB）</param>
    public ImageLoaderService(int maxConcurrentLoads = 0, int memoryCacheSizeMb = 500)
    {
        _cacheService = new ThumbnailCacheService();
        _decoderService = new ImageDecoderService();
        _memoryCache = new LruCache<string, SKBitmap>(maxCapacity: 1000, maxMemoryMb: memoryCacheSizeMb);

        // 0 表示自動使用所有 CPU 核心，否則使用指定數量
        int concurrentThreads = maxConcurrentLoads > 0
            ? maxConcurrentLoads
            : Environment.ProcessorCount;

        // 獨立信號量：普通圖片 vs RAW 圖片
        _regularImageSemaphore = new SemaphoreSlim(concurrentThreads);
        _rawImageSemaphore = new SemaphoreSlim(concurrentThreads * 2); // RAW 2x 並發（I/O 密集）

        Console.WriteLine($"[ImageLoaderService] 使用 {concurrentThreads} 個普通圖片執行緒, {concurrentThreads * 2} 個 RAW 圖片執行緒");
    }

    /// <summary>
    /// 載入縮圖（優先從快取）
    /// </summary>
    public async Task<SKBitmap?> LoadThumbnailAsync(string filePath, CancellationToken ct = default)
    {
        Console.WriteLine($"[LoadThumbnailAsync] Called with: {filePath}");

        if (string.IsNullOrEmpty(filePath))
        {
            Console.WriteLine($"[LoadThumbnailAsync] FilePath is null or empty");
            return null;
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[LoadThumbnailAsync] File does not exist: {filePath}");
            return null;
        }

        // 1. 檢查記憶體快取
        if (_memoryCache.TryGet(filePath, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        // 根據文件類型選擇信號量
        bool isRaw = ImageUtils.IsRawFile(filePath);
        var semaphore = isRaw ? _rawImageSemaphore : _regularImageSemaphore;

        await semaphore.WaitAsync(ct);

        try
        {
            Interlocked.Increment(ref _activeLoadCount);
            LoadingStatusChanged?.Invoke(this, _activeLoadCount);

            // 2. 檢查/建立磁碟快取
            var cacheEntry = await _cacheService.GetOrCreateAsync(filePath, ct);
            
            if (cacheEntry != null)
            {
                // 3. 從磁碟快取載入
                var bitmap = await _cacheService.LoadThumbnailAsync(cacheEntry, ct);
                
                if (bitmap != null)
                {
                    // 4. 加入記憶體快取
                    long size = bitmap.Width * bitmap.Height * 4;
                    _memoryCache.GetOrCreate(filePath, () => bitmap, size);
                    return bitmap;
                }
            }

            // 如果磁碟快取失敗（極少發生），回退到直接解碼
            var fallbackBitmap = await _decoderService.DecodeThumbnailAsync(filePath, 128, ct);
            if (fallbackBitmap != null)
            {
                long size = fallbackBitmap.Width * fallbackBitmap.Height * 4;
                _memoryCache.GetOrCreate(filePath, () => fallbackBitmap, size);
            }
            return fallbackBitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoadThumbnail] Error for {filePath}: {ex.Message}");
            Console.WriteLine($"[LoadThumbnail] Stack trace: {ex.StackTrace}");
            return null;
        }
        finally
        {
            Interlocked.Decrement(ref _activeLoadCount);
            LoadingStatusChanged?.Invoke(this, _activeLoadCount);
            semaphore.Release();
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

        // 根據文件類型選擇信號量
        bool isRaw = ImageUtils.IsRawFile(filePath);
        var semaphore = isRaw ? _rawImageSemaphore : _regularImageSemaphore;

        await semaphore.WaitAsync(ct);

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
            semaphore.Release();
        }
    }

    /// <summary>
    /// 載入動畫圖片 (GIF)
    /// </summary>
    public async Task<PhotoViewer.Core.Models.AnimatedImage?> LoadAnimatedImageAsync(string filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        // 簡單檢查副檔名
        var ext = Path.GetExtension(filePath).ToLower();
        if (ext != ".gif")
            return null;

        return await Task.Run(() => _decoderService.DecodeGif(filePath), ct);
    }

    /// <summary>
    /// 取得圖片 EXIF 資訊
    /// </summary>
    public async Task<Dictionary<string, string>> GetExifDataAsync(string filePath, CancellationToken ct = default)
    {
        return await Task.Run(() => _decoderService.GetExifData(filePath), ct);
    }

    /// <summary>
    /// 取得結構化的 EXIF 資訊
    /// </summary>
    public async Task<ExifInfo> GetExifInfoAsync(string filePath, CancellationToken ct = default)
    {
        return await Task.Run(() => _decoderService.GetExifInfo(filePath), ct);
    }

    /// <summary>
    /// 批次載入縮圖（高效能版本，充分利用所有 CPU 核心）
    /// </summary>
    public async Task<List<(string FilePath, SKBitmap? Bitmap)>> LoadThumbnailBatchAsync(
        IEnumerable<string> filePaths,
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<(string, SKBitmap?)>();
        var filePathList = filePaths.ToList();
        int total = filePathList.Count;
        int completed = 0;

        // 使用更大的批次大小以充分利用並行處理能力
        // 批次大小 = CPU 核心數 * 3（確保管線始終滿載）
        int batchSize = Math.Max(Environment.ProcessorCount * 3, 50);

        for (int i = 0; i < total; i += batchSize)
        {
            if (ct.IsCancellationRequested)
                break;

            var batch = filePathList.Skip(i).Take(batchSize);
            var tasks = batch.Select(async path =>
            {
                var bitmap = await LoadThumbnailAsync(path, ct);
                Interlocked.Increment(ref completed);
                progress?.Report((completed, total));
                return (path, bitmap);
            });

            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults);
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
    /// 智能預載入縮圖（基於滾動預測）
    /// 低優先級後台任務，支援取消
    /// </summary>
    public async Task PreloadThumbnailsIntelligentAsync(
        IEnumerable<string> filePaths,
        int priorityCount = 100,
        CancellationToken ct = default)
    {
        // 取消之前的預載入任務
        await CancelPreloadAsync();

        await _preloadLock.WaitAsync(ct);
        try
        {
            _preloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var preloadCt = _preloadCts.Token;

            _preloadTask = Task.Run(async () =>
            {
                var filePathList = filePaths.Take(priorityCount).ToList();

                foreach (var path in filePathList)
                {
                    if (preloadCt.IsCancellationRequested) break;

                    // 跳過已在記憶體快取的項目
                    if (_memoryCache.TryGet(path, out _)) continue;

                    // 後台載入（忽略錯誤）
                    try
                    {
                        await LoadThumbnailAsync(path, preloadCt);
                    }
                    catch (OperationCanceledException) { break; }
                    catch { /* 忽略後台預載入錯誤 */ }
                }
            }, preloadCt);
        }
        finally
        {
            _preloadLock.Release();
        }
    }

    /// <summary>
    /// 取消進行中的預載入任務
    /// </summary>
    private async Task CancelPreloadAsync()
    {
        if (_preloadCts != null)
        {
            _preloadCts.Cancel();
            _preloadCts.Dispose();
            _preloadCts = null;
        }

        if (_preloadTask != null)
        {
            try { await _preloadTask; } catch { }
            _preloadTask = null;
        }
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
        CancelPreloadAsync().Wait();
        _preloadLock?.Dispose();
        _cacheService?.Dispose();
        _memoryCache?.Clear();
        _regularImageSemaphore?.Dispose();
        _rawImageSemaphore?.Dispose();
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
