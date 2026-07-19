#!/bin/bash
# Enable Civ VI LAN discovery over WireGuard (run once on Ubuntu VPS as root)
# Installs sysctl tweaks + UDP broadcast relay for ports 62900-62999.
set -euo pipefail

WG_IF="${WG_IF:-wg0}"
SUBNET="${SUBNET:-10.0.0.0/24}"
SERVER_IP="${SERVER_IP:-10.0.0.1}"
INSTALL_DIR="${INSTALL_DIR:-/opt/CivLan}"
RELAY_DIR="${INSTALL_DIR}/lan-relay"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "$(id -u)" -ne 0 ]]; then
  echo "Please run as root: sudo bash $0"
  exit 1
fi

echo "==> Enabling IP forward + broadcast forward"
cat >/etc/sysctl.d/99-civlan-lan.conf <<EOF
net.ipv4.ip_forward = 1
net.ipv4.conf.all.bcast_forward = 1
net.ipv4.conf.default.bcast_forward = 1
net.ipv4.conf.${WG_IF}.bcast_forward = 1
EOF
sysctl --system >/dev/null

echo "==> Installing Civ VI UDP LAN relay"
mkdir -p "$RELAY_DIR"
if [[ -f "$SCRIPT_DIR/civ6-lan-relay.py" ]]; then
  cp "$SCRIPT_DIR/civ6-lan-relay.py" "$RELAY_DIR/civ6-lan-relay.py"
elif [[ -f "./civ6-lan-relay.py" ]]; then
  cp "./civ6-lan-relay.py" "$RELAY_DIR/civ6-lan-relay.py"
else
  echo "ERROR: civ6-lan-relay.py not found next to this script."
  exit 1
fi
chmod +x "$RELAY_DIR/civ6-lan-relay.py"

# Prefer python3
if ! command -v python3 >/dev/null 2>&1; then
  apt-get update -y
  apt-get install -y python3
fi

cat >/etc/systemd/system/civlan-lan-relay.service <<EOF
[Unit]
Description=CivLan Civ VI LAN UDP Discovery Relay
After=network.target civlan.service
Wants=civlan.service

[Service]
Type=simple
ExecStart=/usr/bin/python3 ${RELAY_DIR}/civ6-lan-relay.py --subnet ${SUBNET} --server-ip ${SERVER_IP} --include-join-port
Restart=always
RestartSec=2
User=root

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable --now civlan-lan-relay.service

echo "==> Restarting WireGuard + CivLan to apply PostUp rules"
systemctl restart civlan || true
wg-quick down "$WG_IF" 2>/dev/null || true
if [[ -f "/etc/wireguard/${WG_IF}.conf" ]]; then
  wg-quick up "/etc/wireguard/${WG_IF}.conf"
fi

echo ""
echo "==> Status"
systemctl --no-pager --full status civlan-lan-relay.service | sed -n '1,15p' || true
echo ""
echo "Done."
echo "Players must use CivLan Client v1.0.4+ (AllowedIPs includes 255.255.255.255)"
echo "and reconnect VPN after upgrading. Then use Civ VI -> LAN -> Create / Refresh."
