# CivLan

**Civilization VI virtual LAN multiplayer assistant — built-in WireGuard, no separate VPN client required.**

文明6 虚拟局域网联机助手。通过 WireGuard 在 VPS 上组网，解决 NAT / 直连困难。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Server-Ubuntu-blue)](https://ubuntu.com/)
[![Platform](https://img.shields.io/badge/Client-Windows-0078D4)](https://www.microsoft.com/windows)

---

## Features

- **Civilization VI LAN play** over a virtual private network (up to 4 players per room)
- **Built-in WireGuard** in the Windows client — one-click VPN connect
- **Lightweight server** — ASP.NET Core Kestrel only, no Nginx/Docker required
- **DDD architecture** — Domain / Application / Infrastructure / Server / Client
- **Self-contained Linux publish** supported for VPS deployment

---

## Architecture

```
src/
├── CivLan.Domain/           # Room, Peer, virtual IP, WireGuard keys
├── CivLan.Application/      # Room service, DTOs
├── CivLan.Infrastructure/   # JSON persistence, WireGuard config generation
├── CivLan.Server/           # REST API (port 5199)
└── CivLan.Client/           # WPF client with built-in WireGuard

deploy/
└── install-server.sh        # Ubuntu bootstrap script
```

---

## Quick Start

### Server (Ubuntu VPS)

```bash
# Install dependencies
sudo apt update
sudo apt install -y wireguard wireguard-tools

# Publish on dev machine (self-contained for Linux)
dotnet publish src/CivLan.Server -c Release -r linux-x64 --self-contained -o publish-linux

# Upload to VPS, e.g. /opt/CivLan/CivLan-Server
# Edit appsettings.json: ServerApiKey, EndpointPublicHost, EgressInterface

# Firewall
sudo ufw allow 5199/tcp
sudo ufw allow 51820/udp

# Run
chmod +x CivLan.Server
sudo ./CivLan.Server

# Or use systemd (see deploy/install-server.sh)
```

### Client (Windows)

```powershell
# 1. Bundle WireGuard (see below)
.\scripts\prepare-wireguard.ps1

# 2. Build & run
dotnet build
dotnet run --project src/CivLan.Client

# 3. Release for friends
.\scripts\publish-client-release.ps1 -Version 1.0.1
```

Upload **`CivLan.Client-v1.0.1-win-x64.zip`** to GitHub Releases (not source-only).

---

## Bundle WireGuard (required for release)

WireGuard binaries are **not** committed to this repo. Before publishing the client, place these files in `src/CivLan.Client/wireguard/`:

| File | Source |
|------|--------|
| `wireguard.exe` | `C:\Program Files\WireGuard\` (after installing WireGuard once) |
| `wg.exe` | same |
| **`wireguard-amd64.msi`** | [Browse MSIs](https://download.wireguard.com/windows-client/) — **required for offline driver install** |

Do **not** rely on the 85 KB `wireguard-installer.exe` alone — it downloads the MSI online and often fails in China.

One-command prepare + publish:

```powershell
.\scripts\prepare-wireguard.ps1      # copy exe + download MSI
.\scripts\publish-client-release.ps1   # publish, verify, zip -> CivLan.Client-v1.0.1-win-x64.zip
```

Players only run **CivLan** — first VPN connect installs the TUN driver once (admin UAC). No separate WireGuard install needed.

---

## Configuration

### Server `appsettings.json`

```json
{
  "CivLan": {
    "ServerApiKey": "your-strong-secret"
  },
  "WireGuard": {
    "EndpointPublicHost": "YOUR_VPS_PUBLIC_IP",
    "EgressInterface": "eth0",
    "NetworkPrefix": "10.0.0",
    "ApplyOnChange": true,
    "UseSudo": false
  }
}
```

### Client

| Field | Example |
|-------|---------|
| Server URL | `http://203.0.113.10:5199` |
| API Key | same as `CivLan:ServerApiKey` |

---

## How to Play Civ VI

1. Host: **Create room** → **Connect VPN**
2. Friends: **Join room** (6-digit code) → **Connect VPN**
3. Host: Civ VI → Multiplayer → **LAN** → Create game
4. Others: Join LAN list, or **Connect via IP** → host virtual IP (shown in client)

Ensure game version, DLC, and mods match.

---

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/health` | Health check (no API key) |
| POST | `/api/rooms` | Create room |
| POST | `/api/rooms/{code}/join` | Join room |
| GET | `/api/rooms/{code}` | Room status |

Header: `X-Api-Key: <your-key>`

---

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 5199 | TCP | CivLan API |
| 51820 | UDP | WireGuard |

---

## Development

```powershell
dotnet restore
dotnet build
dotnet run --project src/CivLan.Server    # http://localhost:5199
dotnet run --project src/CivLan.Client
```

Development uses `WireGuard:ApplyOnChange=false` — API works on Windows without `wg` on the server.

---

## Third-Party Components

- **[WireGuard](https://www.wireguard.com/)** — VPN tunnel (GPLv2). Binaries bundled in client releases are subject to WireGuard's license. Obtain them from the official installer; do not redistribute without complying with GPLv2.
- **[NSec.Cryptography](https://github.com/ektrah/nsec)** — WireGuard key generation (MIT)

---

## License

This project (CivLan source code) is licensed under the [MIT License](LICENSE).

Bundled WireGuard executables are **not** part of this repository and remain under the [WireGuard GPLv2 license](https://www.wireguard.com/).

---

## Disclaimer

For private friends-only game sessions. Comply with local laws, game EULAs, and your VPS provider's terms of service.

---

## Security / 隐私

**Do not commit secrets.** See [SECURITY.md](SECURITY.md).

- Repository uses placeholders (`YOUR_SERVER_IP`, `REPLACE_WITH_A_STRONG_RANDOM_SECRET`).
- Real API keys and VPS config belong **only on your server** and in the client UI locally.
- WireGuard binaries are not in git; room data under `data/` is gitignored.
