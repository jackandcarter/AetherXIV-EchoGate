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

aether_server_executable_name() {
  local server_name="$1"
  printf 'AetherXIV.Core.%s.exe' "${server_name%% *}"
}

resolve_server_directory() {
  local server_name="$1"
  local configuration="${2:-${CONFIGURATION:-Release}}"
  local exe_name
  exe_name="$(aether_server_executable_name "$server_name")"
  local source_build="$ROOT_DIR/$server_name/bin/$configuration"
  local release_layout="$ROOT_DIR/$server_name"

  if [[ -f "$source_build/$exe_name" ]]; then
    printf '%s\n' "$source_build"
  elif [[ -f "$release_layout/$exe_name" ]]; then
    printf '%s\n' "$release_layout"
  elif [[ -d "$source_build" ]]; then
    printf '%s\n' "$source_build"
  else
    printf '%s\n' "$release_layout"
  fi
}

resolve_server_executable() {
  local server_name="$1"
  local configuration="${2:-${CONFIGURATION:-Release}}"
  local exe_name
  exe_name="$(aether_server_executable_name "$server_name")"
  local server_dir
  server_dir="$(resolve_server_directory "$server_name" "$configuration")"
  local server_exe="$server_dir/$exe_name"

  if [[ -f "$server_exe" ]]; then
    printf '%s\n' "$server_exe"
    return 0
  fi

  local legacy_name="MeteorXIV.Core.${server_name%% *}.exe"
  local legacy_path
  for legacy_path in "$ROOT_DIR/$server_name/bin/$configuration/$legacy_name" "$ROOT_DIR/$server_name/$legacy_name"; do
    if [[ -f "$legacy_path" ]]; then
      echo "$server_name still has a legacy MeteorXIV executable at $legacy_path, but the AetherXIV launch scripts require $exe_name. Rebuild the server core or download a current AetherXIV Server Core release package." >&2
      return 1
    fi
  done

  echo "$server_name executable not found: $server_exe. Build the legacy servers with CONFIGURATION=$configuration ./tools/build-legacy.sh, or use a current AetherXIV Server Core release package." >&2
  return 1
}
