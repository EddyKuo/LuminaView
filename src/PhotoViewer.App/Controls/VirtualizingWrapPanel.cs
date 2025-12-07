using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PhotoViewer.App.Controls;

/// <summary>
/// 虛擬化 WrapPanel
/// 僅渲染可見區域的項目，支持大量圖片流暢滾動
/// </summary>
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    // 預設項目大小
    public double ItemWidth { get; set; } = 160;
    public double ItemHeight { get; set; } = 160;
    
    // 緩衝區大小（螢幕外的額外行數）
    private const int BufferRows = 8;

    // 滾動預測相關欄位
    private readonly PhotoViewer.Core.Services.ScrollPredictionService _scrollPredictor = new();
    private double _lastScrollOffset = 0;

    // 預載入請求事件
    public event EventHandler<PreloadRequestEventArgs>? PreloadRequested;

    // 轉換像素到邏輯單元
    private TranslateTransform _trans = new TranslateTransform();
    private Size _extent = new Size(0, 0);
    private Size _viewport = new Size(0, 0);
    private Point _offset = new Point(0, 0);

    public VirtualizingWrapPanel()
    {
        // 啟用虛擬化
        IsItemsHost = true;
    }

    // IScrollInfo 實作
    public bool CanVerticallyScroll { get; set; } = true;
    public bool CanHorizontallyScroll { get; set; } = false;
    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;
    public ScrollViewer? ScrollOwner { get; set; }

    /// <summary>
    /// 測量邏輯
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateScrollInfo(availableSize);

        // 計算每行可容納的項目數
        int itemsPerRow = CalculateItemsPerRow(availableSize.Width);
        if (itemsPerRow <= 0) itemsPerRow = 1;

        // 取得 ItemsControl
        var itemsControl = ItemsControl.GetItemsOwner(this);
        int itemCount = itemsControl?.Items.Count ?? 0;

        // 計算可見範圍
        int firstVisibleIndex, lastVisibleIndex;
        GetVisibleRange(itemsPerRow, itemCount, out firstVisibleIndex, out lastVisibleIndex);

        // 獲取生成器
        IItemContainerGenerator generator = ItemContainerGenerator;
        if (generator == null) return new Size(0, 0);
        
        // 準備生成項目
        var startPos = generator.GeneratorPositionFromIndex(firstVisibleIndex);
        int childIndex = (startPos.Offset == 0) ? startPos.Index : startPos.Index + 1;

        using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
        {
            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                bool isNewlyRealized;
                var container = generator.GenerateNext(out isNewlyRealized) as UIElement;
                
                if (container != null)
                {
                    if (isNewlyRealized)
                    {
                        if (childIndex >= InternalChildren.Count)
                        {
                            AddInternalChild(container);
                        }
                        else
                        {
                            InsertInternalChild(childIndex, container);
                        }
                        generator.PrepareItemContainer(container);
                    }
                    
                    // 測量容器
                    container.Measure(new Size(ItemWidth, ItemHeight));
                    childIndex++;
                }
            }
        }

        // 清理不可見的項目
        CleanUpItems(firstVisibleIndex, lastVisibleIndex);

        return new Size(availableSize.Width, _extent.Height > availableSize.Height ? availableSize.Height : _extent.Height);
    }

    /// <summary>
    /// 排列邏輯
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        int itemCount = itemsControl?.Items.Count ?? 0;
        int itemsPerRow = CalculateItemsPerRow(finalSize.Width);
        if (itemsPerRow <= 0) itemsPerRow = 1;

        UpdateScrollInfo(finalSize);

        for (int i = 0; i < InternalChildren.Count; i++)
        {
            var container = InternalChildren[i];
            
            // 找出該容器對應的索引
            int index = ((ItemContainerGenerator)ItemContainerGenerator).IndexFromContainer(container);
            
            if (index >= 0)
            {
                // 計算位置
                int row = index / itemsPerRow;
                int col = index % itemsPerRow;

                double x = col * ItemWidth;
                double y = row * ItemHeight;

                // 考慮滾動偏移
                y -= _offset.Y;

                container.Arrange(new Rect(x, y, ItemWidth, ItemHeight));
            }
        }

        return finalSize;
    }

    /// <summary>
    /// 計算每行項目數
    /// </summary>
    private int CalculateItemsPerRow(double availableWidth)
    {
        if (double.IsInfinity(availableWidth)) return 100; // 避免無限寬度
        return (int)(availableWidth / ItemWidth);
    }

    /// <summary>
    /// 更新滾動資訊
    /// </summary>
    private void UpdateScrollInfo(Size availableSize)
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        int itemCount = itemsControl?.Items.Count ?? 0;
        int itemsPerRow = CalculateItemsPerRow(availableSize.Width);
        if (itemsPerRow <= 0) itemsPerRow = 1;

        int rowCount = (int)Math.Ceiling((double)itemCount / itemsPerRow);
        double totalHeight = rowCount * ItemHeight;

        bool extentChanged = _extent.Height != totalHeight || _extent.Width != availableSize.Width;
        bool viewportChanged = _viewport != availableSize;

        _extent = new Size(availableSize.Width, totalHeight);
        _viewport = availableSize;

        // 確保偏移量在有效範圍內
        _offset.Y = Math.Max(0, Math.Min(_offset.Y, _extent.Height - _viewport.Height));

        if (extentChanged || viewportChanged)
        {
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    /// <summary>
    /// 計算可見範圍的索引
    /// </summary>
    private void GetVisibleRange(int itemsPerRow, int itemCount, out int firstIndex, out int lastIndex)
    {
        int firstRow = (int)Math.Floor(_offset.Y / ItemHeight);
        int lastRow = (int)Math.Ceiling((_offset.Y + _viewport.Height) / ItemHeight);

        // 增加緩衝區
        firstRow = Math.Max(0, firstRow - BufferRows);
        lastRow = Math.Min((int)Math.Ceiling((double)itemCount / itemsPerRow) - 1, lastRow + BufferRows);

        firstIndex = firstRow * itemsPerRow;
        lastIndex = Math.Min(itemCount - 1, (lastRow + 1) * itemsPerRow - 1);
    }

    /// <summary>
    /// 清理不可見的項目
    /// </summary>
    private void CleanUpItems(int minIndex, int maxIndex)
    {
        var generator = ItemContainerGenerator;

        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var container = InternalChildren[i];
            var itemIndex = ((ItemContainerGenerator)generator).IndexFromContainer(container);

            if (itemIndex < minIndex || itemIndex > maxIndex)
            {
                RemoveInternalChildRange(i, 1);
                generator.Remove(new GeneratorPosition(i, 0), 1);
            }
        }
    }

    // 滾動方法實作
    public void LineUp() => SetVerticalOffset(_offset.Y - 20);
    public void LineDown() => SetVerticalOffset(_offset.Y + 20);
    public void LineLeft() => SetHorizontalOffset(_offset.X - 20);
    public void LineRight() => SetHorizontalOffset(_offset.X + 20);
    public void PageUp() => SetVerticalOffset(_offset.Y - _viewport.Height);
    public void PageDown() => SetVerticalOffset(_offset.Y + _viewport.Height);
    public void PageLeft() => SetHorizontalOffset(_offset.X - _viewport.Width);
    public void PageRight() => SetHorizontalOffset(_offset.X + _viewport.Width);
    public void MouseWheelUp() => SetVerticalOffset(_offset.Y - 40);
    public void MouseWheelDown() => SetVerticalOffset(_offset.Y + 40);
    public void MouseWheelLeft() => SetHorizontalOffset(_offset.X - 20);
    public void MouseWheelRight() => SetHorizontalOffset(_offset.X + 20);

    public void SetHorizontalOffset(double offset)
    {
        // 不支持水平滾動
    }

    public void SetVerticalOffset(double offset)
    {
        if (offset < 0 || _viewport.Height >= _extent.Height)
        {
            offset = 0;
        }
        else
        {
            if (offset + _viewport.Height >= _extent.Height)
            {
                offset = _extent.Height - _viewport.Height;
            }
        }

        // 新增：追蹤滾動並觸發預載入
        if (Math.Abs(offset - _lastScrollOffset) > 10) // 防抖：忽略微滾動
        {
            _scrollPredictor.UpdateScrollPosition(offset);
            TriggerPreload();
            _lastScrollOffset = offset;
        }

        _offset.Y = offset;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure(); // 觸發重新佈局
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        return Rect.Empty;
    }

    /// <summary>
    /// 觸發預載入事件（基於滾動方向和速度）
    /// </summary>
    private void TriggerPreload()
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        int itemCount = itemsControl?.Items.Count ?? 0;
        if (itemCount == 0) return;

        int itemsPerRow = CalculateItemsPerRow(_viewport.Width);
        if (itemsPerRow <= 0) itemsPerRow = 1;

        var direction = _scrollPredictor.GetDirection();
        var preloadCount = _scrollPredictor.GetPreloadCount();

        // 計算當前可見範圍
        int currentRow = (int)Math.Floor(_offset.Y / ItemHeight);
        int currentIndex = currentRow * itemsPerRow;

        int preloadStartIndex;
        if (direction == PhotoViewer.Core.Services.ScrollPredictionService.ScrollDirection.Down)
        {
            // 向下滾動：預載入下方
            int lastVisibleRow = (int)Math.Ceiling((_offset.Y + _viewport.Height) / ItemHeight);
            preloadStartIndex = (lastVisibleRow + BufferRows) * itemsPerRow;
        }
        else if (direction == PhotoViewer.Core.Services.ScrollPredictionService.ScrollDirection.Up)
        {
            // 向上滾動：預載入上方
            preloadStartIndex = Math.Max(0, (currentRow - BufferRows - (preloadCount / itemsPerRow)) * itemsPerRow);
        }
        else
        {
            return; // 無明確方向
        }

        preloadStartIndex = Math.Max(0, Math.Min(preloadStartIndex, itemCount - 1));
        int actualPreloadCount = Math.Min(preloadCount, itemCount - preloadStartIndex);

        if (actualPreloadCount > 0)
        {
            PreloadRequested?.Invoke(this, new PreloadRequestEventArgs
            {
                StartIndex = preloadStartIndex,
                Count = actualPreloadCount
            });
        }
    }
}

/// <summary>
/// 預載入請求事件參數
/// </summary>
public class PreloadRequestEventArgs : EventArgs
{
    public int StartIndex { get; set; }
    public int Count { get; set; }
}
