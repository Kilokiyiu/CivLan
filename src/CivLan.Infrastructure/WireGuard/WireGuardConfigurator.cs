using System.Diagnostics;
using System.Text;
using CivLan.Application.Abstractions;
using CivLan.Application.Options;
using CivLan.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace CivLan.Infrastructure.WireGuard;

public sealed class WireGuardConfigurator : IWireGuardConfigurator
{
    private readonly IRoomRepository _roomRepository;
    private readonly IWireGuardKeyGenerator _keyGenerator;
    private readonly WireGuardOptions _options;
    private readonly CivLanOptions _civLanOptions;
    private readonly string _secretsPath;

    public WireGuardConfigurator(
        IRoomRepository roomRepository,
        IWireGuardKeyGenerator keyGenerator,
        IOptions<WireGuardOptions> options,
        IOptions<CivLanOptions> civLanOptions)
    {
        _roomRepository = roomRepository;
        _keyGenerator = keyGenerator;
        _options = options.Value;
        _civLanOptions = civLanOptions.Value;
        _secretsPath = Path.Combine(_civLanOptions.DataDirectory, "server-secrets.json");

        Directory.CreateDirectory(_civLanOptions.DataDirectory);
    }

    public string ServerPublicKey { get; private set; } = string.Empty;
    public string EndpointHost => _options.EndpointPublicHost;
    public int ListenPort => _options.ListenPort;

    public async Task EnsureServerKeysAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.ServerPrivateKey))
        {
            ServerPublicKey = await DerivePublicKeyAsync(_options.ServerPrivateKey, cancellationToken);
            return;
        }

        if (File.Exists(_secretsPath))
        {
            var json = await File.ReadAllTextAsync(_secretsPath, cancellationToken);
            var secrets = System.Text.Json.JsonSerializer.Deserialize<ServerSecretsFile>(json)
                          ?? throw new InvalidOperationException("Invalid server secrets file.");
            _options.ServerPrivateKey = secrets.ServerPrivateKey;
            ServerPublicKey = secrets.ServerPublicKey;
            return;
        }

        var (privateKey, publicKey) = await _keyGenerator.GenerateKeyPairAsync(cancellationToken);
        _options.ServerPrivateKey = privateKey;
        ServerPublicKey = publicKey;

        var store = new ServerSecretsFile
        {
            ServerPrivateKey = privateKey,
            ServerPublicKey = publicKey
        };

        await File.WriteAllTextAsync(
            _secretsPath,
            System.Text.Json.JsonSerializer.Serialize(store, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    public async Task ApplyRoomConfigurationAsync(CancellationToken cancellationToken = default)
    {
        await EnsureServerKeysAsync(cancellationToken);

        var rooms = await _roomRepository.ListOpenRoomsAsync(cancellationToken);
        var configText = BuildServerConfig(rooms);
        var configPath = GetInterfaceConfigPath();

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await File.WriteAllTextAsync(configPath, configText, cancellationToken);

        if (!_options.ApplyOnChange)
            return;

        if (!OperatingSystem.IsLinux())
            return;

        await ApplyLinuxConfigurationAsync(configPath, cancellationToken);
    }

    public string BuildClientConfig(string peerPrivateKey, string peerVirtualIp, string serverPublicKey)
    {
        var subnet = $"{_options.NetworkPrefix}.0/24";
        var sb = new StringBuilder();
        sb.AppendLine("[Interface]");
        sb.AppendLine($"PrivateKey = {peerPrivateKey}");
        // /24 so Windows treats the tunnel as a LAN segment (required for Civ VI LAN discovery).
        sb.AppendLine($"Address = {peerVirtualIp}/24");
        sb.AppendLine();
        sb.AppendLine("[Peer]");
        sb.AppendLine($"PublicKey = {serverPublicKey}");
        sb.AppendLine($"Endpoint = {EndpointHost}:{ListenPort}");
        sb.AppendLine($"AllowedIPs = {subnet}");
        sb.AppendLine("PersistentKeepalive = 25");
        return sb.ToString();
    }

    private string BuildServerConfig(IReadOnlyList<Domain.Entities.Room> rooms)
    {
        var serverAddress = $"{_options.NetworkPrefix}.{_options.ServerHostOctet}/24";
        var sb = new StringBuilder();
        sb.AppendLine("[Interface]");
        sb.AppendLine($"Address = {serverAddress}");
        sb.AppendLine($"ListenPort = {_options.ListenPort}");
        sb.AppendLine($"PrivateKey = {_options.ServerPrivateKey}");
        var iface = _options.InterfaceName;
        sb.AppendLine(
            $"PostUp = sysctl -w net.ipv4.conf.{iface}.bcast_forward=1; iptables -A FORWARD -i {iface} -j ACCEPT; iptables -A FORWARD -i {iface} -o {iface} -j ACCEPT; iptables -t nat -A POSTROUTING -o {_options.EgressInterface} -j MASQUERADE");
        sb.AppendLine(
            $"PostDown = iptables -D FORWARD -i {iface} -j ACCEPT; iptables -D FORWARD -i {iface} -o {iface} -j ACCEPT; iptables -t nat -D POSTROUTING -o {_options.EgressInterface} -j MASQUERADE");

        foreach (var peer in rooms.SelectMany(r => r.Peers))
        {
            sb.AppendLine();
            sb.AppendLine($"# {peer.DisplayName}");
            sb.AppendLine("[Peer]");
            sb.AppendLine($"PublicKey = {peer.PublicKey.Value}");
            sb.AppendLine($"AllowedIPs = {peer.VirtualIp.Value}/32");
        }

        return sb.ToString();
    }

    private string GetInterfaceConfigPath()
    {
        if (OperatingSystem.IsLinux() && _options.ConfigDirectory.StartsWith("/etc"))
            return Path.Combine(_options.ConfigDirectory, $"{_options.InterfaceName}.conf");

        return Path.Combine(_civLanOptions.DataDirectory, $"{_options.InterfaceName}.conf");
    }

    private async Task ApplyLinuxConfigurationAsync(string configPath, CancellationToken cancellationToken)
    {
        var interfaceName = _options.InterfaceName;
        var prefix = _options.UseSudo ? "sudo " : string.Empty;
        var targetPath = Path.Combine(_options.ConfigDirectory, $"{interfaceName}.conf");

        if (!string.Equals(configPath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(_options.ConfigDirectory);
            if (_options.UseSudo)
                await RunShellAsync($"{prefix}cp \"{configPath}\" \"{targetPath}\"", cancellationToken);
            else
                File.Copy(configPath, targetPath, overwrite: true);
        }

        await RunShellAsync($"{prefix}wg-quick down {interfaceName}", cancellationToken, throwOnError: false);
        await RunShellAsync($"{prefix}wg-quick up \"{targetPath}\"", cancellationToken);
    }

    private Task<string> DerivePublicKeyAsync(string privateKey, CancellationToken cancellationToken)
    {
        if (File.Exists(_secretsPath))
        {
            var json = File.ReadAllText(_secretsPath);
            var secrets = System.Text.Json.JsonSerializer.Deserialize<ServerSecretsFile>(json);
            if (secrets is not null && secrets.ServerPrivateKey == privateKey)
                return Task.FromResult(secrets.ServerPublicKey);
        }

        return Task.FromResult(WireGuardKeyGenerator.GetPublicKey(privateKey));
    }

    private static async Task<string> RunShellAsync(
        string command,
        CancellationToken cancellationToken,
        bool throwOnError = true)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-lc \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (throwOnError && process.ExitCode != 0)
                throw new InvalidOperationException($"Shell command failed: {command}\n{error}");

            return output;
        }
        catch (Exception) when (!throwOnError)
        {
            return string.Empty;
        }
    }

    private sealed class ServerSecretsFile
    {
        public string ServerPrivateKey { get; set; } = string.Empty;
        public string ServerPublicKey { get; set; } = string.Empty;
    }
}
