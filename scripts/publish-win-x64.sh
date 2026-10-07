#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
PROJECT="$(find "$ROOT" -maxdepth 1 -type f -name '*.csproj' -print -quit)"
if [[ -z "$PROJECT" ]]; then
    echo "ERROR: no .csproj found in project root: $ROOT" >&2
    exit 1
fi
PUBLISH_ROOT="$ROOT/publish"
OUT="$PUBLISH_ROOT/win-x64"

mkdir -p "$PUBLISH_ROOT"
STAGE="$(mktemp -d "$PUBLISH_ROOT/.win-x64-stage.XXXXXX")"
PRESERVE="$(mktemp -d "$PUBLISH_ROOT/.win-x64-preserve.XXXXXX")"

PRESERVED_NAMES=(user-paths.json Mods Source Translations Output tools)

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

migrate_legacy_workspace_names() {
    if [[ -e "$OUT/Cached" && -e "$OUT/Source" ]]; then
        echo "ERROR: both legacy Cached and current Source exist in $OUT; refusing to merge automatically." >&2
        exit 1
    fi
    if [[ -e "$OUT/Editable" && -e "$OUT/Translations" ]]; then
        echo "ERROR: both legacy Editable and current Translations exist in $OUT; refusing to merge automatically." >&2
        exit 1
    fi

    if [[ -e "$OUT/Cached" ]]; then
        mv "$OUT/Cached" "$OUT/Source"
        echo "Migrated workspace: Cached -> Source"
    fi
    if [[ -e "$OUT/Editable" ]]; then
        mv "$OUT/Editable" "$OUT/Translations"
        echo "Migrated workspace: Editable -> Translations"
    fi
}

[[ -e "$OUT" ]] && migrate_legacy_workspace_names

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
mkdir -p "$OUT/Mods" "$OUT/Source" "$OUT/Translations" "$OUT/Output" "$OUT/tools"
[[ -f "$ROOT/tools/README.txt" ]] && cp -f "$ROOT/tools/README.txt" "$OUT/tools/README.txt"

for doc in README.md QUICK_USER_HANDBOOK.md CHANGELOG.md RC_CHECKLIST.md RELEASE_CANDIDATE.md; do
    [[ -f "$ROOT/$doc" ]] && cp -f "$ROOT/$doc" "$OUT/$doc"
done

echo
echo "Windows runtime: $OUT"
echo "The target Windows machine does NOT need the .NET runtime because this publish is self-contained."
echo "Runtime helper tools are NOT bundled. Put user-supplied tools in: $OUT/tools"
