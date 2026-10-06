# Localization Workbench

**Version:** `1.0.0-rc.7`

Windows desktop workbench for extracting, editing and rebuilding S.T.A.L.K.E.R. 2 localization.

## Supported target

The application targets **Windows x64** (`net8.0-windows`). Linux is supported as a development host for cross-publishing and Wine testing, not as a native application target.

The authoritative helper-tool bundle is:

```text
tools/win-x64/
├── retoc.exe
├── repak.exe
├── UAssetGUI.exe
├── Mappings.usmap
└── S2HOCMM.exe
```

No helper tool is downloaded or compiled by the publish scripts.

## GAME workflow

GAME handles the game's `Game.locres` workflow.

1. **SCAN GAME** inspects the supported game localization sources.
2. **EXTRACT** creates the canonical read-only cache under `Cached/Game` and seeds `Editable/Game`.
3. Edit `Editable/Game/<language>.json`.
4. **BUILD** serializes the selected language with S2HOCMM and packages the verified `Game.locres` with repak.

If `Editable/Game` is deleted while a valid `Cached/Game` still exists, the next scan reports **READY FOR EXTRACTION**. EXTRACT restores the missing editable files from Cached without re-reading the game source packages.

## MODS workflow

MODS handles LocalizationDatabase / IoStore localization.

1. Put loose mod files or original ZIP/7z/RAR archives under `Mods`.
2. **SCAN MODS** discovers complete IoStore triplets and inspects localization content.
3. Only OverrideContent-side localization containers are used; NewContent containers are ignored.
4. New or changed mods are marked **Needs extraction**.
5. **EXTRACT** writes canonical rebuild data under `Cached/<mod>` and seeds `Editable/<mod>`.
6. Extracted mods are shown as **Extracted**.
7. Edit `Editable/<mod>/<language>.json`.
8. Build either **MODULAR** or **ALL-IN-ONE**.

If editable translation files are removed while the corresponding Cached workspace is still valid, EXTRACT restores only the missing Editable files and does not overwrite files that are still present.

Archive display names are normalized for the UI: Nexus download IDs are omitted and detected versions are shown as `vX.X` / `vX.X.X` where available.

## Workspace

```text
Localization Workbench.exe

Mods/
Cached/
├── Game/
└── <mod>/
Editable/
├── Game/
└── <mod>/
Output/
tools/
locales/
```

`Cached` is rebuild state and should not be edited manually. `Editable` contains user-editable JSON files.

## Build verification

GAME verifies the S2HOCMM result and the final repak output. MODS rebuilds with stock retoc and verifies the final localization package against the expected package/chunk identity and patched payload.

A selected target language whose values already match the source does not create a redundant physical localization override.

## Publishing

From Linux/CachyOS:

```bash
./scripts/publish-win-x64.sh
```

From Windows PowerShell:

```powershell
.\scripts\publish-win-x64.ps1
```

Both publish paths create a self-contained Windows x64 runtime under:

```text
publish/win-x64/
```

The target Windows machine does not need a separate .NET installation.

## Wine test

```bash
WINEPREFIX="$HOME/.wine-uassetgui" \
WINEDEBUG=-all \
wine "publish/win-x64/Localization Workbench.exe"
```

The Wine prefix used for UAssetGUI must provide the .NET 8 Desktop Runtime expected by that tool.

See `RC_CHECKLIST.md` and `RELEASE_CANDIDATE.md` for the release-candidate regression pass.
