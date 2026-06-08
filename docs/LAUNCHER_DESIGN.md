# Echo Gate Launcher Specification

## Product Role

Echo Gate is the cross-platform client runner for a user-owned Final Fantasy XIV 1.23b installation.

Primary responsibilities:

- validate a local 1.23b client installation
- classify base, patched, and unknown client version states
- validate a user-provided 1.x patch library
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
- patch torrent/metainfo files
- packet captures containing credentials or private account data
- local secrets

Echo Gate may read local client files for validation and setup. Echo Gate must not distribute, bundle, commit, or publish client assets.

## Patch Source Policy

Patch files and torrent/metainfo files are user-provided local artifacts.

Echo Gate must not:

- bundle patch payloads
- commit patch payloads
- publish patch payloads
- require an unofficial mirror as project infrastructure
- download patch payloads without an explicit user-selected source and validation plan

Echo Gate may:

- identify the known 1.x patch chain by filename
- validate local patch files by expected size and CRC32
- validate local torrent/metainfo files by expected path
- apply patches to a backup or controlled copy after a patcher implementation is available

Remote acquisition, torrent downloading, and mirror configuration are future patcher features. Those features require source selection, progress reporting, integrity validation, and failure rollback.

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
- Client readiness states:
  - missing
  - base install
  - patch required
  - 1.23b ready
  - unknown version
- Patch library validator:
  - expects one boot patch and 51 game patches from the known 1.x chain
  - validates patch files by path and expected byte size
  - supports CRC32 verification in the core patch-library model
  - validates torrent metainfo files by path
  - reports missing entries without downloading or applying patches
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
- Torrent download.
- Patch application.
- Client redistribution.
- Plugin or mod support.
- Client code injection.
- Credential storage.
- Account management beyond linking to the local PHP `create_user.php` flow.
- Public launcher polish before the launch path is reproducible.

## App Stack

### Selected Stack: Avalonia/.NET

Rationale:

- alignment with the planned .NET server modernization
- cross-platform UI support
- shared configuration and launch models with server tooling
- native desktop application structure for Windows, macOS, and Linux
- UI-first launcher flow without terminal-based launcher operation

## Repository Layout

```text
launcher/
  README.md
  EchoGate/
    EchoGate.sln
    EchoGate.Core/
    EchoGate.App/
    EchoGate.Tests/
```

## Runtime Model

Echo Gate models the launch path as data:

```text
ClientInstall
  path
  bootVersion
  gameVersion
  readiness
  staticActorsSource
  executableCandidates

PatchLibrary
  rootPath
  expectedPatchCount
  presentPatchCount
  presentMetainfoCount
  invalidPatchCount
  expectedSizeBytes
  expectedCrc32
  missingPatchEntries
  missingMetainfoEntries
  invalidPatchEntries

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
2. Classify client version state.
3. Validate user-provided patch library when selected.
4. Validate selected server profile.
5. Check login URL.
6. Check configured server ports.
7. Prepare or validate `Data/staticactors.bin`.
8. Write or update launcher server configuration.
9. Start the selected Windows/Wine/CrossOver/Proton runtime.
10. Stream launch logs into a local log folder.
11. Record launch outcome:
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

### Milestone 1: Desktop Shell

- Add folder picker.
- Add version/readiness status rows.
- Add patch library validation.
- Add server profile editor.
- Add runtime selector.
- Add launch log viewer.

### Milestone 2: Runtime Manager

- Support CrossOver bottle selection on macOS.
- Support Wine prefix selection on Linux.
- Add runtime probes and diagnostics.

### Milestone 3: Integrated Development Loop

- Check local Meteor service readiness.
- Launch client.
- Show server logs and client launch logs side by side.
- Save packet-test session notes for `CLIENT_REQUIREMENTS.md`.

## Research Items

- Exact 1.23b executable after Seventh Umbral setup.
- Required launcher-generated arguments or tokens for `ffxivgame.exe`.
- Seventh Umbral Launcher behavior under modern Wine/CrossOver.
- Best renderer path for 1.x DirectX 9 on Apple Silicon.
- Required registry keys.
- Client configuration and log paths under Wine.

## References

- Project Meteor client guide: https://wiki.ffxivrp.org/pages/ClientAndLauncher
- Project Novum client patching: https://project-novum.github.io/game-patching/patching/
- Project Novum ZiPatch format: https://project-novum.github.io/game-patching/zipatch/
- XIV Dev Wiki ZiPatch: https://xiv.dev/data-files/zipatch
- Seventh Umbral patch manifest reference: https://github.com/jpd002/SeventhUmbral/blob/eead5fef6a2e5db9ffd82e9377bea72b23bf58af/launcher/PatcherWindow.cpp
- Meteor launcher patch manifest reference: https://github.com/ThiconZ/FFXIV-Meteor-Launcher/blob/d770d4132e7d550aa89d6617484933ac5b6a0244/FFXIV%20Meteor%20Launcher/PatchData.cs
- XIV on Mac: https://www.xivmac.com/about-uscredits
- XIV on Mac source: https://github.com/marzent/XIV-on-Mac
- FFXIVQuickLauncher source: https://github.com/goatcorp/FFXIVQuickLauncher
- CodeWeavers / FFXIV Mac partnership: https://www.codeweavers.com/about/news/press/20190628
