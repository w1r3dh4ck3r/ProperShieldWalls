# Session State — ProperShieldWalls

## Current Task
**PSW itself is DONE and MERGED** (`9fae4a1`, master, 38/38 tests). No PSW code touched for six sessions; this repo
is now just the handoff home. Live work is in `RBMFork` and `MapEventNullFix`.

**The per-battle memory leak is ROOTED and three fixes are DEPLOYED.** Only in-game validation remains.
Full domain knowledge lives in the wiki: **`bannerlord-memory-leak-census`** — read it before touching this.

## Last Action
A PerfView heap snapshot (main menu, after 3 battles, PID 17708) named the roots in ONE snapshot, zero extra
battles. Root: **`RBMAI.OverrideBehaviorAdvance.advanceScaleStartStorage`**, a never-cleared
`static Dictionary<Formation,float>` — and `Formation.Team` → `Team.Mission` means one stale Formation roots the
ENTIRE dead Mission. Fixes deployed + sha-verified in the live DLLs, **not committed until validation** (see below):
- **RBMFork** — `Utilities.ClearAllFormationCaches()` (20 caches) called from `RBMAIPatchLogic.OnRemoveBehavior()`.
- **MapEventNullFix v3.11.27** — `_lastMission`/`_mission` → weak; new `CustomBattleBannerBearersSpawnLogicLeakFixPatch`
  nulls the vanilla `_missionSpawnLogic` at `Mission.EndMission`.

Mark is fighting the validation battles NOW (started 2026-07-13 ~18:40). **Results are pending — do not assume PASS.**

## Next Step — READ THE RESULT OF THE VALIDATION RUN
**⚠ THE SUCCESS BAR IS NOT "0 RETAINED MISSIONS".** The two single-slot roots each keep pinning ≤1 Mission, so a
*working* fix still leaves ~1–2. **The bar: the retained-Mission count must STOP TRACKING the battle count.**

1. Confirm the game is at the **main menu and still RUNNING** (this step has been lost twice — he closes it).
   `tasklist.exe | grep -iE "bannerlord|taleworlds"` → the process is **`Bannerlord.BLSE.LauncherEx.exe`**.
2. Snapshot (needs **Admin** → UAC prompt Mark clicks; output path is POSITIONAL):
   `powershell.exe -NoProfile -Command "Start-Process -FilePath 'C:\Users\w1r3d\Tools\PerfView.exe' -ArgumentList '-noGui','-AcceptEula','-LogFile:C:\Users\w1r3d\Tools\snap2.log','HeapSnapshot','<PID>','C:\Users\w1r3d\Tools\psw_after2.gcdump' -Verb RunAs"`
3. Also take `vmmap` **in the same launch** (before AND after) — the native side is still unpriced and this is free
   to collect: `vmmap64.exe -accepteula -p <PID> "C:\Users\w1r3d\Tools\vm2.csv"`.
4. Analyze: rebuild/run the analyzer (below) and read the exact-match count of `TaleWorlds.MountAndBlade.Mission`.
   - **PASS:** count stays ≤2 after 5–6 battles instead of climbing to 5–6. `Formation` count stops growing 20/battle.
   - **FAIL:** count still tracks battles ⇒ another accumulating root; use `RefGraph` to find which static reaches
     ALL missions (do NOT trust `SpanningTree`, it shows one parent per node and it fingered the wrong static once).
5. **On PASS only:** commit the three dirty repos, turn `EnableMemoryTracker` OFF (its forced per-mission GC is not
   free), and close this out.

## The analyzer (this is the tool — do not build another in-process probe)
`<scratchpad>/gcanalyze/` — .NET 8 console app; reads a `.gcdump` via `GCHeapDump`/`RefGraph`/`SpanningTree` inside
`dotnet-gcdump.dll`. **The scratchpad is session-scoped and WILL be gone** — the wiki page documents how to rebuild
it in ~30 lines (namespaces, the `ForEach`-before-`Parent` gotcha, the referrer idiom). Prior snapshot kept at
`C:\Users\w1r3d\Tools\psw_after.gcdump` — re-analyzable offline forever, no game needed.

## Key facts (durable — the rest is in the wiki)
- **The LIVE RBM fork is `~/AI/projects/RBMFork`. `RealisticBattleAiPerf` is RETIRED** (its own `SUPERSEDED.md`);
  the game's `Modules/` has no `RBM` dir. A fix deployed there is a **silent no-op**. `RBM_WS_Fork` and
  `SmartRBMpatch` are enabled but ship **no DLLs** (XML only) — no duplicate-assembly conflict.
- **Uncommitted on purpose:** `RBMFork` (2 source files + notes.md) and `MapEventNullFix` (v3.11.27) are dirty,
  deployed, and sha-verified. Committed at wrap-up; NOT yet proven in-game.
- **The 07-12 hard freeze** is still unresolved and has not recurred in ~17 battles. Still **no dump**. At the next
  freeze: note per-core CPU (pinned core = spin, ~0% = deadlock), then Task Manager → right-click
  **`Bannerlord.BLSE.LauncherEx.exe`** → *Create dump file*, **THEN** kill. **Never automate this in-process** — a
  self-dump froze the game once already.
- **Square census** (PSW) — never captured; `DiagnosticLogging` is ON, so a Square battle captures it free.
- **ACC:** camera fix VALIDATED in-game. Slow-mo gate CLOSED (Mark's call). Rollback module archived at
  `D:\Backup\Bannerlord BKP\Removed_ArtemsCinematicCombat_original_20260712`.

## Files to touch next
**Nothing is queued for edit — the next input is a SNAPSHOT, not code.** If validation FAILS, the files are
`RBMFork/Source/RBMAI/RBMAI/Utilities.cs` and `RBMFork/Source/RBM/RBM/RBMAIPatchLogic.cs`.
Re-Read before editing — compaction wipes read-state.
