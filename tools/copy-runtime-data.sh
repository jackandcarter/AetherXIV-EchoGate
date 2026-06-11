#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
CONFIGURATION="${CONFIGURATION:-Release}"
DB_NAME="${DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}"
DB_APP_HOST="${DB_APP_HOST:-${METEOR_DB_HOST:-127.0.0.1}}"
DB_APP_PORT="${DB_APP_PORT:-${METEOR_DB_PORT:-3306}}"
DB_APP_USER="${DB_APP_USER:-${METEOR_DB_USER:-meteor}}"
DB_APP_PASS="${DB_APP_PASS:-${METEOR_DB_PASS:-}}"

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

copy_file "$ROOT_DIR/Data/lobby_config.ini" "$ROOT_DIR/Lobby Server/bin/$CONFIGURATION"
copy_file "$ROOT_DIR/Data/world_config.ini" "$ROOT_DIR/World Server/bin/$CONFIGURATION"
copy_file "$ROOT_DIR/Data/map_config.ini" "$ROOT_DIR/Map Server/bin/$CONFIGURATION"
hydrate_db_config "$ROOT_DIR/Lobby Server/bin/$CONFIGURATION/lobby_config.ini"
hydrate_db_config "$ROOT_DIR/World Server/bin/$CONFIGURATION/world_config.ini"
hydrate_db_config "$ROOT_DIR/Map Server/bin/$CONFIGURATION/map_config.ini"
copy_dir "$ROOT_DIR/Data/scripts" "$ROOT_DIR/Map Server/bin/$CONFIGURATION"

if [[ -f "$ROOT_DIR/Data/staticactors.bin" ]]; then
  copy_file "$ROOT_DIR/Data/staticactors.bin" "$ROOT_DIR/Map Server/bin/$CONFIGURATION"
else
  echo "warning: Data/staticactors.bin is missing; Map Server will not be runtime-ready."
fi
