using System.IO;
using System.Windows;
using System.Windows.Input;
using PhotoViewer.Core.Models;
using PhotoViewer.Core.Services;
using PhotoViewer.Core.Utilities;

namespace PhotoViewer.App.Views;

/// <summary>
/// 單張圖片檢視視窗
/// </summary>
public partial class ViewerView : Window
{
    private readonly ImageLoaderService _imageLoader;
    private List<ImageItem> _images = new();
    private int _currentIndex;
    private bool _isLoading = false;

    public ViewerView(List<ImageItem> images, int startIndex = 0)
    {
        InitializeComponent();

        _imageLoader = new ImageLoaderService();
        _images = images;
        _currentIndex = startIndex;

        // 視窗載入完成後再載入圖片
        Loaded += (s, e) =>
        {
            // 短暫延遲確保視窗完全顯示
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadCurrentImage();
                // 載入後取消 Topmost，避免一直在最上層
                Topmost = false;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        };
    }

    /// <summary>
    /// 載入目前圖片
    /// </summary>
    private async void LoadCurrentImage()
    {
        if (_isLoading)
            return;

        if (_currentIndex < 0 || _currentIndex >= _images.Count)
            return;

        _isLoading = true;
        var currentImage = _images[_currentIndex];

        try
        {
            // 顯示載入指示器
            LoadingPanel.Visibility = Visibility.Visible;
            UpdateUI(currentImage);

            // 嘗試載入動畫 (GIF)
            var animatedImage = await _imageLoader.LoadAnimatedImageAsync(currentImage.FilePath);
            
            if (animatedImage != null)
            {
                // 是動畫圖片
                ImageCanvas.SetAnimatedImage(animatedImage);
                
                // 更新圖片尺寸資訊
                if (currentImage.Dimensions.Width == 0)
                {
                    currentImage.Dimensions = (animatedImage.Width, animatedImage.Height);
                    UpdateUI(currentImage);
                }
            }
            else
            {
                // 不是動畫或載入失敗，嘗試載入一般靜態圖片
                var bitmap = await _imageLoader.LoadFullImageAsync(currentImage.FilePath);

                if (bitmap != null)
                {
                    // 更新圖片尺寸資訊
                    if (currentImage.Dimensions.Width == 0)
                    {
                        currentImage.Dimensions = (bitmap.Width, bitmap.Height);
                        UpdateUI(currentImage);
                    }

                    ImageCanvas.CurrentBitmap = bitmap;
                }
                else
                {
                    MessageBox.Show($"無法載入圖片: {currentImage.FileName}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // 載入 EXIF 資訊
            if (currentImage.ExifData.Count == 0 || currentImage.Exif == null)
            {
                // 並行載入原始 EXIF 和結構化 EXIF
                var exifDataTask = _imageLoader.GetExifDataAsync(currentImage.FilePath);
                var exifInfoTask = _imageLoader.GetExifInfoAsync(currentImage.FilePath);

                await Task.WhenAll(exifDataTask, exifInfoTask);

                currentImage.ExifData = exifDataTask.Result;
                currentImage.Exif = exifInfoTask.Result;
            }
            
            ExifListView.ItemsSource = currentImage.ExifData;
            UpdateExifUI(currentImage.Exif);

            // 短暫延遲確保 bitmap 已設定到視覺樹
            await Dispatcher.BeginInvoke(new Action(() =>
            {
                // 確保圖片置中並適應視窗
                ImageCanvas.FitToWindow();
                UpdateZoomDisplay();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"載入圖片時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            _isLoading = false;
        }
    }

    /// <summary>
    /// 更新 UI 資訊
    /// </summary>
    private void UpdateUI(ImageItem image)
    {
        FileNameTextBlock.Text = image.FileName;
        PositionTextBlock.Text = $"{_currentIndex + 1} / {_images.Count}";

        var sizeInfo = image.Dimensions.Width > 0
            ? $"{image.Dimensions.Width} × {image.Dimensions.Height}"
            : "";

        ImageInfoTextBlock.Text = $"{image.Format} | {ImageUtils.FormatFileSize(image.FileSize)} | {sizeInfo}";

        // 更新按鈕狀態
        PreviousButton.IsEnabled = _currentIndex > 0;
        NextButton.IsEnabled = _currentIndex < _images.Count - 1;
    }

    /// <summary>
    /// 更新 EXIF UI
    /// </summary>
    private void UpdateExifUI(PhotoViewer.Core.Models.ExifInfo? exif)
    {
        if (exif == null)
        {
            ExifCameraText.Text = "無相機資訊";
            ExifLensText.Text = "";
            ExifIsoText.Text = "-";
            ExifApertureText.Text = "-";
            ExifShutterText.Text = "-";
            ExifFocalLengthText.Text = "-";
            ExifDateText.Text = "-";
            return;
        }

        // 相機型號
        string camera = $"{exif.Make} {exif.Model}".Trim();
        ExifCameraText.Text = string.IsNullOrEmpty(camera) ? "未知相機" : camera;

        // 鏡頭
        ExifLensText.Text = exif.Lens;

        // 參數
        ExifIsoText.Text = string.IsNullOrEmpty(exif.Iso) ? "-" : exif.Iso;
        ExifApertureText.Text = string.IsNullOrEmpty(exif.FNumber) ? "-" : exif.FNumber;
        ExifShutterText.Text = string.IsNullOrEmpty(exif.ExposureTime) ? "-" : exif.ExposureTime;
        ExifFocalLengthText.Text = string.IsNullOrEmpty(exif.FocalLength) ? "-" : exif.FocalLength;
        
        // 時間
        ExifDateText.Text = exif.DateTaken.HasValue 
            ? exif.DateTaken.Value.ToString("yyyy/MM/dd HH:mm") 
            : "-";
    }

    /// <summary>
    /// 更新縮放顯示
    /// </summary>
    private void UpdateZoomDisplay()
    {
        var zoomPercent = (int)(ImageCanvas.Scale * 100);
        ZoomTextBlock.Text = $"{zoomPercent}%";
    }

    /// <summary>
    /// 上一張
    /// </summary>
    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            LoadCurrentImage();
        }
    }

    /// <summary>
    /// 下一張
    /// </summary>
    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex < _images.Count - 1)
        {
            _currentIndex++;
            LoadCurrentImage();
        }
    }

    /// <summary>
    /// 適應視窗
    /// </summary>
    private void FitButton_Click(object sender, RoutedEventArgs e)
    {
        ImageCanvas.FitToWindow();
        UpdateZoomDisplay();
    }

    /// <summary>
    /// 實際大小
    /// </summary>
    private void ActualSizeButton_Click(object sender, RoutedEventArgs e)
    {
        ImageCanvas.ActualSize();
        UpdateZoomDisplay();
    }

    /// <summary>
    /// 左轉 90 度
    /// </summary>
    private void RotateLeftButton_Click(object sender, RoutedEventArgs e)
    {
        ImageCanvas.Rotate(-90);
    }

    /// <summary>
    /// 右轉 90 度
    /// </summary>
    private void RotateRightButton_Click(object sender, RoutedEventArgs e)
    {
        ImageCanvas.Rotate(90);
    }

    /// <summary>
    /// 顯示/隱藏資訊面板
    /// </summary>
    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleInfoPanel();
    }

    private void ToggleInfoPanel()
    {
        if (InfoPanel.Visibility == Visibility.Visible)
        {
            InfoPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            InfoPanel.Visibility = Visibility.Visible;
        }

        // 延遲執行，等待佈局更新後再自適應視窗
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ImageCanvas.FitToWindow();
            UpdateZoomDisplay();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // 幻燈片播放相關
    private System.Windows.Threading.DispatcherTimer? _slideshowTimer;
    private bool _isSlideshowPlaying = false;

    private void SlideshowButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSlideshow();
    }

    private void ToggleSlideshow()
    {
        if (_isSlideshowPlaying)
        {
            StopSlideshow();
        }
        else
        {
            StartSlideshow();
        }
    }

    private void StartSlideshow()
    {
        if (_isSlideshowPlaying) return;

        _isSlideshowPlaying = true;
        SlideshowButton.Content = "⏹ 停止";
        
        _slideshowTimer = new System.Windows.Threading.DispatcherTimer();
        _slideshowTimer.Interval = TimeSpan.FromSeconds(3); // 預設 3 秒
        _slideshowTimer.Tick += OnSlideshowTick;
        _slideshowTimer.Start();
    }

    private void StopSlideshow()
    {
        if (!_isSlideshowPlaying) return;

        _isSlideshowPlaying = false;
        SlideshowButton.Content = "▶ 播放";
        
        if (_slideshowTimer != null)
        {
            _slideshowTimer.Stop();
            _slideshowTimer.Tick -= OnSlideshowTick;
            _slideshowTimer = null;
        }
    }

    private void OnSlideshowTick(object? sender, EventArgs e)
    {
        if (_currentIndex < _images.Count - 1)
        {
            _currentIndex++;
            LoadCurrentImage();
        }
        else
        {
            // 播放到底後循環回到第一張
            _currentIndex = 0;
            LoadCurrentImage();
        }
    }

    /// <summary>
    /// 鍵盤快捷鍵
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
            case Key.PageUp:
                if (_currentIndex > 0)
                {
                    _currentIndex--;
                    LoadCurrentImage();
                }
                e.Handled = true;
                break;

            case Key.Right:
            case Key.PageDown:
                if (_currentIndex < _images.Count - 1)
                {
                    _currentIndex++;
                    LoadCurrentImage();
                }
                e.Handled = true;
                break;

            case Key.Home:
                _currentIndex = 0;
                LoadCurrentImage();
                e.Handled = true;
                break;

            case Key.End:
                _currentIndex = _images.Count - 1;
                LoadCurrentImage();
                e.Handled = true;
                break;

            case Key.F:
                ImageCanvas.FitToWindow();
                UpdateZoomDisplay();
                e.Handled = true;
                break;

            case Key.D1 when Keyboard.Modifiers == ModifierKeys.Control:
                ImageCanvas.ActualSize();
                UpdateZoomDisplay();
                e.Handled = true;
                break;

            case Key.R when Keyboard.Modifiers == ModifierKeys.Shift:
                ImageCanvas.Rotate(-90);
                e.Handled = true;
                break;

            case Key.R:
                ImageCanvas.Rotate(90);
                e.Handled = true;
                break;

            case Key.I:
                ToggleInfoPanel();
                e.Handled = true;
                break;

            case Key.F5:
                ToggleSlideshow();
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isSlideshowPlaying)
                {
                    StopSlideshow();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopSlideshow();
        base.OnClosed(e);
        ImageCanvas.CurrentBitmap = null;
        _imageLoader?.Dispose();
    }
}
