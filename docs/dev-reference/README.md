# AetherXIV Developer Reference

This directory is the readable front door for new reverse-engineering and server-development work. It collects the practical things a developer usually asks first: what commands exist, what enemy and spawn data we know about, where zone data lives, how client UI hooks are shaped, and what remains before the stat and battle systems can be called faithful to the 1.23b client.

Use this as a guidebook, not as final historical proof. When a page makes a claim that should drive gameplay behavior, follow the linked source files and evidence ledgers before promoting it into implementation.

## Pages

| Page | What It Covers |
| --- | --- |
| [Getting Started](getting-started-reverse-engineering.md) | A practical workflow for exploring the server, client traces, SQL data, Lua scripts, and live playtests. |
| [GM Commands](gm-commands.md) | Local debug commands, their parameters, what they are useful for, and which ones are stale or dangerous as evidence. |
| [Known Enemies](known-enemies.md) | Spawn aliases, model IDs, temporary NPC spawners, and currently seeded battle NPC data. |
| [Zones and Spawns](zones-and-spawns.md) | Zone tables, private areas, static actor spawns, battle NPC joins, and how a row becomes an actor in the map server. |
| [Client UI and Work Values](client-ui-and-work-values.md) | Known client widget names, `SetWorkValue`, `operateUI`, packet probes, event hooks, and UI-facing state paths. |
| [Data Tables](data-tables.md) | Quick inventory of the SQL tables that matter for zones, actors, combat, stats, and progression. |
| [Battle and Stats Roadmap](battle-and-stats-roadmap.md) | Where stat, EXP, battle command, damage, and formula work currently stands, plus the next implementation steps. |

## Evidence Labels

When adding to these pages, prefer the same evidence labels used by the local stat/progression ledger when that file is present:

| Label | Meaning |
| --- | --- |
| `Client-confirmed` | Observed in the target 1.23b client files or live client behavior. |
| `Trace-confirmed` | Observed in repeatable packet or server traces from the target client. |
| `Public-confirmed` | Supported by original-era official notes or dated sources. |
| `Repo-confirmed` | Current repo behavior, schema, comments, or data. |
| `Inferred` | Strong lead from names, schema, or behavior, but not yet implementation-grade truth. |
| `Hypothesis` | Useful lead only. Do not make final gameplay rules from it. |

## Nearby Source Ledgers

- [Reverse Engineering Tools](../REVERSE_ENGINEERING_TOOLS.md) explains diagnostics, trace categories, client scans, and playtest bridge usage.
- `docs/LEVELING_STATS_SOURCE_OF_TRUTH.md`, when present, is the detailed evidence ledger for progression, attributes, equipment, stat recalculation, EXP, and combat formulas.
- `docs/PATCH_NOTES_SOURCE_INDEX.md`, when present, tracks official patch-note URLs and audited claims.

## Ground Rules

- Keep test data clearly marked as `Test-only`, `Inferred`, or `Hypothesis`.
- Do not treat current server code as historical proof by itself.
- Do not paste full external patch notes or large copyrighted source bodies into the repo. Link and summarize.
- Prefer repeatable traces and small fixtures over broad guesses.
- When adding a new command, table, or formula note, include the source file or trace that led to it.
