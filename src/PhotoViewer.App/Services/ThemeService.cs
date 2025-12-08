using System;
using System.Linq;
using System.Windows;

namespace PhotoViewer.App.Services
{
    public class ThemeService
    {
        private const string DarkThemeSource = "Themes/Dark.xaml";
        private const string LightThemeSource = "Themes/Light.xaml";

        public enum ThemeType
        {
            Light,
            Dark
        }

        public ThemeType CurrentTheme { get; private set; } = ThemeType.Dark;

        public void ToggleTheme()
        {
            SetTheme(CurrentTheme == ThemeType.Dark ? ThemeType.Light : ThemeType.Dark);
        }

        public void SetTheme(ThemeType theme)
        {
            var oldThemeSource = theme == ThemeType.Light ? DarkThemeSource : LightThemeSource;
            var newThemeSource = theme == ThemeType.Light ? LightThemeSource : DarkThemeSource;

            var oldDict = App.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains(oldThemeSource));
            var newDict = new ResourceDictionary { Source = new Uri(newThemeSource, UriKind.Relative) };

            // Remove old theme
            if (oldDict != null)
            {
                App.Current.Resources.MergedDictionaries.Remove(oldDict);
            }
            else
            {
                // Fallback: Try to find any theme that isn't Common.xaml and remove it
                var existingTheme = App.Current.Resources.MergedDictionaries.FirstOrDefault(d => d.Source != null && !d.Source.OriginalString.Contains("Common.xaml"));
                if (existingTheme != null)
                {
                    App.Current.Resources.MergedDictionaries.Remove(existingTheme);
                }
            }

            // Add new theme
            App.Current.Resources.MergedDictionaries.Add(newDict);
            CurrentTheme = theme;
        }
    }
}
