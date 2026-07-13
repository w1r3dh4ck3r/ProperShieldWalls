# Session State — ProperShieldWalls

## Current Task
**PSW itself is DONE and MERGED** (`9fae4a1`, master, 38/38 tests, 0.10–0.19% of frame). No PSW code has been
touched for three sessions. The live work is in sibling repos. **The 2026-07-12 hard freeze is now DIAGNOSED
(2026-07-13) and waiting on ONE free piece of evidence from Mark — a process dump at the next freeze.**

## Last Action (2026-07-13, latest) — ACC free-cam camera hijack FIXED; a naive gate would have been worse
Verified both halves of a fix that had been running on inherited guesswork. **RTS Camera really does set
`Mission.MainAgent.Controller = AgentControllerType.AI`** in free-cam (`Utility.AIControlMainAgent()`) and never
nulls `Agent.Main` — so ACC's `== Agent.Main` cinematic paths kept firing while the AI drove the body. The gate is
vanilla's own **`Agent.IsMine`** (`=> Controller == AgentControllerType.Player`). **`Mission.MainAgentController`
does NOT exist** — a subagent proposed it; it would not have compiled.

**The finding: "just early-return" would have been strictly WORSE than the bug.**
`MissionGauntletCinematicCombatView` is *not* camera-only — `HandleLookingAtOnTick` **hides every agent within 3 m
of the shot** (`SetVisible(false)`). A bare `return` would strand the camera **and leave those agents permanently
invisible** if free-cam were entered mid-killmove. The gate therefore **RELEASES** (restore hidden agents + tear the
camera down, mirroring each view's own idiom) before returning. Same family as the CrashDumper lesson: an
intervention that leaves the subject half-configured is worse than no intervention. "It's a MissionView, so it's
just camera" was **inference, not reading** — the advisor caught it.

Two camera-movers only (both via `MissionScreen.CustomCamera`): `MissionGauntletCinematicCombatView` and
`...ViewMercy`. No third site. Killmove animation, lock-on and slow-motion untouched by design.

## Prior action — v3.11.23 VALIDATED over 3 back-to-back battles, and the dt-cap fix is now REFUTED
**Run read 2026-07-13 09:33** (`MapEventNullFix20260713.log`, session `09:06:08`, v3.11.23). Three battles
back-to-back — `09:08:56 → 09:21:13 → 09:26:20 → 09:32:53` (~24 min of mission time), one of them a **1000-agent**
fight. **No freeze. No hang.** `TickWatchdog` was **armed** (`dumps=OFF`, log-only) and **never fired**, so no tick
stall >30 s occurred — positive evidence, not just absence. **Two AVEs, both suppressed and recovered** (`resuming
after AVE #1 (skipped 15 ticks)` ×2). The v3.11.22 CrashDumper regression is **fully closed**; do not reopen it.

### THE FINDING: the high-load dt cap ENGAGED for the first time — and the AVE fired anyway
Prior runs all sat at 406–780 agents, **under** the `HighLoadAgentThreshold = 800` gate, so `MaxDtHighLoad = 0.020f`
had never once engaged. Mark's 1000-agent battle finally crossed it: **775 clamp lines at 0.020**, agents up to 1000.
Then **AVE #1 fired at `entities~809, dt=0.0200`** — i.e. **with the mitigation in force**.

That `dt` is the **post-clamp** value. `MissionTickGuardPatch.Prefix` takes `ref float dt` and writes
`dt = activeCap` (`:320`); the Finalizer prints that same argument (`:364`). **So native `Mission.Tick` was handed a
0.0200 step and faulted regardless** — the cap did its job and the crash did not care.

**Consequence — a queued item is now dead:** *lowering `HighLoadAgentThreshold` below 800 cannot fix this crash
family.* AVE #1 was already **above** the gate and already receiving the tighter cap. Do not spend a run on it.

**The fault signature never matched the dt theory either.** `FaultVA 0xF8 (READ)` is null-base + field offset —
the reference doc's own address dictionary (§10) puts `0x00–0xFF` at "**null pointer + small struct offset**",
whereas §13's formation-overflow-from-dt-spike chain predicts a *use-after-free heap-shaped address*. Two
independent refutations. §13's root-cause chain is **not supported by the evidence**; treat it as history.

**Strategy therefore moves from PREVENTION to RECOVERY.** The native walker is not patchable from managed code.
Suppress-and-skip is holding **9/9 lifetime AVEs** (7/7 on 07-12 + 2/2 today) — that *is* the mitigation, and it works.

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

## Next Step — READ THE CENSUS RESULTS from the run Mark is fighting right now
**Mark is mid-run as of 2026-07-13 15:04**, fighting back-to-back battles with the upgraded census armed.

**When he returns — read `Configs/ModLogs/MapEventNullFix20260713.log` (or 0714), newest `SESSION START`:**
```bash
grep -n "subscriber:"    <log>   # <<< THE ANSWER: names the object each leaking static event is pinning
grep -n "STATIC-CENSUS"  <log>
grep -n "MEMORY(retained)" <log> # after-teardown lines only; expect ~+17-21MB/battle again
```
`grep subscriber:` is the whole point of the run. If a `Target` type is **mission-scoped** (a MissionView /
MissionBehavior / mission-owned input component), **that is the leak** — it pins the whole dead Mission.
If every Target is `static …` or a long-lived singleton, the events are exonerated and the root is elsewhere
(then: `NOTHING GREW` guidance in the census's own output, or a heap dump with `!gcroot`).

**Then turn `EnableMemoryTracker` back OFF.** Its forced per-mission GC is not free.

### ACC free-cam fix — camera VALIDATED IN-GAME; slow-mo NOT separately confirmed
Mark, 2026-07-13: **"the fix to the killmoves worked"** — the camera no longer snaps into a killmove on his
AI-driven character. **He reported the camera only.** The slow-motion gate is deployed and IL-verified but
**he has not confirmed a masterstrike in free-cam without the 0.2× drop.** Do NOT record it as validated.
Ask him next session. (`ArtemsCinematicCombatFork@5f28961`, live DLL sha-verified, `get_IsMine` ×3.)

**Still untouched, deliberately:** lock-on (`EnableLockOn`, ~449/~8403/~8414) still gates on `== Agent.Main`.
Not reported as a problem — leave it unless Mark sees aim-assist weirdness in free-cam. The killmove/masterstrike
**animations** are untouched by design.

### THE CENSUS RAN (2026-07-13 14:18 launch) — leak replicated a 3rd time, narrowed to ~15 roots, root NOT yet named
**Leak REPLICATED, third independent session.** `MEMORY(retained)` at `after-teardown` (forced collect, `agents=0`):
`262.8 → 282.3 → 298.0 MB` = **+19.5, +15.7 MB/battle**. Squarely in the established 17–21 MB band.

**The census produced its first-ever growth data** (baseline + 2 growth reports, 4 battles in one launch).
It cut **77,612 static fields → ~15 roots that grow EVERY battle**. That is the instrument working.

**⚠ THE CENSUS REPORTS ENTRY COUNTS, NOT BYTES.** "+636 entries" ≠ "17 MB". Never read the top of that list as
the leak — rank by *mechanism and scale*, not by count. This is the trap the numbers are shaped to spring.

#### The prior prime suspect is DEAD, and the new one is a different mechanism
- **RBMAI's Agent-keyed statics (the old suspect) did NOT appear in the census at all.** Drop it.
- **RBMAI's FORMATION-keyed statics DO leak** — `OverrideBehaviorAdvance.advanceScaleStartStorage` /
  `advanceLastTickStorage`, `OverrideBehaviorDefend/HoldHighGround.positionsStorage`,
  `OverrideBehaviorMountedSkirmish.rotationDirectionDictionary`. **Verified in source: never cleared** (only
  `OverrideMovementOrder.positionsStorage` is, `Tactics.cs:192`). **But they are arithmetically TOO SMALL to be
  the 17 MB** — a dead agent leaves its formation (`Agent.Formation = null` on removal, vanilla `Agent.cs:15529`),
  so a retained `Formation` roots only its **survivors**. Those battles ended with **52** and **105** agents while
  `DotNetObject.DotnetObjectReferences` grew **+321** and **+280**. Formations cannot root ~300 objects when 52
  were left. **Real leak, wrong scale. Do NOT name it as the root.**
- **NEW PRIME SUSPECT — two never-unsubscribed static events:**
  `TaleWorlds.InputSystem.Input.OnGamepadActiveStateChanged` (**+13/battle**) and
  `HotKeyManager.OnKeybindsChanged` (**+12/battle**). A static event's publisher lives forever and **pins each
  subscriber's `Target`**; if even one Target is mission-scoped it roots that whole dead Mission ≈ one battle's
  agents ≈ 17 MB. The +300 magnitude fits "a whole dead mission retained" and nothing smaller does.
  **Still a suspicion — counts cannot name it.**

#### The instrument that names it is BUILT, DEPLOYED and LIVE-VERIFIED (`MapEventNullFix@858fad7`)
For any grown `[event]` root it now walks the invocation list and logs **each subscriber's `Target` type**
(static handlers labelled as such — they pin nothing, so they cannot be the leak). Also **excludes its own
`_prevStaticCounts`**, which grew every battle by construction and topped the very first report it produced.
Live DLL sha-verified; `x{n} subscriber:` literals confirmed present in the shipped binary.

**NEXT RUN (still armed — `EnableMemoryTracker` is true): 4+ back-to-back battles, ONE launch.** Then grep
`subscriber:` — that names the mod and the object. Expect the answer, not another suspicion.

**Separate REAL bug, logged not fixed:** `HarmonySharedState.originals` grows **+230/battle** ⇒ something
**re-patches Harmony every mission**. Small bytes, not the 17 MB — but it is a genuine bug. Don't conflate.

**Turn `EnableMemoryTracker` back OFF once the root is named.** Its forced per-mission GC is not free.

**The 07-12 freeze remains the other open question** — it did NOT recur across 3 back-to-back battles, so it is
**intermittent, not load-gated**, and there is still **no dump**. Ask is unchanged and free: at the next freeze,
per-core CPU (pinned core = spin, ~0% = deadlock), then Task Manager → right-click
`Bannerlord.BLSE.Standalone.exe` → *Create dump file*, **THEN** kill. `TickWatchdog` prints a banner saying so.
(The census's forced GC was previously held back so it would not perturb this repro. The freeze not recurring
across 3 battles is why that hold was lifted — if the freeze suddenly returns this run, that trade is worth
re-examining before blaming the census.)

## Files to touch next
Nothing queued for EDIT — the next action is a deploy + Mark's in-game validation. If the fix needs iterating,
edit `~/AI/projects/ArtemsCinematicCombatFork/scripts/perf-fixes.patch` (**NOT** `src/` — it is GENERATED;
`normalize.sh` destroys direct edits), then re-run `./scripts/normalize.sh` && build.

**Full report: `~/AI/projects/MapEventNullFix/docs/freeze-2026-07-12-diagnosis.md`** (adversarially verified,
Medium confidence). Fix-plan **steps 2-6 remain UNAUTHORIZED** — three refuters independently killed the claim that
the NRE-fix package prevents the freeze, so shipping it before the dump would make a clean battle weak evidence.

**`EnableMemoryTracker` is OFF on purpose.** The static-root census wants exactly this back-to-back run, but its
forced per-mission GC adds a timing variable to an intermittent freeze repro. The freeze outranks the leak. Arm it
on a LATER run, once the freeze is captured.

### Not yet done (named so it is not silently dropped)
- **DEAD — do not do it: lowering `HighLoadAgentThreshold` below 800.** The 07-13 run settled it: the cap engaged
  at 809+ agents and AVE #1 fired anyway on a 0.0200 step. See the finding above. Left named here only because
  three handoffs queued it; it is now evidence-against, not pending.
- **The boot marker** in `MapEventNullFix.SubModule` that would close `bl-verify-armed`'s known relaunch hole
  (global `CLAUDE.md` documents it). Still not built.
- **The static-root census (`EnableMemoryTracker`) still has NOT run** — it is OFF, so 07-13's 3-battle
  back-to-back run (exactly the shape it needs: baseline at teardown 1, growth from teardown 2 on) produced **no
  leak data**. It was held back so its forced per-mission GC would not perturb the freeze repro. **The freeze did
  not recur, so that reason has weakened** — arming it for the next back-to-back run is now the cheapest open win.

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

<!-- session-state-sync: last written by session 9038547f at 2026-07-13 15:01:29 -0300 -->
