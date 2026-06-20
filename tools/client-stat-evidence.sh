#!/usr/bin/env sh
set -eu

usage() {
  echo "Usage: $0 /path/to/FINAL-FANTASY-XIV"
  echo
  echo "Scans a local, user-owned 1.23b client for visible strings related to"
  echo "leveling, class/job state, bonus points, stats, and combat parameters."
  echo "It prints evidence only; it does not copy client files."
}

if [ "${1:-}" = "--help" ] || [ "${1:-}" = "-h" ]; then
  usage
  exit 0
fi

if [ "$#" -ne 1 ]; then
  usage
  exit 2
fi

ROOT=$1
if [ -d "$ROOT/client" ]; then
  INSTALL_ROOT=$ROOT
  CLIENT_DIR=$ROOT/client
elif [ -d "$ROOT/sqwt" ]; then
  CLIENT_DIR=$ROOT
  INSTALL_ROOT=$(dirname "$ROOT")
else
  echo "CLIENT_STAT_EVIDENCE_FAIL path: expected client directory below: $ROOT" >&2
  exit 2
fi

GAME_EXE=$INSTALL_ROOT/ffxivgame.exe
BOOT_EXE=$INSTALL_ROOT/ffxivboot.exe
DATA_DIR=$INSTALL_ROOT/data
WIDGET_DIR=$CLIENT_DIR/sqwt/widget
WIDGET_C_DIR=$CLIENT_DIR/sqwt/widget_c
SYSTEM_INGAME_DIR=$CLIENT_DIR/sqwt/system/ingame
SCRIPT_DIR=$CLIENT_DIR/script

print_header() {
  echo
  echo "## $1"
}

print_file_state() {
  path=$1
  label=$2
  if [ -f "$path" ]; then
    size=$(wc -c < "$path" | tr -d ' ')
    hash=$(shasum -a 256 "$path" | awk '{print $1}')
    rel=$(printf '%s\n' "$path" | sed "s#^$INSTALL_ROOT/##")
    echo "$label: present path=$rel size=$size sha256=$hash"
  else
    echo "$label: missing path=$path"
  fi
}

visible_strings() {
  strings -a "$1" 2>/dev/null | tr '\015\011\013\014' '    ' || true
}

print_matches() {
  label=$1
  file=$2
  pattern=$3

  if [ ! -f "$file" ]; then
    return
  fi

  matches=$(visible_strings "$file" \
    | grep -Ei "$pattern" \
    | awk 'length($0) <= 180' \
    | sort -u \
    | sed -n '1,80p' || true)

  if [ -n "$matches" ]; then
    rel=$(printf '%s\n' "$file" | sed "s#^$INSTALL_ROOT/##")
    echo
    echo "### $label ($rel)"
    echo "$matches" | sed 's/^/  - /'
  fi
}

print_file_hits() {
  label=$1
  dir=$2
  pattern=$3

  if [ ! -d "$dir" ]; then
    return
  fi

  hits=$(find "$dir" -type f -print0 \
    | xargs -0 grep -aIlE "$pattern" 2>/dev/null \
    | sed "s#^$INSTALL_ROOT/##" \
    | sort \
    | sed -n '1,120p' || true)

  echo
  echo "### $label"
  if [ -n "$hits" ]; then
    echo "$hits" | sed 's/^/  - /'
  else
    echo "  none"
  fi
}

print_data_inventory() {
  if [ "${CLIENT_STAT_INVENTORY_DATA:-0}" != "1" ]; then
    echo "  skipped; set CLIENT_STAT_INVENTORY_DATA=1 to inventory packed DAT files"
    return
  fi

  if [ ! -d "$DATA_DIR" ]; then
    echo "data dir: missing path=$DATA_DIR"
    return
  fi

  dat_count=$(find "$DATA_DIR" -type f -name '*.DAT' 2>/dev/null | wc -l | tr -d ' ')
  data_size=$(du -sh "$DATA_DIR" 2>/dev/null | awk '{print $1}')
  echo "data dir: present path=$(printf '%s\n' "$DATA_DIR" | sed "s#^$INSTALL_ROOT/##") size=$data_size datFiles=$dat_count"

  echo
  echo "### data top-level groups"
  find "$DATA_DIR" -mindepth 1 -maxdepth 1 -type d 2>/dev/null \
    | sed "s#^$INSTALL_ROOT/##" \
    | sort \
    | sed -n '1,80p' \
    | sed 's/^/  - /'

  echo
  echo "### data DAT sample"
  find "$DATA_DIR" -type f -name '*.DAT' 2>/dev/null \
    | sed "s#^$INSTALL_ROOT/##" \
    | sort \
    | sed -n '1,80p' \
    | sed 's/^/  - /'
}

print_optional_data_string_hits() {
  if [ "${CLIENT_STAT_SCAN_DATA:-0}" != "1" ]; then
    echo "  skipped; set CLIENT_STAT_SCAN_DATA=1 to scan DAT visible strings"
    return
  fi

  if [ ! -d "$DATA_DIR" ]; then
    echo "  data dir missing"
    return
  fi

  find "$DATA_DIR" -type f -name '*.DAT' -print0 2>/dev/null \
    | xargs -0 grep -aIlE "$STATE_PATTERN|$BONUS_PATTERN|$STAT_PATTERN|$COMBAT_PATTERN" 2>/dev/null \
    | sed "s#^$INSTALL_ROOT/##" \
    | sort \
    | sed -n '1,160p' \
    | sed 's/^/  - /'
}

STATE_PATTERN='charaWork|battleSave|battleTemp|parameterSave|skillLevel|skillPoint|skillLevelCap|mainSkill|mainSkillLevel|physicalLevel|physicalExp|currentJob|job|class'
BONUS_PATTERN='BonusPoint|bonusPoint|boostPoint|_encodeBonusPoint|_decodeBonusPoint|AssignPoint|Reduction|pointsRemaining|strSpent|vitSpent|dexSpent|intSpent|minSpent|pieSpent'
STAT_PATTERN='Strength|Vitality|Dexterity|Intelligence|Mind|Piety|AttackMagic|HealingMagic|EnhancementMagic|EnfeeblingMagic|MagicAccuracy|MagicEvasion|Accuracy|Evasion|Attack|Defense|Parry|Block'
COMBAT_PATTERN='basePotency|potency|damage|Damage|critical|Critical|miss|parry|block|resist|autoAttack|AutoAttack|battleParameter|generalParameter|EXPPopup|ChainBonus|LevelUp'
UI_PATTERN='PlayerParameterWidget|BonusPointAssignWidget|BonusPointReductionWidget|SkillListWidget|ActionEquipWidget|EquipWidget|ItemDetailWidget|StatusWidget|LevelUpWidget|EXPPopupWidget|ChainBonusEffectWidget'

echo "# Client Stat Evidence Scan"
echo "Install root: $INSTALL_ROOT"
echo "Client dir: $CLIENT_DIR"

print_file_state "$GAME_EXE" "ffxivgame.exe"
print_file_state "$BOOT_EXE" "ffxivboot.exe"
print_file_state "$INSTALL_ROOT/game.ver" "game.ver"
if [ -f "$INSTALL_ROOT/game.ver" ]; then
  echo "game.ver value: $(tr -d '\r\n' < "$INSTALL_ROOT/game.ver")"
fi

print_header "Relevant UI Files"
for file in \
  "$WIDGET_DIR/PlayerParameterWidget.form" \
  "$WIDGET_DIR/BonusPointAssignWidget.form" \
  "$WIDGET_DIR/BonusPointReductionWidget.form" \
  "$WIDGET_C_DIR/BonusPointAssignWidget.form" \
  "$WIDGET_C_DIR/BonusPointReductionWidget.form" \
  "$WIDGET_DIR/SkillListWidget.form" \
  "$WIDGET_DIR/ActionEquipWidget.form" \
  "$WIDGET_DIR/EquipWidget.form" \
  "$WIDGET_DIR/ItemDetailWidget.form" \
  "$WIDGET_DIR/StatusWidget.form" \
  "$SYSTEM_INGAME_DIR/LevelUpWidget.form" \
  "$SYSTEM_INGAME_DIR/EXPPopupWidget.form" \
  "$WIDGET_DIR/ChainBonusEffectWidget.en.form"; do
  print_file_state "$file" "$(basename "$file")"
done

print_header "Executable String Evidence"
print_matches "state and progression symbols" "$GAME_EXE" "$STATE_PATTERN"
print_matches "bonus point symbols" "$GAME_EXE" "$BONUS_PATTERN"
print_matches "stat symbols" "$GAME_EXE" "$STAT_PATTERN"
print_matches "combat and EXP symbols" "$GAME_EXE" "$COMBAT_PATTERN"
print_matches "UI widget symbols" "$GAME_EXE" "$UI_PATTERN"

print_header "Widget String Evidence"
for file in \
  "$WIDGET_DIR/PlayerParameterWidget.form" \
  "$WIDGET_DIR/BonusPointAssignWidget.form" \
  "$WIDGET_DIR/BonusPointReductionWidget.form" \
  "$WIDGET_C_DIR/BonusPointAssignWidget.form" \
  "$WIDGET_C_DIR/BonusPointReductionWidget.form" \
  "$WIDGET_DIR/SkillListWidget.form" \
  "$WIDGET_DIR/ActionEquipWidget.form" \
  "$WIDGET_DIR/EquipWidget.form" \
  "$WIDGET_DIR/ItemDetailWidget.form" \
  "$WIDGET_DIR/StatusWidget.form" \
  "$SYSTEM_INGAME_DIR/LevelUpWidget.form" \
  "$SYSTEM_INGAME_DIR/EXPPopupWidget.form" \
  "$WIDGET_DIR/ChainBonusEffectWidget.en.form"; do
  print_matches "$(basename "$file") state/stat/bonus/combat strings" "$file" "$STATE_PATTERN|$BONUS_PATTERN|$STAT_PATTERN|$COMBAT_PATTERN"
done

print_header "Client File Hits"
print_file_hits "widget files with state/stat/bonus terms" "$WIDGET_DIR" "$STATE_PATTERN|$BONUS_PATTERN|$STAT_PATTERN"
print_file_hits "compiled widget files with state/stat/bonus terms" "$WIDGET_C_DIR" "$STATE_PATTERN|$BONUS_PATTERN|$STAT_PATTERN"
print_file_hits "system ingame files with EXP/level terms" "$SYSTEM_INGAME_DIR" "Level|EXP|Chain|Bonus|Damage"
print_file_hits "script containers with state/stat/bonus terms" "$SCRIPT_DIR" "$STATE_PATTERN|$BONUS_PATTERN|$STAT_PATTERN"

print_header "Packed Data Inventory"
print_data_inventory

print_header "Optional Packed Data String Hits"
print_optional_data_string_hits

print_header "Evidence Notes"
echo "- Visible strings prove the client has names, widgets, or packed references; they do not prove server formula behavior by themselves."
echo "- Widget form hashes are evidence anchors. Do not commit copied client assets."
echo "- Use live traces to confirm which UI paths and parameter names are actually requested by this client."
