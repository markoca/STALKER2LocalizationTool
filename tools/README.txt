Localization Workbench - user-supplied runtime tools
===================================================

Localization Workbench does NOT distribute helper-tool binaries.

Before using GAME or MODS workflows, obtain the required Windows x64 tools
from their original project/release pages and place them beside this README:

    tools/retoc.exe
    tools/repak.exe
    tools/UAssetGUI.exe
    tools/Mappings.usmap
    tools/S2HOCMM.exe

The application resolves this directory relative to Localization Workbench.exe.


DOWNLOAD SOURCES
================

retoc.exe
---------
Project:
    https://github.com/trumank/retoc

Releases:
    https://github.com/trumank/retoc/releases

Tested baseline:
    retoc v0.1.5

Download the Windows x64 release and place the retoc executable in:
    tools/retoc.exe


repak.exe
---------
Project:
    https://github.com/trumank/repak

Releases:
    https://github.com/trumank/repak/releases

Tested baseline:
    repak v0.2.3

Download the Windows x64 release and place the executable in:
    tools/repak.exe


UAssetGUI.exe
-------------
Project:
    https://github.com/atenfyr/UAssetGUI

Releases:
    https://github.com/atenfyr/UAssetGUI/releases

Tested baseline:
    UAssetGUI v1.1.0

Use the stable v1.1.0 release and place UAssetGUI.exe in:
    tools/UAssetGUI.exe

UAssetGUI v1.1.0 may require the .NET 8 Desktop Runtime when that dependency
is not already available.


S2HOCMM.exe
-----------
Nexus Mods:
    https://www.nexusmods.com/stalker2heartofchornobyl/mods/540

Original project:
    https://gitlab.com/PatrykPniewski/s2hocmm

GitLab releases:
    https://gitlab.com/PatrykPniewski/s2hocmm/-/releases

Tested baseline:
    S2HOCMM v2.3

Place S2HOCMM.exe in:
    tools/S2HOCMM.exe


Mappings.usmap
--------------
Mappings are tied to the S.T.A.L.K.E.R. 2 game version.

For the current tested game baseline 2.0.4:
    https://www.nexusmods.com/stalker2heartofchornobyl/mods/2356

Place the downloaded mapping file in:
    tools/Mappings.usmap

IMPORTANT:
Do not keep using a 2.0.4 mapping after the game receives a newer patch unless
it is confirmed compatible. For a newer game version, obtain a .usmap generated
for that exact game build and save it as:

    tools/Mappings.usmap


EXPECTED LAYOUT
===============

Localization Workbench.exe
tools/
    README.txt
    retoc.exe
    repak.exe
    UAssetGUI.exe
    Mappings.usmap
    S2HOCMM.exe


NOTES
=====

retoc.exe
  Used for IoStore inspection, extraction and rebuilding.

repak.exe
  Used by the GAME Game.locres extraction/build workflow.

UAssetGUI.exe
  Used to inspect extracted Unreal assets and LocalizationDatabase metadata.

Mappings.usmap
  Provides Unreal asset mappings required by UAssetGUI and must match the
  target game build.

S2HOCMM.exe
  Used by the GAME workflow for Game.locres serialization and verification.

The publish scripts do not download, validate, copy, or redistribute these
third-party tools. A developer may keep locally supplied tools in
publish/win-x64/tools/ for testing; that folder is preserved across republish.
