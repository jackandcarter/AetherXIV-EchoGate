#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

status() {
  printf '%-18s %s\n' "$1" "$2"
}

check_command() {
  local name="$1"
  if command -v "$name" >/dev/null 2>&1; then
    status "$name" "ok: $(command -v "$name")"
  else
    status "$name" "missing"
  fi
}

check_package() {
  local name="$1"
  if command -v dpkg-query >/dev/null 2>&1; then
    if dpkg-query -W -f='${Status}' "$name" 2>/dev/null | grep -q "install ok installed"; then
      status "$name" "ok"
    else
      status "$name" "missing"
    fi
  else
    status "$name" "not checked"
  fi
}

echo "Project root: $ROOT_DIR"
echo
echo "Tooling"
check_command git
check_command mono
check_command msbuild
check_command xbuild
check_command nuget
check_command dotnet
check_command mysql
check_command php
echo

if command -v dotnet >/dev/null 2>&1; then
  echo ".NET SDK summary"
  dotnet --list-sdks || true
  echo
fi

echo "Runtime files"
if [[ -f "$ROOT_DIR/Data/staticactors.bin" ]]; then
  status "staticactors.bin" "ok"
else
  status "staticactors.bin" "missing: create from local 1.23b client before running Map Server"
fi

if [[ -d "$ROOT_DIR/packages" ]]; then
  status "packages" "present"
else
  status "packages" "missing: run nuget restore AetherXIV.Core.sln"
fi
echo

if command -v wine >/dev/null 2>&1 || command -v dpkg-query >/dev/null 2>&1; then
  echo "Wine graphics runtime"
  check_command wine
  check_command winetricks
  check_package libgl1:i386
  check_package libglx-mesa0:i386
  check_package libgl1-mesa-dri:i386
  check_package libglu1-mesa:i386
  check_package libvulkan1:i386
  check_package mesa-vulkan-drivers:i386
  echo
fi

echo "Git"
if git -C "$ROOT_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git -C "$ROOT_DIR" status --short --branch
  git -C "$ROOT_DIR" remote -v
else
  echo "not a git checkout"
fi
echo

echo "Database"
if command -v mysql >/dev/null 2>&1; then
  DB_HOST="${DB_HOST:-localhost}"
  DB_PORT="${DB_PORT:-3306}"
  DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
  DB_USER="${DB_USER:-${USER:-root}}"
  DB_PASS="${DB_PASS:-}"
  DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
  DB_APP_PORT="${DB_APP_PORT:-${AETHER_DB_PORT:-${METEOR_DB_PORT:-3306}}}"
  DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
  DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-}}}"

  mysql_args=(-h "$DB_HOST" -u "$DB_USER")
  if [[ "$DB_HOST" != "localhost" ]]; then
    mysql_args+=(-P "$DB_PORT")
  fi
  if [[ -n "$DB_PASS" ]]; then
    mysql_args+=("-p$DB_PASS")
  fi
  if mysql "${mysql_args[@]}" -e "SELECT 1;" >/dev/null 2>&1; then
    status "mysql local" "ok: $DB_USER@$DB_HOST"
  else
    status "mysql local" "not reachable as $DB_USER@$DB_HOST"
  fi

  app_args=(-h "$DB_APP_HOST" -P "$DB_APP_PORT" -u "$DB_APP_USER")
  if [[ -n "$DB_APP_PASS" ]]; then
    app_args+=("-p$DB_APP_PASS")
  fi
  if mysql "${app_args[@]}" -D "$DB_NAME" -e "SELECT 1;" >/dev/null 2>&1; then
    table_count="$(mysql "${app_args[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$DB_NAME';" 2>/dev/null || true)"
    status "mysql app user" "ok: $DB_APP_USER@$DB_APP_HOST/$DB_NAME (${table_count:-unknown} tables)"
  else
    status "mysql app user" "not reachable as $DB_APP_USER@$DB_APP_HOST/$DB_NAME"
  fi
else
  status "mysql local" "mysql client missing"
fi
