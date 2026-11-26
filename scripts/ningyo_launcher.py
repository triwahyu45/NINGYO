#!/usr/bin/env python3
"""
ningyo_launcher.py — Auto-launcher & health-check helper for ningyo (VRoid Studio MCP).

Checks whether VRoid Studio's BepInEx MCP endpoint (127.0.0.1:17891/mcp) is live,
launches VRoid Studio via executable or Steam AppID 1486350 if needed, and waits
until the MCP port is ready. Zero external dependencies (Python 3.10+ stdlib only).
"""

from __future__ import annotations

import argparse
import json
import os
import socket
import subprocess
import sys
import time
from pathlib import Path
from urllib.parse import urlparse

DEFAULT_MCP_URL = os.environ.get("NINGYO_MCP_URL", "http://127.0.0.1:17891/mcp")
STEAM_APP_ID = "1486350"

CANDIDATE_EXES = [
    os.environ.get("VROID_STUDIO_PATH", ""),
    r"C:\Program Files (x86)\Steam\steamapps\common\VRoid Studio\VRoidStudio.exe",
    r"C:\Program Files\Steam\steamapps\common\VRoid Studio\VRoidStudio.exe",
    r"D:\SteamLibrary\steamapps\common\VRoid Studio\VRoidStudio.exe",
    r"E:\SteamLibrary\steamapps\common\VRoid Studio\VRoidStudio.exe",
]


def parse_host_port(mcp_url: str) -> tuple[str, int]:
    parsed = urlparse(mcp_url)
    host = parsed.hostname or "127.0.0.1"
    port = parsed.port or 17891
    return host, port


def is_port_open(host: str, port: int, timeout: float = 1.0) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except OSError:
        return False


def find_vroid_exe() -> Path | None:
    for candidate in CANDIDATE_EXES:
        if candidate:
            p = Path(candidate)
            if p.is_file():
                return p
    return None


def cmd_status(mcp_url: str) -> dict:
    host, port = parse_host_port(mcp_url)
    exe = find_vroid_exe()
    running = is_port_open(host, port)
    return {
        "ok": True,
        "mcp_url": mcp_url,
        "port_open": running,
        "vroid_exe": str(exe) if exe else None,
        "steam_uri": f"steam://rungameid/{STEAM_APP_ID}",
    }


def cmd_launch(mcp_url: str, timeout: int = 120) -> dict:
    host, port = parse_host_port(mcp_url)
    if is_port_open(host, port):
        return {"ok": True, "already_running": True, "mcp_url": mcp_url}

    exe = find_vroid_exe()
    if exe is not None:
        subprocess.Popen([str(exe)], cwd=str(exe.parent), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        launched_via = str(exe)
    elif sys.platform == "win32":
        os.startfile(f"steam://rungameid/{STEAM_APP_ID}")
        launched_via = f"steam://rungameid/{STEAM_APP_ID}"
    else:
        subprocess.Popen(["xdg-open", f"steam://rungameid/{STEAM_APP_ID}"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        launched_via = f"steam://rungameid/{STEAM_APP_ID}"

    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if is_port_open(host, port, timeout=1.0):
            return {"ok": True, "already_running": False, "launched_via": launched_via, "mcp_url": mcp_url}
        time.sleep(1.5)

    return {
        "ok": False,
        "error": f"Timed out after {timeout}s waiting for {host}:{port}. Ensure BepInEx 6 and VroidStudioMcp.dll are installed.",
        "launched_via": launched_via,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="ningyo VRoid Studio MCP launcher & health check")
    parser.add_argument("action", choices=["status", "launch"], help="Action to run")
    parser.add_argument("--url", default=DEFAULT_MCP_URL, help="MCP URL (default: http://127.0.0.1:17891/mcp)")
    parser.add_argument("--timeout", type=int, default=120, help="Launch wait timeout in seconds")
    args = parser.parse_args()

    result = cmd_status(args.url) if args.action == "status" else cmd_launch(args.url, args.timeout)
    print(json.dumps(result, indent=2))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
