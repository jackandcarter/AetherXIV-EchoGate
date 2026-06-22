#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
PROJECT_PATH="$ROOT_DIR/launcher/EchoGate/EchoGate.App/EchoGate.App.csproj"
HELPER_PROJECT_PATH="$ROOT_DIR/launcher/EchoGate/EchoGate.ClientLauncher/EchoGate.ClientLauncher.csproj"
RIDS=(
  "win-x86"
  "win-x64"
  "win-arm64"
  "linux-x64"
  "linux-arm64"
  "osx-x64"
  "osx-arm64"
)

for rid in "${RIDS[@]}"; do
  case "$rid" in
    osx-*)
      echo "Building macOS bundle for $rid..."
      RUNTIME_IDENTIFIER="$rid" CONFIGURATION="$CONFIGURATION" "$ROOT_DIR/tools/build-echo-gate-macos.sh"
      ;;
    *)
      output="$ROOT_DIR/build/echo-gate/$rid/publish"
      echo "Publishing Echo Gate for $rid..."
      AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$PROJECT_PATH" \
        --configuration "$CONFIGURATION" \
        --runtime "$rid" \
        --self-contained true \
        --output "$output" \
        /p:PublishSingleFile=false \
        /p:UseAppHost=true
      case "$rid" in
        win-x86)
          helper_rids=("win-x86")
          ;;
        win-x64)
          helper_rids=("win-x64" "win-x86")
          ;;
        win-arm64)
          helper_rids=("win-arm64" "win-x64" "win-x86")
          ;;
        *)
          helper_rids=("win-x64" "win-x86")
          ;;
      esac
      for helper_rid in "${helper_rids[@]}"; do
        helper_output="$output/Helpers/$helper_rid"
        echo "Publishing Echo Gate client launch helper for $helper_rid..."
        AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$HELPER_PROJECT_PATH" \
          --configuration "$CONFIGURATION" \
          --runtime "$helper_rid" \
          --self-contained true \
          --output "$helper_output" \
          /p:PublishSingleFile=false \
          /p:UseAppHost=true
      done
      case "$rid" in
        linux-*)
          "$ROOT_DIR/tools/create-echo-gate-linux-launchers.sh" "$ROOT_DIR/build/echo-gate/$rid" "$output"
          ;;
      esac
      ;;
  esac
done

echo "Echo Gate cross-platform publish complete:"
echo "$ROOT_DIR/build/echo-gate"
