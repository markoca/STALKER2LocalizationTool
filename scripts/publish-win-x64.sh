#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
PROJECT="$ROOT/STALKER2LocalizationTool.csproj"
PUBLISH_ROOT="$ROOT/publish"
OUT="$PUBLISH_ROOT/win-x64"

mkdir -p "$PUBLISH_ROOT"
STAGE="$(mktemp -d "$PUBLISH_ROOT/.win-x64-stage.XXXXXX")"
PRESERVE="$(mktemp -d "$PUBLISH_ROOT/.win-x64-preserve.XXXXXX")"

PRESERVED_NAMES=(settings.json tools locales Mods Cached Editable Output Extracted Ready)

restore_runtime_data() {
    mkdir -p "$OUT"
    for name in "${PRESERVED_NAMES[@]}"; do
        if [[ -e "$PRESERVE/$name" && ! -e "$OUT/$name" ]]; then
            mv "$PRESERVE/$name" "$OUT/$name"
        fi
    done
}

directory_has_entries() {
    local path="$1"
    [[ -d "$path" ]] && [[ -n "$(find "$path" -mindepth 1 -maxdepth 1 -print -quit 2>/dev/null)" ]]
}

migrate_workspace_name() {
    local old_name="$1"
    local new_name="$2"
    local old_path="$OUT/$old_name"
    local new_path="$OUT/$new_name"

    [[ -e "$old_path" ]] || return 0

    if [[ ! -e "$new_path" ]]; then
        mv "$old_path" "$new_path"
        echo "Workspace migrated: $old_name -> $new_name"
        return 0
    fi

    if [[ -d "$old_path" && -d "$new_path" ]]; then
        if ! directory_has_entries "$old_path"; then
            rmdir "$old_path" 2>/dev/null || true
            echo "Removed empty legacy workspace: $old_name"
            return 0
        fi

        if ! directory_has_entries "$new_path"; then
            rmdir "$new_path"
            mv "$old_path" "$new_path"
            echo "Workspace migrated: $old_name -> $new_name (empty destination replaced safely)"
            return 0
        fi
    fi

    echo "WARNING: both $old_name and $new_name contain data; nothing was merged or overwritten. The app will use only $new_name."
}

cleanup() {
    restore_runtime_data
    rm -rf "$STAGE" "$PRESERVE"
}
trap cleanup EXIT

echo "Preparing pinned UAssetGUI dependency..."
"$ROOT/scripts/fetch-uassetgui.sh"

echo "Publishing The STALKER2 Localization Tool (win-x64 single-file)..."
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

if [[ -d "$OUT" ]]; then
    for name in "${PRESERVED_NAMES[@]}"; do
        if [[ -e "$OUT/$name" ]]; then
            mv "$OUT/$name" "$PRESERVE/$name"
        fi
    done
    rm -rf "$OUT"
fi

mkdir -p "$OUT"
restore_runtime_data
migrate_workspace_name Extracted Cached
migrate_workspace_name Ready Editable

cp "$STAGE/STALKER2LocalizationTool.exe" "$OUT/STALKER2LocalizationTool.exe"
mkdir -p "$OUT/Mods" "$OUT/Cached" "$OUT/Editable" "$OUT/Output" "$OUT/tools" "$OUT/locales"

cp -f "$ROOT/locales/en.json" "$OUT/locales/en.json"
for doc in README.md CHANGELOG.md RC_CHECKLIST.md; do
    [[ -f "$ROOT/$doc" ]] && cp -f "$ROOT/$doc" "$OUT/$doc"
done

for tool in retoc.exe UAssetGUI.exe Mappings.usmap repak.exe; do
    if [[ -f "$ROOT/tools/$tool" ]]; then
        cp -f "$ROOT/tools/$tool" "$OUT/tools/$tool"
    fi
done

S2HOCMM_SOURCE=""
if [[ -f "$ROOT/tools/S2HOCMM.exe" ]]; then
    S2HOCMM_SOURCE="$ROOT/tools/S2HOCMM.exe"
fi
if [[ -n "$S2HOCMM_SOURCE" ]]; then
    cp -f "$S2HOCMM_SOURCE" "$OUT/tools/S2HOCMM.exe"
    echo "S2HOCMM prepared from: $S2HOCMM_SOURCE"
elif [[ ! -f "$OUT/tools/S2HOCMM.exe" && ! -f "$OUT/tools/S2HOCMM/S2HOCMM.exe" ]]; then
    echo "INFO: S2HOCMM was not bundled. The app can auto-detect a sibling ../S2HOCMM/S2HOCMM.exe or you can select it in Settings."
fi

echo
echo "Runtime: $OUT"
echo "Existing settings, work folders, and extra tool files were preserved."
echo "Legacy default workspaces are migrated safely to Cached/Editable; old defaults are never used for new runtime writes."
echo "Bundled tools were refreshed from $ROOT/tools; UAssetGUI was fetched/verified. S2HOCMM is copied only when explicitly present in tools/; sibling developer checkouts are auto-detected at runtime."
