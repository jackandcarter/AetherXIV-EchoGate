#!/usr/bin/env sh
set -eu

usage() {
  echo "Usage: $0 /path/to/user-owned/FINAL-FANTASY-XIV-client"
  echo
  echo "Prints a local-only client layout summary. It does not copy client files."
}

if [ "${1:-}" = "--help" ] || [ "${1:-}" = "-h" ]; then
  usage
  exit 0
fi

if [ "$#" -ne 1 ]; then
  usage
  exit 2
fi

CLIENT_ROOT=$1
if [ ! -d "$CLIENT_ROOT" ]; then
  echo "CLIENT_INSPECT_FAIL path: not a directory: $CLIENT_ROOT" >&2
  exit 2
fi

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

echo "Client root: $CLIENT_ROOT"
print_file_state "$CLIENT_ROOT/ffxivboot.exe" "ffxivboot.exe"
print_file_state "$CLIENT_ROOT/ffxivgame.exe" "ffxivgame.exe"
print_file_state "$CLIENT_ROOT/boot.ver" "boot.ver"
print_file_state "$CLIENT_ROOT/game.ver" "game.ver"

if [ -f "$CLIENT_ROOT/boot.ver" ]; then
  echo "boot.ver value: $(tr -d '\r\n' < "$CLIENT_ROOT/boot.ver")"
fi

if [ -f "$CLIENT_ROOT/game.ver" ]; then
  echo "game.ver value: $(tr -d '\r\n' < "$CLIENT_ROOT/game.ver")"
fi

for dir in "$CLIENT_ROOT" "$CLIENT_ROOT/client" "$CLIENT_ROOT/sqpack" "$CLIENT_ROOT/game"; do
  if [ -d "$dir" ]; then
    indexes=$(find "$dir" -name '*.win32.index' -type f 2>/dev/null | wc -l | tr -d ' ')
    dats=$(find "$dir" -name '*.win32.dat*' -type f 2>/dev/null | wc -l | tr -d ' ')
    exes=$(find "$dir" -name '*.exe' -type f 2>/dev/null | wc -l | tr -d ' ')
    echo "Layout: $dir indexes=$indexes dats=$dats executables=$exes"
  fi
done

if [ -f "$CLIENT_ROOT/game.ver" ] && [ "$(tr -d '\r\n' < "$CLIENT_ROOT/game.ver")" = "2012.09.19.0001" ]; then
  echo "CLIENT_INSPECT_OK version: target game.ver detected"
else
  echo "CLIENT_INSPECT_WARN version: target game.ver 2012.09.19.0001 not detected"
fi
