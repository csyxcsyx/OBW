using System.Text.Json.Serialization;

namespace OtpBridge.Models;

public sealed class ApiSmsResponse
{
    public ApiSmsResponse(bool ok, string? code = null, string? error = null)
    {
        Ok = ok;
        Code = code;
        Error = error;
    }

    [JsonPropertyName("ok")]
    public bool Ok { get; }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; }
}
