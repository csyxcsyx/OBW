using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtpBridge.Models;

namespace OtpBridge.Services;

public sealed class LocalHttpServer : IAsyncDisposable
{
    private const long MaxRequestBodyBytes = 32 * 1024;

    private readonly Func<AppSettings> _settingsProvider;
    private readonly Func<SmsRequest, Task<SmsProcessingResult>> _smsHandler;
    private WebApplication? _app;

    public LocalHttpServer(Func<AppSettings> settingsProvider, Func<SmsRequest, Task<SmsProcessingResult>> smsHandler)
    {
        _settingsProvider = settingsProvider;
        _smsHandler = smsHandler;
    }

    public int? RunningPort { get; private set; }

    public bool IsRunning => _app is not null;

    public async Task StartAsync(int port)
    {
        await StopAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(LocalHttpServer).Assembly.FullName
        });

        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var app = builder.Build();

        app.MapGet("/", () => Results.Json(new { ok = true, app = AppInfo.Name, version = AppInfo.DisplayVersion }));
        app.MapGet("/health", () => Results.Json(new { ok = true }));
        app.MapPost("/api/sms", async (HttpContext context) => await HandleSmsAsync(context));

        await app.StartAsync();
        _app = app;
        RunningPort = port;
    }

    public async Task StopAsync()
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            await _app.StopAsync(TimeSpan.FromSeconds(2));
            await _app.DisposeAsync();
        }
        finally
        {
            _app = null;
            RunningPort = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task<IResult> HandleSmsAsync(HttpContext context)
    {
        if (!IsAuthorized(context))
        {
            return Results.Json(
                new ApiSmsResponse(false, error: "unauthorized"),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (context.Request.ContentLength > MaxRequestBodyBytes)
        {
            return Results.Json(
                new ApiSmsResponse(false, error: "request body is too large"),
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        if (!context.Request.HasJsonContentType())
        {
            return Results.Json(
                new ApiSmsResponse(false, error: "content-type must be application/json"),
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        SmsRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<SmsRequest>();
        }
        catch
        {
            return Results.Json(
                new ApiSmsResponse(false, error: "invalid json"),
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.Json(
                new ApiSmsResponse(false, error: "message is required"),
                statusCode: StatusCodes.Status400BadRequest);
        }

        SmsProcessingResult result;
        try
        {
            result = await _smsHandler(request);
        }
        catch
        {
            return Results.Json(
                new ApiSmsResponse(false, error: "processing failed"),
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return result.Success
            ? Results.Json(new ApiSmsResponse(true, result.Code))
            : Results.Json(new ApiSmsResponse(false, error: result.Error), statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private bool IsAuthorized(HttpContext context)
    {
        var expectedToken = _settingsProvider().ApiToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedToken = authorization[bearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedToken),
            Encoding.UTF8.GetBytes(expectedToken));
    }
}
