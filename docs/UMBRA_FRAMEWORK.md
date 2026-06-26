# Umbra Framework Development Notes

Umbra is the AetherXIV client plugin framework for EchoGate-launched FFXIV 1.x clients.
This document records the current implementation boundary so development stays
evidence-led.

![Echo Gate Umbra tab](../Umbra.png)

## Current Mechanics

- EchoGate resolves Umbra settings during launch and passes them to the x86
  client helper.
- The x86 helper starts `ffxivgame.exe` suspended, applies the existing launch
  patches, and injects `Aether.Umbra.Bootstrap.x86.dll` with `LoadLibraryW`.
- The bootstrap DLL reads `AETHER_UMBRA_*` environment values inherited by the
  game process, writes the Umbra log, resolves `hostfxr.dll`, and calls the
  managed Umbra entrypoint inside the game process.
- The managed framework reads the same runtime settings, scans the plugin
  directory for `umbra-plugin.json` or `plugin.json`, validates manifests, and
  records discovered plugins.
- The managed framework fetches supported and custom repository JSON catalogs,
  caches last-successful repository responses, parses Umbra and common
  Dalamud-style store fields, and separates Installed, Supported, Available,
  and Updates state.
- Plugin downloads are verified by size and SHA256 before extraction. Archive
  paths that are absolute or contain `..` are rejected. Installed plugins are
  written with a validated `umbra-plugin.json`, but third-party assemblies are
  not executed in this stage.
- The native bootstrap hooks the Direct3D 9 path, initializes the current ImGui
  shell after a valid device is observed, and renders the initial Umbra controls
  and bottom-right toast panels during live client testing.

This proves the launcher-to-helper-to-game-to-bootstrap-to-framework path and the
first in-game rendering shell without claiming that third-party plugin execution
is enabled yet.

## Catalog Model

Umbra uses separate catalogs for separate trust boundaries.

Framework catalog:

- Endpoint: `/launcher/umbra/framework-catalog?platform=win-x86`
- Backing table: `launcher_umbra_framework_artifacts`
- Purpose: tells EchoGate which Umbra framework bundle can be installed.
- Payload includes archive URL, archive size, SHA256, bootstrap DLL path,
  managed framework path, supported game hashes, active/default flags, and sort
  order.

Plugin catalog:

- Supported repository URLs come from active supported rows in
  `launcher_umbra_plugin_repositories`.
- `launcher_umbra_plugin_releases` remains available as an optional future cache,
  but it is not the source of truth for the in-game installer.
- If no supported repository rows exist, `plugin_catalog_urls` is empty and the
  Supported tab has no entries.
- Fresh databases get these tables from `Data/sql/launcher_services.sql`.
  Existing VPS/dev databases can apply
  `Data/sql/migrations/20260625_launcher_umbra_services.sql` with
  `./tools/apply-db-migrations.sh`.
- Custom repository URLs are user-provided HTTPS URLs, with localhost HTTP
  allowed for development.
- Endpoint: `/launcher/umbra/plugin-blocklist`
- Backing table: `launcher_umbra_plugin_blocks`
- Purpose: disables known-broken plugin ids/versions before plugin execution is
  ever enabled.

The launcher service config exposes:

- `client_plugin_framework_catalog_url`
- `plugin_catalog_urls`
- `plugin_blocklist_url`

Framework bundles and plugin archives should never be trusted by URL alone.
Every downloadable artifact must be checked against the size and SHA256 recorded
in the catalog before installation.

## Plugin Manifest Convention

Umbra scans:

- `<plugin directory>/umbra-plugin.json`
- `<plugin directory>/plugin.json`
- `<plugin directory>/<plugin folder>/umbra-plugin.json`
- `<plugin directory>/<plugin folder>/plugin.json`

Required manifest fields:

- `id`
- `name`
- `version`
- `api_version`
- `entry`
- `minimum_framework_version`
- `enabled`

`entry` must be a relative assembly path and may not contain `..` path segments.

## System Plugins

Umbra now starts a resident managed runtime after bootstrap and initializes
first-party system plugins. These are not third-party community assemblies; they
are bundled with the framework and exist to validate lifecycle, logging, and
developer tooling before external plugin execution is enabled.

Current system plugins:

- `Umbra Dev Bridge`: owns the localhost-only, read-only client observation
  bridge.
- `Umbra Trace Companion`: reserved for the in-game trace UI, including the
  planned lobby, map, world, and client observation panels.

The native ImGui settings window can request the Dev Bridge on or off by writing
the shared control file. The managed Dev Bridge plugin remains the only owner
that starts or stops the HTTP listener, which avoids a race between native UI,
managed runtime, and future plugin code.

On Wine, managed hosting still respects the existing safety gate. EchoGate's
macOS and Linux launcher builds enable `AETHER_UMBRA_ENABLE_MANAGED_ON_WINE=1`
automatically for Umbra launches; Windows native builds leave it off. Manual
bootstrap tests outside EchoGate still need to set that value explicitly before
the managed system plugins can start.

## Umbra Dev Bridge

The Umbra Dev Bridge is separate from the server-side playtest bridge. It is a
client-side reverse-engineering assistant lane for Codex and local developer
tools.

Default control file:

```text
<Umbra cache>/DevBridge/control.json
```

Primary environment variables:

- `AETHER_UMBRA_DEV_BRIDGE=1`
- `AETHER_UMBRA_DEV_BRIDGE_PORT=8797`
- `AETHER_UMBRA_DEV_BRIDGE_DIR=<path>`
- `AETHER_UMBRA_DEV_BRIDGE_CONTROL=<path>`

The bridge binds only to `127.0.0.1` and exposes read-only verbs:

- `GET /status`
- `GET /events?limit=100`
- `GET /logs?limit=120`
- `POST /capture/start`
- `POST /capture/pause`
- `POST /capture/stop`
- `POST /memory/peek`
- `POST /scan/pattern`

Safety boundary:

- no memory writes
- no packet sends
- no server build/reset controls
- no backend, launcher, or framework mutation
- every request is written to Umbra dev logs

## Evidence-Gated Work

The next mechanics need client/runtime evidence before implementation:

- Stabilized Direct3D 9 reset/lost-device handling for the 1.23b client under
  Wine and native Windows.
- A polished ImGui theme, persistent window positioning, and input capture rules
  for an overlay that will not break existing client controls.
- Crash containment strategy for plugin load, update, draw, and disposal failures.
- Managed-to-native draw bridging for third-party plugin UI.

Until those are known, Umbra should stay in catalog, installer, manifest,
first-party system-plugin, and safe in-game shell mode rather than pretending to
run community plugins.
