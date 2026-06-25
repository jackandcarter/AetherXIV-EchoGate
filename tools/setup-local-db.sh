#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

DROP_DATABASE="${DROP_DATABASE:-0}"
IMPORT_SQL=1
CREATE_APP_USER=1
PROMPT_ADMIN=1

usage() {
  cat <<'EOF'
Usage: tools/setup-local-db.sh [--drop] [--no-import] [--no-user] [--admin-sudo] [--no-prompt]

Creates or refreshes the local AetherXIV MariaDB database and app account.

Default created resources:
  database:       ffxiv_server
  app username:   meteor
  app password:   meteor_dev
  app hosts:      localhost, 127.0.0.1

Default setup needs no local env file. The script first tries the current OS
user, then Ubuntu-style sudo root socket auth, then asks for MariaDB admin
credentials when running in an interactive terminal.

Environment is read from .env.defaults, then .env.local when present.
Key values:
  DB_NAME          default ffxiv_server
  DB_HOST          default localhost
  DB_PORT          default 3306
  DB_ADMIN_USER    admin account used only for setup/import
  DB_ADMIN_PASS    admin password when needed
  DB_ADMIN_SUDO    set to 1 to run MariaDB admin commands through sudo
  DB_APP_USER      default meteor
  DB_APP_PASS      default meteor_dev
  DB_APP_HOST      default 127.0.0.1
  DB_APP_HOSTS     default "localhost 127.0.0.1"

Options:
  --drop           drop and recreate the database before importing
  --admin-sudo     use sudo root socket auth for admin commands
  --no-prompt      fail instead of asking for MariaDB admin credentials
EOF
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --drop)
      DROP_DATABASE=1
      shift
      ;;
    --no-import)
      IMPORT_SQL=0
      shift
      ;;
    --no-user)
      CREATE_APP_USER=0
      shift
      ;;
    --admin-sudo)
      DB_ADMIN_SUDO=1
      shift
      ;;
    --no-prompt)
      PROMPT_ADMIN=0
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "${MYSQL_BIN:-}" ]]; then
  if command -v mariadb >/dev/null 2>&1; then
    MYSQL_BIN="mariadb"
  else
    MYSQL_BIN="mysql"
  fi
fi

if ! command -v "$MYSQL_BIN" >/dev/null 2>&1; then
  echo "MariaDB/MySQL client not found. Install mariadb-client or mysql-client." >&2
  exit 2
fi

SQL_DIR="${SQL_DIR:-$ROOT_DIR/Data/sql}"
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}"
DB_ADMIN_USER="${DB_ADMIN_USER:-${DB_USER:-${USER:-root}}}"
DB_ADMIN_PASS="${DB_ADMIN_PASS:-${DB_PASS:-}}"
DB_ADMIN_SUDO="${DB_ADMIN_SUDO:-0}"
DB_APP_HOST="${DB_APP_HOST:-${METEOR_DB_HOST:-127.0.0.1}}"
DB_APP_PORT="${DB_APP_PORT:-${METEOR_DB_PORT:-3306}}"
DB_APP_USER="${DB_APP_USER:-${METEOR_DB_USER:-meteor}}"
DB_APP_PASS="${DB_APP_PASS:-${METEOR_DB_PASS:-meteor_dev}}"
DB_APP_HOSTS="${DB_APP_HOSTS:-localhost 127.0.0.1}"

if [[ " $DB_APP_HOSTS " != *" $DB_APP_HOST "* ]]; then
  DB_APP_HOSTS="$DB_APP_HOSTS $DB_APP_HOST"
fi

if [[ ! -d "$SQL_DIR" ]]; then
  echo "SQL directory not found: $SQL_DIR" >&2
  exit 2
fi

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

app_cmd=("$MYSQL_BIN" -h "$DB_APP_HOST" -P "$DB_APP_PORT" -u "$DB_APP_USER")
if [[ -n "$DB_APP_PASS" ]]; then
  app_cmd+=("-p$DB_APP_PASS")
fi

build_admin_cmd() {
  admin_cmd=()
  if [[ "$DB_ADMIN_SUDO" == "1" ]]; then
    admin_cmd+=(sudo)
  fi
  admin_cmd+=("$MYSQL_BIN" -h "$DB_HOST" -u "$DB_ADMIN_USER")
  if [[ "$DB_HOST" != "localhost" ]]; then
    admin_cmd+=(-P "$DB_PORT")
  fi
  if [[ -n "$DB_ADMIN_PASS" ]]; then
    admin_cmd+=("-p$DB_ADMIN_PASS")
  fi
}

run_admin_sql() {
  "${admin_cmd[@]}" -e "$1"
}

admin_label() {
  if [[ "$DB_ADMIN_SUDO" == "1" ]]; then
    printf 'sudo %s@%s' "$DB_ADMIN_USER" "$DB_HOST"
  else
    printf '%s@%s' "$DB_ADMIN_USER" "$DB_HOST"
  fi
}

prompt_admin_credentials() {
  if [[ "$PROMPT_ADMIN" != "1" || ! -t 0 || ! -t 1 ]]; then
    return 1
  fi

  echo
  echo "MariaDB admin login is needed to create the local database and app user."
  read -r -p "MariaDB admin username [root]: " prompted_admin_user
  prompted_admin_user="${prompted_admin_user:-root}"
  read -r -s -p "MariaDB admin password (leave blank for none): " prompted_admin_pass
  echo

  DB_ADMIN_USER="$prompted_admin_user"
  DB_ADMIN_PASS="$prompted_admin_pass"
  DB_ADMIN_SUDO=0
  build_admin_cmd
}

build_admin_cmd

echo "AetherXIV local database setup"
echo "Database to create: $DB_NAME"
echo "App account:        $DB_APP_USER / $DB_APP_PASS"
echo "App hosts:          $DB_APP_HOSTS"
echo "Admin connection:   $(admin_label)"
echo "App connection:     $DB_APP_USER@$DB_APP_HOST:$DB_APP_PORT/$DB_NAME"
echo

if ! run_admin_sql "SELECT 1;" >/dev/null 2>&1; then
  if [[ -z "${DB_USER:-}" && "${DB_ADMIN_USER}" == "${USER:-}" && "$DB_ADMIN_SUDO" != "1" ]] && command -v sudo >/dev/null 2>&1; then
    echo "Admin login as $DB_ADMIN_USER failed; trying Ubuntu-style sudo root socket auth."
    DB_ADMIN_USER=root
    DB_ADMIN_PASS=
    DB_ADMIN_SUDO=1
    build_admin_cmd
    if ! run_admin_sql "SELECT 1;" >/dev/null 2>&1; then
      if prompt_admin_credentials && run_admin_sql "SELECT 1;" >/dev/null 2>&1; then
        echo "Connected with MariaDB admin account: $(admin_label)"
      else
        echo "Could not connect with the MariaDB admin account." >&2
        echo "Run again with the correct admin username/password, or set DB_ADMIN_USER and DB_ADMIN_PASS in .env.local." >&2
        exit 20
      fi
    fi
  else
    if prompt_admin_credentials && run_admin_sql "SELECT 1;" >/dev/null 2>&1; then
      echo "Connected with MariaDB admin account: $(admin_label)"
    else
      echo "Could not connect with the MariaDB admin account." >&2
      echo "Run again with the correct admin username/password, or set DB_ADMIN_USER and DB_ADMIN_PASS in .env.local." >&2
      exit 20
    fi
  fi
fi

db_name_sql="$(sql_identifier "$DB_NAME")"

if [[ "$DROP_DATABASE" == "1" ]]; then
  echo "Dropping database: $DB_NAME"
  run_admin_sql "DROP DATABASE IF EXISTS \`$db_name_sql\`;"
fi

echo "Creating database if needed: $DB_NAME"
run_admin_sql "CREATE DATABASE IF NOT EXISTS \`$db_name_sql\` CHARACTER SET utf8 COLLATE utf8_general_ci;"

if [[ "$CREATE_APP_USER" == "1" ]]; then
  db_user_sql="$(sql_escape "$DB_APP_USER")"
  db_pass_sql="$(sql_escape "$DB_APP_PASS")"

  for app_host in $DB_APP_HOSTS; do
    app_host_sql="$(sql_escape "$app_host")"
    echo "Granting database access to '$DB_APP_USER'@'$app_host'"
    "${admin_cmd[@]}" <<SQL
CREATE USER IF NOT EXISTS '$db_user_sql'@'$app_host_sql' IDENTIFIED BY '$db_pass_sql';
ALTER USER '$db_user_sql'@'$app_host_sql' IDENTIFIED BY '$db_pass_sql';
GRANT ALL PRIVILEGES ON \`$db_name_sql\`.* TO '$db_user_sql'@'$app_host_sql';
SQL
  done
  run_admin_sql "FLUSH PRIVILEGES;"
fi

if [[ "$IMPORT_SQL" == "1" ]]; then
  echo "Importing SQL files from $SQL_DIR"
  for sql_file in "$SQL_DIR"/*.sql; do
    [[ -e "$sql_file" ]] || continue
    echo "Importing $(basename "$sql_file")"
    "${admin_cmd[@]}" "$DB_NAME" < "$sql_file"
  done
fi

if [[ "$CREATE_APP_USER" == "1" ]]; then
  echo "Validating app user"
  if "${app_cmd[@]}" "$DB_NAME" -e "SELECT 1;" >/dev/null 2>&1; then
    table_count="$("${app_cmd[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$(sql_escape "$DB_NAME")';" 2>/dev/null || true)"
    echo "Database ready: $DB_APP_USER@$DB_APP_HOST/$DB_NAME (${table_count:-unknown} tables)"
  else
    echo "Database was imported, but the app user could not connect." >&2
    echo "Check DB_APP_HOST, DB_APP_USER, DB_APP_PASS, and DB_APP_HOSTS in .env.local." >&2
    exit 21
  fi
else
  echo "Database setup finished; app user creation and validation were skipped."
fi
