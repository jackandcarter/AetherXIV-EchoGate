# Echo Gate Launcher Design

## Purpose

Echo Gate should become the cross-platform client runner for a legally owned Final Fantasy XIV 1.23b install.

Its job is not to replace the server. Its job is to make client validation repeatable on macOS, Linux, and Windows:

- find and validate a local 1.23b client install
- manage local server profiles
- configure the legacy launcher/client connection path
- prepare local server runtime data derived from the client
- start the client through the best available platform runtime
- capture logs and launch diagnostics for server development

## Target Client

- Final Fantasy XIV 1.23b / `2012.09.19.0001`
- Original 1.x Windows client
- Not A Realm Reborn
- Not current retail FFXIV
- Not the current official macOS client

The app must never download, bundle, commit, or redistribute game client files.

## Design Principles

- Treat the client install as user-owned local data.
- Keep server address/profile management explicit and visible.
- Prefer boring, inspectable launch steps over opaque magic.
- Keep Wine/CrossOver runtime selection pluggable.
- Preserve logs for every launch attempt.
- Make macOS/Linux testing first-class, because that is our local development path.
- Keep Windows support simple: direct process launch without Wine.

## MVP Scope

The first launcher should be a developer tool, not a polished public launcher.

MVP features:

- Client folder picker/path setting.
- Version/file validation checklist.
- Server profile editor:
  - profile name
  - lobby address
  - login URL
  - notes
- `servers.xml` writer for Seventh Umbral Launcher compatibility.
- `staticactors.bin` preparation from:

```text
client/script/rq9q1797qvs.san
```

- Launch modes:
  - Windows: launch the configured `.exe` directly.
  - macOS: launch through selected Wine/CrossOver wrapper.
  - Linux: launch through selected Wine/Proton wrapper.
- Launch log viewer.
- Basic troubleshooting checks:
  - server ports reachable
  - PHP login URL reachable
  - client path present
  - Wine runtime present

## Non-Goals For MVP

- Do not distribute a client.
- Do not implement patch downloading until the legal/source story is clear.
- Do not implement plugin/mod support.
- Do not inject code into the client.
- Do not add account management beyond linking to the local PHP `create_user.php` flow.
- Do not build a fancy UI before the launch path works.

## Candidate App Stack

Best first choice: Tauri.

Reasons:

- small cross-platform desktop app
- Rust backend is good for process launching, paths, and file validation
- frontend can stay simple
- builds on macOS, Linux, and Windows
- does not force us into the legacy server runtime

Alternative: Avalonia/.NET.

Reasons:

- fits the planned .NET modernization
- good cross-platform UI
- easier sharing of future config models with server tooling

Recommendation: start with Tauri if the launcher is mostly process/runtime orchestration. Use Avalonia if we want deeper shared .NET tooling later.

## Proposed Repository Layout

```text
launcher/
  README.md
  app/
    # future Tauri or Avalonia project
  docs/
    # launcher-specific notes and screenshots
  scripts/
    # runtime probes and packaging helpers
```

Until the client exists locally, keep this folder documentation-first.

## Runtime Model

Echo Gate should model the launch path as data:

```text
ClientInstall
  path
  detectedVersion
  staticActorsSource
  executableCandidates

ServerProfile
  name
  lobbyAddress
  loginUrl
  notes

RuntimeProfile
  platform
  runtimeKind
  runtimePath
  winePrefix
  dxLayer
  environment
```

This lets us test the same server profile under different launch runtimes without changing server code.

## Launch Flow

1. Validate local client path.
2. Validate local server profile.
3. Check login URL.
4. Check lobby/world/map ports if requested.
5. Prepare or validate `Data/staticactors.bin`.
6. Write/update launcher server configuration.
7. Start the selected Wine/CrossOver/Windows runtime.
8. Stream launcher logs into a local log folder.
9. Record outcome:
   - launched
   - login reached
   - world selected
   - map loaded
   - failed with reason

## Development Milestones

### Milestone 0: Manual Client Runtime

- Get the 1.23b client launching manually through CrossOver/Wine or Windows.
- Confirm which executable and arguments are required.
- Confirm whether Seventh Umbral Launcher is required or can be replaced.
- Record working Wine/runtime settings.

### Milestone 1: CLI Helper

- Add a small CLI that validates a client path.
- Generate/update `servers.xml`.
- Copy `staticactors.bin`.
- Launch through a user-supplied command.

### Milestone 2: Desktop Shell

- Add folder picker.
- Add server profile editor.
- Add runtime selector.
- Add launch log viewer.

### Milestone 3: Runtime Manager

- Support CrossOver bottle selection on macOS.
- Support Wine prefix selection on Linux.
- Add runtime probes and diagnostics.

### Milestone 4: Integrated Dev Loop

- Start or check local Meteor services.
- Launch client.
- Show server logs and client launch logs side by side.
- Save packet-test session notes for `CLIENT_REQUIREMENTS.md`.

## Open Questions

- Which exact 1.23b executable should be launched after Seventh Umbral setup?
- Does the client require launcher-generated arguments or tokens?
- Can Seventh Umbral Launcher run cleanly under modern Wine/CrossOver?
- Which renderer path is best for 1.x DirectX 9 on Apple Silicon: DXVK/MoltenVK, WineD3D, or a CrossOver-specific layer?
- Are any registry keys required by the 1.x client?
- Where does the client write config and logs under Wine?

## References

- Project Meteor client guide: https://wiki.ffxivrp.org/pages/ClientAndLauncher
- XIV on Mac: https://www.xivmac.com/about-uscredits
- XIV on Mac source: https://github.com/marzent/XIV-on-Mac
- FFXIVQuickLauncher source: https://github.com/goatcorp/FFXIVQuickLauncher
- CodeWeavers / FFXIV Mac partnership: https://www.codeweavers.com/about/news/press/20190628
