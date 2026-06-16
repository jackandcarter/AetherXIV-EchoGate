# Zones and Spawns

Zones and spawns are mostly data-driven. The map server reads SQL at startup and turns rows into `Zone`, `PrivateArea`, static actor, and battle NPC objects.

## Load Order

At map server startup, `Map Server/Server.cs` calls the relevant `WorldManager` loaders in this order:

1. `LoadZoneList()`
2. `LoadZoneEntranceList()`
3. `LoadSpawnLocations()`
4. `LoadBattleNpcs()`
5. `LoadBattleCommands()`
6. `LoadBattleTraits()`

That order matters. Static and battle spawns can only attach to zones that were loaded first.

## Zone Tables

| Table | Current Role |
| --- | --- |
| `server_zones` | Primary zone metadata: zone ID, region, name, server route, class path, music, instance flags, chocobo/stealth/navmesh flags. |
| `server_zones_privateareas` | Private areas attached to parent zones. |
| `server_zones_spawnlocations` | Zone-in spawn points by zone ID, private area, and spawn type. |
| `server_seamless_zonechange_bounds` | Bounding boxes for seamless region/zone transitions. |

Useful known zone IDs from current stat/battle work:

| Zone | ID | Notes |
| --- | ---: | --- |
| Central Thanalan | `170` | Current repeatable rat combat fixture. |
| Ul'dah city | `175` | Main city/tutorial investigation area. |
| Ul'dah opening battle/private content | `184..188` | Opening-flow battle/content zones. |

## Static Actor Spawns

Static actors load from `server_spawn_locations`.

Important columns:

| Column | Meaning |
| --- | --- |
| `actorClassId` | Links to `gamedata_actor_class`. |
| `uniqueId` | Script/actor identity. Useful for `!warpid`. |
| `zoneId` | Zone actor ID where the spawn belongs. |
| `privateAreaName` | Empty or named private area. |
| `privateAreaLevel` | Private area layer. |
| `positionX/Y/Z`, `rotation` | Placement. |
| `actorState`, `animationId` | Initial state and animation. |
| `customDisplayName` | Optional display override. |

Loader path:

- `WorldManager.LoadSpawnLocations()` reads all rows.
- It skips rows whose actor class or zone is missing.
- It creates `SpawnLocation`.
- It attaches the spawn to the zone with `zone.AddSpawnLocation(spawn)`.
- Later, `Zone.SpawnAllActors(true)` materializes the actors.

## Battle NPC Spawns

Battle NPCs use a joined model rather than one flat row.

| Table | Role |
| --- | --- |
| `server_battlenpc_spawn_locations` | Individual spawn points, `bnpcId`, group ID, position, rotation. |
| `server_battlenpc_groups` | Group behavior: pool ID, script, min/max level, respawn, HP/MP, allegiance, spawn type, zone/private area. |
| `server_battlenpc_pools` | Actor class, genus, current job, combat skill/delay/damage multiplier, aggro, immunity, link type, spell/skill lists. |
| `server_battlenpc_genus` | Family stats and model behavior: movement speed, kindred, detection, primary stats, attack, defense, resistances, element. |
| `server_battlenpc_genus_mods` | Optional genus-level modifiers. |
| `server_battlenpc_pool_mods` | Optional pool-level modifiers. |
| `server_battlenpc_spawn_mods` | Optional individual-spawn modifiers. |

Loader path:

- `WorldManager.LoadBattleNpcs()` loads modifier overlays first.
- It joins spawn locations to groups, pools, and genus rows per zone.
- It creates `BattleNpc` or ally actors based on allegiance.
- It sets level, allegiance, movement, detection, kindred, spawn type, HP/MP, primary stats, attack/accuracy/defense/evasion, drop list, spell list, skill list, and respawn time.
- It applies pool, genus, and spawn modifiers.
- It runs `CalculateBaseStats()` and `RecalculateStats()`.
- Normal spawn type actors are added to the zone immediately.

## Direct Spawn By ID

`WorldManager.SpawnBattleNpcById(id, area)` uses a similar SQL join to spawn or force-respawn a single battle NPC by `bnpcId`. It is useful for commands and scripts, but it duplicates parts of the startup load path. Keep both paths in mind when fixing battle NPC behavior.

## Debugging A Missing Spawn

Check these in order:

1. Does `server_zones` load the target `zoneId` on the current map server route?
2. Does `gamedata_actor_class` contain the `actorClassId`?
3. For static actors, did `server_spawn_locations` use the right private area name and level?
4. For battle NPCs, does the spawn row join to a group, pool, and genus row?
5. Is `spawnType` normal, or does it expect director/script-driven spawning?
6. Does the actor appear in `session.instance.update` diagnostics?
7. Does the client accept the actor model/class path, or does it silently fail to display it?

## Adding Test Spawns

For combat and formula work, small test packs are acceptable if they are clearly marked. A good test spawn row should include a comment or naming convention that says it is `Test-only`, should be near a known aetheryte or repeatable fixture, and should have a short removal path once real population evidence exists.

Do not treat temporary Central Thanalan combat fixtures as final 1.23b world population.
