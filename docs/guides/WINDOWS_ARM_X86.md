# Windows ARM/x86 Setup

Windows can run the legacy client directly. Windows ARM64 can build Echo Gate, but x86 client emulation still needs validation.

## 1. Install Tools

Install:

- Visual Studio or MSBuild
- .NET Framework 4.7.2 Developer Pack
- NuGet
- MariaDB
- PHP
- .NET 10 SDK

## 2. Restore And Build Servers

```bat
nuget restore MeteorXIV.Core.sln
msbuild MeteorXIV.Core.sln /p:Configuration=Release
```

## 3. Prepare The Database

Create/import the same local database used by macOS and Linux:

```text
database: ffxiv_server
username: meteor
password: meteor_dev
hosts: localhost, 127.0.0.1
```

The SQL files live under:

```text
Data/sql/
```

The cross-platform helper is written for Unix shells, so native Windows setup may still require manual MariaDB import until a Windows database helper is added.

## 4. Prepare Runtime Data

The Map Server needs `staticactors.bin` in the runtime data folder. Prepare it from your local user-owned FFXIV 1.x client folder and copy it into:

```text
Data/staticactors.bin
Map Server\bin\Release\staticactors.bin
```

Also make sure these files/folders are present beside the server executables:

```text
Lobby Server\bin\Release\lobby_config.ini
World Server\bin\Release\world_config.ini
Map Server\bin\Release\map_config.ini
Map Server\bin\Release\scripts\
```

## 5. Start Local Services

Start PHP for the launcher service, then run the servers in this order:

```bat
"Lobby Server\bin\Release\MeteorXIV.Core.Lobby.exe"
"Map Server\bin\Release\MeteorXIV.Core.Map.exe"
"World Server\bin\Release\MeteorXIV.Core.World.exe"
```

Default local ports:

```text
launcher HTTP: 8080
lobby server: 54994
world server: 54992
map server: 1989
```

## 6. Build Echo Gate

Publish all launcher targets from a machine with the .NET 10 SDK:

```sh
./tools/build-echo-gate-all.sh
```

Windows targets:

```text
win-x86
win-x64
win-arm64
```

## 7. Configure Echo Gate

- Server tab: set launcher service to `http://127.0.0.1:8080/launcher`.
- Client tab: select your local FFXIV 1.23b client folder.
- Runtime tab: Windows uses direct launch and does not need Wine.
- Home tab: create an account, log in, and launch.

![Echo Gate server tab](../../server.png)

## Windows ARM64 Note

Echo Gate has a `win-arm64` publish target. The legacy client is still a 32-bit Windows program, so real client launch behavior on Windows ARM64 needs validation.
