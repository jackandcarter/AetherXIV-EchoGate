# FFXIV 1.x Patch Notes Source Index

This file records official Square Enix forum patch-note sources relevant to the 1.23b parity work. It is an index and claim ledger, not a copy of the patch notes.

Rule: do not paste full patch notes into the repository. Record URLs, dates where known, and summarized claims that matter for implementation.

## Official Forum Index

Source index verified from the official Patch Notes forum on 2026-06-15:

| Version | Official URL | Local status |
| --- | --- | --- |
| 1.18a | https://forum.square-enix.com/ffxiv/threads/19719-patch1.18a-Patch-1.18a-Notes | Indexed, not audited for stats |
| 1.18b | https://forum.square-enix.com/ffxiv/threads/20351-patch1.18b-Patch-1.18b-Notes | Indexed, not audited for stats |
| 1.19 | https://forum.square-enix.com/ffxiv/threads/24910-patch1.19-Patch-1.19-Notes | Audited for progression/stat system |
| 1.19a | https://forum.square-enix.com/ffxiv/threads/26963-patch1.19a-1.19a-Patch-Notes | Indexed, not audited for stats |
| 1.20 | https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes | Audited for attribute/stat system |
| 1.20a | https://forum.square-enix.com/ffxiv/threads/34137-patch1.20a-1.20a-Patch-Notes | Indexed, not audited for stats |
| 1.20b | https://forum.square-enix.com/ffxiv/threads/35688-patch1.20b-1.20b-Patch-Notes | Indexed, not audited for stats |
| 1.20c | https://forum.square-enix.com/ffxiv/threads/36591-patch1.20c-1.20c-Patch-Notes | Indexed, not audited for stats |
| 1.21 | https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes | Audited for jobs, rested EXP, and stat changes |
| 1.21a | https://forum.square-enix.com/ffxiv/threads/40824-patch1.21a-Patch-1.21a-Notes | Audited for Keeper's Hymn reset path |
| 1.22 | https://forum.square-enix.com/ffxiv/threads/43599-patch1.22-Patch-1.22-Notes | Indexed, needs detailed stat audit |
| 1.22a | https://forum.square-enix.com/ffxiv/threads/45067-patch1.22a-Patch-1.22a-Notes | Indexed, needs detailed stat audit |
| 1.22b | https://forum.square-enix.com/ffxiv/threads/47128-patch1.22b-Patch-1.22b-Notes | Indexed, needs detailed stat audit |
| 1.22c | https://forum.square-enix.com/ffxiv/threads/48097-patch1.22c-Patch-1.22c-Notes | Indexed, needs detailed stat audit |
| 1.23 | https://forum.square-enix.com/ffxiv/threads/50278-patch1.23-Patch-1.23-Notes | Indexed, needs detailed stat audit |
| 1.23a | https://forum.square-enix.com/ffxiv/threads/51545-patch-1.23a-Patch-1.23a-Notes | Indexed, needs detailed stat audit |
| 1.23b | https://forum.square-enix.com/ffxiv/threads/54142-patch1.23b-Patch-1.23b-Notes | Indexed, needs detailed stat audit |

## Audited Claims

| Source ID | Claims recorded in source-of-truth doc |
| --- | --- |
| `OFF-1.19` | Physical levels abolished; class level/EXP replaces rank/skill point terminology; attributes grow from current class and level; combat EXP factors include base level EXP, player/enemy level difference, party size, enemy type exceptions, link bonuses, EXP chains, Guardian's Aspect, and bonus gear; battle and item stat reforms. |
| `OFF-1.20` | DoW/DoM class-scoped attribute points begin at level 10; point gain and per-parameter caps; six basic parameter effects; official auto-attack and Shot bonus stat pairs by class. |
| `OFF-1.21` | Rested bonus; class quest EXP target class behavior; job unlock requirements; soul-crystal job switching; class/job action restrictions; job base attribute changes; class allocation carries to associated job; job level/EXP shared with base class; changed weaponskill attribute contribution. |
| `OFF-1.21A` | Keeper's Hymn resets current-class attribute allotment through the guild-mark NPC path. |

## Next Audit Pass

- Audit 1.22 through 1.23b manually in the browser for stat, damage, EXP, job, action, equipment, materia, food, rested, and class-change changes.
- Promote claims into `docs/LEVELING_STATS_SOURCE_OF_TRUTH.md` only after reading the actual note text.
- Keep a short claim summary and source URL; do not store the full patch-note body.
