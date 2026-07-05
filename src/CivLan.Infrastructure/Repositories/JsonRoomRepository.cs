using System.Reflection;
using CivLan.Domain.Entities;
using CivLan.Domain.Enums;
using CivLan.Domain.Repositories;
using CivLan.Domain.ValueObjects;
using CivLan.Infrastructure.Persistence;

namespace CivLan.Infrastructure.Repositories;

public sealed class JsonRoomRepository : IRoomRepository
{
    private readonly string _storePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonRoomRepository(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _storePath = Path.Combine(dataDirectory, "rooms.json");
    }

    public async Task<Room?> GetByCodeAsync(RoomCode code, CancellationToken cancellationToken = default)
    {
        var store = await LoadAsync(cancellationToken);
        var record = store.Rooms.FirstOrDefault(r =>
            string.Equals(r.Code, code.Value, StringComparison.OrdinalIgnoreCase));

        return record is null ? null : MapToDomain(record);
    }

    public async Task<IReadOnlyList<Room>> ListOpenRoomsAsync(CancellationToken cancellationToken = default)
    {
        var store = await LoadAsync(cancellationToken);
        return store.Rooms
            .Where(r => !string.Equals(r.Status, RoomStatus.Closed.ToString(), StringComparison.Ordinal))
            .Select(MapToDomain)
            .ToList();
    }

    public async Task SaveAsync(Room room, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            var record = MapToRecord(room);
            var index = store.Rooms.FindIndex(r => r.Id == room.Id);
            if (index >= 0)
                store.Rooms[index] = record;
            else
                store.Rooms.Add(record);

            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(RoomCode code, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadAsync(cancellationToken);
            store.Rooms.RemoveAll(r =>
                string.Equals(r.Code, code.Value, StringComparison.OrdinalIgnoreCase));
            await SaveStoreAsync(store, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<RoomStore> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_storePath))
            return new RoomStore();

        await using var stream = File.OpenRead(_storePath);
        var store = await System.Text.Json.JsonSerializer.DeserializeAsync<RoomStore>(stream, cancellationToken: cancellationToken);
        return store ?? new RoomStore();
    }

    private async Task SaveStoreAsync(RoomStore store, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_storePath);
        await System.Text.Json.JsonSerializer.SerializeAsync(
            stream,
            store,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true },
            cancellationToken);
    }

    private static Room MapToDomain(RoomRecord record)
    {
        var room = (Room)Activator.CreateInstance(typeof(Room), nonPublic: true)!;
        SetProperty(room, nameof(Room.Id), record.Id);
        SetProperty(room, nameof(Room.Code), RoomCode.Create(record.Code));
        SetProperty(room, nameof(Room.Name), record.Name);
        SetProperty(room, nameof(Room.Status), Enum.Parse<RoomStatus>(record.Status));
        SetProperty(room, nameof(Room.HostPeerId), record.HostPeerId);
        SetProperty(room, nameof(Room.CreatedAt), record.CreatedAt);
        SetProperty(room, nameof(Room.ClosedAt), record.ClosedAt);

        var peersField = typeof(Room).GetField("_peers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var peers = (List<Peer>)peersField.GetValue(room)!;
        peers.Clear();
        peers.AddRange(record.Peers.Select(MapPeerToDomain));

        return room;
    }

    private static Peer MapPeerToDomain(PeerRecord record)
    {
        var peer = (Peer)Activator.CreateInstance(typeof(Peer), nonPublic: true)!;
        SetProperty(peer, nameof(Peer.Id), record.Id);
        SetProperty(peer, nameof(Peer.DisplayName), record.DisplayName);
        SetProperty(peer, nameof(Peer.PublicKey), WireGuardKey.Create(record.PublicKey));
        SetProperty(peer, nameof(Peer.PrivateKey), WireGuardKey.Create(record.PrivateKey));
        SetProperty(peer, nameof(Peer.VirtualIp), VirtualIpAddress.Create(record.VirtualIp));
        SetProperty(peer, nameof(Peer.AccessToken), record.AccessToken);
        SetProperty(peer, nameof(Peer.JoinedAt), record.JoinedAt);
        SetProperty(peer, nameof(Peer.IsOnline), record.IsOnline);
        return peer;
    }

    private static RoomRecord MapToRecord(Room room) => new()
    {
        Id = room.Id,
        Code = room.Code.Value,
        Name = room.Name,
        Status = room.Status.ToString(),
        HostPeerId = room.HostPeerId,
        CreatedAt = room.CreatedAt,
        ClosedAt = room.ClosedAt,
        Peers = room.Peers.Select(p => new PeerRecord
        {
            Id = p.Id,
            DisplayName = p.DisplayName,
            PublicKey = p.PublicKey.Value,
            PrivateKey = p.PrivateKey.Value,
            VirtualIp = p.VirtualIp.Value,
            AccessToken = p.AccessToken,
            JoinedAt = p.JoinedAt,
            IsOnline = p.IsOnline
        }).ToList()
    };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property!.SetValue(target, value);
    }
}
