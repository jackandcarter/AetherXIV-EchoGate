# Modern .NET Porting Strategy

## Target

Port the server stack while preserving the Final Fantasy XIV 1.23b protocol and data behavior.

Target client:

- Final Fantasy XIV 1.23b / `2012.09.19.0001`
- Not A Realm Reborn
- Not the current retail client

Target runtime:

- First modern target: `net10.0`
- Keep a compatibility path until the modern build proves it can run the same local smoke tests.

## Guiding Rules

- Preserve packet layouts byte-for-byte.
- Preserve the existing SQL schema until tests prove a migration is safe.
- Preserve Lua script behavior and call signatures.
- Do not commit client files, extracted client assets, packet captures containing credentials, or local secrets.
- Prefer incremental ports over rewrites.

## Recommended Port Path

### Phase 0: Stabilize Current Baseline

Before changing runtime:

1. Make the current Mono/.NET Framework path build once. Done locally with `tools/build-legacy.sh` and Mono `xbuild`.
2. Import the DB locally. Done locally with `tools/import-db.sh`.
3. Add smoke tests for config loading, DB connection, packet compression/encryption, and socket bind/listen.
4. Add unknown opcode logging.

This gives us a behavior baseline for the modern port.

### Phase 1: SDK-Style Projects

Convert the four old `.csproj` files to SDK-style projects:

- `Common Class Lib` -> class library
- `Lobby Server` -> console app
- `Map Server` -> console app
- `World Server` -> console app

Use `PackageReference` instead of `packages.config`.

The current code has about 412 C# files. SDK-style projects can include `.cs` files automatically, but care is needed around:

- duplicated generated files
- `App.config`
- `NLog.config`
- runtime content files from `Data`
- project names/directories with spaces

### Phase 2: Dependency Compatibility

Likely dependency handling:

- `MySql.Data`: keep initially to reduce code changes; consider `MySqlConnector` later.
- `NLog`: update and keep.
- `MoonSharp`: keep for Lua compatibility.
- `Newtonsoft.Json`: keep initially.
- `Portable.BouncyCastle`: likely removable, because the repo has its own `Blowfish.cs`; verify before removing.
- `DotNetZip` / `Ionic.Zlib`: replace with `System.IO.Compression.ZLibStream` in modern .NET, but add packet compression tests first.
- `Cyotek.CircularBuffer`: keep initially or replace with a small local ring buffer.
- `SharpNav.dll`: isolate behind a navmesh interface. Recompile or replace later. This bundled old DLL is the highest-risk binary dependency.
- `Microsoft.Net.Compilers`: remove.

### Phase 3: Runtime And Config

Make the server runnable from the repo root or a predictable output folder:

- Standardize config file lookup.
- Support environment variable overrides.
- Keep local `.ini` support for convenience.
- Copy scripts/static actor data through cross-platform tooling.
- Add a single dev launcher script after individual services work.

### Phase 4: Modernize Internals Safely

After the first modern build runs:

- Move repeated DB connection string construction into one helper.
- Add cancellation-aware server loops.
- Replace old `BeginAccept`/`BeginReceive` patterns with modern async sockets.
- Improve shutdown handling.
- Add structured packet logging.
- Keep protocol tests around every packet change.

### Phase 5: Port The PHP Login Layer

The PHP `Data/www` layer can stay temporarily. Later, replace it with a small ASP.NET Core service so the local stack is:

- MariaDB/MySQL
- Lobby Server
- Map Server
- World Server
- Login/vercheck web API

This removes the PHP dependency and makes local development easier on macOS/Linux.

### Parallel Track: Echo Gate

The launcher/runtime path should move in parallel with the backend port, because macOS/Linux client testing depends on Wine/CrossOver.

Keep this separate from the server runtime:

- design docs live in `docs/LAUNCHER_DESIGN.md` and `docs/WINE_RUNTIME_STRATEGY.md`
- future app code lives in `launcher/`
- initial scope is client validation, server profile writing, static actor data preparation, and Wine/CrossOver launch orchestration
- do not distribute client files or patch files

## Client Reality

No client means no end-to-end validation yet. We can still do useful backend work:

- build and run the servers
- validate DB bootstrap
- validate packet serialization/compression/encryption
- validate login/session SQL behavior
- build a packet replay harness once captures exist

End-to-end validation requires a legally obtained 1.23b client. For Apple Silicon, expect to use Wine/CrossOver/Wineskin or a separate Windows/x86 test environment.

## First Practical Port Milestone

Milestone 1 should be intentionally small:

1. Keep current legacy projects untouched.
2. Add a parallel SDK-style solution or project files.
3. Port `Common Class Lib` first.
4. Add tests for `BasePacket`, `SubPacket`, `Blowfish`, and zlib compression.
5. Port `Lobby Server` next because it has the smallest gameplay surface.

Once Lobby builds and packet tests pass, port World and Map.
