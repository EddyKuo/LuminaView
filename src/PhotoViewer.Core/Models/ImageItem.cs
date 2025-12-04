namespace PhotoViewer.Core.Models;

/// <summary>
/// 圖片項目資料模型
/// </summary>
public class ImageItem
{
    /// <summary>
    /// 檔案完整路徑
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 檔案名稱（不含路徑）
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 檔案修改時間
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// 檔案大小（位元組）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 圖片尺寸（寬度, 高度）
    /// </summary>
    public (int Width, int Height) Dimensions { get; set; }

    /// <summary>
    /// 檔案 Hash（用於快取驗證）
    /// SHA-256 based on first 1MB
    /// </summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// 圖片格式（例如: "jpg", "png", "webp"）
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// 縮圖快取路徑（如果已快取）
    /// </summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// 是否已載入縮圖
    /// </summary>
    public bool IsThumbnailLoaded { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// EXIF 資訊
    /// </summary>
    public Dictionary<string, string> ExifData { get; set; } = new();
}
