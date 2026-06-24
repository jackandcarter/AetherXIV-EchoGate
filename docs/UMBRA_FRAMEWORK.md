# Umbra Framework Development Notes

Umbra is the Meteor client plugin framework for EchoGate-launched FFXIV 1.x clients.
This document records the current implementation boundary so development stays
evidence-led.

## Current Mechanics

- EchoGate resolves Umbra settings during launch and passes them to the x86
  client helper.
- The x86 helper starts `ffxivgame.exe` suspended, applies the existing launch
  patches, and injects `Meteor.Umbra.Bootstrap.x86.dll` with `LoadLibraryW`.
- The bootstrap DLL reads `METEOR_UMBRA_*` environment values inherited by the
  game process, writes the Umbra log, and starts the managed framework with
  `--bootstrap`.
- The managed framework reads the same runtime settings, scans the plugin
  directory for `umbra-plugin.json` or `plugin.json`, validates manifests, and
  records discovered plugins.

This proves the launcher-to-helper-to-game-to-bootstrap-to-framework path without
claiming that in-game rendering or plugin execution exists yet.

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

- Endpoint: `/launcher/umbra/plugin-catalog`
- Backing tables: `launcher_umbra_plugin_repositories` and
  `launcher_umbra_plugin_releases`
- Purpose: lists optional plugin releases available to users.
- Payload includes plugin id, name, version, API version, author, description,
  download URL, size, SHA256, minimum framework version, and active flag.

The launcher service config exposes:

- `client_plugin_framework_catalog_url`
- `plugin_catalog_urls`

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

## Evidence-Gated Work

The next mechanics need client/runtime evidence before implementation:

- Direct3D 9 device discovery and reset/present hook points for the 1.23b client.
- Whether Umbra should host managed plugins in-process, use a native shim with
  out-of-process managed coordination, or move to NativeAOT/plugin ABI boundaries.
- Input capture rules for an overlay that will not break existing client controls.
- Crash containment strategy for plugin load, update, draw, and disposal failures.

Until those are known, Umbra's managed framework should stay in manifest and
diagnostic mode rather than pretending to render or run plugins in-process.
