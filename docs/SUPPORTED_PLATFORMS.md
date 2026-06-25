# Supported Platforms

AetherXIV Core has two runtime layers:

- The server stack: MariaDB, PHP, Mono/MSBuild, and the legacy .NET Framework server projects.
- Echo Gate: a modern cross-platform launcher that builds through .NET and launches the user-selected 1.23b client.

The legacy client is a Windows DirectX 9 application. On macOS and Linux, Echo Gate uses Wine-compatible runtimes to launch it.

![Echo Gate runtime detection](../Runtime.png)

## Platform Matrix

| Platform | Echo Gate | Server stack | Client launch | Status |
| --- | --- | --- | --- | --- |
| macOS Apple Silicon | Native `osx-arm64` build | Supported through Homebrew/Mono/MariaDB/PHP | Through Wine-compatible runtime | Primary macOS path |
| macOS Intel | Native `osx-x64` build | Supported through Homebrew/Mono/MariaDB/PHP | Through Wine-compatible runtime | Expected, needs more reports |
| Linux x64 | Native `linux-x64` build | Supported on Ubuntu/Debian style systems | Through Wine with i386 graphics deps | Primary Linux path |
| Linux ARM64 | Native `linux-arm64` build | Expected where dependencies exist | 32-bit Windows client launch unverified | Partial |
| Windows x86/x64 | Native Windows build | Supported with Visual Studio/MSBuild, MariaDB, and PHP | Direct Windows launch | Supported path |
| Windows ARM64 | Native `win-arm64` build | Expected with Windows tooling | x86 client emulation unverified | Partial |
| Steam Deck | Linux build in Desktop Mode | Expected to follow Linux path | Wine/Proton behavior unverified | Experimental |

## Common Dependencies

Server setup:

- MariaDB or MySQL-compatible server
- PHP for the launcher web services
- Mono/MSBuild/NuGet for the legacy server projects on macOS/Linux
- Visual Studio or MSBuild/NuGet on Windows

Echo Gate setup:

- .NET 10 SDK when building from source
- Platform publish target for the machine you are using

Client runtime setup:

- A local, user-owned FFXIV 1.23b client folder
- DirectX 9 compatible runtime path
- `d3dx9_41` in the Wine prefix for macOS/Linux runtime paths
- `Data/staticactors.bin` prepared locally from the selected client folder

## Runtime Notes

macOS and Linux use Wine-compatible runtimes. Echo Gate can detect common runtimes and can also accept a custom runtime path.

Linux x64 needs both Wine and the 32-bit graphics stack because the legacy client is a 32-bit Windows program. The Ubuntu bootstrap helper installs the expected packages and prepares the default prefix when possible.

Steam Deck is listed separately because SteamOS has a different update/package model and needs hardware-specific validation. The project does not require Steam as a launcher dependency.
