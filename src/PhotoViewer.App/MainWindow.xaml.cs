using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PhotoViewer.Core.Models;
using PhotoViewer.Core.Services;
using PhotoViewer.Core.Utilities;
using PhotoViewer.App.Views;
using System.Collections.ObjectModel;

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

    public ObservableCollection<FolderNode> Folders { get; set; } = new();

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

            // 初始化檔案夾樹
            InitializeFolderTree();
            
            // 設定 DataContext
            DataContext = this;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化錯誤: {ex.Message}\n\n{ex.StackTrace}",
                "啟動錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
        }


    private void InitializeFolderTree()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                var node = new FolderNode(drive.Name);
                node.AddDummyNode(); // 支援延遲加載
                Folders.Add(node);
            }
        }
    }

    private async void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNode node)
        {
            StatusTextBlock.Text = $"Selected: {node.FullPath}"; // Debug
            await LoadFolderAsync(node.FullPath);
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
                
                // 初始化 ImageLoaderBehavior
                PhotoViewer.App.Utilities.ImageLoaderBehavior.Initialize(_imageLoader!);
                
                // 訂閱載入狀態事件
                _imageLoader.LoadingStatusChanged += OnLoadingStatusChanged;
            }

            _currentFolderPath = folderPath;

            // 更新 UI
            StatusTextBlock.Text = "正在掃描檔案夾...";
            OpenFolderButton.IsEnabled = false;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            // 清空列表
            ImageListBox.ItemsSource = null;

            // 開始監控檔案夾
            try
            {
                _fileWatcher.WatchFolder(folderPath, includeSubdirectories: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to watch folder {folderPath}: {ex.Message}");
            }

            // 在背景執行緒掃描圖片
            var imageFiles = await Task.Run(() => ScanImageFiles(folderPath));

            var items = new List<ImageItem>();
            foreach (var path in imageFiles)
            {
                try
                {
                    items.Add(new ImageItem
                    {
                        FilePath = path,
                        FileName = Path.GetFileName(path),
                        Modified = File.GetLastWriteTime(path),
                        FileSize = new FileInfo(path).Length,
                        Format = ImageUtils.GetImageFormat(path),
                        Created = File.GetCreationTime(path)
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {path}: {ex.Message}");
                }
            }
            _currentImages = items;

            // 更新 UI
            FolderPathTextBlock.Text = folderPath;
            ImageCountTextBlock.Text = $"共 {_currentImages.Count} 張圖片";
            StatusTextBlock.Text = $"已載入 {_currentImages.Count} 張圖片";

            // 顯示縮圖
            ImageListBox.ItemsSource = _currentImages;

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
            foreach (var file in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (ImageUtils.IsSupportedImage(file))
                {
                    imageFiles.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning folder {folderPath}: {ex.Message}");
            // 忽略無法存取的檔案夾
        }

        return imageFiles;
    }

    /// <summary>
    /// 縮圖點擊事件
    /// </summary>
    private void Thumbnail_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ImageItem clickedImage)
        {
            var index = _currentImages.IndexOf(clickedImage);
            if (index >= 0)
            {
                var viewerWindow = new ViewerView(_currentImages, index);
                viewerWindow.Show();
            }
        }
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

    private void OnLoadingStatusChanged(object? sender, int activeCount)
    {
        Dispatcher.Invoke(() =>
        {
            if (activeCount > 0)
            {
                LoadingProgressBar.Visibility = Visibility.Visible;
                LoadingProgressBar.IsIndeterminate = true;
                StatusTextBlock.Text = $"正在載入... ({activeCount})";
            }
            else
            {
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                LoadingProgressBar.IsIndeterminate = false;
                StatusTextBlock.Text = $"就緒";
            }
        });
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _memoryUpdateTimer?.Stop();
        _fileWatcher.Dispose();
        if (_imageLoader != null)
        {
            _imageLoader.LoadingStatusChanged -= OnLoadingStatusChanged;
            _imageLoader.Dispose();
        }
    }
}
