#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOCAL_TOOLS_DIR="$ROOT_DIR/.tools/bin"
LOCAL_NUGET_EXE="$ROOT_DIR/.tools/nuget/nuget.exe"
NUGET_EXE_URL="${NUGET_EXE_URL:-https://dist.nuget.org/win-x86-commandline/latest/nuget.exe}"
export PATH="$LOCAL_TOOLS_DIR:$PATH"
CONFIGURATION="${CONFIGURATION:-Release}"
INSTALL_MISSING=1
ASSUME_YES=0
BUILD_LEGACY=1
BUILD_LAUNCHER=1
PUBLISH_LAUNCHER=1
RUN_SMOKE=0
WITH_WINE=1
PREPARE_CLIENT_RUNTIME=1
WINE_SOURCE="${WINE_SOURCE:-distro}"
CLIENT_PREFIX="${WINEPREFIX:-$HOME/.local/share/Demi Dev Unit/Echo Gate/Prefixes/ffxiv-1x}"
LAUNCHER_RID="${RUNTIME_IDENTIFIER:-linux-x64}"

usage() {
  cat <<'USAGE'
Usage: ./tools/bootstrap-ubuntu-build.sh [options]

Checks an Ubuntu/Debian-style machine, installs missing build dependencies,
then builds the Meteor servers and Echo Gate Linux launcher.

Options:
  --yes                 Install packages without prompting.
  --no-install          Check only; do not install missing packages.
  --legacy-only         Build only the legacy Meteor server solution.
  --launcher-only       Build only Echo Gate.
  --no-publish          Run Echo Gate tests but do not publish the launcher.
  --rid RID             Launcher runtime id to publish. Default: linux-x64.
  --configuration NAME  Build configuration. Default: Release.
  --with-wine           Install basic Wine/client-test packages. Default.
  --no-wine             Skip Wine/Winetricks package installation and prefix setup.
  --with-client-runtime Install Wine/Winetricks and prepare the Echo Gate Wine prefix. Default.
  --no-client-runtime   Install Wine packages, but skip Echo Gate Wine prefix setup.
  --wine-source SOURCE  Wine package source: distro or winehq. Default: distro.
  --client-prefix PATH  Wine prefix to prepare. Default: ~/.local/share/Demi Dev Unit/Echo Gate/Prefixes/ffxiv-1x
  --smoke               Run smoke-local after building.
  --help                Show this help.

Examples:
  ./tools/bootstrap-ubuntu-build.sh --yes
  ./tools/bootstrap-ubuntu-build.sh --yes --wine-source winehq
  ./tools/bootstrap-ubuntu-build.sh --yes --no-wine
  ./tools/bootstrap-ubuntu-build.sh --no-install --launcher-only
USAGE
}

log() {
  printf '[bootstrap] %s\n' "$*"
}

warn() {
  printf '[bootstrap] warning: %s\n' "$*" >&2
}

die() {
  printf '[bootstrap] error: %s\n' "$*" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --yes)
      ASSUME_YES=1
      ;;
    --no-install)
      INSTALL_MISSING=0
      ;;
    --legacy-only)
      BUILD_LEGACY=1
      BUILD_LAUNCHER=0
      ;;
    --launcher-only)
      BUILD_LEGACY=0
      BUILD_LAUNCHER=1
      ;;
    --no-publish)
      PUBLISH_LAUNCHER=0
      ;;
    --rid)
      [[ $# -ge 2 ]] || die "--rid requires a value"
      LAUNCHER_RID="$2"
      shift
      ;;
    --configuration)
      [[ $# -ge 2 ]] || die "--configuration requires a value"
      CONFIGURATION="$2"
      shift
      ;;
    --with-wine)
      WITH_WINE=1
      ;;
    --no-wine)
      WITH_WINE=0
      PREPARE_CLIENT_RUNTIME=0
      ;;
    --with-client-runtime)
      WITH_WINE=1
      PREPARE_CLIENT_RUNTIME=1
      ;;
    --no-client-runtime)
      PREPARE_CLIENT_RUNTIME=0
      ;;
    --wine-source)
      [[ $# -ge 2 ]] || die "--wine-source requires a value"
      WINE_SOURCE="$2"
      shift
      ;;
    --client-prefix)
      [[ $# -ge 2 ]] || die "--client-prefix requires a value"
      CLIENT_PREFIX="$2"
      shift
      ;;
    --smoke)
      RUN_SMOKE=1
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      die "unknown option: $1"
      ;;
  esac
  shift
done

if [[ "$BUILD_LEGACY" -eq 0 && "$BUILD_LAUNCHER" -eq 0 ]]; then
  die "nothing to build"
fi

case "$WINE_SOURCE" in
  distro|winehq)
    ;;
  *)
    die "--wine-source must be distro or winehq"
    ;;
esac

if ! command -v apt-get >/dev/null 2>&1; then
  if [[ "$INSTALL_MISSING" -eq 1 ]]; then
    die "automatic dependency install currently supports apt-get based Ubuntu/Debian systems"
  fi
  warn "apt-get not found; dependency install is disabled"
fi

OS_ID="unknown"
OS_VERSION_ID="unknown"
if [[ -r /etc/os-release ]]; then
  # shellcheck disable=SC1091
  source /etc/os-release
  OS_ID="${ID:-unknown}"
  OS_VERSION_ID="${VERSION_ID:-unknown}"
fi

declare -a PACKAGES=()
declare -a PACKAGE_WARNINGS=()
APT_UPDATED=0

package_requested() {
  local candidate="$1"
  local package
  for package in "${PACKAGES[@]}"; do
    [[ "$package" == "$candidate" ]] && return 0
  done
  return 1
}

add_package() {
  local package="$1"
  package_requested "$package" || PACKAGES+=("$package")
}

apt_update_once() {
  [[ "$INSTALL_MISSING" -eq 1 ]] || return 0
  [[ "$APT_UPDATED" -eq 0 ]] || return 0
  log "Updating apt package metadata..."
  sudo apt-get update
  APT_UPDATED=1
}

apt_package_installable() {
  local package="$1"
  local candidate
  command -v apt-cache >/dev/null 2>&1 || return 1
  candidate="$(apt-cache policy "$package" 2>/dev/null | awk '/Candidate:/ {print $2; exit}')"
  [[ -n "$candidate" && "$candidate" != "(none)" ]]
}

add_available_package() {
  local package="$1"
  if apt_package_installable "$package"; then
    add_package "$package"
  else
    PACKAGE_WARNINGS+=("$package has no install candidate from the current apt sources")
  fi
}

command_exists() {
  command -v "$1" >/dev/null 2>&1
}

package_installed() {
  dpkg-query -W -f='${Status}' "$1" 2>/dev/null | grep -q "install ok installed"
}

need_command() {
  local command_name="$1"
  local package_name="$2"

  if command_exists "$command_name"; then
    log "ok: $command_name ($(command -v "$command_name"))"
  else
    log "missing: $command_name -> $package_name"
    add_available_package "$package_name"
  fi
}

need_package() {
  local package_name="$1"

  if package_installed "$package_name"; then
    log "ok: package $package_name"
  else
    log "missing: package $package_name"
    add_available_package "$package_name"
  fi
}

dotnet_has_10_sdk() {
  command_exists dotnet && dotnet --list-sdks 2>/dev/null | grep -Eq '^10\.'
}

prepare_dotnet_package_source() {
  [[ "$INSTALL_MISSING" -eq 1 ]] || return 0
  apt_update_once
  apt_package_installable dotnet-sdk-10.0 && return 0

  if [[ "$OS_ID" == "ubuntu" ]]; then
    warn "dotnet-sdk-10.0 is not visible; adding Ubuntu .NET backports PPA"
    if [[ "$ASSUME_YES" -ne 1 ]]; then
      read -r -p "Add Ubuntu .NET backports PPA with sudo? [y/N] " reply
      case "$reply" in
        y|Y|yes|YES)
          ;;
        *)
          warn "dotnet-sdk-10.0 package source was not added"
          return 0
          ;;
      esac
    fi
    sudo apt-get install -y software-properties-common
    sudo add-apt-repository -y ppa:dotnet/backports
    APT_UPDATED=0
    apt_update_once
  fi
}

enable_i386_architecture() {
  [[ "$INSTALL_MISSING" -eq 1 ]] || return 0
  if dpkg --print-foreign-architectures | grep -qx i386; then
    log "ok: i386 architecture enabled"
    return 0
  fi

  log "Enabling i386 packages for 32-bit Wine support..."
  sudo dpkg --add-architecture i386
  APT_UPDATED=0
  apt_update_once
}

ubuntu_codename() {
  if [[ -n "${UBUNTU_CODENAME:-}" ]]; then
    printf '%s\n' "$UBUNTU_CODENAME"
  elif [[ -n "${VERSION_CODENAME:-}" ]]; then
    printf '%s\n' "$VERSION_CODENAME"
  elif command_exists lsb_release; then
    lsb_release -cs
  else
    printf '%s\n' ""
  fi
}

prepare_winehq_package_source() {
  [[ "$INSTALL_MISSING" -eq 1 ]] || return 0

  if [[ "$OS_ID" != "ubuntu" && "$OS_ID" != "linuxmint" ]]; then
    warn "WineHQ repository setup is only automated for Ubuntu-compatible apt sources; falling back to distro Wine packages"
    WINE_SOURCE="distro"
    return 0
  fi

  local codename
  codename="$(ubuntu_codename)"
  if [[ -z "$codename" ]]; then
    warn "Could not determine Ubuntu codename; falling back to distro Wine packages"
    WINE_SOURCE="distro"
    return 0
  fi

  local source_file="/etc/apt/sources.list.d/winehq-${codename}.sources"
  local key_file="/etc/apt/keyrings/winehq-archive.key"

  apt_update_once
  sudo apt-get install -y ca-certificates wget gnupg2 software-properties-common
  sudo mkdir -pm755 /etc/apt/keyrings

  if [[ ! -f "$key_file" ]]; then
    log "Installing WineHQ package key..."
    sudo wget -O "$key_file" https://dl.winehq.org/wine-builds/winehq.key
  else
    log "ok: WineHQ package key exists"
  fi

  if [[ ! -f "$source_file" ]]; then
    log "Installing WineHQ apt source for $codename..."
    sudo wget -NP /etc/apt/sources.list.d/ "https://dl.winehq.org/wine-builds/ubuntu/dists/${codename}/winehq-${codename}.sources"
  else
    log "ok: WineHQ apt source exists"
  fi

  APT_UPDATED=0
  apt_update_once
}

collect_dependencies() {
  log "Dependency audit for $OS_ID $OS_VERSION_ID"

  if [[ "$INSTALL_MISSING" -eq 1 ]]; then
    apt_update_once
  fi

  need_command git git
  need_command curl curl
  need_package ca-certificates
  need_package build-essential
  need_package pkg-config
  need_package unzip
  need_package zip

  if [[ "$BUILD_LEGACY" -eq 1 ]]; then
    need_command mono mono-complete
    if command_exists msbuild || command_exists xbuild; then
      command_exists msbuild && log "ok: msbuild ($(command -v msbuild))"
      command_exists xbuild && log "ok: xbuild ($(command -v xbuild))"
    else
      log "missing: msbuild/xbuild"
      add_available_package msbuild
      add_available_package mono-xbuild
    fi
    need_command nuget nuget
    need_package libgdiplus
    need_command mysql mariadb-client
    if command_exists mariadbd || command_exists mysqld; then
      command_exists mariadbd && log "ok: mariadbd ($(command -v mariadbd))"
      command_exists mysqld && log "ok: mysqld ($(command -v mysqld))"
    else
      log "missing: mariadb server"
      add_available_package mariadb-server
    fi
    need_command php php-cli
    if command_exists php && php -m 2>/dev/null | grep -qi '^mysqli$'; then
      log "ok: php mysqli extension"
    else
      log "missing: php mysqli extension -> php-mysql"
      add_available_package php-mysql
    fi
  fi

  if [[ "$BUILD_LAUNCHER" -eq 1 ]]; then
    if dotnet_has_10_sdk; then
      log "ok: .NET 10 SDK"
    else
      log "missing: .NET 10 SDK -> dotnet-sdk-10.0"
      prepare_dotnet_package_source
      add_available_package dotnet-sdk-10.0
    fi
  fi

  if [[ "$WITH_WINE" -eq 1 ]]; then
    enable_i386_architecture
    if command_exists wine; then
      log "ok: wine ($(command -v wine))"
      log "Keeping detected Wine; no Wine package will be requested."
    elif [[ "$WINE_SOURCE" == "winehq" ]]; then
      prepare_winehq_package_source
      log "missing: wine -> winehq-stable"
      add_available_package winehq-stable
    else
      log "missing: wine -> wine"
      add_available_package wine
      add_available_package wine32
    fi
    need_command winetricks winetricks
    need_package libgl1:i386
    need_package libglx-mesa0:i386
    need_package libgl1-mesa-dri:i386
    need_package libglu1-mesa:i386
    need_package libvulkan1:i386
    need_package mesa-vulkan-drivers:i386
  fi
}

install_packages() {
  if [[ "${#PACKAGE_WARNINGS[@]}" -gt 0 ]]; then
    local warning
    for warning in "${PACKAGE_WARNINGS[@]}"; do
      warn "$warning"
    done
  fi

  if [[ "${#PACKAGES[@]}" -eq 0 ]]; then
    log "No missing apt packages detected."
    return 0
  fi

  if [[ "$INSTALL_MISSING" -eq 0 ]]; then
    warn "Missing packages detected but --no-install was used:"
    printf '  %s\n' "${PACKAGES[@]}" >&2
    return 0
  fi

  log "Packages to install: ${PACKAGES[*]}"
  if [[ "$ASSUME_YES" -ne 1 ]]; then
    read -r -p "Install missing packages with sudo apt-get? [y/N] " reply
    case "$reply" in
      y|Y|yes|YES)
        ;;
      *)
        die "package install declined"
        ;;
    esac
  fi

  sudo apt-get install -y "${PACKAGES[@]}"
}

ensure_nuget_cli() {
  [[ "$BUILD_LEGACY" -eq 1 ]] || return 0
  command_exists nuget && return 0

  if [[ "$INSTALL_MISSING" -eq 0 ]]; then
    die "nuget is missing; rerun without --no-install or install NuGet manually"
  fi

  command_exists mono || die "mono is required for the local NuGet fallback"
  command_exists curl || die "curl is required for the local NuGet fallback"

  mkdir -p "$(dirname "$LOCAL_NUGET_EXE")" "$LOCAL_TOOLS_DIR"

  if [[ ! -f "$LOCAL_NUGET_EXE" ]]; then
    log "Downloading NuGet CLI fallback..."
    curl -fsSL "$NUGET_EXE_URL" -o "$LOCAL_NUGET_EXE"
  else
    log "ok: local NuGet CLI fallback exists"
  fi

  cat > "$LOCAL_TOOLS_DIR/nuget" <<EOF
#!/usr/bin/env bash
exec mono "$LOCAL_NUGET_EXE" "\$@"
EOF
  chmod +x "$LOCAL_TOOLS_DIR/nuget"

  command_exists nuget || die "failed to create local NuGet wrapper"
  log "ok: nuget ($(command -v nuget))"
}

validate_build_dependencies() {
  command_exists git || die "git is still missing"

  if [[ "$BUILD_LEGACY" -eq 1 ]]; then
    command_exists mono || die "mono is still missing"
    if ! command_exists msbuild && ! command_exists xbuild; then
      die "msbuild or xbuild is still missing"
    fi
    command_exists nuget || die "nuget is still missing"
  fi

  if [[ "$BUILD_LAUNCHER" -eq 1 ]]; then
    dotnet_has_10_sdk || die ".NET 10 SDK is still missing; install dotnet-sdk-10.0 and rerun"
  fi

  if [[ "$WITH_WINE" -eq 1 ]]; then
    command_exists wine || die "wine is still missing"
    command_exists winetricks || die "winetricks is still missing"
  fi
}

build_legacy() {
  log "Building Meteor legacy servers..."
  CONFIGURATION="$CONFIGURATION" "$ROOT_DIR/tools/build-legacy.sh"
  log "Copying server runtime data..."
  CONFIGURATION="$CONFIGURATION" "$ROOT_DIR/tools/copy-runtime-data.sh"
}

publish_echo_gate_linux() {
  local app_project="$ROOT_DIR/launcher/EchoGate/EchoGate.App/EchoGate.App.csproj"
  local helper_project="$ROOT_DIR/launcher/EchoGate/EchoGate.ClientLauncher/EchoGate.ClientLauncher.csproj"
  local bundle_dir="$ROOT_DIR/build/echo-gate/$LAUNCHER_RID"
  local output="$bundle_dir/publish"
  local helper_rid

  log "Publishing Echo Gate for $LAUNCHER_RID..."
  AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$app_project" \
    --configuration "$CONFIGURATION" \
    --runtime "$LAUNCHER_RID" \
    --self-contained true \
    --output "$output" \
    /p:PublishSingleFile=false \
    /p:UseAppHost=true

  for helper_rid in win-x64 win-x86; do
    log "Publishing Echo Gate client launch helper for $helper_rid..."
    AVALONIA_TELEMETRY_OPTOUT=1 dotnet publish "$helper_project" \
      --configuration "$CONFIGURATION" \
      --runtime "$helper_rid" \
      --self-contained true \
      --output "$output/Helpers/$helper_rid" \
      /p:PublishSingleFile=false \
      /p:UseAppHost=true
  done

  create_echo_gate_linux_launchers "$bundle_dir" "$output"
  log "Echo Gate publish output: $output"
}

create_echo_gate_linux_launchers() {
  local bundle_dir="$1"
  local output="$2"
  local launcher_script="$bundle_dir/launch-echo-gate.sh"
  local desktop_file="$bundle_dir/Echo Gate.desktop"
  local icon_source="$ROOT_DIR/launcher/EchoGate/Image/icon.png"
  local icon_dest="$bundle_dir/EchoGate.png"

  if [[ ! -x "$output/EchoGate.App" && ! -f "$output/EchoGate.App.dll" ]]; then
    die "Echo Gate publish output is missing EchoGate.App"
  fi

  cat > "$launcher_script" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$SCRIPT_DIR/publish"

if [[ -x "$APP_DIR/EchoGate.App" ]]; then
  exec "$APP_DIR/EchoGate.App" "$@"
fi

exec dotnet "$APP_DIR/EchoGate.App.dll" "$@"
EOF
  chmod +x "$launcher_script"

  if [[ -f "$icon_source" ]]; then
    cp "$icon_source" "$icon_dest"
  fi

  {
    printf '[Desktop Entry]\n'
    printf 'Type=Application\n'
    printf 'Name=Echo Gate\n'
    printf 'Comment=FFXIV Classic Launcher\n'
    printf 'Exec=%s\n' "$launcher_script"
    if [[ -f "$icon_dest" ]]; then
      printf 'Icon=%s\n' "$icon_dest"
    fi
    printf 'Terminal=false\n'
    printf 'Categories=Game;\n'
  } > "$desktop_file"
  chmod +x "$desktop_file"

  log "Echo Gate launcher script: $launcher_script"
  log "Echo Gate desktop shortcut: $desktop_file"
}

build_launcher() {
  log "Testing Echo Gate solution..."
  AVALONIA_TELEMETRY_OPTOUT=1 dotnet test "$ROOT_DIR/launcher/EchoGate/EchoGate.sln" -m:1 /nr:false

  if [[ "$PUBLISH_LAUNCHER" -eq 1 ]]; then
    publish_echo_gate_linux
  fi
}

prepare_client_runtime() {
  log "Preparing Echo Gate Wine prefix: $CLIENT_PREFIX"
  mkdir -p "$(dirname "$CLIENT_PREFIX")"

  log "Initializing Wine prefix..."
  WINEPREFIX="$CLIENT_PREFIX" WINE_D3D_CONFIG="renderer=gl,csmt=0" wineboot -u

  log "Installing Windows 7 mode and d3dx9_41 into the prefix..."
  WINEPREFIX="$CLIENT_PREFIX" WINE_D3D_CONFIG="renderer=gl,csmt=0" winetricks -q win7 d3dx9_41

  log "Recording WineD3D Direct3D settings in the prefix..."
  WINEPREFIX="$CLIENT_PREFIX" wine reg add 'HKCU\Software\Wine\Direct3D' /v renderer /t REG_SZ /d gl /f >/dev/null
  WINEPREFIX="$CLIENT_PREFIX" wine reg add 'HKCU\Software\Wine\Direct3D' /v csmt /t REG_SZ /d 0 /f >/dev/null

  if command_exists wineserver; then
    WINEPREFIX="$CLIENT_PREFIX" wineserver -w
  fi

  log "Client runtime prefix is ready: $CLIENT_PREFIX"
}

collect_dependencies
install_packages
ensure_nuget_cli
validate_build_dependencies

log "Running environment audit..."
"$ROOT_DIR/tools/audit-env.sh"

if [[ "$BUILD_LEGACY" -eq 1 ]]; then
  build_legacy
fi

if [[ "$BUILD_LAUNCHER" -eq 1 ]]; then
  build_launcher
fi

if [[ "$PREPARE_CLIENT_RUNTIME" -eq 1 ]]; then
  prepare_client_runtime
fi

if [[ "$RUN_SMOKE" -eq 1 ]]; then
  log "Running smoke validation..."
  "$ROOT_DIR/tools/smoke-local.sh" --allow-missing-staticactors
fi

log "Bootstrap build complete."
