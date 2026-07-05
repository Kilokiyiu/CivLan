# GitHub Release 发布清单 (v1.0.1)

## 一、本地打包（在项目根目录 PowerShell 执行）

```powershell
cd G:\3.Projects_Indpnd\CivLan

# 若 wireguard 目录已有 wireguard.exe + wireguard-amd64.msi，可跳过
.\scripts\prepare-wireguard.ps1

# 一键 publish + 校验 + 打 zip
.\scripts\publish-client-release.ps1 -Version 1.0.1
```

成功后得到：

- 文件夹：`client-release\`
- 压缩包：`CivLan.Client-v1.0.1-win-x64.zip`（约 15 MB）

## 二、自检 Release 内容

```
client-release/
  CivLan.Client.exe
  README.md                    ← 玩家说明
  VERSION.txt
  wireguard/
    wireguard.exe              (~9 MB)
    wg.exe
    wireguard-amd64.msi 或 wireguard-amd64-1.1.msi  (~3 MB)
```

**不要**只上传 exe；必须整包 zip。

## 三、上传到 GitHub

1. 打开仓库 → **Releases** → **Draft a new release**
2. **Tag**: `v1.0.1`（或 `CivLan.Client-v1.0.1`）
3. **Title**: `CivLan Client v1.0.1`
4. **上传附件**: `CivLan.Client-v1.0.1-win-x64.zip`
5. 勾选 **Set as the latest release**（若取代旧 Client 包）

### Release 说明（可复制）

```markdown
## CivLan Client v1.0.1

### 修复
- 内置 WireGuard **离线驱动安装包** (MSI)，新电脑无需联网即可首次连接 VPN
- 不再依赖 85KB 在线安装器（国内 Download Error）

### 使用
1. 下载并解压 zip（建议英文路径，如 `D:\CivLan\`）
2. 运行 `CivLan.Client.exe`
3. 填写服务器地址与 API Key，创建/加入房间后点「连接 VPN」
4. **首次连接**会弹出 UAC 安装驱动（仅一次）

### 文明6联机
- 主机：创建房间 → 连接 VPN → 文明6 局域网开房
- 队友：加入房间 → 连接 VPN → IP 直连主机虚拟 IP
```

## 四、通知队友

- 让队友**删除旧版**，下载 **v1.0.1** 新 zip
- 旧版 v1.0.0 在国内新电脑上会 VPN 安装失败

## 五、（可选）同步推送源码

若要把脚本与代码改动一并推到 GitHub：

```powershell
git add README.md scripts/ src/CivLan.Client/
git commit -m "fix(client): bundle offline WireGuard MSI for first-time VPN install"
git push
```

WireGuard 二进制与 zip **不要** commit（已在 .gitignore）。
