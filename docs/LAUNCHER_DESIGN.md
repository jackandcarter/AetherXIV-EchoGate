# Echo Gate Launcher Specification

## Product Role

Echo Gate is the cross-platform client runner for a user-owned Final Fantasy XIV 1.23b installation.

Primary responsibilities:

- validate a local 1.23b client installation
- manage local and private server profiles
- configure the legacy launcher/client connection path
- prepare local server runtime data derived from the client installation
- start the client through a platform-specific runtime
- collect launch diagnostics for server validation

Echo Gate is a launcher and runtime orchestrator. Server emulation, database migration, game-content scripting, and protocol implementation remain part of the Meteor server stack.

## Target Client

- Final Fantasy XIV 1.23b / `2012.09.19.0001`
- Original 1.x Windows client
- A Realm Reborn and current retail clients are outside scope.
- The current official macOS client is outside scope.

## Asset Policy

Client files are user-owned local data.

Repository exclusions:

- game client files
- extracted client assets
- patch files
- packet captures containing credentials or private account data
- local secrets

Echo Gate may read local client files for validation and setup. Echo Gate must not distribute, bundle, commit, or publish client assets.

## Design Laws

- Client installation state is explicit and user-controlled.
- Server profile state is explicit and inspectable.
- Runtime selection is pluggable across Windows, macOS, and Linux.
- Launch steps are logged as discrete events.
- macOS and Linux are first-class development targets.
- Windows launch support remains direct process execution.
- Client-derived server data is generated locally and excluded from version control.
- Runtime behavior is recorded through reproducible profiles rather than hidden application state.

## MVP Functional Requirements

- Client folder picker or path setting.
- Client validation checklist.
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
  - Windows: direct executable launch
  - macOS: Wine/CrossOver runtime launch
  - Linux: Wine/Proton runtime launch
- Launch log viewer.
- Runtime checks:
  - server ports reachable
  - PHP login URL reachable
  - client path present
  - selected runtime present
  - generated `Data/staticactors.bin` present

## MVP Out Of Scope

- Client download.
- Patch download.
- Client redistribution.
- Plugin or mod support.
- Client code injection.
- Credential storage.
- Account management beyond linking to the local PHP `create_user.php` flow.
- Public launcher polish before the launch path is reproducible.

## Candidate App Stack

### Primary Candidate: Tauri

Rationale:

- small cross-platform desktop application footprint
- Rust backend suitable for process launching, paths, filesystem checks, and runtime probes
- simple frontend surface for the first version
- macOS, Linux, and Windows build support
- independent of the legacy server runtime

### Alternative Candidate: Avalonia/.NET

Rationale:

- alignment with the planned .NET server modernization
- cross-platform UI support
- potential shared configuration models with server tooling

### Initial Decision

Use Tauri for the first launcher implementation unless the .NET server port creates a clear need for shared launcher/server libraries.

## Repository Layout

```text
launcher/
  README.md
  app/
    # Tauri or Avalonia project
  docs/
    # launcher-specific notes and screenshots
  scripts/
    # runtime probes and packaging helpers
```

## Runtime Model

Echo Gate models the launch path as data:

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

The same server profile must be testable through multiple runtime profiles.

## Launch Procedure

1. Validate local client path.
2. Validate selected server profile.
3. Check login URL.
4. Check configured server ports.
5. Prepare or validate `Data/staticactors.bin`.
6. Write or update launcher server configuration.
7. Start the selected Windows/Wine/CrossOver/Proton runtime.
8. Stream launch logs into a local log folder.
9. Record launch outcome:
   - launched
   - login reached
   - world selected
   - map loaded
   - failed with reason

## Development Milestones

### Milestone 0: Runtime Characterization

- Launch 1.23b through the selected runtime.
- Record executable path and launch arguments.
- Record Seventh Umbral Launcher requirements.
- Record working runtime settings.

### Milestone 1: CLI Helper

- Validate a client path.
- Generate or update `servers.xml`.
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

### Milestone 4: Integrated Development Loop

- Check local Meteor service readiness.
- Launch client.
- Show server logs and client launch logs side by side.
- Save packet-test session notes for `CLIENT_REQUIREMENTS.md`.

## Research Items

- Exact 1.23b executable after Seventh Umbral setup.
- Required launcher-generated arguments or tokens.
- Seventh Umbral Launcher behavior under modern Wine/CrossOver.
- Best renderer path for 1.x DirectX 9 on Apple Silicon.
- Required registry keys.
- Client configuration and log paths under Wine.

## References

- Project Meteor client guide: https://wiki.ffxivrp.org/pages/ClientAndLauncher
- XIV on Mac: https://www.xivmac.com/about-uscredits
- XIV on Mac source: https://github.com/marzent/XIV-on-Mac
- FFXIVQuickLauncher source: https://github.com/goatcorp/FFXIVQuickLauncher
- CodeWeavers / FFXIV Mac partnership: https://www.codeweavers.com/about/news/press/20190628
