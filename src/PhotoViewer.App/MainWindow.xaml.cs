using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoViewer.Core.Models;
using PhotoViewer.Core.Services;
using PhotoViewer.Core.Utilities;
using PhotoViewer.App.Views;

namespace PhotoViewer.App;

/// <summary>
/// LuminaView 主視窗
/// </summary>
public partial class MainWindow : Window
{
    private ImageLoaderService? _imageLoader;
    private readonly FileWatcherService _fileWatcher;
    private List<ImageItem> _currentImages = new();
    private string _currentFolderPath = string.Empty;
    private System.Windows.Threading.DispatcherTimer? _memoryUpdateTimer;

    public MainWindow()
    {
        try
        {
            InitializeComponent();

            // 延遲初始化 ImageLoader，避免在啟動時就創建資料庫
            // _imageLoader = new ImageLoaderService();
            _fileWatcher = new FileWatcherService();

            // 訂閱檔案監控事件
            _fileWatcher.FileCreated += OnFileCreated;
            _fileWatcher.FileDeleted += OnFileDeleted;
            _fileWatcher.FileModified += OnFileModified;

            // 設定視窗關閉事件
            Closing += MainWindow_Closing;

            // 啟動記憶體使用量更新定時器
            _memoryUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _memoryUpdateTimer.Tick += UpdateMemoryUsage;
            _memoryUpdateTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化錯誤: {ex.Message}\n\n{ex.StackTrace}",
                "啟動錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }

    /// <summary>
    /// 開啟檔案夾按鈕點擊事件
    /// </summary>
    private async void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        // 使用 WPF 原生的 OpenFolderDialog (需要 Windows 10 或更新版本)
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "選擇包含圖片的檔案夾"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadFolderAsync(dialog.FolderName);
        }
    }

    /// <summary>
    /// 載入檔案夾中的所有圖片
    /// </summary>
    private async Task LoadFolderAsync(string folderPath)
    {
        try
        {
            // 延遲初始化 ImageLoader（只在需要時才創建）
            if (_imageLoader == null)
            {
                StatusTextBlock.Text = "正在初始化快取系統...";
                await Task.Run(() =>
                {
                    _imageLoader = new ImageLoaderService();
                });
                StatusTextBlock.Text = "快取系統初始化完成";
            }

            _currentFolderPath = folderPath;

            // 更新 UI
            StatusTextBlock.Text = "正在掃描檔案夾...";
            OpenFolderButton.IsEnabled = false;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ImageWrapPanel.Children.Clear();

            // 開始監控檔案夾
            _fileWatcher.WatchFolder(folderPath, includeSubdirectories: true);

            // 在背景執行緒掃描圖片
            var imageFiles = await Task.Run(() => ScanImageFiles(folderPath));

            _currentImages = imageFiles.Select(path => new ImageItem
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                Modified = File.GetLastWriteTime(path),
                FileSize = new FileInfo(path).Length,
                Format = ImageUtils.GetImageFormat(path),
                Created = File.GetCreationTime(path)
            }).ToList();

            // 更新 UI
            FolderPathTextBlock.Text = folderPath;
            ImageCountTextBlock.Text = $"共 {_currentImages.Count} 張圖片";
            StatusTextBlock.Text = $"已載入 {_currentImages.Count} 張圖片";

            // 顯示縮圖
            await LoadThumbnailsAsync();

            OpenFolderButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"載入檔案夾時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusTextBlock.Text = "載入失敗";
            OpenFolderButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// 掃描檔案夾中的所有圖片
    /// </summary>
    private List<string> ScanImageFiles(string folderPath)
    {
        var imageFiles = new List<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories))
            {
                if (ImageUtils.IsSupportedImage(file))
                {
                    imageFiles.Add(file);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 忽略無權限存取的檔案夾
        }

        return imageFiles;
    }

    /// <summary>
    /// 載入縮圖
    /// </summary>
    private async Task LoadThumbnailsAsync()
    {
        const int batchSize = 20; // 每次處理 20 張
        int loadedCount = 0;

        // 先建立所有控制項
        var tasks = new List<Task>();

        foreach (var image in _currentImages)
        {
            try
            {
                // 建立縮圖控制項
                var thumbnailBorder = CreateThumbnailControl(image);
                ImageWrapPanel.Children.Add(thumbnailBorder);

                loadedCount++;

                // 每批次後更新狀態
                if (loadedCount % batchSize == 0)
                {
                    StatusTextBlock.Text = $"正在載入縮圖... ({loadedCount}/{_currentImages.Count})";
                    await Task.Delay(10); // 讓 UI 更新
                }

                // 加入載入任務（但不等待）
                tasks.Add(LoadThumbnailImageAsync(image, thumbnailBorder));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create thumbnail for {image.FilePath}: {ex.Message}");
            }
        }

        StatusTextBlock.Text = $"正在載入 {_currentImages.Count} 張縮圖...";

        // 等待所有縮圖載入完成（在背景）
        _ = Task.Run(async () =>
        {
            await Task.WhenAll(tasks);
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = $"已載入 {_currentImages.Count} 張圖片";
            });
        });
    }

    /// <summary>
    /// 建立縮圖控制項
    /// </summary>
    private Border CreateThumbnailControl(ImageItem image)
    {
        var border = new Border
        {
            Width = 150,
            Height = 150,
            Margin = new Thickness(5),
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = image
        };

        var grid = new Grid();

        // 載入指示器
        var loadingText = new TextBlock
        {
            Text = "載入中...",
            Foreground = new SolidColorBrush(Colors.Gray),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11
        };
        grid.Children.Add(loadingText);

        // 圖片
        var imageControl = new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(5),
            Name = "ThumbnailImage"
        };
        grid.Children.Add(imageControl);

        // 檔案名稱
        var fileName = new TextBlock
        {
            Text = image.FileName,
            Foreground = new SolidColorBrush(Colors.LightGray),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(5),
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 140
        };
        grid.Children.Add(fileName);

        border.Child = grid;

        // 點擊事件 - 開啟檢視器
        border.MouseLeftButtonDown += (s, e) =>
        {
            var clickedImage = border.Tag as ImageItem;
            if (clickedImage != null)
            {
                var index = _currentImages.IndexOf(clickedImage);
                if (index >= 0)
                {
                    var viewerWindow = new ViewerView(_currentImages, index);
                    viewerWindow.Show();
                }
            }
        };

        return border;
    }

    /// <summary>
    /// 非同步載入縮圖（使用快取系統）
    /// </summary>
    private async Task LoadThumbnailImageAsync(ImageItem image, Border border)
    {
        try
        {
            if (_imageLoader == null)
            {
                Console.WriteLine("ImageLoader is null!");
                return;
            }

            Console.WriteLine($"Loading thumbnail for: {image.FileName}");
            var bitmap = await _imageLoader.LoadThumbnailAsync(image.FilePath);
            Console.WriteLine($"Bitmap loaded: {bitmap != null}, Size: {bitmap?.Width}x{bitmap?.Height}");

            if (bitmap == null)
            {
                Console.WriteLine($"Failed to load bitmap for: {image.FileName}");
                // 在 UI 執行緒移除載入文字
                await Dispatcher.InvokeAsync(() =>
                {
                    var grid = border.Child as Grid;
                    if (grid != null)
                    {
                        var loadingText = grid.Children.OfType<TextBlock>()
                            .FirstOrDefault(tb => tb.Text.Contains("載入"));
                        if (loadingText != null)
                        {
                            grid.Children.Remove(loadingText);
                        }
                    }
                });
                return;
            }

            // 在背景執行緒轉換 SKBitmap 為 WPF BitmapSource
            BitmapSource? bitmapSource = null;
            try
            {
                bitmapSource = await Task.Run(() => ConvertSkBitmapToBitmapSource(bitmap));
                Console.WriteLine($"BitmapSource created: {bitmapSource != null}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting bitmap: {ex.Message}\n{ex.StackTrace}");
                return;
            }

            if (bitmapSource == null)
            {
                Console.WriteLine($"Failed to convert bitmap to BitmapSource for: {image.FileName}");
                return;
            }

            // 更新 UI（必須在 UI 執行緒）
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var grid = border.Child as Grid;
                    if (grid == null)
                    {
                        Console.WriteLine("Grid is null!");
                        return;
                    }

                    // 移除載入文字
                    var loadingText = grid.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Text.Contains("載入"));
                    if (loadingText != null)
                    {
                        grid.Children.Remove(loadingText);
                        Console.WriteLine($"Removed loading text for: {image.FileName}");
                    }

                    var imageControl = grid.Children.OfType<Image>()
                        .FirstOrDefault(img => img.Name == "ThumbnailImage");
                    if (imageControl != null)
                    {
                        imageControl.Source = bitmapSource;
                        imageControl.Visibility = Visibility.Visible;
                        Console.WriteLine($"Image source set for: {image.FileName}");
                    }
                    else
                    {
                        Console.WriteLine($"ImageControl not found for: {image.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating UI: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load thumbnail for {image.FilePath}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// 轉換 SKBitmap 為 WPF BitmapSource
    /// </summary>
    private BitmapSource ConvertSkBitmapToBitmapSource(SkiaSharp.SKBitmap bitmap)
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

    // 檔案監控事件處理
    private void OnFileCreated(object? sender, FileSystemEventArgs e)
    {
        // 未來實作：動態新增圖片到列表
    }

    private void OnFileDeleted(object? sender, FileSystemEventArgs e)
    {
        // 未來實作：從列表移除圖片
    }

    private void OnFileModified(object? sender, FileSystemEventArgs e)
    {
        // 未來實作：重新載入修改的圖片
    }

    /// <summary>
    /// 更新記憶體使用量顯示
    /// </summary>
    private async void UpdateMemoryUsage(object? sender, EventArgs e)
    {
        try
        {
            var processMemory = GC.GetTotalMemory(false);

            if (_imageLoader != null)
            {
                var stats = await _imageLoader.GetCacheStatisticsAsync();
                MemoryUsageTextBlock.Text = $"記憶體: {ImageUtils.FormatFileSize(processMemory)} | " +
                                           $"快取: {stats.MemoryCacheSizeFormatted} ({stats.MemoryCacheCount} 項)";
            }
            else
            {
                MemoryUsageTextBlock.Text = $"記憶體: {ImageUtils.FormatFileSize(processMemory)}";
            }
        }
        catch { }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _memoryUpdateTimer?.Stop();
        _fileWatcher.Dispose();
        _imageLoader?.Dispose();
    }
}