# LOCALIZATION WORKBENCH
## VERSION 2 — QUICK USER HANDBOOK

**v2.0.0-rc.1**

**Extract. Edit. Build.**

Use the **GAME** workflow for original S.T.A.L.K.E.R. 2 localization and the **MODS** workflow for localization supplied by supported mods.

> **IMPORTANT**
>
> Localization Workbench is **not** an automatic translator.
>
> It extracts localization into translation JSON files, lets you edit the text, and builds game-ready localization packages from your changes.

---

## 1. INSTALLATION

### 1.1 Extract Localization Workbench

Extract Localization Workbench to a writable folder.

Example:

```text
C:\LocalizationWorkbench
```

### 1.2 Supply the required third-party tools

Localization Workbench does **not** include or distribute the required third-party tools.

Put compatible copies under:

```text
LocalizationWorkbench\tools\
```

Required files:

```text
retoc.exe
repak.exe
UAssetGUI.exe
S2HOCMM.exe
Mappings.usmap
```

Expected layout:

```text
Localization Workbench.exe
tools\
    retoc.exe
    repak.exe
    UAssetGUI.exe
    S2HOCMM.exe
    Mappings.usmap
```

Download the tools from their original project/release pages:

**retoc v0.1.5**

```text
https://github.com/trumank/retoc/releases
```

Download the Windows x64 release and place the executable as:

```text
tools\retoc.exe
```

**repak v0.2.3**

```text
https://github.com/trumank/repak/releases
```

Download the Windows x64 release and place the executable as:

```text
tools\repak.exe
```

**UAssetGUI v1.1.0**

```text
https://github.com/atenfyr/UAssetGUI/releases
```

Use the stable v1.1.0 release and place:

```text
tools\UAssetGUI.exe
```

**S2HOCMM v2.3**

Nexus Mods:

```text
https://www.nexusmods.com/stalker2heartofchornobyl/mods/540
```

Original project / releases:

```text
https://gitlab.com/PatrykPniewski/s2hocmm
https://gitlab.com/PatrykPniewski/s2hocmm/-/releases
```

Place:

```text
tools\S2HOCMM.exe
```

**Mappings.usmap**

Mappings must match the installed S.T.A.L.K.E.R. 2 game version.

For the current tested game baseline **2.0.4**:

```text
https://www.nexusmods.com/stalker2heartofchornobyl/mods/2356
```

Place the mapping as:

```text
tools\Mappings.usmap
```

If the game is updated beyond 2.0.4, obtain a mapping generated for that game build instead of assuming the old mapping is compatible.

UAssetGUI v1.1.0 may require the .NET 8 Desktop Runtime when that dependency is not already available.

### 1.3 Start the application

Run:

```text
Localization Workbench.exe
```

### 1.4 Configure paths if required

Open **Settings**.

**GAME PAKS FOLDER**

Select the game's Paks directory.

Example:

```text
...\S.T.A.L.K.E.R. 2 Heart of Chornobyl\Stalker2\Content\Paks
```

**MODS FOLDER**

Select the folder containing the original mod files or archives you want Localization Workbench to process.

The default Mods folder is:

```text
Mods\
```

beside `Localization Workbench.exe`.

After a successful scan, valid GAME and MODS source paths are remembered for future launches.

---

## 2. GAME LOCALIZATION

Use **GAME** for original S.T.A.L.K.E.R. 2 localization.

The GAME action buttons are:

```text
SCAN GAME
EXTRACT
BUILD
```

The workflow is:

```text
SCAN GAME
    ->
EXTRACT
    ->
Edit JSONs in Translations\Game\
    ->
Select language(s)
    ->
BUILD
```

### STEP 1 — SCAN GAME

Click:

```text
SCAN GAME
```

When game localization is found, the log reports:

```text
=========== FOUND GAME LOCALIZATION ===========
```

If extraction is required:

```text
=========== LOCALIZATION READY FOR EXTRACTION ===========
```

### STEP 2 — EXTRACT

Click:

```text
EXTRACT
```

Localization Workbench extracts all 18 supported game languages.

Translations JSON files are created under:

```text
Translations\Game\
```

Example:

```text
Translations\Game\serbian.json
```

Internal rebuild data is stored separately under:

```text
Source\Game\
```

After successful extraction, the log reports:

```text
=========== LOCALIZATION EXTRACTION DONE ===========
=========== LOCALIZATION READY FOR BUILD ===========
```

### STEP 3 — EDIT JSON

Click:

```text
Open JSONs
```

Edit the JSON file for the language you want.

> **IMPORTANT**
>
> Edit JSON **VALUES** only.
>
> Do **not** change localization **KEYS**.

Example:

```json
"sid_some_text": "My translated text"
```

Do not rename:

```text
sid_some_text
```

### STEP 4 — BUILD

Select one or more languages in the **Languages** panel.

Click:

```text
BUILD
```

Localization Workbench builds and verifies each selected language.

### STEP 5 — INSTALL THE GAME PACKAGE

Built GAME localization is stored under:

```text
Output\<Language>\Game\
```

Example:

```text
Output\Serbian\Game\
```

The folder contains the generated installable localization `.pak`.

A standalone:

```text
Game.locres
```

is also created for inspection.

For normal game installation, use the generated `.pak`.

Copy the generated `.pak` to:

```text
Stalker2\Content\Paks\~mods
```

---

## 3. MOD LOCALIZATION

Use **MODS** for LocalizationDatabase / IoStore localization supplied by supported mods.

> **IMPORTANT**
>
> MODS scanning is manual.
>
> Localization Workbench does not automatically scan the Mods folder when the application starts, when you switch to MODS, when files change, or when Settings are changed.

A full MODS scan starts only when you click:

```text
SCAN MODS
```

Before the first scan in the current session, entering the MODS tab checks only whether mod sources are present.

If mod sources are found, the log reports once:

```text
=========== MODS FOUND ===========
=========== READY TO SCAN ===========
```

Switching between GAME and MODS does not repeat these messages.

### STEP 1 — ADD MOD SOURCES

Put the original mod files or archives in your configured Mods folder.

Localization Workbench can read:

- loose mod files
- extracted mod folders
- ZIP archives
- 7z archives
- RAR archives

A loose IoStore source must contain a complete container set:

```text
.pak
.utoc
.ucas
```

### STEP 2 — SCAN MODS

Click:

```text
SCAN MODS
```

Localization Workbench scans the configured Mods folder and detects supported OverrideContent localization sources.

New or changed localization is marked:

```text
Needs extraction
```

When extraction is required, the log reports:

```text
=========== MODS READY FOR EXTRACTION ===========
```

### STEP 3 — EXTRACT

Click:

```text
EXTRACT
```

Only localization that requires extraction is processed.

Internal rebuild data is stored under:

```text
Source\<ModName>\
```

Translations localization is stored under:

```text
Translations\<ModName>\
```

Example:

```text
Translations\SomeMod\
    english.json
    serbian.json
    ukrainian.json
    ...
```

When extraction is complete and localization is ready to build, the log reports:

```text
=========== MODS READY FOR BUILD ===========
```

### STEP 4 — EDIT JSON

Click:

```text
Open JSONs
```

Edit the desired language JSON.

> **IMPORTANT**
>
> Edit **VALUES** only.
>
> Do **not** change **KEYS**.

Your files under `Translations` are your working translation files.

### STEP 5 — BUILD

Select one or more languages in the **Languages** panel.

Choose one of the two build modes.

### BUILD MODULAR

Creates a separate localization package for each buildable mod.

Output is organized as:

```text
Output\<Language>\<ModName>\
```

Example:

```text
Output\Serbian\SomeMod\
```

Each generated MODS localization package consists of:

```text
.pak
.utoc
.ucas
```

### BUILD ALL-IN-ONE

Combines all available buildable mod localization into one package for each selected language.

Output is stored under:

```text
Output\<Language>\All-in-One\
```

Example:

```text
Output\Serbian\All-in-One\
```

The generated All-in-One package consists of:

```text
.pak
.utoc
.ucas
```

### STEP 6 — INSTALL THE MOD PACKAGE

Copy the complete generated MODS package to:

```text
Stalker2\Content\Paks\~mods
```

Always copy all three generated files together:

```text
.pak
.utoc
.ucas
```

---

## 4. WHEN A MOD IS UPDATED

Localization data is tied to the version of the mod it was extracted from.

When a mod is updated:

1. Replace the old mod files or archive in your Mods folder.
2. Open the MODS tab.
3. Click **SCAN MODS**.
4. The updated localization should be detected as **Needs extraction**.
5. Click **EXTRACT**.
6. Review your existing Translations JSON against the updated localization data.
7. Build the localization again.

> **IMPORTANT**
>
> Your existing Translations JSON files are not automatically overwritten.

Localization Workbench keeps your translation work separate from newly extracted rebuild data.

---

## 5. CACHED VS EDITABLE VS OUTPUT

### CACHED

Location:

```text
Source\
```

Contains internal extracted data required for comparison and safe rebuilding.

**DO NOT EDIT CACHED FILES.**

### EDITABLE

Location:

```text
Translations\
```

Contains the JSON files you are expected to edit.

GAME localization:

```text
Translations\Game\
```

MOD localization:

```text
Translations\<ModName>\
```

**EDIT YOUR TRANSLATIONS HERE.**

### OUTPUT

Location:

```text
Output\
```

Contains finished localization packages created by build operations.

Only finished packages from Output should be installed into the game.

---

## 6. SUPPORTED MOD SOURCES

Localization Workbench uses supported OverrideContent localization containers only.

Sources may be supplied as:

- loose files
- extracted mod folders
- ZIP archives
- 7z archives
- RAR archives

A loose IoStore source must contain a complete container triplet:

```text
.pak
.utoc
.ucas
```

Common recognized OverrideContent-style container names include:

```text
Stalker2-Windows-OverrideContent
-OverrideContent
_OC
_OC_50
_OC_<number>_P
_O
B_P
```

Localization Workbench groups supported container variants belonging to the same mod and processes the supported OverrideContent localization source.

---

## 7. QUICK REFERENCE

### GAME

```text
SCAN GAME
    ->
EXTRACT
    ->
Edit JSON files in Translations\Game\
    ->
Select language(s)
    ->
BUILD
    ->
Find output in Output\<Language>\Game\
    ->
Copy generated .pak to:
Stalker2\Content\Paks\~mods
```

### MODS

```text
Put original mod files or archives in the Mods folder
    ->
Open MODS
    ->
SCAN MODS
    ->
EXTRACT
    ->
Edit JSON files in Translations\<ModName>\
    ->
Select language(s)
    ->
BUILD MODULAR
or
BUILD ALL-IN-ONE
    ->
Find output under Output\<Language>\
    ->
Copy generated .pak + .utoc + .ucas to:
Stalker2\Content\Paks\~mods
```

---

## 8. IMPORTANT WORKFLOW RULES

GAME and MODS are separate workflows.

GAME handles original game localization.

MODS handles supported mod LocalizationDatabase / IoStore localization.

MODS uses supported OverrideContent localization sources only.

Do not edit Source.

Edit translation files only under Translations.

Do not change localization keys.

Change localization values only.

Do not copy files from Source or Translations into the game.

Install only finished packages from Output.

MODS are never scanned automatically.

Always click:

```text
SCAN MODS
```

when you want the current Mods folder to be scanned.

---

## 9. DELETE CACHE

The Settings window contains:

```text
DELETE CACHE
```

This removes internal source extraction data.

It does **not** delete your Translations translation files.

It does **not** delete your Output files.

After deleting Source data, extraction will be required again.

For MODS, a fresh:

```text
SCAN MODS
```

is required before extraction.

Use DELETE CACHE when you intentionally want Localization Workbench to rebuild its internal extraction state.

---

## 10. PATHS AND PORTABLE DATA

Localization Workbench automatically uses folders beside the EXE:

```text
Mods\
Source\
Translations\
Output\
tools\
```

These internal paths move together with the application.

The application may also create:

```text
user-paths.json
```

`user-paths.json` stores only valid external source locations:

- GAME Paks folder
- MODS folder

These paths are remembered after a successful scan.

The application does not use a general `settings.json` file.

---

## 11. IF SOMETHING FAILS

First check that all required tools exist under:

```text
tools\
```

Required:

```text
retoc.exe
repak.exe
UAssetGUI.exe
S2HOCMM.exe
Mappings.usmap
```

Also verify that the GAME Paks folder points to:

```text
Stalker2\Content\Paks
```

For MODS problems:

- make sure you clicked **SCAN MODS**
- make sure a loose source contains a complete `.pak/.utoc/.ucas` set
- make sure the mod contains supported OverrideContent localization

When reporting a problem, include:

- Localization Workbench version
- whether you used GAME or MODS
- selected language
- mod name and version, if applicable
- whether the source was loose, ZIP, 7z or RAR
- application log
- exact error message shown by the application

---

## 12. APPLICATION LANGUAGE

Localization Workbench itself is English-only.

There are no external locale files required by the application.

The language selector inside Localization Workbench refers to S.T.A.L.K.E.R. 2 localization languages being extracted or built, not the language of the Workbench interface.

---

## PLATFORM

Localization Workbench is built for:

```text
Windows x64
```

The application itself is published as a self-contained Windows build.

A separate .NET installation is not required for Localization Workbench itself.

> **IMPORTANT**
>
> Third-party tools may have their own runtime requirements.

For example, UAssetGUI v1.1.0 may require the appropriate .NET Desktop Runtime in environments where that dependency is not otherwise available.

Development and testing were primarily performed under Linux/Wine.

The distributed application target is Windows x64.
