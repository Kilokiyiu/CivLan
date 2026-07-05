using CivLan.Domain.Enums;
using CivLan.Domain.Exceptions;
using CivLan.Domain.ValueObjects;

namespace CivLan.Domain.Entities;

public sealed class Room
{
    public const int MaxPlayers = 4;

    public Guid Id { get; private set; }
    public RoomCode Code { get; private set; } = RoomCode.Create("AAAAAA");
    public string Name { get; private set; } = string.Empty;
    public RoomStatus Status { get; private set; }
    public Guid? HostPeerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    private readonly List<Peer> _peers = new();
    public IReadOnlyList<Peer> Peers => _peers.AsReadOnly();

    private Room()
    {
    }

    public static Room Create(RoomCode code, string name, Peer creator)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Room name is required.");

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name.Trim(),
            Status = RoomStatus.Open,
            HostPeerId = creator.Id,
            CreatedAt = DateTime.UtcNow
        };

        room._peers.Add(creator);
        return room;
    }

    public Peer AddPeer(string displayName, WireGuardKeyPair keyPair, VirtualIpAddress virtualIp, string accessToken)
    {
        EnsureOpen();
        if (_peers.Count >= MaxPlayers)
            throw new DomainException($"Room is full (max {MaxPlayers} players).");

        if (_peers.Any(p => p.DisplayName.Equals(displayName.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException("Player name already exists in this room.");

        var peer = Peer.Create(displayName, keyPair, virtualIp, accessToken);
        _peers.Add(peer);
        return peer;
    }

    public Peer? FindPeer(Guid peerId) => _peers.FirstOrDefault(p => p.Id == peerId);

    public Peer? FindPeerByToken(string token) =>
        _peers.FirstOrDefault(p => p.MatchesToken(token));

    public void SetHost(Guid peerId)
    {
        EnsureOpen();
        if (_peers.All(p => p.Id != peerId))
            throw new DomainException("Host must be a member of the room.");

        HostPeerId = peerId;
    }

    public void StartGame()
    {
        EnsureOpen();
        if (HostPeerId is null)
            throw new DomainException("Room has no host.");

        Status = RoomStatus.InGame;
    }

    public void Close()
    {
        if (Status == RoomStatus.Closed)
            return;

        Status = RoomStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        foreach (var peer in _peers)
            peer.MarkOffline();
    }

    public Peer RemovePeer(Guid peerId)
    {
        var peer = FindPeer(peerId) ?? throw new DomainException("Peer not found.");
        _peers.Remove(peer);

        if (HostPeerId == peerId)
            HostPeerId = _peers.FirstOrDefault()?.Id;

        if (_peers.Count == 0)
            Close();

        return peer;
    }

    public VirtualIpAddress? GetHostVirtualIp()
    {
        if (HostPeerId is null)
            return null;

        return FindPeer(HostPeerId.Value)?.VirtualIp;
    }

    private void EnsureOpen()
    {
        if (Status == RoomStatus.Closed)
            throw new DomainException("Room is closed.");
    }
}
