# ningyo

BepInEx 6 (IL2CPP) MCP plugin bundle for **VRoid Studio 2.14.0**, combining upstream `vroid-studio-mcp` (`VroidStudioMcp.dll`) with **`VroidVrmExporter.dll`** for headless, dialog-free `.vrm` export (`VRM 1.0` & `VRM 0.0`).

## Included Files
- `BepInEx/plugins/VroidStudioMcp.dll` — Core MCP server (`127.0.0.1:17891/mcp`) & TCP bridge (`127.0.0.1:17890`)
- `BepInEx/plugins/VroidVrmExporter.dll` — Adds `export_vrm(path, version, title, author)` MCP tool (no OS file dialog)
- `BepInEx/config/com.kokokoshka.vroidstudiomcp.servers.json` — Server & MSAA/SSAA preview config
- `i18n/` & `labels/` — Readable English slider/preset labels

## Installation
1. Install **BepInEx 6 (Unity IL2CPP)** into your `VRoid Studio` directory.
2. Copy `BepInEx/`, `i18n/`, and `labels/` from this repo into your `VRoid Studio` folder.
3. Launch VRoid Studio, then connect your MCP client to:
   ```json
   {
     "mcpServers": {
       "vroid-studio": {
         "url": "http://127.0.0.1:17891/mcp"
       }
     }
   }
   ```

## `export_vrm` Tool
```json
{
  "path": "C:\\path\\to\\output.vrm",
  "version": "1.0",
  "title": "Model Title",
  "author": "Author Name"
}
```
- `version`: `"1.0"` (default) or `"0.0"`
