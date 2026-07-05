using CivLan.Domain.Exceptions;

namespace CivLan.Domain.ValueObjects;

public sealed record WireGuardKey
{
    public string Value { get; }

    private WireGuardKey(string value) => Value = value;

    public static WireGuardKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("WireGuard key is required.");

        var trimmed = value.Trim();
        if (trimmed.Length < 40)
            throw new DomainException("WireGuard key format is invalid.");

        return new WireGuardKey(trimmed);
    }

    public override string ToString() => Value;
}

public sealed record WireGuardKeyPair
{
    public WireGuardKey PrivateKey { get; }
    public WireGuardKey PublicKey { get; }

    public WireGuardKeyPair(WireGuardKey privateKey, WireGuardKey publicKey)
    {
        PrivateKey = privateKey;
        PublicKey = publicKey;
    }
}
