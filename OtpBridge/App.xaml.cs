using System.Windows;
using System.Windows.Threading;
using OtpBridge.Services;

namespace OtpBridge;

public partial class App : System.Windows.Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                StartupLog.Error("Unhandled app domain exception.", exception);
            }
        };

        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        StartupLog.Info("Application startup.");

        try
        {
            var startMinimized = e.Args.Any(arg => string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));
            var window = new MainWindow(startMinimized);
            MainWindow = window;
            window.Show();
            window.Activate();
            StartupLog.Info("Main window shown.");
        }
        catch (Exception exception)
        {
            StartupLog.Error("Failed to show main window.", exception);
            System.Windows.MessageBox.Show(
                $"OtpBridge 启动失败，详细日志：{StartupLog.FilePath}{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "OtpBridge",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupLog.Error("Unhandled dispatcher exception.", e.Exception);
        System.Windows.MessageBox.Show(
            $"OtpBridge 发生未处理异常，详细日志：{StartupLog.FilePath}{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}",
            "OtpBridge",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
