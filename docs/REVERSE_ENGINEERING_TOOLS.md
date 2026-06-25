# Reverse Engineering Tools

AetherXIV Core keeps reverse-engineering support separate from normal server behavior. Development diagnostics may record packet classifications, Lua coroutine flow, and event lifecycle state, but they must not force client behavior or become required for public server operation.

For a readable entry point into GM commands, known enemies, zone/spawn data, client UI hooks, and the battle/stat roadmap, start with [Developer Reference](dev-reference/README.md).

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
- `client.login.ready`: map opcode `0x0006`; the client says it is ready for login/zone-in packets.
- `client.login.ready.done`: server-side login/zone-in setup finished after the client ready packet.
- `client.zoneInComplete`: map opcode `0x0007`; the client reports that zone loading completed.
- `client.position`: player position update from the client, useful as the first sign that the load screen released.
- `zone.in.begin`: server begins login/session zone-in resolution for the player.
- `zone.in.area`: resolved area/private-area selected for the zone-in.
- `zone.in.packets`: counts for the zone-in packet bundle assembled for the client.
- `zone.in.end`: server completed zone-in packet assembly and instance update.
- `zone.in.blocked`: server could not resolve the requested zone/private-area.
- `session.instance.update`: nearby actors considered for client instancing.
- `session.instance.update.done`: actor instance list size after an update.
- `zone.change.request`: script/server requested a zone or private-area transition.
- `zone.change.handoff`: requested destination is not hosted by the current map route.
- `zone.change.local.end`: local zone/private-area transition finished.
- `zone.change.content.begin` / `zone.change.content.end`: transition into a content private area.
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
- `game.message`: game text packets sent to the client, including text owner, text id, log channel, sender mode, and message parameters.
- `npcLinkshell.state`: NPC Linkpearl state writes or unchanged writes, including the requested state and saved calling/extra flags.
- `quest.flags`: server-side quest flag mutation for active quest actors.
- `quest.save`: active quest data persistence.
- `quest.phase`: active quest phase transitions.
- `quest.data`: active quest data field changes.
- `lua.dispatch`: Lua script dispatch target and event name.
- `lua.wait`: coroutine wait state registration.
- `lua.resume`: coroutine resume after a matching client event update.
- `lua.resumeMissing`: client update with no matching waiting coroutine.

Packet classification labels are evidence markers only. They do not confirm final protocol names and they do not change behavior. Labels should be promoted to protocol implementation only after repeatable captures confirm the client expectation.

## Playtest Bridge

Use the playtest bridge when live testing needs one control point instead of several terminal windows. The bridge lives in its own directory so local Codex sessions or other helper apps can point at it directly; a human playtester does not need to drive these commands during a Codex-assisted run.

```bash
./playtest-bridge/bridge.py doctor
./playtest-bridge/bridge.py build
./playtest-bridge/bridge.py run --fresh --new-session
```

The bridge wraps the existing server scripts and starts:

- Web launcher service.
- Lobby server with diagnostics.
- Map server with diagnostics.
- World server with diagnostics.

It writes structured traces to `/tmp/meteorxiv-traces` by default and captured server logs to `playtest-bridge/.state/logs/`.

Common bridge commands:

```bash
./playtest-bridge/bridge.py status
./playtest-bridge/bridge.py watch --focus battle
./playtest-bridge/bridge.py watch --focus tutorial
./playtest-bridge/bridge.py watch --focus loading
./playtest-bridge/bridge.py watch --focus errors
./playtest-bridge/bridge.py events --focus battle --limit 80
./playtest-bridge/bridge.py brief --focus battle
./playtest-bridge/bridge.py recipe show opening-uldah-battle
./playtest-bridge/bridge.py assert --recipe opening-uldah-battle
./playtest-bridge/bridge.py logs --service map
./playtest-bridge/bridge.py summary --timeline --max-events 200
./playtest-bridge/bridge.py note "manual observation"
./playtest-bridge/bridge.py snapshot --recipe opening-uldah-battle --note "captured after reproducing issue"
./playtest-bridge/bridge.py compare <old-snapshot-or-session> <new-snapshot-or-session> --recipe opening-uldah-battle
./playtest-bridge/bridge.py reset --character-name "Ian Seven" --town uldah --apply
./playtest-bridge/bridge.py stop
```

The bridge can also expose a local JSON control surface for Codex or another local helper:

```bash
./playtest-bridge/bridge.py serve
curl http://127.0.0.1:8765/status
curl "http://127.0.0.1:8765/events?focus=battle&limit=50"
curl "http://127.0.0.1:8765/brief?focus=battle&limit=40"
curl "http://127.0.0.1:8765/assertions?recipe=opening-uldah-battle"
```

Keep the HTTP bridge bound to `127.0.0.1`; it is a local development control surface, not a public API. Full command details are in `playtest-bridge/README.md`.

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

## Client Evidence Scan

Use the client evidence scanner when a quest, cutscene, or loading transition stalls and the server traces alone do not prove the next client expectation.

```bash
./tools/client-evidence.sh "/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV" man0u
```

The second argument is a cut bundle prefix. Useful opening-flow examples:

```bash
./tools/client-evidence.sh "/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV" man0u
./tools/client-evidence.sh "/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV" man0g
./tools/client-evidence.sh "/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV" man0l
```

The scan reports:

- matching `client/cut` bundles
- visible actor, quest, path, UI, and clip-class strings inside those bundles
- direct string hits in packed `client/script` containers, when present
- version/hash context for the local client executable and `game.ver`

This is an evidence indexer, not a decompiler. A cut bundle containing `RaptureQuestInfoClip`, `Rapture2DMapClip`, or `RaptureMesClip` proves that the client has those sequence pieces in that bundle; it does not prove the server should trigger the bundle at the current quest state. Promote behavior only when client evidence lines up with live traces, actor state, and existing script/database data.

## GM Debug Commands

These are in-game chat commands for local playtests. They are diagnostics or explicit test controls; they should not replace normal scenario logic.

```text
!queststate
```

Prints the current position, private-area state, pending destination, active scenario quests with phase/flags, and active NPC Linkpearl states.

```text
!setnpcls <id> <gone|inactive|active|alert>
```

Sets an NPC Linkpearl state directly. NPC Linkpearl `0` is the Adventurers' Guild linkpearl used by the opening city flows.

```text
!openinguldah status
!openinguldah linkpearl
```

Shows the current Ul'dah `Man0u1` state, or sets the Adventurers' Guild NPC Linkpearl to `alert` only when `Man0u1` is active. This is a narrow rescue command for characters that passed the Momodi handoff before the script activated the linkpearl.

```text
!questevent <questNameOrId> <eventName>
```

Kicks a quest-owned `noticeEvent`, calls a named client event on an active quest actor, and logs the before/after quest phase and flags. This is for evidence gathering only: it does not advance phases, complete quests, replace quests, teleport, or change NPC Linkpearl state. Example probe for the current Ul'dah opening investigation:

```text
!questevent Man0u1 processEvent020
```

```text
!warpaeth <aetheryteId|alias>
!warpaeth list
```

Teleports the current GM/player to a known aetheryte or aetherial gate destination from `Data/scripts/aetheryte.lua`. This is diagnostic movement only: it does not attune, unlock, progress quests, or create missing world objects. Numeric IDs are the canonical source; aliases are only convenience labels.

Useful probes for the current Thanalan work:

```text
!warpaeth 1280032
!warpaeth blackbrush
!warpaeth uldah
```

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
