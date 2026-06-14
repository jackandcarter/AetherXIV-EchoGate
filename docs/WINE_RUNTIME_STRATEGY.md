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
- Runtime executable path configurable for custom profiles.
- Managed runtime executable path resolved from launcher service catalog metadata.
- Managed Wine prefix created automatically under app data.
- Environment variables configurable per runtime profile.
- Client files excluded from repository state.
- Runtime archives excluded from repository state.
- Downloaded runtime caches, install manifests, prefixes, and launch logs excluded from repository state.

## Default Runtime Model

macOS and Linux builds default to `Automatic Managed`.

Runtime selection priority:

1. active service-catalog managed Wine artifact
2. detected Homebrew Wine Stable, free Wine, or XIV on Mac Wine runtime
3. detected CrossOver or Whisky runtime
4. custom runtime profile

Windows builds use native client launch and do not expose runtime setup.

Managed runtime artifacts are advertised by `/launcher/runtime-catalog?platform=<rid>`. Echo Gate downloads the selected archive from static hosting, validates byte size and SHA256, extracts it under app data, writes an install manifest, and uses `WINEPREFIX=<managed-prefix>` for validation and launch.

The actual 1.23b game process is started by the included Windows launch helper `EchoGate.ClientLauncher.exe`. On macOS and Linux, Echo Gate runs the helper through the selected Wine runtime. The helper generates the encrypted session launch argument, creates the user-selected local `ffxivgame.exe` suspended, applies the verified 1.23b lobby host and CPU-thread patches, then resumes the process.

Runtime validation must execute the included `win-x86` launch helper before enabling launch on macOS or Linux. A successful `wine --version` or `wineboot -u` result is not sufficient because the FFXIV 1.x client is a 32-bit PE executable and requires working 32-bit WoW64 support.

Managed runtime state paths:

```text
macOS: ~/Library/Application Support/Demi Dev Unit/Echo Gate/
Linux: $XDG_DATA_HOME/Demi Dev Unit/Echo Gate/
Linux fallback: ~/.local/share/Demi Dev Unit/Echo Gate/
```

Managed runtime subfolders:

```text
Runtimes/
RuntimeCache/
Prefixes/ffxiv-1x/
Logs/
```

## macOS Runtime Options

### Homebrew Wine Stable

Preferred local macOS detection target when no service-catalog managed runtime is installed.

Detected runtime tool:

```text
/Applications/Wine Stable.app/Contents/Resources/wine/bin/wine
```

Echo Gate checks the app-bundle command before generic Homebrew symlinks such as `/opt/homebrew/bin/wine` or `/usr/local/bin/wine`. The runtime still must pass Echo Gate's `win-x86` launch-helper probe before launch is enabled.

Homebrew's cask state can drift from the app bundle on disk, so diagnostics should report the exact command path and `wine --version` output used for each launch.

### D3D9 Renderer Probe

Wine 11.0 on macOS can expose MoltenVK/Vulkan to WineD3D. The FFXIV 1.x client is a 32-bit Direct3D 9 process, and one observed Wine 11.0 crash failed inside the 32-bit `d3d9`/`wined3d` stack after the helper had successfully launched `ffxivgame.exe`.

Wine's WineD3D loader reads Direct3D settings from `HKCU\Software\Wine\Direct3D`, `HKCU\Software\Wine\AppDefaults\<app.exe>\Direct3D`, and the `WINE_D3D_CONFIG` environment variable. `WINE_D3D_CONFIG` takes precedence over the registry. Echo Gate uses the Wine-supported value below for Wine-prefix launches unless a runtime profile explicitly overrides it:

```text
WINE_D3D_CONFIG=renderer=gl,csmt=0
```

The equivalent per-game registry probe is:

```text
HKCU\Software\Wine\AppDefaults\ffxivgame.exe\Direct3D
renderer=gl
csmt=0
```

`renderer=gl` keeps the 1.x DX9 client on Wine's OpenGL-backed WineD3D path while Vulkan/MoltenVK remains an explicit future test target. `csmt=0` disables WineD3D's command-stream worker for this client after an observed Wine 11.0 crash repeated through the `wined3d_cs` path after the Square Enix logo.

This is a runtime compatibility setting, not a gameplay or server behavior change.

### DirectInput Mouse Capture Probe

Wine's DirectInput mouse path reads `MouseWarpOverride` from `HKCU\Software\Wine\AppDefaults\<app.exe>\DirectInput`. For FFXIV 1.x mouse-focus testing, apply any setting only to `ffxivgame.exe`:

```text
HKCU\Software\Wine\AppDefaults\ffxivgame.exe\DirectInput
MouseWarpOverride=force
```

This is a runtime compatibility probe, not a gameplay or server behavior change. It is meant to test whether Wine's cursor clipping/warping path is dropping focus or letting macOS regain the cursor during client input.

Observed Wine 11.0 result: `MouseWarpOverride=force` let the cursor move over the FFXIV 1.x title screen, but clicks and keyboard input did not land in the client. Leave this value unset by default. If a future focus test needs the opposite comparison, use `MouseWarpOverride=disable` for the same app key and remove it afterward.

### CrossOver

Selectable macOS evaluation target.

Rationale:

- maintained Wine distribution
- strong macOS support
- compatibility path for older Windows applications on modern macOS
- practical Apple Silicon option

Constraints:

- commercial runtime
- bottle setup requires documentation
- runtime files are not repository dependencies
- Echo Gate does not download or include CrossOver

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

Detected XIV on Mac Wine builds are candidates only. They must pass Echo Gate's 32-bit helper probe before they can be treated as usable FFXIV 1.x runtimes.

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

Primary Linux baseline and managed-runtime target.

Required profile fields:

- `wine` binary path
- `WINEPREFIX`
- environment overrides
- renderer setting
- log path

Managed Wine archives must be relocatable zip files with the Wine executable path recorded in `launcher_runtime_artifacts.executable_relative_path`.

For local Ubuntu/Debian build machines, `tools/bootstrap-ubuntu-build.sh --yes` installs basic distro Wine and Winetricks packages, initializes Echo Gate's default prefix, sets Windows 7 mode, and installs `d3dx9_41`. Use `--wine-source winehq` to add WineHQ's Ubuntu package source and install WineHQ Stable instead of distro Wine. Use `--no-client-runtime` to install Wine packages without preparing the prefix, or `--no-wine` for server-only build machines.

The 1.23b client depends on the legacy D3DX9 helper DLLs. For a manually managed Linux prefix, install them into the same prefix Echo Gate launches:

```sh
export WINEPREFIX=/path/to/ffxiv-prefix
winetricks -q d3dx9_41
```

In a WoW64 prefix, the 32-bit helper DLL should normally live under:

```text
$WINEPREFIX/drive_c/windows/syswow64/d3dx9_41.dll
```

If the game only finds `d3dx9_41.dll` after copying it beside `ffxivgame.exe`, the DLL itself is probably valid, but the launch path is using a different prefix or DLL lookup context than the one that was prepared.

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

Managed runtime artifact schema:

```text
name = "Echo Gate Wine"
version = "1.0"
platform_rid = "osx-arm64"
runtime_kind = "wine"
archive_url = "https://cdn.example.test/runtimes/echo-gate-wine-osx-arm64.zip"
archive_format = "zip"
size_bytes = 123456789
sha256 = "HEX_SHA256"
executable_relative_path = "bin/wine"
prefix_arch = "win64"
environment_json = {"WINEDEBUG":"-all"}
is_default = true
is_active = true
sort_order = 10
```

Custom runtime profile example:

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

1. Runtime executes the included `win-x86` launch helper probe.
2. CrossOver, managed Wine, or selected Wine profile starts `ffxivgame.exe`.
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
- helper log path
- Wine stdout/stderr log path

## Echo Gate Automation Scope

- Create or select Wine prefix.
- Validate required runtime files.
- Download managed runtime archive from service-catalog static URL.
- Validate managed runtime archive size and SHA256.
- Extract managed runtime zip archives with path traversal protection.
- Write legacy-compatible `Servers.xml`.
- Check local Meteor services.
- Prepare `Data/staticactors.bin`.
- Create accounts through the launcher service.
- Sign in through the launcher service.
- Launch the included helper through Wine.
- Collect logs.

## Echo Gate Exclusions

- Runtime archives in Git.
- CrossOver, Whisky, or commercial runtime redistribution.
- Account/password storage.
- Client modification.
- Plugin injection.

## References

- XIV on Mac: https://www.xivmac.com/about-uscredits
- XIV on Mac source: https://github.com/marzent/XIV-on-Mac
- Homebrew XIV on Mac cask: https://formulae.brew.sh/cask/xiv-on-mac
- CodeWeavers / FFXIV Mac partnership: https://www.codeweavers.com/about/news/press/20190628
- MeteorXIV Core client guide: https://wiki.ffxivrp.org/pages/ClientAndLauncher
