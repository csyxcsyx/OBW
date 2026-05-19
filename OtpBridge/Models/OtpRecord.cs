using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OtpBridge.Models;

public sealed class OtpRecord : INotifyPropertyChanged
{
    private bool _copied;
    private string _toastText = string.Empty;

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

    public string CopiedText => Copied ? "已复制" : "未复制";

    public string ToastText
    {
        get => _toastText;
        set
        {
            if (string.Equals(_toastText, value, StringComparison.Ordinal))
            {
                return;
            }

            _toastText = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
