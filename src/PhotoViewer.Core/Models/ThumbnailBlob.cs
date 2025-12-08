using SQLite;

namespace PhotoViewer.Core.Models;

/// <summary>
/// 縮圖二進位資料表
/// 分開儲存以避免查詢列表時載入大量資料
/// </summary>
[Table("thumbnail_blobs")]
public class ThumbnailBlob
{
    /// <summary>
    /// 檔案 Hash（關聯到 CacheEntry.Hash）
    /// </summary>
    [PrimaryKey]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// 縮圖二進位資料 (WebP)
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
