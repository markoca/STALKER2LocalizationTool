#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT/LocalizationWorkbench.csproj"
if [[ ! -f "$PROJECT" ]]; then
    echo "ERROR: project file not found: $PROJECT" >&2
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

migrate_legacy_workspace_directory() {
    local legacy_name="$1"
    local current_name="$2"
    local legacy="$OUT/$legacy_name"
    local current="$OUT/$current_name"

    [[ -e "$legacy" ]] || return 0

    if [[ -e "$current" ]]; then
        if [[ -d "$current" && -z "$(find "$current" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
            rmdir "$current"
        else
            echo "ERROR: both legacy $legacy_name and populated current $current_name exist in $OUT; refusing to merge automatically." >&2
            exit 1
        fi
    fi

    mv "$legacy" "$current"
    echo "Migrated workspace: $legacy_name -> $current_name"
}

migrate_legacy_workspace_names() {
    migrate_legacy_workspace_directory "Cached" "Source"
    migrate_legacy_workspace_directory "Editable" "Translations"
}

[[ -e "$OUT" ]] && migrate_legacy_workspace_names

echo "Cross-publishing Localization Workbench (win-x64, self-contained)..."
echo "Build host: $(uname -s) / $(uname -m)"

dotnet publish "$PROJECT" \
  -c Release \
  -r win-x64 \
  --self-contained true \
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
