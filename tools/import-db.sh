#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
SQL_DIR="${SQL_DIR:-$ROOT_DIR/Data/sql}"

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
DB_USER="${DB_USER:-${DB_ADMIN_USER:-${USER:-root}}}"
DB_PASS="${DB_PASS:-${DB_ADMIN_PASS:-}}"
DB_ADMIN_SUDO="${DB_ADMIN_SUDO:-0}"
DROP_DATABASE="${DROP_DATABASE:-0}"

if [[ ! -d "$SQL_DIR" ]]; then
  echo "SQL directory not found: $SQL_DIR" >&2
  exit 1
fi

if ! command -v "$MYSQL_BIN" >/dev/null 2>&1; then
  echo "mysql client not found: $MYSQL_BIN" >&2
  exit 1
fi

mysql_args=(-h "$DB_HOST" -u "$DB_USER")
if [[ "$DB_HOST" != "localhost" ]]; then
  mysql_args+=(-P "$DB_PORT")
fi
if [[ -n "$DB_PASS" ]]; then
  mysql_args+=("-p$DB_PASS")
fi
mysql_cmd=()
if [[ "$DB_ADMIN_SUDO" == "1" ]]; then
  mysql_cmd+=(sudo)
fi
mysql_cmd+=("$MYSQL_BIN")

sql_identifier() {
  local value="$1"
  value="${value//\`/\`\`}"
  printf '%s' "$value"
}

db_name_sql="$(sql_identifier "$DB_NAME")"

if [[ "$DB_HOST" == "localhost" ]]; then
  echo "Connecting to local MariaDB/MySQL socket as $DB_USER"
else
  echo "Connecting to $DB_HOST:$DB_PORT as $DB_USER"
fi

if [[ "$DROP_DATABASE" == "1" ]]; then
  echo "Dropping database: $DB_NAME"
  "${mysql_cmd[@]}" "${mysql_args[@]}" -e "DROP DATABASE IF EXISTS \`$db_name_sql\`;"
fi

echo "Creating database if needed: $DB_NAME"
"${mysql_cmd[@]}" "${mysql_args[@]}" -e "CREATE DATABASE IF NOT EXISTS \`$db_name_sql\` CHARACTER SET utf8 COLLATE utf8_general_ci;"

echo "Importing SQL files from $SQL_DIR"
for sql_file in "$SQL_DIR"/*.sql; do
  [[ -e "$sql_file" ]] || continue
  echo "Importing $(basename "$sql_file")"
  "${mysql_cmd[@]}" "${mysql_args[@]}" "$DB_NAME" < "$sql_file"
done

echo "Database import complete: $DB_NAME"
