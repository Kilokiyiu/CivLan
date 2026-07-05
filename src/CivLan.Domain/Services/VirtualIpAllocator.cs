using CivLan.Domain.ValueObjects;

namespace CivLan.Domain.Services;

public interface IVirtualIpAllocator
{
    VirtualIpAddress Allocate(IEnumerable<VirtualIpAddress> usedAddresses);
}

public sealed class VirtualIpAllocator : IVirtualIpAllocator
{
    private readonly string _networkPrefix;
    private readonly int _firstHostOctet;

    public VirtualIpAllocator(string networkPrefix, int firstHostOctet = 2)
    {
        _networkPrefix = networkPrefix.Trim();
        _firstHostOctet = firstHostOctet;
    }

    public VirtualIpAddress Allocate(IEnumerable<VirtualIpAddress> usedAddresses)
    {
        var usedOctets = usedAddresses
            .Select(ip =>
            {
                var parts = ip.Value.Split('.');
                return int.Parse(parts[^1]);
            })
            .ToHashSet();

        for (var octet = _firstHostOctet; octet <= 254; octet++)
        {
            if (!usedOctets.Contains(octet))
                return VirtualIpAddress.FromOctet(_networkPrefix, octet);
        }

        throw new InvalidOperationException("No virtual IP addresses available.");
    }
}
