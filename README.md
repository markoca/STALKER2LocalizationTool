# Localization Workbench

**Version:** `1.0.0-rc.6`

A Windows desktop tool for extracting, editing, and rebuilding S.T.A.L.K.E.R. 2 localization on both the base game and supported mods.

RC6 aligns the MODS LocalizationDatabase pipeline with the current known-good **stock-retoc** `launch.py` baseline: exact 24-hex package identity, NewContent/OverrideContent alias analysis, canonical OverrideContent selection, and post-pack path/chunk/payload verification. No custom retoc patch is required.

## Workflows

### GAME

GAME is the only workflow that handles `Game.locres`.

1. Scan the original game `Stalker2\Content\Paks` directory.
2. Only `pakchunk0` is scanned for localization sources.
3. Extract all supported language JSON files into `Cached\Game`.
4. Create the initial editable copy in `Editable\Game` without overwriting existing user edits.
5. Edit `Editable\Game\<language>.json`.
6. Build one or more selected languages.

For each selected language, `Editable\Game\<language>.json` is authoritative. Non-empty entries are passed directly to S2HOCMM as the culture JSON, verified with an S2HOCMM dump, and then packed with the known-good S.T.A.L.K.E.R. 2 PAK settings.

Example:

```text
Editable\Game\serbian.json
    -> sr.json
    -> S2HOCMM
    -> Stalker2/Content/Localization/Game/sr/Game.locres
    -> repak V11 / mount ../../../ / path-hash-seed 1244705156
```

GAME output includes the installable PAK and a standalone `Game.locres` copy for inspection.

### MODS

MODS handles `LocalizationDatabase` / IoStore localization only. `Game.locres` is not scanned, extracted, or built from MODS.

RC6 scans paired localization containers together instead of discarding NewContent before package analysis. Recognized conventions include:

```text
*-NewContent / *-OverrideContent
*Stalker2-Windows-NewContent / *Stalker2-Windows-OverrideContent
_NC / _OC / _OC_50
_N / _O
A_P / B_P
```

The complete **24-hex ExportBundleData chunk ID** is the package identity. RC6 never deduplicates different chunks merely because their first 16 hex characters match.

For aliases of the same exact chunk, extraction inspects every alias payload, verifies the SID union, prefers a complete `Stalker2/Content/...` OverrideContent alias when available, and keeps the serializer-visible source/plugin package path as the source FPackageId identity. The output directory alias and source package identity are deliberately separate.

Workflow:

1. Put supported mod files under `Mods`.
2. Scan MODS. NewContent and OverrideContent partners are kept together until exact package aliases are resolved.
3. Extract new or changed localization databases. The extraction manifest stores the canonical alias, source package identity, internal UAsset package path, and all discovered aliases.
4. Edit `Editable\<mod-name>\<language>.json`.
5. Build either:
   - **Modular** — one localization IoStore package per mod; or
   - **All-in-One** — one combined IoStore localization package.

RC6 rebuilds MODS overlays with normal stock `retoc.exe`. The finished package is round-tripped and checked for the canonical virtual path, the complete original 24-hex chunk/FPackageId, and the exact patched RawExport payload. If stock retoc changes a package identity for a future mod, that build fails instead of silently shipping a questionable overlay.

## Workspace layout

```text
LocalizationWorkbench\
├── Localization Workbench.exe
├── Mods\
├── Cached\
│   ├── Game\
│   └── <mod>\
├── Editable\
│   ├── Game\
│   └── <mod>\
├── Output\
├── tools\
└── locales\
```

### Cached

`Cached` is the canonical read-only extraction workspace used for safe rebuilding. It contains manifests, pristine asset data, JSON dumps, and other rebuild inputs.

Do not edit files under `Cached`.

### Editable

`Editable` contains the JSON files intended for user edits and builds.

Extraction seeds an Editable workspace only when that destination does not already exist. Re-extraction never overwrites an existing Editable workspace.

## Portable runtime paths

Localization Workbench does not use `settings.json`. Internal paths are resolved from the directory containing the running executable on every launch:

```text
<EXE_DIR>\Mods
<EXE_DIR>\Cached
<EXE_DIR>\Editable
<EXE_DIR>\Output
<EXE_DIR>\tools\...
```

Moving or renaming the application folder therefore does not leave stale absolute runtime paths behind.

## GAME status panel

The GAME panel no longer exposes editing-path wording in the status card. The availability row is intentionally simple:

```text
Available    All JSONs
```

when the complete supported GAME JSON set exists. If only part of the set is present, the value is shown as a neutral `N / total JSONs` count. The actual folder can be opened with **Open JSONs**, which points to `Editable\Game` on the GAME tab.

## Folder shortcuts

The main window provides direct shortcuts for:

- Open JSONs
  - GAME opens `Editable\Game`
  - MODS opens `Editable`
- Open output

## Build verification

### GAME

A successful GAME build verifies:

- the authoritative editable JSON is non-empty after empty-value filtering;
- S2HOCMM created `Game.locres`;
- S2HOCMM `-Dump` reproduces every expected key/value;
- the final PAK uses mount point `../../../`;
- the final PAK is V11;
- the S.T.A.L.K.E.R. 2 path hash seed is `1244705156`;
- the `Game.locres` extracted back out of the final PAK is byte-for-byte identical to the verified S2HOCMM output.

### MODS

A successful MODS build verifies the finished IoStore package after stock-retoc packaging: canonical virtual alias, complete original 24-hex source chunk/FPackageId, and the exact patched RawExport payload after round-trip. As in the current `launch.py` baseline, a database whose requested target-language values are already correct does not produce a redundant physical override asset.

## Tools

The source-tree `tools/` directory is the authoritative deployment bundle. Put these PREBUILT Windows files there before publishing:

```text
retoc.exe
repak.exe
UAssetGUI.exe
Mappings.usmap
S2HOCMM.exe
```

`retoc.exe` is the normal upstream stock Windows CLI build; **no `--source-package-map` patch is required**. The publisher only checks that the local retoc can run. UAssetGUI is normally the pinned v1.1.0 binary. `S2HOCMM.exe` is bundled locally for GAME LOCRES serialization.

No Git, Rust/cargo, network download, dependency fetch, or sibling developer checkout is part of deployment. A normal published runtime carries everything it needs under `tools/`.

## Session configuration

There is no persistent `settings.json`. Internal workspace and tool paths are read-only auto-resolved paths relative to the running executable. External paths such as the game Paks folder can be selected for the current session.

## Startup cleanup

On startup the application performs best-effort cleanup of transient workspaces left by interrupted operations once they are at least six hours old:

- top-level `.*.staging.*` directories under `Cached` and `Editable`;
- `.work` directories under `Output`.

Persistent user data is never deleted by this cleanup.

## Publishing

Linux/CachyOS:

```bash
./scripts/publish-win-x64.sh
```

Windows PowerShell:

```powershell
.\scripts\publish-win-x64.ps1
```

The publish scripts preserve workspace data, remove any obsolete `settings.json`, and refresh `tools/` and `locales/` from the source project on every deployment.

## Linux / Wine

Typical launch:

```bash
cd ~/s2tools/LocalizationWorkbench/publish/win-x64

WINEPREFIX="$HOME/.wine-uassetgui" \
WINEDEBUG=-all \
wine "./Localization Workbench.exe"
```

The UAssetGUI Wine prefix needs .NET 8 Desktop Runtime.

## Release-candidate policy

`1.0.0-rc.6` switches MODS packaging to the proven stock-retoc `launch.py` baseline. The NC/OC alias-resolution and complete 24-hex chunk identity rules remain, while custom source-package-map/ContainerHeader handling is removed. Manifest schema remains 13, so RC5 schema-13 caches remain compatible. GAME LOCRES serialization stays on the existing S2HOCMM + repak path.

The custom `app.manifest` used by RC4 was removed so the .NET SDK supplies the normal Windows apphost manifest. This is part of the Windows Side-by-Side startup investigation; if an older EXE still fails before managed startup, capture `sxstrace` for the exact activation-context dependency.

See `RC_CHECKLIST.md` for the regression pass used before the final 1.0.0 release.
