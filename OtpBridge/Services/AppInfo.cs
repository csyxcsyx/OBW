using System.Reflection;

namespace OtpBridge.Services;

public static class AppInfo
{
    public const string Name = "OtpBridge";

    public static string Version { get; } = GetVersion();

    public static string DisplayVersion =>
        Version.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? Version
            : $"v{Version}";

    public static string WindowTitle => $"{Name} {DisplayVersion}";

    private static string GetVersion()
    {
        var version = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "1.2";

        return version.Split('+', 2)[0];
    }
}
