# Session State — ProperShieldWalls

## Current Task — SPRINT 3 COMPLETE. Stage 1 ANSWERED and MERGED to master. Nothing pending here.
Mark's goal: **rear-rank spearmen should thrust over their allies' heads into the enemy front rank.**
Stage 1 was measurement-only (zero gameplay change) and it is DONE: built, deployed, measured over two
battles, decision rule applied, merged (`master@37c30a4`, 95 tests, deployed + chain-verified).

**THE ANSWER — do not re-measure, do not re-derive:** decision-rule **row 3 fired. Blocker 2 is REAL.**
Rear ranks are NOT idle (80.6% of weapon strikes come from rank>=1) but wield a polearm in only **3.2%**
of them and **88.2% of their strikes are Swings**. They attack constantly holding **swords**.
**=> Stage 2 is the WIELDING fix FIRST.** The collision fix alone would have been wasted.

**Second measured finding — formation is the deciding variable.** The "man directly in front blocks the
thrust" case is **3.2% in a loose Line (spacing=2) but 29.6% in a packed ShieldWall (spacing=0)**, read
against rank>=1 polearm thrusts. So the Stage 2 premise IS sound in packed order — worth doing, after wielding.

Spec (carries both correction boxes): `docs/superpowers/specs/2026-07-21-rank2-thrust-measurement-design.md`
Plan: `docs/superpowers/plans/2026-07-21-rank2-thrust-measurement.md`
Ledger: `.superpowers/sdd/progress.md` (Sprint 3). Numbers + reasoning: `notes.md` 2026-07-21 pt1 + pt2.

## Last Action (2026-07-21) — merged, redeployed from master, wrapped
Branch `feat/rank2-thrust-census` merged `--no-ff` into master and redeployed so the manifest names master.
The branch is fully contained in master and is safe to delete.

## ⚠️ TWO INSTRUMENT DEFECTS — fix BEFORE reusing this census for anything
1. **`reach>=200` bucket is mis-calibrated.** Across ~9000 events only `<120` and `120-199` ever appeared;
   native `mpitems.xml` tops out at `weapon_length=200`. The threshold came from an assumed "~3m spear" that
   does not match this game (weapons cluster 100-200cm; crafted Handle pieces DO reach 295.5cm, so it is the
   extreme tail, not impossible). **Re-bucket around 150/180/200.** The READ is sound — `WeaponLength` is a
   plain int cm from the `weapon_length` XML attr (decompile-verified).
2. **The `IN FRONT` line prints a misleading denominator** (`% of rank>=1`, which mixes in ~3000 sword
   swings → reads 1% when the meaningful figure is 30%). Must be `% of rank>=1 polearm thrusts`.
   This is the SAME denominator trap a review already caught on the reach line — it survived onto another
   line. Pattern written up in the wiki: [[patterns-across-projects]].

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
1. **Restore the two measurement toggles.** They were changed for the census run and are still set for it:
   MCM → Proper Shield Walls → Debug → **Diagnostic Logging back OFF** (the log is already ~646 KB), and
   General → **Cramped Attack Gating back ON**. Neither needs a restart.
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
**The wielding fix is DONE and lives in `~/AI/projects/SpearPreferenceFork`** (branch
`feat/rear-rank-spear-wield`, shipped + deployed 2026-07-21). It did not land in RBMFork or
PickupMeleeWeapons: the root cause was `SpearPreferenceFork`'s OWN sidearm Schmitt trigger being
rank-blind, so a second-rank man ~2 m from the enemy his neighbour is fighting tripped the "enemy on top
of me, draw the sidearm" rule. That model is the LAST WRITER of the favour multipliers, so this was never
the dead-on-arrival RBM-favour path. See that repo's `.claude/SESSION-STATE.md` and
`docs/superpowers/specs/2026-07-21-rear-rank-spear-wielding-design.md`.

**What this repo may be asked for next:** if one battle is to both calibrate the new settings AND validate
the fix, **PSW's `DiagnosticLogging` must be armed in the same battle** — SpearPreferenceFork's census
records the mod's preference *decision*, never the actual wield, so the polearm-wield outcome number can
only come from here. The two instrument defects noted above (reach bucketing, `IN FRONT` denominator) are
still unfixed and must be fixed before this census is reused.

**Dead on arrival, do not propose it:** RBM favour multipliers. `SpearPreferenceFork` resets melee/polearm
favours to `1f` for every human, killing RBM's own multipliers game-wide, and Mark ruled that by design
([[project_spearpreference_clobbers_rbm_favors]]). The lever that DOES work on same-class weapons is
melee **damage** ([[project_troops_prefer_spears_diagnosis]]).

If Stage 2's collision half is ever built, the file here is `Patches/WindupTransparencyPatch.cs` (the
`live-arc` guard inside `Classify`) — and the open unknown is whether the native capsule sweep, once
un-frozen by `ContinueChecking`, actually reaches the enemy behind. Only an in-game test settles that.
Re-Read any file before editing — compaction wipes the harness's read-state.

<!-- session-state-sync: last written by session 19291bfe at 2026-07-17 20:29:52 -0300 -->
