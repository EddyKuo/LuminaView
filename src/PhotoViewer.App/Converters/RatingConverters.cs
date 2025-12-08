using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PhotoViewer.App.Converters;

/// <summary>
/// 將評分轉換為可見性 (評分 > 0 時顯示)
/// </summary>
public class RatingToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int rating && rating > 0)
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 將評分轉換為星星列表 (用於 ItemsControl)
/// </summary>
public class RatingToStarsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int rating && rating > 0)
        {
            // 返回一個長度等於 rating 的列表
            var stars = new List<int>();
            for (int i = 0; i < rating; i++)
            {
                stars.Add(i + 1);
            }
            return stars;
        }
        return new List<int>();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
