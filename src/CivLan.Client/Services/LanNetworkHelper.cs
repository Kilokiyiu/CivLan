using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace CivLan.Client.Services;

/// <summary>
/// Helps Civilization VI LAN discovery over WireGuard by preferring the tunnel
/// interface and opening the UDP ports Civ VI uses for room discovery.
/// </summary>
public static class LanNetworkHelper
{
    public static void ConfigureForCivViLan(IProgress<string>? progress = null)
    {
        try
        {
            PreferWireGuardAdapter(progress);
            EnsureFirewallRules(progress);
        }
        catch (Exception ex)
        {
            progress?.Report($"局域网优化未完全成功（可忽略，先试游戏）：{ex.Message}");
        }
    }

    private static void PreferWireGuardAdapter(IProgress<string>? progress)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            var name = nic.Name + " " + nic.Description;
            if (!name.Contains("WireGuard", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("CivLan", StringComparison.OrdinalIgnoreCase))
                continue;

            // Lower metric so limited broadcasts and LAN traffic prefer the tunnel.
            RunHidden("netsh", $"interface ipv4 set interface \"{nic.Name}\" metric=1");
            progress?.Report($"已优先使用 VPN 网卡：{nic.Name}");
            return;
        }
    }

    private static void EnsureFirewallRules(IProgress<string>? progress)
    {
        // Civ VI LAN discovery: UDP 62900-62999; join: UDP 62056
        EnsureRule("CivLan Civ6 LAN Discovery In", "in", "62900-62999");
        EnsureRule("CivLan Civ6 LAN Discovery Out", "out", "62900-62999");
        EnsureRule("CivLan Civ6 LAN Join In", "in", "62056");
        EnsureRule("CivLan Civ6 LAN Join Out", "out", "62056");
        progress?.Report("已放行文明6局域网 UDP 端口。");
    }

    private static void EnsureRule(string name, string dir, string ports)
    {
        // Delete then recreate to keep idempotent.
        RunHidden("netsh", $"advfirewall firewall delete rule name=\"{name}\"");
        RunHidden(
            "netsh",
            $"advfirewall firewall add rule name=\"{name}\" dir={dir} action=allow protocol=UDP localport={ports}");
    }

    private static void RunHidden(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        try
        {
            process.Start();
            process.WaitForExit(8000);
        }
        catch
        {
            // Best-effort; VPN may still work without these tweaks.
        }
    }
}
