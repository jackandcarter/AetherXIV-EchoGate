#!/usr/bin/env bash
set -euo pipefail

MYSQL_BIN="${MYSQL_BIN:-mysql}"

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-ffxiv_server}"
DB_ADMIN_USER="${DB_ADMIN_USER:-${USER:-root}}"
DB_ADMIN_PASS="${DB_ADMIN_PASS:-}"
DB_APP_USER="${DB_APP_USER:-meteor}"
DB_APP_PASS="${DB_APP_PASS:-${METEOR_DB_PASS:-}}"
DB_APP_HOSTS="${DB_APP_HOSTS:-localhost 127.0.0.1}"

if [[ -z "$DB_APP_PASS" ]]; then
  echo "DB_APP_PASS or METEOR_DB_PASS is required. Set it in .env.local or the environment." >&2
  exit 1
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

for app_host in $DB_APP_HOSTS; do
  echo "Creating/updating '$DB_APP_USER'@'$app_host' for database '$DB_NAME'"
  "$MYSQL_BIN" "${mysql_args[@]}" <<SQL
CREATE USER IF NOT EXISTS '$DB_APP_USER'@'$app_host' IDENTIFIED BY '$DB_APP_PASS';
ALTER USER '$DB_APP_USER'@'$app_host' IDENTIFIED BY '$DB_APP_PASS';
GRANT ALL PRIVILEGES ON \`$DB_NAME\`.* TO '$DB_APP_USER'@'$app_host';
SQL
done

"$MYSQL_BIN" "${mysql_args[@]}" -e "FLUSH PRIVILEGES;"
echo "Database user ready: $DB_APP_USER"
