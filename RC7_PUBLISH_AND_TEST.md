# RC7 publish and test

Localization Workbench has one supported application target: **Windows x64**.

## Authoritative runtime tools

Keep the complete prebuilt Windows tool bundle under:

```text
tools/win-x64/
  retoc.exe
  repak.exe
  UAssetGUI.exe
  Mappings.usmap
  S2HOCMM.exe
```

There are no duplicate root-level tool binaries and no native Linux tool bundle in this project.

## Publish from Linux

```bash
./scripts/publish-win-x64.sh
```

The script cross-publishes the WinForms application as self-contained `win-x64`. It verifies that the Windows helper files exist but does not execute, download or compile them.

## Publish from Windows

```powershell
.\scripts\publish-win-x64.ps1
```

The PowerShell publisher validates the same `tools/win-x64` bundle and produces the same runtime layout.

## Expected output

```text
publish/win-x64/
  Localization Workbench.exe
  tools/
  locales/
  Mods/
  Cached/
  Editable/
  Output/
```

Runtime settings and workspace data are preserved across republish. Legacy default workspaces `Extracted` and `Ready` are migrated conservatively to `Cached` and `Editable` when safe.

## Test

Run the complete regression list in `RC_CHECKLIST.md` before promoting the candidate.
