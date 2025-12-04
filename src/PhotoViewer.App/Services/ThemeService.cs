using System.Windows;

namespace PhotoViewer.App.Services;

public enum ThemeType
{
    Light,
    Dark
}

public class ThemeService
{
    private const string LightThemeSource = "Themes/Light.xaml";
    private const string DarkThemeSource = "Themes/Dark.xaml";

    public ThemeType CurrentTheme { get; private set; } = ThemeType.Dark;

    public void SetTheme(ThemeType theme)
    {
        var appResources = Application.Current.Resources;
        var mergedDicts = appResources.MergedDictionaries;

        // Find the existing theme dictionary
        var currentThemeDict = mergedDicts.FirstOrDefault(d => 
            d.Source != null && 
            (d.Source.OriginalString.EndsWith("Light.xaml") || 
             d.Source.OriginalString.EndsWith("Dark.xaml")));

        if (currentThemeDict != null)
        {
            mergedDicts.Remove(currentThemeDict);
        }

        // Add the new theme dictionary
        var newThemeSource = theme == ThemeType.Light ? LightThemeSource : DarkThemeSource;
        var newDict = new ResourceDictionary { Source = new Uri(newThemeSource, UriKind.Relative) };
        mergedDicts.Add(newDict);

        CurrentTheme = theme;
    }

    public void ToggleTheme()
    {
        SetTheme(CurrentTheme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark);
    }
}
