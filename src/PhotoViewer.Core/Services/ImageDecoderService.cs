using ImageMagick;
using SkiaSharp;
using PhotoViewer.Core.Utilities;
using PhotoViewer.Core.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System.IO;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 圖片解碼服務（使用 SkiaSharp、LibRaw 與 Magick.NET）
/// 混合策略：LibRaw（RAW 快速提取） → Magick.NET（回退）
/// </summary>
public class ImageDecoderService
{
    private readonly LibRawDecoder _libRawDecoder = new();

    // 設定 Magick.NET 資源限制以提升效能
    static ImageDecoderService()
    {
        // 啟用 OpenMP 多執行緒（如果可用）
        ResourceLimits.Thread = (ulong)Environment.ProcessorCount;

        // 限制記憶體使用（避免大型 RAW 檔案耗盡記憶體）
        ResourceLimits.Memory = 2UL * 1024 * 1024 * 1024; // 2GB

        // 啟用 OpenCL GPU 加速（如果可用）
        // 首次使用時會自動進行效能測試，選擇最佳裝置（GPU/CPU）
        try
        {
            OpenCL.IsEnabled = true;

            // 強制使用 GPU（若有可用的 GPU）
            // 可透過環境變數 MAGICK_OCL_DEVICE=GPU 來強制使用 GPU
            // 或 MAGICK_OCL_DEVICE=true 來自動選擇

            Console.WriteLine("[GPU] OpenCL GPU 加速已啟用");
            Console.WriteLine($"[GPU] OpenCL 可用: {OpenCL.IsEnabled}");

            // 列出可用裝置
            var devices = OpenCL.Devices;
            if (devices != null && devices.Any())
            {
                Console.WriteLine($"[GPU] 找到 {devices.Count()} 個 OpenCL 裝置:");
                foreach (var device in devices)
                {
                    Console.WriteLine($"  - {device.Name} ({device.DeviceType})");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GPU] OpenCL 初始化失敗 (將使用 CPU): {ex.Message}");
        }
    }
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
        try
        {
            using var image = new MagickImage();

            // 優化設定以加速 RAW 解碼
            var settings = new MagickReadSettings
            {
                // 使用半尺寸解碼（對於大型 RAW 檔案可節省 75% 時間和記憶體）
                // 如果需要完整解析度，請移除此設定
                // Width = 0 表示保持原始尺寸
            };

            image.Read(filePath, settings);

            // 轉換為 SkiaSharp 可理解的格式（使用 JPEG 比 PNG 快）
            using var memStream = new MemoryStream();
            image.Quality = 95; // 高品質 JPEG
            image.Write(memStream, MagickFormat.Jpeg);
            memStream.Position = 0;
            return SKBitmap.Decode(memStream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to decode RAW image {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 嘗試提取 RAW 檔案的內嵌縮圖（極快，避免完整解碼）
    /// 策略 1: 使用 MetadataExtractor 直接提取 EXIF/Maker Note 縮圖
    /// 策略 2: 使用 Magick.NET Profile 提取
    /// </summary>
    private SKBitmap? ExtractRawEmbeddedThumbnail(string filePath, int maxSize)
    {
        // 策略 1: 使用 MetadataExtractor 提取內嵌 JPEG 縮圖（最快）
        try
        {
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);

            // 嘗試從不同的目錄提取縮圖
            foreach (var directory in directories)
            {
                // 檢查是否為縮圖相關目錄
                var dirName = directory.Name.ToLower();
                if (!dirName.Contains("thumbnail") && !dirName.Contains("preview") && !dirName.Contains("jpeg"))
                    continue;

                foreach (var tag in directory.Tags)
                {
                    // 尋找縮圖數據標籤
                    var tagName = tag.Name?.ToLower() ?? "";
                    var tagDesc = tag.Description?.ToLower() ?? "";

                    if (tagName.Contains("thumbnail") || tagName.Contains("preview") ||
                        tagName.Contains("jpeginterchangeformat") || tagDesc.Contains("bytes"))
                    {
                        try
                        {
                            // 嘗試獲取二進位數據
                            var obj = directory.GetObject(tag.Type);
                            if (obj is byte[] thumbnailData && thumbnailData.Length > 1000) // 至少 1KB
                            {
                                // 驗證是否為有效的 JPEG（檢查 JPEG 簽名）
                                if (thumbnailData.Length > 2 &&
                                    thumbnailData[0] == 0xFF && thumbnailData[1] == 0xD8)
                                {
                                    using var thumbnailStream = new MemoryStream(thumbnailData);
                                    var thumbnail = SKBitmap.Decode(thumbnailStream);

                                    if (thumbnail != null && thumbnail.Width > 50 && thumbnail.Height > 50)
                                    {
                                        Console.WriteLine($"[RAW] 成功提取內嵌縮圖: {thumbnail.Width}x{thumbnail.Height} from {Path.GetFileName(filePath)}");

                                        // 如果縮圖太大，縮小它
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
                        catch
                        {
                            // 繼續嘗試其他標籤
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RAW] MetadataExtractor 提取失敗: {ex.Message}");
        }

        // 策略 2: 使用 Magick.NET 的 EXIF Profile（較慢但更可靠）
        try
        {
            using var image = new MagickImage();
            image.Ping(filePath); // 只讀取元數據，不解碼圖片

            var exifProfile = image.GetExifProfile();
            if (exifProfile != null)
            {
                // 嘗試獲取縮圖數據
                var thumbnailValue = exifProfile.GetValue(ImageMagick.ExifTag.JPEGInterchangeFormat);
                var lengthValue = exifProfile.GetValue(ImageMagick.ExifTag.JPEGInterchangeFormatLength);

                if (thumbnailValue != null && lengthValue != null)
                {
                    // 從原始檔案讀取縮圖數據
                    using var fileStream = File.OpenRead(filePath);
                    fileStream.Seek(Convert.ToInt64(thumbnailValue.Value), SeekOrigin.Begin);

                    var thumbnailData = new byte[Convert.ToInt32(lengthValue.Value)];
                    fileStream.Read(thumbnailData, 0, thumbnailData.Length);

                    using var thumbnailStream = new MemoryStream(thumbnailData);
                    var thumbnail = SKBitmap.Decode(thumbnailStream);

                    if (thumbnail != null)
                    {
                        Console.WriteLine($"[RAW] Magick.NET 提取縮圖成功: {thumbnail.Width}x{thumbnail.Height}");

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
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RAW] Magick.NET Profile 提取失敗: {ex.Message}");
        }

        Console.WriteLine($"[RAW] 無法提取內嵌縮圖，將使用完整解碼: {Path.GetFileName(filePath)}");
        return null;
    }

    /// <summary>
    /// 解碼縮圖（使用 SKCodec 進行優化解碼）
    /// </summary>
    public SKBitmap? DecodeThumbnail(string filePath, int maxSize = 128)
    {
        if (ImageUtils.IsRawFile(filePath))
        {
            // 四級回退策略（優先級從高到低）

            // 策略 1: LibRaw 提取內嵌縮圖（最快，10-50ms）
            var librawThumbnail = _libRawDecoder.ExtractThumbnail(filePath, maxSize);
            if (librawThumbnail != null)
            {
                return librawThumbnail;
            }

            // 策略 2: LibRaw 半尺寸解碼（快速回退，100-200ms）
            var librawHalfSize = _libRawDecoder.DecodeHalfSize(filePath);
            if (librawHalfSize != null)
            {
                // 縮小到 maxSize
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

            // 策略 3: MetadataExtractor/Magick.NET 提取內嵌縮圖（兼容性回退）
            var embeddedThumbnail = ExtractRawEmbeddedThumbnail(filePath, maxSize);
            if (embeddedThumbnail != null)
            {
                return embeddedThumbnail;
            }

            // 策略 4: Magick.NET 完整解碼（最慢但最可靠，200-500ms）
            try
            {
                using var image = new MagickImage();

                // 優化設定：降低品質以加速
                var settings = new MagickReadSettings
                {
                    // 使用草稿模式（draft mode）加速 RAW 解碼
                    Width = (uint)maxSize,
                    Height = (uint)maxSize,
                };

                image.Read(filePath, settings);

                // 確保縮圖不超過 maxSize
                if (image.Width > maxSize || image.Height > maxSize)
                {
                    image.Resize((uint)maxSize, (uint)maxSize);
                }

                using var memStream = new MemoryStream();
                image.Write(memStream, MagickFormat.Png);
                memStream.Position = 0;
                return SKBitmap.Decode(memStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Magick.NET] RAW 縮圖解碼失敗: {ex.Message}");
                return null;
            }
        }

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
                var bitmap = new SKBitmap(info);
                var result = codec.GetPixels(info, bitmap.GetPixels());
                if (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput)
                    return bitmap;
                return null;
            }

            // 計算目標尺寸
            var targetInfo = new SKImageInfo((int)(info.Width * scale), (int)(info.Height * scale));

            // 嘗試直接解碼為縮小的尺寸
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
            // Fallback: 傳統方式 (先載入再縮放)
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
    /// 取得圖片尺寸（不載入完整圖片）
    /// </summary>
    public (int Width, int Height)? GetImageDimensions(string filePath)
    {
        if (ImageUtils.IsRawFile(filePath))
        {
            try
            {
                // Ping is faster than reading the file
                var info = new MagickImageInfo(filePath);
                return ((int)info.Width, (int)info.Height);
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
    /// 儲存圖片為 WebP 格式到檔案
    /// </summary>
    public bool SaveAsWebP(SKBitmap bitmap, string outputPath, int quality = 85)
    {
        try
        {
            // 確保目錄存在
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
    /// 讀取結構化的 EXIF 資訊
    /// </summary>
    public ExifInfo GetExifInfo(string filePath)
    {
        var info = new ExifInfo();

        try
        {
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);

            // 1. 嘗試從 ExifIfd0Directory 讀取相機資訊
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null)
            {
                info.Make = ifd0.GetString(ExifIfd0Directory.TagMake) ?? "";
                info.Model = ifd0.GetString(ExifIfd0Directory.TagModel) ?? "";
            }

            // 2. 嘗試從 ExifSubIfdDirectory 讀取拍攝參數
            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (subIfd != null)
            {
                info.Lens = subIfd.GetString(ExifSubIfdDirectory.TagLensModel) ?? "";
                
                // 光圈
                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagFNumber))
                {
                    double fNumber = subIfd.GetDouble(ExifSubIfdDirectory.TagFNumber);
                    info.FNumber = $"f/{fNumber:0.0}";
                }

                // 快門
                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagExposureTime))
                {
                    double exposureTime = subIfd.GetDouble(ExifSubIfdDirectory.TagExposureTime);
                    info.ExposureTime = exposureTime < 1.0 
                        ? $"1/{Math.Round(1.0 / exposureTime)}" 
                        : $"{exposureTime}s";
                }

                // ISO
                // TagIsoEquivalent = 0x8827, TagIsoSpeed = 0x8833
                string? iso = subIfd.GetString(ExifSubIfdDirectory.TagIsoEquivalent);
                if (string.IsNullOrEmpty(iso))
                {
                     // 嘗試其他 ISO 標籤
                     if (subIfd.ContainsTag(0x8833)) // TagIsoSpeed
                        iso = subIfd.GetString(0x8833);
                }
                
                if (!string.IsNullOrEmpty(iso)) info.Iso = $"ISO {iso}";

                // 焦距
                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagFocalLength))
                {
                    double focalLength = subIfd.GetDouble(ExifSubIfdDirectory.TagFocalLength);
                    info.FocalLength = $"{focalLength}mm";
                }

                // 拍攝時間
                if (subIfd.ContainsTag(ExifSubIfdDirectory.TagDateTimeOriginal))
                {
                    try 
                    {
                        info.DateTaken = subIfd.GetDateTime(ExifSubIfdDirectory.TagDateTimeOriginal);
                    }
                    catch {}
                }
            }

            // 3. 嘗試從 GpsDirectory 讀取位置
            /*
            var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
            if (gps != null)
            {
                var location = gps.GetGeoLocation();
                if (location != null)
                {
                    info.Latitude = location.Latitude;
                    info.Longitude = location.Longitude;
                }
            }
            */
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading structured EXIF: {ex.Message}");
        }

        return info;
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
