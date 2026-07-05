#!/bin/bash
# Enable Civ VI LAN broadcast over WireGuard (run once on Ubuntu VPS as root)
set -euo pipefail

WG_IF="${WG_IF:-wg0}"

echo "==> Enabling IP forward + broadcast forward"
cat >/etc/sysctl.d/99-civlan-lan.conf <<EOF
net.ipv4.ip_forward = 1
net.ipv4.conf.all.bcast_forward = 1
net.ipv4.conf.default.bcast_forward = 1
net.ipv4.conf.${WG_IF}.bcast_forward = 1
EOF
sysctl --system

echo "==> Restarting WireGuard + CivLan to apply wg0.conf PostUp rules"
systemctl restart civlan || true
wg-quick down "$WG_IF" 2>/dev/null || true
wg-quick up "/etc/wireguard/${WG_IF}.conf"

echo "==> Done. Civ VI LAN discovery ports: UDP 62997 (and sometimes 6200)"
echo "    Players must reconnect VPN after updating client to v1.0.3+"
