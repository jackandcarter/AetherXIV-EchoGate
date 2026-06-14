#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LINE_COUNT=120
FILE_COUNT=8

usage() {
  cat <<'EOF'
Usage: tools/collect-echo-gate-logs.sh [--lines N] [--files N]

Prints the newest Echo Gate launch/runtime logs with session values redacted.
Run this after a failed "Log In & Play" attempt.
EOF
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --lines)
      LINE_COUNT="${2:-}"
      shift 2
      ;;
    --files)
      FILE_COUNT="${2:-}"
      shift 2
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

if ! [[ "$LINE_COUNT" =~ ^[0-9]+$ && "$FILE_COUNT" =~ ^[0-9]+$ ]]; then
  echo "--lines and --files must be positive numbers." >&2
  exit 2
fi

log_dirs=()
if [[ -n "${ECHO_GATE_LOGS_DIR:-}" ]]; then
  log_dirs+=("$ECHO_GATE_LOGS_DIR")
fi
if [[ -n "${XDG_DATA_HOME:-}" ]]; then
  log_dirs+=("$XDG_DATA_HOME/Demi Dev Unit/Echo Gate/Logs")
fi
log_dirs+=(
  "$HOME/.local/share/Demi Dev Unit/Echo Gate/Logs"
  "$HOME/Library/Application Support/Demi Dev Unit/Echo Gate/Logs"
  "$ROOT_DIR/launcher/EchoGate/Logs"
)

files=()
for dir in "${log_dirs[@]}"; do
  [[ -d "$dir" ]] || continue
  while IFS= read -r -d '' file; do
    files+=("$file")
  done < <(find "$dir" -maxdepth 1 -type f -name '*.log' -print0 2>/dev/null)
done

echo "Echo Gate log folders checked:"
for dir in "${log_dirs[@]}"; do
  echo "  $dir"
done

if [[ "${#files[@]}" -eq 0 ]]; then
  echo
  echo "No Echo Gate .log files were found."
  exit 1
fi

mapfile -t sorted_files < <(ls -1t "${files[@]}" 2>/dev/null | head -n "$FILE_COUNT")

echo
echo "Newest log files:"
for file in "${sorted_files[@]}"; do
  echo "  $file"
done

redact_log() {
  perl -pe 's/(--session(?:\s+|=))(?:"[^"]*"|'\''[^'\'']*'\''|\S+)/$1<redacted>/ig; s/^(session=).*/$1<redacted>/i; s/(sessionId=)[A-Za-z0-9+\/=_-]+/$1<redacted>/ig'
}

for file in "${sorted_files[@]}"; do
  echo
  echo "===== $file ====="
  redact_log < "$file" | tail -n "$LINE_COUNT"
done
