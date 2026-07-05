using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CivLan.Client.Services;

public sealed class CivLanApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public CivLanApiClient(string baseUrl, string apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<CreateRoomResponse> CreateRoomAsync(string roomName, string playerName)
    {
        var response = await _httpClient.PostAsJsonAsync("api/rooms", new { roomName, playerName });
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<CreateRoomResponse>(JsonOptions))!;
    }

    public async Task<JoinRoomResponse> JoinRoomAsync(string roomCode, string playerName)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/rooms/{roomCode}/join", new { playerName });
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<JoinRoomResponse>(JsonOptions))!;
    }

    public async Task<RoomDetailResponse> GetRoomAsync(string roomCode)
    {
        var response = await _httpClient.GetAsync($"api/rooms/{roomCode}");
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<RoomDetailResponse>(JsonOptions))!;
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static async Task SaveWireGuardConfigAsync(string configText, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(filePath, configText, Encoding.UTF8);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        ApiError? error = null;
        try
        {
            error = JsonSerializer.Deserialize<ApiError>(body, JsonOptions);
        }
        catch
        {
            // ignore parse errors
        }

        throw new InvalidOperationException(error?.Error ?? $"Request failed: {(int)response.StatusCode} {response.ReasonPhrase}");
    }
}

public sealed class ApiError
{
    public string Error { get; set; } = string.Empty;
}

public sealed class CreateRoomResponse
{
    public RoomDetailResponse Room { get; set; } = new();
    public WireGuardConfigResponse WireGuard { get; set; } = new();
    public string AccessToken { get; set; } = string.Empty;
}

public sealed class JoinRoomResponse
{
    public RoomDetailResponse Room { get; set; } = new();
    public WireGuardConfigResponse WireGuard { get; set; } = new();
    public string AccessToken { get; set; } = string.Empty;
}

public sealed class RoomDetailResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? HostVirtualIp { get; set; }
    public List<PeerResponse> Peers { get; set; } = new();
}

public sealed class PeerResponse
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string VirtualIp { get; set; } = string.Empty;
    public bool IsHost { get; set; }
    public bool IsOnline { get; set; }
}

public sealed class WireGuardConfigResponse
{
    public string ConfigText { get; set; } = string.Empty;
    public string VirtualIp { get; set; } = string.Empty;
    public string RoomCode { get; set; } = string.Empty;
    public string? HostVirtualIp { get; set; }
    public string Instructions { get; set; } = string.Empty;
}
