namespace CivLan.Application.Dtos;

public sealed record RoomSummaryDto(
    string Code,
    string Name,
    string Status,
    int PlayerCount,
    int MaxPlayers,
    string? HostName,
    string? HostVirtualIp,
    DateTime CreatedAt);

public sealed record PeerDto(
    Guid Id,
    string DisplayName,
    string VirtualIp,
    bool IsHost,
    bool IsOnline,
    DateTime JoinedAt);

public sealed record RoomDetailDto(
    string Code,
    string Name,
    string Status,
    string? HostVirtualIp,
    IReadOnlyList<PeerDto> Peers);

public sealed record WireGuardClientConfigDto(
    string ConfigText,
    string VirtualIp,
    string RoomCode,
    string? HostVirtualIp,
    string Instructions);

public sealed record CreateRoomResultDto(
    RoomDetailDto Room,
    WireGuardClientConfigDto WireGuard,
    string AccessToken);

public sealed record JoinRoomResultDto(
    RoomDetailDto Room,
    WireGuardClientConfigDto WireGuard,
    string AccessToken);
