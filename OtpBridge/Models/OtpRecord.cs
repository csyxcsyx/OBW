namespace OtpBridge.Models;

public sealed class OtpRecord
{
    public DateTimeOffset ReceivedAt { get; init; }

    public string ReceivedAtText => ReceivedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

    public string Sender { get; init; } = "-";

    public string Code { get; init; } = string.Empty;

    public bool Copied { get; init; }

    public string CopiedText => Copied ? "已复制" : "未复制";

    public string ToastText { get; init; } = string.Empty;
}
