# Lua API Surface

This is the first 2.0 compatibility boundary for current AetherXIV Lua scripts. The intent is not to clone old server classes into Lua. The intent is to keep the script-facing shape that already works, then route those calls through typed 2.0 services.

## Design Rules

- Lua sees narrow adapters, not database rows, sockets, sessions, or packet writers.
- Adapter method names stay close to current script names so existing scripts can carry forward.
- Values returned directly to Lua should use Lua-friendly primitives when existing scripts compare them as numbers or strings. For example, `GetZoneID()` returns a numeric zone id at the Lua boundary even if internal services use `ZoneId`.
- Each script role gets a known call shape: arguments, optional globals, and expected return behavior.
- Missing behavior is added only when a real script or golden compatibility test needs it.
- Case aliases such as `EndEvent` and `endEvent` should be handled by the Lua binding layer, not duplicated in service code.
- Explicit globals supplied by the host may override helpers loaded from `global.lua` for that invocation. This is how early non-coroutine conformance tests can replace coroutine-backed helpers such as `callClientFunction` without editing current Lua scripts.

## Contract Location

- Public script-facing contracts live in `AetherXIV.Scripting`.
- Reusable legacy globals are built by `LegacyLuaGlobalBindings` from `LegacyScriptRuntimeContext`.
- Gameplay implementations should live behind World/Map/Data services and be exposed through adapters.
- Script compatibility tests live in `AetherXIV.Scripting.Tests` and should use current `Data/scripts` fixtures where possible.

## Script Families

### Player Lifecycle

Examples: `Data/scripts/player.lua`, login/opening setup.

Call shape:

- `player: IPlayerScriptApi`
- optional global `GetWorldManager`
- optional global `GetWorldMaster`

Main surface:

- identity/zone: `GetInitialTown`, `GetZoneID`, `GetZone`, `GetPlayTime`, `SavePlayTime`
- inventory/equipment: `GetItemPackage`, `GetEquipment`
- quests: `AddQuest`, `HasQuest`, `GetQuest`, `IsQuestCompleted`, `CompleteQuest`
- events: `KickEvent`, `SetLoginDirector`, `AddDirector`, `EndEvent`
- feedback: `SendMessage`, `SendGameMessage`, `SendDataPacket`
- opening flow: `player:GetZone():CreateDirector(...)`

### NPC Events

Examples: `Data/scripts/base/chara/npc/**`, `Data/scripts/unique/**`.

Call shape:

- `player: IPlayerScriptApi`
- `npc: INpcScriptApi`
- optional global `GetStaticActor`
- optional global `callClientFunction`
- optional global `GetWorldManager`
- optional global `GetWorldMaster`

Main surface:

- `npc:GetActorClassId`
- `npc:SetQuestGraphic`
- `player:EndEvent`
- `player:SetEventStatus`
- `player:RunEventFunction`
- `player:KickEvent`

Observed lowercase player aliases in current scripts:

- `endEvent`: 519 references, pinned by conformance tests.
- `kickEvent`: 2 references.
- `getItemPackage`: 2 references.
- `doEmote`: 2 references.
- `hpstuff`: 2 references.
- `getInventory`: 1 reference.
- `examinePlayer`: 1 reference.

Only aliases exercised by real fixtures should be promoted into adapters. `endEvent` is required immediately; the rest should be added with the specific script that proves the needed signature.

### Directors And Content

Examples: `Data/scripts/directors/**`, `Data/scripts/content/**`.

Call shape:

- `director: IDirectorScriptApi`
- optional `player: IPlayerScriptApi`
- optional `contentArea: IContentAreaScriptApi`
- optional global `wait`
- optional global `GetWorldManager`

Main surface:

- membership: `AddMember`, `GetMembers`, `GetPlayerMembers`
- lifecycle: `StartDirector`, `StartContentGroup`, `EndDirector`
- content: `SpawnActor`, `SpawnBattleNpcById`, `ContentFinished`
- tutorial/guildleve state: `UpdateAimNumNow`

### Commands And Combat

Examples: `Data/scripts/commands/**`, `Data/scripts/effects/**`, `Data/scripts/battlenpc.lua`.

Call shape:

- `player/source: IPlayerScriptApi` or `ICharacterScriptApi`
- optional `target: ICharacterScriptApi`
- optional `command: IBattleCommandScriptApi`
- optional `effect: IStatusEffectScriptApi`

Main surface:

- action execution: battle command metadata, result codes, combo hooks
- actor state: HP, mods, engagement state, position
- feedback: messages, animations, graphics, music
- status effects: magnitude, tier, extra, source

### Quest State

Examples: `Data/scripts/quests/**`.

Call shape:

- `player: IPlayerScriptApi`
- `quest: IQuestScriptApi`

Main surface:

- `GetQuestId`
- `GetPhase`
- `NextPhase`
- `GetQuestFlag`
- `SetQuestFlag`
- `GetQuestFlags`
- `SaveData`

## First Implementation Priority

1. Bind `IPlayerScriptApi`, `IQuestScriptApi`, `IActorScriptApi`, and `IZoneScriptApi` into MoonSharp.
2. Add aliases for known case variants: `EndEvent`/`endEvent`, `GetItemPackage`/`getItemPackage`.
3. Add invocation tests for `player.lua:onBeginLogin` and `player.lua:onLogin` using stub adapters.
4. Add NPC event tests for a simple current `PopulaceStandard` script.
5. Add director tests for one opening/tutorial director with coroutine waits stubbed by `IScriptSchedulerApi`.

This gets us from "scripts load" to "scripts can run against typed 2.0 context" without pretending the full live server exists yet.

## Current Conformance

- `Data/scripts/player.lua:onBeginLogin` runs against `IPlayerScriptApi` stubs and verifies opening quest/home point/checkpoint behavior.
- `Data/scripts/player.lua:onLogin` runs against `IPlayerScriptApi`, `IZoneScriptApi`, and `IDirectorScriptApi` stubs and verifies opening director startup.
- New-player item and equipment initialization runs through `IScriptItemPackageApi`, `IScriptEquipmentApi`, and legacy `playerWork/charaWork` state adapters.
- `Data/scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua:onEventStarted` runs against `IPlayerScriptApi`/`INpcScriptApi` stubs and verifies `GetStaticActor`, `callClientFunction`, and lowercase `player:endEvent()` compatibility.
- `LegacyLuaGlobalBindings` has direct tests for `GetStaticActor`, `callClientFunction`, `GetWorldManager`, `GetWorldMaster`, `wait`, and `waitForSignal`.
- Coroutine invocation is available through `StartCoroutineAsync` and `ResumeCoroutineAsync`.
- `_WAIT_EVENT`, `_WAIT_TIME`, and `_WAIT_SIGNAL` yields are parsed into typed wait requests.
- `Data/scripts/directors/Quest/QuestDirectorMan0u001.lua:onEventStarted` reaches the real `global.lua` coroutine helpers and verifies event, signal, and time waits without overriding `callClientFunction`, `wait`, or `waitForSignal`.
- `ScriptCoroutineScheduler` owns suspended coroutine handles and routes resumes by event owner, signal, and injected clock time.
- Coroutine scheduler diagnostics cover start, wait registration, resume, completion, and error paths.
- `MapScriptEventService` is the Map-side boundary for starting script events, resuming client event waits from decoded Lua parameters, emitting script signals, and ticking due time waits.
- The Map boundary exposes `TickDueAsync` instead of owning timer policy.
- `AetherXIV.Server.Hosting` provides generic async loop/tick-source primitives: `IAsyncServerLoop`, `IIntervalTickSource`, `PeriodicIntervalTickSource`, and `FixedIntervalServerLoop`.
- `MapScriptTimerLoop` is the Map-side hosted adapter around `MapScriptEventService.TickDueAsync`. It reports registered/completed/failed counts and delegates loop lifetime/error continuation to hosting primitives.
- Timer tests use fake tick sources, so timer behavior is deterministic and does not hide script, packet, or gameplay behavior.
- `MapScriptEventDispatcher` is the decoded Map event entry boundary. It accepts `MapEventTrigger` for talk/notice/emote/push starts and `MapClientEventReply` for client event replies.
- `MapScriptEventDispatcher` resolves script invocations through `IMapScriptEventInvocationResolver`, routes starts to `StartEventAsync`, routes replies to `ResumeClientEventAsync`, and queues explicit `MapScriptOutboxItem` records instead of writing packets directly.
- `MapScriptModuleIds` has conformance tests for current real fixtures: `Data/scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua` and `Data/scripts/directors/Quest/QuestDirectorMan0u001.lua`.
- `MapNpcScriptDescriptor.FromLegacyActorClass` preserves Meteor's actor class
  path normalization: lowercase parent directories with the original class
  name, then zone/class/unique script paths for NPC event dispatch.
- `MeteorFileSystemMapNpcScriptModuleSelector` checks the current script tree in
  Meteor order: unique/private-area NPC script first when it defines the
  requested function, then base actor-class script fallback.
- `MapRuntimeNpcActorAdapter` maps actor class and spawn metadata into
  `IMapRuntimeNpcActor` without inventing AI behavior. It exposes persisted
  identity/path/position/state values and narrow runtime HP/modifier state.
- Protocol codecs now cover Meteor's event packet family: event start/update from the client and kick/run/end event packets back to the client.
- `LuaParameterCodec` now follows the real event packet Lua parameter wire format, including big-endian numeric values and the `0x0F` list terminator.
- `MapProtocolEventTranslator` converts decoded `EventStartPacket` and `EventUpdatePacket` values into the Map dispatcher records.
- `PacketRegistry` supports direction-aware codec registration so reused Meteor opcodes do not collide across client/server directions.
- `MeteorStyleMapEventInvocationResolver` resolves event owners in Meteor order and builds NPC/director `onEventStarted` invocation shapes from runtime actor metadata.
- Missing event owners create an explicit `EndEvent` outbox item that translates to `EndEventPacket`.
- `MapEventCommandPlayerAdapter` wraps runtime players for Lua event calls and
  ports Meteor's `KickEvent`, `RunEventFunction`, `EndEvent`, `kickEvent`, and
  `endEvent` behavior into `MapScriptOutboxItem` records.
- Script-emitted event commands now translate at the Map network edge into
  `KickEventPacket`, `RunEventFunctionPacket`, and `EndEventPacket`.
- Map Lua event command parameter conversion matches Meteor's supported object
  cases: int32, uint32, byte, string, booleans, null, arrays, `ActorId`, and
  script actor objects.
- `MapScriptEventDispatcher` wraps Lua-facing Map players with the event
  command adapter, captures packet commands emitted during coroutine start and
  resume, and returns those commands to the packet handler.
- `MapEventPacketHandler` decodes client event start/update packets and routes them through `MapScriptEventDispatcher`, returning supported outgoing subpackets from event outbox items.
- `Data/scripts/unique/wil0Town01a/PopulaceStandard/gogofu.lua` is now covered
  end-to-end through the Map packet path: start packet, current `global.lua`
  `callClientFunction`, client reply resume, and lowercase `endEvent`.

## Next Pass

The next realistic pass is to connect this path to live Map runtime/session ownership:

- add a director notice conformance test using a current tutorial director
  fixture;
- adapt active Map sessions/actors into `IMapRuntimePlayer`,
  `IMapRuntimeNpcActor`, and `IMapRuntimeDirectorActor`;
- connect Map event outbox draining to the transport/session send queue;
- only parse installed client `.gmd`/`.geb`/sqpack data if repo-confirmed actor placement, anchor, or script descriptor data is missing.
