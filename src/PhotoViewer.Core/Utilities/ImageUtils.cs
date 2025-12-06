using System.IO;
using System.Security.Cryptography;

namespace PhotoViewer.Core.Utilities;

/// <summary>
/// 圖片工具類
/// </summary>
public static class ImageUtils
{
    /// <summary>
    /// RAW 格式副檔名
    /// </summary>
    public static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".arw", ".cr2", ".nef", ".dng", ".orf", ".rw2", ".raf", ".pef", ".srw"
    };

    /// <summary>
    /// 支持的圖片格式副檔名
    /// </summary>
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Standard formats
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif"
    };

    static ImageUtils()
    {
        // Add RAW extensions to supported extensions
        foreach (var ext in RawExtensions)
        {
            SupportedExtensions.Add(ext);
        }
    }

    /// <summary>
    /// 檢查檔案是否為支持的圖片格式
    /// </summary>
    public static bool IsSupportedImage(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(extension);
    }

    /// <summary>
    /// 檢查是否為 RAW 檔案
    /// </summary>
    public static bool IsRawFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return RawExtensions.Contains(extension);
    }

    /// <summary>
    /// 計算檔案 Hash（基於前 1MB）
    /// </summary>
    public static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct = default)
    {
        const int bufferSize = 1024 * 1024; // 1MB

        try
        {
            await using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();

            var buffer = new byte[Math.Min(bufferSize, stream.Length)];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);

            var hash = sha256.ComputeHash(buffer, 0, bytesRead);
            return Convert.ToHexString(hash);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to compute hash for {filePath}", ex);
        }
    }

    /// <summary>
    /// 格式化檔案大小（例如: "1.5 MB"）
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 從檔案路徑提取格式
    /// </summary>
    public static string GetImageFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');

        if (IsRawFile(filePath))
        {
            return "RAW";
        }

        return extension.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "JPEG",
            "png" => "PNG",
            "bmp" => "BMP",
            "webp" => "WebP",
            "gif" => "GIF",
            _ => extension.ToUpperInvariant()
        };
    }
}
