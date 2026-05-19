namespace OtpBridge.Models;

public sealed class AppSettings
{
    public const int DefaultPort = 18080;

    public const int DefaultRecentRecordCount = 20;

    public const int MaxRecentRecordCount = 200;

    public int Port { get; set; } = DefaultPort;

    public string ApiToken { get; set; } = string.Empty;

    public bool AutoCopy { get; set; } = true;

    public bool ShowToast { get; set; } = true;

    public bool StartWithWindows { get; set; } = true;

    public int RecentRecordCount { get; set; } = DefaultRecentRecordCount;

    public string? CustomCodeRegex { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Port = Port,
            ApiToken = ApiToken,
            AutoCopy = AutoCopy,
            ShowToast = ShowToast,
            StartWithWindows = StartWithWindows,
            RecentRecordCount = RecentRecordCount,
            CustomCodeRegex = CustomCodeRegex
        };
    }
}
