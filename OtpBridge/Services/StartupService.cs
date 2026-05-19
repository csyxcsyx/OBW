using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace OtpBridge.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OtpBridge";

    public static bool Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true) ??
                            Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                key.SetValue(ValueName, BuildCommandLine(), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception exception)
        {
            StartupLog.Error("Failed to apply startup setting.", exception);
            return false;
        }
    }

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildCommandLine()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            Path.GetExtension(processPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileNameWithoutExtension(processPath).Contains("OtpBridge", StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{processPath}\" --minimized";
        }

        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "OtpBridge.dll");
        if (File.Exists(assemblyPath))
        {
            var dotnetPath = FindDotnetPath();
            return $"\"{dotnetPath}\" \"{assemblyPath}\" --minimized";
        }

        var fallbackPath = processPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(fallbackPath))
        {
            throw new InvalidOperationException("Cannot determine application executable path.");
        }

        return $"\"{fallbackPath}\" --minimized";
    }

    private static string FindDotnetPath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        return "dotnet";
    }
}
