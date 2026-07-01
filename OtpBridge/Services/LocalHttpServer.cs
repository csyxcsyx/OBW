using System.IO;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OtpBridge.Models;

namespace OtpBridge.Services;

public sealed class LocalHttpServer : IAsyncDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxRequestBodyBytes = 32 * 1024;
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<AppSettings> _settingsProvider;
    private readonly Func<SmsRequest, Task<SmsProcessingResult>> _smsHandler;
    private CancellationTokenSource? _stopCts;
    private TcpListener? _listener;
    private Task? _acceptLoopTask;

    public LocalHttpServer(Func<AppSettings> settingsProvider, Func<SmsRequest, Task<SmsProcessingResult>> smsHandler)
    {
        _settingsProvider = settingsProvider;
        _smsHandler = smsHandler;
    }

    public int? RunningPort { get; private set; }

    public bool IsRunning => _listener is not null;

    public async Task StartAsync(int port)
    {
        await StopAsync();

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
        listener.Start(backlog: 32);

        var cts = new CancellationTokenSource();
        _listener = listener;
        _stopCts = cts;
        RunningPort = port;
        _acceptLoopTask = AcceptLoopAsync(listener, cts.Token);
    }

    public async Task StopAsync()
    {
        var listener = _listener;
        var cts = _stopCts;
        var acceptLoopTask = _acceptLoopTask;

        if (listener is null)
        {
            return;
        }

        _listener = null;
        _stopCts = null;
        _acceptLoopTask = null;
        RunningPort = null;

        try
        {
            cts?.Cancel();
            listener.Stop();

            if (acceptLoopTask is not null)
            {
                await acceptLoopTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            StartupLog.Error("HTTP server shutdown failed.", exception);
        }
        finally
        {
            cts?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken stopToken)
    {
        while (!stopToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopToken);
                var acceptedClient = client;
                client = null;
                _ = Task.Run(() => HandleClientAsync(acceptedClient, stopToken), CancellationToken.None);
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (stopToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                StartupLog.Error("HTTP server accept failed.", exception);
                client?.Dispose();
                await Task.Delay(100, stopToken).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stopToken)
    {
        using var _ = client;

        try
        {
            client.NoDelay = true;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
            timeoutCts.CancelAfter(ClientTimeout);

            var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, timeoutCts.Token);
            if (request is null)
            {
                return;
            }

            var path = GetPathOnly(request.Target);
            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                path.Equals("/", StringComparison.Ordinal))
            {
                await WriteJsonAsync(stream, HttpStatusCode.OK, new { ok = true, app = AppInfo.Name, version = AppInfo.DisplayVersion }, timeoutCts.Token);
                return;
            }

            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(stream, HttpStatusCode.OK, new { ok = true }, timeoutCts.Token);
                return;
            }

            if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                path.Equals("/api/sms", StringComparison.OrdinalIgnoreCase))
            {
                await HandleSmsAsync(stream, request, timeoutCts.Token);
                return;
            }

            await WriteJsonAsync(
                stream,
                HttpStatusCode.NotFound,
                new ApiSmsResponse(false, error: "not found"),
                timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            StartupLog.Error("HTTP request handling failed.", exception);
        }
    }

    private async Task HandleSmsAsync(NetworkStream stream, SimpleHttpRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(request))
        {
            await WriteJsonAsync(
                stream,
                HttpStatusCode.Unauthorized,
                new ApiSmsResponse(false, error: "unauthorized"),
                cancellationToken);
            return;
        }

        if (request.BodyTooLarge)
        {
            await WriteJsonAsync(
                stream,
                HttpStatusCode.RequestEntityTooLarge,
                new ApiSmsResponse(false, error: "request body is too large"),
                cancellationToken);
            return;
        }

        if (!HasJsonContentType(request))
        {
            await WriteJsonAsync(
                stream,
                HttpStatusCode.UnsupportedMediaType,
                new ApiSmsResponse(false, error: "content-type must be application/json"),
                cancellationToken);
            return;
        }

        SmsRequest? smsRequest;
        try
        {
            smsRequest = JsonSerializer.Deserialize<SmsRequest>(request.Body.Span, JsonOptions);
        }
        catch
        {
            await WriteJsonAsync(
                stream,
                HttpStatusCode.BadRequest,
                new ApiSmsResponse(false, error: "invalid json"),
                cancellationToken);
            return;
        }

        if (smsRequest is null || string.IsNullOrWhiteSpace(smsRequest.Message))
        {
            await WriteJsonAsync(
                stream,
                HttpStatusCode.BadRequest,
                new ApiSmsResponse(false, error: "message is required"),
                cancellationToken);
            return;
        }

        SmsProcessingResult result;
        try
        {
            result = await _smsHandler(smsRequest);
        }
        catch (Exception exception)
        {
            StartupLog.Error("SMS request processing failed.", exception);
            await WriteJsonAsync(
                stream,
                HttpStatusCode.InternalServerError,
                new ApiSmsResponse(false, error: "processing failed"),
                cancellationToken);
            return;
        }

        if (result.Success)
        {
            await WriteJsonAsync(stream, HttpStatusCode.OK, new ApiSmsResponse(true, result.Code), cancellationToken);
            return;
        }

        await WriteJsonAsync(
            stream,
            HttpStatusCode.UnprocessableEntity,
            new ApiSmsResponse(false, error: result.Error),
            cancellationToken);
    }

    private async Task<SimpleHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var received = new List<byte>(4096);
        var buffer = new byte[4096];
        var headerEnd = -1;

        while (headerEnd < 0)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                return null;
            }

            for (var i = 0; i < bytesRead; i++)
            {
                received.Add(buffer[i]);
            }

            headerEnd = IndexOfHeaderEnd(received);
            if (headerEnd < 0 && received.Count > MaxHeaderBytes)
            {
                throw new InvalidDataException("HTTP request headers are too large.");
            }
        }

        var headerText = Encoding.ASCII.GetString(received.GetRange(0, headerEnd).ToArray());
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0)
        {
            throw new InvalidDataException("HTTP request line is missing.");
        }

        var requestLine = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLine.Length < 2)
        {
            throw new InvalidDataException("HTTP request line is invalid.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var separatorIndex = lines[i].IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = lines[i][..separatorIndex].Trim();
            var value = lines[i][(separatorIndex + 1)..].Trim();
            headers[name] = value;
        }

        var bodyStart = headerEnd + 4;
        var initialBody = received.Count > bodyStart
            ? received.GetRange(bodyStart, received.Count - bodyStart).ToArray()
            : [];

        if (HasChunkedTransferEncoding(headers))
        {
            var chunkedBody = await ReadChunkedBodyAsync(stream, initialBody, cancellationToken);
            return new SimpleHttpRequest(
                requestLine[0],
                requestLine[1],
                headers,
                chunkedBody.Body,
                chunkedBody.BodyTooLarge);
        }

        var contentLength = GetContentLength(headers);
        var bodyTooLarge = contentLength > MaxRequestBodyBytes;
        if (bodyTooLarge)
        {
            return new SimpleHttpRequest(requestLine[0], requestLine[1], headers, ReadOnlyMemory<byte>.Empty, true);
        }

        var body = new byte[contentLength];
        var copyCount = Math.Min(initialBody.Length, contentLength);
        if (copyCount > 0)
        {
            Array.Copy(initialBody, 0, body, 0, copyCount);
        }

        while (copyCount < contentLength)
        {
            var bytesRead = await stream.ReadAsync(body.AsMemory(copyCount, contentLength - copyCount), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            copyCount += bytesRead;
        }

        return new SimpleHttpRequest(requestLine[0], requestLine[1], headers, body, false);
    }

    private static async Task<(ReadOnlyMemory<byte> Body, bool BodyTooLarge)> ReadChunkedBodyAsync(
        NetworkStream stream,
        byte[] initialBody,
        CancellationToken cancellationToken)
    {
        var reader = new BufferedBodyReader(stream, initialBody);
        using var body = new MemoryStream();

        while (true)
        {
            var sizeLine = await reader.ReadLineAsync(maxLength: 128, cancellationToken);
            var sizeText = sizeLine.Split(';', 2)[0].Trim();
            if (!int.TryParse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var chunkSize) ||
                chunkSize < 0)
            {
                throw new InvalidDataException("Invalid chunk size.");
            }

            if (chunkSize == 0)
            {
                while (true)
                {
                    var trailerLine = await reader.ReadLineAsync(maxLength: 1024, cancellationToken);
                    if (trailerLine.Length == 0)
                    {
                        break;
                    }
                }

                return (body.ToArray(), false);
            }

            if (body.Length + chunkSize > MaxRequestBodyBytes)
            {
                return (ReadOnlyMemory<byte>.Empty, true);
            }

            var chunk = new byte[chunkSize];
            await reader.ReadExactAsync(chunk, cancellationToken);
            body.Write(chunk, 0, chunk.Length);

            var crlf = new byte[2];
            await reader.ReadExactAsync(crlf, cancellationToken);
            if (crlf[0] != '\r' || crlf[1] != '\n')
            {
                throw new InvalidDataException("Invalid chunk terminator.");
            }
        }
    }

    private bool IsAuthorized(SimpleHttpRequest request)
    {
        var expectedToken = _settingsProvider().ApiToken?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        if (!request.Headers.TryGetValue("Authorization", out var authorization))
        {
            return false;
        }

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

    private static bool HasJsonContentType(SimpleHttpRequest request)
    {
        if (!request.Headers.TryGetValue("Content-Type", out var contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetContentLength(IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Content-Length", out var value))
        {
            return 0;
        }

        return int.TryParse(value, out var contentLength) && contentLength > 0
            ? contentLength
            : 0;
    }

    private static bool HasChunkedTransferEncoding(IReadOnlyDictionary<string, string> headers)
    {
        return headers.TryGetValue("Transfer-Encoding", out var value) &&
               value.Split(',')
                   .Any(item => item.Trim().Equals("chunked", StringComparison.OrdinalIgnoreCase));
    }

    private static int IndexOfHeaderEnd(IReadOnlyList<byte> bytes)
    {
        for (var i = 0; i <= bytes.Count - 4; i++)
        {
            if (bytes[i] == '\r' &&
                bytes[i + 1] == '\n' &&
                bytes[i + 2] == '\r' &&
                bytes[i + 3] == '\n')
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetPathOnly(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath;
        }

        var queryIndex = target.IndexOf('?');
        return queryIndex >= 0 ? target[..queryIndex] : target;
    }

    private static async Task WriteJsonAsync(
        NetworkStream stream,
        HttpStatusCode statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)statusCode} {GetReasonPhrase(statusCode)}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n" +
            "Cache-Control: no-store\r\n" +
            "\r\n");

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }

    private static string GetReasonPhrase(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.OK => "OK",
            HttpStatusCode.BadRequest => "Bad Request",
            HttpStatusCode.Unauthorized => "Unauthorized",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.RequestEntityTooLarge => "Payload Too Large",
            HttpStatusCode.UnsupportedMediaType => "Unsupported Media Type",
            HttpStatusCode.InternalServerError => "Internal Server Error",
            HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
            _ => statusCode.ToString()
        };
    }

    private sealed record SimpleHttpRequest(
        string Method,
        string Target,
        IReadOnlyDictionary<string, string> Headers,
        ReadOnlyMemory<byte> Body,
        bool BodyTooLarge);

    private sealed class BufferedBodyReader
    {
        private readonly NetworkStream _stream;
        private readonly byte[] _buffer;
        private readonly byte[] _singleByteBuffer = new byte[1];
        private int _offset;

        public BufferedBodyReader(NetworkStream stream, byte[] buffer)
        {
            _stream = stream;
            _buffer = buffer;
        }

        public async Task<string> ReadLineAsync(int maxLength, CancellationToken cancellationToken)
        {
            using var line = new MemoryStream();

            while (true)
            {
                var value = await ReadByteAsync(cancellationToken);
                if (value < 0)
                {
                    throw new EndOfStreamException("Unexpected end of chunked body.");
                }

                if (line.Length >= maxLength)
                {
                    throw new InvalidDataException("Chunk line is too long.");
                }

                if (value == '\n')
                {
                    var bytes = line.ToArray();
                    var length = bytes.Length > 0 && bytes[^1] == '\r'
                        ? bytes.Length - 1
                        : bytes.Length;
                    return Encoding.ASCII.GetString(bytes, 0, length);
                }

                line.WriteByte((byte)value);
            }
        }

        public async Task ReadExactAsync(byte[] destination, CancellationToken cancellationToken)
        {
            var copied = 0;
            while (copied < destination.Length)
            {
                var value = await ReadByteAsync(cancellationToken);
                if (value < 0)
                {
                    throw new EndOfStreamException("Unexpected end of HTTP body.");
                }

                destination[copied++] = (byte)value;
            }
        }

        private async Task<int> ReadByteAsync(CancellationToken cancellationToken)
        {
            if (_offset < _buffer.Length)
            {
                return _buffer[_offset++];
            }

            var bytesRead = await _stream.ReadAsync(_singleByteBuffer, cancellationToken);
            return bytesRead == 0 ? -1 : _singleByteBuffer[0];
        }
    }
}
