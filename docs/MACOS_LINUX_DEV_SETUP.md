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
```

To drop and recreate the database during local development:

```sh
DROP_DATABASE=1 DB_NAME=ffxiv_server ./tools/import-db.sh
```

The import helper defaults to `DB_HOST=localhost` and `DB_USER=$USER`, which matches Homebrew MariaDB socket auth on macOS. The default server configs use `meteor` / `meteor_dev` against `ffxiv_server`; `create-db-user.sh` creates that local dev account. Override `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASS`, `DB_ADMIN_USER`, `DB_ADMIN_PASS`, `DB_APP_USER`, or `DB_APP_PASS` for Linux, CI, or dedicated database accounts. Checked-in dev credentials are local-lab only.

## PHP Login/Vercheck Server

The web root lives at `Data/www`.

For local testing:

```sh
./tools/run-web.sh
```

Launcher login URL example:

```xml
<Server Name="Localhost" Address="127.0.0.1" LoginUrl="http://127.0.0.1:8080/login_su/login.php" />
```

## Runtime Data Copy

After building, copy configs/scripts/static actor data into output folders:

```sh
CONFIGURATION=Release ./tools/copy-runtime-data.sh
```

`Data/staticactors.bin` is required for Map Server runtime. Historical Project Meteor docs describe creating it from the 1.23b client file:

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

- Try CrossOver, Wineskin, or another maintained Wine wrapper for the Windows client and Seventh Umbral launcher.
- Echo Gate can detect available runtime tools such as XIV on Mac's bundled Wine, Game Porting Toolkit Wine, and Whisky's command helper, then model them as explicit runtime profiles.
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

Echo Gate validates a user-provided patch library by checking for the known one-boot-patch and 51-game-patch sequence. Patch files are checked by path and expected byte size. CRC32 verification is available in the core patch-library model and is reserved for explicit integrity passes. The expected local shape is:

```text
ffxiv/2d2a390f/patch/D<version>.patch
ffxiv/2d2a390f/metainfo/D<version>.torrent
ffxiv/48eca647/patch/D<version>.patch
ffxiv/48eca647/metainfo/D<version>.torrent
```

Patch files and metainfo files are local user-provided artifacts and remain excluded from repository state.

Echo Gate does not bundle, host, or automatically download patch payloads. Internet Archive entries, torrent metainfo files, historical S3 URLs, and private mirrors are treated as user-selected sources outside repository state until a controlled patcher flow is implemented.

For Echo Gate launcher/runtime design, see `docs/LAUNCHER_DESIGN.md` and `docs/WINE_RUNTIME_STRATEGY.md`.
