using System.Windows;

namespace PhotoViewer.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化 SQLite
        SQLitePCL.Batteries.Init();

        // 設定全域錯誤處理
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"應用程式錯誤:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        MessageBox.Show($"嚴重錯誤:\n{exception?.Message}\n\n{exception?.StackTrace}",
            "嚴重錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

