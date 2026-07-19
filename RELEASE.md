# GitHub Release 发布清单 (v1.0.4)

## 局域网联机重要说明

文明6 局域网列表依赖 UDP 广播 (`255.255.255.255:62900-62999`)。
WireGuard **不会**自动在玩家之间转发广播，因此必须：

1. **客户端 v1.0.4+**（AllowedIPs 包含 `255.255.255.255/32`）
2. **VPS 安装 UDP 中继**：`sudo bash deploy/enable-civ6-lan.sh`

## 一、更新服务器

```powershell
cd G:\3.Projects_Indpnd\CivLan
git pull
dotnet publish src/CivLan.Server -c Release -r linux-x64 --self-contained -o publish-linux
```

上传 `publish-linux/*` 到 `/opt/CivLan/CivLan.Server/`，并上传整个 `deploy/` 目录到 VPS（例如 `/opt/CivLan/deploy/`）。

在 VPS：

```bash
sudo systemctl restart civlan
sudo bash /opt/CivLan/deploy/enable-civ6-lan.sh
sudo systemctl status civlan-lan-relay
```

应看到 `civlan-lan-relay` 为 `active (running)`。

## 二、打包客户端

```powershell
.\scripts\publish-client-release.ps1 -Version 1.0.4
```

上传 `CivLan.Client-v1.0.4-win-x64.zip` 到 GitHub Release。

## 三、测试步骤

1. 双方更新到 v1.0.4，断开并重连 VPN
2. 双方 `ping` 对方虚拟 IP 应能通
3. 主机：文明6 → 局域网 → 创建游戏
4. 队友：文明6 → 局域网 → 刷新
5. 若仍为空，在 VPS 看中继日志：`sudo journalctl -u civlan-lan-relay -f`
