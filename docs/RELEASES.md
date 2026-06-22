# Release Builds

MeteorXIV has two practical build paths:

- GitHub build artifacts: useful when someone wants GitHub to build downloadable files in the browser.
- Local platform builds: useful when someone wants to verify the full setup on the same machine that will run the launcher, servers, database, Wine prefix, and local client files.

The GitHub path is an added option. It does not replace the platform guides.

## GitHub Build Artifacts

The `Build and release artifacts` workflow can be run from the GitHub Actions page. A manual run builds downloadable artifacts and stores them on the workflow run page.

Use this for:

- quick tester downloads
- CI smoke coverage for packaging
- repeatable launcher archives for each platform
- a server core package that contains built server outputs plus SQL/PHP/config/docs

GitHub artifacts are temporary workflow outputs unless they are attached to a GitHub Release.

## GitHub Releases

When the workflow is run from a release tag, or when `publish_release` is enabled in a manual run, it creates or updates a GitHub Release and attaches the built files.

Expected release assets:

```text
EchoGate-linux-x64-v1.2.tar.gz
EchoGate-linux-arm64-v1.2.tar.gz
EchoGate-osx-arm64-v1.2.zip
EchoGate-osx-x64-v1.2.zip
EchoGate-win-x86-v1.2.zip
EchoGate-win-x64-v1.2.zip
EchoGate-win-arm64-v1.2.zip
MeteorXIV-Server-Core-v1.2.tar.gz
```

The recommended release tag is:

```text
release-v1.2
```

That avoids ambiguity with branch names while still naming the release `v1.2`.

## Local Platform Builds

Local builds are still the more complete workflow because they exercise your actual machine:

- MariaDB install and local admin credentials
- PHP launcher service
- local `.env.defaults` / `.env.local` values
- local FFXIV 1.x client folder
- local patch library
- local `staticactors.bin` preparation
- approved Wine runtime, explicit custom runtime, or Windows direct launch path

Use the platform guides when setting up a machine for real playtests:

- [macOS Apple Silicon](guides/MACOS_APPLE_SILICON.md)
- [Linux ARM/x86](guides/LINUX_ARM_X86.md)
- [Windows ARM/x86](guides/WINDOWS_ARM_X86.md)
- [Steam Deck experimental](guides/STEAM_DECK_EXPERIMENTAL.md)

## macOS Gatekeeper Notes

GitHub-built macOS apps are ad-hoc signed by the build script but are not Apple-notarized. macOS may warn that the app cannot be opened because it was downloaded from the internet or from an unidentified developer.

Common tester options:

- Open the app with Control-click, then choose Open.
- Allow the app from System Settings when macOS shows a blocked-app notice.
- Build locally on your Mac if you want to avoid using the GitHub-built app bundle.

Future release work can add Developer ID signing and notarization, but that requires Apple developer credentials and should stay outside normal public CI secrets until the release process is ready.

## What Release Packages Do Not Include

Release packages do not include:

- FFXIV client installers
- client files
- patch payloads
- patch torrents or metainfo files
- Wine runtime archives
- `Data/staticactors.bin`

`staticactors.bin` must be prepared locally from a user-owned FFXIV 1.x client folder.
