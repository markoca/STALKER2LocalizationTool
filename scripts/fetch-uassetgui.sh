#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
DEST="$ROOT/tools/UAssetGUI.exe"
VERSION="v1.1.0"
URL="https://github.com/atenfyr/UAssetGUI/releases/download/${VERSION}/UAssetGUI.exe"
EXPECTED_SHA256="b7d75c0893f1a60e565853ae638bc21f2416cd12c2d9d854e297abb87ceb3263"

sha256_file() {
    sha256sum "$1" | awk '{print tolower($1)}'
}

if [[ -f "$DEST" ]]; then
    current="$(sha256_file "$DEST")"
    if [[ "$current" == "$EXPECTED_SHA256" ]]; then
        echo "UAssetGUI $VERSION already verified: $DEST"
        exit 0
    fi
    echo "Replacing non-pinned UAssetGUI.exe (sha256=$current)"
fi

mkdir -p "$(dirname "$DEST")"
tmp="$(mktemp "$ROOT/tools/.UAssetGUI.exe.download.XXXXXX")"
cleanup() { rm -f "$tmp"; }
trap cleanup EXIT

echo "Downloading upstream UAssetGUI $VERSION..."
if command -v curl >/dev/null 2>&1; then
    curl --fail --location --retry 3 --connect-timeout 20 --output "$tmp" "$URL"
elif command -v wget >/dev/null 2>&1; then
    wget --tries=3 --timeout=20 --output-document="$tmp" "$URL"
else
    echo "ERROR: curl or wget is required to fetch UAssetGUI." >&2
    echo "Download manually from: $URL" >&2
    exit 1
fi

actual="$(sha256_file "$tmp")"
if [[ "$actual" != "$EXPECTED_SHA256" ]]; then
    echo "ERROR: downloaded UAssetGUI.exe failed SHA-256 verification." >&2
    echo "Expected: $EXPECTED_SHA256" >&2
    echo "Actual:   $actual" >&2
    exit 1
fi

mv -f "$tmp" "$DEST"
trap - EXIT
echo "Installed verified UAssetGUI $VERSION -> $DEST"
echo "SHA-256: $EXPECTED_SHA256"
