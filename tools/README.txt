The STALKER2 Localization Tool - external tools
=====================================================

Expected runtime files:

    retoc.exe
    UAssetGUI.exe
    Mappings.usmap
    repak.exe
    S2HOCMM.exe          (required only for Game.locres builds)

You can also point the app to alternative tool files from Settings.

Game.locres extraction/parsing remains in the application, but final Game.locres
serialization deliberately uses S2HOCMM to match the proven launch.py pipeline.

retoc
-----
Used for IoStore / ModLocalizationDatabaseDataAsset extraction and rebuilding.
Project:
https://github.com/trumank/retoc
Releases:
https://github.com/trumank/retoc/releases

UAssetGUI
---------
Used to inspect legacy .uasset export metadata for localization database assets.
Project:
https://github.com/atenfyr/UAssetGUI
Pinned release:
https://github.com/atenfyr/UAssetGUI/releases/tag/v1.1.0

The source tree intentionally does not redistribute UAssetGUI.exe in v0.8.3+.
The publish and dev helpers fetch the pinned upstream v1.1.0 executable from
GitHub and verify SHA-256 before use:

    b7d75c0893f1a60e565853ae638bc21f2416cd12c2d9d854e297abb87ceb3263

Manual Linux/CachyOS fetch:

    ./scripts/fetch-uassetgui.sh

Manual PowerShell fetch:

    .\scripts\fetch-uassetgui.ps1

UAssetGUI v1.1.0 expects mappings by installed name rather than a full path on
its CLI. The application automatically copies the selected Mappings.usmap to:

    %LOCALAPPDATA%\UAssetGUI\Mappings\STALKER2LocalizationTool.usmap

and invokes UAssetGUI with the mapping name STALKER2LocalizationTool.

On Linux/Wine, upstream documents .NET 8 Desktop Runtime plus micross for
UAssetGUI v1.1.0 and lower.

Mappings.usmap
--------------
Required by UAssetGUI for S.T.A.L.K.E.R. 2 assets.
A public mappings resource is available on Nexus Mods:
https://www.nexusmods.com/stalker2heartofchornobyl/mods/116

Use mappings compatible with the game build you are targeting.

repak
-----
Used only by the GAME workflow to scan/unpack the base Game.locres and to
create the final installable localization PAK with mount point ../../../,
PAK V11, and S.T.A.L.K.E.R. 2's path-hash-seed settings. MODS does not scan
PAK contents for LOCRES.
Project:
https://github.com/trumank/repak
Releases:
https://github.com/trumank/repak/releases

S2HOCMM LOCRES build
--------------------
Final Game.locres serialization uses S2HOCMM -Pack with a flat culture JSON.
The application then verifies the generated binary through S2HOCMM -Dump and
ignores S2HOCMM's own PAK. repak.exe performs the final package step with:

    --mount-point ../../../
    --version V11
    --path-hash-seed 1244705156

Project:
https://gitlab.com/PatrykPniewski/s2hocmm

The publish helper copies S2HOCMM.exe only when it is explicitly present in
tools/. The common sibling developer layout ../S2HOCMM/S2HOCMM.exe is
auto-detected at runtime instead of copied, keeping any companion files beside
the original executable. Otherwise select the binary manually in Settings.

Game global files
-----------------
global.utoc and global.ucas are NOT third-party downloads. They are needed only
for IoStore LocalizationDatabase assets. The app reads the matching files from
your own game installation:

    S.T.A.L.K.E.R. 2 Heart of Chornobyl\Stalker2\Content\Paks
