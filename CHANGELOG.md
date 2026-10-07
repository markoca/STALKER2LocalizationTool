# Changelog

## 2.0.0-rc.1

- Established the cleaned **Localization Workbench** Version 2 release-candidate baseline on `main`.
- Removed the old `settings.json` persistence system and legacy workspace migration paths.
- Internal workspace/tool paths now resolve dynamically from the running executable directory.
- Added lightweight persistence of validated external GAME and MODS source paths through `user-paths.json`.
- Separated GAME and MODS workflow readiness/status logging with consistent decorated banners.
- MODS scanning is now strictly manual: it starts only from **SCAN MODS**.
- Optimized large archive MODS scanning with **UTOC-only archive discovery**: ZIP/7z/RAR scans no longer materialize `.pak` / `.ucas` payloads up front; those files are materialized lazily only on EXTRACT for containers that actually contain localization.
- Removed the full archive SHA-256 read from MODS discovery and collapsed loose/archive source discovery into a single filesystem traversal; scan logs now include discovery time, total scan time, cache hits and actual `retoc` scans.
- Added bounded parallel container scanning with at most **2 concurrent `retoc list` operations**, while preserving deterministic per-mod aggregation and cancellation; per-container logs now report UTOC hash time, `retoc` time or cache hits.
- Cold archive discovery now logs the time required to materialize each scan-only UTOC, making 7z/RAR decompression bottlenecks visible separately from `retoc` time.
- Entering MODS performs only a one-shot pre-scan source-presence check and reports **MODS FOUND / READY TO SCAN** when appropriate.
- Removed obsolete MODS scan snapshot persistence and related dead code.
- Third-party helper-tool binaries are no longer tracked or distributed; users supply compatible tools under `tools/`.
- GAME action buttons were simplified to **SCAN GAME**, **EXTRACT**, and **BUILD**; the v2 quick user handbook was added and aligned with the current workflow.
- MODS extraction button was simplified from **EXTRACT NEW / CHANGED** to **EXTRACT**.
- Added the first **Industrial Zone** visual pass: chamfered cards/action buttons, a compact GAME localization overview, refined surface hierarchy, and color-coded MODS status text.
- GAME EXTRACT now reports its result directly in the central panel: the generic post-extraction popup was removed and the panel lists the actual translation JSON files created under `Translations/Game`.
- Each extracted GAME language result shows its actual translation JSON filename, keeping the post-extraction list concise and directly tied to `Translations/Game`.
- The pre-scan GAME overview was simplified to **READY TO SCAN** without redundant supported-language or `pakchunk0` source text.
- The new main-window styling remains owner-drawn WinForms to preserve deterministic Windows/Wine rendering without adding a UI framework dependency.
- Extended the **Industrial Zone** styling to the remaining UI: language-selection tiles, GAME extraction result tiles, borderless terminal log, chamfered utility buttons, industrial Settings path fields, Auto Scan switch, primary Save action, and destructive Delete Source Data treatment.
- MODS status messages now render as plain color-coded text instead of badge/button-like controls.
- MODS EXTRACT now completes without a success popup; completion remains visible through the grid status, workflow log and status bar.
- Settings typography now uses the same Segoe UI family as the main window, avoiding a separate Bahnschrift rendering dependency across Windows/Wine.
- Fixed borderless-window dragging: the main and Settings custom title bars are draggable from their non-interactive child surfaces while caption buttons remain clickable.
- Main and Settings window title typography now uses matching **17 pt** sizes for `LOCALIZATION` and `WORKBENCH`, keeping emphasis through weight/color instead of mismatched scale.
- Renamed the runtime workspace folders from **Cached / Editable** to gamer-facing **Source / Translations** across the application, build/extraction pipeline, Settings, documentation and publishers.
- **DELETE SOURCE DATA** now reports that both GAME and MODS source data were reset; GAME and MODS tabs then show their correct next scan/extraction state.
- Added a one-time safe workspace-directory rename for existing installs; old folders are moved only when the new destination does not already exist, and publishers refuse ambiguous old/new folder conflicts rather than merging data.
- Consolidated the current development baseline onto `main` and refreshed release-candidate documentation.

## 1.0.0-rc.7
- Publish helper tools are selected by target runtime, not by build-host OS.
- Windows runtime bundle moved to `tools/win-x64/`.
- Linux can cross-publish the self-contained `win-x64` release without Wine and without executing Windows helper tools.
- The final Windows machine does not require .NET to be installed.
- Removed the unused placeholder `tools/linux-x64/` tree and duplicate root-level helper binaries; `tools/win-x64/` is the only authoritative source bundle.
- Removed obsolete RC6 publish notes and unused legacy WinForms tab/checked-list controls.
- Updated RC7 documentation/checklist to match the current Extracted/Cached/Editable workflow and recovery behavior.

## 1.0.0-rc.6

- Switched MODS packaging to the proven stock-retoc baseline after the full current launch.py build passed with unmodified retoc.
- Removed the `--source-package-map` requirement and all custom retoc build/patch expectations.
- Removed ContainerHeader redirect/FMappedName verification that existed only for the custom retoc identity path.
- Kept the important post-pack safety checks: canonical LocalizationDatabase virtual path, complete original 24-hex chunk/FPackageId, and exact patched RawExport round-trip.
- Project-local `tools/` remains authoritative. Publish copies prebuilt tools and performs no Git, download, Rust/cargo build, or retoc compilation.
- `tools/retoc.exe` can now be the ordinary upstream Windows retoc CLI.
- Manifest schema remains 13; existing RC5 schema-13 extraction caches remain valid.

## v1.0.0-rc.5

- Aligned MODS LocalizationDatabase handling with the current known-good `launch.py` baseline.
- Group aliases only by the complete 24-hex ExportBundleData chunk ID; removed first-16-hex package-prefix collapsing.
- Scan NewContent and OverrideContent localization partners together and resolve the canonical alias only after extraction inspects their SID sets.
- Extraction now records internal Unreal package path, source package identity, directory alias path and per-alias SID counts; manifest schema is 13 and existing Cached workspaces must be re-extracted.
- Prefer a complete `Stalker2/Content/...` alias while preserving the original plugin serializer path/FPackageId.
- Requires a prebuilt patched `retoc.exe` exposing `--source-package-map`; the project no longer builds retoc during deployment.
- Modular and All-in-One builds pass source-package identity maps to retoc and verify canonical path, source chunk/FPackageId, ContainerHeader redirect/FMappedName type, internal package path and exact RawExport payload after packaging.
- Changed modular IoStore names to the `zzzzzzz_ISL_*_P` convention used by the release `launch.py` pipeline.
- Do not emit redundant database assets when the selected-language values already match the editable JSON.
- Removed the custom `app.manifest`; the SDK-generated Windows apphost manifest is used for the RC5 Side-by-Side startup test.
- Source `tools/` is the authoritative deployment bundle. Publish validates and copies prebuilt `retoc.exe`, `repak.exe`, `UAssetGUI.exe`, `Mappings.usmap`, and `S2HOCMM.exe`; no Git, Rust/cargo, or dependency download is used.

## v1.0.0-rc.4

- Renamed the shared JSON shortcut to **Open JSONs**.
- Removed the **Open cached** shortcut from the main footer.
- **Open JSONs** now opens only editable JSON locations: `Editable\Game` on GAME and the root `Editable` folder on MODS.
- No localization pipeline behavior changed.

## v1.0.0-rc.3

- Replaced the shared **Open mods / Open editable** footer behavior with a single **Open JSONs folder** shortcut. On GAME it opens `Editable\Game`; on MODS it opens the root `Editable` folder where mod JSONs are extracted.
- Removed the release version from both the window title bar and the large in-app title. The internal/package version remains `1.0.0-rc.3`.
- Updated English locale/fallback strings, README, and RC regression checklist for the RC3 UI.
- No localization pipeline changes; RC2 remains the scan/extract/build/package baseline.

## v1.0.0-rc.2

- Fixed RC1 workspace migration when `settings.json` already stored the legacy default physical paths in the new `CachedFolder` / `EditableFolder` fields. The loader now rewrites those paths to the current `Cached` / `Editable` defaults before any runtime work starts.
- Safe default-workspace migration now resolves empty-side conflicts automatically: an empty legacy folder is removed, while a populated legacy folder replaces an empty new destination. If both sides contain data, nothing is merged or overwritten and only the new workspace is used for future writes.
- Updated Linux and PowerShell publish migration with the same safe empty-folder rules so republishing cannot keep an empty `Cached` / `Editable` destination while preserving the populated legacy default as the effective workspace.
- GAME status-card cleanup: the JSON row label is now **Available** and a complete extracted language set is displayed as **All JSONs**. Partial sets are shown only as a neutral count (`N / total JSONs`), with no editing-state wording.
- Removed the obsolete `ui.game_jsons` locale key and completed the RC terminology pass.
- Updated the RC regression checklist to verify that legacy default paths never receive new writes after migration.
- Localization extraction/build/packaging pipeline remains unchanged from RC1.
- Updated application/package version to `1.0.0-rc.2`.

## v1.0.0-rc.1

- Declared the feature-complete release candidate and froze the localization pipeline for RC testing.
- Renamed the runtime workspace folders to `Cached` (read-only rebuild cache) and `Editable` (user-editable JSON inputs).
- Added safe automatic migration from the legacy default workspace names. Existing custom paths are preserved; destination conflicts are reported and never merged or overwritten automatically.
- Renamed the build-ready status to **Available** and replaced the GAME panel's editing-path row with a simple **JSONs: Available** indicator. The actual path remains accessible through the folder shortcut/tooltip.
- Updated user-facing terminology, help text, settings labels, logs, and current documentation to use Cached / Editable / Available consistently.
- Added **Open mods**, **Open cached**, **Open editable / Open editable GAME**, and **Open output** shortcuts.
- Added **Reset workspace paths** in Settings for restoring the default Mods / Cached / Editable / Output locations without changing tool or game paths.
- Added best-effort startup cleanup for `.*.staging.*` folders under Cached/Editable and `.work` folders under Output after a six-hour stale-age guard. Persistent user data is never deleted by this cleanup.
- Simplified the MODS UI so it reports LocalizationDatabase assets only; LOCRES remains exclusively a GAME workflow.
- Added clearer post-build summaries with per-language results and the final Output folder.
- Added `RC_CHECKLIST.md` for the regression pass before final 1.0.0.
- Release publishing now refreshes `locales/en.json` and includes README / CHANGELOG / RC checklist beside the executable.
- Updated application/package version to `1.0.0-rc.1`.

## v0.9.1

- Made LOCRES exclusively a GAME workflow. MODS no longer scan `.pak` files for `Game.locres`, no longer extract LOCRES assets, and never build LOCRES packages from mod manifests.
- MODS source discovery now scans only eligible `.utoc` source families; companion `.pak` files are used only when `retoc` needs them for IoStore conversion.
- Removed repak/S2HOCMM requirements from normal MODS scan/build validation.
- GAME final PAK creation now passes the known-good mount point `../../../` explicitly in addition to PAK `V11` and path-hash-seed `1244705156`.
- Added `repak info` verification of the finished GAME PAK mount point and PAK version.
- Added final installable-Pak verification: the finished PAK is unpacked again and its `Stalker2/Content/Localization/Game/<culture>/Game.locres` must be byte-for-byte identical to the S2HOCMM-verified binary.
- GAME now writes the standalone inspection copy as `Game.locres` instead of `Game.<culture>.locres`, matching the actual filename inside the PAK and the proven `launch.py` layout.
- GAME output PAK names use the known-good six-`z` load-order prefix and `_P.pak` suffix.
- Added SHA-256 logging for the verified `Game.locres` and final installable GAME PAK.

## v0.9.0

- Fixed Base Game LOCRES cooking to match the proven `launch.py` behavior exactly: `Ready/Game/<language>.json` is now the authoritative S2HOCMM input instead of being patched/matched against merged shipped LOCRES files first.
- Empty Ready values are omitted before S2HOCMM packing, matching `normalize_s2hocmm_input()` in `launch.py`.
- Base Game LOCRES no longer has a `MatchedKeys == 0` skip path; custom Ready values and keys are passed directly to S2HOCMM.
- Added explicit build logging for the exact Ready JSON path, total entries, non-empty entries, and skipped empty entries.
- MOD LOCRES and LocalizationDatabase build behavior is unchanged.

## v0.8.9

- Replaced the native final `Game.locres` writer with the exact proven `launch.py` strategy: create a flat `<culture>.json`, run `S2HOCMM.exe -Pack`, and take `ModOutput/Stalker2/Content/Localization/Game/<culture>/Game.locres` as the authoritative binary.
- Added S2HOCMM `-Dump` verification of every generated LOCRES key/value before a PAK is accepted.
- Final LOCRES PAK creation remains under this application and uses `repak pack --version V11 --path-hash-seed 1244705156 ModOutput <output>`, deliberately ignoring S2HOCMM's own PAK.
- Final LOCRES PAK verification now requires exactly one entry: `Stalker2/Content/Localization/Game/<culture>/Game.locres`.
- Added `S2HOCMM.exe` to Settings and build prerequisites for LOCRES sources. The default path checks `tools/S2HOCMM.exe` and the common sibling `../S2HOCMM/S2HOCMM.exe` developer layout.
- Linux/PowerShell publish helpers copy S2HOCMM only when explicitly placed in source `tools/`; sibling developer installs are auto-detected at runtime and are not copied, preserving any companion files.
- Added per-process environment support and sets `DOTNET_USENLS=1` for S2HOCMM, matching the working Wine launch setup.
- Extraction schema stays at 12 because this changes only the LOCRES build backend; existing Extracted/Ready workspaces remain valid.

## v0.8.8

- MODS scanning is now filename-gated before `retoc`/`repak`: only source files whose extensionless filename ends with `_OC_50`, `_NC`, `B_P`, `_OC`, or `-OverrideContent` are eligible. All other `.utoc`/`.pak` files are ignored for mod-localization scanning.
- Source selection now happens before localization asset discovery, so lower-priority NewContent/fallback packages are never opened when an OverrideContent package for the same mod is available.
- OverrideContent is authoritative at the source-family level. Priority is `_OC_50` / `-OverrideContent` / `_OC`, then `_NC`, then `B_P`; one deterministic source family is scanned per mod.
- Direct files in the MODS root now strip `_OC_50`, `_OC`, `_NC`, `B_P`, and `-OverrideContent` when inferring the mod name, ensuring related variants group as one mod.
- Added scan logging for eligible/ignored source counts and the chosen source family per mod.
- Bumped extraction manifest schema to 12 so previous workspaces are re-evaluated under the stricter source-selection rule. Existing Ready translation JSONs remain untouched.

## v0.8.7

- MODS now treats OverrideContent as authoritative whenever a single mod resolves to more than one LocalizationDatabase asset. OverrideContent is identified primarily by the `Stalker2/Content/...` virtual asset path, not by the physical container filename.
- If 2 or more logical DB assets are discovered and at least one comes from an OverrideContent container, the scanner keeps exactly one deterministic OverrideContent DB and suppresses every other DB from extraction/build/UI counting.
- The decision no longer depends on database filename, `.utoc` filename (`OverrideContent`, `_OC`, custom names, etc.), Zen chunk ID, NewContent filename, or the total number of duplicate DB assets.
- If several OverrideContent DB assets are present, one canonical OverrideContent asset is selected deterministically (base-content virtual path preferred, then virtual path/chunk ordering), guaranteeing `DB 1` for the duplicated mod-localization layout.
- Suppressed source containers still participate in source fingerprinting, so updates to any half of the duplicated layout invalidate stale extraction correctly.
- Bumped the extraction manifest schema to 11 so previous multi-DB mod manifests are regenerated once. Existing Ready translation JSON files remain untouched.

## v0.8.6

- GAME/Base Game scanning is now restricted to `pakchunk0` files in the configured game `Paks` folder.
- The GAME scanner no longer walks every base-game `.utoc` and `.pak`, preventing unrelated or duplicate localization assets from other chunks from entering the base-game workspace.
- Both `pakchunk0*.utoc` (LocalizationDatabase) and `pakchunk0*.pak` (`Game.locres`) are considered; MODS scanning is unchanged.
- Added an explicit scan-log line showing how many pakchunk0 IoStore and PAK containers were selected.
- Bumped the extraction manifest schema to 10 so existing GAME extractions are regenerated once using the narrowed source set. Existing `Ready\Game` JSON files remain untouched.

## v0.8.5

- Mods that expose exactly two LocalizationDatabase assets as a NewContent + OverrideContent pair now use the OverrideContent database only, even when the two Zen chunk IDs do not share the same first 16 characters.
- The two-database preference is intentionally limited to exactly two logical DB assets; single-database mods and mods with three or more DB assets are left untouched.
- Source fingerprinting still includes the suppressed NewContent container so changes to either half of the pair correctly invalidate extraction.
- Bumped the extraction manifest schema to 9 so v0.8.4 extractions that may contain both databases are rebuilt once under the new rule. Existing Ready translation JSON files are unaffected.

## 0.8.4

- Deduplicate LocalizationDatabase assets by the first 16 hexadecimal characters of their 24-character Zen chunk ID, treating that prefix as the package identity.
- NewContent / OverrideContent database pairs that share the same Zen package prefix now collapse to one logical database asset instead of being extracted and built twice.
- When such a pair is found, the `Stalker2/Content` / OverrideContent alias is preferred as the canonical source; the discarded source remains recorded as an alias for fingerprint/update detection.
- Exact full-chunk duplicate aliases continue to be deduplicated and are logged separately from package-prefix duplicates.
- Added conservative validation so prefix grouping is used only for well-formed 24-character hexadecimal Zen chunk IDs.
- Advanced the extraction manifest schema to version 8 so existing v0.8.3 workspaces are re-extracted with the new one-database-per-package rule. Existing `Ready` translations are still preserved.

## 0.8.3

- Removed the damaged/stale `UAssetGUI.exe` binary that produced `.NET` `BundleExtractionFailure` / `Arithmetic overflow while reading bundle` under Wine before any asset could be parsed.
- Added Linux/CachyOS and PowerShell fetch helpers that download pinned upstream UAssetGUI v1.1.0 and verify SHA-256 `b7d75c0893f1a60e565853ae638bc21f2416cd12c2d9d854e297abb87ceb3263` before installing it into `tools`.
- Publish and dev helpers now prepare the verified UAssetGUI dependency automatically instead of trusting a stale executable already present in the source/runtime tree.
- Runtime explicitly rejects the known-bad v0.8.0-v0.8.2 UAssetGUI hash `e9b953245fd3716545558d751a8855d14490cd0e9e377a828e5ab4e0f34e7109`.
- Fixed UAssetGUI v1.1.0 mappings invocation: the selected `.usmap` is installed under `%LOCALAPPDATA%\UAssetGUI\Mappings\LocalizationWorkbench.usmap` and the CLI receives the mapping name rather than an unsupported full mappings path.
- Preserved the v0.8.1 `MatchedSids` build fix and manifest schema version 7; existing v0.8.x Extracted/Ready workspaces remain compatible.

## 0.8.2

- Fixed Windows publish helpers preserving stale or damaged bundled tool binaries in `publish\win-x64\tools` across releases.
- Publish now preserves runtime data and any extra custom tool files, but always refreshes `retoc.exe`, `UAssetGUI.exe`, `Mappings.usmap` and `repak.exe` from the source `tools` directory.
- Added a targeted UAssetGUI diagnostic for .NET single-file bundle failures (`0x8000809F` / `-2147450721`) so extraction identifies the tool/runtime problem instead of implying that the extracted `.uasset` is corrupt.
- Documented the bundled UAssetGUI v1.1.0 SHA-256 and the Linux/Wine .NET 8 Desktop Runtime requirement.
- Kept manifest schema version 7; existing v0.8.x Extracted/Ready workspaces remain compatible.

## 0.8.1

- Fixed Modular database builds incorrectly skipping assets when Ready translations matched SIDs whose source values were already correct.
- Fixed All-in-One database builds using changed SIDs instead of matched SIDs as the inclusion criterion.
- Database overlays now preserve matched localization assets even when `changed=0`, matching the existing LOCRES build semantics.
- Reworded no-output build messages to distinguish genuinely unmatched translations from values that already matched the requested language.
- Empty per-mod and All-in-One output directories are now removed when no buildable localization entries match.
- Kept manifest schema version 7 so existing v0.8.0 Extracted/Ready workspaces remain valid.

## 0.8.0

- Stretched the build-language checklist across the available window width and removed its horizontal scrollbar.
- Changed GAME editing and building to use `Ready\Game`; its help text and workflow tip now point to that location.
- The GAME **Editable / ready JSONs** field displays `-` until at least one language JSON exists.
- Extraction now keeps a canonical read-only workspace in `Extracted` and creates a 1:1 copy in the matching `Ready` folder only when that folder does not already exist.
- Existing Ready mod folders are never overwritten by re-extraction.
- Changed the hint below **READY TRANSLATIONS** to **Ready JSONs can be built**.
- Advanced the extraction manifest schema so existing v0.7 workspaces are re-extracted into the new Ready workflow.

## 0.7.0

- Fresh installs start without a packaged `settings.json`; first-run paths resolve beside the application EXE and are then saved locally for later launches.
- English is the only build language selected by default on a fresh install.
- Expanded the MODS list by reducing unused padding and the shared language/log areas.
- Changed **Open Extracted** to open the normal `Ready` folder.
- Renamed the user-facing application to **Localization Workbench** and removed the version number from the window header.

## 0.6.0

- Moved `settings.json` beside the application EXE for a fully portable runtime configuration.
- Replaced the single build-language selector with a multi-select checklist; only checked languages are built and no build is allowed with none selected.
- Made `Extracted\Game\<language>.json` both editable and directly buildable, with no separate GAME copy under `Ready`.
- Kept the MODS workflow isolated to the normal `Ready` folder.
- Switched Windows publishing to a self-contained single-file EXE.
- Publish scripts now preserve local settings, verified tools, and all runtime work folders while replacing old application/framework files.

## 0.5.0

- Split the main window into separate **GAME** and **MODS** workflows.
- Added base-game scanning, extraction of all supported language JSON files, and rebuilding from `Ready\Game\<language>.json`.
- Renamed **BUILD MONOLITHIC OVERRIDE** to **BUILD ALL-IN-ONE** and simplified its help text.
- First-run workspace and tool paths now default beside `LocalizationWorkbench.exe`.
- Removed duplicate-alias accounting from the Details column; chunk alias deduplication remains in the scan log.
- Limited repak extraction to discovered `Game.locres` entries instead of unpacking an entire source PAK.

## 0.4.0

- Removed the interface-language selector and Serbian UI localization; the application UI is now always English.
- Extraction now writes one editable flat JSON per supported language (`english.json`, `serbian.json`, and so on).
- Removed redundant `localization.json`, `translations.json`, and per-LOCRES human-readable dump files from new workspaces.
- Extracted workspace folders now use readable mod names without hash-like suffixes; numeric suffixes are used only for real name collisions.
- Ready-file matching is language-aware and follows the selected build language.
- Replaced the single build action with **BUILD MODULAR** and **BUILD MONOLITHIC OVERRIDE**.
- Modular mode preserves the existing per-mod database/LOCRES output.
- Monolithic Override mode combines changed `Stalker2/Content` LocalizationDatabase assets into one verified IoStore package and rejects chunk/path collisions.

## 0.3.0

- Removed the S2HOCMM dependency completely.
- Added an in-app native Unreal LOCRES reader/writer for versions 0-3.
- LOCRES extraction now uses `repak` only to unpack the source PAK; the app reads `Game.locres` directly.
- LOCRES builds are written directly by the app and then packed with `repak`.
- Preserves original namespace hashes, key hashes and source-string hashes instead of regenerating them for existing entries.
- Added native LOCRES round-trip verification before the final PAK is accepted.
- Flat translation keys that occur in multiple LOCRES namespaces can be disambiguated with `namespace::key`.
- Unmatched translation keys are ignored by the LOCRES path, allowing one `translations.json` to feed LOCRES and LocalizationDatabase assets together.
- External toolset is now only `retoc.exe`, `UAssetGUI.exe`, `Mappings.usmap` and `repak.exe`.

## 0.2.0

- Renamed the application to **S.T.A.L.K.E.R. 2 Localization Tool**.
- Renamed the project/executable identifier to `LocalizationWorkbench`.
- Added standard `Game.locres` discovery inside `.pak` files.
- Added LOCRES JSON workspace generation and build-language selection.
- Added final LOCRES PAK creation with S.T.A.L.K.E.R. 2 V11/path-hash-seed settings.
- A single `translations.json` can feed LocalizationDatabase assets, Game.locres, or both.
- Added a Linux/CachyOS `publish-win-x64.sh` helper.
- Added `EnableWindowsTargeting=true` for cross-publishing the WinForms app from Linux.
