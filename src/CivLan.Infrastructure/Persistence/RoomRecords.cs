namespace CivLan.Infrastructure.Persistence;

public sealed class RoomRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? HostPeerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<PeerRecord> Peers { get; set; } = new();
}

public sealed class PeerRecord
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string VirtualIp { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public bool IsOnline { get; set; }
}

public sealed class RoomStore
{
    public List<RoomRecord> Rooms { get; set; } = new();
}

public sealed class ServerSecrets
{
    public string ServerPrivateKey { get; set; } = string.Empty;
    public string ServerPublicKey { get; set; } = string.Empty;
}
