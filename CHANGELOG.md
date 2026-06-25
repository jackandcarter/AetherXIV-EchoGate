# Changelog

Record notable project changes here after confirmation.
Use concise entries and link to issues, pull requests, packet captures, or test notes when possible.

## Unreleased

- Set up repository hygiene for macOS/Linux development auditing.
- Added macOS/Linux setup docs, audit notes, and modern .NET porting strategy.
- Added cross-platform helper scripts for environment audit, legacy build, DB import, DB user creation, and runtime data copy.
- Imported the local `ffxiv_server` database and documented local-only DB user setup through ignored `.env.local` values.
- Fixed non-Windows legacy builds by keeping `Microsoft.Net.Compilers` imports Windows-only.
- Added Echo Gate launcher architecture and Wine/CrossOver runtime strategy docs.
- Added `tools/prepare-client-data.sh` for preparing local `staticactors.bin` from a user-owned 1.23b client install.
- Added server `--smoke` checks, Release-mode unknown packet diagnostics, and local run/smoke scripts.
- Added PHP login database overrides through `METEOR_DB_*` environment variables and ignored `config.local.php` files.
- Added the initial Echo Gate Avalonia desktop app foundation with core launcher models and unit tests.
- Added Echo Gate client readiness reports, 1.x patch-library validation with expected size/CRC32 metadata, and macOS runtime candidate discovery.
- Added database-backed Echo Gate launcher services for config, status, news, patch manifests, and runtime catalogs.
- Added Echo Gate managed runtime install, validation, prefix, launch-plan, and launch-log support for macOS/Linux Wine workflows.
- Added service-configured patch download/apply support, Home tab launcher polish, persistent client/patch paths, and cross-platform publish scripts.
- Added Echo Gate launcher-service account login/create endpoints, username-only remember-me profile support, legacy-compatible `Servers.xml` writing, and helper-based 1.23b client launch patching.
- Added 32-bit client-helper runtime probing so macOS/Linux launch is blocked until the selected Wine runtime can execute FFXIV 1.x-compatible PE32 helpers.
- Documented launcher services, runtime archive hosting, managed app-data paths, and macOS/Linux setup/build validation.
- Renamed the legacy server codebase to AetherXIV Core, removed client acquisition from launcher service contracts, tightened local-secret handling, and added focused world/map zone-handoff diagnostics.
- Added dev-only traces for event condition packets, client target/lock requests, unresolved event starts, and provisional map `0x00CE` tutorial/state messages.
- Added dev-only traces for quest flags, quest saves, quest phases, quest data, and actor quest-graphic updates.
- Fixed Ul'dah opening tutorial quest-marker progression so Ascilia, Fretful Farmhand, Gil-digging Mistress, and the exit trigger advance in sequence from persisted quest flags.
- Fixed Ul'dah opening tutorial replay by disabling Ascilia's movement push event after the persisted tutorial flag is complete.
- Updated `tools/run-map.sh` to refresh copied runtime scripts before starting Map by default.
- Added World-to-Map session cleanup on client disconnect and duplicate login so stale player actors are removed through the normal session-end path.
- Fixed Ul'dah opening tutorial journal handoff so completing the NPC/journal tutorial persists the first mini-tutorial flag and moves the quest marker to Fretful Farmhand.
- Disabled the Ul'dah opening exit trigger until all three mini-tutorial flags are complete, preventing premature exit-trigger events from replaying the stalled tutorial state.
