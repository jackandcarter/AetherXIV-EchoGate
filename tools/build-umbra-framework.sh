#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
UMBRA_VERSION="${UMBRA_VERSION:-0.1.0-dev}"
UMBRA_API_VERSION="${UMBRA_API_VERSION:-1.0}"
PLATFORM_RID="${PLATFORM_RID:-win-x86}"
ARTIFACT_BASE_URL="${ARTIFACT_BASE_URL:-http://127.0.0.1:8080/launcher/artifacts/umbra}"
COPY_TO_WEB="${COPY_TO_WEB:-1}"
COPY_TO_APP_BUNDLE="${COPY_TO_APP_BUNDLE:-1}"
APP_BUNDLE="${APP_BUNDLE:-$ROOT_DIR/build/echo-gate/macos-osx-arm64/EchoGate.app}"

BOOTSTRAP_PROJECT_DIR="$ROOT_DIR/launcher/Umbra/Aether.Umbra.Bootstrap"
IMGUI_DIR="$ROOT_DIR/launcher/Umbra/vendor/imgui"
FRAMEWORK_PROJECT="$ROOT_DIR/launcher/Umbra/Aether.Umbra.Framework/Aether.Umbra.Framework.csproj"
BUILD_ROOT="$ROOT_DIR/build/umbra/framework/$PLATFORM_RID"
BUNDLE_DIR="$BUILD_ROOT/Framework"
MANAGED_DIR="$BUNDLE_DIR/Managed"
ARTIFACT_DIR="$ROOT_DIR/build/umbra/artifacts"
WEB_ARTIFACT_DIR="$ROOT_DIR/Data/www/launcher/artifacts/umbra"
ARCHIVE_NAME="aether-umbra-$UMBRA_VERSION-$PLATFORM_RID.zip"
ARCHIVE_PATH="$ARTIFACT_DIR/$ARCHIVE_NAME"
CATALOG_PATH="$ARTIFACT_DIR/aether-umbra-$UMBRA_VERSION-$PLATFORM_RID.catalog.json"
SQL_PATH="$ARTIFACT_DIR/aether-umbra-$UMBRA_VERSION-$PLATFORM_RID.sql"

if [[ "$PLATFORM_RID" != "win-x86" ]]; then
  echo "Umbra bootstrap currently supports win-x86 only." >&2
  exit 20
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is required." >&2
  exit 21
fi

if ! command -v i686-w64-mingw32-g++ >/dev/null 2>&1; then
  echo "i686-w64-mingw32-g++ is required to build the x86 Umbra bootstrap DLL." >&2
  exit 22
fi

if ! command -v zip >/dev/null 2>&1; then
  echo "zip is required to package the Umbra framework artifact." >&2
  exit 23
fi

rm -rf "$BUNDLE_DIR"
mkdir -p "$MANAGED_DIR" "$ARTIFACT_DIR"

echo "Publishing Umbra managed framework for $PLATFORM_RID..."
dotnet publish "$FRAMEWORK_PROJECT" \
  --configuration "$CONFIGURATION" \
  --runtime "$PLATFORM_RID" \
  --self-contained true \
  --output "$MANAGED_DIR" \
  /p:PublishSingleFile=false \
  /p:UseAppHost=true

echo "Building Umbra native bootstrap for $PLATFORM_RID..."
i686-w64-mingw32-g++ \
  -std=c++20 \
  -O2 \
  -fno-builtin \
  -fno-tree-loop-distribute-patterns \
  -fno-exceptions \
  -fno-rtti \
  -DIMGUI_IMPL_WIN32_DISABLE_GAMEPAD \
  -I"$IMGUI_DIR" \
  -I"$IMGUI_DIR/backends" \
  -shared \
  -static \
  -static-libgcc \
  -static-libstdc++ \
  -Wl,--kill-at \
  -o "$BUNDLE_DIR/Aether.Umbra.Bootstrap.x86.dll" \
  "$BOOTSTRAP_PROJECT_DIR/dllmain.cpp" \
  "$IMGUI_DIR/imgui.cpp" \
  "$IMGUI_DIR/imgui_draw.cpp" \
  "$IMGUI_DIR/imgui_tables.cpp" \
  "$IMGUI_DIR/imgui_widgets.cpp" \
  "$IMGUI_DIR/backends/imgui_impl_dx9.cpp" \
  "$IMGUI_DIR/backends/imgui_impl_win32.cpp" \
  -lgdi32 \
  -ldwmapi

printf '%s\n' "$UMBRA_VERSION" > "$BUNDLE_DIR/version.txt"

rm -f "$ARCHIVE_PATH"
(
  cd "$BUNDLE_DIR"
  zip -qr "$ARCHIVE_PATH" .
)

SIZE_BYTES="$(wc -c < "$ARCHIVE_PATH" | tr -d '[:space:]')"
SHA256="$(shasum -a 256 "$ARCHIVE_PATH" | awk '{print $1}')"
ARCHIVE_URL="$ARTIFACT_BASE_URL/$ARCHIVE_NAME"
SUPPORTED_GAME_SHA256="9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9"

cat > "$CATALOG_PATH" <<EOF
{
  "platform": "$PLATFORM_RID",
  "artifacts": [
    {
      "name": "Aether Umbra Dev",
      "version": "$UMBRA_VERSION",
      "api_version": "$UMBRA_API_VERSION",
      "platform_rid": "$PLATFORM_RID",
      "archive_url": "$ARCHIVE_URL",
      "archive_format": "zip",
      "size_bytes": $SIZE_BYTES,
      "sha256": "$SHA256",
      "bootstrap_relative_path": "Aether.Umbra.Bootstrap.x86.dll",
      "framework_relative_path": "Managed/Aether.Umbra.Framework.dll",
      "supported_game_sha256": ["$SUPPORTED_GAME_SHA256"],
      "is_default": true,
      "is_active": true,
      "sort_order": 0
    }
  ]
}
EOF

cat > "$SQL_PATH" <<EOF
CREATE TABLE IF NOT EXISTS launcher_umbra_framework_artifacts (
  id int(11) unsigned NOT NULL AUTO_INCREMENT,
  name varchar(120) NOT NULL,
  version varchar(64) NOT NULL,
  api_version varchar(32) NOT NULL DEFAULT '1.0',
  platform_rid varchar(32) NOT NULL DEFAULT 'win-x86',
  archive_url varchar(500) NOT NULL,
  archive_format varchar(16) NOT NULL DEFAULT 'zip',
  size_bytes bigint(20) NOT NULL,
  sha256 char(64) NOT NULL,
  bootstrap_relative_path varchar(255) NOT NULL DEFAULT 'Aether.Umbra.Bootstrap.x86.dll',
  framework_relative_path varchar(255) NOT NULL DEFAULT 'Managed/Aether.Umbra.Framework.dll',
  supported_game_sha256 text NULL,
  is_default tinyint(1) NOT NULL DEFAULT 0,
  is_active tinyint(1) NOT NULL DEFAULT 1,
  sort_order int(11) NOT NULL DEFAULT 0,
  created_at timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_launcher_umbra_framework_platform (platform_rid, is_active, is_default, sort_order)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DELETE FROM launcher_umbra_framework_artifacts
WHERE name = 'Aether Umbra Dev'
  AND version = '$UMBRA_VERSION'
  AND platform_rid = '$PLATFORM_RID';

INSERT INTO launcher_umbra_framework_artifacts
  (name, version, api_version, platform_rid, archive_url, archive_format,
   size_bytes, sha256, bootstrap_relative_path, framework_relative_path,
   supported_game_sha256, is_default, is_active, sort_order)
VALUES
  ('Aether Umbra Dev', '$UMBRA_VERSION', '$UMBRA_API_VERSION', '$PLATFORM_RID',
   '$ARCHIVE_URL', 'zip',
   $SIZE_BYTES, '$SHA256', 'Aether.Umbra.Bootstrap.x86.dll',
   'Managed/Aether.Umbra.Framework.dll',
   '$SUPPORTED_GAME_SHA256', 1, 1, 0);
EOF

if [[ "$COPY_TO_WEB" == "1" ]]; then
  mkdir -p "$WEB_ARTIFACT_DIR"
  cp "$ARCHIVE_PATH" "$WEB_ARTIFACT_DIR/$ARCHIVE_NAME"
fi

if [[ "$COPY_TO_APP_BUNDLE" == "1" && -d "$APP_BUNDLE/Contents/MacOS" ]]; then
  rm -rf "$APP_BUNDLE/Contents/MacOS/Umbra/Framework"
  mkdir -p "$APP_BUNDLE/Contents/MacOS/Umbra"
  cp -R "$BUNDLE_DIR" "$APP_BUNDLE/Contents/MacOS/Umbra/Framework"
fi

echo "Umbra framework bundle ready: $BUNDLE_DIR"
echo "Umbra framework artifact: $ARCHIVE_PATH"
echo "Umbra framework catalog JSON: $CATALOG_PATH"
echo "Umbra framework SQL seed: $SQL_PATH"
echo "Archive URL: $ARCHIVE_URL"
echo "Size: $SIZE_BYTES"
echo "SHA256: $SHA256"
