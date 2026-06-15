# Steam Deck Experimental Setup

Steam Deck support is experimental. The expected path is close to Linux x64 in Desktop Mode, but it still needs real hardware validation.

## Current Expectation

- Use Desktop Mode.
- Follow the [Linux ARM/x86 guide](LINUX_ARM_X86.md) as the base setup.
- Use Echo Gate's Linux launcher build.
- Use a local Wine-compatible runtime or future managed runtime package.
- Keep Steam/Proton optional; the project should not require Steam as the launcher.

## Why It Is Experimental

SteamOS has a different package and update model than a normal Ubuntu/Debian install. The current bootstrap is designed for Ubuntu/Debian, so package installation, i386 graphics libraries, Wine setup, and prefix setup may need Steam Deck-specific adjustments.

Areas that need validation:

- Whether the Ubuntu/Debian bootstrap can be adapted cleanly.
- Which Wine/Proton path is most reliable.
- Whether the 32-bit graphics stack is present and stable.
- Gamepad/input behavior after launch.
- Echo Gate UI scaling on the Deck screen.
- Persistent storage paths for client, prefix, logs, and server data.

## Useful Reports

If testing on Steam Deck, collect:

- Echo Gate Runtime tab screenshot.
- Echo Gate Launch Log tab screenshot.
- Output from:

```sh
./tools/collect-echo-gate-logs.sh --files 10 --lines 160
```

- Whether the servers were hosted on the Deck or on another machine.
- Whether the client launched through Wine, Proton, or a custom runtime.

Once those reports are consistent, this guide can graduate from experimental to supported.
