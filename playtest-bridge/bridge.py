#!/usr/bin/env python3
import argparse
from collections import Counter
import datetime as dt
import http.server
import json
import os
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import sys
import time
import urllib.parse


BRIDGE_DIR = Path(__file__).resolve().parent
ROOT_DIR = BRIDGE_DIR.parent
STATE_DIR = BRIDGE_DIR / ".state"
STATE_PATH = STATE_DIR / "state.json"
SESSIONS_DIR = STATE_DIR / "sessions"
RECIPES_DIR = BRIDGE_DIR / "recipes"
DEFAULT_TRACE_DIR = Path(os.environ.get("METEOR_DEV_DIAGNOSTICS_DIR", "/tmp/meteorxiv-traces"))
DEFAULT_SERVICES = ("web", "lobby", "map", "world")
DEFAULT_PORTS = {
    "web": ("127.0.0.1", 8080),
    "lobby": ("127.0.0.1", 54994),
    "map": ("127.0.0.1", 1989),
    "world": ("127.0.0.1", 54992),
}
READY_LOG_PATTERNS = {
    "lobby": "Lobby Server has started @",
    "map": "Map Server has started @",
    "world": "World Server accepting connections @",
}

ASSERTION_RECIPES = {
    "opening-uldah-battle": (
        "active-mode-state-seen",
        "no-autoattack-start-without-target",
        "goobbue-damage-seen",
        "goobbue-hp-zero-seen",
        "mobkill-emitted",
        "quest-phase-10",
        "content-finished",
        "post-content-zone-load-complete",
        "stale-owner-missing",
    ),
}

FOCUS_CATEGORIES = {
    "battle": (
        "battle.engage",
        "battle.autoattack",
        "battle.command",
        "battle.action",
        "battle.damage",
        "battle.death",
        "battle.mobkill",
        "character.hp.deathCheck",
        "client.target",
        "client.lockTarget",
        "lua.signal",
        "quest.phase",
        "content.area",
        "event.start.ownerMissing",
    ),
    "tutorial": (
        "client.stateMessage",
        "event.runFunction",
        "event.update",
        "event.kick",
        "event.end",
        "lua.",
        "quest.",
        "content.area",
    ),
    "loading": (
        "client.login.ready",
        "client.zoneInComplete",
        "client.position",
        "zone.in",
        "zone.change",
        "session.instance.update",
        "content.area",
        "world.session",
    ),
    "errors": (
        "ownerMissing",
        "blocked",
        "missing",
        "resumeMissing",
        "noTargets",
        "skipped",
    ),
}


def now_text():
    return dt.datetime.now().strftime("%Y%m%d-%H%M%S")


def iso_now():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def load_state():
    if not STATE_PATH.exists():
        return {}
    try:
        return json.loads(STATE_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def save_state(state):
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    STATE_PATH.write_text(json.dumps(state, indent=2, sort_keys=True), encoding="utf-8")


def trace_dir_from_args(args=None, state=None):
    if args is not None and getattr(args, "trace_dir", None):
        return Path(args.trace_dir).expanduser()
    if state and state.get("trace_dir"):
        return Path(state["trace_dir"]).expanduser()
    return DEFAULT_TRACE_DIR


def load_recipe(name):
    path = RECIPES_DIR / f"{name}.json"
    if not path.exists():
        raise RuntimeError(f"unknown recipe: {name}")
    try:
        recipe = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"invalid recipe {name}: {exc}") from exc
    recipe["_path"] = str(path)
    return recipe


def recipe_names():
    if not RECIPES_DIR.exists():
        return []
    return [path.stem for path in sorted(RECIPES_DIR.glob("*.json"))]


def session_dir_for(session_id):
    return SESSIONS_DIR / session_id


def active_session_dir(state=None):
    state = state or load_state()
    if state.get("session_dir"):
        return Path(state["session_dir"])
    if state.get("session_id"):
        return session_dir_for(state["session_id"])
    return None


def run_captured(cmd, cwd=None, env=None):
    proc = subprocess.run(
        cmd,
        cwd=str(cwd or ROOT_DIR),
        env=env,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
    )
    return proc.returncode, proc.stdout


def tail_lines(path, limit=200):
    path = Path(path)
    if not path.exists():
        return []
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            lines = handle.readlines()
    except OSError:
        return []
    return [line.rstrip("\n") for line in lines[-limit:]]


def port_open(host, port, timeout=0.25):
    try:
        with socket.create_connection((host, int(port)), timeout=timeout):
            return True
    except OSError:
        return False


def log_contains(path, pattern):
    if not path or not pattern:
        return False
    for line in tail_lines(path, 1000):
        if pattern in line:
            return True
    return False


def wait_for_service_ready(name, proc, timeout, log_path=None):
    endpoint = DEFAULT_PORTS.get(name)
    pattern = READY_LOG_PATTERNS.get(name)
    if not endpoint and not pattern:
        return True

    deadline = time.time() + timeout
    while time.time() < deadline:
        if proc.poll() is not None:
            return False
        if pattern and log_contains(log_path, pattern):
            return True
        if not pattern and endpoint:
            host, port = endpoint
            if port_open(host, port):
                return True
        time.sleep(0.25)

    return False


def parse_services(value):
    if not value:
        return list(DEFAULT_SERVICES)
    services = [item.strip() for item in value.split(",") if item.strip()]
    unknown = [item for item in services if item not in DEFAULT_SERVICES]
    if unknown:
        raise SystemExit("unknown service(s): " + ", ".join(unknown))
    return services


def service_command(name):
    if name == "web":
        return [str(ROOT_DIR / "tools" / "run-web.sh")]
    if name == "lobby":
        return [str(ROOT_DIR / "tools" / "run-lobby.sh"), "--dev-diagnostics"]
    if name == "map":
        return [str(ROOT_DIR / "tools" / "run-map.sh"), "--dev-diagnostics", "--no-console"]
    if name == "world":
        return [str(ROOT_DIR / "tools" / "run-world.sh"), "--dev-diagnostics", "--no-console"]
    raise ValueError(name)


def is_alive(pid):
    if not pid:
        return False
    try:
        os.kill(int(pid), 0)
        return True
    except OSError:
        return False


def clear_trace_dir(trace_dir):
    trace_dir.mkdir(parents=True, exist_ok=True)
    for path in trace_dir.glob("*.jsonl"):
        path.unlink()


def make_env(args, trace_dir):
    env = os.environ.copy()
    env["METEOR_DEV_DIAGNOSTICS"] = "1"
    env["METEOR_DEV_DIAGNOSTICS_DIR"] = str(trace_dir)
    env["CONFIGURATION"] = getattr(args, "configuration", None) or env.get("CONFIGURATION", "Release")
    if getattr(args, "no_copy_runtime", False):
        env["REFRESH_RUNTIME_DATA"] = "0"
    return env


def start_services(args):
    services = parse_services(getattr(args, "services", ",".join(DEFAULT_SERVICES)))
    state = load_state()
    if getattr(args, "stop_existing", False):
        stop_services(argparse.Namespace(services=",".join(services), quiet=True))
        state = load_state()

    trace_dir = trace_dir_from_args(args, state)
    if getattr(args, "fresh", False):
        clear_trace_dir(trace_dir)
    else:
        trace_dir.mkdir(parents=True, exist_ok=True)

    if getattr(args, "new_session", False) or not state.get("session_id"):
        session_id = now_text()
        session_dir = session_dir_for(session_id)
    else:
        session_id = state.get("session_id")
        session_dir = active_session_dir(state) or session_dir_for(session_id)

    log_dir = session_dir / "logs"
    log_dir.mkdir(parents=True, exist_ok=True)

    processes = state.get("processes", {})
    env = make_env(args, trace_dir)

    for name in DEFAULT_SERVICES:
        if name not in services:
            continue

        existing = processes.get(name, {})
        if is_alive(existing.get("pid")):
            print(f"{name}: already running pid={existing.get('pid')}")
            continue

        log_path = log_dir / f"{name}.log"
        log_handle = open(log_path, "ab", buffering=0)
        cmd = service_command(name)
        print(f"{name}: starting")
        proc = subprocess.Popen(
            cmd,
            cwd=str(ROOT_DIR),
            env=env,
            stdout=log_handle,
            stderr=subprocess.STDOUT,
            stdin=subprocess.DEVNULL,
            start_new_session=True,
        )
        log_handle.close()
        processes[name] = {
            "pid": proc.pid,
            "cmd": cmd,
            "log": str(log_path),
            "started_at": iso_now(),
        }

        state.update(
            {
                "root": str(ROOT_DIR),
                "session_id": session_id,
                "session_dir": str(session_dir),
                "trace_dir": str(trace_dir),
                "log_dir": str(log_dir),
                "processes": processes,
                "updated_at": iso_now(),
            }
        )
        save_state(state)

        ready_timeout = getattr(args, "ready_timeout", 20.0)
        if wait_for_service_ready(name, proc, ready_timeout, log_path):
            host, port = DEFAULT_PORTS[name]
            print(f"{name}: ready {host}:{port}")
        elif proc.poll() is not None:
            print(f"{name}: exited early code={proc.returncode}; see {log_path}")
        else:
            host, port = DEFAULT_PORTS[name]
            print(f"{name}: not ready after {ready_timeout:.1f}s at {host}:{port}; see {log_path}")

        time.sleep(getattr(args, "delay", 0.25))

    return load_state()


def stop_one(name, info, quiet=False):
    pid = info.get("pid")
    if not is_alive(pid):
        if not quiet:
            print(f"{name}: not running")
        return False

    if not quiet:
        print(f"{name}: stopping pid={pid}")
    try:
        os.killpg(int(pid), signal.SIGTERM)
    except OSError:
        try:
            os.kill(int(pid), signal.SIGTERM)
        except OSError:
            return False

    deadline = time.time() + 5
    while time.time() < deadline:
        if not is_alive(pid):
            return True
        time.sleep(0.2)

    if not quiet:
        print(f"{name}: forcing stop pid={pid}")
    try:
        os.killpg(int(pid), signal.SIGKILL)
    except OSError:
        try:
            os.kill(int(pid), signal.SIGKILL)
        except OSError:
            pass
    return True


def stop_services(args):
    services = parse_services(getattr(args, "services", ",".join(DEFAULT_SERVICES)))
    state = load_state()
    processes = state.get("processes", {})
    for name in reversed(DEFAULT_SERVICES):
        if name in services and name in processes:
            stop_one(name, processes[name], quiet=getattr(args, "quiet", False))
            processes.pop(name, None)
    state["processes"] = processes
    state["updated_at"] = iso_now()
    save_state(state)
    return state


def status_payload():
    state = load_state()
    processes = {}
    for name, info in state.get("processes", {}).items():
        item = dict(info)
        item["running"] = is_alive(info.get("pid"))
        processes[name] = item
    return {
        "root": state.get("root", str(ROOT_DIR)),
        "session_id": state.get("session_id"),
        "session_dir": state.get("session_dir"),
        "trace_dir": state.get("trace_dir", str(DEFAULT_TRACE_DIR)),
        "log_dir": state.get("log_dir"),
        "processes": processes,
        "updated_at": state.get("updated_at"),
    }


def print_status(args):
    payload = status_payload()
    if getattr(args, "json", False):
        print(json.dumps(payload, indent=2, sort_keys=True))
        return

    print(f"root: {payload['root']}")
    print(f"session_id: {payload.get('session_id') or ''}")
    print(f"session_dir: {payload.get('session_dir') or ''}")
    print(f"trace_dir: {payload['trace_dir']}")
    print(f"log_dir: {payload.get('log_dir') or ''}")
    if not payload["processes"]:
        print("services: none managed by bridge")
        return
    for name in DEFAULT_SERVICES:
        if name not in payload["processes"]:
            continue
        info = payload["processes"][name]
        state = "running" if info["running"] else "stopped"
        print(f"{name}: {state} pid={info.get('pid')} log={info.get('log')}")


def compact_event(event):
    parts = [event.get("timestamp", ""), event.get("server", ""), event.get("category", "?")]
    for key in (
        "player",
        "actorName",
        "targetName",
        "state",
        "reason",
        "eventName",
        "waitType",
        "signal",
        "commandName",
        "classification",
        "areaKind",
    ):
        value = event.get(key)
        if value not in (None, ""):
            parts.append(f"{key}={value}")
    for key in (
        "zone",
        "fromZone",
        "toZone",
        "destinationZone",
        "privateArea",
        "fromPrivateArea",
        "toPrivateArea",
        "requestedPrivateArea",
        "spawnType",
        "isLogin",
        "target",
        "requestedTarget",
        "attackTarget",
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


def event_matches(event, focus=None, categories=None, player=None, text=None):
    category = str(event.get("category", ""))
    event_text = compact_event(event)
    if categories and category not in categories:
        return False
    if player:
        lowered = player.lower()
        fields = (
            str(event.get("player", "")),
            str(event.get("actorName", "")),
            str(event.get("targetName", "")),
            event_text,
        )
        if not any(lowered in field.lower() for field in fields):
            return False
    if text:
        if text.lower() not in event_text.lower() and text.lower() not in json.dumps(event, sort_keys=True).lower():
            return False
    if focus and focus != "all":
        needles = FOCUS_CATEGORIES.get(focus)
        if needles is None:
            raise SystemExit(f"unknown focus: {focus}")
        if not any(needle in category or needle in event_text for needle in needles):
            return False
    return True


def read_events(trace_dir, focus=None, categories=None, player=None, text=None):
    events = []
    for path in sorted(Path(trace_dir).glob("*.jsonl")):
        try:
            with open(path, "r", encoding="utf-8") as handle:
                for line_no, line in enumerate(handle, 1):
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        event = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    event["_path"] = str(path)
                    event["_line"] = line_no
                    if event_matches(event, focus=focus, categories=categories, player=player, text=text):
                        events.append(event)
        except OSError:
            continue
    events.sort(key=lambda item: (item.get("timestamp", ""), item.get("_path", ""), item.get("_line", 0)))
    return events


def event_hex(value):
    if value is None:
        return ""
    return str(value).lower()


def event_int(event, key, default=None):
    value = event.get(key)
    if value is None:
        return default
    if isinstance(value, int):
        return value
    try:
        text = str(value)
        return int(text, 16) if text.lower().startswith("0x") else int(text)
    except (TypeError, ValueError):
        return default


def actor_text(event):
    return " ".join(
        str(event.get(key, ""))
        for key in ("actorName", "targetName", "defenderName", "ownerName", "uniqueId")
    ).lower()


def assertion_result(name, status, detail, event=None):
    result = {"name": name, "status": status, "detail": detail}
    if event is not None:
        result["event"] = event
        result["compact"] = compact_event(event)
    return result


def evaluate_assertion(name, events):
    if name == "active-mode-state-seen":
        for event in events:
            if event.get("category") == "battle.engage.state" and event_hex(event.get("newMainState")) in ("0x2", "2"):
                return assertion_result(name, "pass", "active mode state change was traced", event)
        return assertion_result(name, "fail", "no battle.engage.state event with newMainState 0x2 was found")

    if name == "no-autoattack-start-without-target":
        offenders = [
            event for event in events
            if event.get("category") == "battle.autoattack.state"
            and event.get("state") == "start"
            and event.get("target") in (None, "", "0x0", 0)
        ]
        if offenders:
            return assertion_result(name, "fail", "autoattack started without a resolved target", offenders[0])
        return assertion_result(name, "pass", "no autoattack start with target 0x0 was found")

    if name == "goobbue-damage-seen":
        for event in events:
            if event.get("category") == "battle.damage.apply" and "goobbue" in actor_text(event):
                return assertion_result(name, "pass", "goobbue damage was applied", event)
        return assertion_result(name, "fail", "no battle.damage.apply event for goobbue was found")

    if name == "goobbue-hp-zero-seen":
        for event in events:
            if event.get("category") == "battle.damage.apply" and "goobbue" in actor_text(event):
                after_hp = event_int(event, "afterHp")
                if after_hp == 0:
                    return assertion_result(name, "pass", "goobbue reached 0 HP in damage trace", event)
        for event in events:
            if event.get("category") == "character.hp.deathCheck" and "goobbue" in actor_text(event):
                after_hp = event_int(event, "after")
                if after_hp == 0:
                    return assertion_result(name, "pass", "goobbue reached 0 HP in death check", event)
        return assertion_result(name, "fail", "goobbue did not reach 0 HP in known traces")

    if name == "mobkill-emitted":
        for event in events:
            if event.get("category") == "battle.mobkill.emit":
                return assertion_result(name, "pass", "battle.mobkill.emit was traced", event)
        return assertion_result(name, "fail", "no battle.mobkill.emit event was found")

    if name == "quest-phase-10":
        for event in events:
            if event.get("category") == "quest.phase" and event_int(event, "newPhase") == 10:
                return assertion_result(name, "pass", "quest phase advanced to 10", event)
        return assertion_result(name, "fail", "no quest.phase event advanced to 10")

    if name == "content-finished":
        for event in events:
            if event.get("category") == "content.area.finished":
                return assertion_result(name, "pass", "content area finished", event)
        return assertion_result(name, "fail", "content.area.finished was not found")

    if name == "post-content-zone-load-complete":
        content_finished_index = None
        for index, event in enumerate(events):
            if event.get("category") == "content.area.finished":
                content_finished_index = index
                break

        if content_finished_index is None:
            return assertion_result(name, "fail", "content did not finish before the post-content load check")

        zone_event = None
        zone_index = None
        for index, event in enumerate(events[content_finished_index + 1:], content_finished_index + 1):
            if event.get("category") in ("zone.change.local.end", "zone.in.end"):
                zone_event = event
                zone_index = index
                break

        if zone_event is None:
            return assertion_result(name, "fail", "no local zone-in completion trace was found after content finish")

        player = str(zone_event.get("player") or "")
        for event in events[zone_index + 1:]:
            if event.get("category") not in ("client.zoneInComplete", "client.position"):
                continue
            if not player or str(event.get("player") or "") == player:
                return assertion_result(name, "pass", "client acknowledged or moved after the post-content zone load", event)

        return assertion_result(name, "fail", "server completed post-content zone load, but no client zone-in completion or movement followed", zone_event)

    if name == "stale-owner-missing":
        offenders = [event for event in events if event.get("category") == "event.start.ownerMissing"]
        if offenders:
            return assertion_result(name, "warn", f"{len(offenders)} owner-missing event(s) were found", offenders[0])
        return assertion_result(name, "pass", "no owner-missing events were found")

    return assertion_result(name, "warn", "unknown assertion")


def assertion_names_for_recipe(recipe_name):
    return ASSERTION_RECIPES.get(recipe_name, ())


def assertions_payload(recipe_name="opening-uldah-battle", trace_dir=None, player=None, text=None):
    if trace_dir is None:
        trace_dir = trace_dir_from_args(state=load_state())
    events = read_events(trace_dir, focus="all", player=player, text=text)
    names = assertion_names_for_recipe(recipe_name)
    if not names:
        raise RuntimeError(f"no assertions registered for recipe: {recipe_name}")
    results = [evaluate_assertion(name, events) for name in names]
    counts = Counter(result["status"] for result in results)
    return {
        "recipe": recipe_name,
        "trace_dir": str(trace_dir),
        "event_count": len(events),
        "status": "fail" if counts.get("fail", 0) else "warn" if counts.get("warn", 0) else "pass",
        "counts": dict(counts),
        "results": results,
    }


def print_assertions(args):
    state = load_state()
    trace_dir = trace_dir_from_args(args, state)
    try:
        payload = assertions_payload(recipe_name=args.recipe, trace_dir=trace_dir, player=args.player, text=args.text)
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 0 if payload["status"] != "fail" else 1
    print(f"recipe: {payload['recipe']}")
    print(f"trace_dir: {payload['trace_dir']}")
    print(f"status: {payload['status']}")
    for result in payload["results"]:
        print(f"{result['status']}: {result['name']} - {result['detail']}")
        if result.get("compact"):
            print(f"  {result['compact']}")
    return 0 if payload["status"] != "fail" else 1


def watch_traces(args):
    state = load_state()
    trace_dir = trace_dir_from_args(args, state)
    trace_dir.mkdir(parents=True, exist_ok=True)
    categories = getattr(args, "category", None)
    positions = {}

    def seed_existing():
        for path in sorted(trace_dir.glob("*.jsonl")):
            try:
                positions[str(path)] = 0 if args.from_start else path.stat().st_size
            except OSError:
                positions[str(path)] = 0

    seed_existing()
    print(f"watching traces: {trace_dir} focus={args.focus}")
    try:
        while True:
            for path in sorted(trace_dir.glob("*.jsonl")):
                key = str(path)
                if key not in positions:
                    positions[key] = 0
                try:
                    with open(path, "r", encoding="utf-8") as handle:
                        handle.seek(positions[key])
                        for line in handle:
                            positions[key] = handle.tell()
                            line = line.strip()
                            if not line:
                                continue
                            try:
                                event = json.loads(line)
                            except json.JSONDecodeError:
                                continue
                            if event_matches(event, focus=args.focus, categories=categories, player=args.player, text=getattr(args, "text", None)):
                                print(compact_event(event), flush=True)
                        positions[key] = handle.tell()
                except OSError:
                    continue
            time.sleep(args.interval)
    except KeyboardInterrupt:
        print()
        print("left live view; bridge-managed servers are still running")


def follow_file(path, from_start=False):
    path = Path(path)
    position = 0 if from_start else path.stat().st_size if path.exists() else 0
    try:
        while True:
            if path.exists():
                with open(path, "r", encoding="utf-8", errors="replace") as handle:
                    handle.seek(position)
                    for line in handle:
                        position = handle.tell()
                        print(line.rstrip())
                    position = handle.tell()
            time.sleep(0.5)
    except KeyboardInterrupt:
        print()


def show_logs(args):
    state = load_state()
    info = state.get("processes", {}).get(args.service)
    if not info or not info.get("log"):
        raise SystemExit(f"no log known for service: {args.service}")
    print(f"watching log: {info['log']}")
    follow_file(info["log"], from_start=args.from_start)


def run_summary(args):
    state = load_state()
    trace_dir = str(trace_dir_from_args(args, state))
    cmd = [str(ROOT_DIR / "tools" / "trace-analyze.sh"), trace_dir] + list(args.analyzer_args)
    return subprocess.call(cmd, cwd=str(ROOT_DIR))


def reset_character(args):
    cmd = [str(ROOT_DIR / "tools" / "reset-opening-tutorial.sh")]
    if args.character_id:
        cmd += ["--character-id", args.character_id]
    if args.character_name:
        cmd += ["--character-name", args.character_name]
    if args.town:
        cmd += ["--town", args.town]
    if args.apply:
        cmd.append("--apply")
    return subprocess.call(cmd, cwd=str(ROOT_DIR))


def build_project(args):
    env = os.environ.copy()
    env["RESTORE"] = "1" if getattr(args, "restore", False) else env.get("RESTORE", "0")
    env["CONFIGURATION"] = getattr(args, "configuration", None) or env.get("CONFIGURATION", "Release")
    commands = [[str(ROOT_DIR / "tools" / "build-legacy.sh")]]
    if not getattr(args, "no_copy_runtime", False):
        commands.append([str(ROOT_DIR / "tools" / "copy-runtime-data.sh")])

    for cmd in commands:
        code = subprocess.call(cmd, cwd=str(ROOT_DIR), env=env)
        if code != 0:
            return code
    return 0


def smoke_project(args):
    cmd = [str(ROOT_DIR / "tools" / "smoke-local.sh")]
    if getattr(args, "allow_missing_staticactors", False):
        cmd.append("--allow-missing-staticactors")
    return subprocess.call(cmd, cwd=str(ROOT_DIR))


def doctor_payload():
    state = load_state()
    trace_dir = trace_dir_from_args(state=state)
    checks = []

    def add(name, ok, detail=""):
        checks.append({"name": name, "ok": bool(ok), "detail": detail})

    add("repo root", (ROOT_DIR / "MeteorXIV.Core.sln").exists(), str(ROOT_DIR))
    add("trace directory", trace_dir.exists(), str(trace_dir))
    for tool in ("python3", "mono", "php"):
        add(f"tool:{tool}", shutil.which(tool) is not None, shutil.which(tool) or "missing")
    for script in (
        "tools/run-web.sh",
        "tools/run-lobby.sh",
        "tools/run-map.sh",
        "tools/run-world.sh",
        "tools/trace-analyze.sh",
        "tools/reset-opening-tutorial.sh",
    ):
        path = ROOT_DIR / script
        add(script, path.exists(), str(path))
    for name, (host, port) in DEFAULT_PORTS.items():
        if name == "map":
            add(f"port:{name}", True, f"{host}:{port} not probed; map treats raw TCP clients as the world route")
        else:
            add(f"port:{name}", port_open(host, port), f"{host}:{port}")

    payload = status_payload()
    payload["checks"] = checks
    payload["ok"] = all(check["ok"] for check in checks if not check["name"].startswith("port:"))
    return payload


def doctor(args):
    payload = doctor_payload()
    if getattr(args, "json", False):
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 0 if payload["ok"] else 1
    for check in payload["checks"]:
        mark = "ok" if check["ok"] else "missing"
        print(f"{mark}: {check['name']} {check['detail']}")
    return 0 if payload["ok"] else 1


def note_path(state=None):
    session_dir = active_session_dir(state)
    if session_dir is None:
        session_id = now_text()
        session_dir = session_dir_for(session_id)
        state = load_state()
        state.setdefault("session_id", session_id)
        state.setdefault("session_dir", str(session_dir))
        save_state(state)
    session_dir.mkdir(parents=True, exist_ok=True)
    return session_dir / "notes.md"


def add_note(args):
    state = load_state()
    path = note_path(state)
    body = args.text
    if not body and not sys.stdin.isatty():
        body = sys.stdin.read().strip()
    if not body:
        raise SystemExit("missing note text")
    with open(path, "a", encoding="utf-8") as handle:
        handle.write(f"\n## {dt.datetime.now().isoformat(timespec='seconds')}\n\n")
        handle.write(body.strip() + "\n")
    print(path)
    return 0


def summarize_events(events, limit=25):
    categories = Counter(event.get("category", "?") for event in events)
    servers = Counter(event.get("server", "?") for event in events)
    return {
        "count": len(events),
        "servers": dict(servers.most_common()),
        "categories": dict(categories.most_common()),
        "recent": [{"compact": compact_event(event), "event": event} for event in events[-limit:]],
    }


def brief_payload(focus="battle", limit=40, player=None, text=None):
    state = load_state()
    trace_dir = trace_dir_from_args(state=state)
    events = read_events(trace_dir, focus=focus, player=player, text=text)
    summary = summarize_events(events, limit=limit)
    summary["status"] = status_payload()
    summary["focus"] = focus
    summary["trace_dir"] = str(trace_dir)
    summary["watch_hints"] = {
        "battle": "./playtest-bridge/bridge.py watch --focus battle",
        "tutorial": "./playtest-bridge/bridge.py watch --focus tutorial",
        "loading": "./playtest-bridge/bridge.py watch --focus loading",
        "errors": "./playtest-bridge/bridge.py watch --focus errors",
    }
    return summary


def print_brief(args):
    payload = brief_payload(focus=args.focus, limit=args.limit, player=args.player, text=args.text)
    if args.json:
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 0
    print(f"session: {payload['status'].get('session_id') or ''}")
    print(f"trace_dir: {payload['trace_dir']}")
    print(f"focus: {payload['focus']} events={payload['count']}")
    print("top categories:")
    for category, count in list(payload["categories"].items())[:12]:
        print(f"  {category}: {count}")
    print("recent:")
    for item in payload["recent"]:
        print(f"  {item['compact']}")
    return 0


def print_events(args):
    state = load_state()
    trace_dir = trace_dir_from_args(args, state)
    events = read_events(trace_dir, focus=args.focus, categories=args.category, player=args.player, text=args.text)
    events = events[-args.limit:]
    if args.json:
        print(json.dumps([{"compact": compact_event(event), "event": event} for event in events], indent=2, sort_keys=True))
        return 0
    for event in events:
        print(compact_event(event))
    return 0


def list_sessions(args):
    rows = []
    for path in sorted(SESSIONS_DIR.glob("*")):
        if not path.is_dir():
            continue
        logs = sorted((path / "logs").glob("*.log")) if (path / "logs").exists() else []
        snapshots = sorted((path / "snapshots").glob("*")) if (path / "snapshots").exists() else []
        rows.append({
            "session_id": path.name,
            "path": str(path),
            "logs": len(logs),
            "snapshots": len(snapshots),
            "notes": (path / "notes.md").exists(),
        })
    if args.json:
        print(json.dumps(rows, indent=2, sort_keys=True))
        return 0
    for row in rows[-args.limit:]:
        print(f"{row['session_id']} logs={row['logs']} snapshots={row['snapshots']} notes={row['notes']} path={row['path']}")
    return 0


def latest_snapshot_dir(session_dir):
    snap_root = Path(session_dir) / "snapshots"
    if not snap_root.exists():
        return None
    snapshots = [path for path in sorted(snap_root.iterdir()) if path.is_dir()]
    return snapshots[-1] if snapshots else None


def resolve_capture_path(value):
    path = Path(value).expanduser()
    if path.exists():
        if (path / "snapshots").exists():
            latest = latest_snapshot_dir(path)
            if latest is not None:
                return latest
        return path

    session_path = session_dir_for(value)
    if session_path.exists():
        latest = latest_snapshot_dir(session_path)
        return latest or session_path

    raise RuntimeError(f"capture not found: {value}")


def trace_dir_for_capture(path):
    path = Path(path)
    if (path / "traces").exists():
        return path / "traces"
    if (path / "trace_dir").exists():
        return Path((path / "trace_dir").read_text(encoding="utf-8").strip())
    return path


def capture_summary(path, recipe=None):
    trace_dir = trace_dir_for_capture(path)
    events = read_events(trace_dir, focus="all")
    categories = Counter(event.get("category", "?") for event in events)
    errors = [event for event in events if event_matches(event, focus="errors")]
    payload = {
        "path": str(path),
        "trace_dir": str(trace_dir),
        "event_count": len(events),
        "categories": dict(categories),
        "error_count": len(errors),
        "errors": [compact_event(event) for event in errors[-20:]],
    }
    if recipe:
        payload["assertions"] = assertions_payload(recipe_name=recipe, trace_dir=trace_dir)
    return payload


def compare_payload(left, right, recipe=None):
    left_path = resolve_capture_path(left)
    right_path = resolve_capture_path(right)
    left_summary = capture_summary(left_path, recipe=recipe)
    right_summary = capture_summary(right_path, recipe=recipe)
    left_categories = Counter(left_summary["categories"])
    right_categories = Counter(right_summary["categories"])
    category_names = sorted(set(left_categories) | set(right_categories))
    deltas = []
    for category in category_names:
        before = left_categories.get(category, 0)
        after = right_categories.get(category, 0)
        if before != after:
            deltas.append({
                "category": category,
                "before": before,
                "after": after,
                "delta": after - before,
            })
    deltas.sort(key=lambda item: (abs(item["delta"]), item["category"]), reverse=True)
    return {
        "left": left_summary,
        "right": right_summary,
        "category_deltas": deltas,
    }


def print_compare(args):
    try:
        payload = compare_payload(args.left, args.right, recipe=args.recipe)
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps(payload, indent=2, sort_keys=True))
        return 0

    print(f"left: {payload['left']['path']}")
    print(f"right: {payload['right']['path']}")
    print(f"events: {payload['left']['event_count']} -> {payload['right']['event_count']}")
    print(f"errors: {payload['left']['error_count']} -> {payload['right']['error_count']}")
    if args.recipe:
        left_status = payload["left"]["assertions"]["status"]
        right_status = payload["right"]["assertions"]["status"]
        print(f"assertions({args.recipe}): {left_status} -> {right_status}")
    print("category deltas:")
    for item in payload["category_deltas"][: args.limit]:
        print(f"  {item['category']}: {item['before']} -> {item['after']} ({item['delta']:+d})")
    return 0


def copy_matching(src_dir, dst_dir, pattern):
    src_dir = Path(src_dir)
    dst_dir = Path(dst_dir)
    dst_dir.mkdir(parents=True, exist_ok=True)
    if not src_dir.exists():
        return []
    copied = []
    for path in sorted(src_dir.glob(pattern)):
        if path.is_file():
            target = dst_dir / path.name
            shutil.copy2(path, target)
            copied.append(str(target))
    return copied


def create_snapshot(args):
    state = load_state()
    session_dir = active_session_dir(state)
    if session_dir is None:
        raise RuntimeError("no active session; run start/run first or add a note to create one")
    snap_dir = session_dir / "snapshots" / now_text()
    snap_dir.mkdir(parents=True, exist_ok=True)

    trace_dir = trace_dir_from_args(args, state)
    copied = {
        "traces": copy_matching(trace_dir, snap_dir / "traces", "*.jsonl"),
        "logs": copy_matching(state.get("log_dir", ""), snap_dir / "logs", "*.log") if state.get("log_dir") else [],
    }
    status = status_payload()
    (snap_dir / "status.json").write_text(json.dumps(status, indent=2, sort_keys=True), encoding="utf-8")
    (snap_dir / "brief.json").write_text(json.dumps(brief_payload(focus=args.focus, limit=args.limit), indent=2, sort_keys=True), encoding="utf-8")
    recipe = getattr(args, "recipe", None)
    if recipe:
        assertions = assertions_payload(recipe_name=recipe, trace_dir=trace_dir)
        (snap_dir / "assertions.json").write_text(json.dumps(assertions, indent=2, sort_keys=True), encoding="utf-8")
    if args.note:
        (snap_dir / "note.md").write_text(args.note.strip() + "\n", encoding="utf-8")
    code, summary = run_captured([str(ROOT_DIR / "tools" / "trace-analyze.sh"), str(trace_dir), "--timeline", "--max-events", str(args.limit)])
    (snap_dir / "trace-summary.txt").write_text(summary, encoding="utf-8")
    manifest = {
        "created_at": iso_now(),
        "snapshot_dir": str(snap_dir),
        "trace_dir": str(trace_dir),
        "copied": copied,
        "recipe": recipe,
        "summary_exit_code": code,
    }
    (snap_dir / "manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True), encoding="utf-8")
    return manifest


def snapshot(args):
    try:
        manifest = create_snapshot(args)
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 1
    if getattr(args, "json", False):
        print(json.dumps(manifest, indent=2, sort_keys=True))
    else:
        print(manifest["snapshot_dir"])
    return 0


def inspect_client(args):
    return subprocess.call([str(ROOT_DIR / "tools" / "client-inspect.sh"), args.path], cwd=str(ROOT_DIR))


def print_recipe_steps(recipe):
    print(f"recipe: {recipe.get('name')}")
    print(recipe.get("description", ""))
    if recipe.get("steps"):
        print("steps:")
        for index, step in enumerate(recipe["steps"], 1):
            print(f"  {index}. {step}")
    if recipe.get("success_criteria"):
        print("success criteria:")
        for item in recipe["success_criteria"]:
            print(f"  - {item}")


def recipe_command(args):
    if args.action == "list":
        try:
            for name in recipe_names():
                recipe = load_recipe(name)
                print(f"{name}: {recipe.get('description', '')}")
        except RuntimeError as exc:
            print(str(exc), file=sys.stderr)
            return 2
        return 0

    try:
        recipe = load_recipe(args.name)
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 2
    if args.action == "show":
        if args.json:
            print(json.dumps(recipe, indent=2, sort_keys=True))
        else:
            print_recipe_steps(recipe)
        return 0

    if args.action == "run":
        reset = recipe.get("reset", {})
        if args.apply_reset:
            reset_args = argparse.Namespace(
                character_id=args.character_id or "",
                character_name=args.character_name or "",
                town=args.town or reset.get("town", "uldah"),
                apply=True,
            )
            if not reset_args.character_id and not reset_args.character_name:
                raise SystemExit("recipe reset needs --character-id or --character-name")
            code = reset_character(reset_args)
            if code != 0:
                return code

        print_recipe_steps(recipe)
        services = ",".join(recipe.get("services", DEFAULT_SERVICES))
        run_args = argparse.Namespace(
            services=services,
            trace_dir=args.trace_dir,
            configuration=args.configuration,
            fresh=True,
            stop_existing=args.stop_existing,
            new_session=True,
            no_copy_runtime=args.no_copy_runtime,
            delay=args.delay,
            focus=recipe.get("watch_focus", "battle"),
            category=None,
            player=args.character_name,
            text=None,
            interval=args.interval,
        )
        return bridge_run(run_args)

    raise SystemExit(f"unknown recipe action: {args.action}")


def bridge_run(args):
    start_services(args)
    watch_args = argparse.Namespace(
        trace_dir=args.trace_dir,
        focus=args.focus,
        category=args.category,
        player=args.player,
        text=args.text,
        from_start=False,
        interval=args.interval,
    )
    watch_traces(watch_args)


class BridgeHandler(http.server.BaseHTTPRequestHandler):
    server_version = "MeteorPlaytestBridge/1"

    def log_message(self, fmt, *args):
        return

    def read_json(self):
        length = int(self.headers.get("Content-Length", "0") or "0")
        if length <= 0:
            return {}
        raw = self.rfile.read(length)
        if not raw:
            return {}
        return json.loads(raw.decode("utf-8"))

    def send_json(self, status, payload):
        data = json.dumps(payload, indent=2, sort_keys=True).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        query = urllib.parse.parse_qs(parsed.query)
        if parsed.path == "/status":
            self.send_json(200, status_payload())
            return
        if parsed.path == "/events":
            state = load_state()
            trace_dir = trace_dir_from_args(state=state)
            focus = query.get("focus", ["battle"])[0]
            player = query.get("player", [None])[0]
            text = query.get("text", [None])[0]
            categories = query.get("category") or None
            limit = int(query.get("limit", ["100"])[0])
            events = read_events(trace_dir, focus=focus, categories=categories, player=player, text=text)
            self.send_json(
                200,
                {
                    "trace_dir": str(trace_dir),
                    "count": len(events),
                    "events": [
                        {"compact": compact_event(event), "event": event}
                        for event in events[-limit:]
                    ],
                },
            )
            return
        if parsed.path == "/brief":
            focus = query.get("focus", ["battle"])[0]
            player = query.get("player", [None])[0]
            text = query.get("text", [None])[0]
            limit = int(query.get("limit", ["40"])[0])
            self.send_json(200, brief_payload(focus=focus, limit=limit, player=player, text=text))
            return
        if parsed.path == "/doctor":
            self.send_json(200, doctor_payload())
            return
        if parsed.path == "/sessions":
            sessions = []
            for path in sorted(SESSIONS_DIR.glob("*")):
                if path.is_dir():
                    sessions.append({"session_id": path.name, "path": str(path)})
            self.send_json(200, {"sessions": sessions})
            return
        if parsed.path == "/recipes":
            recipes = []
            for name in recipe_names():
                recipe = load_recipe(name)
                recipes.append({"name": name, "description": recipe.get("description", ""), "recipe": recipe})
            self.send_json(200, {"recipes": recipes})
            return
        if parsed.path == "/assertions":
            state = load_state()
            trace_dir = trace_dir_from_args(state=state)
            recipe = query.get("recipe", ["opening-uldah-battle"])[0]
            player = query.get("player", [None])[0]
            text = query.get("text", [None])[0]
            self.send_json(200, assertions_payload(recipe_name=recipe, trace_dir=trace_dir, player=player, text=text))
            return
        if parsed.path == "/compare":
            left = query.get("left", [None])[0]
            right = query.get("right", [None])[0]
            recipe = query.get("recipe", [None])[0]
            if not left or not right:
                self.send_json(400, {"error": "left and right are required"})
                return
            self.send_json(200, compare_payload(left, right, recipe=recipe))
            return
        if parsed.path == "/logs":
            service = query.get("service", ["map"])[0]
            limit = int(query.get("limit", ["200"])[0])
            info = load_state().get("processes", {}).get(service)
            if not info or not info.get("log"):
                self.send_json(404, {"error": f"no log known for service: {service}"})
                return
            self.send_json(200, {"service": service, "log": info["log"], "lines": tail_lines(info["log"], limit)})
            return
        self.send_json(404, {"error": "not found"})

    def do_POST(self):
        parsed = urllib.parse.urlparse(self.path)
        try:
            body = self.read_json()
            if parsed.path == "/start":
                args = argparse.Namespace(
                    services=body.get("services", ",".join(DEFAULT_SERVICES)),
                    trace_dir=body.get("trace_dir"),
                    configuration=body.get("configuration", "Release"),
                    fresh=bool(body.get("fresh", False)),
                    stop_existing=bool(body.get("stop_existing", False)),
                    new_session=bool(body.get("new_session", False)),
                    no_copy_runtime=bool(body.get("no_copy_runtime", False)),
                    delay=float(body.get("delay", 1.0)),
                )
                start_services(args)
                self.send_json(200, status_payload())
                return
            if parsed.path == "/stop":
                args = argparse.Namespace(
                    services=body.get("services", ",".join(DEFAULT_SERVICES)),
                    quiet=True,
                )
                stop_services(args)
                self.send_json(200, status_payload())
                return
            if parsed.path == "/restart":
                args = argparse.Namespace(services=body.get("services", ",".join(DEFAULT_SERVICES)), quiet=True)
                stop_services(args)
                start_args = argparse.Namespace(
                    services=body.get("services", ",".join(DEFAULT_SERVICES)),
                    trace_dir=body.get("trace_dir"),
                    configuration=body.get("configuration", "Release"),
                    fresh=bool(body.get("fresh", True)),
                    stop_existing=False,
                    new_session=bool(body.get("new_session", True)),
                    no_copy_runtime=bool(body.get("no_copy_runtime", False)),
                    delay=float(body.get("delay", 1.0)),
                )
                start_services(start_args)
                self.send_json(200, status_payload())
                return
            if parsed.path == "/reset":
                args = argparse.Namespace(
                    character_id=str(body.get("character_id") or ""),
                    character_name=str(body.get("character_name") or ""),
                    town=str(body.get("town") or "uldah"),
                    apply=bool(body.get("apply", False)),
                )
                code = reset_character(args)
                self.send_json(200 if code == 0 else 500, {"exit_code": code})
                return
            if parsed.path == "/snapshot":
                args = argparse.Namespace(
                    trace_dir=body.get("trace_dir"),
                    focus=body.get("focus", "battle"),
                    limit=int(body.get("limit", 200)),
                    recipe=body.get("recipe"),
                    note=body.get("note"),
                    json=True,
                )
                self.send_json(200, create_snapshot(args))
                return
            if parsed.path == "/note":
                args = argparse.Namespace(text=body.get("text", ""))
                code = add_note(args)
                self.send_json(200 if code == 0 else 500, {"exit_code": code})
                return
            if parsed.path == "/build":
                args = argparse.Namespace(
                    restore=bool(body.get("restore", False)),
                    configuration=body.get("configuration", "Release"),
                    no_copy_runtime=bool(body.get("no_copy_runtime", False)),
                )
                code = build_project(args)
                self.send_json(200 if code == 0 else 500, {"exit_code": code})
                return
            if parsed.path == "/smoke":
                args = argparse.Namespace(allow_missing_staticactors=bool(body.get("allow_missing_staticactors", False)))
                code = smoke_project(args)
                self.send_json(200 if code == 0 else 500, {"exit_code": code})
                return
        except Exception as exc:
            self.send_json(500, {"error": str(exc)})
            return
        self.send_json(404, {"error": "not found"})


def serve(args):
    address = (args.host, args.port)
    httpd = http.server.ThreadingHTTPServer(address, BridgeHandler)
    print(f"playtest bridge listening on http://{args.host}:{args.port}")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print()


def add_common_start_args(parser):
    parser.add_argument("--services", default=",".join(DEFAULT_SERVICES), help="Comma-separated services: web,lobby,map,world.")
    parser.add_argument("--trace-dir", default=None, help="Diagnostic trace directory.")
    parser.add_argument("--configuration", default="Release", help="Build configuration to run.")
    parser.add_argument("--fresh", action="store_true", help="Remove existing *.jsonl files from the trace directory before starting.")
    parser.add_argument("--stop-existing", action="store_true", help="Stop bridge-managed services before starting.")
    parser.add_argument("--new-session", action="store_true", help="Create a new bridge session folder for this run.")
    parser.add_argument("--no-copy-runtime", action="store_true", help="Disable runtime data refresh in run scripts that support it.")
    parser.add_argument("--delay", type=float, default=0.25, help="Seconds to wait between service starts after readiness.")
    parser.add_argument("--ready-timeout", type=float, default=20.0, help="Seconds to wait for each service port to open.")


def build_parser():
    parser = argparse.ArgumentParser(description="Local MeteorXIV playtest bridge.")
    sub = parser.add_subparsers(dest="command", required=True)

    run_p = sub.add_parser("run", help="Start services, then show a live trace view.")
    add_common_start_args(run_p)
    run_p.add_argument("--focus", default="battle", choices=("all", "battle", "tutorial", "loading", "errors"))
    run_p.add_argument("--category", action="append")
    run_p.add_argument("--player")
    run_p.add_argument("--text")
    run_p.add_argument("--interval", type=float, default=0.5)
    run_p.set_defaults(func=bridge_run)

    start_p = sub.add_parser("start", help="Start bridge-managed services.")
    add_common_start_args(start_p)
    start_p.set_defaults(func=start_services)

    stop_p = sub.add_parser("stop", help="Stop bridge-managed services.")
    stop_p.add_argument("--services", default=",".join(DEFAULT_SERVICES))
    stop_p.add_argument("--quiet", action="store_true")
    stop_p.set_defaults(func=stop_services)

    status_p = sub.add_parser("status", help="Show bridge-managed process status.")
    status_p.add_argument("--json", action="store_true")
    status_p.set_defaults(func=print_status)

    doctor_p = sub.add_parser("doctor", help="Check local bridge prerequisites and ports.")
    doctor_p.add_argument("--json", action="store_true")
    doctor_p.set_defaults(func=doctor)

    build_p = sub.add_parser("build", help="Build legacy servers and copy runtime data.")
    build_p.add_argument("--restore", action="store_true")
    build_p.add_argument("--configuration", default="Release")
    build_p.add_argument("--no-copy-runtime", action="store_true")
    build_p.set_defaults(func=build_project)

    smoke_p = sub.add_parser("smoke", help="Run local readiness smoke checks.")
    smoke_p.add_argument("--allow-missing-staticactors", action="store_true")
    smoke_p.set_defaults(func=smoke_project)

    watch_p = sub.add_parser("watch", help="Follow structured trace events.")
    watch_p.add_argument("--trace-dir", default=None)
    watch_p.add_argument("--focus", default="battle", choices=("all", "battle", "tutorial", "loading", "errors"))
    watch_p.add_argument("--category", action="append")
    watch_p.add_argument("--player")
    watch_p.add_argument("--text")
    watch_p.add_argument("--from-start", action="store_true")
    watch_p.add_argument("--interval", type=float, default=0.5)
    watch_p.set_defaults(func=watch_traces)

    events_p = sub.add_parser("events", help="Print recent structured trace events.")
    events_p.add_argument("--trace-dir", default=None)
    events_p.add_argument("--focus", default="battle", choices=("all", "battle", "tutorial", "loading", "errors"))
    events_p.add_argument("--category", action="append")
    events_p.add_argument("--player")
    events_p.add_argument("--text")
    events_p.add_argument("--limit", type=int, default=80)
    events_p.add_argument("--json", action="store_true")
    events_p.set_defaults(func=print_events)

    brief_p = sub.add_parser("brief", help="Print a Codex-readable summary of the current trace session.")
    brief_p.add_argument("--focus", default="battle", choices=("all", "battle", "tutorial", "loading", "errors"))
    brief_p.add_argument("--player")
    brief_p.add_argument("--text")
    brief_p.add_argument("--limit", type=int, default=40)
    brief_p.add_argument("--json", action="store_true")
    brief_p.set_defaults(func=print_brief)

    logs_p = sub.add_parser("logs", help="Follow a captured service log.")
    logs_p.add_argument("--service", required=True, choices=DEFAULT_SERVICES)
    logs_p.add_argument("--from-start", action="store_true")
    logs_p.set_defaults(func=show_logs)

    summary_p = sub.add_parser("summary", help="Run tools/trace-analyze.sh for the active trace directory.")
    summary_p.add_argument("--trace-dir", default=None)
    summary_p.add_argument("analyzer_args", nargs=argparse.REMAINDER)
    summary_p.set_defaults(func=run_summary)

    reset_p = sub.add_parser("reset", help="Reset a local character to an opening tutorial state.")
    reset_p.add_argument("--character-id")
    reset_p.add_argument("--character-name")
    reset_p.add_argument("--town", default="uldah")
    reset_p.add_argument("--apply", action="store_true")
    reset_p.set_defaults(func=reset_character)

    recipe_p = sub.add_parser("recipe", help="List, show, or run named playtest recipes.")
    recipe_sub = recipe_p.add_subparsers(dest="action", required=True)
    recipe_list_p = recipe_sub.add_parser("list", help="List available recipes.")
    recipe_list_p.set_defaults(func=recipe_command)
    recipe_show_p = recipe_sub.add_parser("show", help="Show recipe steps and criteria.")
    recipe_show_p.add_argument("name")
    recipe_show_p.add_argument("--json", action="store_true")
    recipe_show_p.set_defaults(func=recipe_command)
    recipe_run_p = recipe_sub.add_parser("run", help="Run a recipe with a fresh bridge session.")
    recipe_run_p.add_argument("name")
    recipe_run_p.add_argument("--character-id")
    recipe_run_p.add_argument("--character-name")
    recipe_run_p.add_argument("--town")
    recipe_run_p.add_argument("--apply-reset", action="store_true")
    recipe_run_p.add_argument("--trace-dir", default=None)
    recipe_run_p.add_argument("--configuration", default="Release")
    recipe_run_p.add_argument("--stop-existing", action="store_true")
    recipe_run_p.add_argument("--no-copy-runtime", action="store_true")
    recipe_run_p.add_argument("--delay", type=float, default=1.0)
    recipe_run_p.add_argument("--interval", type=float, default=0.5)
    recipe_run_p.set_defaults(func=recipe_command)

    assert_p = sub.add_parser("assert", help="Evaluate recipe assertions against current traces.")
    assert_p.add_argument("--recipe", default="opening-uldah-battle")
    assert_p.add_argument("--trace-dir", default=None)
    assert_p.add_argument("--player")
    assert_p.add_argument("--text")
    assert_p.add_argument("--json", action="store_true")
    assert_p.set_defaults(func=print_assertions)

    compare_p = sub.add_parser("compare", help="Compare two snapshots or session latest snapshots.")
    compare_p.add_argument("left")
    compare_p.add_argument("right")
    compare_p.add_argument("--recipe")
    compare_p.add_argument("--limit", type=int, default=25)
    compare_p.add_argument("--json", action="store_true")
    compare_p.set_defaults(func=print_compare)

    snapshot_p = sub.add_parser("snapshot", help="Capture traces, logs, status, summary, and optional notes for the active session.")
    snapshot_p.add_argument("--trace-dir", default=None)
    snapshot_p.add_argument("--focus", default="battle", choices=("all", "battle", "tutorial", "loading", "errors"))
    snapshot_p.add_argument("--limit", type=int, default=200)
    snapshot_p.add_argument("--recipe")
    snapshot_p.add_argument("--note")
    snapshot_p.add_argument("--json", action="store_true")
    snapshot_p.set_defaults(func=snapshot)

    note_p = sub.add_parser("note", help="Append a manual observation to the active session notes.")
    note_p.add_argument("text", nargs="?")
    note_p.set_defaults(func=add_note)

    sessions_p = sub.add_parser("sessions", help="List bridge session folders.")
    sessions_p.add_argument("--limit", type=int, default=20)
    sessions_p.add_argument("--json", action="store_true")
    sessions_p.set_defaults(func=list_sessions)

    client_p = sub.add_parser("client-inspect", help="Summarize a local FFXIV client layout.")
    client_p.add_argument("path")
    client_p.set_defaults(func=inspect_client)

    serve_p = sub.add_parser("serve", help="Run the local JSON control bridge.")
    serve_p.add_argument("--host", default="127.0.0.1")
    serve_p.add_argument("--port", type=int, default=8765)
    serve_p.set_defaults(func=serve)

    return parser


def main(argv=None):
    parser = build_parser()
    args = parser.parse_args(argv)
    result = args.func(args)
    if isinstance(result, int):
        return result
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
