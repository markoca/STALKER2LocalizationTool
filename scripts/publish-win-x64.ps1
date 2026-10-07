$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Project = Join-Path $Root "LocalizationWorkbench.csproj"
if (-not (Test-Path -LiteralPath $Project -PathType Leaf)) { throw "Project file not found: $Project" }
$PublishRoot = Join-Path $Root "publish"
$Out = Join-Path $PublishRoot "win-x64"
$Stage = Join-Path $PublishRoot (".win-x64-stage-" + [guid]::NewGuid().ToString("N"))
$Preserve = Join-Path $PublishRoot (".win-x64-preserve-" + [guid]::NewGuid().ToString("N"))

$PreservedNames = @("user-paths.json", "Mods", "Source", "Translations", "Output", "tools")

New-Item -ItemType Directory -Force -Path @($PublishRoot, $Stage, $Preserve) | Out-Null

function Move-LegacyWorkspaceDirectory {
    param(
        [string]$LegacyName,
        [string]$CurrentName
    )

    $Legacy = Join-Path $Out $LegacyName
    $Current = Join-Path $Out $CurrentName

    if (-not (Test-Path $Legacy)) {
        return
    }

    if (Test-Path $Current) {
        $CurrentItems = @(Get-ChildItem -LiteralPath $Current -Force)
        if ((Test-Path $Current -PathType Container) -and $CurrentItems.Count -eq 0) {
            Remove-Item $Current -Force
        }
        else {
            throw "Both legacy $LegacyName and populated current $CurrentName exist in $Out; refusing to merge automatically."
        }
    }

    Move-Item $Legacy $Current
    Write-Host "Migrated workspace: $LegacyName -> $CurrentName"
}

if (Test-Path $Out) {
    Move-LegacyWorkspaceDirectory "Cached" "Source"
    Move-LegacyWorkspaceDirectory "Editable" "Translations"
}

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

try {
    Write-Host "Publishing Localization Workbench (win-x64, self-contained)..."
    dotnet publish $Project `
      -c Release `
      -r win-x64 `
      --self-contained true `
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
    # Copy only the application publish output. Runtime helper tools are supplied separately.
    Copy-Item (Join-Path $Stage "*") $Out -Recurse -Force

    foreach ($Name in @("Mods", "Source", "Translations", "Output", "tools")) {
        New-Item -ItemType Directory -Force (Join-Path $Out $Name) | Out-Null
    }

    $ToolsReadme = Join-Path $Root "tools\README.txt"
    if (Test-Path $ToolsReadme) {
        Copy-Item $ToolsReadme (Join-Path $Out "tools\README.txt") -Force
    }

    foreach ($Doc in @("README.md", "QUICK_USER_HANDBOOK.md", "CHANGELOG.md", "RELEASE_NOTES.md")) {
        $SourceDoc = Join-Path $Root $Doc
        if (Test-Path $SourceDoc) {
            Copy-Item $SourceDoc (Join-Path $Out $Doc) -Force
        }
    }

    Write-Host ""
    Write-Host "Runtime: $Out"
    Write-Host "Runtime helper tools are NOT bundled. Put user-supplied tools in: $Out\tools"
    Write-Host "Existing user paths and workspace data were preserved."
}
finally {
    Restore-RuntimeData
    Remove-Item $Stage, $Preserve -Recurse -Force -ErrorAction SilentlyContinue
}
