# ningyo — VRoid Studio 2.14.0 MCP + Headless `.vrm` Exporter

MCP Server Bundle (BepInEx 6 IL2CPP) for **Antigravity**, **Claude Code**, and **OpenAI Codex**. Connects AI agents directly to a running **VRoid Studio 2.14.0** editor at `http://127.0.0.1:17891/mcp` with full preset/slider/camera control **plus** dialog-free `.vrm` export (`VRM 1.0` & `VRM 0.0`).

---

## Requirements & Dependencies (For Agents)

- **Host App**: **VRoid Studio 2.14.0** (Steam / Windows; works under Wine on Linux)
- **Mod Loader**: **BepInEx 6 (Unity IL2CPP x64)** installed in the `VRoid Studio` folder
- **Included Plugins**:
  - `BepInEx/plugins/VroidStudioMcp.dll` — Core MCP HTTP/SSE server (`127.0.0.1:17891/mcp`) & TCP bridge (`127.0.0.1:17890`)
  - `BepInEx/plugins/VroidVrmExporter.dll` — Adds `export_vrm(path, version, title, author)` without OS file dialogs
- **Companion Skill**: [`kagami`](https://github.com/triwahyu45/kagami) (VRoid Protobuf/UV engineering + VRM 0.0/1.0 bone clamps + visual QA)
- **API Keys / Secrets**: None (100% local)

---

## Installation & MCP Client Config (Antigravity / Claude Code / Codex)

1. Install **BepInEx 6 (Unity IL2CPP)** into your `VRoid Studio` directory.
2. Copy `BepInEx/`, `i18n/`, and `labels/` from this repository into your `VRoid Studio` root directory.
3. Launch VRoid Studio (it will start the MCP server automatically on port `17891`).
4. Add this entry to your MCP config:
   - **Antigravity**: `~/.gemini/config/mcp_config.json`
   - **Claude Code**: `~/.claude.json` or project `.mcp.json`
   - **OpenAI Codex**: `~/.codex/config.toml` (HTTP MCP URL `http://127.0.0.1:17891/mcp`)

```json
{
  "mcpServers": {
    "vroid-studio": {
      "url": "http://127.0.0.1:17891/mcp"
    }
  }
}
```

---

## `export_vrm` Tool Schema

```json
{
  "path": "C:\\path\\to\\output.vrm",
  "version": "1.0",
  "title": "Model Title",
  "author": "Author Name"
}
```

- `path` *(required)*: Destination `.vrm` file path
- `version`: `"1.0"` (default) or `"0.0"`
- `title` / `author`: Optional VRM metadata

## Included Files

- `BepInEx/plugins/VroidStudioMcp.dll` & `VroidVrmExporter.dll`
- `BepInEx/config/com.kokokoshka.vroidstudiomcp.servers.json` — MSAA 4x, SSAA `render_scale: 2`, anisotropic filtering
- `i18n/en.json` & `labels/*.json` — Human-readable English slider and preset labels
- `LICENSE`
