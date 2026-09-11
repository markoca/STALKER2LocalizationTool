$ErrorActionPreference = "Stop"
$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Project = Join-Path $Root "STALKER2LocalizationTool.csproj"
$Required = @("retoc.exe", "repak.exe", "UAssetGUI.exe", "Mappings.usmap", "S2HOCMM.exe")
foreach ($Name in $Required) {
    $Path = Join-Path $Root "tools\$Name"
    if (-not (Test-Path $Path -PathType Leaf)) {
        throw "Missing project-local runtime dependency: $Path"
    }
}
dotnet run --project $Project
