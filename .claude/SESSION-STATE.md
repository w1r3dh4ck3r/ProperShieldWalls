# Session State — ProperShieldWalls

## Current Task
Feature-isolation test campaign. Battles 1–3 ran all three features at once, so no result was
attributable to any single feature. Mark is now running one feature per mission.

## Last Action
Wrote `docs/06-TEST-PLAN.md` (Tests 0/A/B/C/D). Added a config stamp to the mission report
(`Diagnostics.DescribeConfig`) so each run is self-labelling, then built + deployed:
live DLL is `feat/cramped-melee-v2@8d3153e` (combat behaviour identical to `674147c`; diagnostics only).

## Next Step
**Waiting on Mark's in-game results — do not start coding.** The key run is **Test D**: `live-arc` reject is
literally `AttackProgress >= WindupThreshold`, and `Windup Threshold` is an in-game slider (max 0.60). Running the
same fight at 0.25 vs 0.60 answers complaint #2 (surrounded enemies unhittable) with NO code change.
- 0.60 fixes it → make it the default / widen in code.
- 0.60 helps but doesn't → the `live-arc` guard must be removed entirely (reverses the "ally in front still stops
  the blade" ruling — Mark's call).
- 0.60 changes nothing → wrong mechanism; go back to the collision data.

When results land: read `Documents/Mount and Blade II Bannerlord/PSW_diag.log` — every mission report now carries
its own `config:` line, so runs need no manual tracking.

## Files to touch next
- `Patches/WindupTransparencyPatch.cs` — the `live-arc` guard is at `Classify()`, lines 106–111 (Test D outcome)
- `Settings.cs` — `WindupThreshold` default/range (0f–0.6f) if the fix is a widened threshold
- `docs/06-TEST-PLAN.md` — record results
- `notes.md` — handoff entry

## Notes
- Back-rank spear investigation (queued 2026-07-10) is **DONE** — it was solved in the sibling repo
  `SpearPreferenceFork` (`73b60bc`, "behavior validated"). Do not re-open it here.
- A build no longer deploys. Use `bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll`.
- Branch `feat/cramped-melee-v2` is still UNMERGED.

<!-- session-state-sync: last written by session 17771830 at 2026-07-11 21:05:01 -0300 -->
