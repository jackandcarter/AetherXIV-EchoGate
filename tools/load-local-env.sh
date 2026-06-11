# shellcheck shell=bash

if [[ -n "${ROOT_DIR:-}" && -f "$ROOT_DIR/.env.local" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$ROOT_DIR/.env.local"
  set +a
fi
