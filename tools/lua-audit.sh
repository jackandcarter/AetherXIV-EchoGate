#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT_DIR="$ROOT_DIR/Data/scripts"
STRICT=0
FINDINGS=0
MAX_RESULTS="${LUA_AUDIT_MAX_RESULTS:-80}"

usage() {
  cat <<'EOF'
Usage: tools/lua-audit.sh [--strict] [--max-results N]

Audits Lua scripts for known AetherXIV Core reverse-engineering hazards.
The audit is informational by default. Use --strict to exit non-zero when
findings are present.
EOF
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --strict)
      STRICT=1
      shift
      ;;
    --max-results)
      MAX_RESULTS="${2:-}"
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

if ! [[ "$MAX_RESULTS" =~ ^[0-9]+$ ]]; then
  echo "invalid --max-results value: $MAX_RESULTS" >&2
  exit 2
fi

if [[ ! -d "$SCRIPT_DIR" ]]; then
  echo "missing script directory: $SCRIPT_DIR" >&2
  exit 2
fi

if command -v rg >/dev/null 2>&1; then
  SEARCH=(rg -n --glob '*.lua')
else
  SEARCH=(grep -RInE --include='*.lua')
fi

check_pattern() {
  local title="$1"
  local pattern="$2"

  echo
  echo "== $title =="
  matches="$("${SEARCH[@]}" "$pattern" "$SCRIPT_DIR" || true)"
  if [[ -n "$matches" ]]; then
    count="$(printf '%s\n' "$matches" | wc -l | tr -d ' ')"
    printf '%s\n' "$matches" | sed -n "1,${MAX_RESULTS}p"
    if [[ "$count" -gt "$MAX_RESULTS" ]]; then
      echo "... $((count - MAX_RESULTS)) more"
    fi
    echo "findings: $count"
    FINDINGS=1
  else
    echo "ok"
  fi
}

echo "Lua audit: $SCRIPT_DIR"

check_pattern \
  "Lowercase opening quest lookups" \
  '\b(GetQuest|HasQuest|IsQuestCompleted|CanAcceptQuest)\("man0[ulg]0"\)'

check_pattern \
  "Lua calls using C# method names with wrong casing" \
  ':(kickEvent|kickEventSpecial|endEvent|startEvent|updateEvent|runEventFunction)\b'

check_pattern \
  "Unsafe director callbacks without nil guard" \
  'GetDirector\("[^"]+"\):'

check_pattern \
  "Temporary debug print markers" \
  'print[[:space:]]*(\(|)[[:space:]]*["'\'']AAAA'

if [[ "$FINDINGS" -eq 0 ]]; then
  echo
  echo "Lua audit passed."
elif [[ "$STRICT" -eq 1 ]]; then
  echo
  echo "Lua audit found issues."
  exit 1
else
  echo
  echo "Lua audit found issues. Re-run with --strict to fail on findings."
fi
