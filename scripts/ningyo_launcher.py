#!/usr/bin/env python3
"""
ningyo_launcher.py — Auto-launcher, restarter & MCP health-check helper for NINGYO (VRoid Studio MCP v2.0).

Checks TCP + JSON-RPC MCP readiness (initialize + tools/list) on 127.0.0.1:17891/mcp,
launches VRoid Studio visibly on Windows via Steam (steam.exe -applaunch 1486350) so
MainWindowHandle != 0 and processes outlive agent tool calls, and supports clean restart.
Zero external dependencies (Python 3.10+ stdlib only).
"""

from __future__ import annotations

import argparse
import json
import os
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path
from urllib.parse import urlparse

DEFAULT_MCP_URL = os.environ.get("NINGYO_MCP_URL", "http://127.0.0.1:17891/mcp")
STEAM_APP_ID = "1486350"

CANDIDATE_STEAM = [
    r"C:\Program Files (x86)\Steam\steam.exe",
    r"C:\Program Files\Steam\steam.exe",
]

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


def find_first(candidates: list[str]) -> Path | None:
    for c in candidates:
        if c:
            p = Path(c)
            if p.is_file():
                return p
    return None


def mcp_rpc(
    mcp_url: str,
    method: str,
    params: dict | None = None,
    timeout: float = 5.0,
    session_id: str | None = None,
) -> tuple[dict | None, str | None]:
    payload = json.dumps({
        "jsonrpc": "2.0",
        "id": 1,
        "method": method,
        "params": params or {},
    }).encode("utf-8")
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if session_id:
        headers["Mcp-Session-Id"] = session_id
    req = urllib.request.Request(
        mcp_url,
        data=payload,
        headers=headers,
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            sid = resp.headers.get("Mcp-Session-Id") or session_id
            raw = resp.read().decode("utf-8", errors="replace").strip()
            for line in raw.splitlines():
                if line.startswith("data:"):
                    raw = line[5:].strip()
                    break
            return (json.loads(raw) if raw else None), sid
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError, OSError):
        return None, session_id


def probe_mcp(mcp_url: str) -> dict:
    host, port = parse_host_port(mcp_url)
    port_open = is_port_open(host, port)
    tool_count = None
    mcp_ready = False
    if port_open:
        init, sid = mcp_rpc(mcp_url, "initialize", {
            "protocolVersion": "2025-06-18",
            "capabilities": {},
            "clientInfo": {"name": "ningyo-launcher", "version": "2.0.0"},
        })
        if init and "result" in init:
            tools_res, _ = mcp_rpc(mcp_url, "tools/list", {}, session_id=sid)
            if tools_res and "result" in tools_res:
                tools = tools_res["result"].get("tools", [])
                tool_count = len(tools)
                mcp_ready = True
    return {
        "port_open": port_open,
        "mcp_ready": mcp_ready,
        "tool_count": tool_count,
    }


def cmd_status(mcp_url: str) -> dict:
    exe = find_first(CANDIDATE_EXES)
    steam = find_first(CANDIDATE_STEAM)
    health = probe_mcp(mcp_url)
    return {
        "ok": True,
        "mcp_url": mcp_url,
        **health,
        "vroid_exe": str(exe) if exe else None,
        "steam_exe": str(steam) if steam else None,
        "steam_uri": f"steam://rungameid/{STEAM_APP_ID}",
    }


def launch_vroid() -> str:
    steam = find_first(CANDIDATE_STEAM) if sys.platform == "win32" else None
    if steam is not None:
        subprocess.Popen(
            [str(steam), "-applaunch", STEAM_APP_ID],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        return f"{steam} -applaunch {STEAM_APP_ID}"

    if sys.platform == "win32":
        os.startfile(f"steam://rungameid/{STEAM_APP_ID}")
        return f"steam://rungameid/{STEAM_APP_ID}"

    exe = find_first(CANDIDATE_EXES)
    if exe is not None:
        subprocess.Popen([str(exe)], cwd=str(exe.parent), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        return str(exe)

    subprocess.Popen(["xdg-open", f"steam://rungameid/{STEAM_APP_ID}"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return f"steam://rungameid/{STEAM_APP_ID}"


def cmd_launch(mcp_url: str, timeout: int = 120) -> dict:
    health = probe_mcp(mcp_url)
    if health["mcp_ready"]:
        return {"ok": True, "already_running": True, "mcp_url": mcp_url, **health}

    launched_via = launch_vroid()
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        health = probe_mcp(mcp_url)
        if health["mcp_ready"]:
            return {"ok": True, "already_running": False, "launched_via": launched_via, "mcp_url": mcp_url, **health}
        time.sleep(1.5)

    return {
        "ok": False,
        "error": f"Timed out after {timeout}s waiting for MCP at {mcp_url}.",
        "launched_via": launched_via,
    }


def cmd_restart(mcp_url: str, timeout: int = 120) -> dict:
    host, port = parse_host_port(mcp_url)
    if is_port_open(host, port):
        mcp_rpc(mcp_url, "tools/call", {"name": "exit", "arguments": {"mode": "discard"}}, timeout=5.0)
        for _ in range(30):
            if not is_port_open(host, port, timeout=0.5):
                break
            time.sleep(0.5)
    time.sleep(1.0)
    return cmd_launch(mcp_url, timeout=timeout)


def main() -> int:
    parser = argparse.ArgumentParser(description="NINGYO VRoid Studio MCP launcher & health check")
    parser.add_argument("action", choices=["status", "launch", "restart"], help="Action to run")
    parser.add_argument("--url", default=DEFAULT_MCP_URL, help="MCP URL (default: http://127.0.0.1:17891/mcp)")
    parser.add_argument("--timeout", type=int, default=120, help="Launch wait timeout in seconds")
    args = parser.parse_args()

    if args.action == "status":
        result = cmd_status(args.url)
    elif args.action == "launch":
        result = cmd_launch(args.url, args.timeout)
    else:
        result = cmd_restart(args.url, args.timeout)

    print(json.dumps(result, indent=2))
    return 0 if result.get("ok") else 1


if __name__ == "__main__":
    raise SystemExit(main())
