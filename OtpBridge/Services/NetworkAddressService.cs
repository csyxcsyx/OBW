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
        return GetUsableNetworkInterfaces()
            .SelectMany(GetAddressCandidates)
            .OrderByDescending(candidate => candidate.HasGateway)
            .ThenByDescending(candidate => candidate.IsPrivate)
            .ThenByDescending(candidate => candidate.Speed)
            .Select(candidate => candidate.Address)
            .DistinctBy(address => address.ToString());
    }

    private static IEnumerable<NetworkInterface> GetUsableNetworkInterfaces()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .Where(item => item.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<(IPAddress Address, bool HasGateway, bool IsPrivate, long Speed)> GetAddressCandidates(
        NetworkInterface networkInterface)
    {
        IPInterfaceProperties properties;
        try
        {
            properties = networkInterface.GetIPProperties();
        }
        catch
        {
            yield break;
        }

        var hasGateway = HasIPv4Gateway(properties);
        var speed = GetInterfaceSpeed(networkInterface);

        foreach (var item in properties.UnicastAddresses)
        {
            var address = item.Address;
            if (address.AddressFamily != AddressFamily.InterNetwork ||
                IPAddress.IsLoopback(address) ||
                IPAddress.Any.Equals(address) ||
                IPAddress.None.Equals(address) ||
                IsLinkLocalIPv4(address))
            {
                continue;
            }

            yield return (address, hasGateway, IsPrivateIPv4(address), speed);
        }
    }

    private static bool HasIPv4Gateway(IPInterfaceProperties properties)
    {
        try
        {
            return properties.GatewayAddresses
                .Any(address => address.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !IPAddress.Any.Equals(address.Address) &&
                                !IPAddress.None.Equals(address.Address));
        }
        catch
        {
            return false;
        }
    }

    private static long GetInterfaceSpeed(NetworkInterface networkInterface)
    {
        try
        {
            return networkInterface.Speed;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsPrivateIPv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 &&
               (bytes[0] == 10 ||
                bytes[0] == 192 && bytes[1] == 168 ||
                bytes[0] == 172 && bytes[1] is >= 16 and <= 31);
    }

    private static bool IsLinkLocalIPv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
    }
}
