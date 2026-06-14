# MeteorXIV Core + Echo Gate XIV Launcher v1.1
MeteorXIV Core for XIV 1.x Windows, macOS, and Linux

A few notes:

MeteorXIV Core is a port and fork of the original Project:Meteor. It is in no way connected to anyone from that project.
The current source targets a legacy .NET Framework runtime. The planned modernization target is dotnet10.

Old Project:Meteor targeted Windows as the hosting platform, while this project targets Windows, Linux, and MacOS.

## Solution layout
- `Lobby Server/`: Login and character selection service.
- `World Server/`: World-level routing and chat/zone session coordination.
- `Map Server/`: Zone/map simulation and gameplay state.
- `Common Class Lib/`: Shared libraries used by all servers.
- `Data/`: Runtime configuration, SQL, and web assets.
- `launcher/EchoGate/`: Cross-platform FFXIV Classic launcher, patcher, runtime manager, and launch diagnostics.

## High-level flow
1. Client connects to the lobby server to authenticate and select a character.
2. Lobby responds with the selected world server address/port.
3. Client connects to the world server, which manages sessions and hands off zone work to map servers.

## Startup order
Start the servers in this order so downstream dependencies are available:
1. **Lobby Server** (`Lobby Server/Program.cs`) starts the login/character selection host and listens on the lobby socket configured by `server_ip`/`server_port` in `lobby_config.ini`.
2. **Map Server** (`Map Server/Program.cs`) loads zone data and opens its configured socket for world connections.
3. **World Server** (`World Server/Program.cs`) loads world data, connects to map servers, and listens for client world connections via its configured socket.

The socket listeners are created in each server's `Server.StartServer()` implementation (`Lobby Server/Server.cs`, `World Server/Server.cs`, `Map Server/Server.cs`), where the server binds and listens before accepting connections.

## Config
Configuration files live in `Data/*_config.ini`:
- `Data/lobby_config.ini`
- `Data/world_config.ini`
- `Data/map_config.ini`

Common INI keys:
- **General**
  - `server_ip`: bind IP address for the server socket.
  - `server_port`: TCP port for the server socket.
  - `showtimestamp`: toggles timestamped logging.
- **Database**
  - `worldid`: world identifier for DB lookups.
  - `host`: database host.
  - `port`: database port.
  - `database`: schema name.
  - `username`: database user.
  - `password`: database password.

Default ports used when `server_port` is omitted are `54994` for the lobby server and `54992` for the world server; the map server defaults to `1989` unless configured otherwise.

## Client connection
When a player selects a character, the lobby server builds a `SelectCharacterConfirmPacket` in `Lobby Server/PacketProcessor.cs` that includes the world server address and port, which tells the client to reconnect to the world server after character selection completes.

## Build & run
The solution targets .NET Framework 4.7.2 and uses `packages.config` for NuGet restore, so the tooling differs slightly per OS. For local macOS/Linux development, the checked-in `.env.defaults` file provides the shared defaults. Run `./tools/setup-local-db.sh`, then use the `tools/run-*` helpers so the same database settings are passed to Lobby, Map, World, and PHP services.

For macOS/Linux details, see `docs/MACOS_LINUX_DEV_SETUP.md`. For the current audit and missing-work map, see `docs/PROJECT_AUDIT_2026-06-07.md`. For the modern .NET porting plan, see `docs/PORTING_STRATEGY.md`. For dev-only reverse-engineering workflow support, see `docs/REVERSE_ENGINEERING_TOOLS.md`. For Echo Gate launcher design and services, see `docs/LAUNCHER_DESIGN.md`, `docs/LAUNCHER_SERVICES.md`, and `docs/WINE_RUNTIME_STRATEGY.md`.

For one-terminal playtest control, live diagnostics, evidence snapshots, and a localhost Codex bridge, see `playtest-bridge/README.md`.

Local server readiness:

```sh
./tools/smoke-local.sh --allow-missing-staticactors
```

Echo Gate test/build:

```sh
AVALONIA_TELEMETRY_OPTOUT=1 dotnet test launcher/EchoGate/EchoGate.sln -m:1 /nr:false
./tools/build-echo-gate-macos.sh
```

### Linux (Ubuntu, etc.)
1. Install Mono build tooling + NuGet (package names vary by distro; common ones are `mono-complete`, `msbuild`, and `nuget`).
2. Set up the local MariaDB database:
   ```
   ./tools/setup-local-db.sh
   ```
   The script uses `.env.defaults`, creates database `ffxiv_server`, and creates app user `meteor` with password `meteor_dev`. It can fall back to Ubuntu socket-auth root through `sudo`, and if MariaDB uses password auth it will ask for admin credentials. Create `.env.local` only if you want to override the defaults.
3. Restore NuGet packages and build:
   ```
   ./tools/build-legacy.sh
   ```
   The helper suppresses known legacy unused-code warnings by default. Use `SHOW_LEGACY_WARNINGS=1 ./tools/build-legacy.sh` when you want the full warning list.
4. Copy or verify the config files are in each output directory (they are marked to copy during build):
   - `Lobby Server/bin/Release/lobby_config.ini`
   - `World Server/bin/Release/world_config.ini`
   - `Map Server/bin/Release/map_config.ini`
   - `Map Server/bin/Release/scripts/`
   - `Map Server/bin/Release/staticactors.bin`
5. On macOS/Linux, run:
   ```
   CONFIGURATION=Release ./tools/copy-runtime-data.sh
   ```
   If `Data/staticactors.bin` is missing, this tries to prepare it from `CLIENT_DIR` or Echo Gate's saved client path. You can also run `./tools/prepare-client-data.sh` and enter the client folder when prompted.
6. Run servers in order:
   ```
   ./tools/run-lobby.sh
   ./tools/run-map.sh
   ./tools/run-world.sh
   ```

### macOS (Intel or Apple Silicon)
1. Install Mono and NuGet (e.g., via Homebrew).
2. Set up the local MariaDB database:
   ```
   ./tools/setup-local-db.sh
   ```
   The default local app login is `meteor` / `meteor_dev` against database `ffxiv_server`; the script asks for MariaDB admin credentials only when needed.
3. Restore packages and build:
   ```
   ./tools/build-legacy.sh
   ```
4. Copy runtime files:
   ```
   CONFIGURATION=Release ./tools/copy-runtime-data.sh
   ```
5. Run servers in order:
   ```
   ./tools/run-lobby.sh
   ./tools/run-map.sh
   ./tools/run-world.sh
   ```

### Windows
1. Install Visual Studio (with .NET Framework 4.7.2 developer pack) or MSBuild + NuGet.
2. Restore packages:
   ```
   nuget restore MeteorXIV.Core.sln
   ```
3. Build (Visual Studio or command line):
   ```
   msbuild MeteorXIV.Core.sln /p:Configuration=Release
   ```
4. Run servers in order from their output directories:
   ```
   "Lobby Server\bin\Release\MeteorXIV.Core.Lobby.exe"
   "Map Server\bin\Release\MeteorXIV.Core.Map.exe"
   "World Server\bin\Release\MeteorXIV.Core.World.exe"
   ```
