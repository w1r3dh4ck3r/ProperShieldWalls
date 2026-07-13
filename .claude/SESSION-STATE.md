# Session State — ProperShieldWalls

## Current Task
**PSW itself is DONE and MERGED** (`9fae4a1`, master, 38/38 tests, 0.10–0.19% of frame). No PSW code has been
touched for three sessions. The live work is in sibling repos. **The 2026-07-12 hard freeze is now DIAGNOSED
(2026-07-13) and waiting on ONE free piece of evidence from Mark — a process dump at the next freeze.**

## Last Action — I SHIPPED A REGRESSION AND THEN FIXED IT. Read this before anything else.
**v3.11.22's `CrashDumper` HUNG THE GAME.** It made things strictly worse: it turned a *recovered* native AVE into
a hard freeze. Mark hit it on the first try, twice, on a Custom Battle loading screen (2026-07-13 ~08:46 / 08:48).

**Mechanism:** `MiniDumpWriteDump` was called with `hProcess = GetCurrentProcess()` — a **self-dump**. It suspends
every thread but the dumper's. The faulting thread is one of them, parked in `RequestDumpFromVeh` on
`_dumpComplete.WaitOne(30000)` — so it is suspended *while waiting* and can never reach its own timeout. The dump
never returns; the process sits frozen holding a 0-byte `.dmp`. (Hard deadlock vs. pathological slowness is NOT
established and does not matter — same fix.) Evidence: two 0-byte `menf_ave_*.dmp`; **zero** `CrashDumper:` lines
(so `WriteDump` reached neither its WROTE nor its FAILED branch); log's last line = the same second as the dump.

**Fixed + deployed: v3.11.23 (`7ea2291`, live DLL sha `d2769573`, clean).** `EnableCrashDumps` now defaults
**false**. The live MCM JSON (mtime 07-12 23:31) contains **neither** key, so the code default governs — verified.
**A minidump can only be taken safely from ANOTHER process. Do not set `EnableCrashDumps = true`.** Automating it
properly needs an out-of-process helper (spawn an exe with the game PID, `ClientPointers = 1`) — NOT built, NOT
authorized. Offer it to Mark; don't build it unasked.

**CONFIRMED FIXED IN-GAME (2026-07-13):** Mark relaunched, battles load, he ran test battles. Committed + pushed.

Three docs were still telling the next session to build the thing that hung the game — **all three now corrected**:
`docs/crash-diagnosis-reference.md` §5 (the RULE-ZERO prior-art doc — it carried the exact self-dump P/Invoke
labelled "Recommended"), `docs/freeze-2026-07-12-diagnosis.md` (fix-plan step 1), and the wiki page
`bannerlord-stale-agent-crashes` (described the dump as automatic and working). A doc that recommends a bug is
worse than the bug: the bug gets fixed once, the doc rebuilds it.

**The VEH still works.** With dumps off, `RequestDumpFromVeh` early-returns (it was the only blocking call in it),
so it goes back to logging the fault address. **The freeze dump is now MANUAL, and that was always the plan** —
Task Manager → right-click `Bannerlord.BLSE.Standalone.exe` → *Create dump file*, **then** kill. Deadlock-proof by
construction. `TickWatchdog` survives as a log-only stall detector that tells Mark to do exactly that. Also get
per-core CPU: a pinned core says spin, ~0% says deadlock. Analyse in WinDbg (`~*k`, `!clrstack` under SOS).

## Next Step — two items, neither started
1. **The 07-12 freeze is STILL the open question.** Nothing about it was solved this session; we only stopped
   *causing a second one*. The missing evidence is unchanged: **a manual process dump at the next freeze.** If Mark
   reports one, get the dump + per-core CPU before he kills it.
2. **NEW deferred bug — ACC cinematic camera hijacks the RTS view** (Mark, 2026-07-13). Commanding from RTS Camera's
   bird's-eye view, a matched-combat killmove fires on his character and the camera snaps into the animation and
   back out. **Cause:** ACC gates its *player* paths on `Agent.Main`, and `Agent.Main` still points at his character
   in free-cam — RTS Camera reassigns *control*, not identity. **Fix the CAMERA, not the animation** (killmove still
   plays, no combat change): gate on `Agent.Controller` (`get_Controller` verified present in
   `TaleWorlds.MountAndBlade.dll`). **Verify FIRST that RTS Camera actually sets `Controller` to AI — the whole fix
   rests on it.** Call sites, the MCM "Cinematic Camera" setting, and caveats: wiki `artemscinematiccombat-fork`.

## Files to touch next
Only if item 2 is authorized: `~/AI/projects/ArtemsCinematicCombatFork/scripts/perf-fixes.patch` (**NOT** `src/` —
`src/ArtemsCinematicCombat.cs` is GENERATED and a `normalize.sh` run destroys direct edits; the patch is the only
durable place). Read the fork's wiki page before touching either.

**Full report: `~/AI/projects/MapEventNullFix/docs/freeze-2026-07-12-diagnosis.md`** (adversarially verified,
Medium confidence). Fix-plan **steps 2-6 remain UNAUTHORIZED** — three refuters independently killed the claim that
the NRE-fix package prevents the freeze, so shipping it before the dump would make a clean battle weak evidence.

**`EnableMemoryTracker` is OFF on purpose.** The static-root census wants exactly this back-to-back run, but its
forced per-mission GC adds a timing variable to an intermittent freeze repro. The freeze outranks the leak. Arm it
on a LATER run, once the freeze is captured.

### Not yet done (named so it is not silently dropped)
- **The `HighLoadAgentThreshold = 800` gate** — every observed AVE fires at 406–780 entities, so the anti-crash dt
  cap (`MaxDtHighLoad = 0.020f`) has **never once engaged**. A one-constant change, deliberately NOT made: if
  freezes then stop we would not know why, and if they don't we learn nothing. Do it AFTER the dump.
- **The boot marker** in `MapEventNullFix.SubModule` that would close `bl-verify-armed`'s known relaunch hole
  (global `CLAUDE.md` documents it). Still not built.

### What the freeze IS
The main thread stops inside **native `Mission.Tick`** — most plausibly the *non-faulting* flavor of the same
freed-object walk that throws the AVEs. Unmapped memory ⇒ it faults and the guard recovers it (7/7). **Mapped
garbage ⇒ the same walk silently loops** — no exception, nothing logs, process alive and stuck. That is exactly
what the log shows. (Spin vs. barrier-wait is UNVERIFIED; only a dump can say.)

### THREE bugs, not one — do NOT ship a unified "stale-agent" fix
1. **The native walker**, `TaleWorlds_Native+0x660135`, `FaultVA 0xF8` (READ). **SEVEN** AVEs on 07-12 across
   **four launches** — plus 4 more on 07-08, so it is **at least 4 days old, not new**.
2. **The managed NRE.** ACC's `ApplyKillMoveLogic` never validates `agent.Key` (the affector) across 121 synthetic
   `RegisterBlow` sites, with no agent-removal cleanup; vanilla `CustomBattleAgentLogic.OnAgentHit` has exactly
   **three unguarded dereferences** (`affectedAgent.Team`, `affectorAgent.Team`, `affectorAgent.Origin`). A real
   bug — but its causal role in the freeze is **n=1 and NOT established**.
3. **The ~17-21 MB/battle leak is INERT.** RBMAI's stale `Agent` keys are **never dereferenced** (only `item.Value`
   is read), so the leak **cannot** cause the AVE or the NRE. It is a separate bug; the census still names its root.

### RULED OUT — do not re-chase
- **The guard is NOT the freeze.** Exactly **ONE** MissionTickGuard NRE suppression exists in the whole
  157,656-line day log. A suppression spin or logging-cost collapse would have produced spam, not silence.
- **"Stale agents surviving teardown" is NOT required** — one AVE fired in the **first battle of a fresh launch**.
  This kills the old unified theory that the leak and the crashes are one bug.
- **Turning MissionTickGuard OFF is a BAD experiment**: the freeze is non-faulting, so there is no exception to
  convert into a crash — it would leave the freeze equally silent while turning recoverable AVEs into CTDs.
- **GPU/TDR**: the seven `Kernel_141` WER folders are old (their identical dir mtime is a WER flush, not the event
  time); the Windows System log has **zero** TDR events in two days.

### ⚠ AN INHERITED PREMISE THAT IS NOT VERIFIED — check it before touching ACC
The "12 of 13 NRE hits at 13:56–13:58 predate the fork ⇒ the ORIGINAL ACC did it too" claim — the **sole basis for
exonerating our ACC fork** — has an **UNLOCATED SOURCE**. This day-log contains exactly ONE suppression, so those
13 hits are not in it. The exoneration may still be right, but it is **inherited, not verified**. Relocate those
hits before acting on ACC.

## The memory leak — REAL and REPLICATED (~17-21 MB/battle, managed) — but INERT
`MEMORY(retained)` `after-teardown` lines (forced collect, mission gone, `agents=0`), two independent sessions:
`208.4 → 224.9 → 245.8` (+16.5, +20.9) and `210.5 → 227.4` (+16.9). ~17 MB ≈ **one battle's worth of Agents**, and
it accumulates one dead battle at a time ⇒ a static root, not one stale copy.

**It cannot cause the freeze, the AVEs or the NRE** (2026-07-13): the leaked `Agent` keys are **never
dereferenced**. Prime suspect for the root is **RBMAI's Agent-keyed statics** (`Tactics.agentDamage` + siblings,
cleared only at the NEXT mission's `EarlyStart`) — but `BattleStatsLogic.cs:96-136` touches only `item.Value`, so
the stale keys never re-enter native code. Fix it as its own bug, and **only after the census names the root** —
shipping a clear before that is exactly the over-claiming this project keeps paying for.

- **RULED OUT by reading:** the ACC fork's HashSet mirrors and its Agent dictionaries are **INSTANCE** fields,
  cleared in `OnBattleEnded()`. Its only statics hold strings/Types. **Not the root — do not re-suspect them.**
  (`CinematicCombatMissionLogic.Instance` / `CCMissionView.Instance` are static and never nulled, but each new
  mission overwrites them ⇒ one stale mission retained, a CONSTANT, which cannot explain a per-battle CLIMB.)
- **The instrument that finds it is BUILT + DEPLOYED** (`MapEventNullFix@f8e8725`): a static-root census at
  `after-teardown` walking every static field of every non-framework assembly, reporting only what **GREW** since
  the last battle (collections by `Count`, delegates by invocation-list length). Grep `STATIC-CENSUS`.
  It got its **baseline only** — the freeze killed the run. **It is EXONERATED for the freeze** (it ran at 23:24:24;
  the game played on 3 more minutes).
  Needs **4+ back-to-back battles in ONE launch** (baseline at teardown 1, growth reports from teardown 2 on).
- **`EnableMemoryTracker` is currently OFF.** Re-arm only once the freeze is understood.

## Key facts (durable)
- **Metric trap:** the sampled `MEMORY`/`MEMORY(mission)` lines are `GC.GetTotalMemory(false)` = bytes **ALLOCATED,
  no collection forced**. A rising floor in them proves NOTHING (a mission read 395.9 MB with `gen2=0`; a forced
  collect found 265.2 MB actually live). **Only `MEMORY(retained)` can prove a leak.** The instrument now says so
  in its own log output.
- **Never send Mark to fight on an unverified instrument.** `bl-verify-armed MapEventNullFix --expect "…"` (exit 0
  = armed). **After a RELAUNCH pass `--since "HH:MM:SS"`** — the mtime anchor can otherwise match a PREVIOUS
  launch's `Hooked` line and give a stale VERIFIED. Documented in global `CLAUDE.md`.
- **`bannerlord-live-config-guard.py`** (PreToolUse, global) BLOCKS writes to `Configs/ModSettings/**`,
  `LauncherData.xml`, `Modules/**/bin/**` while the game runs, and **fails closed**. Deliberate; reasoned in
  global `CLAUDE.md`. Escape: `touch /tmp/.claude-bl-config-approved` (5-min TTL).
- **ACC rollback path:** the ORIGINAL `ArtemsCinematicCombat` module was archived out of the game folder to
  `D:\Backup\Bannerlord BKP\Removed_ArtemsCinematicCombat_original_20260712` (130 MB). To restore: copy it back to
  `Modules/`, set `IsSelected=true` for `ArtemsCinematicCombat` and `false` for `ArtemsCinematicCombatFork` in
  `LauncherData.xml`. Nothing depends on ACC in either direction, so its load-order position does not matter.
  The stock DLL is also committed at `~/AI/projects/ArtemsCinematicCombatFork/stock/`.
- **Perf A/B (600 agents, measured):** RBM `AgentStatusBar` steady-state 20.1% → **0.59%** of frame (FIXED). ACC
  `CCShieldTauntTroopsData` 10.7–22.2% → **~3%** in 7 of 8 battles (FIXED; battle 1 unimproved at ~21% — suspected
  shield-heavy composition, residual O(n) per-taunter work, UNVERIFIED). PSW unchanged at ~0.15%.
  The RBM 49–56 ms hitch is a **mission-LOAD** cost (all breaches land ~5 s before the mission baseline marker),
  present before and after the fix — it was never the target. Don't re-litigate.

## Still open (not started)
- **`RTSCamera.RTSCameraLogic.OnAgentRemoved`** — 32 SLOW breaches, max 46.8 ms, the plurality of the run. Same mod
  as the unresolved 213M-call `CalculateHasSignificantNumberOfMounted` (only an A/B can price it).
- **Square census** — never captured. 9 missions on 07-12: only `Line`, `Loose`, `ShieldWall`. PSW
  `DiagnosticLogging` is ON, so a Square battle captures it for free.
- **Spear hysteresis** — deliberately NOT built. Only needed if a lone enemy at exactly the 2.0 m boundary makes a
  spearman twitch. Enter 2.0 m / exit ~3.5 m if it ever shows up.
- ACC fork docs (wiki page, project-docs set) still unwritten.

## Files to touch next
**Nothing is queued for edit — and that is deliberate.** The next input is EVIDENCE (Mark's dump), not code.
If Mark authorizes code, do **fix-plan step 1 ONLY**: a watchdog + minidump-on-AVE in
`~/AI/projects/MapEventNullFix/MapEventNullFix/Patches/MissionTickGuardPatch.cs` (+ a new Watchdog class) — it is
the automated form of the manual dump and advances the diagnosis whichever hypothesis wins. Steps 2–6 (ACC's
`agent.Key` validation, the `CustomBattleAgentLogic` null-guard prefix, the leak clear) wait for the dump.
Re-verify the game is closed at build time — and use the BROAD process match, never `grep Bannerlord.exe`.

**Do NOT re-run `bannerlord-crash-diagnose` on this freeze.** Its script targets a campaign-map GauntletUI
BrushWidget crash (rgl logs, Silk.NET, BrushWidget suspects) and points at a stale OneDrive launcher path — the
wrong evidence surface entirely. The purpose-built script for this freeze is saved at
`.claude/projects/-home-w1r3d-AI-projects-ProperShieldWalls/62fb5037-*/workflows/scripts/psw-freeze-diagnose-2026-07-12-*.js`.

<!-- session-state-sync: last written by session b72a371c at 2026-07-13 09:00:21 -0300 -->
