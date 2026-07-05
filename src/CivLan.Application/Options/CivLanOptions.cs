namespace CivLan.Application.Options;

public sealed class CivLanOptions
{
    public const string SectionName = "CivLan";

    public string DataDirectory { get; set; } = "./data";
    public string ServerApiKey { get; set; } = "change-me";
}

public sealed class WireGuardOptions
{
    public const string SectionName = "WireGuard";

    public string InterfaceName { get; set; } = "wg0";
    public int ListenPort { get; set; } = 51820;
    public string NetworkPrefix { get; set; } = "10.0.0";
    public int ServerHostOctet { get; set; } = 1;
    public string EndpointPublicHost { get; set; } = "127.0.0.1";
    public string EgressInterface { get; set; } = "eth0";
    public string ConfigDirectory { get; set; } = "/etc/wireguard";
    public string ServerPrivateKey { get; set; } = string.Empty;
    public bool ApplyOnChange { get; set; } = true;
    public bool UseSudo { get; set; } = true;
}
