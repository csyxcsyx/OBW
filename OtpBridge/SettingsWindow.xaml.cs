using System.Text.RegularExpressions;
using System.Windows;
using OtpBridge.Models;
using OtpBridge.Services;

namespace OtpBridge;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings, string configPath)
    {
        InitializeComponent();

        Settings = settings.Clone();
        ConfigPathText.Text = configPath;
        PortBox.Text = Settings.Port.ToString();
        TokenBox.Text = Settings.ApiToken;
        AutoCopyBox.IsChecked = Settings.AutoCopy;
        ShowToastBox.IsChecked = Settings.ShowToast;
        StartWithWindowsBox.IsChecked = Settings.StartWithWindows;
        RecentCountBox.Text = Settings.RecentRecordCount.ToString();
        CustomRegexBox.Text = Settings.CustomCodeRegex ?? string.Empty;
    }

    public AppSettings Settings { get; private set; }

    private void GenerateToken_Click(object sender, RoutedEventArgs e)
    {
        TokenBox.Text = ConfigService.GenerateToken();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PortBox.Text.Trim(), out var port) || port is < 1 or > 65535)
        {
            ShowError("监听端口必须是 1 到 65535 之间的数字。");
            return;
        }

        var token = TokenBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowError("API Token 不能为空。");
            return;
        }

        if (!int.TryParse(RecentCountBox.Text.Trim(), out var recentCount) ||
            recentCount is < 1 or > AppSettings.MaxRecentRecordCount)
        {
            ShowError($"最近记录数量必须是 1 到 {AppSettings.MaxRecentRecordCount} 之间的数字。");
            return;
        }

        var customRegex = CustomRegexBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(customRegex))
        {
            try
            {
                _ = new Regex(customRegex, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException ex)
            {
                ShowError($"自定义正则无效：{ex.Message}");
                return;
            }
        }

        Settings = new AppSettings
        {
            Port = port,
            ApiToken = token,
            AutoCopy = AutoCopyBox.IsChecked == true,
            ShowToast = ShowToastBox.IsChecked == true,
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            RecentRecordCount = recentCount,
            CustomCodeRegex = string.IsNullOrWhiteSpace(customRegex) ? null : customRegex
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        System.Windows.MessageBox.Show(this, message, "OtpBridge 设置", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
