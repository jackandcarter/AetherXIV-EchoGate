# Data Tables

This page is a fast inventory of SQL tables that matter for reverse engineering and server behavior. Row counts are from the current seed files, not from a live database.

## Zones and Actors

| Table | Current Rows | Purpose |
| --- | ---: | --- |
| `server_zones` | `111` | Zone metadata, route, class path, music, flags, navmesh load flag. |
| `server_zones_spawnlocations` | `19` | Zone-in spawn points by zone/private area/spawn type. |
| `server_zones_privateareas` | `8` | Private areas attached to parent zones. |
| `server_seamless_zonechange_bounds` | `13` | Seamless zone transition bounds. |
| `server_spawn_locations` | `999` | Static actor, NPC, object, and town population spawn rows. |
| `gamedata_actor_class` | `7984` | Actor class IDs, class paths, display names, event conditions. |
| `gamedata_actor_pushcommand` | `145` | Push-command metadata for actor interaction conditions. |

## Battle NPCs

| Table | Current Rows | Purpose |
| --- | ---: | --- |
| `server_battlenpc_spawn_locations` | `7` | Individual battle NPC spawn points. |
| `server_battlenpc_groups` | `4` | Spawn group behavior: script, level range, respawn, HP/MP, zone, allegiance. |
| `server_battlenpc_pools` | `4` | Actor class, genus, current job, aggro/link/immunity, skill and spell lists. |
| `server_battlenpc_genus` | `65` | Enemy family stats, movement, detection, kindred, resistances, element. |
| `server_battlenpc_pool_mods` | `7` | Pool-level modifiers. |
| `server_battlenpc_spawn_mods` | `3` | Spawn-level modifiers. |
| `server_battlenpc_genus_mods` | `0` | Genus-level modifiers. |
| `server_battlenpc_skill_list` | `3` | Battle NPC skill-list data. Current seed includes Yda's Gridania tutorial companion list. |
| `server_battlenpc_spell_list` | `3` | Battle NPC spell-list data. Current seed includes Papalymo's Gridania tutorial companion list. |

## Battle Commands, Traits, and Effects

| Table | Current Rows | Purpose |
| --- | ---: | --- |
| `server_battle_commands` | `151` | Action metadata: job/class, level, targeting, AOE, potency, hits, proc, range, status, cast/recast, costs, animations, combo, accuracy, action type. |
| `server_battle_traits` | `77` | Passive trait metadata: class/job, level, modifier, bonus. |
| status effect data | varies | Loaded through `Database.LoadGlobalStatusEffectList()` and Lua status scripts. |

Command scripts live under:

- `Data/scripts/commands/ability/`
- `Data/scripts/commands/magic/`
- `Data/scripts/commands/weaponskill/`
- `Data/scripts/commands/autoattack/`

Status effect scripts live under:

- `Data/scripts/effects/`

## Player Progression and Stats

| Table | Status | Purpose |
| --- | --- | --- |
| `characters_class_levels` | Seeded schema | Per-character class levels using class-name columns. |
| `characters_class_exp` | Seeded schema | Per-character class EXP using class-name columns. |
| `characters_parametersave` | Seeded schema | Current main skill, HP/MP, and parameter-save state. |
| `characters_customattributes` | Legacy schema | Old/global custom attributes. Exists but is not final 1.20+ behavior. |
| `characters_class_attributes` | New schema, currently empty | Class-scoped attribute allocation: remaining points and spent STR/VIT/DEX/INT/MND/PIE. |
| `server_player_base_stats` | New schema, currently empty | Optional evidence-backed base HP/MP/primary stat profiles by class/job, tribe, and level. |

Current important rule: `server_player_base_stats` is intentionally unseeded until base values are client/trace/public-confirmed. Missing rows emit `stats.base.missing` diagnostics instead of inventing stat growth.

## Items and Equipment

Useful item data is loaded through C# data objects, especially:

- `Map Server/DataObjects/ItemData.cs`

Equipment items can carry up to ten `paramBonusType` / `paramBonusValue` pairs. Weapon data includes frequency, damage interval, damage fields, and DPS. Current player stat recalculation applies equipment param bonuses whose IDs fit the known `15001 + Modifier` range.

## Table Change Checklist

Before adding rows to a gameplay table:

1. Identify whether the row is durable behavior, a temporary fixture, or a hypothesis.
2. Confirm the C# loader actually reads the columns you are adding.
3. Confirm joins are complete. Battle NPC rows need spawn, group, pool, and genus rows.
4. Confirm client acceptance with live traces when actor classes, models, or UI state are involved.
5. Add comments or docs when a row is test-only.

Existing databases should receive non-destructive table/data updates through
`Data/sql/migrations/` and `./tools/apply-db-migrations.sh` rather than by
dropping and reimporting local playtest data.
