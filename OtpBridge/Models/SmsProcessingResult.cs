namespace OtpBridge.Models;

public sealed class SmsProcessingResult
{
    private SmsProcessingResult(bool success, string? code, string? error)
    {
        Success = success;
        Code = code;
        Error = error;
    }

    public bool Success { get; }

    public string? Code { get; }

    public string? Error { get; }

    public static SmsProcessingResult Ok(string code) => new(true, code, null);

    public static SmsProcessingResult Fail(string error) => new(false, null, error);
}
