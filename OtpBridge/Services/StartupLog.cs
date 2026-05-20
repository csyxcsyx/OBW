using System.IO;
using System.Text;

namespace OtpBridge.Services;

public static class StartupLog
{
    private static readonly object SyncRoot = new();

    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppInfo.Name);

    public static string FilePath => Path.Combine(DirectoryPath, "startup.log");

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(DirectoryPath);
                var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {level} {message}{Environment.NewLine}";
                File.AppendAllText(FilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Startup logging must never prevent the app from opening.
        }
    }
}
