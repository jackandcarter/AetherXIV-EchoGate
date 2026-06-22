#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
  cat <<'USAGE'
Usage: ./tools/create-echo-gate-linux-launchers.sh BUNDLE_DIR PUBLISH_DIR

Creates portable Linux launcher files for Echo Gate.

The generated install-desktop.sh writes the .desktop file on the target
machine so release artifacts do not contain build-host absolute paths.
USAGE
}

die() {
  printf '[echo-gate-linux] error: %s\n' "$*" >&2
  exit 1
}

if [[ "${1:-}" == "--help" || $# -ne 2 ]]; then
  usage
  [[ "${1:-}" == "--help" ]] && exit 0
  exit 1
fi

bundle_dir="$1"
output="$2"
launcher_script="$bundle_dir/launch-echo-gate.sh"
installer_script="$bundle_dir/install-desktop.sh"
desktop_template="$bundle_dir/Echo Gate.desktop.template"
icon_source="$ROOT_DIR/launcher/EchoGate/Image/icon.png"
icon_dest="$bundle_dir/EchoGate.png"

if [[ ! -d "$bundle_dir" ]]; then
  die "bundle directory does not exist: $bundle_dir"
fi

if [[ ! -x "$output/EchoGate.App" && ! -f "$output/EchoGate.App.dll" ]]; then
  die "Echo Gate publish output is missing EchoGate.App"
fi

cat > "$launcher_script" <<'LAUNCHER'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$SCRIPT_DIR/publish"

if [[ -x "$APP_DIR/EchoGate.App" ]]; then
  exec "$APP_DIR/EchoGate.App" "$@"
fi

exec dotnet "$APP_DIR/EchoGate.App.dll" "$@"
LAUNCHER
chmod +x "$launcher_script"

if [[ -x "$output/EchoGate.App" ]]; then
  chmod +x "$output/EchoGate.App"
fi

if [[ -f "$icon_source" ]]; then
  cp "$icon_source" "$icon_dest"
fi

cat > "$installer_script" <<'INSTALLER'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_NAME="Echo Gate"
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DESKTOP_FILE="$DESKTOP_DIR/echo-gate.desktop"
LAUNCHER="$SCRIPT_DIR/launch-echo-gate.sh"
ICON="$SCRIPT_DIR/EchoGate.png"

desktop_quote() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '"%s"' "$value"
}

if [[ ! -f "$LAUNCHER" ]]; then
  printf '[echo-gate-linux] error: missing launcher: %s\n' "$LAUNCHER" >&2
  exit 1
fi

chmod +x "$LAUNCHER"
if [[ -f "$SCRIPT_DIR/publish/EchoGate.App" ]]; then
  chmod +x "$SCRIPT_DIR/publish/EchoGate.App"
fi

mkdir -p "$DESKTOP_DIR"

{
  printf '[Desktop Entry]\n'
  printf 'Type=Application\n'
  printf 'Name=%s\n' "$APP_NAME"
  printf 'Comment=FFXIV Classic Launcher\n'
  printf 'Exec='
  desktop_quote "$LAUNCHER"
  printf '\n'
  if [[ -f "$ICON" ]]; then
    printf 'Icon=%s\n' "$ICON"
  fi
  printf 'Terminal=false\n'
  printf 'Categories=Game;\n'
} > "$DESKTOP_FILE"

chmod +x "$DESKTOP_FILE"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
fi

printf '[echo-gate-linux] installed desktop launcher: %s\n' "$DESKTOP_FILE"
INSTALLER
chmod +x "$installer_script"

cat > "$desktop_template" <<'TEMPLATE'
[Desktop Entry]
Type=Application
Name=Echo Gate
Comment=FFXIV Classic Launcher
Exec="/absolute/path/to/EchoGate-linux/launch-echo-gate.sh"
Icon=/absolute/path/to/EchoGate-linux/EchoGate.png
Terminal=false
Categories=Game;
TEMPLATE

printf '[echo-gate-linux] launcher script: %s\n' "$launcher_script"
printf '[echo-gate-linux] desktop installer: %s\n' "$installer_script"
printf '[echo-gate-linux] desktop template: %s\n' "$desktop_template"
