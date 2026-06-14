#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
RESTORE="${RESTORE:-1}"
BUILD_TOOL="${BUILD_TOOL:-}"
BUILD_VERBOSITY="${BUILD_VERBOSITY:-minimal}"
SHOW_LEGACY_WARNINGS="${SHOW_LEGACY_WARNINGS:-0}"
LEGACY_NOWARN="${LEGACY_NOWARN:-0108,0162,0168,0169,0219,0414,0649,0659,0675}"

if [[ "$RESTORE" == "1" ]]; then
  if ! command -v nuget >/dev/null 2>&1; then
    echo "nuget is required for package restore" >&2
    exit 1
  fi
  nuget restore "$ROOT_DIR/MeteorXIV.Core.sln"
fi

if [[ -z "$BUILD_TOOL" ]]; then
  if command -v msbuild >/dev/null 2>&1; then
    BUILD_TOOL="msbuild"
  elif command -v xbuild >/dev/null 2>&1; then
    BUILD_TOOL="xbuild"
  else
    echo "Neither msbuild nor xbuild was found" >&2
    exit 1
  fi
fi

build_args=(
  "$ROOT_DIR/MeteorXIV.Core.sln"
  /p:Configuration="$CONFIGURATION"
  /verbosity:"$BUILD_VERBOSITY"
)

if [[ "$SHOW_LEGACY_WARNINGS" != "1" ]]; then
  echo "Suppressing known legacy C# warning noise: $LEGACY_NOWARN"
  echo "Set SHOW_LEGACY_WARNINGS=1 to show all compiler warnings."
  build_args+=("/p:NoWarn=$LEGACY_NOWARN")
fi

echo "Building MeteorXIV.Core.sln with $BUILD_TOOL ($CONFIGURATION)"
"$BUILD_TOOL" "${build_args[@]}"
