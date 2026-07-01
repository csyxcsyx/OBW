using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Threading;
using OtpBridge.Models;
using OtpBridge.Services;
using Forms = System.Windows.Forms;

namespace OtpBridge;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService;
    private readonly LocalHttpServer _httpServer;
    private readonly ToastService _toastService = new();
    private readonly DispatcherTimer _addressRefreshTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _clipboardCopyGate = new(1, 1);
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _startupMenuItem;
    private readonly bool _startMinimized;
    private long _copyRequestVersion;
    private AppSettings _settings;
    private bool _exitRequested;
    private bool _isServerStarting;
    private bool _isRestoringWindowBounds;
    private Rect? _lastNormalBounds;
    private IReadOnlyList<string> _lastLanAddressHosts = [];
    private string _listeningAddresses = string.Empty;
    private string _statusText = "正在启动...";
    private string _statusKey = "Main.Status.Starting";
    private object?[] _statusArgs = [];
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
        SetupAddressRefresh();
        TrySetupTrayIcon();
        Title = AppInfo.WindowTitle;
        ApplyLocalization();
        RefreshSettingsDisplay();

        LocationChanged += (_, _) => CaptureCurrentNormalBounds();
        SizeChanged += (_, _) => CaptureCurrentNormalBounds();
        Loaded += (_, _) => CaptureCurrentNormalBounds();
        Loaded += async (_, _) => await StartServerAsync();
        Loaded += (_, _) => StartAddressRefreshTimer();
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
            SetStatus("Main.Status.Listening", _settings.Port);
            RefreshSettingsDisplay();
            return;
        }

        _isServerStarting = true;
        var requestedPort = _settings.Port;
        SetStatus("Main.Status.StartingListen", requestedPort);
        SetTrayText(Text("Main.Tray.Starting"));

        try
        {
            var result = await StartServerOnAvailablePortAsync(requestedPort);
            if (result.PortChanged)
            {
                SetStatus("Main.Status.PortChanged", requestedPort, result.Port);
            }
            else
            {
                SetStatus("Main.Status.Listening", result.Port);
            }

            SetTrayText(FormatText("Main.Tray.Listening", result.Port));
            StartupLog.Info($"HTTP server started on port {result.Port}.");

            if (result.PortChanged)
            {
                var message = FormatText("Main.PortChanged.Notification", requestedPort, result.Port);
                ShowTrayTip(6000, message, Forms.ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            SetStatus("Main.Status.ListenFailed", ex.Message);
            SetTrayText(Text("Main.Tray.ListenFailed"));
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
        var toastKey = "Main.Toast.Disabled";
        if (settings.ShowToast)
        {
            toastKey = "Main.Toast.Shown";
        }

        var record = new OtpRecord
        {
            ReceivedAt = ParseReceivedAt(request.ReceivedAt),
            Sender = string.IsNullOrWhiteSpace(request.Sender) ? "-" : request.Sender.Trim(),
            Code = code,
            Copied = false,
            ToastKey = toastKey
        };
        record.ApplyLanguage(settings.Language);

        var toastMessage = LocalizationService.Format(
            settings.Language,
            settings.AutoCopy ? "Main.Toast.ReceivedCopying" : "Main.Toast.Received",
            code);

        await Dispatcher.InvokeAsync(() =>
        {
            record.ApplyLanguage(_settings.Language);
            RecentRecords.Insert(0, record);

            TrimRecentRecords();
            UpdateRecentSummary();

            if (settings.ShowToast)
            {
                ShowTrayTip(5000, toastMessage, Forms.ToolTipIcon.Info);
                QueueWindowsToast(toastMessage);
            }
        });

        if (settings.AutoCopy)
        {
            var copyRequestVersion = Interlocked.Increment(ref _copyRequestVersion);
            _ = CompleteAutoCopyAsync(code, record, settings.Language, copyRequestVersion);
        }

        return SmsProcessingResult.Ok(code);
    }

    private void QueueWindowsToast(string message)
    {
        _ = Task.Run(() =>
        {
            try
            {
                _toastService.ShowToast(AppInfo.Name, message);
            }
            catch (Exception exception)
            {
                StartupLog.Error("Queued toast notification failed.", exception);
            }
        });
    }

    private async Task CompleteAutoCopyAsync(
        string code,
        OtpRecord record,
        string language,
        long copyRequestVersion)
    {
        try
        {
            bool copied;
            await _clipboardCopyGate.WaitAsync();
            try
            {
                if (copyRequestVersion != Volatile.Read(ref _copyRequestVersion))
                {
                    return;
                }

                copied = await CopyToClipboardAsync(code);
            }
            finally
            {
                _clipboardCopyGate.Release();
            }

            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (RecentRecords.Contains(record))
                {
                    record.Copied = copied;
                }

                if (!copied)
                {
                    ShowTrayTip(3000, LocalizationService.Text(language, "Main.Copy.Failed"), Forms.ToolTipIcon.Warning);
                }
            });
        }
        catch (Exception exception)
        {
            StartupLog.Error("Background auto-copy failed.", exception);
        }
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
            Text = AppInfo.Name,
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.Add(Text("Main.Tray.OpenSettings"), null, (_, _) => Dispatcher.Invoke(OpenSettings));
        _notifyIcon.ContextMenuStrip.Items.Add(Text("Main.Tray.CopyRecent"), null, (_, _) => _ = Dispatcher.InvokeAsync(CopyRecentCodeAsync));
        _notifyIcon.ContextMenuStrip.Items.Add(Text("Main.Tray.ViewRecent"), null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        _notifyIcon.ContextMenuStrip.Items.Add(new Forms.ToolStripSeparator());
        _startupMenuItem = new Forms.ToolStripMenuItem(Text("Main.Tray.StartWithWindows"))
        {
            CheckOnClick = true,
            Checked = _settings.StartWithWindows
        };
        _startupMenuItem.Click += (_, _) => Dispatcher.Invoke(ToggleStartupFromTray);
        _notifyIcon.ContextMenuStrip.Items.Add(_startupMenuItem);

        _notifyIcon.ContextMenuStrip.Items.Add(Text("Main.Tray.Exit"), null, (_, _) => Dispatcher.Invoke(ExitApplication));
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        SetTrayText(AppInfo.Name);
    }

    private void RefreshSettingsDisplay()
    {
        try
        {
            var urls = GetStableApiUrls(_settings.Port);
            ListeningAddresses = string.Join(Environment.NewLine, urls);
        }
        catch (Exception exception)
        {
            StartupLog.Error("Failed to refresh listening addresses.", exception);
            var fallbackUrls = BuildApiUrls(_lastLanAddressHosts, _settings.Port);
            ListeningAddresses = fallbackUrls.Count > 0
                ? string.Join(Environment.NewLine, fallbackUrls)
                : $"http://127.0.0.1:{_settings.Port}/api/sms";
        }

        OnPropertyChanged(nameof(ApiToken));
        UpdateRecentSummary();
    }

    private void SetupAddressRefresh()
    {
        _addressRefreshTimer.Tick += (_, _) => RefreshSettingsDisplay();
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    private void StartAddressRefreshTimer()
    {
        RefreshSettingsDisplay();
        if (!_addressRefreshTimer.IsEnabled)
        {
            _addressRefreshTimer.Start();
        }
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        QueueAddressRefresh();
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        QueueAddressRefresh();
    }

    private void QueueAddressRefresh()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.InvokeAsync(RefreshSettingsDisplay, DispatcherPriority.Background);
    }

    private IReadOnlyList<string> GetStableApiUrls(int port)
    {
        var urls = NetworkAddressService.GetApiUrls(port);
        var lanHosts = urls
            .Select(TryGetUrlHost)
            .OfType<string>()
            .Where(host => !IsLoopbackHost(host))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (lanHosts.Length > 0)
        {
            _lastLanAddressHosts = lanHosts;
            return BuildApiUrls(_lastLanAddressHosts, port);
        }

        var lastKnownUrls = BuildApiUrls(_lastLanAddressHosts, port);
        return lastKnownUrls.Count > 0 ? lastKnownUrls : urls;
    }

    private static IReadOnlyList<string> BuildApiUrls(IEnumerable<string> hosts, int port)
    {
        return hosts
            .Select(host => $"http://{host}:{port}/api/sms")
            .ToArray();
    }

    private static string? TryGetUrlHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
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
        ApplyLocalization();
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
        var wasMinimized = WindowState == WindowState.Minimized;

        ShowInTaskbar = true;
        Show();

        if (wasMinimized)
        {
            WindowState = WindowState.Normal;
            RestoreLastNormalBounds();
        }

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

    private void CaptureCurrentNormalBounds()
    {
        if (_isRestoringWindowBounds || !IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        if (Width <= 0 || Height <= 0 || double.IsNaN(Left) || double.IsNaN(Top))
        {
            return;
        }

        _lastNormalBounds = new Rect(Left, Top, Width, Height);
    }

    private void CaptureRestoreBounds()
    {
        if (RestoreBounds.Width <= 0 || RestoreBounds.Height <= 0)
        {
            return;
        }

        _lastNormalBounds = RestoreBounds;
    }

    private void RestoreLastNormalBounds()
    {
        if (_lastNormalBounds is not { } bounds)
        {
            return;
        }

        if (bounds.Width < MinWidth || bounds.Height < MinHeight)
        {
            return;
        }

        _isRestoringWindowBounds = true;
        try
        {
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }
        finally
        {
            _isRestoringWindowBounds = false;
        }
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
            ShowTrayTip(2000, Text("Main.Copy.NoRecent"), Forms.ToolTipIcon.Info);
            return;
        }

        if (await CopyToClipboardAsync(recent.Code))
        {
            recent.Copied = true;
            ShowTrayTip(2000, FormatText("Main.Copy.RecentCopied", recent.Code), Forms.ToolTipIcon.Info);
            return;
        }

        ShowTrayTip(2000, Text("Main.Copy.Failed"), Forms.ToolTipIcon.Warning);
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
            ShowTrayTip(2000, Text("Main.Copy.AddressCopied"), Forms.ToolTipIcon.Info);
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
            ? Text("Main.Recent.Empty")
            : FormatText("Main.Recent.Summary", RecentRecords.Count, _settings.RecentRecordCount);
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

    private void ShowTrayTip(int timeout, string message, Forms.ToolTipIcon icon)
    {
        _notifyIcon?.ShowBalloonTip(timeout, AppInfo.Name, message, icon);
    }

    private void UpdateTrayMenuState()
    {
        if (_startupMenuItem is not null)
        {
            _startupMenuItem.Checked = _settings.StartWithWindows;
            _startupMenuItem.Text = Text("Main.Tray.StartWithWindows");
        }

        if (_notifyIcon?.ContextMenuStrip is null)
        {
            return;
        }

        var items = _notifyIcon.ContextMenuStrip.Items;
        if (items.Count >= 6)
        {
            items[0].Text = Text("Main.Tray.OpenSettings");
            items[1].Text = Text("Main.Tray.CopyRecent");
            items[2].Text = Text("Main.Tray.ViewRecent");
            items[5].Text = Text("Main.Tray.Exit");
        }
    }

    private void ApplyLocalization()
    {
        Title = AppInfo.WindowTitle;
        VersionText.Text = AppInfo.DisplayVersion;
        StatusSuffixText.Text = Text("Main.StatusSuffix");
        OpenSettingsButton.Content = Text("Main.Button.OpenSettings");
        CopyAddressButton.Content = Text("Main.Button.CopyAddress");
        CopyRecentButton.Content = Text("Main.Button.CopyRecent");
        ConnectionTitleText.Text = Text("Main.Connection.Title");
        LanReminderText.Text = Text("Main.Connection.LanReminder");
        ConnectionDescriptionText.Text = Text("Main.Connection.Description");
        AddressLabelText.Text = Text("Main.Address.Label");
        TokenTitleText.Text = Text("Main.Token.Title");
        TokenDescriptionText.Text = Text("Main.Token.Description");
        CopyTokenButton.Content = Text("Main.Button.CopyToken");
        GuideTitleText.Text = Text("Main.Guide.Title");
        GuideDescriptionText.Text = Text("Main.Guide.Description");
        Step1TitleText.Text = Text("Main.Guide.Step1.Title");
        Step1BodyText.Text = Text("Main.Guide.Step1.Body");
        Step2TitleText.Text = Text("Main.Guide.Step2.Title");
        Step2BodyText.Text = Text("Main.Guide.Step2.Body");
        Step3TitleText.Text = Text("Main.Guide.Step3.Title");
        Step3BodyText.Text = Text("Main.Guide.Step3.Body");
        Step4TitleText.Text = Text("Main.Guide.Step4.Title");
        Step4BodyText.Text = Text("Main.Guide.Step4.Body");
        Step5TitleText.Text = Text("Main.Guide.Step5.Title");
        Step5BodyText.Text = Text("Main.Guide.Step5.Body");
        RecentTitleText.Text = Text("Main.Recent.Title");
        ClearRecordsButton.Content = Text("Main.Button.ClearRecords");
        TimeColumn.Header = Text("Main.Grid.Time");
        SenderColumn.Header = Text("Main.Grid.Sender");
        CodeColumn.Header = Text("Main.Grid.Code");
        CopiedColumn.Header = Text("Main.Grid.Copied");
        ToastColumn.Header = Text("Main.Grid.Toast");

        foreach (var record in RecentRecords)
        {
            record.ApplyLanguage(_settings.Language);
        }

        UpdateTrayMenuState();
        RefreshStatusText();
        UpdateRecentSummary();
    }

    private void SetStatus(string key, params object?[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        RefreshStatusText();
    }

    private void RefreshStatusText()
    {
        StatusText = _statusArgs.Length == 0
            ? Text(_statusKey)
            : FormatText(_statusKey, _statusArgs);
    }

    private string Text(string key)
    {
        return LocalizationService.Text(_settings.Language, key);
    }

    private string FormatText(string key, params object?[] args)
    {
        return LocalizationService.Format(_settings.Language, key, args);
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
            CaptureRestoreBounds();
            return;
        }

        CaptureCurrentNormalBounds();
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
        _addressRefreshTimer.Stop();
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _notifyIcon?.Dispose();
        await _httpServer.DisposeAsync();
        base.OnClosed(e);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
