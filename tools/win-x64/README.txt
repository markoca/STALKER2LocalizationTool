Localization Workbench - Windows x64 runtime tools
===================================================

This directory is the authoritative prebuilt helper-tool bundle for the current
application target:

    tools/win-x64/retoc.exe
    tools/win-x64/repak.exe
    tools/win-x64/UAssetGUI.exe
    tools/win-x64/Mappings.usmap
    tools/win-x64/S2HOCMM.exe

Publish copies these files to publish/win-x64/tools/. Nothing is downloaded or
compiled during publishing.

retoc.exe
---------
Use a stock Windows x64 retoc CLI build. No custom --source-package-map patch is
required.

UAssetGUI.exe
-------------
The tested baseline is UAssetGUI v1.1.0.

Pinned SHA-256:
    b7d75c0893f1a60e565853ae638bc21f2416cd12c2d9d854e297abb87ceb3263

Mappings.usmap
--------------
Use mappings compatible with the target S.T.A.L.K.E.R. 2 game build.

repak.exe
---------
Used by the GAME Game.locres workflow for the verified final PAK.

S2HOCMM.exe
------------
Used by the GAME workflow for Game.locres serialization and verification.

Game global.utoc/global.ucas are read from the user's game installation and are
not part of this bundle.
