namespace CivLan.Application.Abstractions;

public interface IWireGuardKeyGenerator
{
    Task<(string PrivateKey, string PublicKey)> GenerateKeyPairAsync(CancellationToken cancellationToken = default);
}

public interface IWireGuardConfigurator
{
    Task ApplyRoomConfigurationAsync(CancellationToken cancellationToken = default);
    Task EnsureServerKeysAsync(CancellationToken cancellationToken = default);
    string BuildClientConfig(
        string peerPrivateKey,
        string peerVirtualIp,
        string serverPublicKey);
    string ServerPublicKey { get; }
    string EndpointHost { get; }
    int ListenPort { get; }
}

public interface IAccessTokenGenerator
{
    string Generate();
}
