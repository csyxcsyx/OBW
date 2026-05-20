using System.Windows;
using System.Windows.Threading;
using OtpBridge.Services;

namespace OtpBridge;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private string _language = LocalizationService.DefaultLanguage;

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
            _language = ConfigService.LoadLanguageOrDefault();
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
                LocalizationService.Format(_language, "App.StartFailed", StartupLog.FilePath, Environment.NewLine, exception.Message),
                AppInfo.Name,
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
        var mutexName = $"Local\\{AppInfo.Name}-{currentUserId}";
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
                LocalizationService.Text(_language, "App.AlreadyRunning"),
                AppInfo.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return false;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupLog.Error("Unhandled dispatcher exception.", e.Exception);
        System.Windows.MessageBox.Show(
            LocalizationService.Format(_language, "App.UnhandledDispatcher", StartupLog.FilePath, Environment.NewLine, e.Exception.Message),
            AppInfo.Name,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
