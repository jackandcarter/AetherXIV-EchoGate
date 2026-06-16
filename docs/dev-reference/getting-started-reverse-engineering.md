# Getting Started

This page is the quick path for a developer who wants to understand what exists, reproduce a behavior, and make a change without getting lost in the whole Project Meteor codebase.

## Mental Model

The server is a mix of C# runtime systems, Lua content scripts, and SQL seed data.

| Layer | Where To Look | What It Usually Controls |
| --- | --- | --- |
| Map runtime | `Map Server/` | Actors, combat, packets, zones, events, player state, AI, stat recalculation. |
| World runtime | `World Server/` | Routing, zone session lookup, world-facing zone metadata. |
| Lua scripts | `Data/scripts/` | Commands, NPC behavior, events, abilities, magic, effects, quest logic. |
| SQL data | `Data/sql/` | Zones, actors, battle NPCs, commands, traits, player persistence, item and game data. |
| Tools and docs | `tools/`, `docs/` | Client evidence scans, reset helpers, diagnostics, playtest workflows. |

## First Files To Read

Start with these before changing behavior:

| Topic | File |
| --- | --- |
| Diagnostics and live testing | `docs/REVERSE_ENGINEERING_TOOLS.md` |
| Stat, EXP, and battle evidence | `docs/LEVELING_STATS_SOURCE_OF_TRUTH.md` |
| Official patch-note index | `docs/PATCH_NOTES_SOURCE_INDEX.md` |
| Zone and spawn loading | `Map Server/WorldManager.cs` |
| Player stat and progression work | `Map Server/Actors/Chara/Player/Player.cs` |
| Shared character stat recalculation | `Map Server/Actors/Chara/Character.cs` |
| Battle command metadata | `Map Server/Actors/Chara/Ai/BattleCommand.cs` |
| Damage and hit logic | `Map Server/Actors/Chara/Ai/Utils/BattleUtils.cs` |
| Lua modifier constants | `Data/scripts/modifiers.lua` |
| Lua global/client parameter constants | `Data/scripts/global.lua` |

## Reverse-Engineering Loop

1. Pick one behavior.
2. Find the current C# loader or Lua script path.
3. Find the SQL rows that feed it.
4. Run a small live playtest with diagnostics enabled.
5. Compare server traces, client behavior, and source notes.
6. Mark the finding with an evidence label.
7. Implement only the smallest behavior that the evidence supports.
8. Add a regression test or repeatable manual fixture when possible.

## Useful Diagnostics

Enable structured diagnostics with either `--dev-diagnostics` or `METEOR_DEV_DIAGNOSTICS=1`.

Important trace families for this work:

| Trace Family | Why It Matters |
| --- | --- |
| `client.*` | Client readiness, position, target, lock target, packet classification. |
| `zone.*` | Zone-in, zone change, private area resolution, handoffs. |
| `event.*` | Client event starts, updates, kicks, event data, Lua resumes. |
| `quest.*` | Quest phase, flags, save, and marker updates. |
| `stats.*` | Stat recalculation begin/end and layer application. |
| `battle.*` | Damage input/result/application, battle action completion, respawn lifecycle. |
| `player.exp.*` | EXP grants and level-up state. |

See [Reverse Engineering Tools](../REVERSE_ENGINEERING_TOOLS.md) for the full trace list and playtest bridge commands.

## Good First Fixtures

| Fixture | Why It Is Useful |
| --- | --- |
| Ul'dah opening flow | Exercises quest events, private areas, NPC Linkpearl state, actor visibility, event packets, and city tutorial scripts. |
| Black Brush / Central Thanalan rat loop | Exercises zone change, battle NPC spawn, targeting, auto-attack, EXP, chain bonus, death, despawn, and respawn. |
| Class change and equipment change | Exercises stat recalculation, hotbar load, current class persistence, and client `charaWork` updates. |
| Bonus point UI probe | Exercises `operateUI`, client widget behavior, attribute allocation, and save payload discovery. |

## Development Safety

- Treat `Data/scripts/commands/gm/` as local test tooling.
- Keep temporary spawn rows clearly marked and easy to remove.
- Do not use command behavior as proof of retail behavior.
- Keep source-of-truth evidence separate from provisional implementation notes.
- If a behavior depends on exact formulas, add diagnostics first.
