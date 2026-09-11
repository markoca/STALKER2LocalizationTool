Runtime tool bundles are organized by TARGET runtime, not by the OS used to run dotnet publish.

Windows release target:
  tools/win-x64/
    retoc.exe
    repak.exe
    UAssetGUI.exe
    S2HOCMM.exe
    Mappings.usmap

The current application is WinForms (net8.0-windows), so there is no native Linux application target yet.
A Windows release can be cross-published from Linux with scripts/publish-win-x64.sh. The resulting
Windows package must still contain the Windows helper tools from tools/win-x64/.

If the UI is ported to a cross-platform framework later, tools/linux-x64/ is reserved for native
Linux helper binaries such as retoc and repak.
