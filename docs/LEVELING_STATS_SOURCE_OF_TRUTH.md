# Leveling and Stats Source of Truth

This document is the working evidence ledger for the FFXIV 1.23b leveling, attribute, stat, equipment, and battle-parameter systems.

The rule for this file is simple: do not promote invented behavior. If a behavior cannot be traced to the 1.23b client, original-era public documentation, repeatable packet captures, or existing server data, keep it marked as a hypothesis.

## Evidence Standard

Use these labels when adding or updating findings:

| Label | Meaning | Can drive implementation? |
| --- | --- | --- |
| `Client-confirmed` | Observed directly from the target 1.23b client files or live client behavior. | Yes |
| `Trace-confirmed` | Observed in repeatable packet/server traces from the target client. | Yes |
| `Public-confirmed` | Original-era official notes, archived Lodestone/forum posts, or other dated sources that match 1.23b. | Yes, if source is cited |
| `Repo-confirmed` | Current repo behavior, schema, comments, or data. | Yes for compatibility, not for historical truth |
| `Inferred` | Strong inference from client names, schema shape, packet names, or consistent behavior. | Only behind tests and with a TODO to confirm |
| `Hypothesis` | Plausible but unconfirmed. | No |

Promotion rule: a gameplay rule should ideally have one client or trace source and one independent corroborating source. Repo code alone is not historical proof.

## Scope

These systems are interconnected and should be implemented as one coherent model:

- Class/rank level progression.
- Current class/job state and class change.
- Skill level, skill cap, and XP arrays sent to the client.
- Physical/allocated attributes.
- Derived stats shown in `battleParameter`.
- Gear, weapon, armor, materia, traits, food, buffs, and temporary modifiers.
- HP, MP, TP, attack delay, hit count, block state, and combat-facing values.
- Persistence for class levels, class XP, current class, attributes, current/max HP, and current/max MP.

## Client and Evidence Tooling

| Finding | Status | Source |
| --- | --- | --- |
| Target client version is expected to be `2012.09.19.0001`. | Repo-confirmed | `tools/client-inspect.sh:61` |
| User-owned local client at `/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV` reports `game.ver` value `2012.09.19.0001`. | Client-confirmed | `tools/client-inspect.sh` run on 2026-06-15 |
| Local `ffxivgame.exe` hash is `9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9`; local `ffxivboot.exe` hash is `6a18533d4c3b296ccdedd84c81a3eb99ae5ddb47c3416de60e3414983783efef`. | Client-confirmed | `tools/client-inspect.sh` run on 2026-06-15 |
| Local client contains `client/script/rq9q1797qvs.san`, the static actor script container; its SHA-256 is `bb7306461b1728493242016a16d9dd5257d7512c60e423b017de5ec7aced3d14`. | Client-confirmed | local file scan on 2026-06-15 |
| Local client contains `97` top-level monster asset folders under `client/chara/mon` and `690` top-level cut/event bundle folders under `client/cut`. | Client-confirmed | local file scan on 2026-06-15 |
| Local client contains `client/cut/tan30010/tan30010`; visible strings include Rapture clip classes such as `RaptureAskClip`, `RaptureAutoMoveClip`, `RaptureBlackFadeClip`, `RaptureMapCollisionOnOffClip`, and `RaptureMesClip`. | Client-confirmed | `tools/client-evidence.sh ... tan30010` run on 2026-06-15 |
| Local client contains `client/cut/man0u000/man0u000`; visible strings include `RaptureCaptionClip`, `RaptureBgLoadClip`, and a `#wil_w0_fld01` path/reference. | Client-confirmed | `tools/client-evidence.sh ... man0u000` run on 2026-06-15 |
| Targeted scans for `tan30010` and `man0u000` found no direct script-container string hits. This does not prove absence of logic because client script containers are packed. | Client-confirmed | `tools/client-evidence.sh` runs on 2026-06-15 |
| Client evidence tooling exists, but only indexes visible strings and explicitly does not decompile packed bytecode. | Repo-confirmed | `tools/client-evidence.sh:153`, `docs/REVERSE_ENGINEERING_TOOLS.md:158` |
| Dedicated stat/progression client evidence tooling now scans executable strings, relevant UI form files, and script containers for leveling, job, bonus point, stat, and combat terms. | Repo-confirmed | `tools/client-stat-evidence.sh` |
| Local `ffxivgame.exe` visible strings include `BonusPointAssignWidget`, `BonusPointReductionWidget`, `_encodeBonusPoint`, `_decodeBonusPoint`, `guildleveBoostPoint`, `mainSkillLevel`, and `physicalLevel`. This confirms client-side names and UI machinery exist; it does not prove final server formulas. | Client-confirmed | `tools/client-stat-evidence.sh "/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV"` run on 2026-06-15 |
| Local client UI form anchors exist for `PlayerParameterWidget`, `BonusPointAssignWidget`, `BonusPointReductionWidget`, `SkillListWidget`, `ActionEquipWidget`, `EquipWidget`, `ItemDetailWidget`, `StatusWidget`, `LevelUpWidget`, `EXPPopupWidget`, and `ChainBonusEffectWidget.en`. The executable references the `widget_c` bonus-point form path, and matching `widget_c` files exist locally. | Client-confirmed | `tools/client-stat-evidence.sh` run on 2026-06-15 |
| Local executable strings include job and UI hooks such as `JobChangeReceiver`, `JobQuestCompleteTripleReceiver`, `PlayerParameterWidgetOperator`, `DamagePlate`, `ExpPopup`, and `LevelupPlate`. | Client-confirmed | `tools/client-stat-evidence.sh` run on 2026-06-15 |
| Current repo does not contain the user-owned client files. Any client-side comparison requires a local client root. | Repo-confirmed | repo layout audit |
| The client can request parameter data named `charaWork/exp`; the map server handles opcode `0x012F` for this request. | Repo-confirmed | `Map Server/PacketProcessor.cs:366` |

Required next evidence:

- Run `tools/client-inspect.sh` against a known-good local 1.23b client root.
- Add or extend client evidence scans for `charaWork`, `battleSave`, `battleTemp`, `parameterSave`, `operateUI`, `skillLevel`, `skillPoint`, `skillLevelCap`, `physicalLevel`, stat names, and bonus point UI names.
- Run `tools/client-stat-evidence.sh` after client updates or when validating a new stat/UI hypothesis.
- Capture live traces for login, class change, XP gain, level-up, bonus-point UI open/save/cancel, equip/unequip, buff add/remove, death, recovery, and zone reload.

## Live Character Creation and Opening Content

| Finding | Status | Source |
| --- | --- | --- |
| Returning to the character screen through the exit-game menu succeeded without the prior crash after logout/session cleanup changes. | Client-confirmed / Trace-confirmed | user playtest on 2026-06-16; `/tmp/meteorxiv-traces/map-20260616-170827.jsonl` |
| New character `Ian Smalls` logged into zone `184`, received quest `110009`, started the Ul'dah opening director, displayed the main-scenario start message, and then the client crashed in `ffxivgame.exe`. The server continued to receive idle position/session updates and did not record a matching server exception before the trace ended. | Client-confirmed / Trace-confirmed | user screenshots from 2026-06-16; `/tmp/meteorxiv-traces/map-20260616-170827.jsonl` |
| Earlier in the same run, `Pug Test` entered the Ul'dah opening zone with `17` nearby actors. Later, `Ian Smalls` entered the same opening zone with `14` nearby actors. The missing actors were `ascilia`, `warburton`, and `stocky_stranger`. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260616-170827.jsonl` |
| `Pug Test`'s private battle content spawned actors with IDs `0x45C00001`, `0x45C00002`, and `0x45C00003`, matching the public-zone actor IDs used by `ascilia`, `warburton`, and `stocky_stranger`. This confirmed a private-area actor-ID collision, not a race/class-specific character creation fault. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260616-170827.jsonl` |
| The local client cut bundle `client/cut/man0u005/man0u005` contains visible references to `Warburton` and `Ascilia`. Missing public actors during that cutscene are therefore a strong crash trigger candidate. | Client-confirmed / Inferred | `/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/client/cut/man0u005/man0u005` |
| The user observed that walking to Black Brush after opening/tutorial content showed terrain but not the aetheryte/enemies, while using the teleport command to the Black Brush crystal made the aetheryte and actors appear. This is consistent with stale private-content/public-zone actor state and must be retested after the private actor-ID fix. | Client-confirmed / Inferred | user playtest report on 2026-06-16 |
| Private-area spawned NPCs now allocate actor numbers starting at `0x700`, separating temporary private content actors from low-number public-zone actors while staying within the two-character actor-name encoder range. Duplicate actor IDs inside one area now emit `area.actor.duplicateId` diagnostics and are not added to the actor block twice. | Repo-confirmed | `Map Server/Actors/Area/Area.cs` |

## Current Server Shape

### Class and XP Storage

| Finding | Status | Source |
| --- | --- | --- |
| Class levels are stored in `characters_class_levels` using class-name columns such as `pug`, `gla`, `mrd`, `arc`, `lnc`, `thm`, `cnj`, crafting classes, and gathering classes. | Repo-confirmed | `Data/sql/characters_class_levels.sql` |
| Class XP is stored in `characters_class_exp` with the same class-name columns. | Repo-confirmed | `Data/sql/characters_class_exp.sql` |
| Runtime class state uses `battleSave.skillLevel[52]`, `battleSave.skillLevelCap[52]`, and `battleSave.skillPoint[52]`. | Repo-confirmed | `Map Server/Actors/Chara/BattleSave.cs:24` |
| Runtime also has `physicalLevel` and `physicalExp`, but 1.23b progression should not revive the old physical-level system because patch 1.19 abolished physical levels and moved growth to class level. | Repo-confirmed / Public-confirmed | `Map Server/Actors/Chara/BattleSave.cs:31`; [Patch 1.19 Notes](https://forum.square-enix.com/ffxiv/threads/24910-patch1.19-Patch-1.19-Notes) |
| Player load maps SQL class columns into sparse class IDs using `classId - 1` array positions. | Repo-confirmed | `Map Server/Database.cs:730`, `Map Server/Database.cs:818` |
| `CharacterUtils.GetClassNameForId` maps known sparse class IDs to SQL column names. | Repo-confirmed | `Map Server/Utils/CharacterUtils.cs:120` |

Current class IDs observed in code:

| Class | ID | SQL column |
| --- | ---: | --- |
| Pugilist | 2 | `pug` |
| Gladiator | 3 | `gla` |
| Marauder | 4 | `mrd` |
| Archer | 7 | `arc` |
| Lancer | 8 | `lnc` |
| Thaumaturge | 22 | `thm` |
| Conjurer | 23 | `cnj` |
| Carpenter | 29 | `crp` |
| Blacksmith | 30 | `bsm` |
| Armorer | 31 | `arm` |
| Goldsmith | 32 | `gsm` |
| Leatherworker | 33 | `ltw` |
| Weaver | 34 | `wvr` |
| Alchemist | 35 | `alc` |
| Culinarian | 36 | `cul` |
| Miner | 39 | `min` |
| Botanist | 40 | `btn` |
| Fisher | 41 | `fsh` |

Open verification:

- Confirm whether the client arrays are indexed by raw class ID, `classId - 1`, or another table.
- Confirm which array indices should be `0`, `50`, `0xFF`, or another cap value for inactive/unused classes.
- Confirm how jobs share, mirror, or display class progression in 1.23b.

### Level Progression

| Finding | Status | Source |
| --- | --- | --- |
| The server has a hard-coded 50-entry XP-to-next-level table named `MAXEXP`. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:99` |
| `AddExp` applies an optional percent bonus, sends a class-specific XP message, loops level-ups, updates level and XP packets, and saves XP to SQL. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:2835` |
| `LevelUp` increments `skillLevel[classId - 1]`, increments `state_mainSkillLevel`, sends a level-up message, and equips abilities unlocked at that level. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:2907` |
| Combat EXP helper comments say the formula is based on 1.19 patch notes and is uncertain. | Repo-confirmed | `Map Server/Actors/Chara/Ai/Utils/BattleUtils.cs:792` |
| Patch 1.19 says combat EXP considers base level EXP, player/enemy level difference, party size, enemy type exceptions, link bonuses, EXP chains, Guardian's Aspect, and EXP bonus gear. | Public-confirmed | [Patch 1.19 Notes](https://forum.square-enix.com/ffxiv/threads/24910-patch1.19-Patch-1.19-Notes) |
| Patch 1.21 added rested bonus from inn-room resting mode. Rested bonus increases combat, synthesis, and gathering EXP by `50%`, carries beyond the current level if the gauge overflows, does not decrease on level `50` classes, and does not apply to quest or levequest completion rewards. | Public-confirmed | [Patch 1.21 Notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) |
| Patch 1.21 says class quest EXP rewards are added to the corresponding class's EXP bar even if the quest is completed while playing another class. | Public-confirmed | [Patch 1.21 Notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) |
| A live combat loop advanced Gladiator from level `1` to `2`, displayed `You attain level 2`, learned `Rampart`, then later reached level `3`; the displayed next-level EXP requirement increased from `570` to `700` to `880`. | Client-confirmed | user playtest screenshots from 2026-06-15 |
| The latest trace records repeated `charaWork/exp` client parameter requests after the leveling session, but it still does not emit an explicit server-side EXP grant or level-up diagnostic event. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260615-042554.jsonl` |

Known implementation risks:

- Level-cap checks use `skillLevelCap[classId]` while level and XP use `classId - 1`. This needs client-array confirmation before changing.
- `MAXEXP` needs public/client confirmation before treating it as historical.
- Current EXP model does not prove original 1.23b chain, link, party, rest, food, leve, or level-difference behavior.

### Client XP Packet

| Finding | Status | Source |
| --- | --- | --- |
| `SendCharaExpInfo` responds to `charaWork/exp` by chunking `skillLevel` and `skillLevelCap`. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:1169` |
| First `skillLevel` chunk copies from offset `0` every time instead of `lastPosition`. This likely repeats data in the second chunk. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:1180` |
| `skillLevelCap` chunking uses `lastPosition`. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:1209` |

Required validation:

- Capture actual client request cadence for `charaWork/exp`.
- Confirm exact array length, chunk size, and array-mode flags expected by the client.
- Confirm whether the client asks again after class change/job change and whether the response should include jobs.

## Attributes and Bonus Points

| Finding | Status | Source |
| --- | --- | --- |
| `characters_customattributes` stores `pointsRemaining`, `strSpent`, `vitSpent`, `dexSpent`, `intSpent`, `minSpent`, and `pieSpent`. | Repo-confirmed | `Data/sql/characters_customattributes.sql:25` |
| Runtime has `parameterSave.state_boostPointForSkill[4]`. | Repo-confirmed | `Map Server/Actors/Chara/ParameterSave.cs:34` |
| `BonusPointCommand.lua` calls `operateUI` with hard-coded values and does not save the result. | Repo-confirmed | `Data/scripts/commands/BonusPointCommand.lua:13` |
| Current character load/save does not appear to hydrate or persist `characters_customattributes`. | Repo-confirmed | repo search |
| A new class-scoped allocation table shape exists for 1.20+ behavior. It stores `characterId`, `classId`, remaining points, and spent STR/VIT/DEX/INT/MND/PIE by class without replacing the old unused global table yet. | Repo-confirmed | `Data/sql/characters_class_attributes.sql` |
| Patch 1.20 reintroduced attribute points for Disciples of War and Magic only. Points begin at level `10` with `5` points, then increase by `1` per subsequent level. Points are spent `1:1` on STR, VIT, DEX, INT, MND, and PIE, and issuance/allotment is class-scoped. | Public-confirmed | [Patch 1.20 Notes](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes) |
| Patch 1.20 imposes a level-based per-parameter allotment cap: level `10..11` cap `3`, then +1 cap every two levels through cap `22` at levels `48..49`, and cap `23` at level `50`. | Public-confirmed | [Patch 1.20 Notes](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes) |
| Patch 1.21a allows attribute redistribution for the current class through the Keeper's Hymn item at the guild-mark NPC. | Public-confirmed | [Patch 1.21a Notes](https://forum.square-enix.com/ffxiv/threads/40824-patch1.21a-Patch-1.21a-Notes) |

Required validation:

- Confirm original 1.23b attribute allocation rules: when points are earned, point caps, per-attribute limits, class-specific pools if any, reset behavior, and UI return format.
- Trace `operateUI` inputs and result payloads from the real client.
- Confirm whether `MIN` in schema means Mind and whether `PIT`/`PIE` naming differs between client, scripts, and DB.

Implementation dependency:

- Attribute allocation cannot be finished separately from stat recalculation, because allocated STR/VIT/DEX/INT/MND/PIE feed visible battle parameters and combat formulas.

## Stat and Modifier Model

### Current Structures

| Finding | Status | Source |
| --- | --- | --- |
| `BattleTemp.generalParameter` is a 35-short array used for client-visible stats. | Repo-confirmed | `Map Server/Actors/Chara/BattleTemp.cs:68` |
| C# marks modifier IDs `3..35` as stat changes that should update client battle parameters. | Repo-confirmed | `Map Server/Actors/Chara/Character.cs:336` |
| Player post-update serializes changed modifier values into `charaWork.battleTemp.generalParameter[i]` and sends `charaWork/battleParameter`. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:2364` |
| Lua modifier constants include HP, MP, TP, primary stats, resistances, combat stats, crafting/gathering stats, and many combat modifiers. | Repo-confirmed | `Data/scripts/modifiers.lua:1` |
| Patch 1.20 identifies the six basic parameters and their major effects. STR affects attack power and damage from PGL/GLA/MRD/LNC arms; VIT affects damage taken, enhancement magic potency, max HP, and MRD-arm damage; DEX affects accuracy, block rate, parry, and ARC-arm damage; INT affects attack magic potency and PGL-arm damage; MND affects healing potency, magic accuracy, and GLA/THM/CNJ-arm damage; PIE affects magic evasion, enfeebling potency, max MP, and ARC/LNC/THM/CNJ-arm damage. | Public-confirmed | [Patch 1.20 Notes](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes) |
| Patch 1.20 gives official auto-attack bonus pairs by class: PGL INT+STR, GLA MND+STR, MRD VIT+STR, ARC DEX+PIE, LNC PIE+STR, CNJ MND+PIE, THM MND+PIE. Archer Shot uses the same bonus table. | Public-confirmed | [Patch 1.20 Notes](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes) |

### Stat ID Mismatch

| Stat | C# `BattleTemp` ID | Lua `global.lua` ID | `modifiers.lua` ID |
| --- | ---: | ---: | ---: |
| Attack magic potency | 23 | 24 | 23 |
| Healing magic potency | 24 | 25 | 24 |
| Enhancement magic potency | 25 | 26 | 25 |
| Enfeebling magic potency | 26 | 27 | 26 |
| Magic accuracy | 27 | 28 | 27 |
| Magic evasion | 28 | 29 | 28 |

Status: Repo-confirmed mismatch.

Sources:

- `Map Server/Actors/Chara/BattleTemp.cs:50`
- `Data/scripts/global.lua:105`
- `Data/scripts/modifiers.lua:37`

Required validation:

- Confirm the client-visible `battleTemp.generalParameter` index map from client files or traces.
- Generate C# and Lua constants from one canonical registry after validation.

### Current Calculation Behavior

| Finding | Status | Source |
| --- | --- | --- |
| Base `Character.CalculateBaseStats` only applies HP, HP percent, MP, MP percent, and hit count. It contains TODO comments for race, level, job, and stats. | Repo-confirmed | `Map Server/Actors/Chara/Character.cs:812` |
| `Character.RecalculateStats` now clears previously recalculated contributions, reruns base calculation, and emits `stats.recalc.begin/end` diagnostics. | Repo-confirmed | `Map Server/Actors/Chara/Character.cs` |
| `Player.CalculateBaseStats` applies weapon delay, hit count, block capability, and rough derived stat formulas from primary stats. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:3006` |
| The HP-from-VIT line previously added the numeric enum value for `Vitality`; it now reads the current VIT modifier value. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs` |
| `SubtractMod(Modifier, double)` previously added instead of subtracting, which could make scripted buff removal increase stats. | Repo-confirmed | `Map Server/Actors/Chara/Character.cs` before 2026-06-15 stat pass |
| `SubtractMod(Modifier, double)` now dispatches to the subtracting overload. | Repo-confirmed | `Map Server/Actors/Chara/Character.cs` |
| Level-up now emits `player.level.up` diagnostics and requests stat recalculation for the active class. This wires the lifecycle, but real stat growth still depends on confirmed base stat tables and final formulas. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs` |
| EXP grants now emit `player.exp.grant.begin`, `battle.exp.calculated`, and `player.exp.grant.end` diagnostics, including class, level, base EXP, bonus percent, chain/link values, and old/new EXP state. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs`, `Map Server/Actors/Chara/Ai/Utils/BattleUtils.cs` |
| Stat recalculation now has a `stats.recalc.begin/end` trace path and a recalculated-mod contribution bucket so prototype derived stats can be removed and re-applied without drifting upward on repeated recalculations. | Repo-confirmed | `Map Server/Actors/Chara/Character.cs`, `Map Server/Actors/Chara/Player/Player.cs` |
| Player stat recalculation now has separate base-profile, class-allocation, and equipment-param layers before the existing derived formulas. Base profile rows are optional and trace `stats.base.missing` when absent. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs`, `Map Server/Database.cs`, `Data/sql/server_player_base_stats.sql` |
| Class-scoped attribute allocation now has a load path from `characters_class_attributes`; the allocation UI/save path is still not implemented. | Repo-confirmed | `Map Server/Database.cs`, `Data/sql/characters_class_attributes.sql` |
| Equipment changes now call `RecalculateStats("equip")` instead of calling the old additive `CalculateBaseStats` path directly. | Repo-confirmed | `Data/scripts/commands/EquipCommand.lua` |
| Physical and spell mitigation now emit `battle.damage.input` and `battle.damage.result` diagnostics with attacker/defender level, major attacker stats, defender defense/vitality, dLVL, and pre/post mitigation amounts. | Repo-confirmed | `Map Server/Actors/Chara/Ai/Utils/BattleUtils.cs` |
| Auto-attack currently uses a hard-coded command `basePotency = 100`; `Data/scripts/commands/autoattack/default.lua` sets `action.amount = skill.basePotency` before mitigation. This means normal auto-attack starting damage does not yet include level, weapon damage power, attack, or primary-stat scaling. | Repo-confirmed | `Map Server/Actors/Chara/Ai/State/AttackState.cs:130`, `Data/scripts/commands/autoattack/default.lua:9` |
| In the live level-up test, HP/MP stayed visually at `1000/115` from levels `1..3`, and player auto-attack results continued clustering around `99` normal hits and `74` parried hits after leveling. | Client-confirmed / Trace-confirmed | user playtest screenshots from 2026-06-15; `/tmp/meteorxiv-traces/map-20260615-042554.jsonl` |
| In the 2026-06-16 repeated `wharf_rat` test, the player leveled from class `3` level `6` to level `7`. EXP grants were stable, chain bonuses applied, and post-level-up base EXP dropped from `84` to `73` against the same level `1` target. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260616-155417.jsonl` |
| The same trace showed `stats.base.missing` for class/job `3`, tribe `3`, levels `6` and `7`. Recalculation ran on login, equip, combat updates, and level-up, but STR/VIT/DEX/INT/MND/PIE and derived attack/accuracy/defense/evasion remained `0`. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260616-155417.jsonl` |
| Attribute allocation correctly contributed nothing at levels `6` and `7`: under the 1.20 rules, allocatable points begin at level `10`, and the trace reported `earnedPoints=0` and `statCap=0`. | Trace-confirmed / Public-confirmed | `/tmp/meteorxiv-traces/map-20260616-155417.jsonl`; [Patch 1.20 Notes](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes) |
| Equipment recalculation saw `6..7` equipped items during the test, but every tracked equipment stat contribution was `0`. This means the current gear contributed no recognized HP/MP/primary-stat bonuses, or the live equipped item IDs need raw bonus-pair diagnostics before changing the mapping. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260616-155417.jsonl` |
| Normal player hits still entered mitigation as `100` and landed as `99` after the level `6 -> 7` transition. This confirms damage is still dominated by the hard-coded auto-attack potency and not by level/base-stat growth. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260616-155417.jsonl` |
| The 2026-06-16 trace also showed stale persisted current HP on login (`1900`) while max HP was `1000`, before later combat/recalc state settled at or below max. This should be treated as an HP/MP clamp/persistence cleanup issue, separate from base stat truth. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260616-155417.jsonl` |
| Patch 1.21 introduced seven jobs. Initial job quests require one class at level `30` and one specified secondary class at level `15`; jobs are activated by equipping a soul crystal in the dedicated slot or with `/job`. | Public-confirmed | [Patch 1.21 Notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) |
| Patch 1.21 says job changes automatically change base attributes, class-spent attribute points carry to the associated job, and job level/EXP are shared with the base class. | Public-confirmed | [Patch 1.21 Notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) |
| Patch 1.21 changed how player attributes feed weaponskill attack power, increasing the maximum attribute contribution to damage. | Public-confirmed | [Patch 1.21 Notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) |

Known implementation risks:

- Direct script mutations can still drift if an effect adds/removes asymmetrically, but recalculated base/allocation/equipment/trait/derived contributions are now cleared and reapplied idempotently.
- Buffs and food/medicine/rested effects are not fully separated into layers yet.
- The current formulas are useful prototypes but not verified 1.23b truth.
- Damage scaling cannot be fixed only in `LevelUp`; it needs confirmed base stat tables, equipment contribution, and the auto-attack/weaponskill damage model wired into the stat layers.

Required model:

- Base layer: race/tribe, class, level/rank, base HP/MP/primary stats.
- Allocation layer: player-spent STR/VIT/DEX/INT/MND/PIE.
- Equipment layer: weapon, armor, accessory, shield, ammo/throwing, materia, durability/condition if applicable.
- Trait layer: active class/job traits unlocked by level.
- Status layer: buffs, debuffs, food, rest, sanction/company effects, temporary combat effects.
- Final layer: deterministic derived stats, client-visible `generalParameter`, combat-facing hidden modifiers, current/max HP/MP/TP adjustments.

Recalculation invariant:

- Running stat recalculation twice without input changes must produce exactly the same values.
- Equip then unequip should restore the previous values.
- Apply then remove a buff should restore the previous values.

## Equipment, Traits, and Status Effects

| Finding | Status | Source |
| --- | --- | --- |
| Equipment item data loads up to ten `paramBonusType` and `paramBonusValue` pairs. | Repo-confirmed | `Map Server/DataObjects/ItemData.cs:425` |
| Weapon data loads `frequency`, `damageInterval`, damage fields, and DPS. | Repo-confirmed | `Map Server/DataObjects/ItemData.cs:505` |
| `Player.CalculateBaseStats` currently uses main-hand weapon damage attribute, delay, and frequency. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs:3016` |
| Equipment param bonuses with IDs in the known `15001 + Modifier` range are now applied during stat recalculation as a recalculated equipment layer. | Repo-confirmed | `Map Server/Actors/Chara/Player/Player.cs` |
| Class change, job change, level-up, and equipment changes now call stat recalculation. | Repo-confirmed | `Data/scripts/commands/EquipCommand.lua`, `Map Server/Actors/Chara/Player/Player.cs` |

Required validation:

- Confirm which equipment fields the 1.23b client displays and which the server must calculate.
- Confirm whether the server should trust client-side display data for item stat text or calculate all stat effects from DB.
- Confirm item bonus type IDs against original client parameter names.

## Persistence Contract

Current persisted pieces:

- Class levels: `characters_class_levels`.
- Class XP: `characters_class_exp`.
- Current class and basic character state: `characters`.
- HP/MP/current main skill: `characters_parametersave`.
- Legacy custom attributes schema exists but is not wired: `characters_customattributes`.
- 1.20+ class-scoped attribute allocation schema exists and is loaded: `characters_class_attributes`.
- Confirmed base stat schema exists but is intentionally unseeded until base values are client/trace/public-confirmed: `server_player_base_stats`.

Required persistence behavior:

- Load all class levels and XP.
- Load current class/job state.
- Load or initialize allocated attributes.
- Recalculate max HP/MP and visible stats on login from source layers.
- Preserve current HP/MP only where historically appropriate; do not let stale max values become the stat source of truth.
- Save class level, XP, current class, allocated attributes, and current HP/MP at known save points.

## Enemy Spawn Dependency

Leveling and stat validation need a repeatable combat loop. The current server can load battle NPCs, but the field population data is almost empty.

| Finding | Status | Source |
| --- | --- | --- |
| Normal NPC/world-object spawns load from `server_spawn_locations` and are already populated for Ul'dah and Thanalan. | Repo-confirmed | `Map Server/WorldManager.cs:371`, `Data/sql/server_spawn_locations.sql` |
| Battle NPCs load from `server_battlenpc_spawn_locations`, joined through groups, pools, and genus data. | Repo-confirmed | `Map Server/WorldManager.cs:446` |
| Only seven battle NPC spawn rows exist in the seed SQL: two test rats, three scripted wolves, and two opening-scene allies. | Repo-confirmed | `Data/sql/server_battlenpc_spawn_locations.sql:44` |
| Central Thanalan is zone `170`; Ul'dah city is zone `175`; Ul'dah opening battle zones are `184..188`. | Repo-confirmed | `Data/sql/server_zones.sql:91`, `Data/sql/server_zones.sql:96`, `Data/sql/server_zones.sql:104` |
| Existing Central Thanalan battle group `1` uses pool `1`, script `wharf_rat`, and zone `170`. | Repo-confirmed | `Data/sql/server_battlenpc_groups.sql:53` |
| Existing pool `1` uses actor class `2104001`, class path `/Chara/Npc/Monster/Lemming/LemmingStandard`, and display/model data already present in actor class data. | Repo-confirmed | `Data/sql/server_battlenpc_pools.sql:50`, `Data/sql/gamedata_actor_class.sql:4726` |
| The local 1.23b client has a monster asset tree at `client/chara/mon`; this confirms model assets exist locally, but not final spawn placement or enemy level. | Client-confirmed | local file scan on 2026-06-15 |
| The repo actor-class table already contains many `/Chara/Npc/Monster/...` rows, including Cactus, Bug, Lemming, Bat, Wolf, Mole, Sprite, and other monster families. | Repo-confirmed | `Data/sql/gamedata_actor_class.sql` |
| A live playtest loaded exactly `2` monsters at map startup, matching the two normal Central Thanalan `wharf_rat` rows. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260615-033201.jsonl`, map terminal log from 2026-06-15 |
| Teleporting from Ul'dah to Central Thanalan requested zone `170`, spawn type `2`, position `(35.98447, 200.1, -480.8309)`, then completed into zone actor `0xAA`. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260615-033201.jsonl` |
| Both `wharf_rat` actors spawned as `LemmingStandard` using parent script `./scripts/base//chara/npc/monster/lemming/LemmingStandard.lua`; no unique `wharf_rat.lua` script exists. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260615-033201.jsonl` |
| The live client accepted the Black Brush aetheryte interaction after teleport and displayed the aetheryte model and menu. | Client-confirmed | user playtest screenshot from 2026-06-15 |
| The live client accepted targeting and auto-attacking both `wharf_rat` battle NPCs. The server trace shows out-of-range blocks, physical hits/misses, enemy counterattack, TP gain, two deaths, and two `mobkill` signals. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260615-033201.jsonl` |
| The first rat death was a critical player hit for `119` damage against `80` HP; the second rat sequence included a player miss, a `100` damage enemy hit, then a `99` damage player hit against `80` HP. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260615-033201.jsonl` |
| The live client displayed `150` EXP for the first rat, then `EXP chain #1`, an `80` second chain timer, and `180 (+20%)` EXP for the second rat. | Client-confirmed | user playtest screenshot from 2026-06-15 |
| Current dev traces do not emit an explicit EXP grant event; EXP values above are client-visible and corroborated by `BattleUtils.AddBattleBonusEXP`, not directly JSONL-confirmed. | Repo-confirmed | `Map Server/Actors/Chara/Ai/Utils/BattleUtils.cs:920` |
| Before the respawn parity fix, normal startup-loaded battle NPCs did not call `SetRespawnTime(reader.GetUInt32("respawnTime"))`; the direct `SpawnBattleNpcById` path did. This likely explained why the two normal Central Thanalan rats did not return even though SQL group `1` has `respawnTime = 10`. | Repo-confirmed | `Map Server/WorldManager.cs`, `Data/sql/server_battlenpc_groups.sql:53` |
| Normal startup-loaded battle NPCs now set database `respawnTime`, apply pool/genus/spawn modifier lists through the same helper used by `SpawnBattleNpcById`, then recalculate base/current stats. | Repo-confirmed | `Map Server/WorldManager.cs` |
| Battle NPC lifecycle diagnostics now include `respawnTime`, `despawnTime`, `bnpcId`, `uniqueId`, zone, and position data on death; they also emit `battle.despawn.start`, `battle.respawn.ready`, `battle.respawn.spawn`, and `battle.respawn.skipped`. | Repo-confirmed | `Map Server/Actors/Chara/Npc/BattleNpc.cs` |
| After the respawn parity fix, a live playtest trace confirmed `13` completed `wharf_rat` respawn cycles. Each cycle reused the same actor id, emitted `battle.death`, `battle.despawn.start`, `battle.respawn.ready`, then `battle.respawn.spawn`, and restored the rat to `80/80` HP. Observed timing was about `10` seconds death-to-despawn plus `10` seconds despawn-to-spawn. | Trace-confirmed | `/tmp/meteorxiv-traces/map-20260615-042554.jsonl` |

Required validation before real population:

- Confirm which low-level enemies belonged near Ul'dah/Central Thanalan in 1.23b.
- Confirm actor class IDs, display names, model paths, and genus/category for those enemies from client files or public-era sources.
- Confirm spawn coordinates from client data, archived maps, repeatable manual observation, or clearly marked temporary test placement.
- Confirm whether field enemies should be persistent zone spawns, event/director spawns, leve spawns, or private-area/tutorial spawns.
- Trace actor instance packets after spawning to verify the client accepts model/class/path/state values.
- For future enemy rows, confirm killed BattleNPCs emit `battle.death`, `battle.despawn.start`, `battle.respawn.ready`, and `battle.respawn.spawn` in that order. If `battle.respawn.skipped` appears, confirm the underlying SQL intentionally uses `respawnTime = 0`.

Allowed temporary implementation:

- A small, clearly marked local test pack can seed a few low-level enemies around known aetheryte coordinates only to unblock combat/XP traces.
- Temporary rows must be marked `Hypothesis` or `Test-only`; they must not be treated as final 1.23b population.

## Public Source Status

Accepted public sources:

| Source ID | Type | URL | Claims supported |
| --- | --- | --- | --- |
| `OFF-1.19` | Official patch notes, 2011-09-30 | https://forum.square-enix.com/ffxiv/threads/24910-patch1.19-Patch-1.19-Notes | Physical levels abolished; rank/skill point terminology changed to class level/EXP; attributes develop automatically by class and level; combat EXP factors; battle and item stat reforms; PIE affects max MP; VIT/PIE from gear affect max HP/MP. |
| `OFF-1.20` | Official patch notes, 2011-12-14 | https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes | Class-scoped DoW/DoM attribute points; level-based allotment caps; six basic parameter effects; official auto-attack bonus stat pairs by class. |
| `OFF-1.21` | Official patch notes, 2012-03-08 | https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes | Rested bonus; class quest EXP target class behavior; job unlock requirements; soul-crystal job switching; class/job action restrictions; job base attribute changes; class allocation carries to job; job EXP/level shared with base class; 1.21 weaponskill attribute contribution change. |
| `OFF-1.21A` | Official patch notes, 2012-03-26 | https://forum.square-enix.com/ffxiv/threads/40824-patch1.21a-Patch-1.21a-Notes | Keeper's Hymn reset path for current-class attribute allotment. Needs re-open/re-archive because the forum endpoint may intermittently fail. |
| `COMM-SEIKEN-LNC-1.21` | Original-era community testing, official forum, 2012-03-26 | https://forum.square-enix.com/ffxiv/threads/36412-STR-PIE-ATK-Testing/page2 | Lancer/Dragoon post-1.21 stat-damage leads: STR and PIE weaponskill contribution ranges, soft/hard caps, attack-power contribution, early auto-attack caps, and similar behavior claimed for WAR/MNK with VIT/INT substitutions. |
| `COMM-KANICAN-PDT` | Original-era community testing, LiveJournal | https://kanican.livejournal.com/55915.html | Physical damage taken lead: defense lowers damage linearly; dLVL affects mitigation slope and damage floor; VIT behaves like about `0.67` defense for physical damage; proposed physical-damage-taken formula. |

Source rules:

- Official patch notes are implementation-grade for behavior they state directly.
- Community testing is useful for formula shape, fixture design, and initial provisional constants, but it should not be the only source for final combat math.
- Patch notes before 1.19 explain removed systems; they should not reintroduce physical-level behavior into a 1.23b target unless the 1.23b client still requires a packet/display placeholder.
- Patch notes after 1.21 may override earlier mechanics. For 1.23b parity, keep checking 1.22, 1.22a, 1.22b, 1.23, 1.23a, and 1.23b before locking formulas.

Confirmed public facts that should drive implementation now:

- 1.23b should use class level/EXP, not physical level, as the progression source of truth.
- Base attributes must change automatically with class and level.
- DoW/DoM classes earn class-scoped attribute points starting at level `10`; jobs inherit associated class allotment.
- Jobs share level and EXP with the base class and switch by soul crystal or `/job`.
- The stat layer must support official class-specific damage bonus pairs for auto-attack and Shot.
- Level-up should trigger a stat rebuild because class/level is an input to base attributes, HP, MP, and combat-facing values.

Public sources still needed:

- Official 1.22, 1.22a, 1.22b, 1.23, 1.23a, and 1.23b patch notes for later overrides.
- Exact XP-to-next-level table and base EXP table.
- Exact class/job/race/level base attribute and HP/MP tables.
- Exact 1.23b equipment parameter IDs, materia behavior, item condition scaling, food/medicine rules, and rested-bonus packet representation.
- Better archived copies for sources that are intermittently unavailable.

## Implementation Work Packages

Do these in evidence-first order. The goal is not just to make numbers go up; the goal is to make each number traceable to a layer and each layer traceable to evidence.

### 1. Add progression and stat diagnostics

Add diagnostics before changing formulas broadly:

- `player.exp.grant`: source, class/job, base EXP, bonuses, chain/link/rest/food/equipment modifiers, final EXP, old/new EXP, old/new level.
- `player.level.up`: class, old/new level, awarded actions, points earned, recalc trigger, client packets sent.
- `player.class.change` / `player.job.change`: old/new class/job, weapon/soul crystal source, level/EXP shared state, stat rebuild result.
- `stats.recalc.begin` / `stats.recalc.end`: reason, class/job, level, race/tribe, source layers, final visible stats, HP/MP handling.
- `stats.layer.snapshot`: base, allocation, equipment, materia, traits, status, food/rest, and final layers.
- `battle.damage.input` / `battle.damage.result`: action, potency, weapon damage/delay/frequency, class bonus stats, attack, target defense/vitality, dLVL, variance, crit/block/parry/miss, final damage.

### 2. Build canonical class, job, and stat registries

- Create one source of truth for class IDs, job IDs, SQL columns, display abbreviations, base-class job links, and class/job unlock requirements.
- Create one source of truth for modifier/general-parameter IDs, then generate or validate C# and Lua constants from it.
- Do not fix the C# vs Lua magic-stat mismatch by guessing. First trace or client-confirm `battleTemp.generalParameter`.

### 3. Separate progression from removed physical level

- Treat `physicalLevel` and `physicalExp` as legacy/client-structure fields only unless a trace proves the 1.23b client displays them.
- Make class level and class EXP the only progression source for combat, attributes, actions, and HP/MP.
- Keep class quest EXP and job EXP routing aligned with official 1.21 behavior: class quests can grant EXP to the corresponding class, and jobs share the base class bar.

### 4. Implement class/job and attribute allocation state

- Model jobs as overlays on their associated class: shared level/EXP, different base attributes, class-spent attribute points carried over.
- Store attribute allotment by class and stat, with remaining/spent/cap data. Current SQL is too narrow if it is global-only; extend only after confirming UI payload needs.
- Enforce official point rules: DoW/DoM only, first `5` points at level `10`, `+1` per later level, `1:1` spend, per-parameter cap by level.
- Wire `BonusPointCommand.lua` to real state after tracing `operateUI` result packets.
- Implement Keeper's Hymn reset only after the base allocation loop is working.

### 5. Replace stat calculation with idempotent layers

Rebuild stats from clean inputs every time:

- Base layer: race/tribe, class/job, level, base HP/MP, base STR/VIT/DEX/INT/MND/PIE.
- Allocation layer: class-spent attributes.
- Equipment layer: weapon, shield, armor, accessories, materia, durability/condition scaling if confirmed.
- Trait/action layer: passive traits and class/job-specific modifiers.
- Status layer: buffs, debuffs, food, medicine, rested state, temporary combat effects.
- Derived layer: attack power, defense, accuracy, evasion, magic stats, block/parry, max HP/MP, delay, hit count, hidden combat modifiers, and client-visible `generalParameter`.

Required invariant: running recalculation twice with the same inputs produces identical output; equip/unequip and buff/remove restore the prior snapshot.

### 6. Replace hard-coded auto-attack and weaponskill damage

- Remove the current hard-coded auto-attack `basePotency = 100` path once diagnostics and stat layers exist.
- Feed damage with confirmed inputs: weapon damage, delay/frequency, class bonus stats, attack power, action/WS potency, target defense/vitality, level difference, random variance, crit, block, parry, miss, and resist state.
- Start formulas behind a clearly named provisional mode if needed. Official patch notes define which stats matter; community tests can seed initial coefficients such as STR/PIE/ATK rates, but fixtures must be allowed to replace them.
- Keep normal attack, Shot, weaponskill, magic, and enemy damage paths separate enough that each can match its own evidence.

### 7. Preserve and verify EXP behavior while stats change

- Keep the currently working level-up and chain behavior intact while adding diagnostics.
- Validate `MAXEXP` against client display and public/client evidence before treating it as final.
- Add explicit support later for party size, links, enemy exceptions, rested bonus, food/equipment bonuses, leve behavior, and class-quest target EXP.

### 8. Promote test-only data only after evidence

- Keep the Central Thanalan rat loop as the first combat fixture because respawn, EXP, level-up, and damage are already trace-confirmed.
- Add more field enemies only as `Test-only` or `Hypothesis` rows until population, level, name, model, and spawn placement are independently confirmed.

## Required Test Fixtures

Core progression:

- Class ID to SQL column mapping.
- Class array index mapping.
- XP add without level-up.
- XP rollover across one level.
- XP rollover across multiple levels.
- XP cap behavior.
- Ability unlock on level-up.
- Class quest EXP awarded to the class quest's class.
- Job EXP awarded to and read from the base class.
- `charaWork/exp` chunk serialization.

Class/job/attribute state:

- Class change with existing and newly initialized class.
- Job switch on/off with base class level/EXP preserved.
- Job switch changes base attributes while carrying class allotment.
- Attribute allocation save/load by class.
- Attribute point earning at level `10` and later levels.
- Attribute per-parameter cap enforcement.
- Attribute reset restores spendable points for current class.

Stat calculation:

- Login stat rebuild from persisted source data.
- Stat recalculation idempotence.
- Level-up changes base stats, max HP/MP, and derived visible stats.
- Current HP/MP transform on max HP/MP change, once confirmed.
- Equip/unequip returns to original stats.
- Buff add/remove returns to original stats.
- Food/medicine/rested state layer application and removal.
- C# and Lua general-parameter constants match the confirmed client map.

Combat:

- Auto-attack damage changes when official class bonus stats change.
- Gladiator auto-attack uses MND+STR, not only STR.
- Lancer auto-attack uses PIE+STR, and Archer Shot uses DEX+PIE.
- Player attack damage changes with weapon damage, attack power, and level.
- Enemy damage taken by player changes with defense, vitality, and dLVL.
- Damage recalculation does not drift across repeated stat rebuilds.
- Wharf-rat level `1..3` live fixture keeps respawn, targeting, EXP, and level-up behavior working while damage starts scaling.

## Open Questions

- What exact class/skill array layout does the client expect?
- Does 1.23b still expose `physicalLevel` anywhere visible, or are the fields only retained for older client structures?
- What is the exact XP-to-next-level table for the target version?
- What are the exact base stat tables by race, clan/tribe, class, job, and level?
- What are the exact HP/MP growth tables, and how should current HP/MP transform when max HP/MP changes?
- How are jobs represented in `skillLevel`, `skillPoint`, soul-crystal state, action bars, and UI?
- What is the exact `operateUI` result payload for bonus points?
- Does the current `characters_customattributes` table need to become class-scoped to match official behavior?
- What is the exact `battleTemp.generalParameter` index map?
- Which stats are pure display values and which affect combat server-side?
- Which item data fields are authoritative for stat bonuses?
- How do item condition, optimal rank/class, materia, HQ values, and dated gear affect stat contribution?
- What exact formulas, caps, variance, floors, crit, miss, parry, block, and resist rules can be confirmed from original sources or captures?
