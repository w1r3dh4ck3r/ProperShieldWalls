# Session State — ProperShieldWalls

## Current Task — SPRINT 3: rank-2 thrust census (Stage 1 MEASUREMENT), branch `feat/rank2-thrust-census`
PSW code is live again after seven idle sessions. Mark's goal: **rear-rank spearmen should thrust over their
allies' heads into the enemy front rank.** Stage 1 changes NO gameplay — it only records who the existing
`live-arc` guard turns away (rank / weapon class / reach / strike type / attack direction), so Stage 2 is
designed on data instead of a guess.

Spec: `docs/superpowers/specs/2026-07-21-rank2-thrust-measurement-design.md`
Plan: `docs/superpowers/plans/2026-07-21-rank2-thrust-measurement.md`
Ledger: `.superpowers/sdd/progress.md` (Sprint 3 section) — **read this before re-dispatching anything.**

**Two blockers, not one** (this is the thing not to re-derive): (1) the ally's capsule eats the thrust —
defeated by `MeleeCollisionReaction.ContinueChecking`, which this mod already uses for wind-up and
deliberately declines for a live arc (`WindupTransparencyPatch.cs`, the `live-arc` guard); (2) rear ranks may
not be wielding polearms at all (Mark's own 2026-07-10 note). **If (2) is real, fixing (1) alone buys nothing.**
Stage 1 exists to tell them apart.

## Last Action (2026-07-21) — Tasks 1-3 implemented, reviewed clean, deploying
`LiveArcCensus.cs` (pure, 15 unit tests, 53 total) → `Diagnostics.RecordLiveArc` adapter + mission-report block
→ one call on the `live-arc` reject path. All three task reviews came back SPEC OK / APPROVED.
Plan gap found and fixed during Task 2: the main csproj is old-style and needs an explicit
`<Compile Include>` for every new `.cs` — the test csproj entry alone does not build the mod.

## Deployed 2026-07-13
- **`SpearPreferenceFork@10f2e06`** — **VALIDATED IN-GAME 2026-07-17, flapping stopped. CLOSED.** Schmitt trigger
  on the sidearm decision: the enemy-search radius widens from `MaxDistanceToSwitchToSidearms` (2.0 m) to
  `+SidearmHysteresisGap` (1.5 m ⇒ 3.5 m exit) once a unit already prefers its sidearm, so staying is easier than
  entering and boundary noise cannot flip it. Latches the **`num > num2` boolean**, not the distance. Kept only as
  a record of what shipped; no action.
- **`MapEventNullFix@ff9e4ee` (v3.11.28)** (deployed + verified). `SpawnedItemEntityFix: Initialize() fired` was
  logging **unconditionally on the battle hot path** — 175,359 of 186,482 lines, **94% of a 28 MB day-log** — and
  `SubModule.Log` also does a `Debug.Print` and a UDP datagram per call. Now gated behind
  `EnableMissionTickDiagnostics`. The `TryRemove` is load-bearing and stays unconditional.

## ⏳ AWAITING MARK (ask before anything else)
1. **Sprint 3 measurement battle.** MCM → Proper Shield Walls → Debug → **Diagnostic Logging = ON**
   (`RequireRestart=false`, no relaunch; the saved MCM JSON overrides the C# default of `false`, so this step
   is mandatory). Then a **spear-heavy force in a packed order** (Shield Wall or Line), fought to a **normal
   end** — the report is written by `OnEndMission`, so quitting out produces nothing. Read
   `Documents/Mount and Blade II Bannerlord/PSW_diag.log`, `==== mission report ====` →
   `live-arc census`. **Turn logging back OFF afterwards.**
   **An absent census block is NOT a result** — it is indistinguishable from "no rank≥1 rejections exist",
   which is itself one of the decision-rule outcomes. Prove the block appears on a throwaway mission first.
2. **Do heavy battles feel like SLOW MOTION?** — the dt-clamp question below. Still unasked/unanswered.

## PARKED (2026-07-13) — RBMAI re-emits its ENTIRE detour set every mission. Root NAMED, no symptom, NOT hunted.
This is the `HarmonySharedState.originals +230/battle` item the census logged and nobody chased. **It is NOT a
memory leak — do not file it as one.** It is a repeated **battle-LOAD-time CPU cost** (IL emit + JIT), with a small
write-only-dictionary side-effect that is not worth chasing on its own.

**GATED AND CLOSED: Mark was asked the predicted symptom — a stall at battle load that worsens across a launch —
and answered "no, loading feels fine" (2026-07-13). So it stays PARKED.** The mechanism below is recorded only so
nobody re-derives it; it is not a work item. **Do not reopen without a symptom** — this is the same shape as the
memory hunt that burned six sessions on a growing counter nobody could attach a symptom to.

**Mechanism, verified from both ends (this is why it is trustworthy):**
- `RBM.RBMAIPatchLogic : MissionLogic` — a MissionBehavior — calls `RBMAiPatcher.DoPatching()` from `EarlyStart()`,
  i.e. **once per mission** (`RBMFork/Source/RBM/RBM/RBMAIPatchLogic.cs:10`).
- `DoPatching()` does `harmony.UnpatchAll("com.rbmai")` and then re-patches **every type in the RBMAI assembly**.
- Decompiled `0Harmony.dll` **2.4.2** (the live one, `Modules/Bannerlord.Harmony/`): **both** `Patch` and `Unpatch`
  route through `PatchFunctions.UpdateWrapper`, which calls `MethodCreator.CreateReplacement()` **unconditionally —
  no caching**. Each call emits a brand-new replacement `DynamicMethod`.
- `HarmonySharedState.UpdatePatchInfo` then does `originals[replacement.Identifiable()] = original;` — and
  **`originals` is `Dictionary<MethodInfo, MethodBase>` keyed by the REPLACEMENT, with no `.Remove()` anywhere in
  the assembly.** Write-only ⇒ every superseded DynamicMethod is pinned for the life of the process.
- ⇒ **2 permanent entries per patched original, per battle.** Magnitude is consistent with the census's +230
  (RBMAI has 184 `[HarmonyPatch]` classes / 81 patch-method attributes — the distinct-originals count was **not**
  pinned, and pinning it is scope creep; the mechanism holds at 200 or 260).

**Refuted by arithmetic, do not re-suspect it:** `AIKickNBash` *does* patch on mission start and `UnpatchAll` on
mission end (`AIKickNBashMissionBehavior.cs:108`) — a real per-mission cycle — but it patches exactly **ONE** method
(`Agent.OnAIInputSet`, via reflection in `HarmonyPatcher.cs:48`). ~2 entries/battle. It cannot be the 230.
MapEventNullFix's own loop-patching diagnostics (`MissionBehaviorBreakdownPatch`, `HarmonyPatchAttributionPatch`)
de-dup via `_patched`/`_labelMap`/`_wrapped`, so they patch new methods only — they cannot grow it *every* battle.

**Done this session:** the false comment in our own fork is fixed (`RBMAiPatcher.cs:23-53`). It had claimed
*"Harmony dedups re-applied patches, so re-running per mission is free"* — true behaviourally, **false on cost**,
and it would have rebuilt that wrong belief every session.
**NOT done, deliberately:** `DoPatching`/`EarlyStart` is untouched, and should stay that way. It is unknown why
upstream re-patches per mission (likely to catch mission-assembly types that load late); hoisting it to `SubModule`
is a **behaviour change needing in-game validation** — an expensive battle from Mark — bought with no symptom.

## Next Step — the dt clamp: START FROM THE SYMPTOM, NOT THE COUNTER
`MissionTickGuard` clamped dt **62,000 times in one launch** (2026-07-13). **Before hunting this, know that the
count is expected by construction:** the clamp fires whenever a frame exceeds the cap, and above
`HighLoadAgentThreshold = 800` agents the cap is `MaxDtHighLoad = 0.020f` — **50 fps**. A 1000-agent battle almost
certainly runs below 50 fps, so the guard clamps **nearly every frame**. The number is not evidence of a fault.

**The real question:** clamping dt below the true frame time advances less game-time than wall-clock, so **heavy
battles may be running in slow motion.** That is a gameplay symptom Mark can confirm or refute in one battle, for
free. **Ask him first.** If he cannot feel it, there is nothing here — this project has already burned six
sessions on a growing number with no named symptom, and this is the same shape.
(Source: `MapEventNullFix/Patches/MissionTickGuardPatch.cs:31-33`, clamp sites at `:236` and `:318`.)

**On the memory leak: NOTHING. Do not reopen without a symptom.** It was MEASURED and closed — see the wiki page
`bannerlord-memory-leak-census`. Doubling retained Missions (3→6) cost **0.4 MB** of an 87 MB heap; native
plateaus (6.64 → 8.28 → 8.40 GB). Dead Missions *are* retained 1:1 with battles (a real unbounded-retention bug),
but the gigabytes were never there, and **nobody could ever name the user-visible symptom that started it.**
If a real symptom ever appears, the next roots are `[StrongHandle]` (native-interop GC handle) and the
`MissionSharedLibrary` **mixin statics** (a framework-wide pattern shared by RTSCamera / ACC / FormationFilter).

## Logging state — the modlist is CLEAN for a normal playthrough (audited 2026-07-13, all verified on disk)
- Every MapEventNullFix perf/diag flag, incl. `EnableMemoryTracker`, is **false** (confirmed in the log, not
  assumed: `MEMORY` lines stop at 18:58, config flipped 19:09).
- Newly off: **PSW `DiagnosticLogging`**, **ACC `Debug`** (it printed "X is performing killmove on Y" on-screen).
  `.bak_20260713_prenormal` copies sit beside both.
- **Still noisy, left alone:** `Retinues/debug.log` (~2 MB/day, third-party, unconditional despite its own
  `DebugMode: false`). Not worth a fork.
- **Method that found the storm:** *don't read the flags, read the log FILES.* Every flag was already false; the
  storm came from code no flag governed. `find -mtime -3 -iname "*.log"` found in seconds what a config audit
  never would. A config audit says what we *asked for*; the artifacts on disk say what is *happening*.

## Key facts (durable — the rest is in the wiki)
- **The LIVE RBM fork is `~/AI/projects/RBMFork`. `RealisticBattleAiPerf` is RETIRED** (its own `SUPERSEDED.md`);
  the game's `Modules/` has no `RBM` dir. A fix deployed there is a **silent no-op**.
- **`SpearPreferenceFork/src/` is GENERATED** from the stock DLL. Local fixes live in `scripts/local-fixes.patch`
  (6 hunks), which `normalize.sh` re-applies and **aborts without**. Edit `src/`, then REGENERATE the patch —
  a bare re-normalize would silently ship stock behaviour that still builds and still looks fine.
- **`strings -el` on a .NET DLL is a FALSE NEGATIVE for symbol names.** Metadata names (properties, methods) are
  **UTF-8** — use plain `strings -a`. Only string *literals* are UTF-16. This cost a wrong "absent" reading today.
- **`Debug.Print` output is captured by NOTHING on this machine** — a mod that logs only via `Debug.Print` is
  invisible. That is why the UDP sender's liveness could not be confirmed.
- **PerfView needs Admin**; output path is **POSITIONAL**; it **freezes the target** ⇒ snapshot at the **main
  menu**. Process is **`Bannerlord.BLSE.LauncherEx.exe`**. Snapshots kept at `C:\Users\w1r3d\Tools\*.gcdump`.
- **The 2026-07-12 hard freeze is STILL UNRESOLVED and is a SEPARATE bug** — not recurred in ~23 battles, still
  **no dump**. At the next freeze: note per-core CPU (pinned core = spin, ~0% = deadlock), then Task Manager →
  right-click `Bannerlord.BLSE.LauncherEx.exe` → *Create dump file*, **THEN** kill. **Never automate this
  in-process** — a self-dump froze the game once already.
- **Square census** (PSW) — never captured, and no longer free: `DiagnosticLogging` is now **OFF**. Re-enable it
  in MCM deliberately if it is ever wanted.

## OPEN LEAD (2026-07-13, noted NOT investigated) — AIKickNBash patches the one method the wiki says never to patch
`wiki/harmony-patching.md` has a whole section: **NEVER Harmony-patch `Agent.OnAIInputSet`** — it is an
`[MBCallback]` the C++ engine calls across the interop boundary with three `ref` params, and merely *installing* a
postfix on it folds every character into a spike (confirmed on PSW v2.0.0, game v1.4.7; the patch body never has to
run). **`AIKickNBash` installs exactly that postfix**, and it is `<IsSelected>true</IsSelected>`. It is applied **via
reflection** (`AIKickNBash/HarmonyPatcher.cs:48`, from `AIKickNBashMissionBehavior.TryActivate()`), which is why no
grep for `[HarmonyPatch]` ever surfaced it.

**This is a code-shape match, NOT an observed symptom — do not act on it as fact.** Mark reports no folded
characters, so either it does not reproduce for a reflection-applied postfix or the wiki's trigger is narrower than
stated. **Where it might actually matter:** the **unresolved 2026-07-12 hard freeze + the native AVEs** — a corrupted
native thunk is exactly the shape that yields a faulting address with no managed stack. Check it there first, and
**verify before believing it in either direction.** Cheapest test by far: have Mark disable AIKickNBash for a few
battles and see if the AVEs stop. Costs nothing to set up.

## Files to touch next
**Sprint 3 is code-complete; the next move is Mark's battle, not an edit.** When his numbers land, apply the
pre-registered decision rule in the spec §5 / plan Task 5, then write the result into `notes.md`.
If Stage 2 goes ahead, the file is `Patches/WindupTransparencyPatch.cs` (the `live-arc` guard in `Classify`).
If the dt-clamp thread opens instead: `MapEventNullFix/MapEventNullFix/Patches/MissionTickGuardPatch.cs`.
Re-Read any file before editing — compaction wipes the harness's read-state.

<!-- session-state-sync: last written by session 19291bfe at 2026-07-17 20:29:52 -0300 -->
