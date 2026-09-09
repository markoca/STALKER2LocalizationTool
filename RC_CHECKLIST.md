# 1.0.0-rc.4 Regression Checklist

Use this checklist against a clean RC build before promoting to final 1.0.0.

## A. Fresh runtime

- [ ] Publish to a new/empty `publish/win-x64` directory.
- [ ] `Mods`, `Cached`, `Editable`, and `Output` are created.
- [ ] Fresh runtime does not create legacy `Extracted` or `Ready` folders.
- [ ] No development workspace or test output is bundled.
- [ ] UAssetGUI is present and passes the pinned hash check.
- [ ] Application/window title is `The STALKER2 Localization Tool` and does not include a version string.
- [ ] Settings open without errors.
- [ ] Default workspace paths point to Mods / Cached / Editable / Output.

## B. Upgrade migration

Test with a copy of an older runtime.

- [ ] Legacy default extraction workspace migrates to `Cached` when `Cached` does not exist.
- [ ] Legacy default editable workspace migrates to `Editable` when `Editable` does not exist.
- [ ] Existing JSON edits survive migration byte-for-byte.
- [ ] Existing settings survive migration.
- [ ] Custom workspace paths are preserved.
- [ ] If both old and new workspace folders contain data, nothing is merged or overwritten.
- [ ] The conflict is reported to the user.
- [ ] If the new destination is empty and the legacy default contains data, the legacy workspace is moved into the new name safely.
- [ ] If the legacy default is empty, it is removed.
- [ ] After startup, `settings.json` does not point Cached/Editable at the legacy default paths.
- [ ] New extraction/editable data never appears under legacy `Extracted` / `Ready`.

## C. GAME scan

- [ ] Configure the real game `Stalker2/Content/Paks` directory.
- [ ] GAME scan inspects pakchunk0 only.
- [ ] GAME locres sources are detected.
- [ ] GAME availability row label is `Available`.
- [ ] With the complete GAME language set present, its value is `All JSONs`.
- [ ] A partial set is shown as an `N / total JSONs` count, never as an Editable/Ready state.
- [ ] The GAME status card does not expose old Ready/Extracted terminology.

## D. GAME extraction

- [ ] Extract GAME once.
- [ ] `Cached/Game` is created.
- [ ] `Editable/Game` is created.
- [ ] Every supported language JSON expected from the source is present.
- [ ] Re-extract after changing the source fingerprint.
- [ ] Existing `Editable/Game` files are not overwritten.

## E. GAME build

Pick at least Serbian and one other language.

- [ ] Modify a clearly visible GAME string in `Editable/Game/<language>.json`.
- [ ] Build only that language.
- [ ] S2HOCMM verification passes.
- [ ] Final PAK verification passes.
- [ ] Final PAK contains only `Stalker2/Content/Localization/Game/<culture>/Game.locres`.
- [ ] PAK mount point is `../../../`.
- [ ] PAK version is V11.
- [ ] Path-hash seed is `1244705156`.
- [ ] Standalone `Game.locres` matches the PAK payload.
- [ ] Install only the built GAME PAK in `~mods`.
- [ ] The edited string is visible in-game.
- [ ] Repeat for a second language to confirm the workflow is language-generic.

## F. MODS scan

- [ ] MODS ignores Game.locres PAK localization entirely.
- [ ] Only eligible source-family filename endings are scanned.
- [ ] `_OC_50`, `_OC`, and `-OverrideContent` beat `_NC` and `B_P`.
- [ ] A mod with an OverrideContent family is listed with the expected database count.
- [ ] No LOCRES type/count appears in the MODS UI.

## G. MODS extraction

- [ ] Extract a known LocalizationDatabase mod.
- [ ] `Cached/<mod>` is created.
- [ ] `Editable/<mod>` is created.
- [ ] Re-extraction does not overwrite existing editable JSONs.
- [ ] Updated source containers correctly trigger `Needs extraction`.

## H. MODS Modular build

- [ ] Modify one mod JSON value.
- [ ] Build Modular for one language.
- [ ] `.pak/.utoc/.ucas` output is produced.
- [ ] Verification passes.
- [ ] Install the localization overlay with the source mod.
- [ ] The edited string is visible in-game.
- [ ] A matched asset with `changed=0` still produces the required overlay.

## I. MODS All-in-One build

- [ ] Select at least two supported editable mod localizations.
- [ ] Build All-in-One.
- [ ] One combined IoStore package is produced.
- [ ] Verification passes.
- [ ] Intentional package/path collisions are reported rather than silently overwritten.
- [ ] Install and verify strings from multiple included mods in-game.

## J. UI / cleanup

- [ ] MODS editable section says `EDITABLE JSONS`.
- [ ] Buildable status is `Available`.
- [ ] `Open JSONs` is visible on both GAME and MODS.
- [ ] On GAME, `Open JSONs` opens `Editable/Game`.
- [ ] On MODS, `Open JSONs` opens `Editable`.
- [ ] Open output works.
- [ ] Reset workspace paths updates only Mods / Cached / Editable / Output.
- [ ] Build completion dialog includes per-language result summary and Output path.
- [ ] Stale `.work` folders under Output older than six hours are cleaned on startup.
- [ ] Stale top-level staging folders under Cached/Editable older than six hours are cleaned on startup.
- [ ] Recent transient folders are not removed by startup cleanup.

## K. Final release gate

- [ ] No blocker or data-loss bug remains.
- [ ] No localization-pipeline changes were introduced after this regression pass.
- [ ] README matches the shipped UI.
- [ ] CHANGELOG contains RC notes.
- [ ] Internal/package version is changed from `1.0.0-rc.4` to `1.0.0` only after all required checks pass; the main title remains version-free.
