# Wine Runtime Strategy For FFXIV 1.x

## Summary

Using a Wine/CrossOver-style runtime is feasible and likely necessary for macOS development. This mirrors the broad idea behind the modern FFXIV Mac ecosystem, but the 1.x client is a different target:

- older Windows client
- likely 32-bit process behavior
- DirectX 9 era rendering
- 1.23b server protocol
- Seventh Umbral Launcher compatibility path

So we should borrow the runtime-management concepts from modern XIV launchers, not their ARR-specific login, patch, plugin, or DirectX 11 assumptions.

## Runtime Goals

- Run the original Windows 1.23b client on Apple Silicon macOS.
- Run the same client on Linux through Wine/Proton where possible.
- Keep Windows support as direct launch.
- Use one server-profile model across all platforms.
- Keep runtime setup reproducible enough for contributors.

## macOS Runtime Options

### CrossOver

Most likely first choice on Apple Silicon.

Pros:

- maintained Wine distribution
- strong macOS support
- handles many older Windows apps on modern macOS
- practical support for 32-bit Windows applications through Wine/CrossOver compatibility layers

Cons:

- commercial
- exact bottle setup must be documented
- runtime is not redistributable as our own bundled dependency

### XIV on Mac Concepts

XIV on Mac is aimed at current FFXIV, not 1.x, but its architecture is useful:

- native macOS launcher
- managed Wine prefix
- optimized Wine build
- launch diagnostics
- runtime update handling
- user-friendly install folder/log access

For Echo Gate, we should apply those ideas to 1.x without assuming modern FFXIV boot files, Dalamud, or current-game authentication.

### Wineskin / WineCX / Other Wine Builds

Useful for experiments and possibly a free runtime path.

Risks:

- compatibility changes between Wine builds
- 32-bit Windows behavior on modern macOS can be brittle
- contributors may end up with inconsistent prefixes

## Linux Runtime Options

### Wine

Good baseline for Linux.

The launcher should support:

- custom `wine` binary path
- custom `WINEPREFIX`
- environment overrides
- launch logs

### Proton

Useful if the client behaves better with Proton.

The launcher should not require Steam, but it can support a user-provided Proton path later.

## Graphics Translation

The 1.x client is DirectX 9 era, while modern XIV-on-Mac work is focused on newer FFXIV and newer graphics paths.

Things to test:

- WineD3D
- DXVK for DirectX 9 through Vulkan/MoltenVK
- CrossOver-specific DirectX translation layers

Avoid assuming DXMT/D3DMetal is the answer for 1.x. Those concepts may help, but the 1.x client needs direct testing.

## Runtime Profile Fields

Echo Gate should eventually store runtime profiles like:

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

No secrets or client files should be stored in repo.

## First Manual Test Matrix

Once a client exists locally, test in this order:

1. CrossOver bottle launches Seventh Umbral Launcher.
2. Seventh Umbral Launcher sees the 1.23b client path.
3. Server profile points to local Meteor PHP/lobby.
4. Client reaches login.
5. Client reaches character select.
6. Client transitions to World Server.
7. Client reaches Map Server.

Record each run with:

- runtime name and version
- macOS/Linux version
- client version
- renderer path
- exact executable launched
- launch arguments
- observed failure or success

## What The Launcher Should Automate Later

- Create or select Wine prefix.
- Validate required runtime files.
- Write `servers.xml`.
- Check local Meteor services.
- Prepare `Data/staticactors.bin`.
- Open the local account creation URL.
- Launch Seventh Umbral Launcher or the game executable.
- Collect logs.

## What It Should Not Automate Yet

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
