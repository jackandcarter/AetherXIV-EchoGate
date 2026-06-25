#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

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
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"
DB_APP_HOSTS="${DB_APP_HOSTS:-localhost 127.0.0.1}"

if [[ " $DB_APP_HOSTS " != *" $DB_APP_HOST "* ]]; then
  DB_APP_HOSTS="$DB_APP_HOSTS $DB_APP_HOST"
fi

if ! command -v "$MYSQL_BIN" >/dev/null 2>&1; then
  echo "mysql client not found: $MYSQL_BIN" >&2
  exit 1
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

sql_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\'/\\\'}"
  printf '%s' "$value"
}

sql_identifier() {
  local value="$1"
  value="${value//\`/\`\`}"
  printf '%s' "$value"
}

db_name_sql="$(sql_identifier "$DB_NAME")"
db_user_sql="$(sql_escape "$DB_APP_USER")"
db_pass_sql="$(sql_escape "$DB_APP_PASS")"

for app_host in $DB_APP_HOSTS; do
  app_host_sql="$(sql_escape "$app_host")"
  echo "Creating/updating '$DB_APP_USER'@'$app_host' for database '$DB_NAME'"
  "${mysql_cmd[@]}" "${mysql_args[@]}" <<SQL
CREATE USER IF NOT EXISTS '$db_user_sql'@'$app_host_sql' IDENTIFIED BY '$db_pass_sql';
ALTER USER '$db_user_sql'@'$app_host_sql' IDENTIFIED BY '$db_pass_sql';
GRANT ALL PRIVILEGES ON \`$db_name_sql\`.* TO '$db_user_sql'@'$app_host_sql';
SQL
done

"${mysql_cmd[@]}" "${mysql_args[@]}" -e "FLUSH PRIVILEGES;"
echo "Database user ready: $DB_APP_USER"
