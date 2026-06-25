# Echo Gate

This folder contains Echo Gate, the cross-platform launcher/client runner for AetherXIV Core.

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

Launcher service admin:

- News posts are stored in the `launcher_news` MariaDB table and served through `/launcher/news`.
- Local admins can edit launcher news through `/launcher/admin-news` or `/launcher/admin/news/`.
- The admin tool reuses `Data/www/launcher/config.php`, so it uses the same `METEOR_DB_*` environment variables and `config.local.php` database credentials as the launcher service.
- Localhost access is allowed by default. For hosted or remote access, set `METEOR_LAUNCHER_ADMIN_PASSWORD`, `METEOR_LAUNCHER_ADMIN_PASSWORD_HASH`, or the matching `$launcher_admin_password` / `$launcher_admin_password_hash` value in `config.local.php`.

Repository policy: client files, patch files, and patch torrent/metainfo files are excluded from version control. Echo Gate treats patch payload acquisition as a user-selected local input, not a bundled launcher asset.
