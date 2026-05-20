using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OtpBridge.Models;

namespace OtpBridge.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string ConfigDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppInfo.Name);

    public string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public AppSettings Load()
    {
        AppSettings settings;
        var shouldSave = !File.Exists(ConfigPath);

        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                settings = new AppSettings();
                shouldSave = true;
            }
        }
        else
        {
            settings = new AppSettings();
        }

        shouldSave |= Normalize(settings);
        if (shouldSave)
        {
            Save(settings);
        }

        return settings;
    }

    public static string LoadLanguageOrDefault()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.Name,
            "config.json");

        if (!File.Exists(configPath))
        {
            return LocalizationService.DefaultLanguage;
        }

        try
        {
            var json = File.ReadAllText(configPath, Encoding.UTF8);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("language", out var languageElement) &&
                languageElement.ValueKind == JsonValueKind.String)
            {
                return LocalizationService.NormalizeLanguage(languageElement.GetString());
            }
        }
        catch
        {
            return LocalizationService.DefaultLanguage;
        }

        return LocalizationService.DefaultLanguage;
    }

    public void Save(AppSettings settings)
    {
        Normalize(settings);
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(ConfigPath, json, Encoding.UTF8);
    }

    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool Normalize(AppSettings settings)
    {
        var changed = false;

        if (settings.Port is < 1 or > 65535)
        {
            settings.Port = AppSettings.DefaultPort;
            changed = true;
        }

        var token = settings.ApiToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            settings.ApiToken = GenerateToken();
            changed = true;
        }
        else if (!string.Equals(settings.ApiToken, token, StringComparison.Ordinal))
        {
            settings.ApiToken = token;
            changed = true;
        }

        if (settings.RecentRecordCount < 1)
        {
            settings.RecentRecordCount = AppSettings.DefaultRecentRecordCount;
            changed = true;
        }

        var language = LocalizationService.NormalizeLanguage(settings.Language);
        if (!string.Equals(settings.Language, language, StringComparison.Ordinal))
        {
            settings.Language = language;
            changed = true;
        }

        if (settings.RecentRecordCount > AppSettings.MaxRecentRecordCount)
        {
            settings.RecentRecordCount = AppSettings.MaxRecentRecordCount;
            changed = true;
        }

        var customRegex = settings.CustomCodeRegex?.Trim();
        if (string.IsNullOrWhiteSpace(customRegex))
        {
            if (settings.CustomCodeRegex is not null)
            {
                settings.CustomCodeRegex = null;
                changed = true;
            }
        }
        else if (!string.Equals(settings.CustomCodeRegex, customRegex, StringComparison.Ordinal))
        {
            settings.CustomCodeRegex = customRegex;
            changed = true;
        }

        return changed;
    }
}
