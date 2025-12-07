using System;
using System.Collections.Generic;
using System.Linq;

namespace PhotoViewer.Core.Services;

/// <summary>
/// 預測滾動方向和速度，以智能預載入圖片
/// </summary>
public class ScrollPredictionService
{
    private double _lastOffset = 0;
    private DateTime _lastTime = DateTime.Now;
    private double _velocity = 0;
    private ScrollDirection _direction = ScrollDirection.None;
    private readonly Queue<double> _recentOffsets = new(capacity: 5);

    public enum ScrollDirection { None, Up, Down }

    /// <summary>
    /// 更新滾動位置並計算速度
    /// </summary>
    public void UpdateScrollPosition(double currentOffset)
    {
        var now = DateTime.Now;
        var deltaTime = (now - _lastTime).TotalSeconds;

        if (deltaTime > 0.01) // 忽略小於 10ms 的更新
        {
            var deltaOffset = currentOffset - _lastOffset;
            _velocity = deltaOffset / deltaTime;

            // 確定方向
            if (Math.Abs(deltaOffset) > 1) // 閾值以忽略微滾動
            {
                _direction = deltaOffset > 0 ? ScrollDirection.Down : ScrollDirection.Up;
            }

            _recentOffsets.Enqueue(deltaOffset);
            if (_recentOffsets.Count > 5) _recentOffsets.Dequeue();

            _lastOffset = currentOffset;
            _lastTime = now;
        }
    }

    /// <summary>
    /// 獲取預測的滾動方向
    /// </summary>
    public ScrollDirection GetDirection() => _direction;

    /// <summary>
    /// 根據速度獲取預載入數量
    /// </summary>
    public int GetPreloadCount()
    {
        var avgVelocity = Math.Abs(_velocity);

        // 根據速度激進預載入
        if (avgVelocity > 2000) return 200;      // 快速滾動
        if (avgVelocity > 1000) return 150;      // 中速滾動
        if (avgVelocity > 500) return 100;       // 慢速滾動
        return 50;                                // 閒置/微滾動
    }

    /// <summary>
    /// 檢查滾動是否穩定（用戶停止）
    /// </summary>
    public bool IsStabilizing()
    {
        if (_recentOffsets.Count < 3) return false;

        var recent = _recentOffsets.ToArray();
        var avgRecent = recent.Average();
        return Math.Abs(avgRecent) < 50; // 平均小於 50px/更新
    }
}
