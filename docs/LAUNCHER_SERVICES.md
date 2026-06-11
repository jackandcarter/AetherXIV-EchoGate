# Echo Gate Launcher Services

## Purpose

Echo Gate uses launcher services for server status, news, patch manifests, runtime catalogs, and account/session metadata. Game protocol services remain in Lobby, World, and Map servers. Launcher services are HTTP endpoints backed by MariaDB and served through the existing web host or VPS PHP stack.

Large binary payloads, including patch files and managed runtime archives, should be hosted on static storage or a web host. The launcher service stores URLs and integrity metadata; it does not stream large payloads through PHP. Client installers and client files are not part of the launcher service contract.

## Endpoint Contract

Base path:

```text
/launcher
```

Endpoints:

```text
GET /launcher/config
GET /launcher/status
GET /launcher/news
GET /launcher/patch-manifest
GET /launcher/runtime-catalog?platform=<rid>
POST /launcher/login
POST /launcher/create-account
```

`/launcher/config` returns service URLs and target versions. The response includes `patch_manifest_url`, `runtime_catalog_url`, `login_url`, `account_create_url`, `client_login_url`, and `patch_base_url`.

`/launcher/status` returns the public launcher status string used by the Home tab.

`/launcher/news` returns published news items ordered by `sort_order`, then `published_at`.

`/launcher/patch-manifest` returns individual patch files with relative paths, byte sizes, CRC32 values, and optional SHA256 values. Echo Gate downloads each file into the same local folder structure used by manual patching and validates every file before applying patches.

`/launcher/runtime-catalog?platform=<rid>` returns active managed runtime archives for the requested platform RID. Echo Gate selects the default active row first, downloads the referenced archive from static hosting, validates byte size and SHA256, extracts it under app data, and creates a managed Wine prefix.

`/launcher/login` accepts JSON or form POST data with `username` and `password`. It verifies the account against the existing `users` table, refreshes or creates a row in `sessions`, and returns a 56-character `session_id`.

`/launcher/create-account` accepts JSON or form POST data with `username`, `password`, `confirm_password`, and `email`. It creates a compatible Meteor account using the existing SHA-224 plus salt scheme, creates a session, and returns the same response shape as `/launcher/login`.

Auth response:

```json
{
  "success": true,
  "message": "Login accepted.",
  "username": "tester",
  "session_id": "56_CHARACTER_SESSION_ID"
}
```

Runtime catalog response:

```json
{
  "platform": "osx-arm64",
  "artifacts": [
    {
      "name": "Echo Gate Wine",
      "version": "1.0",
      "platform_rid": "osx-arm64",
      "runtime_kind": "wine",
      "archive_url": "https://cdn.example.test/runtimes/echo-gate-wine-osx-arm64.zip",
      "archive_format": "zip",
      "size_bytes": 123456789,
      "sha256": "HEX_SHA256",
      "executable_relative_path": "bin/wine",
      "prefix_arch": "win64",
      "environment": {
        "WINEDEBUG": "-all"
      },
      "is_default": true,
      "is_active": true,
      "sort_order": 10
    }
  ]
}
```

## Database

Apply:

```text
Data/sql/launcher_services.sql
```

Tables:

- `launcher_config`
- `launcher_news`
- `launcher_patch_files`
- `launcher_runtime_artifacts`

The `patch_base_url` config value should point at the remote folder containing the `ffxiv` or `ffxiv_patches` layout. For example:

```text
https://cdn.example.test/ffxiv_patches
```

Patch manifest rows should use relative paths like:

```text
ffxiv/48eca647/patch/D2012.09.19.0001.patch
```

Runtime artifact rows should use static archive URLs and immutable integrity metadata. Required fields:

- `name`
- `version`
- `platform_rid`
- `runtime_kind`
- `archive_url`
- `archive_format`
- `size_bytes`
- `sha256`
- `executable_relative_path`
- `prefix_arch`
- `environment_json`
- `is_default`
- `is_active`
- `sort_order`

Supported v1 archive format:

```text
zip
```

Runtime binaries, runtime archives, downloaded runtime caches, Wine prefixes, and client files are not stored in Git.

Launcher config values for auth and client launch:

- `login_url`: launcher JSON login endpoint, default `login`
- `account_create_url`: launcher JSON account creation endpoint, default `create-account`
- `client_login_url`: legacy client-facing login URL written into `Servers.xml`, default `../login/index.php`

## Launcher Behavior

On startup, Echo Gate reads the saved local profile, then attempts to call the launcher service. If service calls fail, the launcher remains usable for manual client path selection, manual patching, detected runtimes, and custom runtimes.

If the client path is missing, the Home tab exposes client location. After a client path is selected and saved, client selection moves to the Client tab.

If the client is valid and reports `game.ver` `2012.09.19.0001`, the `Log In & Play` button is enabled. The button signs in through `/launcher/login` when no current session exists, then launches the client with the returned session id.

Echo Gate writes a legacy-compatible `Servers.xml` under app data:

```xml
<Servers>
  <Server Name="Local MeteorXIV Core" Address="127.0.0.1" LoginUrl="http://127.0.0.1:8080/login/index.php" />
</Servers>
```

The macOS/Linux launch path runs the included `EchoGate.ClientLauncher.exe` helper through the selected Wine runtime. Windows builds run the same helper natively. The helper generates the 1.23b `sqex0002...!////` launch argument, starts the user-selected local `ffxivgame.exe` suspended, applies the verified 1.23b lobby host and CPU-thread patches, then resumes the game process.

Windows builds launch the client natively. macOS and Linux builds default to `Automatic Managed` runtime mode. Runtime priority is:

1. active service-catalog managed Wine artifact
2. detected free Wine or XIV on Mac Wine runtime
3. detected CrossOver or Whisky runtime
4. custom runtime profile

Managed runtime app-data paths:

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
