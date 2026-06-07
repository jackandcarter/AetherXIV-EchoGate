#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
RESTORE="${RESTORE:-1}"
BUILD_TOOL="${BUILD_TOOL:-}"

if [[ "$RESTORE" == "1" ]]; then
  if ! command -v nuget >/dev/null 2>&1; then
    echo "nuget is required for package restore" >&2
    exit 1
  fi
  nuget restore "$ROOT_DIR/Meteor.sln"
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

echo "Building Meteor.sln with $BUILD_TOOL ($CONFIGURATION)"
"$BUILD_TOOL" "$ROOT_DIR/Meteor.sln" /p:Configuration="$CONFIGURATION"
