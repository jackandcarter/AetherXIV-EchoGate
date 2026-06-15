# Future Development

This file tracks forward-looking work only. Finished design notes and older planning docs have been replaced by the current guides and status matrix.

## Setup And Packaging

- Validate the GitHub release workflow on every target artifact after the first public run.
- Add a clearer release checklist for macOS Developer ID signing/notarization, Linux archives, and Windows publish artifacts.
- Host runtime catalog artifacts outside Git with immutable size and SHA256 metadata.
- Add better first-run checks for missing MariaDB, PHP, Wine, Winetricks, .NET SDK, and Mono tooling.
- Keep README screenshots current as Echo Gate UI changes.

## Platform Validation

- Validate Steam Deck in Desktop Mode with the Linux x64 guide.
- Verify Linux ARM64 hosting and document what client runtime path is realistic.
- Verify Windows ARM64 Echo Gate publish and client launch behavior.
- Collect user reports for Intel macOS and multiple Linux distributions beyond Ubuntu/Debian.

## Launcher

- Add a small browser admin panel for creating, editing, hiding, and pinning launcher news posts.
- Improve the runtime catalog deployment process.
- Add clearer client and patch validation messaging for common failure states.
- Expand launch-log summaries so the launcher can point users at likely graphics/runtime issues.
- Continue improving custom runtime support for users with non-default Wine prefixes.

## Server And Gameplay

- Expand automated smoke tests around login, character selection, world handoff, and map startup.
- Validate unknown packet fields against real traces.
- Improve behavior coverage for inventory, trade, bazaar, quests, battle, actor state, and persistence.
- Add safer admin/dev commands for local testing.
- Continue porting legacy assumptions toward modern, testable code paths where it reduces setup pain.

## Documentation

- Add screenshots for the Client tab and Launch Log tab.
- Add troubleshooting sections based on real user failure reports.
- Keep platform guides short enough for first-time setup, with deeper details linked only when needed.
