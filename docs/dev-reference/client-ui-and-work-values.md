# Client UI and Work Values

The server often communicates with the client through named `charaWork`, `playerWork`, actor work values, event functions, UI calls, and property packets. This page collects the useful names and probes we currently know about.

## Known Client/UI Names

The dedicated client stat scan has found these useful anchors in the local 1.23b client and UI form files:

| Name | Why It Matters |
| --- | --- |
| `PlayerParameterWidget` | Character parameter/stat panel. |
| `BonusPointAssignWidget` | Attribute allocation UI. |
| `BonusPointReductionWidget` | Attribute reset/reduction UI. |
| `_encodeBonusPoint`, `_decodeBonusPoint` | Client-side bonus point data handling lead. |
| `SkillListWidget` | Class/job skill list display. |
| `ActionEquipWidget` | Action equip UI. |
| `EquipWidget` | Equipment UI. |
| `ItemDetailWidget` | Item stat/detail display. |
| `StatusWidget` | Character status UI. |
| `LevelUpWidget` | Level-up display. |
| `EXPPopupWidget` | EXP gain display. |
| `ChainBonusEffectWidget.en` | EXP chain display. |
| `JobChangeReceiver` | Job-change UI/event hook. |
| `PlayerParameterWidgetOperator` | Parameter UI operation lead. |
| `DamagePlate`, `ExpPopup`, `LevelupPlate` | Combat/progression feedback display leads. |
| `mainSkillLevel`, `physicalLevel` | Client-visible names; physical level appears as a legacy structure name, not a target gameplay system. |

The detailed evidence belongs in `docs/LEVELING_STATS_SOURCE_OF_TRUTH.md` when that local ledger is present.

## `SetWorkValue`

The key dev hook is:

```text
!workvalue <actorName|@t> <workName> <uiFunc> <value>
```

Lua path:

- `Data/scripts/commands/gm/workvalue.lua`

C# path:

- `Map Server/Actors/Actor.cs`

What it does:

- Resolves a target actor by name or current target with `@t`.
- Converts the value to number, boolean, or string.
- Calls `targetActor:SetWorkValue(player, workName, uiFunc, value)`.
- Reports whether the named work value and datatype were accepted.

Use this to probe client UI behavior when you know or suspect a work value path and callback name. Record every successful probe with:

- target actor
- work name
- UI function
- value and datatype
- visible client result
- trace file, if diagnostics were enabled

## `operateUI` and Bonus Points

`Data/scripts/commands/BonusPointCommand.lua` currently calls `operateUI` with hard-coded values and does not save a result. The client confirms bonus-point widgets and encode/decode names exist, but the server still needs the real result payload.

Current status:

| Piece | Status |
| --- | --- |
| Client has bonus-point UI names | `Client-confirmed` |
| Server has old/global custom attributes table | `Repo-confirmed`, not wired as final behavior |
| Server has new class-scoped allocation table shape | `Repo-confirmed` |
| Server loads `characters_class_attributes` if present | `Repo-confirmed` |
| UI result save path | Not implemented |
| Exact `operateUI` result shape | Unknown |

Next evidence needed:

- Trace opening the bonus-point UI.
- Trace save/cancel/assignment actions.
- Decode the result payload.
- Confirm point rules against 1.20+ behavior.
- Save class-scoped allocation and trigger stat recalculation.

## Useful State Paths

| Path | Meaning |
| --- | --- |
| `charaWork.parameterSave.state_mainSkill[0]` | Current class/main skill ID. |
| `charaWork.parameterSave.state_mainSkillLevel` | Current class level shown to client. |
| `charaWork.battleSave.skillLevel[]` | Class level array. |
| `charaWork.battleSave.skillPoint[]` | Class EXP array. |
| `charaWork.battleSave.skillLevelCap[]` | Class level cap array. |
| `charaWork.battleTemp.generalParameter[]` | Client-visible battle/stat parameter array. |
| `playerWork.comboNextCommandId[]` | Combo continuation IDs. |
| `playerWork.comboCostBonusRate` | Combo cost bonus state. |
| `playerWork.restBonusExpRate` | Rested bonus display/state lead. |

## Packet and Event Probes

| Tool | Use |
| --- | --- |
| `!sendpacket` | Sends packet fixtures from `./packets/` for packet experiments. |
| `!questevent` | Calls quest-owned event functions and logs phase/flag state around the call. |
| `!endevent` | Closes active event/script state. |
| `!setnpcls` | Changes NPC Linkpearl state and helps test linkpearl-driven UI. |
| `!queststate` | Prints quest, private area, and NPC Linkpearl state. |

## Stat Parameter Caution

There is a known mismatch between C# and Lua names for magic stat indices:

| Stat | C# `BattleTemp` ID | Lua `global.lua` ID | `modifiers.lua` ID |
| --- | ---: | ---: | ---: |
| Attack magic potency | `23` | `24` | `23` |
| Healing magic potency | `24` | `25` | `24` |
| Enhancement magic potency | `25` | `26` | `25` |
| Enfeebling magic potency | `26` | `27` | `26` |
| Magic accuracy | `27` | `28` | `27` |
| Magic evasion | `28` | `29` | `28` |

Do not "fix" this by guessing. First confirm the client-visible `battleTemp.generalParameter` index map from client files or traces, then create one canonical registry and validate both C# and Lua against it.
