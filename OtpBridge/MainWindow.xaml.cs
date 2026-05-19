using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using OtpBridge.Models;
using OtpBridge.Services;
using Forms = System.Windows.Forms;

namespace OtpBridge;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService;
    private readonly LocalHttpServer _httpServer;
    private readonly ToastService _toastService = new();
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _startupMenuItem;
    private readonly bool _startMinimized;
    private AppSettings _settings;
    private bool _exitRequested;
    private bool _isServerStarting;
    private string _listeningAddresses = string.Empty;
    private string _statusText = "正在启动...";
    private string _recentSummary = "尚未收到验证码。";

    public MainWindow(bool startMinimized = false)
    {
        _startMinimized = startMinimized;
        StartupLog.Info("MainWindow constructor started.");
        InitializeComponent();
        StartupLog.Info("MainWindow XAML initialized.");

        _configService = new ConfigService();
        _settings = _configService.Load();
        StartupLog.Info("Configuration loaded.");
        StartupService.Apply(_settings.StartWithWindows);
        _httpServer = new LocalHttpServer(() => _settings, ProcessSmsAsync);

        DataContext = this;
        TrySetupTrayIcon();
        RefreshSettingsDisplay();

        Loaded += async (_, _) => await StartServerAsync();
        Loaded += (_, _) => ApplyInitialWindowState();
        StartupLog.Info("MainWindow constructor finished.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OtpRecord> RecentRecords { get; } = [];

    public string ListeningAddresses
    {
        get => _listeningAddresses;
        private set
        {
            _listeningAddresses = value;
            OnPropertyChanged(nameof(ListeningAddresses));
        }
    }

    public string ApiToken => _settings.ApiToken;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string RecentSummary
    {
        get => _recentSummary;
        private set
        {
            _recentSummary = value;
            OnPropertyChanged(nameof(RecentSummary));
        }
    }

    private async Task StartServerAsync()
    {
        StartupLog.Info("HTTP server startup requested.");

        if (_isServerStarting)
        {
            return;
        }

        if (_httpServer.RunningPort == _settings.Port)
        {
            StatusText = $"已监听 0.0.0.0:{_settings.Port}";
            RefreshSettingsDisplay();
            return;
        }

        _isServerStarting = true;
        var requestedPort = _settings.Port;
        StatusText = $"正在监听 0.0.0.0:{requestedPort}...";
        SetTrayText("OtpBridge 正在启动");

        try
        {
            var result = await StartServerOnAvailablePortAsync(requestedPort);
            StatusText = result.PortChanged
                ? $"端口 {requestedPort} 被占用，已自动改用 0.0.0.0:{result.Port}"
                : $"已监听 0.0.0.0:{result.Port}";
            SetTrayText($"OtpBridge 已监听 {result.Port}");
            StartupLog.Info($"HTTP server started on port {result.Port}.");

            if (result.PortChanged)
            {
                var message = $"端口 {requestedPort} 被占用，已改用 {result.Port}。iPhone 快捷指令请使用新的监听地址。";
                _notifyIcon?.ShowBalloonTip(6000, "OtpBridge", message, Forms.ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"监听失败：{ex.Message}";
            SetTrayText("OtpBridge 监听失败");
            StartupLog.Error("HTTP server startup failed.", ex);
        }
        finally
        {
            _isServerStarting = false;
        }

        RefreshSettingsDisplay();
    }

    private async Task<(int Port, bool PortChanged)> StartServerOnAvailablePortAsync(int requestedPort)
    {
        var port = requestedPort;
        var portChanged = false;

        if (!PortService.IsAvailable(port))
        {
            port = PortService.FindAvailablePort(port);
            SavePortChange(port);
            portChanged = true;
            StartupLog.Info($"Port {requestedPort} is unavailable. Falling back to {port}.");
        }

        try
        {
            await _httpServer.StartAsync(port);
            return (port, portChanged);
        }
        catch (Exception exception) when (PortService.LooksLikeAddressInUse(exception))
        {
            var fallbackPort = PortService.FindAvailablePort(port);
            SavePortChange(fallbackPort);
            StartupLog.Info($"Port {port} became unavailable while starting. Falling back to {fallbackPort}.");
            await _httpServer.StartAsync(fallbackPort);
            return (fallbackPort, true);
        }
    }

    private void SavePortChange(int port)
    {
        if (_settings.Port == port)
        {
            return;
        }

        _settings.Port = port;
        _configService.Save(_settings);
    }

    private async Task<SmsProcessingResult> ProcessSmsAsync(SmsRequest request)
    {
        var settings = _settings.Clone();
        var extraction = OtpExtractor.Extract(request.Message, settings.CustomCodeRegex);
        if (!extraction.Success || string.IsNullOrWhiteSpace(extraction.Code))
        {
            return SmsProcessingResult.Fail(extraction.Error ?? "code not found");
        }

        var code = extraction.Code;
        var copied = false;
        if (settings.AutoCopy)
        {
            copied = await CopyToClipboardAsync(code);
        }

        var toastText = "未启用";
        if (settings.ShowToast)
        {
            var message = copied ? $"收到验证码 {code}，已复制。" : $"收到验证码 {code}。";
            await Dispatcher.InvokeAsync(() =>
            {
                _notifyIcon?.ShowBalloonTip(5000, "OtpBridge", message, Forms.ToolTipIcon.Info);
                _ = Task.Run(() => _toastService.ShowToast("OtpBridge", message));
            });
            toastText = "已弹出";
        }

        await Dispatcher.InvokeAsync(() =>
        {
            RecentRecords.Insert(0, new OtpRecord
            {
                ReceivedAt = ParseReceivedAt(request.ReceivedAt),
                Sender = string.IsNullOrWhiteSpace(request.Sender) ? "-" : request.Sender.Trim(),
                Code = code,
                Copied = copied,
                ToastText = toastText
            });

            TrimRecentRecords();
            UpdateRecentSummary();
        });

        return SmsProcessingResult.Ok(code);
    }

    private void TrySetupTrayIcon()
    {
        try
        {
            SetupTrayIcon();
        }
        catch (Exception exception)
        {
            StartupLog.Error("Tray icon setup failed. Continuing without tray icon.", exception);
        }
    }

    private void SetupTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = GetTrayIcon(),
            Text = "OtpBridge",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.Add("打开设置", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        _notifyIcon.ContextMenuStrip.Items.Add("复制最近验证码", null, (_, _) => _ = Dispatcher.InvokeAsync(CopyRecentCodeAsync));
        _notifyIcon.ContextMenuStrip.Items.Add("查看最近记录", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        _notifyIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _startupMenuItem = new Forms.ToolStripMenuItem("开机自启动")
        {
            CheckOnClick = true,
            Checked = _settings.StartWithWindows
        };
        _startupMenuItem.Click += (_, _) => Dispatcher.Invoke(ToggleStartupFromTray);
        _notifyIcon.ContextMenuStrip.Items.Add(_startupMenuItem);

        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        SetTrayText("OtpBridge");
    }

    private void RefreshSettingsDisplay()
    {
        try
        {
            var urls = NetworkAddressService.GetApiUrls(_settings.Port);
            ListeningAddresses = string.Join(Environment.NewLine, urls);
        }
        catch (Exception exception)
        {
            StartupLog.Error("Failed to refresh listening addresses.", exception);
            ListeningAddresses = $"http://127.0.0.1:{_settings.Port}/api/sms";
        }

        OnPropertyChanged(nameof(ApiToken));
        UpdateRecentSummary();
    }

    private void OpenSettings()
    {
        ShowMainWindow();

        var oldPort = _settings.Port;
        var window = new SettingsWindow(_settings.Clone(), _configService.ConfigPath)
        {
            Owner = this
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        _settings = window.Settings.Clone();
        _configService.Save(_settings);
        StartupService.Apply(_settings.StartWithWindows);
        UpdateTrayMenuState();
        TrimRecentRecords();
        RefreshSettingsDisplay();

        if (oldPort != _settings.Port)
        {
            _ = StartServerAsync();
        }
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Activate();
    }

    private void ApplyInitialWindowState()
    {
        if (!_startMinimized)
        {
            return;
        }

        WindowState = WindowState.Minimized;
        ShowInTaskbar = false;
        Hide();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }

        System.Windows.Application.Current.Shutdown();
    }

    private static async Task<bool> CopyToClipboardAsync(string value)
    {
        return await ClipboardService.SetTextAsync(value);
    }

    private async Task CopyRecentCodeAsync()
    {
        var recent = RecentRecords.FirstOrDefault();
        if (recent is null)
        {
            _notifyIcon?.ShowBalloonTip(2000, "OtpBridge", "暂无最近验证码。", Forms.ToolTipIcon.Info);
            return;
        }

        if (await CopyToClipboardAsync(recent.Code))
        {
            recent.Copied = true;
            _notifyIcon?.ShowBalloonTip(2000, "OtpBridge", $"已复制最近验证码 {recent.Code}。", Forms.ToolTipIcon.Info);
            return;
        }

        _notifyIcon?.ShowBalloonTip(2000, "OtpBridge", "复制失败，请稍后重试。", Forms.ToolTipIcon.Warning);
    }

    private async Task CopyListeningAddressAsync()
    {
        var address = ListeningAddresses
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        if (await CopyToClipboardAsync(address))
        {
            _notifyIcon?.ShowBalloonTip(2000, "OtpBridge", "已复制监听地址。", Forms.ToolTipIcon.Info);
        }
    }

    private void ToggleStartupFromTray()
    {
        if (_startupMenuItem is null)
        {
            return;
        }

        _settings.StartWithWindows = _startupMenuItem.Checked;
        _configService.Save(_settings);
        StartupService.Apply(_settings.StartWithWindows);
        UpdateTrayMenuState();
    }

    private void TrimRecentRecords()
    {
        while (RecentRecords.Count > _settings.RecentRecordCount)
        {
            RecentRecords.RemoveAt(RecentRecords.Count - 1);
        }
    }

    private void UpdateRecentSummary()
    {
        RecentSummary = RecentRecords.Count == 0
            ? "尚未收到验证码。"
            : $"内存中保留最近 {RecentRecords.Count} 条，设置上限 {_settings.RecentRecordCount} 条。";
    }

    private static DateTimeOffset ParseReceivedAt(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var receivedAt)
            ? receivedAt
            : DateTimeOffset.Now;
    }

    private void SetTrayText(string value)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Text = value.Length <= 63 ? value : $"{value[..60]}...";
    }

    private void UpdateTrayMenuState()
    {
        if (_startupMenuItem is not null)
        {
            _startupMenuItem.Checked = _settings.StartWithWindows;
        }

    }

    private static System.Drawing.Icon GetTrayIcon()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Environment.ProcessPath)
                ? System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? System.Drawing.SystemIcons.Application
                : System.Drawing.SystemIcons.Application;
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private async void CopyAddress_Click(object sender, RoutedEventArgs e) => await CopyListeningAddressAsync();

    private async void CopyRecent_Click(object sender, RoutedEventArgs e) => await CopyRecentCodeAsync();

    private async void CopyToken_Click(object sender, RoutedEventArgs e) => await CopyToClipboardAsync(_settings.ApiToken);

    private void ClearRecords_Click(object sender, RoutedEventArgs e)
    {
        RecentRecords.Clear();
        UpdateRecentSummary();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    protected override async void OnClosed(EventArgs e)
    {
        StartupLog.Info("MainWindow closed.");
        _notifyIcon?.Dispose();
        await _httpServer.DisposeAsync();
        base.OnClosed(e);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
