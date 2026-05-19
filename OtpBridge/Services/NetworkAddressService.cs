using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OtpBridge.Services;

public static class NetworkAddressService
{
    public static IReadOnlyList<string> GetApiUrls(int port)
    {
        var urls = GetLanIPv4Addresses()
            .Select(address => $"http://{address}:{port}/api/sms")
            .ToList();

        if (urls.Count == 0)
        {
            urls.Add($"http://127.0.0.1:{port}/api/sms");
        }

        return urls;
    }

    private static IEnumerable<IPAddress> GetLanIPv4Addresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up)
            .Where(item => item.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Where(HasIPv4Gateway)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(item => item.Address)
            .Where(address => !IPAddress.IsLoopback(address));
    }

    private static bool HasIPv4Gateway(NetworkInterface networkInterface)
    {
        return networkInterface.GetIPProperties()
            .GatewayAddresses
            .Any(address => address.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.Any.Equals(address.Address) &&
                            !IPAddress.None.Equals(address.Address));
    }
}
