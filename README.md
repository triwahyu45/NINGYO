# NINGYO — VRoid Studio 2.14.0 MCP + Headless `.vrm` Exporter

MCP Server Bundle (BepInEx 6 IL2CPP) for **Antigravity**, **Claude Code**, and **OpenAI Codex**. Connects AI agents directly to **VRoid Studio 2.14.0**'s internal API at `http://127.0.0.1:17891/mcp` — works even while the window is minimized/in the background — with full preset/slider/camera control **plus** dialog-free `.vrm` export (`VRM 1.0` & `VRM 0.0`).

---

## Architecture (Why Internal API Hook)

```
MCP Client (Claude / Codex / Antigravity)
   └──► http://127.0.0.1:17891/mcp (VroidStudioMcp.dll + VroidVrmExporter.dll)
           └──► VRoid Studio 2.14.0 Internal IL2CPP API (no OCR, no mouse hijack, works minimized)
```

---

## Requirements & Dependencies (For Agents)

- **Host App**: **VRoid Studio 2.14.0** (Steam AppID `1486350` / Windows; also works under Wine on Linux)
- **Mod Loader**: **BepInEx 6 (Unity IL2CPP x64)** installed in the `VRoid Studio` folder
- **Auto-Launcher Helper** *(optional)*: **Python 3.10+** (stdlib only, `scripts/ningyo_launcher.py`)
- **Companion Skill**: [`kagami`](https://github.com/triwahyu45/KAGAMI) (VRoid `.vroid` Protobuf/UV engineering + VRM 0.0/1.0 bone clamps + visual QA)
- **API Keys / Secrets**: None (100% local & free)

---

## Installation & MCP Client Config (Antigravity / Claude Code / Codex)

1. Install **BepInEx 6 (Unity IL2CPP)** into your `VRoid Studio` directory.
2. Copy `BepInEx/`, `i18n/`, and `labels/` from this repository into your `VRoid Studio` root directory.
3. Launch VRoid Studio (or run `python scripts/ningyo_launcher.py launch`), which starts the MCP server on port `17891`.
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

### Optional Auto-Launcher & Health Check (`scripts/ningyo_launcher.py`)

```bash
# Check if port 17891 is up and locate VRoidStudio.exe
python scripts/ningyo_launcher.py status

# Auto-launch VRoid Studio (via exe or steam://rungameid/1486350) and wait for port 17891
python scripts/ningyo_launcher.py launch --timeout 120
```

Environment variables:
- `NINGYO_MCP_URL` (default: `http://127.0.0.1:17891/mcp`)
- `VROID_STUDIO_PATH` (optional custom path to `VRoidStudio.exe`)

---

## Available MCP Tools (23 Tools)

### Lifecycle & Session
| Tool | Description |
| :--- | :--- |
| `state` | Current editor state (open model, section, category) |
| `status` | Non-blocking engine/queue status (safe to poll during `batch`) |
| `open(path)` | Open an existing `.vroid` file |
| `create(gender)` | Create a new character (`Fem` / `Masc`) |
| `save(path?)` | Save the active `.vroid` project |
| `main_menu` / `exit` | Return to main menu or close editor |
| `help(topic?)` | Authoritative reference for categories, sliders, and colors |

### Navigation, Presets & Parameters
| Tool | Description |
| :--- | :--- |
| `sections` | List top-level sections (`Face`, `Hairstyle`, `Body`, `Outfit`) |
| `categories(section)` | List tabs inside a section |
| `enter(section, category)` | Enter a category and return its numeric/color parameters + camera view |
| `set_param(param, value)` | Set a numeric slider or `#RRGGBB` / `r,g,b` color |
| `presets` | List available presets in the current category |
| `preset_preview_list` / `preset_preview_range` | Fetch preset thumbnail previews |
| `select_preset(index)` | Apply a preset by index |
| `create_custom_preset` / `import_custom_preset` / `export_custom_preset` | Manage `.vroidcustomitem` presets |

### Camera, Visual QA & `.vrm` Export
| Tool | Description |
| :--- | :--- |
| `camera(...)` | Frame camera (yaw, pitch, distance, target) |
| `screenshot(...)` | Capture MSAA/SSAA rendered preview of the character |
| `batch(ops)` | Execute ordered operations + captures in one call |
| `export_vrm(path, version, title, author)` | **Headless `.vrm` export (`"1.0"` or `"0.0"`) with zero OS dialogs** |

---

## `export_vrm` Example

```json
{
  "path": "C:\\path\\to\\output.vrm",
  "version": "1.0",
  "title": "Model Title",
  "author": "Author Name"
}
```

## Included Files

- `BepInEx/plugins/VroidStudioMcp.dll` & `VroidVrmExporter.dll`
- `BepInEx/config/com.kokokoshka.vroidstudiomcp.servers.json` — MSAA 4x, SSAA `render_scale: 2`, anisotropic filtering
- `scripts/ningyo_launcher.py` — Zero-dependency Python auto-launcher & port readiness checker
- `i18n/en.json` & `labels/*.json` — Human-readable English slider and preset labels
- `LICENSE`
