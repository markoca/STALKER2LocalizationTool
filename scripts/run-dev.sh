#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
for tool in retoc.exe repak.exe UAssetGUI.exe Mappings.usmap S2HOCMM.exe; do
    [[ -f "$ROOT/tools/$tool" ]] || { echo "ERROR: missing project-local runtime dependency: tools/$tool" >&2; exit 1; }
done
dotnet run --project "$ROOT/STALKER2LocalizationTool.csproj" -p:EnableWindowsTargeting=true
