$ErrorActionPreference = "Stop"

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Destination = Join-Path $Root "tools\UAssetGUI.exe"
$Version = "v1.1.0"
$Url = "https://github.com/atenfyr/UAssetGUI/releases/download/$Version/UAssetGUI.exe"
$ExpectedSha256 = "b7d75c0893f1a60e565853ae638bc21f2416cd12c2d9d854e297abb87ceb3263"

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if (Test-Path -LiteralPath $Destination -PathType Leaf) {
    $Current = Get-Sha256 $Destination
    if ($Current -eq $ExpectedSha256) {
        Write-Host "UAssetGUI $Version already verified: $Destination"
        exit 0
    }
    Write-Host "Replacing non-pinned UAssetGUI.exe (sha256=$Current)"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
$Temp = Join-Path (Split-Path -Parent $Destination) (".UAssetGUI.exe.download." + [guid]::NewGuid().ToString("N"))

try {
    Write-Host "Downloading upstream UAssetGUI $Version..."
    Invoke-WebRequest -Uri $Url -OutFile $Temp -UseBasicParsing

    $Actual = Get-Sha256 $Temp
    if ($Actual -ne $ExpectedSha256) {
        throw "Downloaded UAssetGUI.exe failed SHA-256 verification. Expected $ExpectedSha256, got $Actual"
    }

    Move-Item -LiteralPath $Temp -Destination $Destination -Force
    Write-Host "Installed verified UAssetGUI $Version -> $Destination"
    Write-Host "SHA-256: $ExpectedSha256"
}
finally {
    Remove-Item -LiteralPath $Temp -Force -ErrorAction SilentlyContinue
}
