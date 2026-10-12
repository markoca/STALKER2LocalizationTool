# Localization Workbench — Quick User Handbook

**Version 2.0.0 · Windows x64**

Extract, edit and build S.T.A.L.K.E.R. 2 localization. **GAME** handles the original game's text; **MODS** handles localization contained in supported mods. Localization Workbench **does not translate text automatically**.

## 1. Install and set up

1. Extract Localization Workbench into a writable folder, such as `C:\LocalizationWorkbench`.
2. Download the required third-party tools and place them in the `tools` folder beside `Localization Workbench.exe`:

   | File | Tested baseline / source |
   | --- | --- |
   | `retoc.exe` | [retoc v0.1.5](https://github.com/trumank/retoc/releases) |
   | `repak.exe` | [repak v0.2.3](https://github.com/trumank/repak/releases) |
   | `UAssetGUI.exe` | [UAssetGUI v1.1.0](https://github.com/atenfyr/UAssetGUI/releases) |
   | `S2HOCMM.exe` | [S2HOCMM v2.3](https://gitlab.com/PatrykPniewski/s2hocmm/-/releases) |
   | `Mappings.usmap` | Game-compatible UE mappings (see [S.T.A.L.K.E.R. 2 mappings](https://www.nexusmods.com/stalker2heartofchornobyl/mods/2356)) |

   Use compatible Windows x64 executables. The application does not bundle these tools. Keep the mappings filename exactly `Mappings.usmap`, and update its contents when the game version requires different mappings.

3. Start `Localization Workbench.exe` and open **Settings**:
   - **Game Paks folder:** point to `...\Stalker2\Content\Paks`.
   - **Mods folder:** select the folder containing original mod files or archives; the default is `Mods` beside the application.
   - **Interface language:** choose the Workbench UI language independently of the languages you will build.
4. Save your settings. Valid source paths are remembered after a successful scan.

The Workbench itself is self-contained and needs no separate .NET installation. Third-party programs may need their own runtimes; for example, UAssetGUI v1.1.0 may require the .NET 8 Desktop Runtime.

## 2. GAME — translate the original game

1. In **GAME**, click **SCAN GAME** to discover game localization.
2. Click **EXTRACT**. The Workbench prepares JSON files for all 18 supported game languages in `Translations\Game`.
3. Click **Open JSONs** and edit the desired language file, for example `Translations\Game\serbian.json`.
4. Select one or more **Build languages** and click **BUILD**.
5. Open `Output\<Language>\Game` and copy the generated **.pak** to the game's `Stalker2\Content\Paks\~mods` folder.

The build also creates `Game.locres` for inspection; **install the .pak, not the standalone .locres**.

## 3. MODS — translate supported mods

Supported inputs include loose mod files, extracted mod folders, and **ZIP, 7z or RAR** archives. Loose IoStore mods need their matching `.pak`, `.utoc` and `.ucas` files. The Workbench processes supported **OverrideContent / LocalizationDatabase** localization; not every mod contains supported text.

1. Put the original mod files or archives in the configured **Mods folder**.
2. Open **MODS** and click **SCAN MODS**. Scanning is **manual**: opening the tab, changing Settings or adding files does not start a full scan.
3. Mods with new or updated localization show **Needs extraction**. Click **EXTRACT** to process pending sources.
4. Click **Open JSONs** and edit `Translations\<ModName>\<language>.json`.
5. Select one or more **Build languages**, choose a packaging setting, and build:

   | Setting / action | Result |
   | --- | --- |
   | **Pack only edited translations** (default) | Package only localization databases with changed text for the selected language. |
   | **Pack all translations** | Include all buildable databases with matching editable text, even when unchanged. |
   | **BUILD MODULAR** | A separate package for each buildable mod in `Output\<Language>\<ModName>`. |
   | **BUILD ALL-IN-ONE** | One combined mod-localization package per language in `Output\<Language>\All-in-One`. |

   The packaging setting applies to both MODULAR and ALL-IN-ONE builds. If no text differs from the extracted source under the default setting, no redundant override needs to be generated.

6. Copy the **complete generated package** (`.pak` + `.utoc` + `.ucas`) into `Stalker2\Content\Paks\~mods`.

Use **either** the individual MODULAR packages **or** the corresponding ALL-IN-ONE package for the same content, not both. Remove outdated packages you previously generated when replacing them with new builds.

### When a mod is updated

1. Replace its old source files/archive in your **Mods folder**.
2. Click **SCAN MODS**, then **EXTRACT** for items marked **Needs extraction**.
3. Compare your existing JSON translation against the updated source. **Existing translation files are preserved, but new or changed source text is not automatically merged into your translation.**
4. Make any needed translation changes and build again.

If a translation JSON is missing but valid extracted source data still exists, **EXTRACT** can restore the missing file without overwriting the others.

## 4. Editing JSON safely

Edit **text values**, not localization keys. Keep the JSON structure valid, including quotes, commas and escape sequences.

```json
{
  "sid_some_text": "Your translated text"
}
```

Keep `sid_some_text` unchanged. For longer projects, back up `Translations` before major edits or source updates.

### Where files belong

| Folder | Purpose |
| --- | --- |
| `Mods` | Original mod sources used for scanning. |
| `Source\Game` / `Source\<ModName>` | Internal extracted data used for rebuilding. **Do not edit.** |
| `Translations\Game` / `Translations\<ModName>` | **Your editable language JSON files.** |
| `Output\<Language>\... ` | Verified packages to install into the game. |
| `tools` | Required third-party executables and `Mappings.usmap`. |
| `Languages` | Workbench interface language dictionaries, not game translations. |

All workspace folders are located beside the running executable. The app uses `user-paths.json` for remembered settings. To move a portable workspace, move its data folders with the executable; recheck external source paths afterward.

**Settings → DELETE SOURCE DATA** deletes extracted `Source` data but **does not delete `Translations` or `Output`**. You will need to extract again; for MODS, click **SCAN MODS** first.

## 5. Troubleshooting

| Problem | What to check |
| --- | --- |
| **SCAN GAME** cannot find localization | Confirm **Game Paks folder** points to `Stalker2\Content\Paks` and that the required tools are in `tools`. |
| MODS list is not updated | Click **SCAN MODS** after adding, removing or updating sources. |
| A mod shows no supported localization | It may not contain a supported OverrideContent localization database. Confirm complete IoStore file sets for loose sources. |
| Extraction fails | Check `UAssetGUI.exe`, a game-compatible `Mappings.usmap`, tool runtime dependencies and the application log. |
| **BUILD** is unavailable | Finish extraction and select at least one **Build language**. |
| No MODS package is produced | With **Pack only edited translations**, unchanged text is intentionally skipped. Confirm that your JSON contains changes, or choose **Pack all translations**. |
| Translation does not appear in-game | Install only files from `Output`, keep each MODS triplet together, and remove conflicting older generated packages. |

When reporting a problem, include the **Workbench version**, GAME or MODS workflow, selected language, mod name/version (if relevant), source format (loose/ZIP/7z/RAR), and the **application log with the exact error**.

## 6. Useful distinctions

- The **Build languages** selector controls which **game translation languages** to package; **Settings → Interface language** changes the Workbench UI. Missing interface translations fall back to English.
- **GAME** generates a single installable `.pak`; **MODS** generates a `.pak/.utoc/.ucas` set.
- **SCAN MODS** is always manual. Workspace **Auto Refresh** is not automatic scanning of mod archives.
- Only packages from **Output** belong in the game's `~mods` folder; never install files directly from **Source** or **Translations**.
