# Release candidate publish and test

Localization Workbench has one supported application target: **Windows x64**.

## Runtime layout

The authoritative helper-tool bundle is:

```text
tools/win-x64/
  retoc.exe
  repak.exe
  UAssetGUI.exe
  Mappings.usmap
  S2HOCMM.exe
```

Internal runtime paths are derived from the directory containing `Localization Workbench.exe`:

```text
Cached/
Editable/
Output/
tools/
locales/
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

Both publishers preserve:

```text
user-paths.json
Mods/
Cached/
Editable/
Output/
```

They do not contain legacy workspace or settings migration behavior.

## Expected runtime

```text
publish/win-x64/
  Localization Workbench.exe
  user-paths.json        # created after a successful scan
  tools/
  locales/
  Mods/
  Cached/
  Editable/
  Output/
```

## Workflow expectations

GAME opens without an automatic scan. When all required GAME resources are available, the log reports:

```text
=========== GAME FOUND ===========
=========== READY TO SCAN ===========
```

MODS is manual-only. It is never scanned at startup, by watchers, by Settings, or by entering the MODS tab. Entering MODS performs only a lightweight source-presence check; when mod sources are present the log reports `=========== MODS FOUND ===========` and `=========== READY TO SCAN ===========`. The full MODS scan starts only when the user clicks **SCAN MODS**.

Run the complete regression list in `RC_CHECKLIST.md` before shipping the candidate.
