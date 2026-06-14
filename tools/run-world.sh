#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
CONFIGURATION="${CONFIGURATION:-Release}"
SERVER_IP="${SERVER_IP:-127.0.0.1}"
WORLD_PORT="${WORLD_PORT:-54992}"
DB_NAME="${DB_NAME:-ffxiv_server}"
DB_APP_HOST="${DB_APP_HOST:-127.0.0.1}"
DB_APP_USER="${DB_APP_USER:-meteor}"
DB_APP_PASS="${DB_APP_PASS:-${METEOR_DB_PASS:-meteor_dev}}"

cd "$ROOT_DIR/World Server/bin/$CONFIGURATION"
exec mono "MeteorXIV.Core.World.exe" --ip "$SERVER_IP" --port "$WORLD_PORT" --host "$DB_APP_HOST" --db "$DB_NAME" --user "$DB_APP_USER" --p "$DB_APP_PASS" "$@"
