# Release candidate checklist

## Publish

- [ ] `tools/win-x64/` contains `retoc.exe`, `repak.exe`, `UAssetGUI.exe`, `Mappings.usmap`, and `S2HOCMM.exe`.
- [ ] Linux `publish-win-x64.sh` succeeds without executing Windows helper binaries.
- [ ] Windows `publish-win-x64.ps1` succeeds with the same target bundle.
- [ ] Published runtime contains only the expected `tools/` bundle and no duplicate `tools/win-x64/` nesting.
- [ ] `Localization Workbench.exe` starts on Windows and under the development Wine prefix.

## GAME

- [ ] SCAN GAME reaches **LOCALIZATION READY FOR EXTRACTION** for a new/changed source.
- [ ] EXTRACT creates `Cached/Game` and `Editable/Game`.
- [ ] A valid existing extraction reaches **LOCALIZATION READY FOR BUILD** without rescanning after extraction.
- [ ] Deleting `Editable/Game` while keeping valid `Cached/Game` makes EXTRACT available again and reports **LOCALIZATION READY FOR EXTRACTION**.
- [ ] Cached -> Editable recovery does not require a new source extraction.
- [ ] BUILD verifies S2HOCMM output and the final repak package.

## MODS

- [ ] MODS is not scanned at startup or while GAME is active.
- [ ] Entering the MODS tab triggers MODS scanning.
- [ ] SCAN MODS discovers loose and ZIP/7z/RAR sources.
- [ ] NewContent containers are ignored and OverrideContent is used.
- [ ] Nexus download IDs are not shown in mod display names and detected versions are normalized.
- [ ] New/changed mods show **Needs extraction**.
- [ ] Successful extraction changes the status to **Extracted** without an automatic second scan.
- [ ] Removing Editable translation files while keeping valid Cached data enables EXTRACT recovery.
- [ ] Recovery copies only missing Editable files and never overwrites existing edits.
- [ ] Modular and All-in-One builds complete and round-trip verification passes.
- [ ] Already-correct target-language values do not emit redundant physical overlays.

## Workspace and UI

- [ ] DELETE CACHE removes Cached state and forces MODS to be scanned again.
- [ ] GAME / MODS workflow buttons remain step-aware.
- [ ] Custom title bar, window controls and spinning radiation mark render correctly.
