using System.ComponentModel;
using System.Runtime.CompilerServices;
using OtpBridge.Services;

namespace OtpBridge.Models;

public sealed class OtpRecord : INotifyPropertyChanged
{
    private bool _copied;
    private string _language = AppSettings.DefaultLanguage;
    private string _toastKey = "Main.Toast.Disabled";

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTimeOffset ReceivedAt { get; init; }

    public string ReceivedAtText => ReceivedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    public string Sender { get; init; } = "-";

    public string Code { get; init; } = string.Empty;

    public bool Copied
    {
        get => _copied;
        set
        {
            if (_copied == value)
            {
                return;
            }

            _copied = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CopiedText));
        }
    }

    public string CopiedText => LocalizationService.Text(
        _language,
        Copied ? "Main.Record.Copied" : "Main.Record.NotCopied");

    public string ToastText => LocalizationService.Text(_language, _toastKey);

    public string ToastKey
    {
        get => _toastKey;
        set
        {
            if (string.Equals(_toastKey, value, StringComparison.Ordinal))
            {
                return;
            }

            _toastKey = value;
            OnPropertyChanged(nameof(ToastText));
        }
    }

    public void ApplyLanguage(string language)
    {
        var normalizedLanguage = LocalizationService.NormalizeLanguage(language);
        if (string.Equals(_language, normalizedLanguage, StringComparison.Ordinal))
        {
            return;
        }

        _language = normalizedLanguage;
        OnPropertyChanged(nameof(CopiedText));
        OnPropertyChanged(nameof(ToastText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
