# macOS Apple Silicon Setup

This is the main macOS path. Intel Macs can use the same flow, with the Intel build note near the end.

## 1. Install Tools

Install the local development tools you need:

- Homebrew
- MariaDB
- PHP
- Mono/MSBuild
- NuGet
- .NET 10 SDK
- A Wine-compatible runtime such as Homebrew Wine, Whisky, CrossOver, or another local runtime you already use

## 2. Prepare The Database

From the repository root:

```sh
./tools/setup-local-db.sh
```

Defaults:

```text
database: ffxiv_server
username: meteor
password: meteor_dev
hosts: localhost, 127.0.0.1
```

The script asks for MariaDB admin credentials only if it cannot create/import the database with the current local login.

## 3. Build The Legacy Servers

```sh
./tools/build-legacy.sh
```

## 4. Prepare Runtime Data

If Echo Gate already knows your client folder, this can run without extra input:

```sh
CONFIGURATION=Release ./tools/copy-runtime-data.sh
```

If the helper cannot find the client folder, provide the path to your local user-owned FFXIV 1.x client folder:

```sh
CLIENT_DIR="/path/to/FINAL FANTASY XIV" ./tools/prepare-client-data.sh
CONFIGURATION=Release ./tools/copy-runtime-data.sh
```

This prepares files the server expects, including `Data/staticactors.bin`.

## 5. Start Local Services

Use separate terminal tabs or windows:

```sh
./tools/run-web.sh
./tools/run-lobby.sh
./tools/run-map.sh
./tools/run-world.sh
```

Default local ports:

```text
launcher HTTP: 8080
lobby server: 54994
world server: 54992
map server: 1989
```

## 6. Build Echo Gate

```sh
./tools/build-echo-gate-macos.sh
```

The Apple Silicon app is published under:

```text
build/echo-gate/macos-osx-arm64/Echo Gate.app
```

## 7. Configure Echo Gate

Server tab:

![Echo Gate server tab](../../server.png)

- Launcher service: `http://127.0.0.1:8080/launcher`
- Server host: `127.0.0.1`
- Lobby port: `54994`
- World port: `54992`

Client tab:

- Select your local FFXIV 1.23b client folder.
- Validate the client.
- Select and validate any local patch library you already have.

Runtime tab:

![Echo Gate runtime tab](../../Runtime.png)

- Use a detected runtime when possible.
- Use custom runtime mode if your Wine executable or prefix lives somewhere Echo Gate did not detect.
- Validate the runtime before launching.

## 8. Log In And Play

Home tab:

![Echo Gate home tab](../../Home.png)

- Create an account if needed.
- Log in.
- Use `Log In & Play`.

If the client crashes or exits without a useful terminal message:

```sh
./tools/collect-echo-gate-logs.sh --files 10 --lines 160
```

## Intel Mac Note

On Intel macOS, build the Intel app target:

```sh
RUNTIME_IDENTIFIER=osx-x64 ./tools/build-echo-gate-macos.sh
```

The rest of the setup is the same, but Intel macOS still needs more user reports than Apple Silicon.
