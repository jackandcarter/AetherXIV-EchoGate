#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

APPLY=0
CHARACTER_ID=""
CHARACTER_NAME=""
TOWN="uldah"

usage() {
  cat <<'EOF'
Usage: tools/reset-opening-tutorial.sh (--character-id ID | --character-name NAME) [--town uldah|gridania|limsa] [--apply]

Resets a local development character to the selected opening tutorial.
Dry-run is the default. Add --apply to modify MariaDB.

Environment:
  DB_APP_HOST  default 127.0.0.1
  DB_APP_PORT  default 3306
  DB_NAME      default ffxiv_server
  DB_APP_USER  default meteor
  DB_APP_PASS  default meteor_dev
EOF
}

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --apply)
      APPLY=1
      shift
      ;;
    --character-id)
      CHARACTER_ID="${2:-}"
      shift 2
      ;;
    --character-name)
      CHARACTER_NAME="${2:-}"
      shift 2
      ;;
    --town)
      TOWN="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$CHARACTER_ID" && -z "$CHARACTER_NAME" ]]; then
  echo "missing --character-id or --character-name" >&2
  usage >&2
  exit 2
fi

DB_APP_HOST="${DB_APP_HOST:-${DB_HOST:-127.0.0.1}}"
DB_APP_PORT="${DB_APP_PORT:-${DB_PORT:-3306}}"
DB_NAME="${DB_NAME:-ffxiv_server}"
DB_APP_USER="${DB_APP_USER:-meteor}"
DB_APP_PASS="${DB_APP_PASS:-${METEOR_DB_PASS:-meteor_dev}}"

if command -v mariadb >/dev/null 2>&1; then
  MYSQL_BIN="${MYSQL_BIN:-mariadb}"
elif command -v mysql >/dev/null 2>&1; then
  MYSQL_BIN="${MYSQL_BIN:-mysql}"
else
  echo "missing mysql/mariadb client" >&2
  exit 2
fi

MYSQL_ARGS=(-h "$DB_APP_HOST" -P "$DB_APP_PORT" -u "$DB_APP_USER" --batch --raw "$DB_NAME")

run_sql() {
  MYSQL_PWD="$DB_APP_PASS" "$MYSQL_BIN" "${MYSQL_ARGS[@]}" -e "$1"
}

sql_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\'/\\\'}"
  printf '%s' "$value"
}

case "$TOWN" in
  uldah|ul\'dah)
    TOWN="uldah"
    QUEST_ID=110009
    ZONE_ID=184
    HOMEPOINT=1280031
    POSITION_X=5.364327
    POSITION_Y=196.0
    POSITION_Z=133.6561
    ROTATION=-2.849384
    ;;
  gridania)
    QUEST_ID=110005
    ZONE_ID=166
    HOMEPOINT=1280061
    POSITION_X=369.5434
    POSITION_Y=4.21
    POSITION_Z=-706.1074
    ROTATION=-1.26721
    ;;
  limsa|limsa-lominsa|limsa_lominsa)
    TOWN="limsa"
    QUEST_ID=110001
    ZONE_ID=193
    HOMEPOINT=1280001
    POSITION_X=0.016
    POSITION_Y=10.35
    POSITION_Z=-36.91
    ROTATION=0.025
    ;;
  *)
    echo "unknown town: $TOWN" >&2
    usage >&2
    exit 2
    ;;
esac

if [[ -n "$CHARACTER_NAME" ]]; then
  escaped_name="$(sql_escape "$CHARACTER_NAME")"
  CHARACTER_ID="$(run_sql "SELECT id FROM characters WHERE name='${escaped_name}' ORDER BY id LIMIT 1;" | tail -n 1)"
  if [[ -z "$CHARACTER_ID" ]]; then
    echo "character not found: $CHARACTER_NAME" >&2
    exit 1
  fi
fi

if ! [[ "$CHARACTER_ID" =~ ^[0-9]+$ ]]; then
  echo "invalid character id: $CHARACTER_ID" >&2
  exit 2
fi

CURRENT_SQL="
SELECT id, name, currentZoneId, currentPrivateArea, currentPrivateAreaType, positionX, positionY, positionZ, rotation
FROM characters
WHERE id=${CHARACTER_ID};
SELECT characterId, slot, questId, currentPhase, questFlags
FROM characters_quest_scenario
WHERE characterId=${CHARACTER_ID}
ORDER BY slot;
"

RESET_SQL="
SET @characterId := ${CHARACTER_ID};
SET @questId := ${QUEST_ID};
SET @zoneId := ${ZONE_ID};
SET @homepoint := ${HOMEPOINT};

DELETE FROM characters_quest_completed
WHERE characterId=@characterId
  AND questId IN (110001, 110005, 110009);

DELETE FROM characters_quest_scenario
WHERE characterId=@characterId
  AND questId IN (110001, 110005, 110009);

SELECT COALESCE(MIN(candidate.slot), 0) INTO @slot
FROM (
  SELECT 0 AS slot UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3
  UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7
  UNION ALL SELECT 8 UNION ALL SELECT 9 UNION ALL SELECT 10 UNION ALL SELECT 11
  UNION ALL SELECT 12 UNION ALL SELECT 13 UNION ALL SELECT 14 UNION ALL SELECT 15
) AS candidate
LEFT JOIN characters_quest_scenario scenario
  ON scenario.characterId=@characterId AND scenario.slot=candidate.slot
WHERE scenario.slot IS NULL;

INSERT INTO characters_quest_scenario
  (characterId, slot, questId, currentPhase, questData, questFlags)
VALUES
  (@characterId, @slot, @questId, 0, '{}', 0);

UPDATE characters
SET currentZoneId=@zoneId,
    currentPrivateArea='',
    currentPrivateAreaType=0,
    destinationZoneId=0,
    destinationSpawnType=0,
    positionX=${POSITION_X},
    positionY=${POSITION_Y},
    positionZ=${POSITION_Z},
    rotation=${ROTATION},
    homepoint=@homepoint,
    playTime=0
WHERE id=@characterId;

SELECT id, name, currentZoneId, positionX, positionY, positionZ, rotation, homepoint
FROM characters
WHERE id=@characterId;
SELECT characterId, slot, questId, currentPhase, questFlags
FROM characters_quest_scenario
WHERE characterId=@characterId AND questId=@questId;
"

echo "Opening tutorial reset"
echo "Database: $DB_APP_USER@$DB_APP_HOST:$DB_APP_PORT/$DB_NAME"
echo "Character id: $CHARACTER_ID"
echo "Town: $TOWN"
echo "Quest id: $QUEST_ID"
echo
echo "Current state:"
run_sql "$CURRENT_SQL"

if [[ "$APPLY" -ne 1 ]]; then
  echo
  echo "Dry run only. Planned SQL:"
  echo "$RESET_SQL"
  echo
  echo "Add --apply to execute."
  exit 0
fi

echo
echo "Applying reset..."
run_sql "$RESET_SQL"
echo "Reset complete."
