using System.Collections.Concurrent;

namespace PhotoViewer.Core.Utilities;

/// <summary>
/// LRU (Least Recently Used) 快取實作
/// 線程安全，支持記憶體限制
/// </summary>
public class LruCache<TKey, TValue> where TKey : notnull where TValue : class
{
    private readonly int _maxCapacity;
    private readonly long _maxMemoryBytes;
    private readonly ConcurrentDictionary<TKey, CacheNode> _cache = new();
    private readonly LinkedList<TKey> _lruList = new();
    private readonly object _lock = new();
    private long _currentMemoryUsage;

    private class CacheNode
    {
        public TValue Value { get; set; } = default!;
        public LinkedListNode<TKey> Node { get; set; } = default!;
        public long Size { get; set; }
    }

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="maxCapacity">最大項目數量</param>
    /// <param name="maxMemoryMb">最大記憶體使用量（MB）</param>
    public LruCache(int maxCapacity = 500, int maxMemoryMb = 200)
    {
        _maxCapacity = maxCapacity;
        _maxMemoryBytes = maxMemoryMb * 1024L * 1024L;
    }

    /// <summary>
    /// 目前快取項目數量
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// 目前記憶體使用量（位元組）
    /// </summary>
    public long CurrentMemoryUsage => _currentMemoryUsage;

    /// <summary>
    /// 取得或建立快取項目
    /// </summary>
    public TValue GetOrCreate(TKey key, Func<TValue> factory, long estimatedSize = 0)
    {
        lock (_lock)
        {
            // 快取命中
            if (_cache.TryGetValue(key, out var cacheNode))
            {
                // 移到最前面（最近使用）
                _lruList.Remove(cacheNode.Node);
                cacheNode.Node = _lruList.AddFirst(key);
                return cacheNode.Value;
            }

            // 快取未命中，建立新項目
            var value = factory();
            var size = estimatedSize > 0 ? estimatedSize : EstimateSize(value);

            // 檢查是否需要清理
            EvictIfNeeded(size);

            // 加入快取
            var node = _lruList.AddFirst(key);
            _cache[key] = new CacheNode
            {
                Value = value,
                Node = node,
                Size = size
            };
            _currentMemoryUsage += size;

            return value;
        }
    }

    /// <summary>
    /// 嘗試取得快取項目
    /// </summary>
    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cacheNode))
            {
                // 移到最前面
                _lruList.Remove(cacheNode.Node);
                cacheNode.Node = _lruList.AddFirst(key);
                value = cacheNode.Value;
                return true;
            }

            value = null;
            return false;
        }
    }

    /// <summary>
    /// 移除特定項目
    /// </summary>
    public void Remove(TKey key)
    {
        lock (_lock)
        {
            if (_cache.TryRemove(key, out var cacheNode))
            {
                _lruList.Remove(cacheNode.Node);
                _currentMemoryUsage -= cacheNode.Size;

                // 如果實作 IDisposable，呼叫 Dispose
                (cacheNode.Value as IDisposable)?.Dispose();
            }
        }
    }

    /// <summary>
    /// 清空快取
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var item in _cache.Values)
            {
                (item.Value as IDisposable)?.Dispose();
            }

            _cache.Clear();
            _lruList.Clear();
            _currentMemoryUsage = 0;
        }
    }

    /// <summary>
    /// 如果需要，驅逐最少使用的項目
    /// </summary>
    private void EvictIfNeeded(long newItemSize)
    {
        // 當快取滿或記憶體不足時，移除最少使用的項目
        while (_cache.Count >= _maxCapacity ||
               _currentMemoryUsage + newItemSize > _maxMemoryBytes)
        {
            if (_lruList.Last == null) break;

            var keyToRemove = _lruList.Last.Value;
            Remove(keyToRemove);
        }
    }

    /// <summary>
    /// 估算項目大小（位元組）
    /// </summary>
    private long EstimateSize(TValue value)
    {
        // 簡單估算：假設每個物件約 1KB
        // 對於 SKBitmap，應該使用實際像素數據大小
        return 1024;
    }
}
