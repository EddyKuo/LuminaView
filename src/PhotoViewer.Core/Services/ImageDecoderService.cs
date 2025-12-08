using SkiaSharp;
using PhotoViewer.Core.Utilities;
using PhotoViewer.Core.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System.IO;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 圖片解碼服務（使用 SkiaSharp 與 LibRaw）
/// LibRaw 用於 RAW 檔案，SkiaSharp 用於一般圖檔
/// 注意：Nikon Z9 HE/HE* 格式目前 LibRaw 不支援，需等待 LibRaw 更新
/// </summary>
public class ImageDecoderService
{
    private readonly LibRawDecoder _libRawDecoder = new();

    /// <summary>
    /// 解碼完整圖片
    /// </summary>
    public SKBitmap? DecodeBitmap(string filePath)
    {
        try
        {
            if (ImageUtils.IsRawFile(filePath))
            {
                return DecodeRaw(filePath);
            }

            using var stream = File.OpenRead(filePath);
            return SKBitmap.Decode(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to decode image {filePath}: {ex.Message}");
            return null;
        }
    }

    private SKBitmap? DecodeRaw(string filePath)
    {
        // 策略 1: 使用 LibRaw 完整解碼
        var bitmap = _libRawDecoder.DecodeFull(filePath);
        if (bitmap != null)
        {
            return bitmap;
        }

        // 策略 2: 嘗試半尺寸解碼
        bitmap = _libRawDecoder.DecodeHalfSize(filePath);
        if (bitmap != null)
        {
            Console.WriteLine($"[RAW] 使用半尺寸解碼: {Path.GetFileName(filePath)}");
            return bitmap;
        }

        Console.WriteLine($"[RAW] 無法解碼 RAW 檔案: {Path.GetFileName(filePath)}");
        return null;
    }

    /// <summary>
    /// 嘗試提取 RAW 檔案的內嵌縮圖（使用 MetadataExtractor）
    /// </summary>
    private SKBitmap? ExtractRawEmbeddedThumbnail(string filePath, int maxSize)
    {
        try
        {
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);

            foreach (var directory in directories)
            {
                var dirName = directory.Name.ToLower();
                if (!dirName.Contains("thumbnail") && !dirName.Contains("preview") && !dirName.Contains("jpeg"))
                    continue;

                foreach (var tag in directory.Tags)
                {
                    var tagName = tag.Name?.ToLower() ?? "";
                    var tagDesc = tag.Description?.ToLower() ?? "";

                    if (tagName.Contains("thumbnail") || tagName.Contains("preview") ||
                        tagName.Contains("jpeginterchangeformat") || tagDesc.Contains("bytes"))
                    {
                        try
                        {
                            var obj = directory.GetObject(tag.Type);
                            if (obj is byte[] thumbnailData && thumbnailData.Length > 1000)
                            {
                                if (thumbnailData.Length > 2 &&
                                    thumbnailData[0] == 0xFF && thumbnailData[1] == 0xD8)
                                {
                                    using var thumbnailStream = new MemoryStream(thumbnailData);
                                    var thumbnail = SKBitmap.Decode(thumbnailStream);

                                    if (thumbnail != null && thumbnail.Width > 50 && thumbnail.Height > 50)
                                    {
                                        Console.WriteLine($"[RAW] 成功提取內嵌縮圖: {thumbnail.Width}x{thumbnail.Height}");

                                        if (thumbnail.Width > maxSize || thumbnail.Height > maxSize)
                                        {
                                            var scale = Math.Min((float)maxSize / thumbnail.Width, (float)maxSize / thumbnail.Height);
                                            var targetWidth = (int)(thumbnail.Width * scale);
                                            var targetHeight = (int)(thumbnail.Height * scale);
                                            var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);
                                            var resized = thumbnail.Resize(new SKImageInfo(targetWidth, targetHeight), samplingOptions);
                                            thumbnail.Dispose();
                                            return resized;
                                        }
                                        return thumbnail;
                                    }
                                    thumbnail?.Dispose();
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RAW] MetadataExtractor 提取失敗: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 解碼縮圖
    /// </summary>
    public SKBitmap? DecodeThumbnail(string filePath, int maxSize = 128)
    {
        if (ImageUtils.IsRawFile(filePath))
        {
            // 策略 1: LibRaw 提取內嵌縮圖（最快）
            var librawThumbnail = _libRawDecoder.ExtractThumbnail(filePath, maxSize);
            if (librawThumbnail != null)
            {
                return librawThumbnail;
            }

            // 策略 2: LibRaw 半尺寸解碼
            var librawHalfSize = _libRawDecoder.DecodeHalfSize(filePath);
            if (librawHalfSize != null)
            {
                if (librawHalfSize.Width > maxSize || librawHalfSize.Height > maxSize)
                {
                    var scale = Math.Min((float)maxSize / librawHalfSize.Width, (float)maxSize / librawHalfSize.Height);
                    var targetWidth = (int)(librawHalfSize.Width * scale);
                    var targetHeight = (int)(librawHalfSize.Height * scale);
                    var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);
                    var resized = librawHalfSize.Resize(new SKImageInfo(targetWidth, targetHeight), samplingOptions);
                    librawHalfSize.Dispose();
                    return resized;
                }
                return librawHalfSize;
            }

            // 策略 3: MetadataExtractor 提取內嵌縮圖
            var embeddedThumbnail = ExtractRawEmbeddedThumbnail(filePath, maxSize);
            if (embeddedThumbnail != null)
            {
                return embeddedThumbnail;
            }

            Console.WriteLine($"[RAW] 無法解碼縮圖: {Path.GetFileName(filePath)}");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            if (codec == null)
                return null;

            var info = codec.Info;
            float scale = Math.Min((float)maxSize / info.Width, (float)maxSize / info.Height);

            if (scale >= 1.0f)
            {
                var bitmap = new SKBitmap(info);
                var result = codec.GetPixels(info, bitmap.GetPixels());
                if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                    return bitmap;
                return null;
            }

            var targetInfo = new SKImageInfo((int)(info.Width * scale), (int)(info.Height * scale));
            var scaledBitmap = new SKBitmap(targetInfo);
            var scaledResult = codec.GetPixels(targetInfo, scaledBitmap.GetPixels());

            if (scaledResult == SKCodecResult.Success || scaledResult == SKCodecResult.IncompleteInput)
            {
                return scaledBitmap;
            }

            throw new NotSupportedException("Scaling not supported for this codec");
        }
        catch (Exception)
        {
            try
            {
                using var original = DecodeBitmap(filePath);
                if (original == null)
                    return null;

                var scale = Math.Min((float)maxSize / original.Width, (float)maxSize / original.Height);
                var targetWidth = (int)(original.Width * scale);
                var targetHeight = (int)(original.Height * scale);

                var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);
                return original.Resize(new SKImageInfo(targetWidth, targetHeight), samplingOptions);
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"Fallback decode failed for {filePath}: {ex2.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 取得圖片尺寸
    /// </summary>
    public (int Width, int Height)? GetImageDimensions(string filePath)
    {
        if (ImageUtils.IsRawFile(filePath))
        {
            try
            {
                var thumbnail = _libRawDecoder.ExtractThumbnail(filePath, 256);
                if (thumbnail != null)
                {
                    var dims = (thumbnail.Width, thumbnail.Height);
                    thumbnail.Dispose();
                    return dims;
                }
            }
            catch { }

            try
            {
                var bitmap = _libRawDecoder.DecodeHalfSize(filePath);
                if (bitmap != null)
                {
                    var dims = (bitmap.Width * 2, bitmap.Height * 2);
                    bitmap.Dispose();
                    return dims;
                }
            }
            catch
            {
                return null;
            }
        }

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
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            using var fileStream = File.Create(outputPath);
            return SaveAsWebP(bitmap, fileStream, quality);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save WebP {outputPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 儲存圖片為 WebP 格式到串流
    /// </summary>
    public bool SaveAsWebP(SKBitmap bitmap, Stream outputStream, int quality = 85)
    {
        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Webp, quality);

            data.SaveTo(outputStream);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save WebP to stream: {ex.Message}");
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
    public AnimatedImage? DecodeGif(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            if (codec == null || codec.FrameCount <= 1)
                return null;

            var animatedImage = new AnimatedImage();
            var info = codec.Info;

            for (int i = 0; i < codec.FrameCount; i++)
            {
                var duration = codec.FrameInfo[i].Duration;
                if (duration < 10) duration = 100;

                var bitmap = new SKBitmap(info);
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
    /// 讀取結構化的 EXIF 資訊
    /// </summary>
    public ExifInfo GetExifInfo(string filePath)
    {
        var info = new ExifInfo();

        try
        {
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);

            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null)
            {
                info.Make = ifd0.GetString(ExifIfd0Directory.TagMake) ?? "";
                info.Model = ifd0.GetString(ExifIfd0Directory.TagModel) ?? "";
            }

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd != null)
            {
                info.Lens = subIfd.GetString(ExifSubIfdDirectory.TagLensModel) ?? "";
                
                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagFNumber))
                {
                    double fNumber = subIfd.GetDouble(ExifSubIfdDirectory.TagFNumber);
                    info.FNumber = $"f/{fNumber:0.0}";
                }

                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagExposureTime))
                {
                    double exposureTime = subIfd.GetDouble(ExifSubIfdDirectory.TagExposureTime);
                    info.ExposureTime = exposureTime < 1.0 
                        ? $"1/{Math.Round(1.0 / exposureTime)}" 
                        : $"{exposureTime}s";
                }

                string? iso = subIfd.GetString(ExifSubIfdDirectory.TagIsoEquivalent);
                if (string.IsNullOrEmpty(iso))
                {
                     if (subIfd.ContainsTag(0x8833))
                        iso = subIfd.GetString(0x8833);
                }
                
                if (!string.IsNullOrEmpty(iso)) info.Iso = $"ISO {iso}";

                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagFocalLength))
                {
                    double focalLength = subIfd.GetDouble(ExifSubIfdDirectory.TagFocalLength);
                    info.FocalLength = $"{focalLength}mm";
                }

                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagDateTimeOriginal))
                {
                    try 
                    {
                        info.DateTaken = subIfd.GetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal);
                    }
                    catch {}
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading structured EXIF: {ex.Message}");
        }

        return info;
    }

    /// <summary>
    /// 讀取完整 EXIF 資訊
    /// </summary>
    public Dictionary<string, string> GetExifData(string filePath)
    {
        var exifData = new Dictionary<string, string>();

        try
        {
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);

            foreach (var directory in directories)
            {
                foreach (var tag in directory.Tags)
                {
                    var key = $"{directory.Name} - {tag.Name}";
                    
                    if (!exifData.ContainsKey(key))
                    {
                        exifData[key] = tag.Description ?? "";
                    }
                }
            }

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
