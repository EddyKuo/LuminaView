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
    /// 解碼縮圖（使用 SKCodec 進行優化解碼）
    /// </summary>
    public SKBitmap? DecodeThumbnail(string filePath, int maxSize = 128)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            if (codec == null)
                return null;

            var info = codec.Info;

            // 計算縮放比例
            float scale = Math.Min((float)maxSize / info.Width, (float)maxSize / info.Height);

            // 如果圖片本身比縮圖小，直接解碼
            if (scale >= 1.0f)
            {
                // SKCodec.GetPixels needs a bitmap to write to.
                // Or just use SKBitmap.Decode(codec) if available, but SKBitmap.Decode(stream) is easier.
                // Here we use GetPixels to be consistent.
                var bitmap = new SKBitmap(info);
                var result = codec.GetPixels(info, bitmap.GetPixels());
                if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                    return bitmap;
                return null;
            }

            // 計算目標尺寸
            var targetInfo = new SKImageInfo((int)(info.Width * scale), (int)(info.Height * scale));

            // 嘗試直接解碼為縮小的尺寸
            // SKCodec.GetPixels 支援縮放的程度取決於格式 (JPEG 支援較好)
            // 我們嘗試直接請求目標尺寸
            var scaledBitmap = new SKBitmap(targetInfo);
            var scaledResult = codec.GetPixels(targetInfo, scaledBitmap.GetPixels());

            if (scaledResult == SKCodecResult.Success || scaledResult == SKCodecResult.IncompleteInput)
            {
                return scaledBitmap;
            }

            // 如果直接縮放失敗 (例如不支持的格式)，我們需要一個 fallback
            // 我們可以嘗試讀取 full size 但這在 stream 上可能失敗，因為 codec 已經讀了一些
            // 所以我們在 catch 區塊處理 fallback

            // 主動拋出異常以觸發 fallback (或者在這裡重開 stream)
            throw new NotSupportedException("Scaling not supported for this codec");
        }
        catch (Exception)
        {
            // Fallback: 傳統方式 (先載入再縮放)
            // 由於 stream 可能已被讀取，我們重新打開檔案
            try
            {
                using var original = DecodeBitmap(filePath);
                if (original == null)
                    return null;

                var scale = Math.Min((float)maxSize / original.Width, (float)maxSize / original.Height);
                var targetWidth = (int)(original.Width * scale);
                var targetHeight = (int)(original.Height * scale);

                return original.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Fallback decode failed for {filePath}: {ex2.Message}");
                return null;
            }
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
                
                // 正確使用 SKCodecOptions 來指定解碼哪一幀
                var options = new SKCodecOptions(i);

                codec.GetPixels(info, bitmap.GetPixels(), options);
                
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
}
