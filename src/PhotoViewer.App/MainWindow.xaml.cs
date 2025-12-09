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
    private readonly Services.ThemeService _themeService;
    private readonly RatingService _ratingService;
    private List<ImageItem> _allImages = new(); // 所有圖片
    public ObservableCollection<ImageItem> FilteredImages { get; private set; } = new(); // 過濾後的圖片
    private string _currentFolderPath = string.Empty;

    // 篩選條件
    private string _selectedFormat = "All";
    private int _selectedSizeIndex = 0; // 0=All, 1=<1MB, 2=1-10MB, 3=>10MB
    private int _selectedDateIndex = 0; // 0=All, 1=Today, 2=This Week, 3=This Month
    private int _selectedRatingIndex = 0; // 0=All, 1=5星, 2=4+星, 3=3+星, 4=已評分, 5=未評分

    public string SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (_selectedFormat != value)
            {
                _selectedFormat = value;
                ApplyFilters();
            }
        }
    }

    public int SelectedSizeIndex
    {
        get => _selectedSizeIndex;
        set
        {
            if (_selectedSizeIndex != value)
            {
                _selectedSizeIndex = value;
                ApplyFilters();
            }
        }
    }

    public int SelectedDateIndex
    {
        get => _selectedDateIndex;
        set
        {
            if (_selectedDateIndex != value)
            {
                _selectedDateIndex = value;
                ApplyFilters();
            }
        }
    }

    public int SelectedRatingIndex
    {
        get => _selectedRatingIndex;
        set
        {
            if (_selectedRatingIndex != value)
            {
                _selectedRatingIndex = value;
                ApplyFilters();
            }
        }
    }

    private System.Windows.Threading.DispatcherTimer? _memoryUpdateTimer;
    private PhotoViewer.App.Controls.VirtualizingWrapPanel? _virtualizingPanel;

    public ObservableCollection<FolderNode> Folders { get; set; } = new();

    public MainWindow()
    {
        try
        {
            InitializeComponent();

            // 延遲初始化 ImageLoader，避免在啟動時就創建資料庫
            // _imageLoader = new ImageLoaderService();
            _fileWatcher = new FileWatcherService();
            _themeService = new Services.ThemeService();
            _ratingService = new RatingService();

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

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        _themeService.ToggleTheme();
    }

    private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (SidebarToggleButton.IsChecked == true)
        {
            // Expand
            var sb = (System.Windows.Media.Animation.Storyboard)FindResource("SidebarExpandStoryboard");
            sb.Begin();
        }
        else
        {
            // Collapse
            var sb = (System.Windows.Media.Animation.Storyboard)FindResource("SidebarCollapseStoryboard");
            sb.Begin();
        }
    }

    /// <summary>
    /// 載入指示器顯示開關
    /// </summary>
    public static bool IsLoadingIndicatorEnabled { get; set; } = true;

    private void AnimationToggleButton_Click(object sender, RoutedEventArgs e)
    {
        IsLoadingIndicatorEnabled = AnimationToggleButton.IsChecked == true;
        StatusTextBlock.Text = IsLoadingIndicatorEnabled ? "載入指示器已啟用" : "載入指示器已停用";
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
                _imageLoader!.LoadingStatusChanged += OnLoadingStatusChanged;
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
            _allImages = items;

            // 載入已儲存的評分
            var savedRatings = _ratingService.GetRatings(items.Select(i => i.FilePath));
            foreach (var item in _allImages)
            {
                if (savedRatings.TryGetValue(item.FilePath, out var rating))
                {
                    item.Rating = rating;
                }
            }

            // 更新 UI
            FolderPathTextBlock.Text = folderPath;
            
            // 應用篩選
            ApplyFilters();

            // 顯示縮圖
            ImageListBox.ItemsSource = FilteredImages;

            // 訂閱預載入事件（僅執行一次）
            if (_virtualizingPanel == null)
            {
                SubscribeToPreloadEvents();
            }

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

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var path = files[0];
                if (Directory.Exists(path))
                {
                    _ = LoadFolderAsync(path);
                }
                else if (File.Exists(path))
                {
                    // If it's a file, load parent folder and scroll to it (TODO)
                    var folder = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder))
                    {
                        _ = LoadFolderAsync(folder);
                    }
                }
            }
        }
    }

    private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ImageItem item)
        {
             try
             {
                 System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
             }
             catch (Exception ex)
             {
                 MessageBox.Show($"無法開啟檔案總管: {ex.Message}");
             }
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ImageItem item)
        {
            try
            {
                Clipboard.SetText(item.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法複製路徑: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 縮圖點擊事件
    /// </summary>
    /// <summary>
    /// 縮圖點擊事件
    /// </summary>
    private void Thumbnail_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem clickedImage)
            {
                var index = FilteredImages.IndexOf(clickedImage);
                if (index >= 0)
                {
                    var viewerWindow = new ViewerView(FilteredImages.ToList(), index);
                    viewerWindow.Show();
                }
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

    /// <summary>
    /// 訂閱 VirtualizingWrapPanel 的預載入事件
    /// </summary>
    private void SubscribeToPreloadEvents()
    {
        // 在視覺樹中尋找 VirtualizingWrapPanel
        var listBox = ImageListBox;
        _virtualizingPanel = FindVisualChild<PhotoViewer.App.Controls.VirtualizingWrapPanel>(listBox);

        if (_virtualizingPanel != null)
        {
            _virtualizingPanel.PreloadRequested += OnPreloadRequested;
        }
    }

    /// <summary>
    /// 處理預載入請求事件
    /// </summary>
    private void OnPreloadRequested(object? sender, PhotoViewer.App.Controls.PreloadRequestEventArgs e)
    {
        if (_imageLoader == null || FilteredImages.Count == 0) return;

        var filePaths = FilteredImages
            .Skip(e.StartIndex)
            .Take(e.Count)
            .Select(img => img.FilePath)
            .ToList();

        // 後台預載入（fire-and-forget）
        _ = Task.Run(async () =>
        {
            try
            {
                await _imageLoader.PreloadThumbnailsIntelligentAsync(filePaths, e.Count);
            }
            catch { /* 忽略後台錯誤 */ }
        });
    }

    /// <summary>
    /// 在視覺樹中尋找指定類型的子元素
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;

            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _memoryUpdateTimer?.Stop();
        _fileWatcher.Dispose();
        _ratingService.Dispose();
        if (_imageLoader != null)
        {
            _imageLoader.LoadingStatusChanged -= OnLoadingStatusChanged;
            _imageLoader.Dispose();
        }
    }

    /// <summary>
    /// 設定圖片評分
    /// </summary>
    private void SetRating_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string tagStr && int.TryParse(tagStr, out int rating))
        {
            // 從 ContextMenu 向上找到 ImageItem
            var contextMenu = menuItem.Parent as ItemsControl;
            while (contextMenu != null && !(contextMenu is ContextMenu))
            {
                contextMenu = contextMenu.Parent as ItemsControl;
            }

            if (contextMenu is ContextMenu cm && cm.PlacementTarget is FrameworkElement element)
            {
                if (element.DataContext is ImageItem item)
                {
                    // 更新評分 (ImageItem 現在實作 INotifyPropertyChanged，UI 會自動更新)
                    item.Rating = rating;
                    _ratingService.SetRating(item.FilePath, rating);

                    StatusTextBlock.Text = rating > 0 
                        ? $"已設定 {item.FileName} 為 {rating} 星評分" 
                        : $"已清除 {item.FileName} 的評分";
                }
            }
        }
    }

    /// <summary>
    /// 評分篩選變更
    /// </summary>
    private void RatingFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }


    /// <summary>
    /// 應用篩選條件
    /// </summary>
    private void ApplyFilters()
    {
        if (_allImages == null) return;

        var query = _allImages.AsEnumerable();

        // 1. 格式篩選
        if (!string.IsNullOrEmpty(SelectedFormat) && SelectedFormat != "All")
        {
            query = query.Where(img => img.Format.Equals(SelectedFormat, StringComparison.OrdinalIgnoreCase));
        }

        // 2. 大小篩選
        switch (SelectedSizeIndex)
        {
            case 1: // < 1MB
                query = query.Where(img => img.FileSize < 1024 * 1024);
                break;
            case 2: // 1 - 10MB
                query = query.Where(img => img.FileSize >= 1024 * 1024 && img.FileSize <= 10 * 1024 * 1024);
                break;
            case 3: // > 10MB
                query = query.Where(img => img.FileSize > 10 * 1024 * 1024);
                break;
        }

        // 3. 日期篩選
        var now = DateTime.Now;
        switch (SelectedDateIndex)
        {
            case 1: // Today
                query = query.Where(img => img.Modified.Date == now.Date);
                break;
            case 2: // This Week
                var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
                query = query.Where(img => img.Modified >= startOfWeek);
                break;
            case 3: // This Month
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                query = query.Where(img => img.Modified >= startOfMonth);
                break;
        }

        // 4. 評分篩選
        switch (SelectedRatingIndex)
        {
            case 1: // 5 星
                query = query.Where(img => img.Rating == 5);
                break;
            case 2: // 4+ 星
                query = query.Where(img => img.Rating >= 4);
                break;
            case 3: // 3+ 星
                query = query.Where(img => img.Rating >= 3);
                break;
            case 4: // 已評分
                query = query.Where(img => img.Rating > 0);
                break;
            case 5: // 未評分
                query = query.Where(img => img.Rating == 0);
                break;
        }

        // 更新 FilteredImages
        FilteredImages.Clear();
        foreach (var item in query)
        {
            FilteredImages.Add(item);
        }

        // 更新狀態列
        ImageCountTextBlock.Text = $"共 {FilteredImages.Count} 張圖片 (總計 {_allImages.Count})";
        StatusTextBlock.Text = $"已篩選 {FilteredImages.Count} 張圖片";
    }
}
