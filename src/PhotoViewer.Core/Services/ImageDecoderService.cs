using SkiaSharp;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 圖片解碼服務（使用 SkiaSharp）
/// </summary>
public class ImageDecoderService
{
    /// <summary>
    /// 解碼完整圖片
    /// </summary>
    public SKBitmap? DecodeBitmap(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return SKBitmap.Decode(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to decode image {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 解碼縮圖（直接縮放解碼，不載入完整圖片）
    /// </summary>
    public SKBitmap? DecodeThumbnail(string filePath, int maxSize = 128)
    {
        try
        {
            // 先載入完整圖片
            using var original = DecodeBitmap(filePath);
            if (original == null)
                return null;

            // 計算縮放比例
            var scale = Math.Min((float)maxSize / original.Width, (float)maxSize / original.Height);
            var targetWidth = (int)(original.Width * scale);
            var targetHeight = (int)(original.Height * scale);

            // 縮放
            var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
            return resized;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to decode thumbnail {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 取得圖片尺寸（不載入完整圖片）
    /// </summary>
    public (int Width, int Height)? GetImageDimensions(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            if (codec == null)
                return null;

            return (codec.Info.Width, codec.Info.Height);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 儲存圖片為 WebP 格式
    /// </summary>
    public bool SaveAsWebP(SKBitmap bitmap, string outputPath, int quality = 85)
    {
        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Webp, quality);

            if (data == null)
                return false;

            // 確保目錄存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = File.Create(outputPath);
            data.SaveTo(fileStream);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save WebP {outputPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 非同步解碼完整圖片
    /// </summary>
    public Task<SKBitmap?> DecodeBitmapAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() => DecodeBitmap(filePath), ct);
    }

    /// <summary>
    /// 非同步解碼縮圖
    /// </summary>
    public Task<SKBitmap?> DecodeThumbnailAsync(string filePath, int maxSize = 128, CancellationToken ct = default)
    {
        return Task.Run(() => DecodeThumbnail(filePath, maxSize), ct);
    }
}
