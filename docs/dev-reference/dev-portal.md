# Aether Dev Portal

The local dev portal is served by `tools/run-web.sh` and is available at:

```text
http://127.0.0.1:8080/dev/
```

The first portal module is the Enemy Restoration Workbench. It reads the local
server database and shows:

- actor class, appearance, pool, group, and spawn readiness
- missing or present base Lua scripts for actor class paths
- provisional `!pinspawn` groups
- appearance review state from `server_battlenpc_appearance_audit`
- recent diagnostic trace files from `/tmp/aetherxiv-traces`
- safe in-game preview commands

Use `!previewappearance <appearanceId>` for incomplete enemy candidates. It
spawns a known-good shell actor and applies the candidate appearance, so blank
or unverified `classPath` rows do not need to be guessed. The preview actor is
labeled as `app <appearanceId>` when the running map server includes the
`SetCustomDisplayName` helper.

Use `!previewrange <startAppearanceId> <count>` to lay out a small labeled grid
of nearby appearance IDs. This is the fastest way to visually search a local
range when the database suggests the right area but the exact actor is unclear.

Use `!previewpair <serverActorClassId> <appearanceId>` when a row has enough
server data to attempt a real spawn. It places the real actor class beside the
safe-shell appearance preview. Mark the portal row as `Match`, `Wrong`,
`Unsafe`, or `Unsure` after comparing them in game.

Use `!spawn <actorClassId>` only for actor classes with nonblank, verified class
paths.

The portal audit table is intentionally separate from durable spawn tables.
Rows in `server_battlenpc_appearance_audit` are workbench evidence only; promote
confirmed enemy data through explicit SQL migrations.
