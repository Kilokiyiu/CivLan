using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

namespace CivLan.Client.Services;

public sealed class WireGuardTunnelManager
{
    private const string WireGuardInstallerUrl = "https://download.wireguard.com/wireguard-installer.exe";
    private const string WireGuardManagerService = "WireGuardManager";
    private static readonly string ProgramFilesExe = @"C:\Program Files\WireGuard\wireguard.exe";
    private static readonly string AppRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CivLan");
    private static readonly string BinDir = Path.Combine(AppRoot, "wireguard");
    private static readonly string TunnelDir = Path.Combine(AppRoot, "tunnels");
    private static readonly string BundledDir = Path.Combine(AppContext.BaseDirectory, "wireguard");

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    public static string GetTunnelName(string roomCode) => $"CivLan-{roomCode.Trim().ToUpperInvariant()}";

    public async Task EnsureToolsAsync(string? serverBaseUrl, IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(BinDir);
        Directory.CreateDirectory(TunnelDir);

        await EnsureWireGuardPlatformAsync(serverBaseUrl, progress);

        if (File.Exists(ResolveWireGuardExePath()))
        {
            progress?.Report(File.Exists(Path.Combine(BundledDir, "wireguard.exe"))
                ? "已使用软件内置 WireGuard 组件。"
                : "WireGuard 组件已就绪。");
            return;
        }

        var bundledInstaller = Path.Combine(BundledDir, "wireguard-installer.exe");
        if (File.Exists(bundledInstaller))
        {
            progress?.Report("正在安装 WireGuard 驱动（仅首次，需管理员权限）...");
            await InstallFromInstallerAsync(bundledInstaller, progress);
            return;
        }

        progress?.Report("正在获取 WireGuard 安装包...");
        var installerPath = await DownloadInstallerAsync(serverBaseUrl, progress);
        await InstallFromInstallerAsync(installerPath, progress);
    }

    public async Task InstallFromInstallerFileAsync(string installerPath, IProgress<string>? progress = null)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("找不到 WireGuard 安装包。", installerPath);

        Directory.CreateDirectory(BinDir);
        var cachedInstaller = Path.Combine(BinDir, "wireguard-installer.exe");
        if (!string.Equals(Path.GetFullPath(installerPath), Path.GetFullPath(cachedInstaller), StringComparison.OrdinalIgnoreCase))
            File.Copy(installerPath, cachedInstaller, overwrite: true);

        await InstallFromInstallerAsync(cachedInstaller, progress);
    }

    public async Task ConnectAsync(
        string roomCode,
        string configText,
        string? serverBaseUrl,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
            throw new InvalidOperationException("请先创建或加入房间。");

        await EnsureToolsAsync(serverBaseUrl, progress);

        var tunnelName = GetTunnelName(roomCode);
        var configPath = Path.Combine(TunnelDir, $"{tunnelName}.conf");
        await File.WriteAllTextAsync(configPath, configText);

        if (IsTunnelServiceRunning(roomCode))
            await DisconnectAsync(roomCode, suppressErrors: true);

        progress?.Report("正在连接 VPN（需要管理员授权）...");
        var wireGuardExe = ResolveWireGuardExePath();
        var exitCode = RunElevated(
            wireGuardExe,
            $"/installtunnelservice \"{configPath}\"",
            Path.GetDirectoryName(wireGuardExe)!);

        if (exitCode != 0)
            throw new InvalidOperationException("VPN 连接失败。请确认已允许管理员权限，并重试。");

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            if (IsTunnelServiceRunning(roomCode))
            {
                progress?.Report("VPN 已连接。");
                return;
            }
        }

        throw new InvalidOperationException("VPN 服务未成功启动。请确认 WireGuard 驱动已安装，然后重试。");
    }

    public Task DisconnectAsync(string roomCode, bool suppressErrors = false)
    {
        if (!IsTunnelServiceInstalled(roomCode))
            return Task.CompletedTask;

        var tunnelName = GetTunnelName(roomCode);
        var wireGuardExe = ResolveWireGuardExePath();

        try
        {
            var exitCode = RunElevated(
                wireGuardExe,
                $"/uninstalltunnelservice \"{tunnelName}\"",
                Path.GetDirectoryName(wireGuardExe)!);

            if (exitCode != 0 && !suppressErrors)
                throw new InvalidOperationException("断开 VPN 失败。");
        }
        catch (Exception) when (suppressErrors)
        {
        }

        return Task.CompletedTask;
    }

    public bool IsConnected(string roomCode) => IsTunnelServiceRunning(roomCode);

    private static string GetServiceName(string roomCode) =>
        $"WireGuardTunnel${GetTunnelName(roomCode)}";

    private string GetWireGuardExePath() => Path.Combine(BinDir, "wireguard.exe");

    private string ResolveWireGuardExePath()
    {
        var candidates = new[]
        {
            Path.Combine(BundledDir, "wireguard.exe"),
            GetWireGuardExePath(),
            ProgramFilesExe
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        throw new InvalidOperationException("找不到 wireguard.exe，请确认 wireguard 文件夹完整。");
    }

    private async Task EnsureWireGuardPlatformAsync(string? serverBaseUrl, IProgress<string>? progress)
    {
        if (IsServiceInstalled(WireGuardManagerService))
            return;

        progress?.Report("首次使用，正在安装 WireGuard 驱动（需管理员权限，仅一次）...");

        var installerCandidates = new[]
        {
            Path.Combine(BundledDir, "wireguard-installer.exe"),
            Path.Combine(BinDir, "wireguard-installer.exe")
        };

        foreach (var installer in installerCandidates)
        {
            if (!File.Exists(installer))
                continue;

            await InstallFromInstallerAsync(installer, progress);
            if (IsServiceInstalled(WireGuardManagerService))
                return;
        }

        try
        {
            var downloaded = await DownloadInstallerAsync(serverBaseUrl, progress);
            await InstallFromInstallerAsync(downloaded, progress);
        }
        catch
        {
            // fall through
        }

        if (!IsServiceInstalled(WireGuardManagerService))
        {
            throw new InvalidOperationException(
                "WireGuard 驱动尚未安装。\n\n" +
                "请在本机双击运行一次 wireguard\\wireguard-installer.exe 完成驱动安装，然后再点「连接 VPN」。");
        }
    }

    private static bool IsServiceInstalled(string serviceName)
    {
        var output = RunScQuery(serviceName);
        return output.Contains("SERVICE_NAME", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTunnelServiceRunning(string roomCode)
    {
        var output = RunScQuery(GetServiceName(roomCode));
        return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTunnelServiceInstalled(string roomCode)
    {
        return IsServiceInstalled(GetServiceName(roomCode));
    }

    private static string RunScQuery(string serviceName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"query \"{serviceName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool TryCopyBundledTools()
    {
        var bundledExe = Path.Combine(BundledDir, "wireguard.exe");
        if (!File.Exists(bundledExe))
            return false;

        foreach (var pattern in new[] { "wireguard.exe", "wintun.dll", "wg.exe" })
        {
            var source = Path.Combine(BundledDir, pattern);
            var destination = Path.Combine(BinDir, pattern);
            if (!File.Exists(source))
                continue;

            if (File.Exists(destination))
                continue;

            try
            {
                File.Copy(source, destination, overwrite: false);
            }
            catch (IOException)
            {
                // AppData copy locked or unavailable; bundled copy is still usable.
            }
        }

        return File.Exists(GetWireGuardExePath()) || File.Exists(bundledExe);
    }

    private async Task<string> DownloadInstallerAsync(string? serverBaseUrl, IProgress<string>? progress)
    {
        var installerPath = Path.Combine(BinDir, "wireguard-installer.exe");
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(serverBaseUrl))
        {
            try
            {
                var mirrorUrl = $"{serverBaseUrl.TrimEnd('/')}/assets/wireguard-installer.exe";
                progress?.Report($"正在从 CivLan 服务器下载...\n{mirrorUrl}");
                await DownloadFileAsync(mirrorUrl, installerPath);
                return installerPath;
            }
            catch (Exception ex)
            {
                errors.Add($"CivLan 服务器: {ex.Message}");
            }
        }

        try
        {
            progress?.Report("正在从 WireGuard 官方下载...");
            await DownloadFileAsync(WireGuardInstallerUrl, installerPath);
            return installerPath;
        }
        catch (Exception ex)
        {
            errors.Add($"官方源: {ex.Message}");
        }

        throw new InvalidOperationException(
            "无法获取 WireGuard 安装包。\n\n" +
            string.Join("\n", errors) +
            "\n\n请使用软件目录 wireguard\\wireguard-installer.exe，或点击「手动选择安装包」。");
    }

    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(destinationPath);
        await stream.CopyToAsync(file);

        if (new FileInfo(destinationPath).Length < 10 * 1024)
            throw new InvalidOperationException("下载的文件大小异常。");
    }

    private async Task InstallFromInstallerAsync(string installerPath, IProgress<string>? progress)
    {
        progress?.Report("正在安装 WireGuard 驱动（请允许管理员权限）...");
        var exitCode = RunElevated(installerPath, "/S", Path.GetDirectoryName(installerPath)!);
        if (exitCode != 0)
            throw new InvalidOperationException("WireGuard 驱动安装失败。");

        for (var i = 0; i < 30; i++)
        {
            if (IsServiceInstalled(WireGuardManagerService) || File.Exists(ProgramFilesExe))
                break;
            await Task.Delay(1000);
        }

        if (File.Exists(ProgramFilesExe))
            CopyWireGuardFromProgramFiles();
        else
            TryCopyBundledTools();

        progress?.Report("WireGuard 驱动已安装。");
    }

    private void CopyWireGuardFromProgramFiles()
    {
        var sourceDir = Path.GetDirectoryName(ProgramFilesExe)!;
        foreach (var pattern in new[] { "wireguard.exe", "wintun.dll", "wg.exe" })
        {
            var source = Path.Combine(sourceDir, pattern);
            var destination = Path.Combine(BinDir, pattern);
            if (!File.Exists(source) || File.Exists(destination))
                continue;

            try
            {
                File.Copy(source, destination, overwrite: false);
            }
            catch (IOException)
            {
                // Prefer Program Files path via ResolveWireGuardExePath.
            }
        }
    }

    private static int RunElevated(string fileName, string arguments, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        try
        {
            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }
}
