using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using OtpBridge.Models;
using OtpBridge.Services;

namespace OtpBridge;

public partial class SettingsWindow : Window
{
    private bool _updatingLanguageOptions;
    private string _language;

    public SettingsWindow(AppSettings settings, string configPath)
    {
        InitializeComponent();

        Settings = settings.Clone();
        Settings.Language = LocalizationService.NormalizeLanguage(Settings.Language);
        _language = Settings.Language;
        ConfigPathText.Text = configPath;
        PopulateLanguageOptions();
        PortBox.Text = Settings.Port.ToString();
        TokenBox.Text = Settings.ApiToken;
        AutoCopyBox.IsChecked = Settings.AutoCopy;
        ShowToastBox.IsChecked = Settings.ShowToast;
        StartWithWindowsBox.IsChecked = Settings.StartWithWindows;
        RecentCountBox.Text = Settings.RecentRecordCount.ToString();
        CustomRegexBox.Text = Settings.CustomCodeRegex ?? string.Empty;
        ApplyLocalization();
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
            ShowError(Text("Settings.Error.Port"));
            return;
        }

        var token = TokenBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            ShowError(Text("Settings.Error.Token"));
            return;
        }

        if (!int.TryParse(RecentCountBox.Text.Trim(), out var recentCount) ||
            recentCount is < 1 or > AppSettings.MaxRecentRecordCount)
        {
            ShowError(FormatText("Settings.Error.RecentCount", AppSettings.MaxRecentRecordCount));
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
                ShowError(FormatText("Settings.Error.CustomRegex", ex.Message));
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
            Language = _language,
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
        System.Windows.MessageBox.Show(this, message, Text("Settings.Error.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingLanguageOptions)
        {
            return;
        }

        if (LanguageBox.SelectedValue is not string selectedLanguage)
        {
            return;
        }

        _language = LocalizationService.NormalizeLanguage(selectedLanguage);
        PopulateLanguageOptions();
        ApplyLocalization();
    }

    private void PopulateLanguageOptions()
    {
        _updatingLanguageOptions = true;
        try
        {
            LanguageBox.SelectedValuePath = nameof(LanguageOption.Code);
            LanguageBox.DisplayMemberPath = nameof(LanguageOption.DisplayName);
            LanguageBox.ItemsSource = LocalizationService.GetLanguageOptions(_language);
            LanguageBox.SelectedValue = _language;
        }
        finally
        {
            _updatingLanguageOptions = false;
        }
    }

    private void ApplyLocalization()
    {
        Title = Text("Settings.WindowTitle");
        SettingsTitleText.Text = Text("Settings.Title");
        SettingsSubtitleText.Text = Text("Settings.Subtitle");
        SaveButton.Content = Text("Settings.Button.Save");
        CancelButton.Content = Text("Settings.Button.Cancel");
        ConfigPathLabelText.Text = Text("Settings.ConfigPath");
        LanguageLabelText.Text = Text("Settings.Language");
        LanguageHintText.Text = Text("Settings.Language.Hint");
        PortLabelText.Text = Text("Settings.Port");
        PortHintText.Text = Text("Settings.Port.Hint");
        TokenLabelText.Text = Text("Settings.Token");
        GenerateTokenButton.Content = Text("Settings.GenerateToken");
        TokenHintText.Text = Text("Settings.Token.Hint");
        AutoCopyLabelText.Text = Text("Settings.AutoCopy");
        AutoCopyBox.Content = Text("Settings.AutoCopy.Content");
        ShowToastLabelText.Text = Text("Settings.ShowToast");
        ShowToastBox.Content = Text("Settings.ShowToast.Content");
        StartWithWindowsLabelText.Text = Text("Settings.StartWithWindows");
        StartWithWindowsBox.Content = Text("Settings.StartWithWindows.Content");
        RecentCountLabelText.Text = Text("Settings.RecentCount");
        RecentCountHintText.Text = Text("Settings.RecentCount.Hint");
        CustomRegexLabelText.Text = Text("Settings.CustomRegex");
        CustomRegexHintText.Text = Text("Settings.CustomRegex.Hint");
    }

    private string Text(string key)
    {
        return LocalizationService.Text(_language, key);
    }

    private string FormatText(string key, params object?[] args)
    {
        return LocalizationService.Format(_language, key, args);
    }
}
