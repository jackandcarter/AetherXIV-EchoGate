# macOS/Linux Development Setup

This project currently targets .NET Framework 4.7.2 through Mono's legacy build tooling and `packages.config`. Modern `dotnet build` is not enough by itself.

## Required Tools

macOS:

```sh
brew install mono nuget mariadb php
```

Linux package names vary by distro. On Debian/Ubuntu-style systems, install equivalents for:

```sh
mono-complete msbuild nuget mariadb-server mariadb-client php-cli php-mysql
```

Ubuntu/Debian contributors can use the bootstrap helper instead of installing each package manually:

```sh
./tools/bootstrap-ubuntu-build.sh --yes
```

The helper checks for existing tools first, installs only missing apt packages, runs the environment audit, builds the legacy Meteor server solution, copies runtime data, runs Echo Gate tests, installs Wine/Winetricks, prepares the Echo Gate Wine prefix, and publishes the Linux launcher to:

```text
build/echo-gate/linux-x64/
```

Linux publish output includes:

```text
build/echo-gate/linux-x64/launch-echo-gate.sh
build/echo-gate/linux-x64/Echo Gate.desktop
build/echo-gate/linux-x64/publish/
```

Run the shell launcher directly, or open the `.desktop` shortcut from your file manager. There is no macOS-style `.app` bundle on Linux.

Useful options:

```sh
./tools/bootstrap-ubuntu-build.sh --no-install
./tools/bootstrap-ubuntu-build.sh --yes --wine-source winehq
./tools/bootstrap-ubuntu-build.sh --yes --no-client-runtime
./tools/bootstrap-ubuntu-build.sh --yes --no-wine
./tools/bootstrap-ubuntu-build.sh --yes --launcher-only
./tools/bootstrap-ubuntu-build.sh --yes --legacy-only
./tools/bootstrap-ubuntu-build.sh --yes --rid linux-arm64
```

Wine and Winetricks are installed by default for local client testing. If `wine` is already present on `PATH`, the helper reuses that detected Wine and does not request `wine`, `wine32`, or `winehq-stable` from apt. `--no-client-runtime` installs Wine packages but skips prefix preparation. `--no-wine` skips Wine/Winetricks package installation and prefix setup for server-only build machines. This does not replace Echo Gate runtime validation, and it does not install a project-owned Wine runtime archive.

Some newer Ubuntu releases list `nuget` in package metadata but do not provide an install candidate. When that happens, the bootstrap helper downloads the official NuGet command-line executable into the ignored local `.tools/` folder and creates a `nuget` wrapper that runs through Mono.

By default the helper also prepares the Echo Gate Wine prefix:

```text
~/.local/share/Demi Dev Unit/Echo Gate/Prefixes/ffxiv-1x
```

It enables 32-bit Wine package support, initializes the prefix, sets Windows 7 mode, installs `d3dx9_41`, and records the current Direct3D compatibility settings. Use `--wine-source winehq` when testing WineHQ Stable instead of Ubuntu's distro Wine packages, or `--client-prefix /path/to/prefix` when Echo Gate should use a custom prefix.

Run the environment audit:

```sh
./tools/audit-env.sh
```

## Restore And Build

```sh
./tools/build-legacy.sh
```

The helper restores NuGet packages, then uses `msbuild` when present or `xbuild` when Homebrew Mono only exposes that older entry point. By default it suppresses known legacy C# warning noise from unused fields and variables so Linux setup output is easier to read. To inspect every compiler warning, run:

```sh
SHOW_LEGACY_WARNINGS=1 ./tools/build-legacy.sh
```

If Linux falls back to `xbuild`, it may still print a toolset warning that `.NET Framework 4.7.2` is not officially supported by that older tool. That warning is expected for the legacy server projects; the important line is whether the build ends with `Build succeeded` or exits with an error. If NuGet creates `packages/`, leave it uncommitted.

## Database Bootstrap

Start MariaDB/MySQL first. The repo includes `.env.defaults`, which is loaded by all local helper scripts. No local env file is required for the default setup.

The checked-in defaults create:

```text
Database: ffxiv_server
App username: meteor
App password: meteor_dev
App hosts: localhost, 127.0.0.1
```

Run:

```sh
./tools/setup-local-db.sh
```

The setup script first tries the current OS user for MariaDB admin access. If that fails on Ubuntu/Debian, it tries sudo/root socket auth. If MariaDB uses password auth instead, it asks for the MariaDB admin username and password in the terminal. You do not need to create or edit any env files for the normal local path.

The same defaults are also exposed to the servers and PHP services:

```sh
DB_NAME=ffxiv_server
DB_APP_USER=meteor
DB_APP_PASS=meteor_dev
METEOR_DB_USER=meteor
METEOR_DB_PASS=meteor_dev
```

If you want a different local database name or app password, create `.env.local` and override the values there. For example:

```sh
DB_APP_PASS=your-local-app-password
METEOR_DB_PASS=your-local-app-password
```

To force a known MariaDB admin account without being prompted, put admin values in `.env.local`:

```sh
DB_ADMIN_USER=root
DB_ADMIN_PASS=your-local-root-password
DB_ADMIN_SUDO=0
```

To drop and recreate the database during local development:

```sh
./tools/setup-local-db.sh --drop
```

The app account is separate from the admin account. `DB_ADMIN_*` is used only for setup/import. `DB_APP_*` and `METEOR_DB_*` are used by Lobby, Map, World, and the PHP launcher/login services. Do not create grants for your Ubuntu login user unless you intentionally want that login to be the database admin.

`tools/load-local-env.sh` is an internal helper that the other scripts source; it is not a standalone setup step. It loads `.env.defaults` first, then `.env.local` if present. Use `tools/setup-local-db.sh` for database setup.

The lower-level `tools/import-db.sh` and `tools/create-db-user.sh` remain available for advanced cases, but the one-command setup path above should be the default for local development. `Data/sql/launcher_services.sql` is imported with the rest of `Data/sql/*.sql`.

## PHP Login/Vercheck Server

The web root lives at `Data/www`.

For local testing:

```sh
./tools/run-web.sh
```

Launcher service base URL:

```text
http://127.0.0.1:8080/launcher
```

Launcher login URL example:

```xml
<Server Name="Localhost" Address="127.0.0.1" LoginUrl="http://127.0.0.1:8080/login/index.php" />
```

The launcher service reads `launcher_config`, `launcher_news`, `launcher_patch_files`, and `launcher_runtime_artifacts` from MariaDB. Configure static hosting URLs in `launcher_config`:

```text
patch_base_url
runtime_catalog_url
login_url
account_create_url
client_login_url
```

Patch files and managed runtime archives are hosted outside Git on a web host, VPS static path, or object storage. Client files remain user-owned local files and are not configured through launcher services.
`login_url` and `account_create_url` point Echo Gate at JSON launcher-service endpoints. `client_login_url` is the legacy client-facing login URL written into `Servers.xml`.

## Runtime Data Copy

After building, copy configs/scripts/static actor data into output folders:

```sh
CONFIGURATION=Release ./tools/copy-runtime-data.sh
```

`Data/staticactors.bin` is required for Map Server runtime. Historical MeteorXIV Core docs describe creating it from the 1.23b client file:

```text
client/script/rq9q1797qvs.san -> Data/staticactors.bin
```

If Echo Gate has already saved your client path, or `CLIENT_DIR` is set in the environment, `tools/copy-runtime-data.sh` and `tools/run-map.sh` try to prepare `Data/staticactors.bin` automatically when it is missing.

To prepare it explicitly:

```sh
CLIENT_DIR="/path/to/FINAL FANTASY XIV" ./tools/prepare-client-data.sh
CONFIGURATION=Release ./tools/copy-runtime-data.sh
```

If the client path is not known, run `./tools/prepare-client-data.sh` and enter the client folder when prompted.

Client-derived files remain local and excluded from version control.

## Server Startup Order

For a full local client path:

1. Start MariaDB/MySQL.
2. Start the PHP web server from `Data/www`.
3. Start Lobby Server.
4. Start Map Server.
5. Start World Server.

From build output folders:

```sh
./tools/run-lobby.sh
./tools/run-map.sh
./tools/run-world.sh
```

World Server connects to map/zone servers at startup. Map Server must be listening before World Server starts.

For pre-client server validation:

```sh
./tools/smoke-local.sh --allow-missing-staticactors
```

For strict validation after `Data/staticactors.bin` is prepared:

```sh
./tools/smoke-local.sh
```

## Apple Silicon Client Notes

The server supports Apple Silicon development. The 1.23b game client is a legacy Windows client. Runtime options:

- Echo Gate defaults to `Automatic Managed` runtime mode on macOS and Linux.
- Automatic mode uses `/launcher/runtime-catalog?platform=<rid>` to install a service-recommended free Wine archive when one is configured.
- Echo Gate creates and reuses a managed prefix at `~/Library/Application Support/Demi Dev Unit/Echo Gate/Prefixes/ffxiv-1x/` on macOS.
- Detected runtime mode can use Homebrew Wine Stable, generic Homebrew Wine, XIV on Mac's bundled Wine, Game Porting Toolkit Wine, CrossOver, or Whisky when available.
- On macOS, Homebrew Wine Stable is checked at `/Applications/Wine Stable.app/Contents/Resources/wine/bin/wine` before generic Homebrew symlinks. It must pass the `win-x86` launch-helper probe before launch is enabled.
- Custom runtime mode allows explicit runtime commands and prefixes for advanced testing.
- Use a Windows x86 environment on another machine or VM/emulator for client testing when Wine compatibility blocks a path.
- Use client packet captures to update `CLIENT_REQUIREMENTS.md`.

The later official macOS FFXIV client is for A Realm Reborn/current FFXIV and is not compatible with this 1.23b server protocol.

## Patch Library Shape

The base client reports:

```text
boot.ver = 2010.07.10.0000
game.ver = 2010.07.10.0000
```

The 1.23b target reports:

```text
boot.ver = 2010.09.18.0000
game.ver = 2012.09.19.0001
```

Echo Gate validates a user-provided patch library by checking for the known one-boot-patch and 51-game-patch sequence. Patch files are checked by path, expected byte size, and CRC32 during apply. The expected local shape is:

```text
ffxiv/2d2a390f/patch/D<version>.patch
ffxiv/2d2a390f/metainfo/D<version>.torrent
ffxiv/48eca647/patch/D<version>.patch
ffxiv/48eca647/metainfo/D<version>.torrent
```

Patch files and metainfo files are local user-provided artifacts and remain excluded from repository state.

Echo Gate can also download individual patch files from a service-configured `patch_base_url`, then apply the same validation and patch chain used by manual libraries. Internet Archive entries, torrent metainfo files, historical S3 URLs, and private mirrors remain outside repository state.

## Echo Gate Builds

Build and test from source:

```sh
AVALONIA_TELEMETRY_OPTOUT=1 dotnet test launcher/EchoGate/EchoGate.sln -m:1 /nr:false
```

Build a local macOS Apple Silicon app bundle:

```sh
./tools/build-echo-gate-macos.sh
```

Build all configured launcher publish targets:

```sh
./tools/build-echo-gate-all.sh
```

Publish targets:

```text
osx-arm64
osx-x64
linux-arm64
linux-x64
win-arm64
win-x64
win-x86
```

Windows builds launch the client natively. macOS and Linux builds expose runtime setup and validation.

For Echo Gate launcher/runtime design, see `docs/LAUNCHER_DESIGN.md` and `docs/WINE_RUNTIME_STRATEGY.md`.
