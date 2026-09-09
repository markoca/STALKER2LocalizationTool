$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Project = Join-Path $Root "STALKER2LocalizationTool.csproj"
& (Join-Path $Root "scripts\fetch-uassetgui.ps1")
dotnet run --project $Project
