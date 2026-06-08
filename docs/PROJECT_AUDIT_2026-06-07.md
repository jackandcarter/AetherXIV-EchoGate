# Project Audit - 2026-06-07

## Scope

This audit reviews the local state of `jackandcarter/Meteor`, a fork/port of Project Meteor for the Final Fantasy XIV 1.23b client. It focuses on repository readiness, macOS/Linux development setup, server/database runtime requirements, client compatibility, and known gaps.

## Local Repository State

- The folder is now initialized as a git checkout and tracks `origin/main` at `https://github.com/jackandcarter/Meteor.git`.
- Local branch: `main`.
- Upstream branch: `origin/main`.
- GitHub connector reports push/admin-level access for `jackandcarter/Meteor`.
- Existing local difference before this audit: `README.md` was missing the latest upstream "project notes" section.
- No open GitHub issues were found through the GitHub connector.

## Current Project Shape

- C# solution: `Meteor.sln`.
- Projects:
  - `Lobby Server`: login/session/character-list socket server.
  - `World Server`: session routing, chat, party/linkshell/group coordination, zone server routing.
  - `Map Server`: zone simulation, actors, inventory, battle, events, Lua integration.
  - `Common Class Lib`: packet/common utilities.
- Data:
  - `Data/sql`: 64 SQL dump files, about 44k lines total.
  - `Data/www`: PHP login/vercheck files.
  - `Data/scripts`: Lua content, commands, NPC scripts, battle abilities/effects, directors.

## Build And Tooling State

- The solution targets .NET Framework 4.7.2 and old-style `packages.config` NuGet packages.
- The local machine has modern `dotnet` SDKs, but this legacy solution needs Mono build tooling plus NuGet.
- A `dotnet build Meteor.sln` attempt stalled and was stopped; modern `dotnet build` is not a reliable path for this legacy solution.
- Required local tooling for the current codebase:
  - Mono with `msbuild` or `xbuild`
  - NuGet CLI
  - MariaDB/MySQL server and client
  - PHP with mysqli support for `Data/www`
- Current local machine status:
  - `mono`, `xbuild`, `nuget`, and `php` are installed through Homebrew.
  - `msbuild` is not available from the current Homebrew Mono package, so `tools/build-legacy.sh` uses `xbuild`.
  - `xbuild Meteor.sln /p:Configuration=Release` succeeds after guarding `Microsoft.Net.Compilers` imports on non-Windows.
  - NuGet restore works, but reports known vulnerabilities in `DotNetZip` 1.10.1 and `Newtonsoft.Json` 9.0.1.
  - `mysql` client exists, MariaDB client 12.1.2.
  - MariaDB 12.1.2 is installed through Homebrew.
  - Homebrew MariaDB socket auth works for the local `imac` user.
  - root/no-password is not valid for TCP or socket auth.

## Runtime Data State

- `Data/staticactors.bin` is missing.
- The historical Project Meteor compile guide says this file is created by copying the 1.23b client file `client/script/rq9q1797qvs.san` into the server data folder as `staticactors.bin`.
- `Map Server/Server.cs` loads `./staticactors.bin` at startup. Map runtime readiness requires this file in the Map Server output folder.
- `tools/copy-runtime-data.sh` copies configs and scripts into the output folders. It warns when `Data/staticactors.bin` is missing.
- `tools/prepare-client-data.sh` can prepare `Data/staticactors.bin` from a local 1.23b client install once one is available.

## Database State

- Runtime configs point to:
  - host: `127.0.0.1`
  - port: `3306`
  - database: `ffxiv_server`
  - username: `meteor`
  - password: `meteor_dev`
- These credentials are local-lab defaults and are not suitable for exposed deployments.
- `Data/sql/import.sh` was stale and syntactically invalid for MySQL/MariaDB. Use `tools/import-db.sh` instead.
- `tools/import-db.sh` now defaults to `localhost` socket auth and the current OS username, with `DB_HOST`, `DB_PORT`, `DB_USER`, and `DB_PASS` overrides.
- `tools/create-db-user.sh` creates or updates the default local `meteor` DB user.
- `ffxiv_server` has been imported locally with 65 tables.
- `server_zones.sql` maps all zones to `127.0.0.1:1989`, matching the default Map Server port.
- `servers.sql` maps world id 1 to `127.0.0.1:54992`, matching the default World Server port.

## Startup Order

Recommended local startup order:

1. Start MariaDB/MySQL.
2. Start the PHP login/vercheck web root from `Data/www`.
3. Start Lobby Server.
4. Start Map Server.
5. Start World Server.

Reason: `World Server/Server.cs` calls `LoadZoneServerList()` and `ConnectToZoneServers()` during startup. Map Server must already be listening when World Server starts. The historical wiki also starts Lobby, then Map, then World.

## Smoke Test Results

- `tools/audit-env.sh` passes for installed tooling, package restore state, and MariaDB app-user connectivity, except `msbuild` is unavailable and `staticactors.bin` is missing.
- `RESTORE=0 ./tools/build-legacy.sh` succeeds through Mono `xbuild` with 5 warnings and 0 errors after package restore.
- `CONFIGURATION=Release ./tools/copy-runtime-data.sh` copies configs and scripts, and warns about missing `Data/staticactors.bin`.
- PHP lint passes for all `Data/www/**/*.php` files.
- Lobby Server starts from a normal local shell, connects to MariaDB as `meteor`, and listens on `0.0.0.0:54994`.
- World Server starts from a normal local shell, connects to MariaDB as `meteor`, loads world/zone rows, and listens on `0.0.0.0:54992`; Map routing requires Map Server availability.
- Map Server starts from a normal local shell, connects to MariaDB as `meteor`, then exits with `FileNotFoundException` for `Map Server/bin/Release/staticactors.bin`.

## Client Compatibility

- This project targets the Final Fantasy XIV 1.23b client, not A Realm Reborn or current retail FFXIV.
- The Project Meteor setup wiki lists Final Fantasy XIV 1.23b version `2012.09.19.0001` and Seventh Umbral Launcher 1.03 as the expected client stack.
- There was no native official Mac client for the 1.x era. The later official macOS client belongs to A Realm Reborn/Heavensward era, not this protocol.
- On Apple Silicon, the practical client paths are:
  - CrossOver/Wine/Wineskin style wrapper for the Windows 1.23b client and Seventh Umbral launcher.
  - A Windows x86 VM or emulation environment for Wine compatibility fallback.
  - A separate Intel/Windows/Linux test machine for packet capture and client validation, if available.
- Client assets remain local and excluded from version control and redistribution.
- Echo Gate launcher design is documented in `docs/LAUNCHER_DESIGN.md` and `docs/WINE_RUNTIME_STRATEGY.md`.

## Current Implementation Signals

The local `CLIENT_REQUIREMENTS.md` is the best project-owned status matrix. It marks:

- Lobby secure/session/character list/select flows as implemented.
- Character modify as partial.
- Retainer modify as missing.
- World session routing, ping, zone changes, party chat, login trigger as implemented.
- Map session begin/end, chat, language/login, position update, event start as implemented.
- Map event update as partial.
- Bazaar/trade, some inventory checks, company systems, quests, shops, and several NPC flows as partial/TODO.

The historical feature-status wiki is older, but it confirms the project was not feature-complete and that many gameplay systems were missing or partial in earlier Project Meteor history.

## High Priority Gaps

1. Build reproducibility on macOS/Linux
   - Maintain `tools/build-legacy.sh` as the known-good baseline through the modern port.
   - Consider a Docker/Podman dev environment for Linux consistency.
   - Add a CI workflow once build commands are stable.

2. Runtime bootstrap
   - Obtain a legally owned 1.23b client install and create local `Data/staticactors.bin`.
   - Re-run `CONFIGURATION=Release ./tools/copy-runtime-data.sh`.
   - Re-run Map and then World smoke tests together.

3. Client validation loop
   - Obtain a legally owned 1.23b client install.
   - Configure Seventh Umbral Launcher or equivalent local launcher path.
   - Use CrossOver/Wine as the primary macOS/Linux client runtime path.
   - Capture Lobby -> World -> Map packet phases and fill `CLIENT_REQUIREMENTS.md`.

4. Launcher development
   - Start with an Echo Gate desktop app that validates the client path, prepares `staticactors.bin`, writes server profiles, and launches through a user-selected Wine/CrossOver runtime.
   - Keep client download/patching out of scope pending a clear legal/source model.

5. Unknown opcode instrumentation
   - Add structured logging for unknown lobby/world/map opcodes.
   - Record opcode, packet type, source/target, byte length, and a bounded hex dump.
   - Feed results into `CLIENT_REQUIREMENTS.md`.

6. Database migrations
   - Replace unordered SQL dump import with ordered bootstrap/migration tooling.
   - Add schema validation checks for required tables and seed rows.

7. Tests and smoke checks
   - Add packet encode/decode unit tests.
   - Add DB fixture tests for login/session/character queries.
   - Add a no-client smoke test that verifies all servers can reach DB and bind/listen.

8. Security and config hygiene
   - Keep default configs local-only.
   - Avoid root/no-password credentials outside localhost.
   - Move secrets into local override files or environment variables.

9. Modern .NET port
   - Port incrementally instead of rewriting.
   - Preserve packet layouts, Lua call signatures, and SQL behavior.
   - See `docs/PORTING_STRATEGY.md`.

## Useful External References

- Project Meteor setup wiki: https://wiki.ffxivrp.org/pages/Project_Meteor_Setup
- Project Meteor server setup wiki: https://wiki.ffxivrp.org/pages/ServerSetup
- Project Meteor compile guide: https://wiki.ffxivrp.org/pages/Compiling-Simple
- Project Meteor client/launcher guide: https://wiki.ffxivrp.org/pages/ClientAndLauncher
- Packet headers: https://wiki.ffxivrp.org/pages/Packet_Headers
- Game opcodes: https://wiki.ffxivrp.org/pages/Game_Opcodes
- Project Novum documentation index: https://project-novum.github.io/
- Original/mirror repository context: https://git.luje.net/zydronium/project-meteor-server
