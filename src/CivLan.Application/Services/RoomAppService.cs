using CivLan.Application.Abstractions;
using CivLan.Application.Dtos;
using CivLan.Domain.Entities;
using CivLan.Domain.Enums;
using CivLan.Domain.Exceptions;
using CivLan.Domain.Repositories;
using CivLan.Domain.Services;
using CivLan.Domain.ValueObjects;

namespace CivLan.Application.Services;

public sealed class RoomAppService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IVirtualIpAllocator _ipAllocator;
    private readonly IWireGuardKeyGenerator _keyGenerator;
    private readonly IWireGuardConfigurator _wireGuardConfigurator;
    private readonly IAccessTokenGenerator _tokenGenerator;
    private readonly Random _random = new();

    public RoomAppService(
        IRoomRepository roomRepository,
        IVirtualIpAllocator ipAllocator,
        IWireGuardKeyGenerator keyGenerator,
        IWireGuardConfigurator wireGuardConfigurator,
        IAccessTokenGenerator tokenGenerator)
    {
        _roomRepository = roomRepository;
        _ipAllocator = ipAllocator;
        _keyGenerator = keyGenerator;
        _wireGuardConfigurator = wireGuardConfigurator;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<CreateRoomResultDto> CreateRoomAsync(string roomName, string playerName, CancellationToken cancellationToken = default)
    {
        await _wireGuardConfigurator.EnsureServerKeysAsync(cancellationToken);

        var code = await GenerateUniqueRoomCodeAsync(cancellationToken);
        var keyPair = await CreateKeyPairAsync(cancellationToken);
        var token = _tokenGenerator.Generate();
        var virtualIp = await AllocateVirtualIpAsync(cancellationToken);
        var creator = CreatePeerForRoom(playerName, keyPair, virtualIp, token);
        var room = Room.Create(code, roomName, creator);

        await _roomRepository.SaveAsync(room, cancellationToken);
        await _wireGuardConfigurator.ApplyRoomConfigurationAsync(cancellationToken);

        return new CreateRoomResultDto(
            MapRoomDetail(room, creator.Id),
            BuildClientConfig(room, creator),
            creator.AccessToken);
    }

    public async Task<JoinRoomResultDto> JoinRoomAsync(
        string roomCode,
        string playerName,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        await _wireGuardConfigurator.EnsureServerKeysAsync(cancellationToken);

        var code = RoomCode.Create(roomCode);
        var room = await _roomRepository.GetByCodeAsync(code, cancellationToken)
                   ?? throw new DomainException("Room not found.");

        room.EvictStalePeers(Room.DefaultStalePeerTimeout);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            var existing = room.FindPeerByToken(accessToken);
            if (existing is not null)
            {
                existing.Touch();
                await _roomRepository.SaveAsync(room, cancellationToken);
                return new JoinRoomResultDto(
                    MapRoomDetail(room, existing.Id),
                    BuildClientConfig(room, existing),
                    existing.AccessToken);
            }
        }

        var keyPair = await CreateKeyPairAsync(cancellationToken);
        var token = _tokenGenerator.Generate();
        var virtualIp = await AllocateVirtualIpAsync(cancellationToken);
        var peer = room.AddPeer(playerName, keyPair, virtualIp, token);

        await _roomRepository.SaveAsync(room, cancellationToken);
        await _wireGuardConfigurator.ApplyRoomConfigurationAsync(cancellationToken);

        return new JoinRoomResultDto(
            MapRoomDetail(room, peer.Id),
            BuildClientConfig(room, peer),
            peer.AccessToken);
    }

    public async Task<RoomDetailDto> GetRoomAsync(string roomCode, CancellationToken cancellationToken = default)
    {
        var code = RoomCode.Create(roomCode);
        var room = await _roomRepository.GetByCodeAsync(code, cancellationToken)
                   ?? throw new DomainException("Room not found.");

        room.EvictStalePeers(Room.DefaultStalePeerTimeout);
        if (room.Status == RoomStatus.Closed)
        {
            await _roomRepository.DeleteAsync(code, cancellationToken);
            throw new DomainException("Room not found.");
        }

        await _roomRepository.SaveAsync(room, cancellationToken);
        return MapRoomDetail(room, null);
    }

    public async Task<IReadOnlyList<RoomSummaryDto>> ListOpenRoomsAsync(CancellationToken cancellationToken = default)
    {
        var rooms = await _roomRepository.ListOpenRoomsAsync(cancellationToken);
        return rooms
            .Where(r => r.Status != RoomStatus.Closed)
            .Select(MapRoomSummary)
            .ToList();
    }

    public async Task<WireGuardClientConfigDto> GetClientConfigAsync(
        string roomCode,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var code = RoomCode.Create(roomCode);
        var room = await _roomRepository.GetByCodeAsync(code, cancellationToken)
                   ?? throw new DomainException("Room not found.");

        var peer = room.FindPeerByToken(accessToken)
                   ?? throw new DomainException("Invalid access token.");

        peer.Touch();
        await _roomRepository.SaveAsync(room, cancellationToken);

        return BuildClientConfig(room, peer);
    }

    public async Task HeartbeatAsync(string roomCode, string accessToken, CancellationToken cancellationToken = default)
    {
        var code = RoomCode.Create(roomCode);
        var room = await _roomRepository.GetByCodeAsync(code, cancellationToken)
                   ?? throw new DomainException("Room not found.");

        var peer = room.FindPeerByToken(accessToken)
                   ?? throw new DomainException("Invalid access token.");

        peer.Touch();
        room.EvictStalePeers(Room.DefaultStalePeerTimeout);
        if (room.Status == RoomStatus.Closed)
        {
            await _roomRepository.DeleteAsync(code, cancellationToken);
            return;
        }

        await _roomRepository.SaveAsync(room, cancellationToken);
    }

    public async Task<RoomDetailDto> SetHostAsync(
        string roomCode,
        string accessToken,
        Guid hostPeerId,
        CancellationToken cancellationToken = default)
    {
        var room = await GetAuthorizedRoomAsync(roomCode, accessToken, cancellationToken);
        room.SetHost(hostPeerId);
        await _roomRepository.SaveAsync(room, cancellationToken);
        return MapRoomDetail(room, null);
    }

    public async Task LeaveRoomAsync(string roomCode, string accessToken, CancellationToken cancellationToken = default)
    {
        var code = RoomCode.Create(roomCode);
        var room = await _roomRepository.GetByCodeAsync(code, cancellationToken)
                   ?? throw new DomainException("Room not found.");

        var peer = room.FindPeerByToken(accessToken)
                   ?? throw new DomainException("Invalid access token.");

        room.RemovePeer(peer.Id);
        if (room.Status == RoomStatus.Closed)
            await _roomRepository.DeleteAsync(code, cancellationToken);
        else
            await _roomRepository.SaveAsync(room, cancellationToken);

        await _wireGuardConfigurator.ApplyRoomConfigurationAsync(cancellationToken);
    }

    public async Task CloseRoomAsync(string roomCode, string accessToken, CancellationToken cancellationToken = default)
    {
        var room = await GetAuthorizedRoomAsync(roomCode, accessToken, cancellationToken);
        if (room.HostPeerId != room.FindPeerByToken(accessToken)?.Id)
            throw new DomainException("Only the host can close the room.");

        room.Close();
        await _roomRepository.DeleteAsync(room.Code, cancellationToken);
        await _wireGuardConfigurator.ApplyRoomConfigurationAsync(cancellationToken);
    }

    private async Task<Room> GetAuthorizedRoomAsync(string roomCode, string accessToken, CancellationToken cancellationToken)
    {
        var code = RoomCode.Create(roomCode);
        var room = await _roomRepository.GetByCodeAsync(code, cancellationToken)
                   ?? throw new DomainException("Room not found.");

        if (room.FindPeerByToken(accessToken) is null)
            throw new DomainException("Invalid access token.");

        return room;
    }

    private async Task<VirtualIpAddress> AllocateVirtualIpAsync(CancellationToken cancellationToken)
    {
        var rooms = await _roomRepository.ListOpenRoomsAsync(cancellationToken);
        var used = rooms.SelectMany(r => r.Peers).Select(p => p.VirtualIp);
        return _ipAllocator.Allocate(used);
    }

    private async Task<RoomCode> GenerateUniqueRoomCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = RoomCode.Generate(_random);
            if (await _roomRepository.GetByCodeAsync(code, cancellationToken) is null)
                return code;
        }

        throw new DomainException("Unable to generate a unique room code.");
    }

    private async Task<WireGuardKeyPair> CreateKeyPairAsync(CancellationToken cancellationToken)
    {
        var (privateKey, publicKey) = await _keyGenerator.GenerateKeyPairAsync(cancellationToken);
        return new WireGuardKeyPair(WireGuardKey.Create(privateKey), WireGuardKey.Create(publicKey));
    }

    private static Peer CreatePeerForRoom(
        string displayName,
        WireGuardKeyPair keyPair,
        VirtualIpAddress virtualIp,
        string accessToken) =>
        Peer.Create(displayName, keyPair, virtualIp, accessToken);

    private WireGuardClientConfigDto BuildClientConfig(Room room, Peer peer)
    {
        var hostIp = room.GetHostVirtualIp()?.Value;
        var config = _wireGuardConfigurator.BuildClientConfig(
            peer.PrivateKey.Value,
            peer.VirtualIp.Value,
            _wireGuardConfigurator.ServerPublicKey);

        var instructions = hostIp is null
            ? "Connect WireGuard first, then create a Civilization VI LAN game."
            : $"Connect WireGuard first. Host IP for Civ VI: {hostIp} (LAN or direct IP join).";

        return new WireGuardClientConfigDto(
            config,
            peer.VirtualIp.Value,
            room.Code.Value,
            hostIp,
            instructions);
    }

    private static RoomDetailDto MapRoomDetail(Room room, Guid? currentPeerId)
    {
        var hostIp = room.GetHostVirtualIp()?.Value;
        var peers = room.Peers.Select(p => new PeerDto(
            p.Id,
            p.DisplayName,
            p.VirtualIp.Value,
            room.HostPeerId == p.Id,
            p.IsOnline,
            p.JoinedAt)).ToList();

        return new RoomDetailDto(
            room.Code.Value,
            room.Name,
            room.Status.ToString(),
            hostIp,
            peers);
    }

    private static RoomSummaryDto MapRoomSummary(Room room)
    {
        var host = room.HostPeerId.HasValue
            ? room.FindPeer(room.HostPeerId.Value)
            : null;

        return new RoomSummaryDto(
            room.Code.Value,
            room.Name,
            room.Status.ToString(),
            room.Peers.Count,
            Room.MaxPlayers,
            host?.DisplayName,
            room.GetHostVirtualIp()?.Value,
            room.CreatedAt);
    }
}
