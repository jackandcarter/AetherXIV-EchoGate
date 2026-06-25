#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
ALLOW_MISSING_STATICACTORS=0

for arg in "$@"; do
  case "$arg" in
    --allow-missing-staticactors)
      ALLOW_MISSING_STATICACTORS=1
      ;;
    *)
      echo "unknown argument: $arg" >&2
      exit 10
      ;;
  esac
done

CONFIGURATION="${CONFIGURATION:-Release}"
SERVER_IP="${SERVER_IP:-127.0.0.1}"
LOBBY_PORT="${LOBBY_PORT:-54994}"
WORLD_PORT="${WORLD_PORT:-54992}"
MAP_PORT="${MAP_PORT:-1989}"
DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"

run_server_smoke() {
  local server_name="$1"
  local server_dir="$2"
  local exe_name="$3"
  local port="$4"

  echo
  echo "Smoke: $server_name"
  (
    cd "$ROOT_DIR/$server_dir/bin/$CONFIGURATION"
    mono "$exe_name" --ip "$SERVER_IP" --port "$port" --host "$DB_APP_HOST" --db "$DB_NAME" --user "$DB_APP_USER" --p "$DB_APP_PASS" --smoke
  )
}

echo "Smoke baseline"
"$ROOT_DIR/tools/audit-env.sh"

echo
echo "Build"
RESTORE="${RESTORE:-0}" "$ROOT_DIR/tools/build-legacy.sh"

echo
echo "Runtime data"
CONFIGURATION="$CONFIGURATION" "$ROOT_DIR/tools/copy-runtime-data.sh"

echo
echo "PHP lint"
while IFS= read -r php_file; do
  php -l "$php_file" >/dev/null
  echo "ok: $php_file"
done < <(find "$ROOT_DIR/Data/www" -name '*.php' -print | sort)

run_server_smoke "Lobby" "Lobby Server" "AetherXIV.Core.Lobby.exe" "$LOBBY_PORT"
run_server_smoke "World" "World Server" "AetherXIV.Core.World.exe" "$WORLD_PORT"

if [[ -f "$ROOT_DIR/Data/staticactors.bin" ]]; then
  run_server_smoke "Map" "Map Server" "AetherXIV.Core.Map.exe" "$MAP_PORT"
elif [[ "$ALLOW_MISSING_STATICACTORS" -eq 1 ]]; then
  echo
  echo "SMOKE_SKIP Map runtime prerequisite: Data/staticactors.bin is missing"
else
  echo
  run_server_smoke "Map" "Map Server" "AetherXIV.Core.Map.exe" "$MAP_PORT"
fi
