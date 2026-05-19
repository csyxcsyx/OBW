namespace OtpBridge.Models;

public sealed class OtpExtractionResult
{
    private OtpExtractionResult(bool success, string? code, string? error)
    {
        Success = success;
        Code = code;
        Error = error;
    }

    public bool Success { get; }

    public string? Code { get; }

    public string? Error { get; }

    public static OtpExtractionResult Found(string code) => new(true, code, null);

    public static OtpExtractionResult NotFound(string error) => new(false, null, error);
}
