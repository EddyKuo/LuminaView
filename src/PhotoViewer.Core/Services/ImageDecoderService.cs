using SkiaSharp;
using System.IO;

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

    /// <summary>
    /// 解碼 GIF 動畫
    /// </summary>
    public PhotoViewer.Core.Models.AnimatedImage? DecodeGif(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            if (codec == null || codec.FrameCount <= 1)
                return null;

            var animatedImage = new PhotoViewer.Core.Models.AnimatedImage();
            var info = codec.Info;

            for (int i = 0; i < codec.FrameCount; i++)
            {
                var duration = codec.FrameInfo[i].Duration;
                // 確保每一幀至少有 10ms 的持續時間，避免過快
                if (duration < 10) duration = 100;

                var bitmap = new SKBitmap(info);
                var opts = new SKCodecOptions(i);
                
                codec.GetPixels(info, bitmap.GetPixels(), opts);
                
                animatedImage.AddFrame(bitmap, duration);
            }

            return animatedImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to decode GIF {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 讀取完整 EXIF 資訊 (使用 MetadataExtractor)
    /// </summary>
    public Dictionary<string, string> GetExifData(string filePath)
    {
        var exifData = new Dictionary<string, string>();

        try
        {
            // 讀取所有 metadata
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    // 組合 Key: "目錄名 - 標籤名" 以避免重複並提供更多資訊
                    var key = $"{directory.Name} - {tag.Name}";
                    
                    // 避免重複 Key (雖然加上目錄名後重複機率低，但仍需防範)
                    if (!exifData.ContainsKey(key))
                    {
                        exifData[key] = tag.Description ?? "";
                    }
                }
            }

            // 補充檔案屬性 (如果 metadata 中沒有類似資訊，或為了方便查看)
            var fileInfo = new FileInfo(filePath);
            if (!exifData.ContainsKey("File - Name"))
                exifData["File - Name"] = fileInfo.Name;
            
            if (!exifData.ContainsKey("File - Size"))
                exifData["File - Size"] = $"{fileInfo.Length / 1024.0:F2} KB";
            
            if (!exifData.ContainsKey("File - Created"))
                exifData["File - Created"] = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
            
            if (!exifData.ContainsKey("File - Modified"))
                exifData["File - Modified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read EXIF {filePath}: {ex.Message}");
            // 發生錯誤時至少回傳基本檔案資訊
            try
            {
                var fileInfo = new FileInfo(filePath);
                exifData["File - Name"] = fileInfo.Name;
                exifData["File - Size"] = $"{fileInfo.Length / 1024.0:F2} KB";
            }
            catch { }
        }

        return exifData;
    }

    // 移除不再需要的輔助方法
}
