# NINGYO — VRoid Studio MCP Suite (v2.0)

**NINGYO** turns **VRoid Studio** (Steam / Windows, tested on **v2.16.0** & **v2.14.0+**) into a headless-capable, AI-controllable 3D character studio over **Model Context Protocol (MCP)** (`http://127.0.0.1:17891/mcp`).

It uses a **Modular Companion Extension Architecture**:
- **`VroidStudioMcp.dll`** (upstream [`brainrom/vroid-studio-mcp`](https://github.com/brainrom/vroid-studio-mcp) v1.1.0, MIT) — kept clean and unmodified for effortless upstream updates.
- **`NingyoExtensions.dll`** (NINGYO v2.0, MIT) — Harmony + BepInEx 6 IL2CPP companion plugin that hooks into `VroidStudioMcp` at runtime to add **15 new MCP tools**, upgrade `export_vrm` with optimization controls, and fix the post-`main_menu` / post-`export_vrm` camera freeze bug (`CameraRebindPatch`).

---

## What's New in v2.0 (23 -> 38 MCP Tools)

- **Headless VRM 1.0 / 0.0 Export v2 (`export_vrm`)**: Direct export with zero OS Save File dialogs, plus optimization flags (`hair_combine`, `mesh_erasure`, `material_reduction`, `atlas_resolution`, `texture_compression`, `disable_spring_bones`) and clean preview cleanup back to Edit Mode.
- **Camera Freeze Fix (`CameraRebindPatch`)**: Automatically rebinds `GlobalBus.EditAvatarCameraPosition` / `PhotoBoothAvatarCameraPosition` after `main_menu` (`BackToModelSelect`) and `export_vrm`.
- **Direct Raster Texture I/O (`texture_list`, `texture_export_layer`, `texture_import_layer`)**: Export and import PNG layers directly via `VRoidCore` commands without opening Texture Editor or OS file pickers (undoable).
- **Expressions (`expressions_list`, `expression_set`)**: Inspect and drive all 14 Expression Editor blendshape categories or reset expressions.
- **Look & Shading (`look_get`, `look_set`)**: Control Outline, Rim Light, Shading, SpringBone amplitude, and Eye Excursion parameters.
- **Accessories (`accessory_list`, `accessory_add`, `accessory_remove`, `accessory_params`)**: List templates/instances, attach/remove accessories, and tune parameters.
- **PhotoBooth Control (`photobooth`)**: Enter/exit PhotoBooth, switch `pose_index`, toggle `auto_blink` / `look_at_camera`, set background color/alpha, or reset pose/lighting.
- **Session & Metrics (`undo`, `redo`, `body_metrics`)**: History undo/redo plus real-time 3D character height, bounding box, and anatomical landmark measurements.
- **Windows Steam Launcher (`scripts/ningyo_launcher.py`)**: Launches VRoid Studio visibly via `steam.exe -applaunch 1486350` and verifies MCP readiness (`Mcp-Session-Id` handshake).

---

## Tool Catalog (38 Tools)

| Category | Tools |
| :--- | :--- |
| **Core Session & Navigation** | `state`, `open`, `create`, `save`, `main_menu`, `exit`, `help`, `status`, `undo`, `redo`, `body_metrics` |
| **Editing & Presets** | `sections`, `categories`, `enter`, `presets`, `preset_preview_list`, `preset_preview_range`, `select_preset`, `create_custom_preset`, `import_custom_preset`, `export_custom_preset`, `set_param`, `batch` |
| **Camera & Capture** | `camera`, `screenshot` |
| **Look & Shading** | `look_get`, `look_set` |
| **Expressions** | `expressions_list`, `expression_set` |
| **Accessories** | `accessory_list`, `accessory_add`, `accessory_remove`, `accessory_params` |
| **Textures (Headless)** | `texture_list`, `texture_export_layer`, `texture_import_layer` |
| **PhotoBooth & VRM Export** | `photobooth`, `export_vrm` |

---

## Installation & Build

### Option A: Prebuilt Plugins
Copy `BepInEx/plugins/VroidStudioMcp.dll` and `BepInEx/plugins/NingyoExtensions.dll` into:
```text
<VRoid Studio>/BepInEx/plugins/
```
Make sure BepInEx 6 (Unity.IL2CPP) and `com.kokokoshka.vroidstudiomcp.servers.json` (`enabled: true`) are installed.

### Option B: Build from Source (.NET 6.0 SDK)
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -DeployToVroid
```
*(Stop `VRoidStudio.exe` before deploying so `NingyoExtensions.dll` is not file-locked.)*

### Launch & Health Check
```bash
python scripts/ningyo_launcher.py launch --timeout 90
python scripts/ningyo_launcher.py status
python scripts/ningyo_launcher.py restart --timeout 90
```

---

## Repository Structure
```text
NINGYO/
├── BepInEx/
│   ├── config/
│   └── plugins/
│       ├── VroidStudioMcp.dll       # Upstream core MCP host (v1.1.0, MIT)
│       └── NingyoExtensions.dll     # NINGYO v2.0 companion extension (15 tools + fixes)
├── src/
│   └── NingyoExtensions/            # C# (.NET 6.0 / BepInEx 6 IL2CPP) source
├── scripts/
│   ├── build.ps1                    # Build & deploy NingyoExtensions.dll
│   └── ningyo_launcher.py           # Steam launcher + Mcp-Session-Id health probe
└── README.md
```

---

## Credits & License
- **MIT License**
- Built on top of [`brainrom/vroid-studio-mcp`](https://github.com/brainrom/vroid-studio-mcp) (`VroidStudioMcp.dll`).
