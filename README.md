# AnamnesisBridge

Dalamud plugin that exposes **GPose** and **territory** state over localhost HTTP for the native Linux Anamnesis client.

## Default URL

`http://127.0.0.1:6679/anamnesis/v1/`

| Path | Description |
|------|-------------|
| `GET /health` | Plugin liveness |
| `GET /gpose` | `{ "isInGpose": bool }` |
| `GET /territory` | Territory id + signed-in |
| `GET /status` | All fields |
| `GET /actors` | Nearby actors from Dalamud `IObjectTable` (names, indices, addresses) |

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
https://raw.githubusercontent.com/DeadlyDeathAngel/Anamnesis-Bridge/main/repo.json
```

(Release zips must be published separately for one-click install.)

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
