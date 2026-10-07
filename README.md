# Localization Workbench

**Version:** `2.0.0-rc.1`

Windows desktop workbench for extracting, editing and rebuilding S.T.A.L.K.E.R. 2 localization.

This branch is the current **Version 2 Release Candidate 1** baseline. The project now uses `main` as the authoritative development branch.

## v2 RC1 highlights

- Unified **Localization Workbench** project/application identity.
- English UI strings are embedded in the application; there are no loose locale files.
- Clean portable runtime layout with internal paths resolved from the running EXE directory.
- No `settings.json` runtime configuration system.
- Only user-selected external source paths are persisted in `user-paths.json`.
- GAME and MODS use separate, step-aware workflows.
- MODS scanning is strictly manual and starts only when the user clicks **SCAN MODS**.
- MODS tab entry performs only a lightweight source-presence check before the first scan.
- Clear decorated workflow log states such as:

```text
=========== GAME FOUND ===========
=========== READY TO SCAN ===========
=========== FOUND GAME LOCALIZATION ===========
=========== LOCALIZATION READY FOR EXTRACTION ===========
=========== LOCALIZATION EXTRACTION DONE ===========
=========== LOCALIZATION READY FOR BUILD ===========
=========== MODS FOUND ===========
=========== SCANNING MODS ===========
=========== MODS READY FOR EXTRACTION ===========
=========== MODS READY FOR BUILD ===========
```

## Supported target

The application targets **Windows x64** (`net8.0-windows`).

Linux is supported as a development host for cross-publishing and Wine testing, not as a native application target.

Localization Workbench does **not** distribute helper-tool binaries.

The user must obtain compatible Windows x64 copies of the required tools and place them beside the application under:

```text
tools/
├── retoc.exe
├── repak.exe
├── UAssetGUI.exe
├── Mappings.usmap
└── S2HOCMM.exe
```

Tested tool baselines and download sources:

- **retoc v0.1.5** — https://github.com/trumank/retoc/releases
- **repak v0.2.3** — https://github.com/trumank/repak/releases
- **UAssetGUI v1.1.0** — https://github.com/atenfyr/UAssetGUI/releases
- **S2HOCMM v2.3** — https://www.nexusmods.com/stalker2heartofchornobyl/mods/540 or https://gitlab.com/PatrykPniewski/s2hocmm/-/releases
- **Mappings.usmap** — must match the installed game version. For the current tested S.T.A.L.K.E.R. 2 **2.0.4** baseline: https://www.nexusmods.com/stalker2heartofchornobyl/mods/2356

If S.T.A.L.K.E.R. 2 is updated, obtain a mapping generated for that game build rather than assuming an older `.usmap` is compatible.

The publish scripts do not download, validate, copy, or redistribute these third-party tools.

## GAME workflow

GAME handles the game's localization workflow.

The GAME action buttons are deliberately simple:

```text
SCAN GAME
EXTRACT
BUILD
```

Workflow:

1. **SCAN GAME** scans the game localization source.
2. **EXTRACT** creates one translation JSON file for every supported language.
3. Edit JSONs in `Translations/Game`.
4. Select one or more languages.
5. **BUILD** creates the selected localization package(s).

**SCAN GAME** inspects the supported game localization sources. A successful discovery reports:

```text
=========== FOUND GAME LOCALIZATION ===========
```

**EXTRACT** creates canonical rebuild source data under `Source/Game` and seeds `Translations/Game`.

After successful extraction:

```text
=========== LOCALIZATION EXTRACTION DONE ===========
=========== LOCALIZATION READY FOR BUILD ===========
```

If `Translations/Game` is deleted while a valid `Source/Game` still exists, the next scan reports **LOCALIZATION READY FOR EXTRACTION**. EXTRACT restores the missing translation files from Source without re-reading the game source packages.

**BUILD** serializes the selected language with S2HOCMM and packages the verified `Game.locres` with repak.

## MODS workflow

MODS handles LocalizationDatabase / IoStore localization.

MODS scanning is intentionally **manual-only**:

- startup never scans MODS;
- switching to the MODS tab never starts a scan;
- watchers never start a MODS scan;
- changing Settings never starts a MODS scan;
- only the **SCAN MODS** button starts the full scan.

Before the first MODS scan in the current session, entering the MODS tab performs only a lightweight source-presence check. If mod sources are present, these banners are shown once:

```text
=========== MODS FOUND ===========
=========== READY TO SCAN ===========
```

Switching between GAME and MODS does not repeat those pre-scan banners.

The full MODS workflow is:

1. Place loose mod files or original ZIP/7z/RAR archives under the configured Mods source folder.
2. Click **SCAN MODS**.
3. Complete IoStore triplets are discovered and localization content is inspected.
4. MODS localization uses supported OverrideContent-side containers only.
5. New or changed mods are marked **Needs extraction**.
6. **EXTRACT** writes canonical rebuild data under `Source/<mod>` and seeds `Translations/<mod>`.
7. Edit `Translations/<mod>/<language>.json`.
8. Build either **MODULAR** or **ALL-IN-ONE**.

If translation files are removed while corresponding Source data is still valid, EXTRACT restores only the missing translation files and does not overwrite files that are still present.

Archive display names are normalized for the UI: Nexus download IDs are omitted and detected versions are shown as `vX.X` / `vX.X.X` where available.

## Paths and persistence

Internal paths always follow the currently running executable:

```text
Localization Workbench.exe

Source/
Translations/
Output/
tools/
```

They are derived from `AppContext.BaseDirectory` and are never persisted as user configuration.

The application persists only the external user-selected source locations:

- GAME Paks folder
- MODS source folder

Those values are stored in:

```text
user-paths.json
```

They are written only after a successful GAME or MODS scan and reused on future launches when the stored paths are still valid.

## Workspace

Typical runtime layout:

```text
Localization Workbench.exe
user-paths.json

Mods/
Source/
├── Game/
└── <mod>/
Translations/
├── Game/
└── <mod>/
Output/
tools/
```

`Source` is rebuild state and should not be edited manually.

`Translations` contains the JSON files you edit and build.

## Build verification

GAME verifies the S2HOCMM result and the final repak output.

MODS rebuilds with stock retoc and verifies the final localization package against the expected package/chunk identity and patched payload.

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

Existing `user-paths.json`, `Mods`, `Source`, `Translations`, `Output`, and locally supplied `tools` data are preserved across republish.

A clean distribution should not include helper-tool binaries. The release may contain only `tools/README.txt`; the user supplies the actual tools.

The target Windows machine does not need a separate .NET installation.

## Wine test

```bash
WINEPREFIX="$HOME/.wine-uassetgui" \
WINEDEBUG=-all \
wine "publish/win-x64/Localization Workbench.exe"
```

The Wine prefix used for UAssetGUI must provide the .NET 8 Desktop Runtime expected by that tool.

## Release candidate validation

This baseline is **Localization Workbench v2.0.0-rc.1**.

Before promoting it to a final Version 2 release, run the complete regression pass in:

- `RC_CHECKLIST.md`
- `RELEASE_CANDIDATE.md`

For end-user instructions, see `QUICK_USER_HANDBOOK.md`.
