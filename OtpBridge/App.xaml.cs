using System.Windows;
using System.Windows.Threading;
using OtpBridge.Services;

namespace OtpBridge;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;

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
            if (!TryAcquireSingleInstance(startMinimized))
            {
                Shutdown();
                return;
            }

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

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private bool TryAcquireSingleInstance(bool startMinimized)
    {
        var currentUserId = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var mutexName = $"Local\\OtpBridge-{currentUserId}";
        _singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        StartupLog.Info("Another OtpBridge instance is already running.");
        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;

        if (!startMinimized)
        {
            System.Windows.MessageBox.Show(
                "OtpBridge 已经在运行。请查看右下角系统托盘里的 OtpBridge 图标；如需关闭，请右键托盘图标选择“退出”。",
                "OtpBridge",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
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
