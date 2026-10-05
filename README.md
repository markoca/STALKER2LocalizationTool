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

MODS source discovery follows the current release `launch.py` model rather than a filename whitelist. The workbench recursively accepts complete adjacent `.pak/.utoc/.ucas` triplets from loose/extracted trees and can read original `.zip`, `.7z`, and `.rar` mod archives directly. Archive triplets are materialized into the Cached source cache, while the original archive remains the source fingerprint.

Container families are inferred only for grouping physical source partners; whether a container actually carries localization is determined by inspecting it with retoc. This also handles numbered OverrideContent packages such as `...OverrideContent_30_P`.

The complete **24-hex ExportBundleData chunk ID** is the package identity. Different chunks are never deduplicated merely because a prefix matches.

For aliases of the same exact chunk, extraction inspects every alias payload, verifies the SID union, prefers a complete `Stalker2/Content/...` OverrideContent alias when available, and keeps the serializer-visible source/plugin package path as the source FPackageId identity. The output directory alias and source package identity are deliberately separate.

Workflow:

1. Put loose/extracted mod triplets or original ZIP/7z/RAR archives under `Mods`.
2. Scan MODS. NewContent and OverrideContent partners are kept together until exact package aliases are resolved.
3. Extract new or changed localization databases. The extraction manifest stores the canonical alias, source package identity, internal UAsset package path, and all discovered aliases.
4. Edit `Editable\<mod-name>\<language>.json`.
5. Build either:
   - **Modular** — one localization IoStore package per mod; or
   - **All-in-One** — one combined IoStore localization package.

When a source container uses Unreal numbered patch naming (`_<NN>_P`), the output automatically uses the next patch level (`_<NN+1>_P`) so the localization overlay wins normal load order, matching the current launch pipeline.

MODS overlays are rebuilt with normal stock `retoc.exe`. The finished package is round-tripped and checked for the canonical virtual path, the complete original 24-hex chunk/FPackageId, and the exact patched RawExport payload. If stock retoc changes a package identity for a future mod, that build fails instead of silently shipping a questionable overlay.

## Workspace layout

```text
STALKER2LocalizationTool\
├── Localization Workbench.exe
├── settings.json
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

## RC2 workspace migration

The release-candidate cleanup renamed the two development-era workspace folders:

```text
Extracted -> Cached
Ready     -> Editable
```

The publish scripts and runtime settings loader migrate the old default folders automatically. They also repair older RC settings that already contain `CachedFolder` / `EditableFolder` fields but still point at the legacy default physical paths.

Safe migration rules are deliberately conservative:

- populated legacy + missing new folder -> rename to the new folder;
- populated legacy + empty new folder -> replace the empty destination safely;
- empty legacy + existing new folder -> remove the empty legacy folder;
- populated legacy + populated new folder -> never merge or overwrite; use only the new folder and report the conflict.

After migration, the application never writes new runtime data to the legacy default folders. Custom workspace paths are preserved.

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

No Git, Rust/cargo, network download, dependency fetch, or sibling developer checkout is part of deployment. A normal published runtime carries everything it needs under `tools/`; advanced users can still override tool paths in Settings.

## Settings

Settings are stored in `settings.json` beside the executable.

The Settings window is grouped into workspace/game paths and tool paths. **Reset workspace paths** restores the default locations for:

```text
Mods
Cached
Editable
Output
```

It does not change the game Paks path or tool executable paths.

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

The publish scripts preserve runtime settings and workspace data, but refresh `tools/` and `locales/` from the source project on every deployment. This prevents stale runtime executables from surviving an upgrade. They also migrate legacy default workspace names when safe to do so.

## Linux / Wine

Typical launch:

```bash
cd ~/s2tools/STALKER2LocalizationTool/publish/win-x64

WINEPREFIX="$HOME/.wine-uassetgui" \
WINEDEBUG=-all \
wine Localization Workbench.exe
```

The UAssetGUI Wine prefix needs .NET 8 Desktop Runtime.

## Release-candidate policy

`1.0.0-rc.6` switches MODS packaging to the proven stock-retoc `launch.py` baseline. The NC/OC alias-resolution and complete 24-hex chunk identity rules remain, while custom source-package-map/ContainerHeader handling is removed. Manifest schema is 14 because physical source provenance and source container labels are now part of the packaging contract; older MODS caches must be extracted again. GAME LOCRES serialization stays on the existing S2HOCMM + repak path.

The custom `app.manifest` used by RC4 was removed so the .NET SDK supplies the normal Windows apphost manifest. This is part of the Windows Side-by-Side startup investigation; if an older EXE still fails before managed startup, capture `sxstrace` for the exact activation-context dependency.

See `RC_CHECKLIST.md` for the regression pass used before the final 1.0.0 release.
