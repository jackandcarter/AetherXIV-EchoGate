#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB_BIND="${WEB_BIND:-127.0.0.1}"
WEB_PORT="${WEB_PORT:-8080}"

export METEOR_DB_HOST="${METEOR_DB_HOST:-127.0.0.1}"
export METEOR_DB_PORT="${METEOR_DB_PORT:-3306}"
export METEOR_DB_NAME="${METEOR_DB_NAME:-ffxiv_server}"
export METEOR_DB_USER="${METEOR_DB_USER:-meteor}"
export METEOR_DB_PASS="${METEOR_DB_PASS:-meteor_dev}"

cd "$ROOT_DIR/Data/www"
exec php -S "$WEB_BIND:$WEB_PORT" -t .
