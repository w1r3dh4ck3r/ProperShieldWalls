# Session State — ProperShieldWalls

## Current Task
**PSW IS DONE AND MERGED.** `feat/cramped-melee-v2` → `master` (`9fae4a1`, 50 commits). Master builds clean,
38/38 tests pass. All four features in-game validated. Perf swept: PSW is **0.149% of frame cost**.

**NEXT SESSION IS NOT PSW — it is the frame eaters (Mark's call: "3 no doubt!").**

## Next Step — #1: FIX THE FRAME EATERS (this is the job)
A clean 300v300 perf sweep (attribution OFF, so these ms are trustworthy) found the modlist's real cost, and
**none of it is ours**:

| Owner | % of frame cost |
|---|---|
| **ArtemsCinematicCombat** (`CCShieldTauntTroopsData.OnMissionTick` 17.2% + `CinematicCombatMissionLogic` 4.5%) | **21.8%** |
| **RBM `AgentStatusBar.UnitStatusMissionView.OnMissionTick`** — a UI status bar. Also the single worst hitch in BOTH runs (**56 ms**). Probably just a SETTING, not a code fix. | **13.2%** |
| BetterPikes / StaminaSystem / BreakablePolearms | 3.5 / 2.6 / 1.3% |
| ProperShieldWalls | 0.149% |

**Two mods eat 35% of Mark's frame time.** Start with `RBM AgentStatusBar` — a status-bar UI costing 13% and
throwing 56 ms hitches smells like a toggle. Use the **`bannerlord-perf-sweep`** skill; it owns the whole loop
(enable → battle → evaluate → **turn the flags back off**) and already records these findings.

## Then — #2: memory long-battle capture
**INCONCLUSIVE, NOT CLEAN.** 52 samples over one 4.3-min battle captured exactly ONE GC cycle: 273.8 → 287.5 MB,
GC drops 48.5 MB to 238.9, then the floor rises monotonically back to 274.0 and the battle ended. A rising floor
WITHIN a cycle is normal; ACROSS cycles it is a leak. **One cycle cannot tell them apart.** Needs a LONG battle
(or several back-to-back) for a second peak/floor pair. No 50 MB spikes fired. This is the only open item touching
Mark's "a leak is a bug to find, never a reason to play shorter" rule — do not call it clean.

## Then — #3: the Square census (the last PSW gap)
Square has **never appeared in a diagnostic census**. Its rank-means-perimeter geometry is decompile-verified but
never seen in-game. Same code path as ShieldWall, so risk is low. `DiagnosticLogging` is back ON — the next battle
with a Square captures it automatically. Look for `Square spacing=0 interval=0.000 eligible=1` + a swap count.

## Files to touch next
Not in this repo. The work is in the frame-eater mods (`ArtemsCinematicCombat`, RBM/`RBMFork`) and their MCM
settings. PSW itself needs nothing.

## Notes
- **PARKED, needs an A/B:** `RTSCamera.CommandSystem` calls `Formation.get_CalculateHasSignificantNumberOfMounted`
  **213,684,301×** per battle (~1.19 M/sec). Its true cost is **UNMEASURABLE** the way we measured: with attribution
  OFF there is no per-patch table, and the prefix runs inside vanilla `Formation.Tick`, so its cost is billed to the
  ENGINE, not to RTSCamera's owner total (0.26%). **0.26% is NOT an acquittal.** Run 1's 330,000 ms is INFLATED by
  the attribution stopwatch — **the call count is real, the ms is not.** Only disabling the mod for one battle prices it.
- Two profiler defects were FIXED + deployed in MapEventNullFix this session (`a12b9b8`, `0f81910`, branch `main`):
  the report **left-truncated** the Method column (which is why the 213M-call entry had no owner), and `MemoryTracker`
  only hooked `Campaign.DailyTick`, which never fires mid-mission ⇒ ZERO battle memory data.
- **New MCM keys must be hand-written into the live JSON** or they read as `0`/`false` while looking perfect in source.
  `MemorySampleIntervalSeconds` hit this exact trap this session.
- **Check "is Bannerlord running" with `tasklist.exe`, NEVER `pgrep`** — `pgrep -f Bannerlord` matches its own command
  line and returns a false positive. Burned a turn on this.
- A build does not deploy. Use `bl-deploy`. The `Deployed ProperShieldWalls to:` line a build prints copies
  **SubModule.xml only**, never the DLL.
- Gemini's round-1 "stale snapshot" Critical was **REFUTED from the decompile** — `ReconstructUnitsFromUnits2D`
  rebuilds only the flat `_allUnits` list and assigns no rank/file indices. Do not re-open it.
- Perf instruments are currently **OFF** and PSW `DiagnosticLogging` is **ON** (ready for the Square census).
