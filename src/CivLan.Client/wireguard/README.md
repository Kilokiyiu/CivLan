# WireGuard 内置组件

将以下文件放入此目录，编译后会自动复制到输出目录，**玩家无需联网下载**。

## 需要的文件

| 文件 | 来源 |
|------|------|
| `wireguard.exe` | `C:\Program Files\WireGuard\`（安装 WireGuard 后） |
| `wintun.dll` | 同上 |
| `wg.exe` | 同上（可选） |
| `wireguard-installer.exe` | https://download.wireguard.com/wireguard-installer.exe |

## 一键准备（开发机已装 WireGuard 时）

```powershell
cd G:\3.Projects_Indpnd\CivLan
.\scripts\prepare-wireguard.ps1
```

## 发布客户端

```powershell
dotnet publish src/CivLan.Client -c Release -o client-release
```

将 `client-release` 文件夹整包发给队友即可，内含 `wireguard\` 子目录。

## 首次连接说明

- 若本机 **从未装过 WireGuard 驱动**，会静默运行内置的 `wireguard-installer.exe`（需管理员权限，仅一次）
- 之后直接使用内置 `wireguard.exe` 连接，**不再下载**
