# CivLan 客户端 — 使用说明

## 安装

1. 解压整个文件夹（不要只复制 exe）
2. 建议放在**英文路径**，例如 `D:\Games\CivLan\`
3. 双击 `CivLan.Client.exe` 运行

## 首次连接 VPN

- 若电脑**从未装过 WireGuard 驱动**，点击「连接 VPN」时会弹出 **UAC 管理员授权**（仅一次）
- 软件会使用内置 `wireguard\wireguard-amd64.msi` 自动安装驱动，**无需再去官网下载 WireGuard**
- 安装完成后自动连接；以后不再重复安装

## 联机步骤（文明6 局域网）

1. 填写服主提供的 **服务器地址** 和 **API Key**
2. 主机：**创建房间** → **连接 VPN**
3. 队友：**加入房间** → **连接 VPN**
4. 文明6 → **多人游戏** → **局域网**
5. 主机：**创建游戏**；队友：点击刷新，等待房间出现

> 必须使用 **v1.0.4+**，升级后请 **断开 VPN 再重连** 一次。
>
> 服主必须在 VPS 执行：`sudo bash deploy/enable-civ6-lan.sh`（安装 UDP 广播中继）。
> 仅改客户端、不装中继，局域网列表仍会为空。

## 目录结构（勿删改）

```
CivLan.Client.exe
wireguard/
  wireguard.exe
  wg.exe
  wireguard-amd64.msi   ← 离线驱动，必需
```

## 常见问题

| 问题 | 处理 |
|------|------|
| Download Error | 请下载含 MSI 的新版 Release，或手动双击 `wireguard\*.msi` 装驱动 |
| VPN 连接失败 | 确认点了 UAC「是」；路径不要有中文 |
| 找不到服务器 | 检查服务器地址、API Key、防火墙 |
| 局域网刷不出房间 | 确认双方已重连 VPN；服主在 VPS 运行 `enable-civ6-lan.sh`；`systemctl status civlan-lan-relay` 为 running |
