#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
CONFIGURATION="${CONFIGURATION:-Release}"
DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_PORT="${DB_APP_PORT:-${AETHER_DB_PORT:-${METEOR_DB_PORT:-3306}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"

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

set_ini_value() {
  local file="$1"
  local key="$2"
  local value="$3"

  VALUE="$value" KEY="$key" perl -0pi -e 'my $key = $ENV{KEY}; my $value = $ENV{VALUE}; s/^\Q$key\E=.*/$key=$value/m' "$file"
}

hydrate_db_config() {
  local file="$1"

  set_ini_value "$file" "host" "$DB_APP_HOST"
  set_ini_value "$file" "port" "$DB_APP_PORT"
  set_ini_value "$file" "database" "$DB_NAME"
  set_ini_value "$file" "username" "$DB_APP_USER"
  set_ini_value "$file" "password" "$DB_APP_PASS"
}

echo "Using configuration: $CONFIGURATION"

LOBBY_DIR="$(resolve_server_directory "Lobby Server" "$CONFIGURATION")"
WORLD_DIR="$(resolve_server_directory "World Server" "$CONFIGURATION")"
MAP_DIR="$(resolve_server_directory "Map Server" "$CONFIGURATION")"

copy_file "$ROOT_DIR/Data/lobby_config.ini" "$LOBBY_DIR"
copy_file "$ROOT_DIR/Data/world_config.ini" "$WORLD_DIR"
copy_file "$ROOT_DIR/Data/map_config.ini" "$MAP_DIR"
hydrate_db_config "$LOBBY_DIR/lobby_config.ini"
hydrate_db_config "$WORLD_DIR/world_config.ini"
hydrate_db_config "$MAP_DIR/map_config.ini"
copy_dir "$ROOT_DIR/Data/scripts" "$MAP_DIR"

if [[ ! -f "$ROOT_DIR/Data/staticactors.bin" && "${AUTO_PREPARE_STATICACTORS:-1}" != "0" ]]; then
  prepare_args=()
  if [[ ! -t 0 || ! -t 1 ]]; then
    prepare_args+=(--no-prompt)
  fi

  if "$ROOT_DIR/tools/prepare-client-data.sh" "${prepare_args[@]}"; then
    echo "prepared: Data/staticactors.bin"
  else
    echo "warning: static actor data was not prepared automatically."
  fi
fi

if [[ -f "$ROOT_DIR/Data/staticactors.bin" ]]; then
  copy_file "$ROOT_DIR/Data/staticactors.bin" "$MAP_DIR"
else
  echo "warning: Data/staticactors.bin is missing; Map Server will not be runtime-ready."
  echo "hint: run CLIENT_DIR=\"/path/to/FINAL FANTASY XIV\" ./tools/prepare-client-data.sh"
fi
