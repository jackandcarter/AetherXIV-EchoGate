# Modern .NET Porting Strategy

## Target

Server runtime:

- first modern target: `net10.0`
- legacy compatibility path retained as the acceptance gate for the modern build

Client protocol target:

- Final Fantasy XIV 1.23b / `2012.09.19.0001`
- A Realm Reborn is outside scope.
- Current retail FFXIV is outside scope.

## Design Laws

- Preserve packet layouts byte-for-byte.
- Preserve the existing SQL schema unless migration tests prove equivalence.
- Preserve Lua script behavior and call signatures.
- Preserve observable 1.23b client protocol behavior.
- Prefer incremental ports over rewrites.

Repository exclusions:

- client files
- extracted client assets
- patch files
- packet captures containing credentials
- local secrets

## Phase 0: Legacy Baseline

Baseline requirements:

1. Current Mono/.NET Framework path builds.
2. Database imports locally.
3. Smoke checks cover config loading, DB connection, socket bind/listen, packet compression/encryption, and server startup.
4. Unknown opcode logging exists for lobby, world, and map packets.

Current status:

- Legacy build confirmed with `tools/build-legacy.sh` and Mono `xbuild`.
- Local database import confirmed with `tools/import-db.sh`.
- Lobby and World DB/startup smoke checks confirmed.
- Map startup blocked by missing local `Data/staticactors.bin`.

## Phase 1: SDK-Style Projects

Convert the four old `.csproj` files to SDK-style projects:

- `Common Class Lib` -> class library
- `Lobby Server` -> console app
- `Map Server` -> console app
- `World Server` -> console app

Use `PackageReference` instead of `packages.config`.

Conversion requirements:

- include existing source files without accidental duplicates
- preserve `App.config` behavior
- preserve `NLog.config` behavior
- preserve runtime content from `Data`
- account for project names and directories containing spaces

## Phase 2: Dependency Compatibility

Initial dependency policy:

- `MySql.Data`: retain for first port to reduce behavior changes
- `NLog`: update and retain
- `MoonSharp`: retain for Lua compatibility
- `Newtonsoft.Json`: retain for first port
- `Portable.BouncyCastle`: verify usage before removal
- `DotNetZip` / `Ionic.Zlib`: replace only after packet compression tests exist
- `Cyotek.CircularBuffer`: retain or replace with tested local ring buffer
- `SharpNav.dll`: isolate behind a navmesh interface before replacement
- `Microsoft.Net.Compilers`: remove from modern projects

Highest-risk dependency:

- `SharpNav.dll`, because it is a bundled legacy binary dependency.

Known vulnerable legacy packages:

- `DotNetZip` 1.10.1
- `Newtonsoft.Json` 9.0.1

## Phase 3: Runtime And Config

Runtime requirements:

- predictable working directory
- standardized config file lookup
- environment variable overrides
- local `.ini` compatibility
- cross-platform runtime data copy
- clear output folder layout
- service-specific startup commands

## Phase 4: Modernize Internals

Internal modernization targets:

- shared DB connection string helper
- cancellation-aware server loops
- modern async socket accept/receive paths
- structured shutdown handling
- structured packet logging
- protocol tests around every packet layout change

## Phase 5: Login Layer

Current login/vercheck layer:

- PHP in `Data/www`

Modern target:

- ASP.NET Core login/vercheck service

Target local stack:

- MariaDB/MySQL
- Lobby Server
- Map Server
- World Server
- Login/vercheck web API

## Parallel Track: Echo Gate

Echo Gate is the launcher/runtime track for macOS/Linux client validation.

Boundary:

- server runtime remains in the Meteor server stack
- launcher/runtime orchestration remains in `launcher/`

Scope:

- client path validation
- server profile writing
- static actor data preparation
- Wine/CrossOver/Proton launch orchestration
- launch diagnostics

Exclusions:

- client distribution
- patch distribution
- client modification

Design references:

- `docs/LAUNCHER_DESIGN.md`
- `docs/WINE_RUNTIME_STRATEGY.md`

## Validation Requirements

Backend validation:

- build all projects
- import database
- validate config loading
- validate DB connectivity
- validate socket bind/listen
- validate packet serialization
- validate packet compression/encryption
- validate login/session SQL behavior

Client-path validation:

- local 1.23b client install
- local `Data/staticactors.bin`
- launcher/server profile configuration
- Lobby -> World -> Map connection path
- packet captures or structured packet logs for gaps in `CLIENT_REQUIREMENTS.md`

## First Modern Port Milestone

Milestone 1:

1. Keep current legacy projects intact.
2. Add parallel SDK-style project files.
3. Port `Common Class Lib`.
4. Add tests for `BasePacket`, `SubPacket`, `Blowfish`, and zlib compression.
5. Port `Lobby Server`.

Completion criteria:

- Common library tests pass.
- Lobby builds on modern .NET.
- Legacy build remains available.
- Packet layout tests pass against known fixtures.
