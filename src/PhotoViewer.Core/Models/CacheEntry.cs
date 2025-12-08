using SQLite;

namespace PhotoViewer.Core.Models;

/// <summary>
/// SQLite 快取資料庫項目
/// </summary>
[Table("cache_entries")]
public class CacheEntry
{
    /// <summary>
    /// 原始圖片檔案路徑（主鍵）
    /// </summary>
    [PrimaryKey]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 檔案 Hash（SHA-256）
    /// </summary>
    [Indexed]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// 檔案修改時間
    /// </summary>
    [Indexed]
    public DateTime Modified { get; set; }



    /// <summary>
    /// 圖片寬度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 圖片高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 檔案大小（位元組）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 快取建立時間
    /// </summary>
    public DateTime CachedAt { get; set; }

    /// <summary>
    /// 最後存取時間（用於 LRU 清理）
    /// </summary>
    [Indexed]
    public DateTime LastAccessed { get; set; }

    /// <summary>
    /// 圖片格式
    /// </summary>
    public string Format { get; set; } = string.Empty;
}
