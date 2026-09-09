# The STALKER2 Localization Tool

**Version:** `1.0.0-rc.4`

A Windows desktop tool for extracting, editing, and rebuilding S.T.A.L.K.E.R. 2 localization on both the base game and supported mods.

The RC2 localization pipeline remains the frozen baseline. RC3 contains only targeted UI/release-candidate cleanup and does not change scan, extraction, build, packaging, or verification behavior.

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

Eligible mod source families are selected before container scanning. Supported filename endings are:

```text
_OC_50
_OC
-OverrideContent
_NC
B_P
```

Priority is:

```text
OverrideContent family (_OC_50 / _OC / -OverrideContent)
    > _NC
    > B_P
```

If an OverrideContent family exists for a mod, lower-priority families are ignored for localization scanning.

Workflow:

1. Put supported mod files under `Mods`.
2. Scan MODS.
3. Extract new or changed localization databases.
4. Edit `Editable\<mod-name>\<language>.json`.
5. Build either:
   - **Modular** — one localization package per mod; or
   - **All-in-One** — one combined OverrideContent localization package.

## Workspace layout

```text
STALKER2LocalizationTool\
├── STALKER2LocalizationTool.exe
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

A successful MODS build verifies the rebuilt IoStore localization payloads after packaging. Matched SIDs are included even when their values already equal the editable JSON (`changed=0`), because the overlay still needs the asset to win load precedence.

## Tools

The application uses:

- `retoc.exe`
- `repak.exe`
- `UAssetGUI.exe`
- `Mappings.usmap`
- `S2HOCMM.exe` for GAME LOCRES serialization

UAssetGUI is pinned to upstream v1.1.0 and verified during publish.

The common Linux/Wine setup can keep S2HOCMM in a sibling checkout; the application can auto-detect it or it can be selected manually in Settings.

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

The publish scripts preserve runtime settings, workspace data, mod inputs, output, and extra tool files. They also migrate legacy default workspace names when safe to do so.

## Linux / Wine

Typical launch:

```bash
cd ~/s2tools/STALKER2LocalizationTool/publish/win-x64

WINEPREFIX="$HOME/.wine-uassetgui" \
WINEDEBUG=-all \
wine STALKER2LocalizationTool.exe
```

The UAssetGUI Wine prefix needs .NET 8 Desktop Runtime.

## Release-candidate policy

For `1.0.0-rc.4`, the core localization pipeline remains frozen. RC4 only adjusts the JSON folder shortcut and its label; scan, extraction, build, packaging, and verification behavior are unchanged.

See `RC_CHECKLIST.md` for the regression pass used before the final 1.0.0 release.
