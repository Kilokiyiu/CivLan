using CivLan.Domain.Exceptions;

namespace CivLan.Domain.ValueObjects;

public sealed record VirtualIpAddress
{
    public string Value { get; }

    private VirtualIpAddress(string value) => Value = value;

    public static VirtualIpAddress Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Virtual IP is required.");

        var parts = value.Trim().Split('.');
        if (parts.Length != 4 || !parts.All(p => byte.TryParse(p, out _)))
            throw new DomainException($"Invalid virtual IP: {value}");

        return new VirtualIpAddress(value.Trim());
    }

    public static VirtualIpAddress FromOctet(string prefix, int lastOctet)
    {
        if (lastOctet is < 1 or > 254)
            throw new DomainException($"Invalid host octet: {lastOctet}");

        return Create($"{prefix}.{lastOctet}");
    }

    public override string ToString() => Value;
}
