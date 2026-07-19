#!/usr/bin/env python3
"""
Civ VI LAN discovery relay for WireGuard.

Civilization VI discovers LAN rooms by sending UDP broadcasts to
255.255.255.255 on ports 62900-62999. WireGuard does not forward those
broadcasts between peers, so rooms never appear in the LAN list.

This daemon:
  1. Listens on those UDP ports on the WireGuard interface
  2. Relays each packet as unicast to every other peer in the subnet
  3. Spoofs the original source IP/port so the host can reply correctly

Run as root (needed for IP spoofing via raw sockets).
"""

from __future__ import annotations

import argparse
import ipaddress
import logging
import os
import select
import socket
import struct
import subprocess
import sys
import time
from typing import Iterable

LOG = logging.getLogger("civ6-lan-relay")

PORT_START = 62900
PORT_END = 62999  # inclusive
JOIN_PORT = 62056  # post-discovery join traffic (unicast; relay as safety net)


def checksum(data: bytes) -> int:
    if len(data) % 2:
        data += b"\x00"
    s = sum(struct.unpack("!%dH" % (len(data) // 2), data))
    s = (s >> 16) + (s & 0xFFFF)
    s += s >> 16
    return (~s) & 0xFFFF


def list_peers(subnet: ipaddress.IPv4Network, server_ip: ipaddress.IPv4Address) -> set[ipaddress.IPv4Address]:
    """Return WireGuard peer tunnel IPs inside the CivLan subnet."""
    peers: set[ipaddress.IPv4Address] = set()
    try:
        out = subprocess.check_output(["wg", "show", "all", "allowed-ips"], text=True, stderr=subprocess.DEVNULL)
    except (subprocess.CalledProcessError, FileNotFoundError) as ex:
        LOG.warning("wg show failed: %s", ex)
        return peers

    for line in out.splitlines():
        # format: interface\tpeer_key\t10.0.0.2/32
        parts = line.split()
        if len(parts) < 3:
            continue
        for token in parts[2].split(","):
            token = token.strip()
            if not token:
                continue
            try:
                net = ipaddress.ip_network(token, strict=False)
            except ValueError:
                continue
            if isinstance(net, ipaddress.IPv4Network) and net.subnet_of(subnet):
                # AllowedIPs are usually /32 host routes
                for host in net.hosts() if net.num_addresses > 1 else [net.network_address]:
                    if host != server_ip:
                        peers.add(host)
                if net.prefixlen == 32 and net.network_address != server_ip:
                    peers.add(net.network_address)
    return peers


def build_udp_ip_packet(
    src: ipaddress.IPv4Address,
    dst: ipaddress.IPv4Address,
    sport: int,
    dport: int,
    payload: bytes,
) -> bytes:
    udp_len = 8 + len(payload)
    udp_header = struct.pack("!HHHH", sport, dport, udp_len, 0)
    # UDP checksum with IPv4 pseudo-header
    pseudo = struct.pack("!4s4sBBH", src.packed, dst.packed, 0, socket.IPPROTO_UDP, udp_len)
    udp_csum = checksum(pseudo + udp_header + payload)
    if udp_csum == 0:
        udp_csum = 0xFFFF
    udp_header = struct.pack("!HHHH", sport, dport, udp_len, udp_csum)

    ip_header_len = 20
    total_len = ip_header_len + udp_len
    ip_header = struct.pack(
        "!BBHHHBBH4s4s",
        0x45,  # version+ihl
        0,  # tos
        total_len,
        0,  # id
        0,  # flags/fragment
        64,  # ttl
        socket.IPPROTO_UDP,
        0,  # checksum placeholder
        src.packed,
        dst.packed,
    )
    ip_csum = checksum(ip_header)
    ip_header = struct.pack(
        "!BBHHHBBH4s4s",
        0x45,
        0,
        total_len,
        0,
        0,
        64,
        socket.IPPROTO_UDP,
        ip_csum,
        src.packed,
        dst.packed,
    )
    return ip_header + udp_header + payload


def send_spoofed(
    raw: socket.socket,
    src: ipaddress.IPv4Address,
    dst: ipaddress.IPv4Address,
    sport: int,
    dport: int,
    payload: bytes,
) -> None:
    packet = build_udp_ip_packet(src, dst, sport, dport, payload)
    raw.sendto(packet, (str(dst), 0))


def open_listen_sockets(ports: Iterable[int], bind_ip: str) -> dict[int, socket.socket]:
    sockets: dict[int, socket.socket] = {}
    for port in ports:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
        try:
            sock.bind((bind_ip, port))
        except OSError as ex:
            LOG.warning("bind %s:%s failed: %s", bind_ip, port, ex)
            sock.close()
            continue
        sock.setblocking(False)
        sockets[port] = sock
    return sockets


def main() -> int:
    parser = argparse.ArgumentParser(description="Civ VI LAN discovery relay for WireGuard")
    parser.add_argument("--subnet", default="10.0.0.0/24")
    parser.add_argument("--server-ip", default="10.0.0.1")
    parser.add_argument("--bind", default="0.0.0.0", help="Listen address (0.0.0.0 = all)")
    parser.add_argument("--include-join-port", action="store_true", help="Also relay UDP 62056")
    parser.add_argument("--peer-refresh", type=float, default=5.0)
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    if os.geteuid() != 0:
        LOG.error("Must run as root (raw IP spoofing requires CAP_NET_RAW).")
        return 1

    subnet = ipaddress.ip_network(args.subnet, strict=False)
    if not isinstance(subnet, ipaddress.IPv4Network):
        LOG.error("Only IPv4 subnet is supported.")
        return 1
    server_ip = ipaddress.ip_address(args.server_ip)
    if not isinstance(server_ip, ipaddress.IPv4Address):
        LOG.error("server-ip must be IPv4.")
        return 1

    ports = list(range(PORT_START, PORT_END + 1))
    if args.include_join_port:
        ports.append(JOIN_PORT)

    listen = open_listen_sockets(ports, args.bind)
    if not listen:
        LOG.error("No listen sockets opened.")
        return 1
    LOG.info("Listening on %d UDP ports (%s-%s)%s",
             len(listen), PORT_START, PORT_END,
             f" + {JOIN_PORT}" if args.include_join_port else "")

    raw = socket.socket(socket.AF_INET, socket.SOCK_RAW, socket.IPPROTO_RAW)
    raw.setsockopt(socket.IPPROTO_IP, socket.IP_HDRINCL, 1)

    peers = list_peers(subnet, server_ip)
    LOG.info("Initial peers: %s", ", ".join(str(p) for p in sorted(peers)) or "(none)")
    last_refresh = time.time()

    # Track recently relayed packets to avoid echo loops: (src, sport, dport, payload_hash)
    recent: dict[tuple, float] = {}
    DEDUP_SEC = 0.3

    while True:
        now = time.time()
        if now - last_refresh >= args.peer_refresh:
            peers = list_peers(subnet, server_ip)
            last_refresh = now
            # purge dedup
            recent = {k: t for k, t in recent.items() if now - t < 2.0}

        readable, _, _ = select.select(list(listen.values()), [], [], 1.0)
        for sock in readable:
            try:
                data, addr = sock.recvfrom(65535)
            except BlockingIOError:
                continue

            src_ip = ipaddress.ip_address(addr[0])
            if not isinstance(src_ip, ipaddress.IPv4Address):
                continue
            if src_ip not in subnet or src_ip == server_ip:
                continue

            sport = addr[1]
            # Find which port this socket is bound to
            dport = next(p for p, s in listen.items() if s is sock)

            key = (str(src_ip), sport, dport, hash(data))
            if key in recent and now - recent[key] < DEDUP_SEC:
                continue
            recent[key] = now

            targets = [p for p in peers if p != src_ip]
            if not targets:
                LOG.debug("No peers to relay from %s:%s", src_ip, sport)
                continue

            LOG.info("Relay %s:%s -> %s peers (dport %s, %d bytes)",
                     src_ip, sport, len(targets), dport, len(data))
            for dst in targets:
                try:
                    send_spoofed(raw, src_ip, dst, sport, dport, data)
                except OSError as ex:
                    LOG.warning("send to %s failed: %s", dst, ex)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(0)
