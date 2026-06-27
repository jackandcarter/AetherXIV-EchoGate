# AetherXIV Playtest Bridge

The playtest bridge is a local reverse-engineering workbench. It gives you one place to start and stop the local servers, watch live diagnostics, capture evidence from a playtest, reset a test character, and expose a small localhost API that Codex or another helper can inspect while you are playing.

It does not fake client behavior or force gameplay progress. It controls local development infrastructure and reads the traces/logs the servers already produce.

## First Run

From the repository root:

```bash
./playtest-bridge/bridge.py doctor
./playtest-bridge/bridge.py build
./playtest-bridge/bridge.py run --fresh --new-session
```

`run` starts the web, lobby, map, and world services with diagnostics enabled, then opens a focused live trace view in the same terminal.

Press `Ctrl-C` to leave the live trace view. The servers keep running. Stop them with:

```bash
./playtest-bridge/bridge.py stop
```

## Command Catalog

These commands are primarily for Codex or another local helper to drive while the human playtester keeps the client and visible server terminals in focus.

### Setup and Health

```bash
./playtest-bridge/bridge.py doctor
```

Checks local prerequisites, bridge state, known scripts, the trace folder, and whether the usual service ports are listening.

```bash
./playtest-bridge/bridge.py build
```

Runs the legacy server build and copies runtime data. Use this after code changes.

```bash
./playtest-bridge/bridge.py smoke --allow-missing-staticactors
```

Runs local readiness checks. Use this before a longer playtest when you want a quick sanity check.

### Server Control

```bash
./playtest-bridge/bridge.py run --fresh --new-session
```

Starts services and immediately opens a live trace view.

```bash
./playtest-bridge/bridge.py start --fresh --new-session
```

Starts services and returns to the shell. Use this if Codex or another process will watch/query the bridge separately.

```bash
./playtest-bridge/bridge.py status
```

Shows bridge-managed process IDs, trace folder, session folder, and log paths.

```bash
./playtest-bridge/bridge.py stop
```

Stops bridge-managed services in reverse startup order.

### Recipes

```bash
./playtest-bridge/bridge.py recipe list
```

Lists named playtest flows.

```bash
./playtest-bridge/bridge.py recipe show opening-uldah-battle
```

Shows the recipe's purpose, steps, and success criteria.

```bash
./playtest-bridge/bridge.py recipe run opening-uldah-battle --character-name "Ian Seven" --apply-reset --stop-existing
```

Optionally resets the character, starts a fresh bridge session, and opens the recipe's preferred live trace view.

### Live Observation

```bash
./playtest-bridge/bridge.py watch --focus battle
```

Live combat view: target selection, active mode, autoattack, battle commands, damage, death, mobkill, quest phase, and content-area lifecycle.

```bash
./playtest-bridge/bridge.py watch --focus tutorial
```

Live tutorial view: client tutorial state messages, event function calls, Lua waits/resumes, quest data, and content-area events.

```bash
./playtest-bridge/bridge.py watch --focus loading
```

Live loading/zone view: login readiness, zone-in bundles, local/content zone changes, instance updates, client zone-in completion, and first movement after loading.

```bash
./playtest-bridge/bridge.py watch --focus errors
```

Live problem view: missing owners/scripts, blocked actions, skipped death paths, missing resumes, and no-target battle commands.

Useful filters:

```bash
./playtest-bridge/bridge.py watch --category battle.mobkill.emit
./playtest-bridge/bridge.py watch --player "Ian Seven"
./playtest-bridge/bridge.py watch --text MAN0U020
```

### Querying a Run

```bash
./playtest-bridge/bridge.py events --focus battle --limit 80
```

Prints recent matching structured trace events, then exits.

```bash
./playtest-bridge/bridge.py brief --focus battle
```

Prints a helper-readable summary: session, trace folder, event counts, top categories, and recent compact events.

```bash
./playtest-bridge/bridge.py assert --recipe opening-uldah-battle
```

Evaluates recipe assertions against the active trace folder. For the Ul'dah battle recipe, this checks active-mode state, targetless autoattack, goobbue damage and HP zero, mobkill, quest phase, content finish, post-content loading completion, and stale owner-missing events.

```bash
./playtest-bridge/bridge.py compare <old-snapshot-or-session> <new-snapshot-or-session> --recipe opening-uldah-battle
```

Compares event counts, problem events, category deltas, and recipe assertion status between two captures. You can pass snapshot folders, session folders, or session ids.

```bash
./playtest-bridge/bridge.py summary --timeline --max-events 200
```

Runs the existing `tools/trace-analyze.sh` against the active trace folder.

```bash
./playtest-bridge/bridge.py logs --service map
```

Follows the captured stdout/stderr log for one service.

### Evidence Capture

```bash
./playtest-bridge/bridge.py note "Reached battle tutorial, killed goobbue, zoned out."
```

Adds a timestamped manual observation to the active session.

```bash
./playtest-bridge/bridge.py snapshot --recipe opening-uldah-battle --note "After first goobbue kill test"
```

Copies current traces, captured logs, status, a brief JSON summary, optional assertion results, and analyzer output into the active session folder. Use this before changing code after a useful test.

```bash
./playtest-bridge/bridge.py sessions
```

Lists saved bridge sessions.

### Test Character Helpers

```bash
./playtest-bridge/bridge.py reset --character-name "Ian Seven" --town uldah --apply
```

Wraps `tools/reset-opening-tutorial.sh` to return a local character to a known opening tutorial state.

```bash
./playtest-bridge/bridge.py client-inspect /path/to/FINAL-FANTASY-XIV-client
```

Wraps `tools/client-inspect.sh` to summarize a local client layout and version files.

## Codex / Local App Control

Start the localhost JSON bridge:

```bash
./playtest-bridge/bridge.py serve
```

Default bind: `http://127.0.0.1:8765`

Read endpoints:

- `GET /status`
- `GET /doctor`
- `GET /recipes`
- `GET /brief?focus=battle&limit=40`
- `GET /events?focus=battle&limit=100`
- `GET /assertions?recipe=opening-uldah-battle`
- `GET /compare?left=<old>&right=<new>&recipe=opening-uldah-battle`
- `GET /logs?service=map&limit=200`
- `GET /sessions`

Control endpoints:

- `POST /start`
- `POST /stop`
- `POST /restart`
- `POST /reset`
- `POST /snapshot`
- `POST /note`
- `POST /build`
- `POST /smoke`

Examples:

```bash
curl http://127.0.0.1:8765/status
curl "http://127.0.0.1:8765/brief?focus=battle&limit=30"
curl "http://127.0.0.1:8765/events?focus=errors&limit=50"
```

Start request:

```json
{
  "fresh": true,
  "new_session": true,
  "services": "web,lobby,map,world"
}
```

Reset request:

```json
{
  "character_name": "Ian Seven",
  "town": "uldah",
  "apply": true
}
```

Snapshot request:

```json
{
  "focus": "battle",
  "limit": 200,
  "recipe": "opening-uldah-battle",
  "note": "Captured after goobbue kill."
}
```

Keep the HTTP bridge bound to `127.0.0.1`; it is a local development control surface, not a public API.

For endpoint details, see `playtest-bridge/API.md`.

## Files

- Bridge state: `playtest-bridge/.state/state.json`
- Sessions: `playtest-bridge/.state/sessions/`
- Session logs: `playtest-bridge/.state/sessions/<session-id>/logs/`
- Session notes: `playtest-bridge/.state/sessions/<session-id>/notes.md`
- Session snapshots: `playtest-bridge/.state/sessions/<session-id>/snapshots/`
- Recipes: `playtest-bridge/recipes/`
- Default trace folder: `/tmp/aetherxiv-traces`

## Recommended Reverse-Engineering Loop

1. Build: `./playtest-bridge/bridge.py build`
2. Start clean: `./playtest-bridge/bridge.py run --fresh --new-session`
3. Play the client normally.
4. Watch one focused stream instead of many terminal windows.
5. Add notes when something visible happens.
6. Run recipe assertions.
7. Capture a snapshot before changing code.
8. Compare the next run against the previous run.
9. Ask Codex to inspect `/brief`, `/events`, `/assertions`, `/compare`, or the session snapshot.
10. Implement only behavior supported by repeatable traces.
