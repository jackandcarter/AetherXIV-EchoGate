# MeteorXIV Server Features

MeteorXIV Core contains the legacy server stack needed by the FFXIV 1.x client. The current project focus is reliable local setup, cross-platform hosting, and enough gameplay/runtime coverage to make repeated playtests practical.

## Lobby Server

- Accepts client lobby connections.
- Validates sessions created by the launcher/PHP service.
- Handles account and character selection flow.
- Sends the selected world server host and port to the client.
- Uses shared database settings from the local setup helpers.

## World Server

- Accepts world connections after lobby handoff.
- Tracks connected sessions.
- Coordinates chat, world routing, and zone transitions.
- Connects to configured Map Server instances.

## Map Server

- Loads zone data, actors, command handlers, Lua scripts, region data, and gameplay tables.
- Requires `Data/staticactors.bin`, which can be prepared locally from a selected FFXIV 1.x client folder.
- Handles map connections from World Server.
- Contains partial gameplay systems for actors, events, commands, battle behaviors, and zone state.

## PHP Launcher Services

- Exposes `/launcher/status` and `/launcher/news` for Echo Gate.
- Exposes `/launcher/login` and `/launcher/create-account`.
- Exposes patch manifest and runtime catalog metadata endpoints.
- Shares the same local MariaDB database as the legacy servers.

The endpoint reference lives in [Echo Gate Launcher Services](LAUNCHER_SERVICES.md).

## Local Setup Helpers

- `.env.defaults` provides default local database and service values.
- `.env.local` is optional for machine-specific overrides.
- `tools/setup-local-db.sh` creates/imports the database and creates the app user.
- `tools/load-local-env.sh` is used internally by setup/run helpers.
- `tools/copy-runtime-data.sh` copies INI/script/runtime files into server output folders.
- `tools/prepare-client-data.sh` prepares client-derived data such as `staticactors.bin`.
- `tools/run-web.sh`, `tools/run-lobby.sh`, `tools/run-map.sh`, and `tools/run-world.sh` launch the local stack.

## Known Partial Areas

- Many gameplay systems still need verification against real packet captures.
- Some packet and actor fields are known from legacy source structure but still need naming and behavior confirmation.
- Quest, trade, bazaar, inventory edge cases, battle systems, and world-state persistence need more test coverage.
- Steam Deck, Linux ARM64 client launch, and Windows ARM64 client launch need hardware validation.

See [Current Status](STATUS.md) for the active matrix and [Future Development](FUTURE_DEVELOPMENT.md) for the next work areas.
