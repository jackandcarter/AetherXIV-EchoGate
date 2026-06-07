#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

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
  status "packages" "missing: run nuget restore Meteor.sln"
fi
echo

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
  DB_NAME="${DB_NAME:-ffxiv_server}"
  DB_USER="${DB_USER:-${USER:-root}}"
  DB_PASS="${DB_PASS:-}"
  DB_APP_HOST="${DB_APP_HOST:-127.0.0.1}"
  DB_APP_PORT="${DB_APP_PORT:-3306}"
  DB_APP_USER="${DB_APP_USER:-meteor}"
  DB_APP_PASS="${DB_APP_PASS:-meteor_dev}"

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
