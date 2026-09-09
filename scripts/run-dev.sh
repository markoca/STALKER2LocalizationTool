#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
"$ROOT/scripts/fetch-uassetgui.sh"
dotnet run --project "$ROOT/STALKER2LocalizationTool.csproj" -p:EnableWindowsTargeting=true
