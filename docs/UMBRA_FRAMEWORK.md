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

