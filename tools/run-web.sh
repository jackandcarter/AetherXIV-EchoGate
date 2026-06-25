#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
WEB_BIND="${WEB_BIND:-127.0.0.1}"
WEB_PORT="${WEB_PORT:-8080}"

export AETHER_DB_HOST="${AETHER_DB_HOST:-${DB_APP_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
export AETHER_DB_PORT="${AETHER_DB_PORT:-${DB_APP_PORT:-${METEOR_DB_PORT:-3306}}}"
export AETHER_DB_NAME="${AETHER_DB_NAME:-${DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
export AETHER_DB_USER="${AETHER_DB_USER:-${DB_APP_USER:-${METEOR_DB_USER:-aetherxiv}}}"
export AETHER_DB_PASS="${AETHER_DB_PASS:-${DB_APP_PASS:-${METEOR_DB_PASS:-aether_dev}}}"
export METEOR_DB_HOST="${METEOR_DB_HOST:-$AETHER_DB_HOST}"
export METEOR_DB_PORT="${METEOR_DB_PORT:-$AETHER_DB_PORT}"
export METEOR_DB_NAME="${METEOR_DB_NAME:-$AETHER_DB_NAME}"
export METEOR_DB_USER="${METEOR_DB_USER:-$AETHER_DB_USER}"
export METEOR_DB_PASS="${METEOR_DB_PASS:-$AETHER_DB_PASS}"

cd "$ROOT_DIR/Data/www"
exec php -S "$WEB_BIND:$WEB_PORT" -t .
