#!/usr/bin/env python3
import argparse
import glob
import json
import os
import sys
from collections import Counter, defaultdict


DEFAULT_TRACE_DIR = os.environ.get(
    "AETHER_DEV_DIAGNOSTICS_DIR",
    os.environ.get("METEOR_DEV_DIAGNOSTICS_DIR", "/tmp/meteorxiv-traces"),
)


def iter_paths(inputs):
    if not inputs:
        inputs = [DEFAULT_TRACE_DIR]

    for item in inputs:
        if os.path.isdir(item):
            matches = sorted(glob.glob(os.path.join(item, "*.jsonl")))
            for match in matches:
                yield match
        elif any(ch in item for ch in "*?[]"):
            for match in sorted(glob.glob(item)):
                yield match
        else:
            yield item


def load_events(paths):
    events = []
    errors = []
    for path in paths:
        try:
            with open(path, "r", encoding="utf-8") as handle:
                for number, line in enumerate(handle, 1):
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        event = json.loads(line)
                    except json.JSONDecodeError as exc:
                        errors.append(f"{path}:{number}: invalid JSON: {exc}")
                        continue
                    event["_path"] = path
                    event["_line"] = number
                    events.append(event)
        except OSError as exc:
            errors.append(f"{path}: {exc}")
    return events, errors


def matches_filters(event, args):
    if args.category and event.get("category") not in args.category:
        return False
    if args.player:
        player = str(event.get("player", ""))
        actor_name = str(event.get("actorName", ""))
        if args.player.lower() not in player.lower() and args.player.lower() not in actor_name.lower():
            return False
    return True


def compact(event):
    category = event.get("category", "?")
    parts = [event.get("timestamp", ""), event.get("server", ""), category]
    for key in (
        "player",
        "actorName",
        "targetName",
        "targetActorName",
        "ownerName",
        "sourceName",
        "textOwnerName",
        "mode",
        "state",
        "reason",
        "eventName",
        "function",
        "waitType",
        "signal",
        "commandName",
        "skillName",
        "paramName",
        "params",
        "classification",
        "areaKind",
    ):
        value = event.get(key)
        if value not in (None, ""):
            parts.append(f"{key}={value}")

    for key in (
        "questId",
        "questName",
        "phase",
        "oldPhase",
        "newPhase",
        "flags",
        "textId",
        "log",
        "npcLsId",
        "isCalling",
        "isExtra",
        "unchanged",
        "zone",
        "fromZone",
        "toZone",
        "destinationZone",
        "privateArea",
        "fromPrivateArea",
        "toPrivateArea",
        "requestedPrivateArea",
        "spawnType",
        "destinationSpawnType",
        "loginSpawnType",
        "isLogin",
        "targets",
        "amount",
        "beforeHp",
        "afterHp",
        "handled",
        "resolved",
        "instanceActorCount",
        "instanceActorCountAfter",
    ):
        if key in event:
            parts.append(f"{key}={event[key]}")

    return " | ".join(str(part) for part in parts if part != "")


def find_blockers(events):
    blockers = []
    signal_emits = Counter()
    waits = {}
    first_autoattack_state = None
    first_battle_tutorial = None
    first_battle_output = None
    first_battle_damage = None
    first_battle_death = None
    zone_load_completions = []
    client_load_acks_by_player = defaultdict(list)
    client_positions_by_player = defaultdict(list)

    for index, event in enumerate(events):
        category = event.get("category")

        if category == "lua.signal.emit":
            signal_emits[str(event.get("signal", ""))] += 1

        if category in ("lua.wait", "lua.wait.register"):
            coroutine = event.get("coroutine")
            if coroutine is not None:
                waits[str(coroutine)] = event

        if category in ("lua.signal.resume", "lua.time.resume", "lua.resume"):
            coroutine = event.get("coroutine")
            if coroutine is not None:
                waits.pop(str(coroutine), None)

        if category == "lua.script.resolve" and event.get("resolved") is False:
            blockers.append(("missing-script", event))

        if category == "lua.commandScript.resolve" and event.get("resolved") is False:
            blockers.append(("missing-command-script", event))

        if category == "client.parameter.request" and event.get("handled") is False:
            blockers.append(("unhandled-parameter-request", event))

        if category == "event.start.ownerMissing":
            blockers.append(("missing-event-owner", event))

        if category in ("zone.in.blocked", "zone.change.content.blocked"):
            blockers.append(("zone-load-blocked", event))

        if category in ("zone.in.end", "zone.change.local.end", "zone.change.content.end"):
            zone_load_completions.append((index, event))

        if category in ("client.zoneInComplete", "client.position"):
            client_load_acks_by_player[str(event.get("player", ""))].append(index)

        if category == "client.position":
            client_positions_by_player[str(event.get("player", ""))].append(index)

        if category == "event.runFunction" and "processTtrBtl" in str(event.get("params", "")):
            if first_battle_tutorial is None:
                first_battle_tutorial = event

        if category == "lua.resumeMissing":
            blockers.append(("lua-resume-missing", event))

        if category == "battle.command.noTargets":
            blockers.append(("battle-command-no-targets", event))

        if category == "battle.command.start" and int(event.get("targets", 1) or 0) == 0:
            blockers.append(("battle-command-zero-targets", event))

        if category == "battle.autoattack.state":
            if first_autoattack_state is None:
                first_autoattack_state = event
            if event.get("state") == "start" and event.get("target") in (None, "", "0x0", 0):
                blockers.append(("autoattack-start-without-target", event))

        if category in ("battle.command.start", "battle.command.action", "battle.action.finish"):
            if first_battle_output is None:
                first_battle_output = event

        if category == "battle.damage.apply":
            if first_battle_damage is None:
                first_battle_damage = event
            before = event.get("beforeHp")
            after = event.get("afterHp")
            amount = event.get("amount")
            if before == after and amount not in (None, 0, "0"):
                blockers.append(("damage-no-hp-change", event))
            if event.get("maxHp") in (0, "0"):
                blockers.append(("target-zero-max-hp", event))

        if category == "battle.death" and first_battle_death is None:
            first_battle_death = event

        if category == "quest.phase" and first_battle_tutorial is not None:
            if event.get("newPhase") in (10, "10") and first_battle_damage is None and first_battle_death is None:
                blockers.append(("tutorial-phase-advanced-without-combat", event))

    if first_autoattack_state is not None and first_battle_output is None:
        blockers.append(("combat-state-without-actions", first_autoattack_state))

    if first_battle_tutorial is not None and first_battle_damage is None:
        blockers.append(("tutorial-battle-without-damage", first_battle_tutorial))

    for index, event in zone_load_completions:
        player = str(event.get("player", ""))
        ack_indexes = client_load_acks_by_player.get(player, [])
        position_indexes = client_positions_by_player.get(player, [])
        if not any(ack_index > index for ack_index in ack_indexes):
            blockers.append(("zone-load-no-client-complete", event))
        elif not any(position_index > index for position_index in position_indexes):
            blockers.append(("zone-load-no-client-position", event))

    for event in waits.values():
        if event.get("waitType") == "_WAIT_SIGNAL":
            signal = str(event.get("signal", ""))
            if signal and signal_emits[signal] == 0:
                blockers.append(("signal-wait-without-emit", event))
            else:
                blockers.append(("open-signal-wait", event))
        elif event.get("waitType") == "_WAIT_EVENT":
            blockers.append(("open-event-wait", event))
        elif event.get("waitType") == "_WAIT_TIME":
            blockers.append(("open-time-wait", event))

    return blockers


def print_summary(events):
    categories = Counter(event.get("category", "?") for event in events)
    servers = Counter(event.get("server", "?") for event in events)
    print(f"Events: {len(events)}")
    print("Servers:")
    for server, count in servers.most_common():
        print(f"  {server}: {count}")
    print("Categories:")
    for category, count in categories.most_common():
        print(f"  {category}: {count}")


def print_blockers(events, max_items):
    blockers = find_blockers(events)
    if not blockers:
        print("Blockers: none detected by current heuristics")
        return

    counts = Counter(name for name, _ in blockers)
    print("Blockers:")
    for name, count in counts.most_common():
        print(f"  {name}: {count}")

    print("Blocker Samples:")
    for name, event in blockers[:max_items]:
        print(f"  [{name}] {compact(event)}")


def main():
    parser = argparse.ArgumentParser(description="Summarize AetherXIV Core dev diagnostic JSONL traces.")
    parser.add_argument("paths", nargs="*", help="Trace JSONL files, directories, or globs. Defaults to AETHER_DEV_DIAGNOSTICS_DIR, METEOR_DEV_DIAGNOSTICS_DIR, or /tmp/meteorxiv-traces.")
    parser.add_argument("--category", action="append", help="Only include one category. May be repeated.")
    parser.add_argument("--player", help="Only include events mentioning this player or actor name.")
    parser.add_argument("--timeline", action="store_true", help="Print a compact timeline after the summary.")
    parser.add_argument("--max-events", type=int, default=80, help="Maximum timeline or blocker sample lines.")
    parser.add_argument("--no-summary", action="store_true", help="Skip summary output.")
    parser.add_argument("--no-blockers", action="store_true", help="Skip blocker heuristics.")
    args = parser.parse_args()

    if not args.paths and not os.path.exists(DEFAULT_TRACE_DIR):
        print(f"No trace events found. Default trace directory does not exist: {DEFAULT_TRACE_DIR}")
        return 0

    paths = list(dict.fromkeys(iter_paths(args.paths)))
    events, errors = load_events(paths)
    events = [event for event in events if matches_filters(event, args)]
    events.sort(key=lambda event: (event.get("timestamp", ""), event.get("_path", ""), event.get("_line", 0)))

    if errors:
        print("Read Warnings:", file=sys.stderr)
        for error in errors:
            print(f"  {error}", file=sys.stderr)

    if not events:
        print("No trace events found.")
        return 1 if errors else 0

    if not args.no_summary:
        print_summary(events)

    if not args.no_blockers:
        if not args.no_summary:
            print()
        print_blockers(events, args.max_events)

    if args.timeline:
        print()
        print("Timeline:")
        for event in events[: args.max_events]:
            print(f"  {compact(event)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
