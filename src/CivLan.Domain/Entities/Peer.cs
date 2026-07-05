using CivLan.Domain.Exceptions;
using CivLan.Domain.ValueObjects;

namespace CivLan.Domain.Entities;

public sealed class Peer
{
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public WireGuardKey PublicKey { get; private set; } = WireGuardKey.Create("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
    public WireGuardKey PrivateKey { get; private set; } = WireGuardKey.Create("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
    public VirtualIpAddress VirtualIp { get; private set; } = VirtualIpAddress.Create("10.0.0.2");
    public string AccessToken { get; private set; } = string.Empty;
    public DateTime JoinedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public bool IsOnline { get; private set; }

    private Peer()
    {
    }

    public static Peer Create(
        string displayName,
        WireGuardKeyPair keyPair,
        VirtualIpAddress virtualIp,
        string accessToken)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Player name is required.");

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new DomainException("Access token is required.");

        return new Peer
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            PublicKey = keyPair.PublicKey,
            PrivateKey = keyPair.PrivateKey,
            VirtualIp = virtualIp,
            AccessToken = accessToken,
            JoinedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            IsOnline = true
        };
    }

    public void Touch()
    {
        LastSeenAt = DateTime.UtcNow;
        IsOnline = true;
    }

    public bool IsStale(TimeSpan inactiveFor) =>
        DateTime.UtcNow - LastSeenAt > inactiveFor;

    public void MarkOnline() => IsOnline = true;

    public void MarkOffline() => IsOnline = false;

    public bool MatchesToken(string token) =>
        !string.IsNullOrWhiteSpace(token) &&
        string.Equals(AccessToken, token.Trim(), StringComparison.Ordinal);
}
