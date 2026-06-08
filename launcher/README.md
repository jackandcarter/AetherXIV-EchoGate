# Echo Gate

This folder contains Echo Gate, the cross-platform launcher/client runner for Meteor.

Initial launcher goals:

- validate a local 1.23b client folder
- classify base, patched, and unknown client version states
- validate a user-provided 1.x patch library by path, expected size, and CRC32 metadata
- prepare local client-derived runtime data for the server
- manage local/private server profiles
- configure Seventh Umbral Launcher compatibility files
- detect available Wine/Whisky runtime tools
- launch through CrossOver/Wine on macOS and Linux
- launch directly on Windows
- collect client launch logs and server test notes

Current project:

- `EchoGate/EchoGate.sln`
- `EchoGate/EchoGate.Core`
- `EchoGate/EchoGate.App`
- `EchoGate/EchoGate.Tests`

Design docs:

- `../docs/LAUNCHER_DESIGN.md`
- `../docs/WINE_RUNTIME_STRATEGY.md`

Repository policy: client files, patch files, and patch torrent/metainfo files are excluded from version control. Echo Gate treats patch payload acquisition as a user-selected local input, not a bundled launcher asset.
