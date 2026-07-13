# Session State — ProperShieldWalls

## Current Task
**PSW itself is DONE and MERGED** (`9fae4a1`, master, 38/38 tests, 0.10–0.19% of frame). No PSW code has been
touched for two sessions. The live work is in sibling repos. **The session ended on an unresolved HARD FREEZE —
that is the next job, ahead of everything else.**

## Last Action
Killed the frozen game, turned **every** perf/memory instrument OFF in `MapEventNullFix_v1.json`
(`MissionTickGuard` crash protection left ON), and rolled back **nothing** — see the false lead below.

## Next Step — the HARD FREEZE (2026-07-12 23:27). Stability outranks the leak and everything else.
Use the **`bannerlord-crash-diagnose`** skill (multi-agent Opus). Do NOT hand-roll it. A freeze with no crash
report, a fixed-address native null-deref, and a suspected stale-reference root is exactly its brief.
Run **`bannerlord-backup`** before any destructive mitigation.

### The evidence (all in `Configs/ModLogs/MapEventNullFix20260712.log`)
- **Freeze:** last line `23:27:48` — an NRE suppressed by MissionTickGuard, then the log simply STOPS (main thread
  stuck, not a clean crash). Stack: `ArtemsCinematicCombat.CinematicCombatMissionLogic.RegisterBlow →
  ArtemCore.RegisterBlow → Agent.RegisterBlow → Agent.HandleBlow → Mission.OnAgentHit →
  CustomBattleAgentLogic.OnAgentHit  ← NRE`
- **6 AccessViolationExceptions (18/19/21/23h), IDENTICAL every time:** `FaultVA 0x00000000000000F8 (READ)`,
  `TaleWorlds_Native+0x660135`, inside `Mission.Tick`. `0xF8` = **null pointer + field offset** — native reading a
  field of a freed/absent object, at the SAME site each time.

### ⚠ FALSE LEAD — do not re-derive it (it nearly caused a wrong rollback)
"That NRE is new today ⇒ our ACC fork regressed it" is **WRONG**. 12 of the 13 NRE hits are at **13:56–13:58**;
the fork only went live at **16:23**, so the **ORIGINAL** ACC was doing it too. And "0 hits on 07-09/10/11" is
**not** evidence of absence — `CustomBattleAgentLogic` only runs in **Custom Battles**, which Mark likely was not
fighting those days. **The same caveat applies to the AVEs: "new today" is UNPROVEN, not established.**

### UNVERIFIED HYPOTHESIS (label it as such) — the leak and the crashes may be ONE bug
Something holds **stale Agent references across mission teardown** (proven: ~17-21 MB of dead agents survive each
battle). A managed ref to an agent whose NATIVE side is freed would produce exactly this pair — a native null-deref
at a fixed offset and NREs when vanilla touches a half-dead agent. **Not proven.** The static-root census names the
holder if it is a static collection/event.

## The memory leak — REAL and REPLICATED (~17-21 MB/battle, managed)
`MEMORY(retained)` `after-teardown` lines (forced collect, mission gone, `agents=0`), two independent sessions:
`208.4 → 224.9 → 245.8` (+16.5, +20.9) and `210.5 → 227.4` (+16.9). ~17 MB ≈ **one battle's worth of Agents**, and
it accumulates one dead battle at a time ⇒ a static root, not one stale copy.

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
Freeze first — start from `Configs/ModLogs/MapEventNullFix20260712.log` (AVE + freeze stacks) via
`bannerlord-crash-diagnose`. No source file is queued for edit; do not open one until the diagnosis names it.
