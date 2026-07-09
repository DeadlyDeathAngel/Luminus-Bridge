# Luminus Bridge

**Author:** [DeadlyDeathAngel](https://github.com/DeadlyDeathAngel)

Companion Dalamud plugin for the [native Linux Luminus](https://github.com/imchillin/Luminus) port. Exposes game state and actor editing over localhost HTTP so the Linux client can drive the game in-process (reliable on Wine/XLCore).

**v0.2.8.10** — camera control, `.shot` import/export, world time freeze + weather hold (GPose), legacy bone names for `.pose`, and everything from v0.2.7.6 (appearance, equipment, skeleton posing, auto-start).

Not an official Luminus Team plugin — designed and maintained for the Linux bridge workflow.

## Default URL

`http://127.0.0.1:6679/luminus/v1/`

| Path | Description |
|------|-------------|
| `GET /health` | Plugin liveness + version |
| `GET /capabilities` | Supported API features |
| `GET /gpose` | `{ "isInGpose": bool }` |
| `POST /gpose/prepare-posing` | Enter GPose and pin target for posing |
| `GET/POST /camera` | Read or write GPose camera |
| `GET/POST /camera/shot` | Export or import `.shot` camera files |
| `GET/POST /world` | Read or write Eorzea time, freeze time, weather |
| `GET /territory` | Territory id + signed-in |
| `GET /status` | All fields |
| `GET /actors` | Nearby actors (names, indices, addresses) |
| `GET/POST /target` | Read or set game target |
| `GET/POST /actors/{id}/appearance` | Read or write customize |
| `GET/POST /actors/{id}/equipment` | Read or write gear |
| `POST /actors/{id}/redraw` | Force actor redraw |
| `GET/POST /actors/{id}/skeleton` | Read or write bone transforms |
| `POST /actors/{id}/skeleton/apply-pose` | Apply Brio/Luminus pose JSON |
| `GET/POST /ipc` | Posing flags and physics hooks |
| `GET /game-data/*` | Items, dyes, weathers, colors, customize options, icons |

## Build (dev)

Requires **.NET 10 SDK** (matches Dalamud 15 / current XLCore). .NET 9 cannot compile against Dalamud 15.

```bash
dotnet --version   # must be 10.x
./scripts/linux/build-luminus-bridge.sh
```

Output: `dist/luminus-bridge/LuminusBridge/`

Plugin icon for `/xlplugins`: `LuminusBridge/images/icon.png` (512×512). The build copies it beside the DLL as `images/icon.png`. Custom-repo installs also use `IconUrl` in `LuminusBridge.json` / `repo.json`.

If you only have .NET 9 on Linux, build on another machine with .NET 10 or install the [SDK 10 preview](https://dotnet.microsoft.com/download/dotnet/10.0), then copy the output folder.

## Install into XLCore

1. Build the plugin (above).
2. Copy the output folder into your XLCore Dalamud plugins directory, e.g.:

```bash
./scripts/linux/install-luminus-bridge-xlcore.sh
```

3. In-game: `/xlplugins` → enable **Luminus Bridge**.
4. Verify: `/luminusbridge` or `curl http://127.0.0.1:6679/luminus/v1/health`

### Custom repository (optional)

Add to `/xlsettings` → Experimental → Custom Plugin Repositories:

```text
https://raw.githubusercontent.com/DeadlyDeathAngel/Luminus-Bridge/refs/heads/main/repo.json
```

Install or update from **Releases** (`v0.2.8.10` and later) for one-click install via the custom repo.

## Flatpak XIVLauncher

Native Linux Luminus outside the Flatpak may need shared network:

```bash
flatpak override --user --share=network dev.goats.xivlauncher
```

## Linux Luminus

Set optional override:

```bash
export LUMINUS_BRIDGE_URL=http://127.0.0.1:6679/luminus/v1
```

Attach as usual; session log should show `Dalamud bridge connected` and reliable GPose state.
