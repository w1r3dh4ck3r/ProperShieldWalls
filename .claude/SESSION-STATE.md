# Session State — ProperShieldWalls

## Current Task
**SHIELD ROTATION IS VALIDATED IN SHIELD WALL. There was no bug. Only Square remains unproven.**

Live DLL: `feat/cramped-melee-v2@c5b9adf` (sha `91f5338a`, verified). 38/38 tests. Patch count still 2.

## The "0 swaps" mystery — SOLVED, not a bug (2026-07-12)
The formation census settled it in one battle. Verbatim:
```
mission 1:  Line         spacing=2 interval=0.760 eligible=0  x2112     <- ONLY Line, the whole mission
            shield rotation : 0 swaps ...   <-- FEATURE NEVER FIRED
mission 2:  Line         spacing=2 interval=0.760 eligible=0  x805
            ShieldWall   spacing=0 interval=0.000 eligible=1  x263      <- eligible, exactly as predicted
            shield rotation : 444 swaps across 263 formation-sweeps (0 skipped as detached)
```
No `errors caught:` line in either ⇒ the sweep never threw.

**Conclusion: the gate and the sweep are CORRECT.** Whenever a ShieldWall formation exists it is eligible
(`spacing=0 interval=0.000`) and the rotation fires on every single sweep. The earlier `0/0` missions simply had
**no formation in ShieldWall/Square at all** — the census proves the formation sat in `Line` for the entire
mission. Mark believed he had ordered Shield Wall; the arrangement was Line regardless. That is a question about
the game's ORDER system (did the order take? did a Charge or an AI mod revert it?), **not** about this feature.

Mark also visually confirmed the shuffle working in-game, and `skipped as detached` is **0** across every sweep
ever recorded (2606 + 263) — the detachment risk is empirically dead, and men trading places at zero spacing does
NOT look wrong. Both of the sprint's two standing risks are now closed by data.

## Next Step — TWO GATES BEFORE MERGE (do not merge on the small-battle data)

### GATE 1 (blocking) — churn + scale. Live DLL is `feat/cramped-melee-v2@e2cd488`.
444 swaps across 263 sweeps in a TINY battle is ~1.7 swaps EVERY sweep, sustained. Totals cannot separate:
  (a) legitimate — shields keep breaking and deaths re-pack the line, so re-sorting is real work; or
  (b) CHURN — we swap, the game re-packs between sweeps, we swap back, forever at 2 Hz.
(b) would be a permanent main-thread cost: every `SwitchUnitLocations` triggers a full
`ReconstructUnitsFromUnits2D` grid rebuild. This is the exact shape of the 2x/sec stall `SpearPreferenceFork`
already ate once. **Mark's standing directive makes this blocking: a cost that only bites at scale or duration is
a bug to find, never something to wave off.**

A `churn check:` line is now in every mission report:
`churn check: X of Y formation-sweeps emitted swaps (max Z in one sweep)` + `<-- CHURNING? formation is not settling`
when more than half of all sweeps still emit swaps.

Two runs settle it:
1. **Static test:** form a shield wall, stand still, NO combat. Swaps should decay to ~0 (a settled formation emits
   nothing). If a static formation keeps swapping → CHURN → fix it (suspect `HasShieldCached` flicker, or the game
   re-packing between our sweeps).
2. **Scale test:** one 500+/side, long battle. Watch frame time and the churn line.

### GATE 2 — Square has still NEVER appeared in a census.
The wall is proven; the schiltron's perimeter behaviour remains a code + decompile argument only. Form a Square
once and the census will print `Square spacing=0 interval=0.000 eligible=1` with real swap counts.

After BOTH gates: `feat/cramped-melee-v2` (13+ commits) is ready to MERGE to `master`.
**Do not build from `master`** until then — it still holds the old othismos source.

Optional, if Mark cares WHY his Shield Wall orders keep reading as `Line`: log arrangement-order CHANGES per
formation. That is a value-of-feature question, not a correctness one.

## Files to touch next
Only if the in-game test finds something. `Behaviours/ShieldRotationBehavior.cs` is the sweep;
`ShieldRotation.cs` is the pure planner (38 tests); `Settings.cs` / `Diagnostics.cs` hold the toggles + report.

## Notes
- **The find of this sprint:** vanilla ALREADY wrote shield rotation and wired it into both ShieldWall and Square —
  then gated it behind `if (Interval <= 0f) return;`, and `ArrangementOrder.GetUnitSpacingOf` returns **0** for both
  of those orders. `Interval = 0.38 × 0 = 0`. **It has never run for anyone, in any playthrough.** Not a mod
  conflict (all 85 enabled mods scanned, none touch the path).
- Square is `RectilinearSchiltronFormation : SquareFormation : LineFormation`, where `fileIndex` picks the SIDE and
  `rankIndex` walks INWARD — so the single rule "shielded men belong at low rank" yields shields-to-the-front in a
  wall AND shields-on-the-perimeter in a square, with no square-specific code.
- **Combat work is DONE and validated** (Mark: *"feels good, the units fight correctly and use their spears"*).
  **Do not touch `WindupTransparencyPatch.Classify` / the `live-arc` guard / `WindupThreshold`.** Mark's ruling: the
  constraint is a FEATURE — "allies in a shield wall are defensive and should not also be super strong on the attack."
- Gemini's round-1 Critical (a stale-snapshot race) was **REFUTED from the decompile** — do not re-open it.
  `ReconstructUnitsFromUnits2D` rebuilds only the flat `_allUnits` list and assigns no rank/file indices.
- Javelin-melee breakage: root-caused, NOT urgent — memory `javelin-melee-breakage.md`.
- A build no longer deploys. Use `bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll`.
- Branch `feat/cramped-melee-v2` is still UNMERGED and is now **8 commits ahead of origin**.

<!-- session-state-sync: last written by session 1566b843 at 2026-07-12 11:21:15 -0300 -->
