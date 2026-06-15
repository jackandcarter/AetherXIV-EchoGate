# Echo Gate

This folder contains Echo Gate, the cross-platform launcher/client runner for MeteorXIV Core.

Current launcher features:

- validate a local 1.23b client folder
- validate and apply user-provided 1.x patch metadata/payloads
- prepare local client-derived runtime data for the server
- manage local/private server profiles
- call launcher services for status, news, login, and account creation
- detect Wine-compatible runtimes on macOS and Linux
- support custom runtime paths and prefixes
- launch through Wine-compatible runtimes on macOS and Linux
- launch directly on Windows
- collect client launch and runtime validation logs

Current project folders:

- `EchoGate/EchoGate.sln`
- `EchoGate/EchoGate.Core`
- `EchoGate/EchoGate.App`
- `EchoGate/EchoGate.Tests`

Docs:

- `../docs/ECHO_GATE_FEATURES.md`
- `../docs/SUPPORTED_PLATFORMS.md`
- `../docs/guides/MACOS_APPLE_SILICON.md`
- `../docs/guides/LINUX_ARM_X86.md`
- `../docs/guides/WINDOWS_ARM_X86.md`
- `../docs/guides/STEAM_DECK_EXPERIMENTAL.md`

Repository policy: client files, patch files, and patch torrent/metainfo files are excluded from version control. Echo Gate treats patch payload acquisition as a user-selected local input, not a bundled launcher asset.
