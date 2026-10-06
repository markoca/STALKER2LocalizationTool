Localization Workbench - user-supplied runtime tools
===================================================

Localization Workbench does NOT distribute helper-tool binaries.

Before using GAME or MODS workflows, obtain compatible Windows x64 copies of
the required tools yourself and place them beside this README under:

    tools/retoc.exe
    tools/repak.exe
    tools/UAssetGUI.exe
    tools/Mappings.usmap
    tools/S2HOCMM.exe

The application resolves this directory relative to Localization Workbench.exe.

Notes
-----
retoc.exe
  Use a compatible stock Windows x64 retoc CLI build.

UAssetGUI.exe
  The tested baseline is UAssetGUI v1.1.0.

Mappings.usmap
  Use mappings compatible with the target S.T.A.L.K.E.R. 2 game build.

repak.exe
  Used by the GAME Game.locres extraction/build workflow.

S2HOCMM.exe
  Used by the GAME workflow for Game.locres serialization and verification.

The publish scripts do not download, validate, copy, or redistribute these
third-party tools. A developer may keep locally supplied tools in
publish/win-x64/tools/ for testing; that folder is preserved across republish.
