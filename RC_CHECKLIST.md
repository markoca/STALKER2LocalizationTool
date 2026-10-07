# Release candidate checklist

## Publish

- [ ] Source/release does not contain third-party helper-tool binaries.
- [ ] Linux `publish-win-x64.sh` succeeds without requiring helper tools.
- [ ] Windows `publish-win-x64.ps1` succeeds without requiring helper tools.
- [ ] Published runtime creates/preserves `tools/` and includes only the tool provisioning README unless the developer supplied local test tools.
- [ ] A clean distribution does not contain `retoc.exe`, `repak.exe`, `UAssetGUI.exe`, `Mappings.usmap`, or `S2HOCMM.exe`.
- [ ] `Localization Workbench.exe` starts on Windows and under the development Wine prefix.

## GAME

- [ ] GAME action buttons are labeled exactly **SCAN GAME**, **EXTRACT**, and **BUILD**.
- [ ] SCAN GAME reaches **LOCALIZATION READY FOR EXTRACTION** for a new/changed source.
- [ ] EXTRACT creates `Cached/Game` and `Editable/Game`.
- [ ] A valid existing extraction reaches **LOCALIZATION READY FOR BUILD** without rescanning after extraction.
- [ ] Deleting `Editable/Game` while keeping valid `Cached/Game` makes EXTRACT available again and reports **LOCALIZATION READY FOR EXTRACTION**.
- [ ] Cached -> Editable recovery does not require a new source extraction.
- [ ] BUILD verifies S2HOCMM output and the final repak package.

## MODS

- [ ] MODS extraction action is labeled exactly **EXTRACT**.
- [ ] MODS is not scanned at startup or while GAME is active.
- [ ] Before the first MODS scan in a session, entering the MODS tab shows **MODS FOUND** and **READY TO SCAN** once when mod sources are present; tab switching does not duplicate the banners.
- [ ] MODS scanning starts only when the user clicks **SCAN MODS**.
- [ ] SCAN MODS discovers loose and ZIP/7z/RAR sources.
- [ ] MODS localization uses supported OverrideContent-side containers only.
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
- [ ] Industrial Zone chamfered cards and action buttons render cleanly with no clipped text at 100%, 125% and 150% Windows scaling.
- [ ] GAME localization overview shows only **READY TO SCAN** before scanning, without redundant supported-language or `pakchunk0` text.
- [ ] After GAME EXTRACT, the central panel shows **EXTRACTED** and lists the actual editable language JSON files; no extraction-complete MessageBox is shown.
- [ ] Each extracted GAME language result shows the correct editable JSON filename.
- [ ] Before extraction is complete, a successful GAME scan may report **LOCALIZATION FOUND** with extraction readiness, but must not show stale extraction results.
- [ ] MODS status badges remain readable for every status and selected row state.
