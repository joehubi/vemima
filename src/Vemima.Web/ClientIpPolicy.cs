using System.Net;
using System.Net.Sockets;

namespace Vemima.Web;

/// <summary>Whitelist der erlaubten Geräte (IPv4). Es gibt keinen Login.</summary>
public sealed class ClientIpPolicy
{
    private readonly HashSet<IPAddress> allowed;

    public ClientIpPolicy(IEnumerable<string> allowedAddresses)
    {
        allowed = allowedAddresses
            .Select(address => IPAddress.TryParse(address, out var ip) ? ip : null)
            .Where(ip => ip is { AddressFamily: AddressFamily.InterNetwork })
            .Select(ip => ip!)
            .ToHashSet();
    }

    public bool IsAllowed(IPAddress? remoteAddress)
    {
        if (remoteAddress is null)
        {
            return false;
        }

        if (remoteAddress.IsIPv4MappedToIPv6)
        {
            remoteAddress = remoteAddress.MapToIPv4();
        }

        return allowed.Contains(remoteAddress);
    }
}
