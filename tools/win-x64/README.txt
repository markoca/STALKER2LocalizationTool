Localization Workbench - project-local runtime tools
===========================================================

The source project tools\ directory is the authoritative deployment bundle.
Put the complete PREBUILT Windows toolset here before publishing:

    tools\retoc.exe
    tools\repak.exe
    tools\UAssetGUI.exe
    tools\Mappings.usmap
    tools\S2HOCMM.exe

Publish copies these files into publish\win-x64\tools\. The end user does
not need Git, Rust/cargo, Python, downloads, or any tool compilation.

retoc.exe
---------
RC6 deliberately uses normal STOCK retoc. No custom --source-package-map
patch is required. Use the ordinary Windows x64 retoc CLI release:

    https://github.com/trumank/retoc/releases

The MODS pipeline still verifies the final canonical LocalizationDatabase path,
the complete original 24-hex ExportBundleData chunk/FPackageId, and the exact
patched RawExport after stock retoc repacks the overlay.

UAssetGUI.exe
-------------
Used to inspect extracted legacy .uasset metadata. The tested/pinned binary is
UAssetGUI v1.1.0:

    https://github.com/atenfyr/UAssetGUI/releases/tag/v1.1.0

Expected SHA-256 for the pinned binary:
    b7d75c0893f1a60e565853ae638bc21f2416cd12c2d9d854e297abb87ceb3263

Mappings.usmap
--------------
Use mappings compatible with the target S.T.A.L.K.E.R. 2 game build.

repak.exe
---------
Used for GAME Game.locres packaging with the known-good V11/path-hash-seed
settings.

S2HOCMM.exe
------------
Used for final GAME Game.locres serialization and verification. Keep it in
tools\ so deployment is fully self-contained.

Game global.utoc/global.ucas are read from the user's own game installation;
they are not bundled third-party tools.
