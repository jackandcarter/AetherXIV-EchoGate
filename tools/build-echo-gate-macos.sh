#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME_IDENTIFIER="${RUNTIME_IDENTIFIER:-osx-arm64}"
APP_NAME="${APP_NAME:-EchoGate}"
BUNDLE_IDENTIFIER="${BUNDLE_IDENTIFIER:-org.aetherxiv.echogate}"
VERSION="${VERSION:-0.1.0}"
INCLUDE_UMBRA_DEV_FRAMEWORK="${INCLUDE_UMBRA_DEV_FRAMEWORK:-1}"

PROJECT_PATH="$ROOT_DIR/launcher/EchoGate/EchoGate.App/EchoGate.App.csproj"
HELPER_PROJECT_PATH="$ROOT_DIR/launcher/EchoGate/EchoGate.ClientLauncher/EchoGate.ClientLauncher.csproj"
NATIVE_INJECTOR_SOURCE="$ROOT_DIR/launcher/EchoGate/EchoGate.NativeInjector/umbra_native_injector.cpp"
BUILD_ROOT="$ROOT_DIR/build/echo-gate/macos-$RUNTIME_IDENTIFIER"
PUBLISH_DIR="$BUILD_ROOT/publish"
APP_BUNDLE="$BUILD_ROOT/$APP_NAME.app"
CONTENTS_DIR="$APP_BUNDLE/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"
ICON_SOURCE="$ROOT_DIR/launcher/EchoGate/Image/icon.png"
ICON_FILE="$RESOURCES_DIR/EchoGate.icns"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "SMOKE_FAIL echo-gate macos: this bundle script must run on macOS" >&2
  exit 40
fi

echo "Publishing EchoGate for $RUNTIME_IDENTIFIER..."
AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$PROJECT_PATH" \
  --configuration "$CONFIGURATION" \
  --runtime "$RUNTIME_IDENTIFIER" \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  /p:PublishSingleFile=false \
  /p:UseAppHost=true

for helper_rid in win-x64 win-x86; do
  helper_output="$PUBLISH_DIR/Helpers/$helper_rid"
  echo "Publishing EchoGate client launch helper for $helper_rid..."
  AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$HELPER_PROJECT_PATH" \
    --configuration "$CONFIGURATION" \
    --runtime "$helper_rid" \
    --self-contained true \
    --output "$helper_output" \
    /p:PublishSingleFile=false \
    /p:UseAppHost=true
done

if [[ "$INCLUDE_UMBRA_DEV_FRAMEWORK" == "1" ]]; then
  if ! command -v i686-w64-mingw32-g++ >/dev/null 2>&1; then
    echo "SMOKE_FAIL echo-gate macos: i686-w64-mingw32-g++ is required for Umbra native injection" >&2
    exit 41
  fi

  native_injector_output="$BUILD_ROOT/native/Umbra.NativeInjector.x86.exe"
  mkdir -p "$(dirname "$native_injector_output")"
  echo "Building Umbra native x86 injector..."
  i686-w64-mingw32-g++ \
    -std=c++20 \
    -O2 \
    -municode \
    -static \
    "$NATIVE_INJECTOR_SOURCE" \
    -o "$native_injector_output"

  cp "$native_injector_output" "$PUBLISH_DIR/Helpers/win-x64/Umbra.NativeInjector.x86.exe"
  cp "$native_injector_output" "$PUBLISH_DIR/Helpers/win-x86/Umbra.NativeInjector.x86.exe"
fi

if [[ "$INCLUDE_UMBRA_DEV_FRAMEWORK" == "1" ]]; then
  echo "Building bundled Umbra development framework..."
  COPY_TO_APP_BUNDLE=0 "$ROOT_DIR/tools/build-umbra-framework.sh"
fi

rm -rf "$APP_BUNDLE"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -R "$PUBLISH_DIR"/. "$MACOS_DIR"/

if [[ "$INCLUDE_UMBRA_DEV_FRAMEWORK" == "1" ]]; then
  mkdir -p "$MACOS_DIR/Umbra"
  cp -R "$ROOT_DIR/build/umbra/framework/win-x86/Framework" "$MACOS_DIR/Umbra/Framework"
fi

if [[ -f "$ICON_SOURCE" ]] && command -v iconutil >/dev/null 2>&1 && command -v sips >/dev/null 2>&1; then
  ICONSET_DIR="$BUILD_ROOT/EchoGate.iconset"
  ICON_BASE="$BUILD_ROOT/EchoGate-icon-1024.png"
  rm -rf "$ICONSET_DIR"
  mkdir -p "$ICONSET_DIR"
  sips -z 1024 1024 "$ICON_SOURCE" --out "$ICON_BASE" >/dev/null
  sips -z 16 16 "$ICON_BASE" --out "$ICONSET_DIR/icon_16x16.png" >/dev/null
  sips -z 32 32 "$ICON_BASE" --out "$ICONSET_DIR/icon_16x16@2x.png" >/dev/null
  sips -z 32 32 "$ICON_BASE" --out "$ICONSET_DIR/icon_32x32.png" >/dev/null
  sips -z 64 64 "$ICON_BASE" --out "$ICONSET_DIR/icon_32x32@2x.png" >/dev/null
  sips -z 128 128 "$ICON_BASE" --out "$ICONSET_DIR/icon_128x128.png" >/dev/null
  sips -z 256 256 "$ICON_BASE" --out "$ICONSET_DIR/icon_128x128@2x.png" >/dev/null
  sips -z 256 256 "$ICON_BASE" --out "$ICONSET_DIR/icon_256x256.png" >/dev/null
  sips -z 512 512 "$ICON_BASE" --out "$ICONSET_DIR/icon_256x256@2x.png" >/dev/null
  sips -z 512 512 "$ICON_BASE" --out "$ICONSET_DIR/icon_512x512.png" >/dev/null
  sips -z 1024 1024 "$ICON_BASE" --out "$ICONSET_DIR/icon_512x512@2x.png" >/dev/null
  if ! iconutil -c icns "$ICONSET_DIR" -o "$ICON_FILE"; then
    echo "Warning: iconutil rejected the generated iconset; using sips icns fallback." >&2
    if ! sips -s format icns "$ICON_BASE" --out "$ICON_FILE" >/dev/null; then
      echo "Warning: sips rejected the generated icon; continuing without a bundle icns." >&2
      rm -f "$ICON_FILE"
    fi
  fi
  rm -rf "$ICONSET_DIR" "$ICON_BASE"
fi

cat > "$CONTENTS_DIR/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_NAME</string>
  <key>CFBundleExecutable</key>
  <string>EchoGate.App</string>
  <key>CFBundleIdentifier</key>
  <string>$BUNDLE_IDENTIFIER</string>
  <key>CFBundleIconFile</key>
  <string>EchoGate</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$VERSION</string>
  <key>CFBundleVersion</key>
  <string>$VERSION</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF

chmod +x "$MACOS_DIR/EchoGate.App"

if command -v xattr >/dev/null 2>&1; then
  xattr -dr com.apple.quarantine "$APP_BUNDLE" 2>/dev/null || true
fi

if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$APP_BUNDLE"
fi

rm -rf "$PUBLISH_DIR"

echo "EchoGate app bundle ready:"
echo "$APP_BUNDLE"
