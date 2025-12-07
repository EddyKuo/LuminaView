using SkiaSharp;
using System;
using System.IO;
using Sdcb.LibRaw;

namespace PhotoViewer.Core.Services;

/// <summary>
/// LibRaw-based RAW decoder for high-performance thumbnail extraction
/// LibRaw 提供比 Magick.NET 更快的 RAW 縮圖提取（5-10x 速度提升）
/// </summary>
public class LibRawDecoder : IDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// Extract embedded thumbnail from RAW file (fastest method)
    /// 從 RAW 檔案提取內嵌 JPEG 縮圖（最快方法，10-50ms）
    /// </summary>
    public SKBitmap? ExtractThumbnail(string filePath, int maxSize = 128)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            using var raw = RawContext.OpenFile(filePath);

            // 提取內嵌縮圖（索引 0 = 第一個縮圖）
            using var thumbnail = raw.ExportThumbnail(thumbnailIndex: 0);

            // 轉換為 byte array
            byte[] imageData = thumbnail.AsSpan<byte>().ToArray();

            // 解碼 JPEG 縮圖為 SKBitmap
            using var ms = new MemoryStream(imageData);
            var bitmap = SKBitmap.Decode(ms);

            if (bitmap == null)
            {
                Console.WriteLine($"[LibRaw] 縮圖解碼失敗: {Path.GetFileName(filePath)}");
                return null;
            }

            Console.WriteLine($"[LibRaw] 提取縮圖成功: {bitmap.Width}x{bitmap.Height}");

            // 如果縮圖大於需求，縮小它
            if (bitmap.Width > maxSize || bitmap.Height > maxSize)
            {
                var scale = Math.Min((float)maxSize / bitmap.Width, (float)maxSize / bitmap.Height);
                var targetWidth = (int)(bitmap.Width * scale);
                var targetHeight = (int)(bitmap.Height * scale);

                var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest);
                var resized = bitmap.Resize(new SKImageInfo(targetWidth, targetHeight), samplingOptions);

                bitmap.Dispose();
                return resized;
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            // 某些 RAW 檔案沒有內嵌縮圖，這是正常的
            Console.WriteLine($"[LibRaw] 無法提取內嵌縮圖: {Path.GetFileName(filePath)} - {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Decode RAW file at half-size (faster than full decode)
    /// 以半尺寸解碼 RAW 檔案（比完整解碼快 4x）
    /// </summary>
    public SKBitmap? DecodeHalfSize(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            using var raw = RawContext.OpenFile(filePath);

            // 解壓縮 RAW 數據
            raw.Unpack();

            // 處理圖像（在 config 中設定所有參數）
            raw.DcrawProcess(config =>
            {
                config.HalfSize = true;        // 半尺寸解碼
                config.UseCameraWb = true;     // 使用相機白平衡
                config.OutputBps = 8;          // 8-bit 輸出
                config.Gamma[0] = 0.45f;       // sRGB gamma (1/2.2)
                config.Gamma[1] = 4.5f;        // Toe slope
                config.Brightness = 1.0f;
            });

            // 製作記憶體影像
            using var processedImage = raw.MakeDcrawMemoryImage();

            // 轉換為 SKBitmap
            var bitmap = ProcessedImageToSKBitmap(processedImage);

            if (bitmap != null)
            {
                Console.WriteLine($"[LibRaw] 半尺寸解碼成功: {bitmap.Width}x{bitmap.Height}");
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LibRaw] 半尺寸解碼失敗: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Decode full resolution RAW file
    /// 完整解碼 RAW 檔案（最高品質但最慢）
    /// </summary>
    public SKBitmap? DecodeFull(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            using var raw = RawContext.OpenFile(filePath);

            // 解壓縮 RAW 數據
            raw.Unpack();

            // 處理圖像（高品質設定）
            raw.DcrawProcess(config =>
            {
                config.HalfSize = false;       // 完整解析度
                config.UseCameraWb = true;     // 使用相機白平衡
                config.OutputBps = 8;          // 8-bit 輸出
                config.Gamma[0] = 0.45f;       // sRGB gamma
                config.Gamma[1] = 4.5f;        // Toe slope
                config.Brightness = 1.0f;
                config.Interpolation = true;   // 啟用去馬賽克
            });

            // 製作記憶體影像
            using var processedImage = raw.MakeDcrawMemoryImage();

            // 轉換為 SKBitmap
            var bitmap = ProcessedImageToSKBitmap(processedImage);

            if (bitmap != null)
            {
                Console.WriteLine($"[LibRaw] 完整解碼成功: {bitmap.Width}x{bitmap.Height}");
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LibRaw] 完整解碼失敗: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 將 LibRaw ProcessedImage 轉換為 SKBitmap
    /// LibRaw 輸出 RGB24 格式，需要轉換為 SkiaSharp 的 BGRA8888
    /// </summary>
    private unsafe SKBitmap ProcessedImageToSKBitmap(ProcessedImage image)
    {
        try
        {
            int width = image.Width;
            int height = image.Height;

            // 建立 BGRA8888 格式的 SKBitmap（SkiaSharp 偏好格式）
            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);

            // 取得來源和目標指標
            IntPtr srcPtr = image.DataPointer;
            IntPtr dstPtr = bitmap.GetPixels();

            // 轉換 RGB24 到 BGRA8888
            byte* src = (byte*)srcPtr;
            byte* dst = (byte*)dstPtr;

            int pixelCount = width * height;

            for (int i = 0; i < pixelCount; i++)
            {
                byte r = src[i * 3 + 0];
                byte g = src[i * 3 + 1];
                byte b = src[i * 3 + 2];

                // BGRA 格式（注意：從 RGB 交換為 BGR）
                dst[i * 4 + 0] = b;    // Blue
                dst[i * 4 + 1] = g;    // Green
                dst[i * 4 + 2] = r;    // Red
                dst[i * 4 + 3] = 255;  // Alpha（完全不透明）
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LibRaw] 轉換為 SKBitmap 失敗: {ex.Message}");
            return null!;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // LibRaw 資源由 RawContext 的 using 自動釋放
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
