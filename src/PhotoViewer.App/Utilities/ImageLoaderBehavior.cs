using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using PhotoViewer.Core.Services;
using System.IO;

namespace PhotoViewer.App.Utilities;

public static class ImageLoaderBehavior
{
    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.RegisterAttached(
            "ImagePath",
            typeof(string),
            typeof(ImageLoaderBehavior),
            new PropertyMetadata(null, OnImagePathChanged));

    public static string GetImagePath(DependencyObject obj)
    {
        return (string)obj.GetValue(ImagePathProperty);
    }

    public static void SetImagePath(DependencyObject obj, string value)
    {
        obj.SetValue(ImagePathProperty, value);
    }

    // 追蹤每個控制項的 CancellationTokenSource
    private static readonly Dictionary<Image, CancellationTokenSource> _cancellationTokens = new();

    // 共享的 ImageLoaderService 實例
    private static ImageLoaderService? _imageLoader;

    public static void Initialize(ImageLoaderService loader)
    {
        _imageLoader = loader;
    }

    private static async void OnImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image imageControl) return;

        var path = e.NewValue as string;

        // 取消之前的載入任務
        CancelLoading(imageControl);

        // 清除舊圖片
        imageControl.Source = null;

        if (string.IsNullOrEmpty(path))
        {
            Console.WriteLine("[ImageLoaderBehavior] Path is null or empty");
            return;
        }

        if (_imageLoader == null)
        {
            Console.WriteLine("[ImageLoaderBehavior] ERROR: _imageLoader is null! ImageLoaderBehavior.Initialize() was not called!");
            return;
        }

        Console.WriteLine($"[ImageLoaderBehavior] Loading thumbnail for: {path}");

        // 建立新的 CancellationTokenSource
        var cts = new CancellationTokenSource();
        _cancellationTokens[imageControl] = cts;

        try
        {
            // 非同步載入 (傳遞 CancellationToken)
            var bitmap = await _imageLoader.LoadThumbnailAsync(path, cts.Token);

            // 如果任務被取消，則不更新 UI
            if (cts.Token.IsCancellationRequested) return;

            if (bitmap != null)
            {
                // 轉換為 BitmapSource
                var bitmapSource = await Task.Run(() => ConvertSkBitmapToBitmapSource(bitmap), cts.Token);
                
                if (cts.Token.IsCancellationRequested) return;

                // 在 UI 執行緒更新
                imageControl.Source = bitmapSource;
            }
        }
        catch (OperationCanceledException)
        {
            // 忽略取消異常
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageLoaderBehavior] Error loading image {path}: {ex.Message}");
            Console.WriteLine($"[ImageLoaderBehavior] Stack trace: {ex.StackTrace}");
        }
        finally
        {
            // 清理 CTS
            if (_cancellationTokens.TryGetValue(imageControl, out var existingCts) && existingCts == cts)
            {
                _cancellationTokens.Remove(imageControl);
                cts.Dispose();
            }
        }
    }

    private static void CancelLoading(Image imageControl)
    {
        if (_cancellationTokens.TryGetValue(imageControl, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _cancellationTokens.Remove(imageControl);
        }
    }

    private static BitmapSource ConvertSkBitmapToBitmapSource(SkiaSharp.SKBitmap bitmap)
    {
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

        var memoryStream = new MemoryStream();
        data.SaveTo(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = memoryStream;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }
}
