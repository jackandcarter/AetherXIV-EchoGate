# Battle and Stats Roadmap

This page summarizes where the stat, EXP, battle, and formula work currently stands. The deeper evidence ledger belongs in `docs/LEVELING_STATS_SOURCE_OF_TRUTH.md` when that local file is present.

## Current Status

| Area | Status |
| --- | --- |
| Class level and EXP storage | Existing SQL and runtime arrays are wired. Current code uses sparse class IDs mapped to `skillLevel[classId - 1]` and `skillPoint[classId - 1]`. |
| EXP grants and level-up | Working enough for the Central Thanalan rat fixture. EXP grants, class-specific messages, level-up, action unlock, and SQL saves exist. |
| Class change | Loads hotbars, updates current class/main skill, sends packets, saves current class, and triggers stat recalculation. |
| Job overlay | Basic current-job setter exists. Final job behavior still needs soul crystal, action restrictions, shared class EXP/level validation, and job base stat confirmation. |
| Attribute allocation | New class-scoped schema and load path exist. UI save/result path is not implemented. |
| Base stat profiles | Optional schema and lookup path exist. Table is intentionally empty until values are confirmed. |
| Stat recalculation | Recalculation clears previous recalculated contributions and reapplies base/allocation/equipment/derived layers. This prevents simple drift. |
| Equipment stats | Equipment `paramBonusType/value` pairs in the known range are applied as a recalculated layer. |
| Traits | Battle traits load and apply when class/job and level match. |
| Battle commands | 151 command rows load, Lua command scripts are cached, targeting/cast/recast/action metadata exists. |
| Auto-attack | Functional but still provisional. It uses a hard-coded `Attack` command with `basePotency = 100`. |
| Damage formulas | Physical/spell mitigation, hit/miss/crit/block/parry/resist paths exist, but coefficients are provisional and not final 1.23b truth. |
| Diagnostics | `stats.*`, `battle.*`, `player.exp.*`, and respawn lifecycle diagnostics now exist for evidence capture. |

## What Is Working Enough To Use

- Live combat fixture around Central Thanalan rats.
- Player targeting and auto-attacking battle NPCs.
- Enemy counterattacks.
- EXP display and simple EXP chain behavior.
- Level-up messaging and action learning.
- Battle NPC death, despawn, and respawn traces.
- Stat recalculation lifecycle on login, level-up, class change, job change, and equipment changes.
- Equipment stat contribution experiments when item data exposes compatible parameter IDs.

## What Is Not Done

| Gap | Why It Matters |
| --- | --- |
| Confirmed class/job/race/tribe base stat tables | Without these, HP/MP and primary stat growth cannot be historically correct. |
| Bonus-point UI result decoding | Attribute allocation cannot be completed until the client payload is known. |
| Canonical stat ID registry | C# and Lua magic-stat indices currently disagree. |
| Exact XP table and bonus rules | Current EXP behavior works for tests but needs client/public confirmation. |
| Real auto-attack formula | Current auto-attack starts from hard-coded potency, not weapon damage, class bonus stats, attack power, and level. |
| Weaponskill and magic formula parity | Scripts and metadata exist, but exact 1.23b formulas, caps, variance, and attribute contribution need evidence. |
| Job equipment/soul-crystal workflow | Current job switching is not the final 1.21+ flow. |
| Full battle NPC population | Durable field enemy population is mostly empty. |
| Food, medicine, materia, durability, rested bonus, buffs by layer | These need separated stat layers and evidence-backed rules. |

## Important Current Formula Leads

These are implementation leads, not final proof:

- Patch 1.20 defines which primary stats affect major outcomes.
- Patch 1.20 gives official auto-attack and Shot bonus stat pairs by class.
- Patch 1.21 says jobs share base class level/EXP and carry base class attribute allocation while changing base attributes.
- Current mitigation code uses simplified dLVL and defense/vitality relationships.
- Current physical hit flow checks miss, then stoneskin, then crit, then block, then parry, then hit.
- Current magic hit flow handles stoneskin, resist, crit, then hit.
- Current block/parry/crit/resist formulas include comments that they are approximate.

## Next Work Packages

Do these in evidence-first order.

### 1. Finish Diagnostics Before More Formula Work

The current diagnostics are a good start. Next useful additions:

- A single `stats.layer.snapshot` event that dumps base, allocation, equipment, traits, status, and final values.
- Explicit weapon inputs in `battle.damage.input`: weapon damage, delay, frequency, damage type, and class bonus stats.
- EXP source labels for mob kill, quest, leve, rested, food, equipment, chain, and link bonuses.
- Class/job change diagnostics that include old/new class, old/new job, soul crystal state, level/EXP source, and stat rebuild result.

### 2. Build Canonical Registries

Create one source of truth for:

- Class IDs, job IDs, SQL columns, and class/job links.
- Modifier IDs and client-visible `battleTemp.generalParameter` indices.
- Equipment parameter IDs.

Do not resolve the C# vs Lua magic-stat mismatch by hand-editing only one side. Confirm the client map first.

### 3. Decode Bonus Point UI

The allocation rules are well supported by 1.20 patch notes, but the server still needs the exact client interaction:

- Open assignment UI.
- Spend points.
- Save.
- Cancel.
- Reset with Keeper's Hymn later.

Once decoded, wire the result to `characters_class_attributes`, recalculate stats, and persist by class.

### 4. Seed Base Stats Only With Evidence

`server_player_base_stats` is ready as a shape, but empty by design. Fill it only when values are backed by:

- Client files or repeatable live observations.
- Original-era public sources.
- Trace-confirmed behavior.

Rows should include source/confidence metadata so future developers know why a value exists.

### 5. Replace Hard-Coded Auto-Attack

Remove the hard-coded `basePotency = 100` path only after stat layers and diagnostics are good enough to compare before/after.

The replacement should account for:

- Weapon damage and delay/frequency.
- Class bonus stats from official 1.20 notes.
- Attack power.
- Level difference.
- Target defense/vitality.
- Hit/miss/crit/block/parry.
- Variance and caps once confirmed.

### 6. Expand Combat Fixtures Carefully

The rat loop is useful because it already exercises many systems. Add more enemies only when each row has a clear purpose:

- Damage fixture.
- Magic fixture.
- Ranged/Shot fixture.
- Block/parry fixture.
- Status effect fixture.
- Respawn/population fixture.

Mark every temporary row as `Test-only` until final population evidence exists.

## Acceptance Targets

A future stat/battle pass is in good shape when:

- Recalculating stats twice with no input changes produces identical values.
- Equip then unequip restores previous values.
- Buff add then remove restores previous values.
- Level-up changes appropriate base stats and HP/MP once base rows exist.
- Class and job share level/EXP correctly.
- Jobs can change base attributes while carrying class allocation.
- Auto-attack damage changes with weapon, class bonus stats, attack, level, and target defense.
- Client-visible parameter values match the server's final stat snapshot.
- Formula changes are backed by fixtures, traces, and source labels.
