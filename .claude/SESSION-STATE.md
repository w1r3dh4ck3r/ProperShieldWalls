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

## GATE 1 — CHURN: **PASSED, DISPROVEN BY DATA** (300v300, 2026-07-12). Live DLL `feat/cramped-melee-v2@e2cd488`.
```
mission 1:  churn check: 88 of 443 sweeps emitted swaps (20%, max 38 in one sweep)
mission 2:  churn check:  7 of 209 sweeps emitted swaps ( 3%, max 15)
mission 3:  churn check: 213 of 727 sweeps emitted swaps (29%, max 19)
```
**71–97% of sweeps emit ZERO swaps — the formation SETTLES.** The pattern is bursty (a big re-sort when a volley
of shields breaks or a chunk of the line dies, then silence), which is legitimate work, not churn. The
`<-- CHURNING?` warning trips above 50% and was never close. At 300v300 with 17,498 friendly hits in one mission
Mark reported no stall (he caught SpearPreferenceFork's 2 Hz stall immediately, so he is a reliable detector).
**The main-thread cost concern is dead. Do not re-open it.**

## UNEXPECTED FINDING (benign, but know it)
The census caught formations that are NOT ShieldWall/Square sitting at spacing 0:
```
Skein  spacing=0 interval=0.000 eligible=1  x27
Line   spacing=0 interval=0.000 eligible=1  x13
```
`ArrangementOrder.GetUnitSpacingOf` returns **2** for both Line and Skein, yet `UnitSpacing` briefly reads 0 —
almost certainly a transient mid-transition state while an order is being applied. Short-lived (6–13 s).
**The feature did the right thing anyway:** the gate is `Interval <= 0`, NOT a hard-coded enum list, so it fills
vanilla's hole *wherever the hole actually is* — and vanilla's rotation is equally dead in ANY formation at
spacing 0. This is the robust gate paying off. **Do not "fix" it by hard-coding ShieldWall/Square.**

## PERF SWEEP IN FLIGHT (2026-07-12) — read this before touching configs
Mark is running a **clean perf run** right now. State deliberately set (game was CLOSED; MCM clobbers on exit):
- `MapEventNullFix` perf flags **ON**: `EnablePerformanceProfiling`, `EnableMissionBehaviorTiming`,
  `EnableHarmonyPatchAttribution`, `EnablePerfScopeLog`, `EnableMemoryTracker`. All are `RequireRestart=true`,
  so they only bind after a full game restart. Output -> `Documents/.../PerfScope<YYYYMMDD>.log`.
- **`ProperShieldWalls.DiagnosticLogging` = false** for this run — its per-hit lines are synchronous main-thread
  file IO (the pattern that stalled SpearPreferenceFork), i.e. the instrument would sit inside the measurement.
  **So an EMPTY `PSW_diag.log` after this battle is EXPECTED, not a failure.** `ShieldRotation` itself stays ON.
- Backups of both JSONs: scratchpad `cfg-backup/`.
- **AFTERWARDS: turn the 5 perf flags back OFF** (heavy/blunt by their own source comment) and DiagnosticLogging
  back ON for the run-2 feature/Square test. The `bannerlord-perf-sweep` skill owns this whole loop.

## Next Step — GATE 2, the last one: Square has STILL never appeared in a census.
Every census so far shows Line, ShieldWall and Skein — no schiltron. The perimeter behaviour is the one claim in
this sprint still resting on a decompile argument rather than a number. Form a Square in one fight; the census
will print `Square spacing=0 interval=0.000 eligible=1` with real swap counts.

After that: `feat/cramped-melee-v2` (14+ commits) is ready to MERGE to `master`.
**Do not build from `master`** until then — it still holds the old othismos source.

Optional, if Mark cares WHY his Shield Wall orders sometimes read as `Line`: log arrangement-order CHANGES per
formation. A value-of-feature question, not a correctness one.

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

<!-- session-state-sync: last written by session 1566b843 at 2026-07-12 13:33:15 -0300 -->
