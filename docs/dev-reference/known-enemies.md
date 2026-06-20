# Known Enemies

This page collects the enemy/model knowledge currently exposed by repo scripts and SQL. The largest friendly list comes from the `!spawnnpc` GM command, which maps aliases to actor class IDs for quick local testing.

The alias list is `Repo-confirmed` as a developer convenience. It is not proof that those enemies spawn at a specific retail location, level, or population density.

## Quick Spawn Path

Use `!spawnnpc <alias>` to spawn a level 52 battle NPC near the player. The script currently:

- Looks up an alias in `Data/scripts/commands/gm/spawnnpc.lua`.
- Creates actor class `2104001` first, then calls `ChangeNpcAppearance(modelId)`.
- Sets HP/MP and selected stats/mods directly.
- Sets `state_mainSkillLevel = 52`.

That means this command is good for visual/combat experiments, but not a faithful final enemy data path.

## Seeded Battle NPCs

Current durable battle NPC rows load from:

- `server_battlenpc_spawn_locations`
- `server_battlenpc_groups`
- `server_battlenpc_pools`
- `server_battlenpc_genus`
- `server_battlenpc_*_mods`

The seed SQL currently has only a small set of battle NPC spawn rows. The known useful combat fixture is Central Thanalan zone `170`, where group `1` uses pool `1`, script `wharf_rat`, and actor class `2104001`. In live testing this accepted targeting, auto-attack, EXP, death, despawn, and respawn.

See [Zones and Spawns](zones-and-spawns.md) for the durable data model.

## Enemy Restoration Evidence

The next enemy-population pass should be treated as a data-ledger project, not a guess-and-seed project. The client and repo can reveal useful pieces of truth, but no single source currently proves full retail battle NPC placement, levels, stats, abilities, and density.

Current findings:

- `Repo-confirmed`: `server_spawn_locations` contains normal NPCs, aetherytes, doors, and interactable world objects. These rows are good zone anchors, but they are not battle NPC population rows.
- `Repo-confirmed`: battle NPC population loads through `server_battlenpc_spawn_locations`, `server_battlenpc_groups`, `server_battlenpc_pools`, `server_battlenpc_genus`, and modifier tables.
- `Repo-confirmed`: current durable battle NPC population is still tiny and includes temporary/test combat fixtures. Do not treat those fixtures as final 1.23b population.
- `Client-confirmed`: the local 1.23b client has monster model assets under `client/chara/mon`, and the repo actor-class table contains many `/Chara/Npc/Monster/...` rows. This can confirm that a model family exists, but not where it spawned or what level it should be.
- `Client-confirmed / Repo-confirmed`: the `rq9q1797qvs.san` / `staticactors.bin` path currently used by the map server contains static actor command/path entries. The current parser reads IDs and path strings, not battle NPC coordinates. It should not be cited as proof of monster spawn positions.
- `Repo-confirmed`: `staticactors.bin` includes monster command paths such as monster range attacks, weaponskills, and abilities. These are useful leads for command/action mapping, but not complete enemy AI tables by themselves.
- `Trace-confirmed`: the client accepted the current Central Thanalan test battle NPC lifecycle after the respawn parity fixes: target, attack, kill, despawn, respawn, and retarget.

Promotion rules for real enemy data:

1. Use existing schema first. Do not add parallel spawn/genus/action schemas unless the current schema is proven unable to represent the client behavior.
2. Record an evidence label for every new enemy row or action mapping.
3. Split identity, placement, and behavior evidence. A confirmed model does not prove a confirmed spawn coordinate; a confirmed public-era location does not prove stats or actions.
4. Prefer client files, current repo SQL, repeatable traces, and original-era public sources over modern AI summaries or memory.
5. Mark unverified restoration rows as `Hypothesis` or `Test-only` until a playtest trace proves the client accepts the actor, name, targeting, combat, death, despawn, and respawn flow.

## Spawn Aliases

| Alias | Actor Class / Model ID |
| --- | ---: |
| `ahriman` | `2201704` |
| `amaljaa` | `2206502` |
| `angler` | `2204507` |
| `apkallu` | `2202902` |
| `atomos` | `2111002` |
| `basilisk` | `2200708` |
| `bat` | `2104113` |
| `bird` | `2201208` |
| `boar` | `2201505` |
| `bomb` | `2201611` |
| `buffalo` | `2200802` |
| `cactuar` | `2200905` |
| `chigoe` | `2105613` |
| `chimera` | `2308701` |
| `clouddragon` | `2208101` |
| `coblyn` | `2202103` |
| `couerl` | `2203203` |
| `crab` | `2107613` |
| `cyclops` | `2210701` |
| `diremite` | `2101108` |
| `dodo` | `9111263` |
| `doe` | `2200303` |
| `drake` | `2202209` |
| `elemental` | `2105104` |
| `flan` | `2103404` |
| `fungus` | `2205907` |
| `garlean` | `2207005` |
| `garuda` | `2209501` |
| `garudahelper` | `2209516` |
| `ghost` | `2204317` |
| `gnat` | `2200604` |
| `goat` | `2102312` |
| `goblin` | `2210301` |
| `golem` | `2208901` |
| `gong` | `1200050` |
| `goobbue` | `2103301` |
| `hedgemole` | `2105709` |
| `helper` | `2310605` |
| `hippogryph` | `2200405` |
| `ifrit` | `2207302` |
| `ifrithotair` | `2207310` |
| `imp` | `2202607` |
| `ixal` | `2206434` |
| `jellyfish` | `2105415` |
| `juggernaut` | `6000243` |
| `kobold` | `2206629` |
| `lantern` | `1200329` |
| `lemur` | `2200505` |
| `mammet` | `6000246` |
| `meteor` | `2210903` |
| `mog` | `2210408` |
| `monolith` | `2209506` |
| `morbol` | `2201002` |
| `nael` | `2210902` |
| `nail` | `2207307` |
| `ogre` | `2202502` |
| `plume` | `2209502` |
| `puk` | `2200112` |
| `qiqirn` | `2206304` |
| `raptor` | `2200205` |
| `rat` | `9111275` |
| `salamander` | `2201302` |
| `skeleton` | `2201902` |
| `slug` | `2104205` |
| `snurble` | `2204403` |
| `spriggan` | `2290036` |
| `swarm` | `2105304` |
| `sylph` | `2206702` |
| `titan` | `2107401` |
| `toad` | `2203107` |
| `trap` | `2202710` |
| `treant` | `2202801` |
| `wisp` | `2209903` |
| `wolf` | `2201429` |
| `wyvern` | `2203801` |
| `yarzon` | `2205520` |
| `zombie` | `2201807` |

## How To Promote A Test Enemy To Durable Data

Before adding permanent population data:

1. Confirm model/actor class ID from client files, existing `gamedata_actor_class`, or repeatable live acceptance.
2. Confirm intended zone and approximate coordinates.
3. Decide whether it should be static zone population, event/director spawn, leve spawn, or private-area/tutorial spawn.
4. Add a battle NPC pool/group/spawn row rather than relying on `!spawnnpc`.
5. Mark uncertain rows as `Test-only` or `Hypothesis`.
6. Trace the client accepting the model, targeting, combat, death, despawn, and respawn.
