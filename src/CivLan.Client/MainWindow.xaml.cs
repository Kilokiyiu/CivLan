using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CivLan.Client.Services;
using Microsoft.Win32;

namespace CivLan.Client;

public partial class MainWindow : Window
{
    private CivLanApiClient? _client;
    private readonly WireGuardTunnelManager _tunnelManager = new();
    private DispatcherTimer? _heartbeatTimer;
    private bool _isClosing;
    private string? _currentRoomCode;
    private string? _currentAccessToken;
    private string? _currentHostIp;
    private string? _currentConfigText;

    public MainWindow()
    {
        InitializeComponent();
        WireGuardConfigTextBox.Text =
            "1. 填写服务器地址和 API Key\n" +
            "2. 创建或加入房间\n" +
            "3. 点击「连接 VPN」（内置 WireGuard，无需单独安装）\n" +
            "4. 文明6 → 多人 → 局域网：主机创建游戏，队友刷新列表加入";

        Loaded += (_, _) => UpdateVpnStatus();
        Closing += MainWindow_Closing;
    }

    private CivLanApiClient GetClient()
    {
        var baseUrl = ServerUrlTextBox.Text.Trim();
        var apiKey = ApiKeyTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("请填写服务器地址。");

        _client = new CivLanApiClient(baseUrl, apiKey);
        return _client;
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            var ok = await GetClient().HealthCheckAsync();
            MessageBox.Show(ok ? "服务器连接成功。" : "无法连接服务器。", "CivLan", MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void CreateRoomButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            var roomName = CreateRoomNameTextBox.Text.Trim();
            var playerName = CreatePlayerNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(playerName))
                throw new InvalidOperationException("请填写昵称。");

            await LeaveCurrentRoomIfAnyAsync();

            var result = await GetClient().CreateRoomAsync(roomName, playerName);
            ApplySession(result.Room.Code, result.AccessToken, result.WireGuard);

            var connectNow = MessageBox.Show(
                $"房间已创建，房间码：{result.Room.Code}\n\n是否立即连接 VPN？",
                "CivLan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (connectNow == MessageBoxResult.Yes)
                await ConnectVpnInternalAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void JoinRoomButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            var roomCode = JoinRoomCodeTextBox.Text.Trim().ToUpperInvariant();
            var playerName = JoinPlayerNameTextBox.Text.Trim();

            if (roomCode.Length != 6)
                throw new InvalidOperationException("房间码必须是 6 位。");

            if (string.IsNullOrWhiteSpace(playerName))
                throw new InvalidOperationException("请填写昵称。");

            var rejoinToken = string.Equals(_currentRoomCode, roomCode, StringComparison.OrdinalIgnoreCase)
                ? _currentAccessToken
                : null;

            if (rejoinToken is null)
                await LeaveCurrentRoomIfAnyAsync();

            var result = await GetClient().JoinRoomAsync(roomCode, playerName, rejoinToken);
            ApplySession(result.Room.Code, result.AccessToken, result.WireGuard);

            var connectNow = MessageBox.Show(
                $"已加入房间 {result.Room.Code}\n\n是否立即连接 VPN？",
                "CivLan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (connectNow == MessageBoxResult.Yes)
                await ConnectVpnInternalAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ConnectVpnButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true);
            await ConnectVpnInternalAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void DisconnectVpnButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentRoomCode))
        {
            MessageBox.Show("当前没有已加入的房间。", "CivLan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SetBusy(true);
            StatusMessageTextBlock.Text = "正在断开 VPN...";
            await _tunnelManager.DisconnectAsync(_currentRoomCode);
            UpdateVpnStatus();
            StatusMessageTextBlock.Text = "VPN 已断开。";
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RefreshRoomButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentRoomCode))
        {
            MessageBox.Show("请先创建或加入房间。", "CivLan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SetBusy(true);
            var room = await GetClient().GetRoomAsync(_currentRoomCode);
            RoomCodeTextBlock.Text = room.Code;
            HostIpTextBlock.Text = room.HostVirtualIp ?? "-";
            _currentHostIp = room.HostVirtualIp;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void CopyHostIpButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentHostIp))
        {
            MessageBox.Show("当前没有主机 IP。", "CivLan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Clipboard.SetText(_currentHostIp);
        MessageBox.Show($"已复制主机 IP：{_currentHostIp}\n\n在文明6中选择「通过 IP 连接」并粘贴。", "CivLan",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task ConnectVpnInternalAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentRoomCode) || string.IsNullOrWhiteSpace(_currentConfigText))
            throw new InvalidOperationException("请先创建或加入房间。");

        var progress = new Progress<string>(message => StatusMessageTextBlock.Text = message);
        await _tunnelManager.ConnectAsync(
            _currentRoomCode,
            _currentConfigText,
            ServerUrlTextBox.Text.Trim(),
            progress);
        UpdateVpnStatus();
        StatusMessageTextBlock.Text = "VPN 已连接。可以启动文明6 并联机。";
        MessageBox.Show(
            "VPN 连接成功！\n\n" +
            "局域网联机步骤：\n" +
            "1. 主机：文明6 → 多人 → 局域网 → 创建游戏\n" +
            "2. 队友：文明6 → 多人 → 局域网 → 点击刷新\n\n" +
            "若列表仍为空：双方断开并重连 VPN，确认服主已在 VPS 运行 enable-civ6-lan.sh。",
            "CivLan",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void ManualInstallButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "WireGuard 离线安装包 (*.msi)|*.msi|WireGuard 安装包 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            Title = "选择 WireGuard 安装包（推荐 wireguard-amd64.msi）"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            SetBusy(true);
            StatusMessageTextBlock.Text = "正在安装 WireGuard...";
            var progress = new Progress<string>(message => StatusMessageTextBlock.Text = message);
            await _tunnelManager.InstallFromInstallerFileAsync(dialog.FileName, progress);
            MessageBox.Show("WireGuard 组件安装完成。现在可以点击「连接 VPN」。", "CivLan",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplySession(string roomCode, string accessToken, WireGuardConfigResponse wireGuard)
    {
        _currentRoomCode = roomCode;
        _currentAccessToken = accessToken;
        _currentHostIp = wireGuard.HostVirtualIp;
        _currentConfigText = wireGuard.ConfigText;

        RoomCodeTextBlock.Text = roomCode;
        VirtualIpTextBlock.Text = wireGuard.VirtualIp;
        HostIpTextBlock.Text = wireGuard.HostVirtualIp ?? "你是主机";
        WireGuardConfigTextBox.Text = wireGuard.ConfigText;
        JoinRoomCodeTextBox.Text = roomCode;
        UpdateVpnStatus();
        StartHeartbeat();
    }

    private void ClearSession()
    {
        StopHeartbeat();
        _currentRoomCode = null;
        _currentAccessToken = null;
        _currentHostIp = null;
        _currentConfigText = null;
        RoomCodeTextBlock.Text = "-";
        VirtualIpTextBlock.Text = "-";
        HostIpTextBlock.Text = "-";
        UpdateVpnStatus();
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        _heartbeatTimer.Tick -= HeartbeatTimer_Tick;
        _heartbeatTimer.Tick += HeartbeatTimer_Tick;
        if (!_heartbeatTimer.IsEnabled)
            _heartbeatTimer.Start();
    }

    private void StopHeartbeat()
    {
        _heartbeatTimer?.Stop();
    }

    private async void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentRoomCode) || string.IsNullOrWhiteSpace(_currentAccessToken))
            return;

        try
        {
            await GetClient().SendHeartbeatAsync(_currentRoomCode, _currentAccessToken);
        }
        catch
        {
            // Ignore transient heartbeat failures.
        }
    }

    private async Task LeaveCurrentRoomIfAnyAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentRoomCode) || string.IsNullOrWhiteSpace(_currentAccessToken))
            return;

        var roomCode = _currentRoomCode;
        var accessToken = _currentAccessToken;
        ClearSession();

        await _tunnelManager.DisconnectAsync(roomCode, suppressErrors: true);
        await GetClient().LeaveRoomAsync(roomCode, accessToken);
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing)
            return;

        if (string.IsNullOrWhiteSpace(_currentRoomCode) || string.IsNullOrWhiteSpace(_currentAccessToken))
            return;

        e.Cancel = true;
        _isClosing = true;

        try
        {
            await LeaveCurrentRoomIfAnyAsync();
        }
        catch
        {
            // Best effort when exiting.
        }
        finally
        {
            Close();
        }
    }

    private void UpdateVpnStatus()
    {
        var connected = !string.IsNullOrWhiteSpace(_currentRoomCode) &&
                        _tunnelManager.IsConnected(_currentRoomCode);

        if (connected)
        {
            VpnStatusTextBlock.Text = "已连接";
            VpnStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
        }
        else
        {
            VpnStatusTextBlock.Text = "未连接";
            VpnStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
        }
    }

    private void SetBusy(bool isBusy)
    {
        IsEnabled = !isBusy;
        Cursor = isBusy ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show(ex.Message, "CivLan", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
