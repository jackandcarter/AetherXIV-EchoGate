#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"

copy_file() {
  local source="$1"
  local dest_dir="$2"
  if [[ ! -f "$source" ]]; then
    echo "missing: $source"
    return 1
  fi
  mkdir -p "$dest_dir"
  cp "$source" "$dest_dir/"
  echo "copied: $source -> $dest_dir/"
}

copy_dir() {
  local source="$1"
  local dest_dir="$2"
  if [[ ! -d "$source" ]]; then
    echo "missing: $source"
    return 1
  fi
  mkdir -p "$dest_dir"
  rm -rf "$dest_dir/$(basename "$source")"
  cp -R "$source" "$dest_dir/"
  echo "copied: $source -> $dest_dir/"
}

echo "Using configuration: $CONFIGURATION"

copy_file "$ROOT_DIR/Data/lobby_config.ini" "$ROOT_DIR/Lobby Server/bin/$CONFIGURATION"
copy_file "$ROOT_DIR/Data/world_config.ini" "$ROOT_DIR/World Server/bin/$CONFIGURATION"
copy_file "$ROOT_DIR/Data/map_config.ini" "$ROOT_DIR/Map Server/bin/$CONFIGURATION"
copy_dir "$ROOT_DIR/Data/scripts" "$ROOT_DIR/Map Server/bin/$CONFIGURATION"

if [[ -f "$ROOT_DIR/Data/staticactors.bin" ]]; then
  copy_file "$ROOT_DIR/Data/staticactors.bin" "$ROOT_DIR/Map Server/bin/$CONFIGURATION"
else
  echo "warning: Data/staticactors.bin is missing; Map Server will not be runtime-ready."
fi
