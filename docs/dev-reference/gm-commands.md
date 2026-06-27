# GM Commands

GM commands live in `Data/scripts/commands/gm/`. They are local playtest controls and evidence probes. They are not proof that the real 1.23b client or server behaved the same way.

Parameter strings come from each command's Lua `properties.parameters` value. In practice, `s` is usually a string-like argument, `i` an integer, and `d` a numeric argument.

## Most Useful Commands

| Command | Parameters | Use |
| --- | --- | --- |
| `!queststate` | none | Prints position, private-area state, pending destination, scenario quest phases/flags, and NPC Linkpearl states. |
| `!mypos` | none | Prints the current in-game position for spawn and zone work. |
| `!pinspawn` | `ss` | Captures a provisional battle NPC spawn audit pin at the player's current position. Use prompt mode with no arguments, or one-shot mode with enemy name and source note. |
| `!warp` | `sssssss` | Moves inside the current zone or requests a zone change. Supports target names and `@` offsets. |
| `!warpaeth` | `s` | Teleports to a known aetheryte or aetherial gate from `Data/scripts/aetheryte.lua`; does not attune or unlock anything. |
| `!warpid` | `s` | Teleports to an actor by unique ID. |
| `!warpplayer` | `ssss` | Warps to another player or warps another player to a target player. |
| `!spawn` | `d` | Spawns a static actor near the player by actor class ID. Useful for fast model/path checks. |
| `!spawnnpc` | `sss` | Quick-spawns a level 52 battle NPC from a friendly alias list. Useful for combat and model tests. |
| `!despawn` | `d` | Despawns an actor. The Lua description is stale. |
| `!reloadzone` | `s` | Reloads the current zone and sends instance updates. |
| `!zonecount` | none | Prints actor count in the current zone. |
| `!workvalue` | `ssss` | Sets a named actor work value and calls a UI function. Important for UI/client state probing. |
| `!sendpacket` | `ssss` | Sends a packet file from `./packets/` to a player. Use only for explicit packet experiments. |
| `!questevent` | `ss` | Kicks a quest-owned event or calls a named client event on an active quest actor. |
| `!setnpcls` | `is` | Sets NPC Linkpearl state to `gone`, `inactive`, `active`, or `alert`. |
| `!openinguldah` | `s` | Narrow Ul'dah opening-flow helper for status and Adventurers' Guild linkpearl rescue. |
| `!ba` | `sss` | Calls `DoBattleAction` against a target; useful for battle-action packet experiments. |

## Movement, Zone, and Actor Commands

| Command | Parameters | Notes |
| --- | --- | --- |
| `!mypos` | none | Reports `x`, `y`, `z`, and rotation. |
| `!pinspawn` | `ss` | Records `enemyName`, source note, zone, position, rotation, character, and timestamp into `server_battlenpc_spawn_audit_pins`. These rows are provisional audit pins only and do not spawn actors. |
| `!nudge` | `ss` | Moves the player forward, up, or down. |
| `!warp` | `sssssss` | Broad movement helper. |
| `!warpaeth` | `s` | Known destination aliases plus numeric IDs. |
| `!warpid` | `s` | Moves to an actor unique ID. |
| `!warpplayer` | `ssss` | Player-to-player movement helper. |
| `!reloadzone` | `s` | Rebuilds current zone state. |
| `!zonecount` | none | Counts actors in the zone. |
| `!spawn` | `d` | Static actor spawn near player; optional grid width/height behavior exists in script. |
| `!spawnnpc` | `sss` | Battle NPC spawn by alias. |
| `!despawn` | `d` | Despawns actor by ID. |
| `!setstate` | `s` | Changes an actor state. |
| `!setsize` | `s` | Broadcasts size-change packet. Description is stale. |
| `!setappearance` | `s` | Changes appearance on a target. Description is stale. |
| `!graphic` | `sssss` | Changes equipment/appearance graphics and broadcasts appearance. |
| `!speed` | `sss` | Changes movement speed. |
| `!playanimation` | `sss` | Plays an animation on an actor. |
| `!testmapobj` | `sssss` | Map-object spawn/animation test command. |

## Inventory, Currency, and Character State

| Command | Parameters | Notes |
| --- | --- | --- |
| `!giveitem` | `sssss` | Adds item by ID, quantity, and package/location. |
| `!delitem` | `sssss` | Removes item by ID, quantity, and package/location. |
| `!givekeyitem` | `sss` | Adds key item. |
| `!delkeyitem` | `ssss` | Removes key item. |
| `!givecurrency` | `ssss` | Adds currency; defaults are often used for gil. |
| `!delcurrency` | `ssss` | Removes currency. |
| `!givegil` | `sss` | Adds gil through older package path. |
| `!giveexp` | `sss` | Adds EXP through player EXP path. |
| `!setjob` | `sss` | Sets current job. Description is stale. |
| `!setmaxhp` | `sss` | Sets max HP and heals. |
| `!setmaxmp` | `sss` | Sets max MP. Description says HP but code targets MP path. |
| `!settp` | `sss` | Sets TP. |
| `!setmod` | `ss` | Calls `player:SetMod(mod, value)`. Useful but can leave state unlike recalculated layers. |
| `!setproc` | `sss` | Sets proc state. Description is stale. |
| `!eaction` | `s` | Equips a command ID in the first open slot for current class/job. |
| `!equipactions` | `s` | Equips all commands available to the class/job. |

## Quest, Event, and UI Commands

| Command | Parameters | Notes |
| --- | --- | --- |
| `!quest` | `ssss` | Adds/removes quests and mutates phase/flag state. |
| `!questevent` | `ss` | Calls quest-owned client events for evidence gathering. |
| `!queststate` | none | Best first command for tutorial and quest debugging. |
| `!setnpcls` | `is` | Direct NPC Linkpearl state control. |
| `!openinguldah` | `s` | Ul'dah opening helper. |
| `!endevent` | `ss` | Calls `endEvent()` to close script/event state. |
| `!workvalue` | `ssss` | Calls `SetWorkValue(player, workName, uiFunc, value)` on target actor. |
| `!sendpacket` | `ssss` | Sends packet fixtures. |
| `!music` | `s` | Changes music. |
| `!weather` | `ssss` | Changes weather and transition. |
| `!effect` | `iiii` | Effect/status visual test. |
| `!test` | `ss` | General debug path for zone/content/event experiments. |

## Leve, Party, and Battle Probes

| Command | Parameters | Notes |
| --- | --- | --- |
| `!addguildleve` | `s` | Adds a guildleve by ID. |
| `!removeguildleve` | `s` | Removes a guildleve. Description is stale. |
| `!addtoparty` | `sss` | Adds target to current party. |
| `!ba` | `sss` | Direct battle-action probe. |
| `!vdragon` | `sssss` | Ability-angle test. |
| `!yolo` | `ssss` | Older quick battle NPC spawn path similar to `spawnnpc`. |

## Notes For Developers

- Many command descriptions are stale because the `properties.description` text was copied during early development.
- Prefer `!queststate`, diagnostics, and trace files when trying to understand a flow.
- Use `!pinspawn` when visually comparing archival footage or packet evidence to the live world. Promote reviewed pins into durable spawn rows with an explicit migration; do not point loaders at the audit table.
- Prefer SQL seed rows and loader paths for durable spawn work. `!spawn` and `!spawnnpc` are great probes but not final content.
- `!setmod` is useful for experiments, but stat-system work should flow through recalculated layers whenever possible.
- `!workvalue` is one of the most important commands for client UI reverse engineering because it exercises server-to-client work value writes and UI callbacks.
