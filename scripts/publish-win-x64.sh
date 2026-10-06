#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
PROJECT="$(find "$ROOT" -maxdepth 1 -type f -name '*.csproj' -print -quit)"
if [[ -z "$PROJECT" ]]; then
    echo "ERROR: no .csproj found in project root: $ROOT" >&2
    exit 1
fi
TOOLS_ROOT="$ROOT/tools/win-x64"
PUBLISH_ROOT="$ROOT/publish"
OUT="$PUBLISH_ROOT/win-x64"

mkdir -p "$PUBLISH_ROOT"
STAGE="$(mktemp -d "$PUBLISH_ROOT/.win-x64-stage.XXXXXX")"
PRESERVE="$(mktemp -d "$PUBLISH_ROOT/.win-x64-preserve.XXXXXX")"

PRESERVED_NAMES=(user-paths.json Mods Cached Editable Output)
REQUIRED_TOOLS=(retoc.exe repak.exe UAssetGUI.exe Mappings.usmap S2HOCMM.exe)

restore_runtime_data() {
    mkdir -p "$OUT"
    for name in "${PRESERVED_NAMES[@]}"; do
        if [[ -e "$PRESERVE/$name" && ! -e "$OUT/$name" ]]; then
            mv "$PRESERVE/$name" "$OUT/$name"
        fi
    done
}

cleanup() {
    restore_runtime_data
    rm -rf "$STAGE" "$PRESERVE"
}
trap cleanup EXIT

for tool in "${REQUIRED_TOOLS[@]}"; do
    if [[ ! -f "$TOOLS_ROOT/$tool" ]]; then
        echo "ERROR: required Windows runtime tool is missing: tools/win-x64/$tool" >&2
        echo "The build host may be Linux, but the TARGET is win-x64, so Windows helper binaries are required." >&2
        exit 1
    fi
done

echo "Windows target tool bundle found: tools/win-x64/"
echo "Cross-publishing Localization Workbench (win-x64, self-contained)..."
echo "Build host: $(uname -s) / $(uname -m)"

dotnet publish "$PROJECT" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:EnableWindowsTargeting=true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o "$STAGE"

if [[ -e "$OUT" ]]; then
    for name in "${PRESERVED_NAMES[@]}"; do
        if [[ -e "$OUT/$name" ]]; then
            mv "$OUT/$name" "$PRESERVE/$name"
        fi
    done
    rm -rf "$OUT"
fi

mkdir -p "$OUT"
restore_runtime_data


cp -a "$STAGE/." "$OUT/"
mkdir -p "$OUT/Mods" "$OUT/Cached" "$OUT/Editable" "$OUT/Output"

for doc in README.md CHANGELOG.md RC_CHECKLIST.md RELEASE_CANDIDATE.md; do
    [[ -f "$ROOT/$doc" ]] && cp -f "$ROOT/$doc" "$OUT/$doc"
done

echo
echo "Windows runtime: $OUT"
echo "The target Windows machine does NOT need the .NET runtime because this publish is self-contained."
echo "No Wine, Git, cargo/Rust, helper-tool execution, download, or retoc compilation was used during publish."
