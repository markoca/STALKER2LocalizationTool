$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Project = Join-Path $Root "STALKER2LocalizationTool.csproj"
$PublishRoot = Join-Path $Root "publish"
$Out = Join-Path $PublishRoot "win-x64"
$Stage = Join-Path $PublishRoot (".win-x64-stage-" + [guid]::NewGuid().ToString("N"))
$Preserve = Join-Path $PublishRoot (".win-x64-preserve-" + [guid]::NewGuid().ToString("N"))
$PreservedNames = @("settings.json", "tools", "locales", "Mods", "Cached", "Editable", "Output", "Extracted", "Ready")

New-Item -ItemType Directory -Force -Path @($PublishRoot, $Stage, $Preserve) | Out-Null

function Restore-RuntimeData {
    New-Item -ItemType Directory -Force $Out | Out-Null
    foreach ($Name in $PreservedNames) {
        $Saved = Join-Path $Preserve $Name
        $Destination = Join-Path $Out $Name
        if ((Test-Path $Saved) -and -not (Test-Path $Destination)) {
            Move-Item $Saved $Destination
        }
    }
}

function Test-DirectoryHasEntries([string]$Path) {
    if (-not (Test-Path $Path -PathType Container)) { return $false }
    return $null -ne (Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | Select-Object -First 1)
}

function Migrate-WorkspaceName([string]$OldName, [string]$NewName) {
    $OldPath = Join-Path $Out $OldName
    $NewPath = Join-Path $Out $NewName
    if (-not (Test-Path $OldPath)) { return }

    if (-not (Test-Path $NewPath)) {
        Move-Item $OldPath $NewPath
        Write-Host "Workspace migrated: $OldName -> $NewName"
        return
    }

    if ((Test-Path $OldPath -PathType Container) -and (Test-Path $NewPath -PathType Container)) {
        $OldHasEntries = Test-DirectoryHasEntries $OldPath
        $NewHasEntries = Test-DirectoryHasEntries $NewPath

        if (-not $OldHasEntries) {
            Remove-Item $OldPath -Force
            Write-Host "Removed empty legacy workspace: $OldName"
            return
        }

        if (-not $NewHasEntries) {
            Remove-Item $NewPath -Force
            Move-Item $OldPath $NewPath
            Write-Host "Workspace migrated: $OldName -> $NewName (empty destination replaced safely)"
            return
        }
    }

    Write-Warning "Both $OldName and $NewName contain data; nothing was merged or overwritten. The app will use only $NewName."
}

try {
    Write-Host "Preparing pinned UAssetGUI dependency..."
    & (Join-Path $Root "scripts\fetch-uassetgui.ps1")

    Write-Host "Publishing The STALKER2 Localization Tool (win-x64 single-file)..."
    dotnet publish $Project `
      -c Release `
      -r win-x64 `
      --self-contained true `
      -p:EnableWindowsTargeting=true `
      -p:PublishSingleFile=true `
      -p:IncludeNativeLibrariesForSelfExtract=true `
      -p:EnableCompressionInSingleFile=true `
      -p:DebugType=None `
      -p:DebugSymbols=false `
      -o $Stage

    if (Test-Path $Out) {
        foreach ($Name in $PreservedNames) {
            $Source = Join-Path $Out $Name
            if (Test-Path $Source) {
                Move-Item $Source (Join-Path $Preserve $Name)
            }
        }
        Remove-Item $Out -Recurse -Force
    }

    New-Item -ItemType Directory -Force $Out | Out-Null
    Restore-RuntimeData
    Migrate-WorkspaceName "Extracted" "Cached"
    Migrate-WorkspaceName "Ready" "Editable"

    Copy-Item (Join-Path $Stage "STALKER2LocalizationTool.exe") (Join-Path $Out "STALKER2LocalizationTool.exe") -Force
    foreach ($Name in @("Mods", "Cached", "Editable", "Output", "tools", "locales")) {
        New-Item -ItemType Directory -Force (Join-Path $Out $Name) | Out-Null
    }

    Copy-Item (Join-Path $Root "locales\en.json") (Join-Path $Out "locales\en.json") -Force
    foreach ($Doc in @("README.md", "CHANGELOG.md", "RC_CHECKLIST.md")) {
        $SourceDoc = Join-Path $Root $Doc
        if (Test-Path $SourceDoc) {
            Copy-Item $SourceDoc (Join-Path $Out $Doc) -Force
        }
    }

    foreach ($Tool in @("retoc.exe", "UAssetGUI.exe", "Mappings.usmap", "repak.exe")) {
        $Source = Join-Path $Root "tools\$Tool"
        $Destination = Join-Path $Out "tools\$Tool"
        if (Test-Path $Source) {
            Copy-Item $Source $Destination -Force
        }
    }

    $S2HocmmSource = Join-Path $Root "tools\S2HOCMM.exe"
    if (Test-Path $S2HocmmSource) {
        Copy-Item $S2HocmmSource (Join-Path $Out "tools\S2HOCMM.exe") -Force
        Write-Host "S2HOCMM prepared from: $S2HocmmSource"
    }
    elseif (-not (Test-Path (Join-Path $Out "tools\S2HOCMM.exe")) -and
            -not (Test-Path (Join-Path $Out "tools\S2HOCMM\S2HOCMM.exe"))) {
        Write-Host "INFO: S2HOCMM was not bundled. The app can auto-detect a sibling ..\S2HOCMM\S2HOCMM.exe or you can select it in Settings."
    }

    Write-Host ""
    Write-Host "Runtime: $Out"
    Write-Host "Existing settings, work folders, and extra tool files were preserved."
    Write-Host "Legacy default workspaces are migrated safely to Cached/Editable; old defaults are never used for new runtime writes."
    Write-Host "Bundled tools were refreshed from $Root\tools; UAssetGUI was fetched/verified. S2HOCMM is copied only when explicitly present in tools/; sibling developer checkouts are auto-detected at runtime."
}
finally {
    Restore-RuntimeData
    Remove-Item $Stage, $Preserve -Recurse -Force -ErrorAction SilentlyContinue
}
