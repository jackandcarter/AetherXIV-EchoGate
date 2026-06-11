# Reverse Engineering Tools

MeteorXIV Core keeps reverse-engineering support separate from normal server behavior. Development diagnostics may record packet classifications, Lua coroutine flow, and event lifecycle state, but they must not force client behavior or become required for public server operation.

## Dev Diagnostics

Enable structured traces with either `--dev-diagnostics` or `METEOR_DEV_DIAGNOSTICS=1`.

```bash
METEOR_DEV_DIAGNOSTICS=1 ./tools/run-map.sh
METEOR_DEV_DIAGNOSTICS=1 ./tools/run-world.sh
```

The run scripts pass additional flags through to the server binaries:

```bash
./tools/run-map.sh --dev-diagnostics
./tools/run-world.sh --dev-diagnostics
./tools/run-lobby.sh --dev-diagnostics
```

Trace files are JSON Lines. By default they are written beneath the server process working directory:

```text
Map Server/bin/Release/dev-diagnostics/
World Server/bin/Release/dev-diagnostics/
Lobby Server/bin/Release/dev-diagnostics/
```

Set `METEOR_DEV_DIAGNOSTICS_DIR` to write all traces to a dedicated folder.

```bash
METEOR_DEV_DIAGNOSTICS=1 \
METEOR_DEV_DIAGNOSTICS_DIR=/tmp/meteorxiv-traces \
./tools/run-map.sh
```

Current trace categories:

- `packet.classification`: unknown packet/subpacket metadata with evidence labels.
- `client.target`: target selection request from the client, including resolved actor name when known.
- `client.lockTarget`: target lock request from the client, including resolved actor name when known.
- `client.stateMessage`: provisional decode for map opcode `0x00CE`, including tutorial/state tokens such as `MAN0U000` or `man0u005` when present.
- `actor.questGraphic`: quest marker/icon updates sent for individual actors.
- `event.condition`: event condition definition sent to the client for talk, notice, emote, push-circle, push-fan, and push-box triggers.
- `event.condition.status`: enabled/disabled status sent to the client for known event condition names.
- `event.start`: client event start request, actor, owner, event name, and parameters.
- `event.start.ownerMissing`: client event start request whose owner actor could not be resolved by the server.
- `event.update`: client event update payload for the active event.
- `event.kick`: server event kick messages sent back to the client.
- `event.kickSpecial`: special event kick path used by selected script flows.
- `event.runFunction`: server-side event function dispatch.
- `event.end`: active event teardown.
- `event.data`: event data packets sent to the client.
- `quest.flags`: server-side quest flag mutation for active quest actors.
- `quest.save`: active quest data persistence.
- `quest.phase`: active quest phase transitions.
- `quest.data`: active quest data field changes.
- `lua.dispatch`: Lua script dispatch target and event name.
- `lua.wait`: coroutine wait state registration.
- `lua.resume`: coroutine resume after a matching client event update.
- `lua.resumeMissing`: client update with no matching waiting coroutine.

Packet classification labels are evidence markers only. They do not confirm final protocol names and they do not change behavior. Labels should be promoted to protocol implementation only after repeatable captures confirm the client expectation.

## Lua Audit

Run the Lua audit before tutorial or scenario work:

```bash
./tools/lua-audit.sh
```

Use strict mode in CI or before larger script cleanup commits:

```bash
./tools/lua-audit.sh --strict
```

The audit currently checks for:

- lowercase opening quest IDs where the C# quest registry expects canonical names
- Lua calls using wrong C# method casing
- unsafe director callbacks without a nil guard
- temporary debug print markers

The audit is intentionally narrow. Add checks only when they correspond to a confirmed recurring failure pattern.

## Opening Tutorial Reset

Use the reset helper to put a local character back at a known opening tutorial state. Dry-run is the default:

```bash
./tools/reset-opening-tutorial.sh --character-name "Ian Stackhouse" --town uldah
```

Apply the reset explicitly:

```bash
./tools/reset-opening-tutorial.sh --character-id 2 --town uldah --apply
```

Supported towns:

- `uldah`
- `gridania`
- `limsa`

The reset helper clears the three opening scenario quest IDs, inserts the selected opening quest at a free scenario slot, clears pending zone destinations, and moves the character to the town-specific opening coordinate. It is a local database tool for repeatable testing, not gameplay logic.

## Workflow

1. Build and copy runtime data.
2. Start Map and World with dev diagnostics enabled.
3. Reproduce one tutorial or zone transition issue.
4. Save the Map, World, and Lobby JSONL trace files with the manual observation.
5. Classify unknown packets as candidates, then implement only confirmed server behavior.
6. Use `tools/reset-opening-tutorial.sh` to return the character to the same checkpoint for another pass.

This workflow is designed to separate evidence capture from implementation. Normal release builds and normal run scripts remain valid without diagnostics enabled.
