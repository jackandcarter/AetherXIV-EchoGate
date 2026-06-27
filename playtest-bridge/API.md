# Playtest Bridge API Contract

The bridge API is a localhost-only control and inspection surface for Codex or another local helper. Start it with:

```bash
./playtest-bridge/bridge.py serve
```

Default base URL:

```text
http://127.0.0.1:8765
```

Keep it bound to `127.0.0.1`.

## Read Endpoints

### `GET /status`

Returns active session metadata, trace/log paths, and bridge-managed process status.

### `GET /doctor`

Returns prerequisite checks, known script paths, and local port state.

### `GET /recipes`

Returns available recipe definitions from `playtest-bridge/recipes/`.

### `GET /brief?focus=battle&limit=40`

Returns a compact helper-readable trace summary.

Query parameters:

- `focus`: `all`, `battle`, `tutorial`, `loading`, or `errors`.
- `limit`: recent event count.
- `player`: optional player/actor text filter.
- `text`: optional raw text filter.

### `GET /events?focus=battle&limit=100`

Returns matching trace events with both compact strings and raw event JSON.

Query parameters:

- `focus`: `all`, `battle`, `tutorial`, `loading`, or `errors`.
- `category`: repeatable exact category filter.
- `limit`: recent event count.
- `player`: optional player/actor text filter.
- `text`: optional raw text filter.

### `GET /assertions?recipe=opening-uldah-battle`

Evaluates the named recipe assertions against the active trace folder.

### `GET /compare?left=<old>&right=<new>&recipe=opening-uldah-battle`

Compares two snapshot folders, session folders, or session ids. If a session is supplied, the latest snapshot is used.

### `GET /logs?service=map&limit=200`

Returns recent captured log lines for `web`, `lobby`, `map`, or `world`.

### `GET /sessions`

Lists known bridge session folders.

## Control Endpoints

### `POST /start`

Starts bridge-managed services.

```json
{
  "services": "web,lobby,map,world",
  "fresh": true,
  "new_session": true,
  "trace_dir": "/tmp/aetherxiv-traces"
}
```

### `POST /stop`

Stops bridge-managed services.

```json
{
  "services": "web,lobby,map,world"
}
```

### `POST /restart`

Stops and starts bridge-managed services.

```json
{
  "services": "web,lobby,map,world",
  "fresh": true,
  "new_session": true
}
```

### `POST /reset`

Resets a local character to an opening tutorial state.

```json
{
  "character_name": "Ian Seven",
  "town": "uldah",
  "apply": true
}
```

### `POST /snapshot`

Captures traces, logs, status, summary, and optional assertions.

```json
{
  "focus": "battle",
  "limit": 200,
  "recipe": "opening-uldah-battle",
  "note": "Captured after goobbue kill."
}
```

### `POST /note`

Adds a timestamped manual observation to the active session.

```json
{
  "text": "The client showed the defeat message before zone-out."
}
```

### `POST /build`

Runs the legacy build helper.

```json
{
  "restore": false,
  "configuration": "Release"
}
```

### `POST /smoke`

Runs local smoke checks.

```json
{
  "allow_missing_staticactors": true
}
```
