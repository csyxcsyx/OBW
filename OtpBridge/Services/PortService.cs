using System.Net;
using System.Net.Sockets;

namespace OtpBridge.Services;

public static class PortService
{
    private const int MaxPort = 65535;
    private const int SequentialSearchCount = 100;

    public static bool IsAvailable(int port)
    {
        if (port is < 1 or > MaxPort)
        {
            return false;
        }

        try
        {
            using var listener = new TcpListener(IPAddress.Any, port);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static int FindAvailablePort(int preferredPort)
    {
        var start = Math.Clamp(preferredPort + 1, 1, MaxPort);
        var end = Math.Min(MaxPort, start + SequentialSearchCount);

        for (var port = start; port <= end; port++)
        {
            if (IsAvailable(port))
            {
                return port;
            }
        }

        return GetEphemeralPort();
    }

    public static bool LooksLikeAddressInUse(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketException &&
                socketException.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                return true;
            }

            if (current.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetEphemeralPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
