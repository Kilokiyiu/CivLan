# Security

## Do not commit

- **API keys** (`CivLan:ServerApiKey`)
- **VPS public/private IPs** used in production (use placeholders in source code)
- **WireGuard private keys** (`ServerPrivateKey`, peer keys in `data/`)
- **Room data** (`data/rooms.json`, `data/server-secrets.json`)
- **WireGuard `.conf` files** with real keys
- **Published builds** (`client-release/`, `publish/`)

## Safe defaults in repository

| File | Contains |
|------|----------|
| `appsettings.json` | Placeholders only |
| `appsettings.example.json` | Copy this on the server and fill in real values |
| Client UI defaults | `http://YOUR_SERVER_IP:5199`, empty API key |

## Production setup

1. On VPS, copy `appsettings.example.json` → `appsettings.Production.json` (or edit `appsettings.json` locally on server only).
2. Set a strong random `ServerApiKey`; share it privately with players.
3. Keep `appsettings.Production.json` and `data/` **only on the server**, not in git.

## Before pushing to GitHub

```powershell
# Search for accidental secrets
git grep -i "apikey\|privatekey\|ServerApiKey" 
git grep -E "\d+\.\d+\.\d+\.\d+"
```

If you find real IPs or keys, replace with placeholders before commit.

## Reporting

If you discover a committed secret, rotate the API key and WireGuard keys immediately.
