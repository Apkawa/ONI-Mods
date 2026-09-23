#!/usr/bin/env bash
set -euo pipefail

# Convert the SpaceOverlay menu icon (icon.svg) to a 48x48 PNG for the mod's ModAssets.
# Works from any cwd: all paths are resolved relative to this script's location.

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
src="$script_dir/icon.svg"
dst="$script_dir/ModAssets/overlay_space.png"

if ! command -v rsvg-convert >/dev/null 2>&1; then
    echo "error: rsvg-convert not found in PATH (install librsvg2-bin)" >&2
    exit 1
fi

mkdir -p -- "$(dirname -- "$dst")"
rsvg-convert -w 48 -h 48 "$src" -o "$dst"
echo "wrote $dst"
