# Session State — ProperShieldWalls

## Current Task
**Shield rotation WORKS, but fired in only 2 of 6 missions. Diagnosing why. Instrumented build deployed.**

Live DLL: `feat/cramped-melee-v2@c5b9adf` (sha `91f5338a`, verified). 38/38 tests. Patch count still 2.

## The open question (this IS the task)
6 missions ran on the rotation build. **2 worked**: 379 swaps / 1766 formation-sweeps, and 334 / 840 — with
**0 skipped as detached in both**, which empirically KILLS the detachment risk (melee does not detach men).
Mark also SAW the shuffle in-game and says the battles feel good.

**4 missions logged `0 swaps across 0 formation-sweeps`** — no formation ever passed the `formation.Interval <= 0f`
gate. Mark confirms he ordered **both Shield Wall and Square** in those battles, so this is a REAL bug, not
"he never formed up".

Three worlds all print an identical `0/0`, and the old report could not separate them:
1. no formation was actually in ShieldWall/Square (spacing never reached 0);
2. formations WERE in those orders but `Interval` was not 0;
3. `Sweep()` threw on its first tick — the catch routes to `Debug.Print`, which **nothing here captures**.

**RULED OUT from the decompile — do not re-chase:** `Formation.Interval` takes a cavalry branch when
`CalculateHasSignificantNumberOfMounted`, and `CavalryInterval(0) = 0.18f`, NOT 0 — so a *mounted* shield wall is
skipped by our gate. That is CORRECT, not the bug: vanilla's rotation uses the same `Interval`, so it still runs
there and we rightly defer to it.

## Next Step — MARK AT THE KEYBOARD
Run ONE battle, order **Shield Wall** (a Square too if convenient). Then I read `PSW_diag.log`.
The report now carries a **formation census** (every formation seen: arrangement order, unit spacing, computed
interval, whether it passed the gate) plus `errors caught: N   <-- SWEEP IS THROWING`. That separates all three
worlds in a single run, with no guessing:
- `ShieldWall spacing=0 interval=0.000 eligible=1` but 0 swaps → gate is fine; the swap logic isn't firing.
- `ShieldWall spacing=2 interval=0.760 eligible=0`             → world 2: spacing is not what we assumed.
- `(no formations seen at all)`                                → world 3, or the behaviour is dead.
- `errors caught: N` > 0                                       → world 3, confirmed.

**PRIMARY RISK, only a battle can answer it:** at `Interval == 0` men stand shoulder-to-shoulder, so two men
trading slots must physically walk past each other mid-melee. They may shove/clip/jitter. This may be exactly WHY
TaleWorlds gated the rotation off. If it looks bad: restrict swaps to men not in contact, or raise `Rotation Interval`.

**Detachment risk — DOWNGRADED (verified from the decompile, 2026-07-12).** `Agent.IsDetachedFromFormation` is
`_detachment != null`, and `_detachment` is an **`IDetachment`** — the STANDING-POINT system (siege ladders, walls,
engines; see `detachment.IsStandingPointAvailableForAgent`, Agent.cs:1143). **Ordinary melee contact does NOT
detach an agent.** So in a field battle men keep valid file/rank while fighting and the sweep will see them.
A large `skipped as detached` count is therefore expected mainly in SIEGES, not open-field tests.

**Live everywhere, not just the test arena:** `ShieldRotation` is `true` in the live MCM JSON, so this
not-yet-in-game-validated behaviour is active in EVERY battle, in the same DLL as the validated combat work. It is
`RequireRestart=false`, so it can be switched off mid-battle from Mod Options if it misbehaves.

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

<!-- session-state-sync: last written by session 1566b843 at 2026-07-12 11:14:09 -0300 -->
