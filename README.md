# Anamnesis Bridge

**Author:** [DeadlyDeathAngel](https://github.com/DeadlyDeathAngel)

Companion Dalamud plugin for the [native Linux Anamnesis](https://github.com/imchillin/Anamnesis) port. Exposes game state and actor editing over localhost HTTP so the Linux client can drive the game in-process (reliable on Wine/XLCore).

**v0.2.7.6** — auto-starts when you sign in, appearance/equipment editing, skeleton posing, `.pose` import, and GPose actor targeting.

Not an official Anamnesis Team plugin — designed and maintained for the Linux bridge workflow.

## Default URL

`http://127.0.0.1:6679/anamnesis/v1/`

| Path | Description |
|------|-------------|
| `GET /health` | Plugin liveness + version |
| `GET /capabilities` | Supported API features |
| `GET /gpose` | `{ "isInGpose": bool }` |
| `GET /territory` | Territory id + signed-in |
| `GET /status` | All fields |
| `GET /actors` | Nearby actors (names, indices, addresses) |
| `GET/POST /target` | Read or set game target |
| `GET/POST /actors/{id}/appearance` | Read or write customize |
| `GET/POST /actors/{id}/equipment` | Read or write gear |
| `POST /actors/{id}/redraw` | Force actor redraw |
| `GET/POST /actors/{id}/skeleton` | Read or write bone transforms |
| `POST /actors/{id}/skeleton/apply-pose` | Apply Brio/Anamnesis pose JSON |
| `GET/POST /ipc` | Posing flags and physics hooks |
| `GET /game-data/*` | Items, dyes, colors, customize options, icons |

## Build (dev)

Requires **.NET 10 SDK** (matches Dalamud 15 / current XLCore). .NET 9 cannot compile against Dalamud 15.

```bash
dotnet --version   # must be 10.x
./scripts/linux/build-anamnesis-bridge.sh
```

Output: `dist/anamnesis-bridge/AnamnesisBridge/`

If you only have .NET 9 on Linux, build on another machine with .NET 10 or install the [SDK 10 preview](https://dotnet.microsoft.com/download/dotnet/10.0), then copy the output folder.

## Install into XLCore

1. Build the plugin (above).
2. Copy the output folder into your XLCore Dalamud plugins directory, e.g.:

```bash
./scripts/linux/install-anamnesis-bridge-xlcore.sh
```

3. In-game: `/xlplugins` → enable **Anamnesis Bridge**.
4. Verify: `/anamnesisbridge` or `curl http://127.0.0.1:6679/anamnesis/v1/health`

### Custom repository (optional)

Add to `/xlsettings` → Experimental → Custom Plugin Repositories:

```text
https://raw.githubusercontent.com/DeadlyDeathAngel/Anamnesis-Bridge/refs/heads/main/repo.json
```

Install or update from **Releases** (`v0.2.7.6` and later) for one-click install via the custom repo.

## Flatpak XIVLauncher

Native Linux Anamnesis outside the Flatpak may need shared network:

```bash
flatpak override --user --share=network dev.goats.xivlauncher
```

## Linux Anamnesis

Set optional override:

```bash
export ANAMNESIS_BRIDGE_URL=http://127.0.0.1:6679/anamnesis/v1
```

Attach as usual; session log should show `Dalamud bridge connected` and reliable GPose state.
