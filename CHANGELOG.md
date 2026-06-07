# Changelog

All notable project changes should be recorded here after they are confirmed.
Use concise entries and link to issues, pull requests, packet captures, or test notes when possible.

## Unreleased

- Set up repository hygiene for macOS/Linux development auditing.
- Added macOS/Linux setup docs, audit notes, and modern .NET porting strategy.
- Added cross-platform helper scripts for environment audit, legacy build, DB import, DB user creation, and runtime data copy.
- Imported the local `ffxiv_server` database and configured a local `meteor` / `meteor_dev` DB user.
- Fixed non-Windows legacy builds by keeping `Microsoft.Net.Compilers` imports Windows-only.
- Added future Echo Gate launcher architecture and Wine/CrossOver runtime strategy docs.
- Added `tools/prepare-client-data.sh` for preparing local `staticactors.bin` from a user-owned 1.23b client install.
