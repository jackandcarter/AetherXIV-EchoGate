#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

CLIENT_DIR="${CLIENT_DIR:-}"
OUTPUT_PATH="${OUTPUT_PATH:-$ROOT_DIR/Data/staticactors.bin}"
PROMPT_CLIENT_DIR=1

usage() {
  cat <<'EOF'
Usage: tools/prepare-client-data.sh [CLIENT_DIR]
       tools/prepare-client-data.sh --client-dir /path/to/FINAL_FANTASY_XIV

Prepares Data/staticactors.bin from a local user-owned FFXIV 1.x client.
The script checks, in order:
  1. --client-dir or CLIENT_DIR
  2. Echo Gate's saved client path
  3. common local client folders
  4. an interactive prompt when the terminal allows it

Client-derived files remain local and excluded from version control.
EOF
}

normalize_input_path() {
  local path="$1"
  path="${path#"${path%%[![:space:]]*}"}"
  path="${path%"${path##*[![:space:]]}"}"

  if [[ "$path" == \"*\" && "$path" == *\" ]]; then
    path="${path:1:${#path}-2}"
  elif [[ "$path" == \'*\' && "$path" == *\' ]]; then
    path="${path:1:${#path}-2}"
  fi

  if [[ "$path" == "~" ]]; then
    path="$HOME"
  elif [[ "$path" == "~/"* ]]; then
    path="$HOME/${path#"~/"}"
  fi

  printf '%s' "$path"
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --client-dir)
      CLIENT_DIR="$(normalize_input_path "${2:-}")"
      shift 2
      ;;
    --output)
      OUTPUT_PATH="$(normalize_input_path "${2:-}")"
      shift 2
      ;;
    --no-prompt)
      PROMPT_CLIENT_DIR=0
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      if [[ -z "$CLIENT_DIR" ]]; then
        CLIENT_DIR="$(normalize_input_path "$1")"
        shift
      else
        echo "unknown argument: $1" >&2
        usage >&2
        exit 2
      fi
      ;;
  esac
done

add_candidate_dir() {
  local dir="$1"
  dir="$(normalize_input_path "$dir")"
  [[ -n "$dir" ]] || return 0
  CLIENT_DIR_CANDIDATES+=("$dir")
}

read_echo_gate_client_dir() {
  local profile_path="$1"
  [[ -f "$profile_path" ]] || return 0

  perl -0ne '
    if (/"ClientRootPath"\s*:\s*"((?:\\.|[^"\\])*)"/) {
      my $value = $1;
      $value =~ s#\\/#/#g;
      $value =~ s#\\\\#\\#g;
      $value =~ s#\\"#"#g;
      print "$value\n";
      exit 0;
    }
  ' "$profile_path"
}

find_staticactors_source() {
  local client_dir="$1"
  local candidate
  local recursive_candidate
  local candidates=(
    "$client_dir/client/script/rq9q1797qvs.san"
    "$client_dir/script/rq9q1797qvs.san"
    "$client_dir/rq9q1797qvs.san"
    "$client_dir/client/script/staticactors.bin"
    "$client_dir/script/staticactors.bin"
    "$client_dir/staticactors.bin"
  )

  if [[ -f "$client_dir" ]]; then
    case "$(basename "$client_dir" | tr '[:upper:]' '[:lower:]')" in
      rq9q1797qvs.san|staticactors.bin)
        printf '%s' "$client_dir"
        return 0
        ;;
    esac
  fi

  for candidate in "${candidates[@]}"; do
    if [[ -f "$candidate" ]]; then
      printf '%s' "$candidate"
      return 0
    fi
  done

  if [[ -d "$client_dir" ]]; then
    recursive_candidate="$(
      find "$client_dir" -type f \( -iname 'rq9q1797qvs.san' -o -iname 'staticactors.bin' \) -print -quit 2>/dev/null || true
    )"
    if [[ -n "$recursive_candidate" ]]; then
      printf '%s' "$recursive_candidate"
      return 0
    fi
  fi

  return 1
}

CLIENT_DIR_CANDIDATES=()
add_candidate_dir "$CLIENT_DIR"

profile_candidates=()
if [[ -n "${ECHO_GATE_PROFILE_PATH:-}" ]]; then
  profile_candidates+=("$ECHO_GATE_PROFILE_PATH")
fi
if [[ -n "${XDG_DATA_HOME:-}" ]]; then
  profile_candidates+=("$XDG_DATA_HOME/Demi Dev Unit/Echo Gate/profile.json")
fi
profile_candidates+=(
  "$HOME/.local/share/Demi Dev Unit/Echo Gate/profile.json"
  "$HOME/.config/Demi Dev Unit/Echo Gate/profile.json"
  "$HOME/Library/Application Support/Demi Dev Unit/Echo Gate/profile.json"
)

for profile_path in "${profile_candidates[@]}"; do
  profile_client_dir="$(read_echo_gate_client_dir "$profile_path" || true)"
  add_candidate_dir "$profile_client_dir"
done

add_candidate_dir "$ROOT_DIR/launcher/EchoGate/Client"
add_candidate_dir "$HOME/.wine/drive_c/Program Files (x86)/SquareEnix/FINAL FANTASY XIV"
add_candidate_dir "$HOME/.wine/drive_c/Program Files/SquareEnix/FINAL FANTASY XIV"
add_candidate_dir "$HOME/wine/drive_c/Program Files (x86)/SquareEnix/FINAL FANTASY XIV"
add_candidate_dir "$HOME/wine/drive_c/Program Files/SquareEnix/FINAL FANTASY XIV"
add_candidate_dir "$HOME/Desktop/FINAL FANTASY XIV"
add_candidate_dir "$HOME/Desktop/FFXIV"
add_candidate_dir "$HOME/FINAL FANTASY XIV"

source_path=""
source_client_dir=""
for candidate_dir in "${CLIENT_DIR_CANDIDATES[@]}"; do
  if source_path="$(find_staticactors_source "$candidate_dir")"; then
    source_client_dir="$candidate_dir"
    break
  fi
done

if [[ -z "$source_path" && "$PROMPT_CLIENT_DIR" == "1" && -t 0 && -t 1 ]]; then
  echo "Could not find static actor data automatically."
  read -r -p "Path to your FFXIV 1.x client folder: " prompted_client_dir
  prompted_client_dir="$(normalize_input_path "$prompted_client_dir")"
  if [[ -n "$prompted_client_dir" ]]; then
    if source_path="$(find_staticactors_source "$prompted_client_dir")"; then
      source_client_dir="$prompted_client_dir"
    fi
  fi
fi

if [[ -z "$source_path" ]]; then
  echo "Could not find static actor data in a local FFXIV 1.x client." >&2
  echo "Expected one of these files under the client folder:" >&2
  echo "  client/script/rq9q1797qvs.san" >&2
  echo "  client/script/staticactors.bin" >&2
  echo "The script also searches recursively by those filenames." >&2
  echo >&2
  echo "Tried client folders:" >&2
  for candidate_dir in "${CLIENT_DIR_CANDIDATES[@]}"; do
    echo "  $candidate_dir" >&2
  done
  echo >&2
  echo "Run with: CLIENT_DIR=\"/path/to/FINAL FANTASY XIV\" ./tools/prepare-client-data.sh" >&2
  echo "Or pass the source file directly if you find it manually." >&2
  exit 1
fi

mkdir -p "$(dirname "$OUTPUT_PATH")"
cp "$source_path" "$OUTPUT_PATH"
echo "Prepared static actor data:"
echo "  client: $source_client_dir"
echo "  source: $source_path"
echo "  output: $OUTPUT_PATH"
echo "Repository policy: client-derived assets remain local and excluded from version control."
