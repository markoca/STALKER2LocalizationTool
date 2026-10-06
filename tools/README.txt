Runtime helper binaries are organized by TARGET runtime.

Current supported application target:
  tools/win-x64/
    retoc.exe
    repak.exe
    UAssetGUI.exe
    S2HOCMM.exe
    Mappings.usmap

Localization Workbench is a Windows Forms application targeting net8.0-windows.
Linux may be used as a build host and Wine test environment, but there is no
native Linux application target or Linux helper bundle in this repository.

scripts/publish-win-x64.sh and scripts/publish-win-x64.ps1 both consume only
tools/win-x64/. The published application receives those files under tools/.
