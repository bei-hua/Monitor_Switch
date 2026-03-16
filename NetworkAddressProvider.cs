using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MonitorSwitcher;

internal static class NetworkAddressProvider
{
    public static IReadOnlyList<string> GetLanAddresses()
    {
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "127.0.0.1"
        };

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(unicastAddress.Address))
                {
                    continue;
                }

                addresses.Add(unicastAddress.Address.ToString());
            }
        }

        return addresses.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
