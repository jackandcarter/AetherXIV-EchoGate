# Current Status

This matrix is the short version of where the project stands today.

| Area | Status | Notes |
| --- | --- | --- |
| Local MariaDB setup | Working | `tools/setup-local-db.sh` creates `ffxiv_server`, imports SQL, and creates `aetherxiv` / `aether_dev` by default. |
| Shared env defaults | Working | `.env.defaults` is checked in. `.env.local` is optional for overrides. |
| PHP launcher services | Working locally | Status, news, login, account creation, patch manifest, and runtime catalog endpoints are present. |
| Lobby Server | Working for local flow | Auth/session and character handoff path are usable for playtests. |
| World Server | Working for local flow | Accepts client world connections and coordinates map handoff. |
| Map Server | Working for startup/playtest | Loads zone data, actors, commands, Lua scripts, and `staticactors.bin` when prepared. |
| `staticactors.bin` setup | Working locally | Helpers can prepare it from the saved Echo Gate client path, `CLIENT_DIR`, or a prompted path. |
| Echo Gate Home/Server tabs | Working | Account, login, news, service status, and local server profile settings are implemented. |
| Echo Gate Client tab | Working | Validates the local 1.23b client and user-provided patch library metadata. |
| Echo Gate Runtime tab | Working | Uses the approved-runtime flow, validates runtimes, supports install/catalog plumbing, and keeps custom runtime paths as an advanced option. |
| macOS Apple Silicon | Working path | Native Echo Gate app builds; client launch works through supported Wine-compatible runtimes. |
| macOS Intel | Expected path | Same guide applies with Intel runtime identifier, but Apple Silicon is the primary tested path. |
| Linux x64 | Working path | Ubuntu/Debian bootstrap covers build tools, Wine, Winetricks, i386 graphics deps, prefix prep, and launcher publish. |
| Linux ARM64 | Partial | Launcher/server build target exists; launching the 32-bit Windows client still needs validation. |
| Windows x86/x64 | Supported path | Native Windows client launch path exists; Windows setup docs need more user reports. |
| Windows ARM64 | Partial | Echo Gate publish target exists; Windows x86 client emulation and runtime behavior need validation. |
| Steam Deck | Experimental | Expected to follow the Linux path in Desktop Mode, but hardware validation is still pending. |
| Approved runtime catalog | Partial | Service contract exists; hosted runtime archives and release process still need production packaging. |
| Gameplay systems | Partial | Core flow is usable, but quests, inventory, trade, bazaar, battle details, and persistence need deeper testing. |
| Reverse-engineering tools | Available | Dev-only packet and trace tooling is documented in `docs/REVERSE_ENGINEERING_TOOLS.md`. |

## Current Best Test Path

1. Set up the local database.
2. Build the legacy servers.
3. Copy runtime data and prepare `staticactors.bin`.
4. Run PHP, Lobby, Map, and World services.
5. Build or open Echo Gate.
6. Select a local user-owned client folder.
7. Validate runtime.
8. Create an account, log in, and launch.

Platform-specific details live under [docs/guides](guides/).
