$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Project = Join-Path $Root "STALKER2LocalizationTool.csproj"
$ToolsRoot = Join-Path $Root "tools\win-x64"
$PublishRoot = Join-Path $Root "publish"
$Out = Join-Path $PublishRoot "win-x64"
$Stage = Join-Path $PublishRoot (".win-x64-stage-" + [guid]::NewGuid().ToString("N"))
$Preserve = Join-Path $PublishRoot (".win-x64-preserve-" + [guid]::NewGuid().ToString("N"))

# User data survives republishing. Runtime tools/locales do NOT: source-tree files
# are authoritative and are refreshed on every deployment.
$PreservedNames = @("settings.json", "Mods", "Cached", "Editable", "Output", "Extracted", "Ready")
$RequiredTools = @("retoc.exe", "repak.exe", "UAssetGUI.exe", "Mappings.usmap", "S2HOCMM.exe")
$PinnedUAssetGuiSha256 = "b7d75c0893f1a60e565853ae638bc21f2416cd12c2d9d854e297abb87ceb3263"
$KnownBadUAssetGuiSha256 = "e9b953245fd3716545558d751a8855d14490cd0e9e377a828e5ab4e0f34e7109"

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

function Assert-ProjectToolBundle {
    Write-Host "Checking project-local runtime tool bundle..."

    $Missing = @()
    foreach ($Tool in $RequiredTools) {
        $Path = Join-Path $ToolsRoot $Tool
        if (-not (Test-Path $Path -PathType Leaf)) {
            $Missing += $Tool
        }
    }

    if ($Missing.Count -gt 0) {
        throw @"
The source project is missing required runtime files in tools\win-x64\:
  $($Missing -join "`n  ")

Put the PREBUILT Windows files in the project tools\win-x64\ directory and publish again.
No dependency is downloaded or compiled by the publish script.
"@
    }

    $Retoc = Join-Path $ToolsRoot "retoc.exe"
    $Help = (& $Retoc to-zen --help 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw @"
tools\win-x64\retoc.exe could not run its to-zen help command.
Use a normal prebuilt Windows retoc.exe from the official retoc release.
The publish script never downloads or compiles retoc.
"@
    }

    $UAssetGui = Join-Path $ToolsRoot "UAssetGUI.exe"
    $UAssetGuiHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $UAssetGui).Hash.ToLowerInvariant()
    if ($UAssetGuiHash -eq $KnownBadUAssetGuiSha256) {
        throw "tools\win-x64\UAssetGUI.exe is the known-bad bundle (SHA-256 $UAssetGuiHash). Replace it before publishing."
    }
    if ($UAssetGuiHash -ne $PinnedUAssetGuiSha256) {
        Write-Warning "UAssetGUI.exe is not the pinned v1.1.0 binary (SHA-256 $UAssetGuiHash). Publishing is allowed because custom tool binaries are supported."
    }

    Write-Host "Tool bundle OK: retoc / repak / UAssetGUI / Mappings / S2HOCMM"
}

try {
    Assert-ProjectToolBundle

    Write-Host "Publishing Localization Workbench (win-x64, self-contained)..."
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

    # Copy the complete SDK publish output. tools/ and locales/ are already present
    # because the csproj marks them as publish content.
    Copy-Item (Join-Path $Stage "*") $Out -Recurse -Force

    foreach ($Name in @("Mods", "Cached", "Editable", "Output")) {
        New-Item -ItemType Directory -Force (Join-Path $Out $Name) | Out-Null
    }

    foreach ($Doc in @("README.md", "CHANGELOG.md", "RC_CHECKLIST.md", "RC7_PUBLISH_AND_TEST.md")) {
        $SourceDoc = Join-Path $Root $Doc
        if (Test-Path $SourceDoc) {
            Copy-Item $SourceDoc (Join-Path $Out $Doc) -Force
        }
    }

    Write-Host ""
    Write-Host "Runtime: $Out"
    Write-Host "No Git, Rust/cargo, dependency download, or retoc compilation was used."
    Write-Host "Project tools\win-x64\ is authoritative and was copied into runtime tools\."
    Write-Host "Existing settings and workspace data were preserved."
}
finally {
    Restore-RuntimeData
    Remove-Item $Stage, $Preserve -Recurse -Force -ErrorAction SilentlyContinue
}
