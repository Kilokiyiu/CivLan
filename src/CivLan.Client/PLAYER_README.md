# CivLan 客户端 — 使用说明

## 安装

1. 解压整个文件夹（不要只复制 exe）
2. 建议放在**英文路径**，例如 `D:\Games\CivLan\`
3. 双击 `CivLan.Client.exe` 运行

## 首次连接 VPN

- 若电脑**从未装过 WireGuard 驱动**，点击「连接 VPN」时会弹出 **UAC 管理员授权**（仅一次）
- 软件会使用内置 `wireguard\wireguard-amd64.msi` 自动安装驱动，**无需再去官网下载 WireGuard**
- 安装完成后自动连接；以后不再重复安装

## 联机步骤

1. 填写服主提供的 **服务器地址** 和 **API Key**
2. 主机：**创建房间** → **连接 VPN**
3. 队友：**加入房间**（6 位房间码）→ **连接 VPN**
4. 文明6：主机开 **局域网** 游戏，其他人 **IP 直连** 主机虚拟 IP（客户端里可复制）

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
