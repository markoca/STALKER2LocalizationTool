# RC7 publish model

The application is currently a Windows Forms application (`net8.0-windows`).
There is one supported application target in RC7: **win-x64**.

## Build Windows on Linux

Install the .NET 8 SDK on the Linux development machine, put the Windows helper tools in:

    tools/win-x64/

Required:

    retoc.exe
    repak.exe
    UAssetGUI.exe
    S2HOCMM.exe
    Mappings.usmap

Then run:

    ./scripts/publish-win-x64.sh

The script cross-publishes `win-x64` as a self-contained single-file application.
The Windows machine that runs the resulting package does **not** need .NET installed.

The publish script does not execute the Windows helper tools on Linux. It only verifies that the
required target files exist, then `dotnet publish` copies them into `publish/win-x64/tools/`.

## Build Windows on Windows

If a Windows development machine has the .NET 8 SDK, the equivalent command is:

    .\\scripts\\publish-win-x64.ps1

Both scripts target the same `win-x64` application and the same `tools/win-x64` bundle.

## Linux target

RC7 does not provide a native Linux application build because the UI is Windows Forms. A native
Linux release would require porting the UI to a cross-platform framework. The `tools/linux-x64/`
directory is reserved for that future target.
