# WireGuard 内置组件

将以下文件放入此目录，编译后会自动复制到输出目录，**玩家无需联网下载**。

## 需要的文件

| 文件 | 大小参考 | 作用 |
|------|----------|------|
| `wireguard.exe` | ~9 MB | 创建隧道服务（必需） |
| `wg.exe` | ~138 KB | 命令行工具（可选） |
| `wintun.dll` | — | 同上目录复制（可选，驱动装好后系统里有） |
| **`wireguard-amd64.msi`** | **~2 MB** | **离线驱动安装包（新电脑必需）** |

## 不要只靠 85KB 的 wireguard-installer.exe

官方 `wireguard-installer.exe`（约 85 KB）是在线安装器，会先联网下载 MSI。国内网络常失败，出现 “Download Error”。

**Release 包必须包含 `wireguard-amd64.msi`**，客户端会优先用它静默安装驱动。

### 获取 MSI

- 浏览器打开 https://www.wireguard.com/install/ → Browse MSIs → 下载 `wireguard-amd64-1.1.msi`
- 直链：`https://download.wireguard.com/windows-client/wireguard-amd64-1.1.msi`
- 或在 VPS：`wget https://download.wireguard.com/windows-client/wireguard-amd64-1.1.msi -O wireguard-amd64.msi`

### 复制 wireguard.exe / wg.exe（开发机已装 WireGuard 时）

```powershell
Copy-Item "C:\Program Files\WireGuard\wireguard.exe","C:\Program Files\WireGuard\wg.exe" `
  -Destination "G:\3.Projects_Indpnd\CivLan\src\CivLan.Client\wireguard\"
```

## 发布客户端

```powershell
cd G:\3.Projects_Indpnd\CivLan
.\scripts\publish-client-release.ps1 -Version 1.0.1
```

将 `client-release` 整包 zip 上传 GitHub Release。**不要只发源码仓库。**

## 首次连接说明

- 若本机 **从未装过 WireGuard 驱动**，会静默运行 `wireguard-amd64.msi`（需管理员权限，仅一次）
- 之后直接使用内置 `wireguard.exe` 连接，**不再下载**
