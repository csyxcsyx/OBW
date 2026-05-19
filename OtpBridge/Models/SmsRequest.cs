using System.Text.Json.Serialization;

namespace OtpBridge.Models;

public sealed class SmsRequest
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("sender")]
    public string? Sender { get; set; }

    [JsonPropertyName("receivedAt")]
    public string? ReceivedAt { get; set; }
}
