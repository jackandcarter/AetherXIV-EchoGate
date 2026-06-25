#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
CONFIGURATION="${CONFIGURATION:-Release}"
SERVER_IP="${SERVER_IP:-127.0.0.1}"
MAP_PORT="${MAP_PORT:-1989}"
DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"

if [[ "${REFRESH_RUNTIME_DATA:-1}" != "0" ]]; then
  "$ROOT_DIR/tools/copy-runtime-data.sh"
fi

cd "$ROOT_DIR/Map Server/bin/$CONFIGURATION"
exec mono "AetherXIV.Core.Map.exe" --ip "$SERVER_IP" --port "$MAP_PORT" --host "$DB_APP_HOST" --db "$DB_NAME" --user "$DB_APP_USER" --p "$DB_APP_PASS" "$@"
