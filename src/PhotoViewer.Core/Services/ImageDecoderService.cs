using SkiaSharp;
using PhotoViewer.Core.Utilities;
using PhotoViewer.Core.Models;
using System.IO;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 圖片解碼服務
/// 負責解碼各種圖片格式，包括 RAW 檔案
/// 
/// 支援的 RAW 格式由 ImageUtils.RawExtensions 定義
/// 注意：Nikon Z9 HE/HE* 格式目前 LibRaw 不支援，需等待 LibRaw 更新
/// </summary>
public class ImageDecoderService
{
    #region Fields

    private readonly LibRawDecoder _libRawDecoder = new();
    private readonly ExifService _exifService = new();

    #endregion

    #region Public Methods - Full Image Decoding

    /// <summary>
    /// 解碼完整圖片
    /// </summary>
    /// <param name="filePath">圖片檔案路徑</param>
    /// <returns>解碼後的 SKBitmap，失敗時回傳 null</returns>
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
            Console.WriteLine($"[Decode] 解碼失敗 {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 非同步解碼完整圖片
    /// </summary>
    public Task<SKBitmap?> DecodeBitmapAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() => DecodeBitmap(filePath), ct);
    }

    #endregion

    #region Public Methods - Thumbnail Decoding

    /// <summary>
    /// 解碼縮圖
    /// </summary>
    /// <param name="filePath">圖片檔案路徑</param>
    /// <param name="maxSize">縮圖最大尺寸</param>
    /// <returns>解碼後的縮圖 SKBitmap，失敗時回傳 null</returns>
    public SKBitmap? DecodeThumbnail(string filePath, int maxSize = 128)
    {
        if (ImageUtils.IsRawFile(filePath))
        {
            return DecodeRawThumbnail(filePath, maxSize);
        }

        return DecodeStandardThumbnail(filePath, maxSize);
    }

    /// <summary>
    /// 非同步解碼縮圖
    /// </summary>
    public Task<SKBitmap?> DecodeThumbnailAsync(string filePath, int maxSize = 128, CancellationToken ct = default)
    {
        return Task.Run(() => DecodeThumbnail(filePath, maxSize), ct);
    }

    #endregion

    #region Public Methods - GIF Animation

    /// <summary>
    /// 解碼 GIF 動畫
    /// </summary>
    /// <param name="filePath">GIF 檔案路徑</param>
    /// <returns>包含所有影格的 AnimatedImage，失敗時回傳 null</returns>
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
                if (duration < 10) duration = 100; // 最小 10ms，預設 100ms

                var bitmap = new SKBitmap(info);
                var options = new SKCodecOptions(i);

                codec.GetPixels(info, bitmap.GetPixels(), options);
                animatedImage.AddFrame(bitmap, duration);
            }

            return animatedImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GIF] 解碼失敗 {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Public Methods - Image Info

    /// <summary>
    /// 取得圖片尺寸（不載入完整圖片）
    /// </summary>
    public (int Width, int Height)? GetImageDimensions(string filePath)
    {
        if (ImageUtils.IsRawFile(filePath))
        {
            return GetRawDimensions(filePath);
        }

        return GetStandardDimensions(filePath);
    }

    #endregion

    #region Public Methods - EXIF (委派給 ExifService)

    /// <summary>
    /// 讀取結構化的 EXIF 資訊
    /// </summary>
    public ExifInfo GetExifInfo(string filePath)
    {
        return _exifService.GetExifInfo(filePath);
    }

    /// <summary>
    /// 讀取完整 EXIF 資訊
    /// </summary>
    public Dictionary<string, string> GetExifData(string filePath)
    {
        return _exifService.GetExifData(filePath);
    }

    #endregion

    #region Public Methods - Image Saving

    /// <summary>
    /// 儲存圖片為 WebP 格式到檔案
    /// </summary>
    public bool SaveAsWebP(SKBitmap bitmap, string outputPath, int quality = 85)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = File.Create(outputPath);
            return SaveAsWebP(bitmap, fileStream, quality);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Save] WebP 儲存失敗 {outputPath}: {ex.Message}");
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
            Console.WriteLine($"[Save] WebP 串流儲存失敗: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Private Methods - RAW Decoding

    /// <summary>
    /// 解碼 RAW 檔案（完整解析度）
    /// </summary>
    private SKBitmap? DecodeRaw(string filePath)
    {
        // 策略 1: LibRaw 完整解碼
        var bitmap = _libRawDecoder.DecodeFull(filePath);
        if (bitmap != null) return bitmap;

        // 策略 2: LibRaw 半尺寸解碼（回退）
        bitmap = _libRawDecoder.DecodeHalfSize(filePath);
        if (bitmap != null)
        {
            Console.WriteLine($"[RAW] 使用半尺寸解碼: {Path.GetFileName(filePath)}");
            return bitmap;
        }

        Console.WriteLine($"[RAW] 無法解碼: {Path.GetFileName(filePath)}");
        return null;
    }

    /// <summary>
    /// 解碼 RAW 縮圖
    /// </summary>
    private SKBitmap? DecodeRawThumbnail(string filePath, int maxSize)
    {
        // 策略 1: LibRaw 提取內嵌縮圖（最快）
        var thumbnail = _libRawDecoder.ExtractThumbnail(filePath, maxSize);
        if (thumbnail != null) return thumbnail;

        // 策略 2: LibRaw 半尺寸解碼
        var halfSize = _libRawDecoder.DecodeHalfSize(filePath);
        if (halfSize != null)
        {
            return ResizeBitmapIfNeeded(halfSize, maxSize);
        }

        // 策略 3: MetadataExtractor 提取內嵌縮圖
        var embedded = ExtractEmbeddedThumbnail(filePath, maxSize);
        if (embedded != null) return embedded;

        Console.WriteLine($"[RAW] 無法解碼縮圖: {Path.GetFileName(filePath)}");
        return null;
    }

    #endregion

    #region Private Methods - Standard Image Decoding

    /// <summary>
    /// 解碼標準格式縮圖（JPG、PNG 等）
    /// </summary>
    private SKBitmap? DecodeStandardThumbnail(string filePath, int maxSize)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            if (codec == null) return null;

            var info = codec.Info;
            float scale = Math.Min((float)maxSize / info.Width, (float)maxSize / info.Height);

            // 圖片小於縮圖尺寸，直接解碼
            if (scale >= 1.0f)
            {
                var bitmap = new SKBitmap(info);
                var result = codec.GetPixels(info, bitmap.GetPixels());
                return (result == SKCodecResult.Success || result == SKCodecResult.IncompleteInput) ? bitmap : null;
            }

            // 縮放解碼
            var targetInfo = new SKImageInfo((int)(info.Width * scale), (int)(info.Height * scale));
            var scaledBitmap = new SKBitmap(targetInfo);
            var scaledResult = codec.GetPixels(targetInfo, scaledBitmap.GetPixels());

            if (scaledResult == SKCodecResult.Success || scaledResult == SKCodecResult.IncompleteInput)
            {
                return scaledBitmap;
            }

            throw new NotSupportedException("縮放解碼不支援此格式");
        }
        catch
        {
            // 回退：先載入完整圖片再縮放
            return DecodeAndResizeFallback(filePath, maxSize);
        }
    }

    /// <summary>
    /// 回退解碼：先載入完整圖片再縮放
    /// </summary>
    private SKBitmap? DecodeAndResizeFallback(string filePath, int maxSize)
    {
        try
        {
            using var original = DecodeBitmap(filePath);
            if (original == null) return null;

            return ResizeBitmapIfNeeded(original, maxSize);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Decode] 回退解碼失敗 {Path.GetFileName(filePath)}: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region Private Methods - Image Dimensions

    /// <summary>
    /// 取得 RAW 圖片尺寸
    /// </summary>
    private (int Width, int Height)? GetRawDimensions(string filePath)
    {
        // 嘗試從縮圖取得尺寸
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

        // 嘗試從半尺寸解碼估計
        try
        {
            var halfSize = _libRawDecoder.DecodeHalfSize(filePath);
            if (halfSize != null)
            {
                var dims = (halfSize.Width * 2, halfSize.Height * 2);
                halfSize.Dispose();
                return dims;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 取得標準格式圖片尺寸
    /// </summary>
    private (int Width, int Height)? GetStandardDimensions(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);

            return codec != null ? (codec.Info.Width, codec.Info.Height) : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Private Methods - Thumbnail Extraction

    /// <summary>
    /// 使用 MetadataExtractor 提取內嵌縮圖
    /// </summary>
    private SKBitmap? ExtractEmbeddedThumbnail(string filePath, int maxSize)
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
                        var thumbnail = TryExtractThumbnailFromTag(directory, tag, maxSize);
                        if (thumbnail != null) return thumbnail;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Thumbnail] MetadataExtractor 提取失敗: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 嘗試從特定標籤提取縮圖
    /// </summary>
    private SKBitmap? TryExtractThumbnailFromTag(MetadataExtractor.Directory directory, MetadataExtractor.Tag tag, int maxSize)
    {
        try
        {
            var obj = directory.GetObject(tag.Type);
            if (obj is not byte[] thumbnailData || thumbnailData.Length <= 1000)
                return null;

            // 驗證 JPEG 簽名
            if (thumbnailData[0] != 0xFF || thumbnailData[1] != 0xD8)
                return null;

            using var stream = new MemoryStream(thumbnailData);
            var thumbnail = SKBitmap.Decode(stream);

            if (thumbnail == null || thumbnail.Width <= 50 || thumbnail.Height <= 50)
            {
                thumbnail?.Dispose();
                return null;
            }

            Console.WriteLine($"[Thumbnail] 成功提取內嵌縮圖: {thumbnail.Width}x{thumbnail.Height}");
            return ResizeBitmapIfNeeded(thumbnail, maxSize);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Private Methods - Image Utilities

    /// <summary>
    /// 如果圖片超過指定大小則縮放
    /// </summary>
    private SKBitmap? ResizeBitmapIfNeeded(SKBitmap bitmap, int maxSize)
    {
        if (bitmap.Width <= maxSize && bitmap.Height <= maxSize)
            return bitmap;

        var scale = Math.Min((float)maxSize / bitmap.Width, (float)maxSize / bitmap.Height);
        var targetWidth = (int)(bitmap.Width * scale);
        var targetHeight = (int)(bitmap.Height * scale);

        var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);
        var resized = bitmap.Resize(new SKImageInfo(targetWidth, targetHeight), samplingOptions);

        bitmap.Dispose();
        return resized;
    }

    #endregion
}
