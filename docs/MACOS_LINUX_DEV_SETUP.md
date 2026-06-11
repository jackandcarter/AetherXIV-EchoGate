# macOS/Linux Development Setup

This project currently targets .NET Framework 4.7.2 through Mono's legacy build tooling and `packages.config`. Modern `dotnet build` is not enough by itself.

## Required Tools

macOS:

```sh
brew install mono nuget mariadb php
```

Linux package names vary by distro. On Debian/Ubuntu-style systems, install equivalents for:

```sh
mono-complete msbuild nuget mariadb-server mariadb-client php php-mysqli
```

Run the environment audit:

```sh
./tools/audit-env.sh
```

## Restore And Build

```sh
./tools/build-legacy.sh
```

The helper restores NuGet packages, then uses `msbuild` when present or `xbuild` when Homebrew Mono only exposes that older entry point. If NuGet creates `packages/`, leave it uncommitted.

## Database Bootstrap

Start MariaDB/MySQL first, then import the SQL files:

```sh
DB_NAME=ffxiv_server ./tools/import-db.sh
DB_NAME=ffxiv_server ./tools/create-db-user.sh
mysql ffxiv_server < Data/sql/launcher_services.sql
```

To drop and recreate the database during local development:

```sh
DROP_DATABASE=1 DB_NAME=ffxiv_server ./tools/import-db.sh
```

The import helper defaults to `DB_HOST=localhost` and `DB_USER=$USER`, which matches Homebrew MariaDB socket auth on macOS. Server run scripts read ignored `.env.local` values when present, then pass database settings to the server processes. Override `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASS`, `DB_ADMIN_USER`, `DB_ADMIN_PASS`, `DB_APP_USER`, or `DB_APP_PASS` for Linux, CI, or dedicated database accounts.

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

When a local client install is available:

```sh
CLIENT_DIR="/path/to/FINAL FANTASY XIV" ./tools/prepare-client-data.sh
CONFIGURATION=Release ./tools/copy-runtime-data.sh
```

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
- Detected runtime mode can use Homebrew Wine, XIV on Mac's bundled Wine, Game Porting Toolkit Wine, CrossOver, or Whisky when available.
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
