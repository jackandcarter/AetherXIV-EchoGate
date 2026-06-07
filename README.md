# Meteor
Project Meteor for XIV 1.x MacOS and Linux

A few notes:

This Meteor project is a port and fork of the original Project:Meteor. It is in no way connected to anyone from that project.
The current source targets a very old dotnet version, which will be updated for dotnet10 in this project in the near future.

Until then, the main branch is mostly for reference. Old Project:Meteor targeted Windows as the hosting platform, while this project targets Windows, Linux, and MacOS.

## Solution layout
- `Lobby Server/`: Login and character selection service.
- `World Server/`: World-level routing and chat/zone session coordination.
- `Map Server/`: Zone/map simulation and gameplay state.
- `Common Class Lib/`: Shared libraries used by all servers.
- `Data/`: Runtime configuration, SQL, and web assets.

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
The solution targets .NET Framework 4.7.2 and uses `packages.config` for NuGet restore, so the tooling differs slightly per OS. Make sure MariaDB/MySQL is reachable with credentials configured in the `Data/*_config.ini` files before starting the servers.

For macOS/Linux details, see `docs/MACOS_LINUX_DEV_SETUP.md`. For the current audit and missing-work map, see `docs/PROJECT_AUDIT_2026-06-07.md`. For the modern .NET porting plan, see `docs/PORTING_STRATEGY.md`. For the future client launcher, see `docs/LAUNCHER_DESIGN.md` and `docs/WINE_RUNTIME_STRATEGY.md`.

### Linux (Ubuntu, etc.)
1. Install Mono build tooling + NuGet (package names vary by distro; common ones are `mono-complete`, `msbuild`, and `nuget`).
2. Restore NuGet packages and build:
   ```
   ./tools/build-legacy.sh
   ```
3. Copy or verify the config files are in each output directory (they are marked to copy during build):
   - `Lobby Server/bin/Release/lobby_config.ini`
   - `World Server/bin/Release/world_config.ini`
   - `Map Server/bin/Release/map_config.ini`
   - `Map Server/bin/Release/scripts/`
   - `Map Server/bin/Release/staticactors.bin`
4. On macOS/Linux, run:
   ```
   CONFIGURATION=Release ./tools/copy-runtime-data.sh
   ```
5. Run servers in order (from their output folders):
   ```
   mono "Lobby Server/bin/Release/Lobby Server.exe"
   mono "Map Server/bin/Release/Map Server.exe"
   mono "World Server/bin/Release/World Server.exe"
   ```

### macOS (Intel or Apple Silicon)
1. Install Mono and NuGet (e.g., via Homebrew).
2. Restore packages and build:
   ```
   ./tools/build-legacy.sh
   ```
3. Copy runtime files:
   ```
   CONFIGURATION=Release ./tools/copy-runtime-data.sh
   ```
4. Run servers in order:
   ```
   mono "Lobby Server/bin/Release/Lobby Server.exe"
   mono "Map Server/bin/Release/Map Server.exe"
   mono "World Server/bin/Release/World Server.exe"
   ```

### Windows
1. Install Visual Studio (with .NET Framework 4.7.2 developer pack) or MSBuild + NuGet.
2. Restore packages:
   ```
   nuget restore Meteor.sln
   ```
3. Build (Visual Studio or command line):
   ```
   msbuild Meteor.sln /p:Configuration=Release
   ```
4. Run servers in order from their output directories:
   ```
   "Lobby Server\bin\Release\Lobby Server.exe"
   "Map Server\bin\Release\Map Server.exe"
   "World Server\bin\Release\World Server.exe"
   ```
