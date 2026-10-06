#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
for tool in retoc.exe repak.exe UAssetGUI.exe Mappings.usmap S2HOCMM.exe; do
    [[ -f "$ROOT/tools/$tool" ]] || { echo "ERROR: missing project-local runtime dependency: tools/$tool" >&2; exit 1; }
done
PROJECT="$(find "$ROOT" -maxdepth 1 -type f -name '*.csproj' -print -quit)"
if [[ -z "$PROJECT" ]]; then
    echo "ERROR: no .csproj found in project root: $ROOT" >&2
    exit 1
fi
dotnet run --project "$PROJECT" -p:EnableWindowsTargeting=true
