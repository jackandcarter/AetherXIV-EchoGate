#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="${CLIENT_DIR:-${1:-}}"
OUTPUT_PATH="${OUTPUT_PATH:-$ROOT_DIR/Data/staticactors.bin}"

if [[ -z "$CLIENT_DIR" ]]; then
  echo "Usage: CLIENT_DIR=/path/to/FINAL_FANTASY_XIV ./tools/prepare-client-data.sh" >&2
  echo "   or: ./tools/prepare-client-data.sh /path/to/FINAL_FANTASY_XIV" >&2
  exit 1
fi

candidates=(
  "$CLIENT_DIR/client/script/rq9q1797qvs.san"
  "$CLIENT_DIR/script/rq9q1797qvs.san"
)

source_path=""
for candidate in "${candidates[@]}"; do
  if [[ -f "$candidate" ]]; then
    source_path="$candidate"
    break
  fi
done

if [[ -z "$source_path" ]]; then
  echo "Could not find rq9q1797qvs.san in expected client paths:" >&2
  for candidate in "${candidates[@]}"; do
    echo "  $candidate" >&2
  done
  exit 1
fi

cp "$source_path" "$OUTPUT_PATH"
echo "Prepared static actor data:"
echo "  source: $source_path"
echo "  output: $OUTPUT_PATH"
echo "Repository policy: client-derived assets remain local and excluded from version control."
