# shellcheck shell=bash
# Internal helper. Source it from another script or shell; running it directly
# cannot change the parent shell's environment.

if [[ -z "${ROOT_DIR:-}" ]]; then
  _METEOR_ENV_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
else
  _METEOR_ENV_ROOT="$ROOT_DIR"
fi

if [[ -f "$_METEOR_ENV_ROOT/.env.defaults" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$_METEOR_ENV_ROOT/.env.defaults"
  set +a
fi

if [[ -f "$_METEOR_ENV_ROOT/.env.local" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "$_METEOR_ENV_ROOT/.env.local"
  set +a
fi

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  echo "This helper was loaded in a child shell only."
  echo "To load .env.defaults and optional .env.local into your current shell, run: source tools/load-local-env.sh"
fi

unset _METEOR_ENV_ROOT
