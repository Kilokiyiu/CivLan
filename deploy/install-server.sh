#!/bin/bash
set -euo pipefail

# CivLan server bootstrap for Ubuntu (WireGuard + lightweight API)
# Usage: sudo ./install-server.sh

CIVLAN_USER="${CIVLAN_USER:-$USER}"
INSTALL_DIR="/opt/civlan"
SERVICE_NAME="civlan"
WG_PORT=51820
API_PORT=5199

echo "==> Installing packages"
apt update
apt install -y wireguard wireguard-tools iptables

echo "==> Enabling IP forwarding"
grep -q 'net.ipv4.ip_forward=1' /etc/sysctl.conf || echo 'net.ipv4.ip_forward=1' >> /etc/sysctl.conf
sysctl -p

echo "==> Creating install directory"
mkdir -p "$INSTALL_DIR/data"
chown -R "$CIVLAN_USER:$CIVLAN_USER" "$INSTALL_DIR"

EGRESS_IF=$(ip route | awk '/default/ {print $5; exit}')
echo "Detected egress interface: $EGRESS_IF"

cat > /etc/systemd/system/${SERVICE_NAME}.service <<EOF
[Unit]
Description=CivLan Server
After=network.target

[Service]
WorkingDirectory=${INSTALL_DIR}
ExecStart=/usr/bin/dotnet ${INSTALL_DIR}/CivLan.Server.dll
Restart=always
RestartSec=5
User=${CIVLAN_USER}
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOF

echo "==> Next steps"
cat <<EOF

1. Publish locally:
   dotnet publish src/CivLan.Server/CivLan.Server.csproj -c Release -o publish

2. Copy publish output to ${INSTALL_DIR} on VPS

3. Edit ${INSTALL_DIR}/appsettings.json:
   - CivLan:ServerApiKey
   - WireGuard:EndpointPublicHost (VPS public IP)
   - WireGuard:EgressInterface (${EGRESS_IF})

4. Allow firewall:
   ufw allow ${API_PORT}/tcp
   ufw allow ${WG_PORT}/udp

5. Grant WireGuard permissions (choose one):
   - Run service as root, set WireGuard:UseSudo=false
   - Or add user to sudoers for wg-quick without password

6. Start service:
   systemctl daemon-reload
   systemctl enable --now ${SERVICE_NAME}

EOF
