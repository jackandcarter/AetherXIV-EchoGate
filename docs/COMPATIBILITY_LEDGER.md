# Compatibility Ledger

AetherXIV 2.0 is a modernization of known-working AetherXIV behavior, not a
fresh attempt to rediscover how the client works.

## Rules

- Preserve current packet and byte behavior unless a fixture proves it changed.
- Keep v1 identity values stable during import mapping: account, character,
  world, zone, actor, and battle NPC IDs must not silently change meaning.
- Do not promote restored gameplay data as canonical unless it is
  `RepoConfirmed`, `ClientConfirmed`, `TraceConfirmed`, or `RetailConfirmed`.
- `Provisional` and `TestOnly` data may exist for development, but must remain
  labeled.
- Any client-mined or restored data needs a source type and source reference.
- Client `.gmd`, `.geb`, and sqpack-derived data may confirm map geometry,
  environmental anchors, layout objects, localized text, and placement hints.
  It must not be treated as server-side NPC AI, battle behavior, quest logic,
  or spawn timing unless another source confirms that behavior.

## Current First Fixtures

- Weather payloads from the legacy `SetWeatherPacket` shape.
- Dalamud payloads from the legacy `SetDalamudPacket` shape.
- Lua parameter primitive byte ordering from Meteor/client event packets:
  int32/uint32/actor ids are big-endian inside the Lua parameter stream,
  strings are null-terminated, and the parameter list ends with `0x0F`.
- Representative v1 character and battle NPC row mappings.
- Map event condition kind mapping from current Meteor actor behavior:
  talk = 1, push = 2, emote = 3, notice = 5.
- Current real Lua fixture paths for NPC and director event starts.
- Meteor NPC script lookup uses the unique script first only when it defines
  the requested function, then falls back to the base actor-class script. 2.0
  has tests for `Data/scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua`,
  `Data/scripts/base/chara/npc/populace/PopulaceStandard.lua`, and the
  child-file-without-function fallback case.
- Actor class path normalization follows Meteor's `Npc` constructor behavior:
  lowercase parent directories, preserve the final class name, and use the
  spawn `uniqueId` as the unique script name.
- Map event packet opcodes and fixed offsets from Meteor:
  `0x012D` event start, `0x012E` event update, `0x012F` kick event,
  `0x0130` run event function, `0x0131` end event.
- Script-side event commands preserve Meteor player behavior:
  `KickEvent` sends a notice-type kick event to the supplied actor,
  `RunEventFunction` uses the player's current event owner/name/type, and
  `EndEvent` sends the current event close packet before clearing event state.
- Event command Lua parameter conversion follows Meteor's supported cases:
  signed int, unsigned int, byte, string, booleans, null, and actor id.
- Current NPC talk fixture `gogofu.lua` is covered through the Map packet path:
  event start, `global.lua` `callClientFunction`, `RunEventFunctionPacket`,
  client event update resume, lowercase `endEvent`, and `EndEventPacket`.
- Map event owner resolution order from Meteor:
  static actor, player's spawned retainer, actor in current area, then player's
  director. Missing owners should close the event with an end-event response.
- Packet opcode meanings are direction-specific. For example, `0x012F` is a
  server-to-client kick event but a client-to-server parameter data request.
- Direction-aware codec registration is required for Map packet work.
- Client event start/update packet handling should pass through
  `MapEventPacketHandler`, which owns packet decode and outbox packet output
  while leaving actor/session resolution to the Meteor-style resolver.

The goal is not endless analysis. The goal is to stop accidental drift while
the 2.0 core gets cleaner networking, services, and database boundaries.
