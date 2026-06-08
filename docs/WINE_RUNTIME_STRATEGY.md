# Wine Runtime Specification For FFXIV 1.x

## Purpose

This document defines the runtime strategy for launching the Final Fantasy XIV 1.23b Windows client on macOS and Linux.

The modern FFXIV macOS ecosystem demonstrates a proven pattern: native launcher, managed Wine runtime, controlled prefix, launch diagnostics, and graphics translation. Echo Gate applies that pattern to the 1.x client while keeping ARR/current-client authentication, patching, plugin, and DirectX 11 assumptions out of scope.

## Runtime Target

- Original Windows 1.23b client
- DirectX 9 era rendering
- 1.23b server protocol
- Seventh Umbral Launcher compatibility path
- macOS Apple Silicon development machines
- Linux Wine/Proton development machines
- Windows direct-launch machines

## Runtime Requirements

- One server-profile model across all platforms.
- Runtime profiles stored as explicit configuration.
- Launch logs preserved per run.
- Runtime executable path configurable.
- Wine prefix or CrossOver bottle path configurable.
- Environment variables configurable per runtime profile.
- Client files excluded from repository state.

## macOS Runtime Options

### CrossOver

Primary macOS evaluation target.

Rationale:

- maintained Wine distribution
- strong macOS support
- compatibility path for older Windows applications on modern macOS
- practical Apple Silicon option

Constraints:

- commercial runtime
- bottle setup requires documentation
- runtime files are not repository dependencies

### XIV on Mac Pattern

Adopted concepts:

- native launcher shell
- managed Wine prefix
- optimized Wine build selection
- launch diagnostics
- runtime update handling
- user-accessible install folder and logs

Excluded assumptions:

- current FFXIV boot layout
- Dalamud or plugin integration
- current-game authentication
- current-game patching
- DirectX 11 defaults

Detected runtime tool:

```text
/Applications/XIV on Mac.app/Contents/Resources/wine/bin/wine
```

This runtime is modeled as a Wine prefix profile. Echo Gate does not assume the XIV on Mac application profile or current-game configuration.

### Whisky

Secondary macOS evaluation target.

Runtime command shape:

```text
WhiskyCmd run <bottle-name> <windows-executable> [args]
```

Detected runtime tool:

```text
/Applications/Whisky.app/Contents/Resources/WhiskyCmd
```

Echo Gate models Whisky as a bottle runtime. Bottle selection remains explicit.

### Game Porting Toolkit

Secondary macOS evaluation target.

Detected runtime tool:

```text
/usr/local/Cellar/game-porting-toolkit/1.1/bin/wine64
```

Echo Gate models Game Porting Toolkit as a Wine prefix profile. Renderer behavior must be validated against the 1.x DirectX 9 client before it becomes a recommended default.

### WineCX / Wineskin / Other Wine Builds

Secondary macOS evaluation targets.

Use cases:

- free runtime path
- compatibility comparison
- contributor environments without CrossOver

Risks:

- runtime drift across Wine builds
- brittle 32-bit Windows behavior on modern macOS
- inconsistent prefix setup between contributors

## Linux Runtime Options

### Wine

Primary Linux baseline.

Required profile fields:

- `wine` binary path
- `WINEPREFIX`
- environment overrides
- renderer setting
- log path

### Proton

Secondary Linux runtime.

Required profile fields:

- Proton path
- compatibility data path
- launch command template
- environment overrides

Steam must not be a hard requirement for Echo Gate.

## Graphics Translation

The 1.x client requires direct runtime testing across DirectX 9 translation paths.

Evaluation targets:

- WineD3D
- DXVK for DirectX 9 through Vulkan/MoltenVK
- CrossOver-specific DirectX translation layers

Constraint:

- DXMT/D3DMetal is not assumed as the default renderer for 1.x.

## Runtime Profile Schema

Example:

```text
name = "CrossOver - FFXIV 1.x"
platform = "macos"
runtime_kind = "crossover"
runtime_path = "/Applications/CrossOver.app"
wine_prefix = "~/Library/Application Support/CrossOver/Bottles/ffxiv-1x"
dx_layer = "auto"
client_path = "/path/to/FINAL FANTASY XIV"
launcher_path = "/path/to/Launcher.exe"
```

Repository policy:

- exclude secrets
- exclude client files
- exclude generated local runtime data

## Runtime Test Matrix

1. CrossOver bottle launches Seventh Umbral Launcher.
2. Seventh Umbral Launcher detects the 1.23b client path.
3. Server profile points to local Meteor PHP/lobby.
4. Client reaches login.
5. Client reaches character select.
6. Client transitions to World Server.
7. Client reaches Map Server.

Record for each run:

- runtime name and version
- operating system version
- client version
- renderer path
- executable launched
- launch arguments
- observed result

## Echo Gate Automation Scope

- Create or select Wine prefix.
- Validate required runtime files.
- Write `servers.xml`.
- Check local Meteor services.
- Prepare `Data/staticactors.bin`.
- Open the local account creation URL.
- Launch Seventh Umbral Launcher or game executable.
- Collect logs.

## Echo Gate Exclusions

- Client download.
- Client patch download.
- Runtime bundling.
- Account/password storage.
- Client modification.
- Plugin injection.

## References

- XIV on Mac: https://www.xivmac.com/about-uscredits
- XIV on Mac source: https://github.com/marzent/XIV-on-Mac
- Homebrew XIV on Mac cask: https://formulae.brew.sh/cask/xiv-on-mac
- CodeWeavers / FFXIV Mac partnership: https://www.codeweavers.com/about/news/press/20190628
- Project Meteor client guide: https://wiki.ffxivrp.org/pages/ClientAndLauncher
