# Localization Workbench v2.0.0

**Release:** `2.0.0`

Localization Workbench Version 2 is the finalized Windows x64 release baseline for extracting, editing and rebuilding S.T.A.L.K.E.R. 2 localization.

## Version 2 baseline

- One portable **Localization Workbench** application identity.
- Separate GAME and MODS workflows with step-aware controls.
- Manual-only MODS source scanning.
- Direct UTOC directory-index scanning with bounded compatibility fallback to stock retoc.
- JSON translation workspaces separated from internal rebuild state.
- Modular and All-in-One MODS builds with round-trip verification.
- GAME `Game.locres` generation through S2HOCMM with final repak verification.
- .NET 10 LTS (`net10.0-windows`) application target with self-contained Windows x64 publishing.
- User-supplied third-party helper tools only; helper binaries are not distributed by this project.

## Final audit hardening

The final v2 audit additionally verified or corrected:

- cancellation propagates cleanly through GAME scanning and archive discovery;
- `Translations` is a JSON-only user workspace on fresh extraction;
- individually missing translation JSONs can be restored from `Source` without overwriting existing edits;
- archive materialization never writes directly from untrusted archive member paths;
- MODS scan concurrency remains bounded to two container jobs;
- scan/archive caches remain optimization-only and cannot replace source fingerprint validation;
- build manifests are schema/fingerprint checked before rebuild;
- final GAME and MODS outputs remain verification-gated;
- log section decorators are standardized as `=== TEXT ===` with no timestamp prefixes;
- taskbar/window icon handling works under Windows/Wine without adding an icon to the custom title bar;
- Settings now describes workspace watching as **Auto Refresh**, not automatic source scanning.

## Runtime layout

```text
Localization Workbench.exe
user-paths.json        # created after a successful scan
tools/
  README.txt
  retoc.exe            # user supplied
  repak.exe            # user supplied
  UAssetGUI.exe        # user supplied
  Mappings.usmap       # user supplied
  S2HOCMM.exe          # user supplied
Mods/
Source/
Translations/
Output/
```

Internal paths follow the executable directory. Only the external GAME Paks and MODS source paths are persisted in `user-paths.json`.

## Publishing

Linux/CachyOS:

```bash
./scripts/publish-win-x64.sh
```

Windows PowerShell:

```powershell
.\scripts\publish-win-x64.ps1
```

The published application is self-contained; the target Windows machine does not need a separate .NET installation for Localization Workbench itself.

Before shipping a distribution archive, complete `RELEASE_CHECKLIST.md` and ensure third-party helper binaries are not included.
