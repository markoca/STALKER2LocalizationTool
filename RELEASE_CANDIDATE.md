# Localization Workbench v2.0.0-rc.1

**Release candidate:** `2.0.0-rc.1`

Localization Workbench has one supported application target: **Windows x64**.

## Runtime layout

Helper-tool binaries are **not distributed** with Localization Workbench.

The user supplies compatible Windows x64 tools under:

```text
tools/
  retoc.exe
  repak.exe
  UAssetGUI.exe
  Mappings.usmap
  S2HOCMM.exe
```

English UI strings are embedded in the executable; no loose `locales/` directory is required.

Internal runtime paths are derived from the directory containing `Localization Workbench.exe`:

```text
Source/
Translations/
Output/
tools/
```

Only the external GAME and MODS source paths are persisted in `user-paths.json` after a successful scan.

## Publish

Linux/CachyOS:

```bash
./scripts/publish-win-x64.sh
```

Windows PowerShell:

```powershell
.\scripts\publish-win-x64.ps1
```

Both publishers preserve locally existing runtime data:

```text
user-paths.json
Mods/
Source/
Translations/
Output/
tools/
```

The publish process never sources or redistributes tool binaries from the repository.

They do not contain legacy workspace or settings migration behavior.

## Expected runtime

```text
publish/win-x64/
  Localization Workbench.exe
  user-paths.json        # created after a successful scan
  tools/
    README.txt            # user places required tools here
  Mods/
  Source/
  Translations/
  Output/
```

## Workflow expectations

GAME uses the compact action labels **SCAN GAME**, **EXTRACT**, and **BUILD**.

GAME opens without an automatic scan. When all required GAME resources are available, the log reports:

```text
=========== GAME FOUND ===========
=========== READY TO SCAN ===========
```

MODS is manual-only. It is never scanned at startup, by watchers, by Settings, or by entering the MODS tab. Entering MODS performs only a lightweight source-presence check. Before the first MODS scan in the current session, when mod sources are present the log reports `=========== MODS FOUND ===========` and `=========== READY TO SCAN ===========` once; switching tabs does not repeat them. The full MODS scan starts only when the user clicks **SCAN MODS**.

Run the complete regression list in `RC_CHECKLIST.md` before shipping the candidate.
