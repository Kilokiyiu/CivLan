using CivLan.Application.Abstractions;
using NSec.Cryptography;

namespace CivLan.Infrastructure.WireGuard;

public sealed class WireGuardKeyGenerator : IWireGuardKeyGenerator
{
    public Task<(string PrivateKey, string PublicKey)> GenerateKeyPairAsync(CancellationToken cancellationToken = default)
    {
        var algorithm = KeyAgreementAlgorithm.X25519;
        using var key = Key.Create(algorithm, new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextExport
        });

        var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey).ToArray();
        ClampPrivateKey(privateKeyBytes);

        using var clampedKey = Key.Import(algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
        var publicKeyBytes = clampedKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        return Task.FromResult((
            Convert.ToBase64String(privateKeyBytes),
            Convert.ToBase64String(publicKeyBytes)));
    }

    public static string GetPublicKey(string privateKeyBase64)
    {
        var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
        ClampPrivateKey(privateKeyBytes);

        using var key = Key.Import(KeyAgreementAlgorithm.X25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
        return Convert.ToBase64String(key.PublicKey.Export(KeyBlobFormat.RawPublicKey));
    }

    private static void ClampPrivateKey(Span<byte> privateKey)
    {
        privateKey[0] &= 248;
        privateKey[^1] &= 127;
        privateKey[^1] |= 64;
    }
}

public sealed class AccessTokenGenerator : IAccessTokenGenerator
{
    public string Generate() => Convert.ToBase64String(Guid.NewGuid().ToByteArray())
        .TrimEnd('=')
        .Replace('+', 'x')
        .Replace('/', 'y');
}
