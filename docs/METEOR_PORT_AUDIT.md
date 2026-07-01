# Meteor Port Audit

This audit keeps AetherXIV 2.0 grounded in the current Meteor repo. The goal is
to port known-working behavior into modern boundaries, not to replace it with
placeholder logic.

## Source Rules

- Meteor repo behavior is the primary source for server behavior.
- Current Lua scripts are compatibility fixtures, not optional examples.
- Client packet handling and client files may reveal missing context, but they
  do not replace server-side logic already present in Meteor.
- Client `.gmd`, `.geb`, and sqpack-derived data may enrich placement,
  anchors, layout objects, text, and map context. They should not be treated as
  NPC AI, battle logic, quest flow, or spawn timing without another source.
- Direction matters for packet opcodes. Some numeric opcodes are reused across
  client-to-server and server-to-client meanings.

## Drift Audit Notes

- The Map event path should have one packet output boundary:
  `MapScriptOutboxItem` records emitted by Map/Lua services and encoded by
  `MapEventOutboxPacketTranslator` at the network edge. Lua adapters must not
  write packets directly.
- Script-emitted event commands are captured per dispatcher call. This avoids a
  parallel shared packet queue and prevents commands from one event dispatch
  leaking into another concurrent event dispatch.
- `LegacyLuaGlobalBindings` can override helpers such as `callClientFunction`
  for isolated scripting tests, but the Map conformance path uses current
  `global.lua`: `callClientFunction` calls `player:RunEventFunction(...)` and
  yields `_WAIT_EVENT`.
- NPC script lookup is function-aware like Meteor: child/unique scripts are
  preferred only when the requested function exists, otherwise the base
  actor-class script gets a chance to handle the event.
- `MapRuntimeNpcActorAdapter` intentionally exposes only actor-class/spawn
  metadata and narrow runtime state. Its default HP/modifier behavior mirrors
  Meteor's incomplete NPC defaults; it is not a final battle/stat source.
- `SetEventStatus` needs a separate audit before porting. Meteor's
  `Player.SetEventStatus` builds a `SetEventStatusPacket` but does not visibly
  queue it in the inspected code path, so 2.0 should not invent behavior there
  without confirming the intended packet flow.
- Lua parameter support is intentionally limited to the event-command types
  currently proven by Meteor/client packet fixtures. Types such as item refs and
  type-9 payloads should be ported when a real packet/script fixture requires
  them.

## Legacy Event Path

Meteor sources:

- `Map Server/PacketProcessor.cs`
- `Map Server/Packets/Receive/Events/EventStartPacket.cs`
- `Map Server/Packets/Receive/Events/EventUpdatePacket.cs`
- `Map Server/Packets/Send/Events/KickEventPacket.cs`
- `Map Server/Packets/Send/Events/RunEventFunctionPacket.cs`
- `Map Server/Packets/Send/Events/EndEventPacket.cs`
- `Map Server/Actors/Chara/Player/Player.cs`
- `Map Server/Lua/LuaEngine.cs`
- `Map Server/Actors/Director/Director.cs`

Observed behavior:

- Client event start is `0x012D`.
- Client event update is `0x012E`.
- Server kick event is `0x012F`.
- Server run event function is `0x0130`.
- Server end event is `0x0131`.
- `PacketProcessor` resolves the event owner before script dispatch:
  static actor, spawned retainer, actor in current area, then player director.
- If no owner resolves, Meteor queues `EndEventPacket` rather than pretending
  the event can run.
- `Player.StartEvent` stores `currentEventOwner`, `currentEventName`, and
  `currentEventType`, then calls `LuaEngine.EventStarted`.
- `Player.UpdateEvent` resumes the Lua event wait through
  `LuaEngine.OnEventUpdate`.
- `LuaEngine.EventStarted` either resumes an existing player event coroutine or
  dispatches `onEventStarted`.
- Director events are special: `Director.OnEventStart` creates a coroutine and
  resumes with `(player, director, ...)` shape.
- Non-director actor events call `onEventStarted` through the actor script path.

2.0 status:

- Event start/update/kick/run/end packet codecs exist in `AetherXIV.Protocol`.
- The Lua parameter codec now follows the Meteor event wire format.
- `MapProtocolEventTranslator` converts decoded event packets into Map event
  records.
- `MapScriptEventDispatcher` routes decoded starts/replies into the script
  service boundary.
- `MeteorStyleMapEventInvocationResolver` ports Meteor's event owner lookup
  order and resolves NPC/director call shapes from runtime actor metadata.
- Missing event owners now produce an explicit end-event outbox item, matching
  Meteor's client-visible fallback.

## Legacy Actor And Spawn Data Path

Meteor sources:

- `Map Server/WorldManager.cs`
- `Map Server/Actors/Area/Area.cs`
- `Map Server/Actors/Chara/Npc/Npc.cs`
- `Map Server/Actors/Chara/Npc/BattleNpc.cs`
- `Map Server/Actors/Chara/Npc/ActorClass.cs`
- `Map Server/Actors/EventList.cs`
- `Data/sql/gamedata_actor_class.sql`
- `Data/sql/server_spawn_locations.sql`
- `Data/sql/server_battlenpc_groups.sql`
- `Data/sql/server_battlenpc_spawn_locations.sql`

Observed behavior:

- Actor class data comes from `gamedata_actor_class` joined to
  `gamedata_actor_pushcommand`.
- Actor classes carry class path, display name id, property flags, event
  condition JSON, and push command metadata.
- Static/NPC spawns come from `server_spawn_locations`.
- Spawn rows include class id, unique id, zone/private area, position,
  rotation, state, and animation id.
- `Area.SpawnActor` creates the runtime actor from actor class plus spawn row,
  loads event conditions, initializes basic HP/speeds, and adds the actor to
  the zone.
- `Npc.CreateScriptBindPacket` calls Lua `init()` where available. Map objects
  may get layout/instance data from script `init()`.
- Directors are actors too. They advertise notice events
  `noticeEvent`, `noticeRequest`, and `reqForChild`.

2.0 status:

- The database foundation has static actor and battle NPC spawn records, but it
  does not yet model actor class metadata, event condition JSON, push command
  metadata, static actor lookup, or runtime Map actor/session ownership.
- Map now has actor-class/spawn metadata records and an
  `IMapRuntimeNpcActor` adapter that builds Meteor-compatible script
  descriptors from actor class path, zone script directory, private-area
  metadata, and spawn unique id.
- NPC script module selection now checks current script files in Meteor order:
  unique/private-area script first when it defines the requested function, then
  base actor-class script fallback.

## Packet Direction Risk

Meteor reuses numeric opcodes across directions:

- `0x012F` is server-to-client `KickEventPacket`, but client-to-server
  `PacketProcessor` handles `0x012F` as `ParameterDataRequestPacket`.
- `0x0131` is server-to-client `EndEventPacket`, but client-to-server
  `PacketProcessor` handles `0x0131` as `UpdateItemPackagePacket`.

2.0 packet registration is now direction-aware. The simple `Register/Get`
overloads remain for existing server-to-client codec tests, while Map packet
work can register by `PacketDirection`.

## Best Next Pass

The next pass should port Meteor's event/session resolver path, not invent a
generic placeholder resolver.

Implemented:

- Direction-aware packet catalog/dispatch metadata for codecs.
- Map runtime contracts for player, actor, NPC, director, actor directory, and
  current event state.
- Actor-class/spawn-backed NPC runtime adapter for script-facing metadata,
  state, position, HP, and modifier values.
- Meteor-style NPC script module selection with function-aware
  unique/private-area preference and base actor-class fallback.
- Meteor-style event owner resolver:
  static actor -> player's spawned retainer -> current area actor -> player's
  director.
- Missing owner end-event outbox item plus packet translation.
- `MapEventCommandPlayerAdapter` ports Meteor's script-side event commands:
  `KickEvent` emits notice-type `KickEventPacket` work, `RunEventFunction`
  uses the player's current event owner/name/type, and `EndEvent` emits an
  end-event response before clearing current event state.
- Lua event command parameters now use a Meteor-style object-to-Lua-parameter
  conversion at the Map boundary: int32, uint32, byte, string, booleans, null,
  and actor-id parameters.
- Map dispatcher now wraps Lua-facing runtime players with event-command
  adapters, captures script-emitted packet commands during coroutine start and
  resume, and returns those commands to `MapEventPacketHandler` as outgoing
  subpackets.
- Concrete Map event packet handler for client `0x012D` event start and
  `0x012E` event update packets.
- Tests for direction-aware registration, missing owner close, NPC invocation,
  director invocation, client event reply resume, event packet dispatch,
  missing-owner end-event packet output, current script path selection, base
  script fallback, child-without-function fallback, actor-class/spawn
  descriptor construction, and script-side kick/run/end event command packet
  output.
- `Data/scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua` now runs
  through the Map packet path: event start -> `global.lua` `callClientFunction`
  -> `RunEventFunctionPacket` -> client event update resume -> lowercase
  `endEvent` -> `EndEventPacket`.

Remaining scope:

- Resolve director event invocations with the director call shape.
- Add a director notice fixture through the same packet path.

Out of scope for that pass:

- Full battle AI.
- Full spawn import tooling.
- Client `.gmd`/`.geb` extraction unless an actor placement or layout anchor is
  missing from repo data being ported.
- Rewriting all Map opcodes at once.

## Recommended Order

1. Add a director notice conformance test around a current tutorial director
   fixture.
2. Add runtime Map actor directories backed by imported static actor rows and
   live Map sessions.
3. Use client `.gmd`/`.geb` extraction only if actor placement or layout
   anchors are missing from repo-confirmed data.
