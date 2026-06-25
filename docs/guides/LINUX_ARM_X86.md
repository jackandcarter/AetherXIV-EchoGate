# Linux ARM/x86 Setup

Ubuntu/Debian x64 is the primary Linux path. Linux ARM64 can build the launcher/server target, but launching the 32-bit Windows client still needs validation.

## 1. Bootstrap Ubuntu/Debian

On Ubuntu/Debian x64, the easiest path is:

```sh
./tools/bootstrap-ubuntu-build.sh --yes
```

This installs expected build tools, MariaDB/PHP dependencies, Wine/Winetricks when needed, i386 graphics packages, prepares the runtime prefix, builds the legacy servers, tests Echo Gate, and publishes the Linux launcher.

Without `--yes`, the bootstrap asks whether to use the default Echo Gate Wine setup or a custom Wine executable/prefix. Custom mode lists detected Wine executables, lets you choose one, then prepares the selected prefix with the same Wine command used by `wineboot`, `winetricks`, registry setup, and validation.

Useful options:

```sh
./tools/bootstrap-ubuntu-build.sh --yes --wine-source winehq
./tools/bootstrap-ubuntu-build.sh --client-runtime-mode custom
./tools/bootstrap-ubuntu-build.sh --yes --client-runtime-mode custom --wine-command /opt/wine-stable/bin/wine --client-prefix "$HOME/.wine-ffxiv"
./tools/bootstrap-ubuntu-build.sh --yes --no-client-runtime
./tools/bootstrap-ubuntu-build.sh --yes --no-wine
./tools/bootstrap-ubuntu-build.sh --yes --launcher-only
./tools/bootstrap-ubuntu-build.sh --yes --legacy-only
./tools/bootstrap-ubuntu-build.sh --yes --rid linux-arm64
```

## 2. Prepare The Database

If the bootstrap did not already do it, run:

```sh
./tools/setup-local-db.sh
```

Defaults:

```text
database: ffxiv_server
username: aetherxiv
password: aether_dev
hosts: localhost, 127.0.0.1
```

The script can use Ubuntu socket-auth root through `sudo` when needed, or it can ask for MariaDB admin credentials.

## 3. Prepare Runtime Data

If Echo Gate has saved your client path, or `CLIENT_DIR` is set, the run helpers can prepare `staticactors.bin` automatically.

Manual path:

```sh
CLIENT_DIR="/path/to/FINAL FANTASY XIV" ./tools/prepare-client-data.sh
CONFIGURATION=Release ./tools/copy-runtime-data.sh
```

The default Wine prefix is usually under `~/.wine`, but users may have custom prefixes. Do not move your client just to match a hard-coded path; use Echo Gate's Runtime tab or `CLIENT_DIR` to point at the actual local folder.

## 4. Start Local Services

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

## 5. Open Echo Gate

After bootstrap, the Linux launcher is published under:

```text
build/echo-gate/linux-x64/
```

Use the generated launch script from that folder. In Echo Gate:

- Server tab: set launcher service to `http://127.0.0.1:8080/launcher`.
- Client tab: select your local FFXIV 1.23b client folder.
- Runtime tab: choose a detected Wine runtime or enter a custom Wine executable/prefix.
- Home tab: create an account, log in, and launch.

To add Echo Gate to the Linux desktop menu or Steam Deck Desktop Mode launcher,
run the installer from the published folder:

```sh
cd build/echo-gate/linux-x64
./install-desktop.sh
```

The installer writes `~/.local/share/applications/echo-gate.desktop` with the
current local path. Do not reuse a `.desktop` file generated on another machine,
because Linux desktop entries need absolute `Exec=` paths.

![Echo Gate runtime tab](../../Runtime.png)

## 6. Troubleshooting Launch Crashes

Collect logs after the crash:

```sh
./tools/collect-echo-gate-logs.sh --files 10 --lines 160
```

Common Linux issues:

- Missing 32-bit graphics libraries.
- Missing `d3dx9_41` in the selected Wine prefix.
- Wrong Wine prefix selected.
- Client folder selected one level too high or too low.
- A custom prefix path exists but the selected Wine executable is from a different runtime.

The bootstrap validates that the selected prefix has Wine registry files, `drive_c`, and `d3dx9_41.dll`. Echo Gate's Runtime tab should then point at the same Wine executable and prefix used during bootstrap.

## Linux ARM64 Note

Build/publish for ARM64 with:

```sh
./tools/bootstrap-ubuntu-build.sh --yes --rid linux-arm64
```

The launcher/server build path exists. The legacy 32-bit Windows client launch path on Linux ARM64 is still partial until it is validated on real hardware.
