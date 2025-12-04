using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace PhotoViewer.App.Controls;

/// <summary>
/// SkiaSharp 畫布控制項
/// 用於高效能圖片渲染和編輯
/// </summary>
public class SkiaCanvasControl : SKElement
{
    private SKBitmap? _currentBitmap;
    private SKMatrix _matrix = SKMatrix.Identity;
    private float _scale = 1.0f;
    private SKPoint _translate = SKPoint.Empty;
    private float _rotation = 0f;

    // 拖拽狀態
    private bool _isDragging;
    private Point _lastMousePosition;

    public SkiaCanvasControl()
    {
        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// 目前顯示的圖片
    /// </summary>
    /// <summary>
    /// 目前顯示的圖片
    /// </summary>
    public SKBitmap? CurrentBitmap
    {
        get => _currentBitmap;
        set
        {
            StopAnimation();
            _currentBitmap = value;
            if (value != null)
            {
                ResetTransform();
            }
            InvalidateVisual();
        }
    }

    private PhotoViewer.Core.Models.AnimatedImage? _animatedImage;
    private System.Windows.Threading.DispatcherTimer? _animationTimer;
    private int _currentFrameIndex;

    /// <summary>
    /// 設定動畫圖片
    /// </summary>
    public void SetAnimatedImage(PhotoViewer.Core.Models.AnimatedImage? animatedImage)
    {
        StopAnimation();
        _animatedImage = animatedImage;

        if (_animatedImage != null && _animatedImage.FrameCount > 0)
        {
            _currentBitmap = _animatedImage.Frames[0];
            ResetTransform();
            StartAnimation();
        }
        else
        {
            _currentBitmap = null;
            InvalidateVisual();
        }
    }

    private void StartAnimation()
    {
        if (_animatedImage == null || _animatedImage.FrameCount <= 1)
            return;

        _currentFrameIndex = 0;
        _animationTimer = new System.Windows.Threading.DispatcherTimer();
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Interval = TimeSpan.FromMilliseconds(_animatedImage.Durations[0]);
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer != null)
        {
            _animationTimer.Stop();
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer = null;
        }
        _animatedImage = null;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_animatedImage == null)
        {
            StopAnimation();
            return;
        }

        _currentFrameIndex = (_currentFrameIndex + 1) % _animatedImage.FrameCount;
        _currentBitmap = _animatedImage.Frames[_currentFrameIndex];
        
        // 更新下一幀的間隔
        if (_animationTimer != null)
        {
            _animationTimer.Interval = TimeSpan.FromMilliseconds(_animatedImage.Durations[_currentFrameIndex]);
        }

        InvalidateVisual();
    }

    /// <summary>
    /// 縮放比例
    /// </summary>
    public float Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Clamp(value, 0.1f, 10f);
            UpdateMatrix();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// 旋轉角度（度數）
    /// </summary>
    public float Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value % 360;
            UpdateMatrix();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// 繪製圖片
    /// </summary>
    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);

        if (_currentBitmap == null)
        {
            DrawPlaceholder(canvas, e.Info.Width, e.Info.Height);
            return;
        }

        // 套用變換矩陣
        canvas.Save();
        canvas.SetMatrix(_matrix);

        // 繪製圖片
        var paint = new SKPaint
        {
            FilterQuality = SKFilterQuality.High,
            IsAntialias = true
        };

        canvas.DrawBitmap(_currentBitmap, 0, 0, paint);
        canvas.Restore();
    }

    /// <summary>
    /// 繪製佔位符
    /// </summary>
    private void DrawPlaceholder(SKCanvas canvas, int width, int height)
    {
        var paint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 24,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        var text = "No Image";
        canvas.DrawText(text, width / 2, height / 2, paint);
    }

    /// <summary>
    /// 重設變換
    /// </summary>
    public void ResetTransform()
    {
        if (_currentBitmap == null)
            return;

        _scale = CalculateFitScale();
        _rotation = 0f;
        _translate = CalculateCenterPosition();
        UpdateMatrix();
        InvalidateVisual();
    }

    /// <summary>
    /// 計算適應視窗的縮放比例
    /// </summary>
    private float CalculateFitScale()
    {
        if (_currentBitmap == null || ActualWidth == 0 || ActualHeight == 0)
            return 1.0f;

        var scaleX = (float)ActualWidth / _currentBitmap.Width;
        var scaleY = (float)ActualHeight / _currentBitmap.Height;

        return Math.Min(scaleX, scaleY) * 0.95f; // 留 5% 邊距
    }

    /// <summary>
    /// 計算置中位置
    /// </summary>
    /// <summary>
    /// 計算置中位置 (返回 Canvas 中心點)
    /// </summary>
    private SKPoint CalculateCenterPosition()
    {
        if (ActualWidth == 0 || ActualHeight == 0)
            return SKPoint.Empty;

        return new SKPoint((float)ActualWidth / 2f, (float)ActualHeight / 2f);
    }

    /// <summary>
    /// 更新變換矩陣
    /// </summary>
    private void UpdateMatrix()
    {
        if (_currentBitmap == null)
            return;

        var imageCenterX = _currentBitmap.Width / 2f;
        var imageCenterY = _currentBitmap.Height / 2f;

        _matrix = SKMatrix.Identity;

        // 1. 移到圖片中心 (原點)
        _matrix = _matrix.PostConcat(SKMatrix.CreateTranslation(-imageCenterX, -imageCenterY));

        // 2. 縮放
        _matrix = _matrix.PostConcat(SKMatrix.CreateScale(_scale, _scale));

        // 3. 旋轉
        if (_rotation != 0)
        {
            _matrix = _matrix.PostConcat(SKMatrix.CreateRotationDegrees(_rotation));
        }

        // 4. 移到目標位置 (_translate 現在是 Canvas 上的中心點)
        _matrix = _matrix.PostConcat(SKMatrix.CreateTranslation(_translate.X, _translate.Y));
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_currentBitmap == null)
            return;

        var zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
        var newScale = _scale * zoomFactor;

        var mousePos = e.GetPosition(this);
        ZoomAtPoint(newScale, (float)mousePos.X, (float)mousePos.Y);

        e.Handled = true;
    }

    private void ZoomAtPoint(float newScale, float x, float y)
    {
        newScale = Math.Clamp(newScale, 0.1f, 10f);

        if (_currentBitmap == null)
            return;

        // 計算縮放前後的差異，調整中心點位置以保持滑鼠下的點不動
        // 原理：(Mouse - Translate) / OldScale = (Mouse - NewTranslate) / NewScale
        // NewTranslate = Mouse - (Mouse - OldTranslate) * (NewScale / OldScale)
        
        var scaleRatio = newScale / _scale;
        
        _translate.X = x - (x - _translate.X) * scaleRatio;
        _translate.Y = y - (y - _translate.Y) * scaleRatio;

        _scale = newScale;
        UpdateMatrix();
        InvalidateVisual();
    }

    /// <summary>
    /// 滑鼠滾輪縮放
    /// </summary>


    /// <summary>
    /// 滑鼠左鍵按下（開始拖拽）
    /// </summary>
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_currentBitmap == null)
            return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 滑鼠左鍵放開（結束拖拽）
    /// </summary>
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    /// <summary>
    /// 滑鼠移動（拖拽平移）
    /// </summary>
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _currentBitmap == null)
            return;

        var currentPosition = e.GetPosition(this);
        var delta = currentPosition - _lastMousePosition;

        _translate.X += (float)delta.X;
        _translate.Y += (float)delta.Y;

        _lastMousePosition = currentPosition;
        UpdateMatrix();
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>
    /// 視窗大小改變
    /// </summary>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_currentBitmap != null)
        {
            ResetTransform();
        }
    }

    /// <summary>
    /// 旋轉圖片
    /// </summary>
    public void Rotate(float degrees)
    {
        Rotation += degrees;
    }

    /// <summary>
    /// 適應視窗
    /// </summary>
    public void FitToWindow()
    {
        if (_currentBitmap == null)
            return;

        _scale = CalculateFitScale();
        _rotation = 0f;
        _translate = CalculateCenterPosition();
        UpdateMatrix();
        InvalidateVisual();
    }

    /// <summary>
    /// 實際大小（100%）
    /// </summary>
    public void ActualSize()
    {
        if (_currentBitmap == null)
            return;

        _scale = 1.0f;
        _rotation = 0f;
        _translate = CalculateCenterPosition();
        UpdateMatrix();
        InvalidateVisual();
    }
}
