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
- [ ] EXTRACT creates `Source/Game` and `Translations/Game`.
- [ ] A valid existing extraction reaches **LOCALIZATION READY FOR BUILD** without rescanning after extraction.
- [ ] Deleting `Translations/Game` while keeping valid `Source/Game` makes EXTRACT available again and reports **LOCALIZATION READY FOR EXTRACTION**.
- [ ] Source -> Translations recovery does not require a new source extraction.
- [ ] BUILD verifies S2HOCMM output and the final repak package.

## MODS

- [ ] MODS extraction action is labeled exactly **EXTRACT**.
- [ ] MODS is not scanned at startup or while GAME is active.
- [ ] Before the first MODS scan in a session, entering the MODS tab shows **MODS FOUND** and **READY TO SCAN** once when mod sources are present; tab switching does not duplicate the banners.
- [ ] MODS scanning starts only when the user clicks **SCAN MODS**.
- [ ] SCAN MODS discovers loose and ZIP/7z/RAR sources.
- [ ] ZIP/7z/RAR SCAN MODS materializes only `.utoc` on the normal path and reads localization database paths/chunk IDs directly from its directory index.
- [ ] If direct UTOC indexing is unsupported, only that container's `.ucas` is materialized and stock `retoc list` is used as fallback; EXTRACT later materializes the required `.pak` / `.ucas` payloads normally.
- [ ] A second unchanged archive scan reuses discovery/scan caches and logs discovery time, total scan time, cache reuse, direct UTOC-index counts and `retoc` fallback counts.
- [ ] Initial MODS scanning runs at most 2 concurrent container scans and remains cancellable without converting cancellation into scan errors.
- [ ] Cold-scan logs show per-container UTOC hash / direct-index timing; any compatibility fallback is explicit and reports its `retoc` timing.
- [ ] MODS localization uses supported OverrideContent-side containers only.
- [ ] Nexus download IDs are not shown in mod display names and detected versions are normalized.
- [ ] New/changed mods show **Needs extraction**.
- [ ] Successful extraction changes the status to **Extracted** without an automatic second scan.
- [ ] Removing translation files while keeping valid Source data enables EXTRACT recovery.
- [ ] Recovery copies only missing Translation files and never overwrites existing edits.
- [ ] Modular and All-in-One builds complete and round-trip verification passes.
- [ ] Already-correct target-language values do not emit redundant physical overlays.

## Workspace and UI

- [ ] DELETE SOURCE DATA removes Source state and forces MODS to be scanned again.
- [ ] After DELETE SOURCE DATA, the log reports that GAME and MODS source data were reset; GAME shows READY TO SCAN or READY FOR EXTRACTION as appropriate, while MODS shows READY TO SCAN MODS.
- [ ] GAME / MODS workflow buttons remain step-aware.
- [ ] Custom title bar, window controls and spinning radiation mark render correctly.
- [ ] Main and Settings windows can be dragged from the title area, including over title labels/panels, while minimize/maximize/close remain clickable.
- [ ] Main and Settings title words use the same 17 pt size without clipping at 100%, 125% and 150% scaling.
- [ ] Industrial Zone chamfered cards and action buttons render cleanly with no clipped text at 100%, 125% and 150% Windows scaling.
- [ ] GAME localization overview shows only **READY TO SCAN** before scanning, without redundant supported-language or `pakchunk0` text.
- [ ] After GAME EXTRACT, the central panel shows **EXTRACTED** and lists the actual translation JSON files; no extraction-complete MessageBox is shown.
- [ ] Each extracted GAME language result shows the correct translation JSON filename.
- [ ] Before extraction is complete, a successful GAME scan may report **LOCALIZATION FOUND** with extraction readiness, but must not show stale extraction results.
- [ ] MODS status messages remain readable as plain color-coded text for every status and selected row state.
- [ ] Build-language selection tiles clearly distinguish unchecked, hover, checked and disabled states at 100%, 125% and 150% scaling.
- [ ] GAME extracted-file result tiles render filenames without clipping and retain full-path tooltips.
- [ ] LOG remains readable and scrollable with the borderless terminal treatment on Windows and Wine.
- [ ] Settings path fields, Browse buttons, AUTO indicators, Auto Scan switch, Save/Cancel and Delete Source Data render consistently with the Industrial Zone theme.
