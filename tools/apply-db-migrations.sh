#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

MIGRATIONS_DIR="${MIGRATIONS_DIR:-$ROOT_DIR/Data/sql/migrations}"

if [[ -z "${MYSQL_BIN:-}" ]]; then
  if command -v mariadb >/dev/null 2>&1; then
    MYSQL_BIN="mariadb"
  else
    MYSQL_BIN="mysql"
  fi
fi

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_ADMIN_USER="${DB_ADMIN_USER:-${DB_USER:-${USER:-root}}}"
DB_ADMIN_PASS="${DB_ADMIN_PASS:-${DB_PASS:-}}"
DB_ADMIN_SUDO="${DB_ADMIN_SUDO:-0}"

if [[ ! -d "$MIGRATIONS_DIR" ]]; then
  echo "Migration directory not found: $MIGRATIONS_DIR" >&2
  exit 2
fi

if ! command -v "$MYSQL_BIN" >/dev/null 2>&1; then
  echo "MariaDB/MySQL client not found: $MYSQL_BIN" >&2
  exit 2
fi

mysql_args=(-h "$DB_HOST" -u "$DB_ADMIN_USER")
if [[ "$DB_HOST" != "localhost" ]]; then
  mysql_args+=(-P "$DB_PORT")
fi
if [[ -n "$DB_ADMIN_PASS" ]]; then
  mysql_args+=("-p$DB_ADMIN_PASS")
fi

mysql_cmd=()
if [[ "$DB_ADMIN_SUDO" == "1" ]]; then
  mysql_cmd+=(sudo)
fi
mysql_cmd+=("$MYSQL_BIN")

echo "Applying AetherXIV DB migrations to $DB_NAME from $MIGRATIONS_DIR"
for sql_file in "$MIGRATIONS_DIR"/*.sql; do
  [[ -e "$sql_file" ]] || continue
  echo "Applying $(basename "$sql_file")"
  "${mysql_cmd[@]}" "${mysql_args[@]}" "$DB_NAME" < "$sql_file"
done
echo "Database migrations complete: $DB_NAME"
