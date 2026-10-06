# RC6 publish and test

RC6 uses **stock retoc**. There is no retoc patch/build step.

## 1. Prepare project-local tools

Before publishing, put these Windows files in `tools\`:

```text
tools\retoc.exe
tools\repak.exe
tools\UAssetGUI.exe
tools\Mappings.usmap
tools\S2HOCMM.exe
```

`retoc.exe` is the ordinary upstream Windows CLI build. It does **not** need
`--source-package-map`.

No Git, Cargo/Rust, Python helper, download, or dependency compilation is run
by the publisher.

## 2. Publish on Windows

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\publish-win-x64.ps1
```

Output:

```text
publish\win-x64\LocalizationWorkbench.exe
publish\win-x64\tools\...
```

The publisher refreshes runtime tools/locales from the source tree while
preserving `settings.json`, `Mods`, `Cached`, `Editable`, and `Output`.

## 3. MODS regression test

Use a known working NewContent/OverrideContent mod pair.

1. Scan MODS.
2. Extract.
3. Edit one value in `Editable\<mod>\<language>.json`.
4. Build Modular.
5. Confirm the output triplet exists.
6. The build must complete stock-retoc verification of:
   - canonical virtual LocalizationDatabase path;
   - complete original 24-hex chunk/FPackageId;
   - exact patched RawExport after round-trip.
7. Test the output in game.

Repeat once with All-in-One if that mode is part of the release test.

## 4. GAME regression test

Build one language from `Editable\Game`. Confirm S2HOCMM creates and verifies
`Game.locres`, and repak creates the final V11 PAK with the known S2 path hash
seed.
