#!/usr/bin/env sh
set -eu

usage() {
  echo "Usage: $0 /path/to/FINAL-FANTASY-XIV [cut-prefix]"
  echo
  echo "Indexes visible strings from local client cut bundles and script containers."
  echo "Default cut-prefix: man0u"
  echo
  echo "Examples:"
  echo "  $0 \"/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV\""
  echo "  $0 \"/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV\" man0g"
}

if [ "${1:-}" = "--help" ] || [ "${1:-}" = "-h" ]; then
  usage
  exit 0
fi

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  usage
  exit 2
fi

ROOT=$1
FOCUS=${2:-man0u}

if [ -d "$ROOT/cut" ] && [ -d "$ROOT/script" ]; then
  CLIENT_DIR=$ROOT
  INSTALL_ROOT=$(dirname "$ROOT")
elif [ -d "$ROOT/client/cut" ]; then
  INSTALL_ROOT=$ROOT
  CLIENT_DIR=$ROOT/client
else
  echo "CLIENT_EVIDENCE_FAIL path: expected client/cut below: $ROOT" >&2
  exit 2
fi

CUT_DIR=$CLIENT_DIR/cut
SCRIPT_DIR=$CLIENT_DIR/script

print_header() {
  echo
  echo "## $1"
}

print_file_state() {
  path=$1
  label=$2
  if [ -f "$path" ]; then
    size=$(wc -c < "$path" | tr -d ' ')
    hash=$(shasum -a 256 "$path" | awk '{print $1}')
    echo "$label: present size=$size sha256=$hash"
  else
    echo "$label: missing"
  fi
}

visible_strings() {
  strings -a "$1" 2>/dev/null | tr '\015\011\013\014' '    ' || true
}

print_matches() {
  label=$1
  file=$2
  pattern=$3
  matches=$(visible_strings "$file" | grep -E "$pattern" | awk 'length($0) <= 140' | sort -u | sed -n '1,40p' || true)
  if [ -n "$matches" ]; then
    echo "$label:"
    echo "$matches" | sed 's/^/  - /'
  fi
}

one_line_matches() {
  file=$1
  pattern=$2
  visible_strings "$file" \
    | grep -Eo "$pattern" \
    | awk 'length($0) <= 100' \
    | sort -u \
    | sed -n '1,12p' \
    | awk 'NR == 1 { out=$0; next } { out=out ", " $0 } END { print out }'
}

echo "# Client Evidence Scan"
echo "Install root: $INSTALL_ROOT"
echo "Client dir: $CLIENT_DIR"
echo "Cut prefix: $FOCUS"

print_file_state "$INSTALL_ROOT/ffxivgame.exe" "ffxivgame.exe"
print_file_state "$INSTALL_ROOT/game.ver" "game.ver"
if [ -f "$INSTALL_ROOT/game.ver" ]; then
  echo "game.ver value: $(tr -d '\r\n' < "$INSTALL_ROOT/game.ver")"
fi

print_header "Cut Bundles"
cut_files=$(find "$CUT_DIR" -maxdepth 2 -type f -name "${FOCUS}*" -print | sort || true)
if [ -z "$cut_files" ]; then
  echo "No cut bundle files matched prefix: $FOCUS"
else
  echo "$cut_files" | sed "s#^$INSTALL_ROOT/##" | sed 's/^/  - /'
fi

print_header "Cut Bundle Summary"
if [ -n "$cut_files" ]; then
  echo "$cut_files" | while IFS= read -r file; do
    rel=$(printf '%s\n' "$file" | sed "s#^$INSTALL_ROOT/##")
    tokens=$(one_line_matches "$file" "Man0[ugl][0-9A-Za-z_]*|man0[ugl][0-9]{3}|Momodi|Miounne|Baderon|Fretful|Ascilia|Velodyna|Adventurers")
    clips=$(one_line_matches "$file" "Rapture(2DMap|Ask|Mes|Caption|QuestInfo|MapCollision|BlackFade|AutoMove|Load|Zone|Warp)[A-Za-z0-9_]*Clip")
    echo "- $rel"
    if [ -n "$tokens" ]; then
      echo "  tokens: $tokens"
    fi
    if [ -n "$clips" ]; then
      echo "  selected clips: $clips"
    fi
  done
fi

print_header "Cut Bundle String Evidence"
if [ -n "$cut_files" ]; then
  echo "$cut_files" | while IFS= read -r file; do
    rel=$(printf '%s\n' "$file" | sed "s#^$INSTALL_ROOT/##")
    echo
    echo "### $rel"
    print_matches "Actors and quest tokens" "$file" "Man0[ugl][0-9A-Za-z_]*|${FOCUS}[0-9A-Za-z_]*|Momodi|Miounne|Baderon|Fretful|Ascilia|Velodyna|Adventurers"
    print_matches "Client clip classes" "$file" "Rapture[A-Za-z0-9_]*Clip"
    print_matches "Client paths" "$file" "lua/[^[:space:]]+|wil_[a-z0-9_]+|fst_[a-z0-9_]+|sea_[a-z0-9_]+|prv_[a-z0-9_]+|man0[ugl][0-9]{3}|q_event\\\\[^[:space:]]+"
    print_matches "UI or flow hints" "$file" "Quest|Journal|Linkpearl|Npc|NPC|Map|Ask|Mes|Caption|Notice|Talk|Event|Load|Zone|Warp|Shop|Item"
  done
fi

print_header "Client Script Containers"
if [ -d "$SCRIPT_DIR" ]; then
  script_files=$(find "$SCRIPT_DIR" -type f | wc -l | tr -d ' ')
  lpb_files=$(find "$SCRIPT_DIR" -type f -name '*.lpb' | wc -l | tr -d ' ')
  echo "script files: $script_files"
  echo "lpb files: $lpb_files"

  direct_hits=$(find "$SCRIPT_DIR" -type f -print0 \
    | xargs -0 grep -aIlE "$FOCUS|Man0u1|Momodi|processEvent|Quest|Linkpearl" 2>/dev/null \
    | sort || true)
  if [ -n "$direct_hits" ]; then
    echo "direct string hits:"
    echo "$direct_hits" | sed "s#^$INSTALL_ROOT/##" | sed 's/^/  - /'
  else
    echo "direct string hits: none"
  fi
else
  echo "script dir: missing"
fi

print_header "Evidence Notes"
echo "- This tool records visible client strings only; it does not decompile packed client bytecode."
echo "- Cut bundle names and clip classes are evidence that the client contains a sequence, not proof that the server should trigger it."
echo "- Promote a behavior only after the same event name, actor, or packet expectation appears in client evidence and live traces."
