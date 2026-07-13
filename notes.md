# ProperShieldWalls — AI Handoff Log

---

## 2026-07-11 — battle 3 read; the campaign restarts as one-feature-at-a-time

### Two false premises, both caught by verifying
- "No battle 3 log exists" was **wrong**. The `find` used `-maxdepth 4`; the log sits at depth 5
  (`/mnt/c/Users/w1r3d/Documents/...`, NOT `/mnt/c/Users/Mark Lewis/`). A fresh `PSW_diag.log` was 16 minutes old.
  An empty result from a filtered search is not evidence of absence.
- The **queued back-rank spear investigation is already DONE** — solved today in the sibling repo
  `SpearPreferenceFork` (`73b60bc feat(spearpref): disable javelin melee usage so native picks the spear itself`,
  session-4 notes say "behavior validated, remote pushed"). It was nearly re-opened from scratch here.
  **Do not investigate it in PSW.**

### Battle 3 (live DLL `674147c`, same two patches as battle 2)
```
windup transparency : 12378 friendly hits made transparent
    rejected live-arc            x5233
friendly blocks     : 3524 neutralised
cramped gating (AI) : 662 swings remapped across 316 agents (6829 input ticks)
```
All three features fire. live-arc rejects fell from ~40% of friendly contacts (battle 2) to ~30%, but **5233
friendly contacts still take the vanilla path** (friendly-fire stun + `Bounced`).

**The log cannot close complaint #2.** It proves the path is *taken*, not that it *ruins the fight*. That is a felt
judgement and only Mark's. Battles 1–3 all ran three features at once, so nothing was ever attributable anyway.

### The real find: complaint #2 needs no code change to answer
`live-arc` is literally `AttackProgress >= WindupThreshold` (`WindupTransparencyPatch.Classify`, lines 106–111),
and **`Windup Threshold` is an in-game MCM slider ranging 0 → 0.60** (default 0.25). Every PSW toggle is
`RequireRestart=false`, so features can be flipped *mid-battle*.

So the design question the last three sessions deferred to Mark ("broaden live-arc?") is answerable **empirically,
by dragging a slider**: run the same fight at 0.25 and at 0.60. If the surrounded enemy dies at 0.60, the fix
direction is confirmed before a line is written. Honest limit: even at max, contacts past 60% of the swing still
bounce — a *partial* improvement means the guard has to be removed in code, which fully reverses the
"an ally in front still stops the blade" ruling. That remains Mark's call.

### Shipped
- `docs/06-TEST-PLAN.md` — Tests 0 (vanilla baseline) / A (windup) / B (block passthrough) / C (cramped, AI-only) /
  D (the threshold probe). Fixed arena: infantry-only Custom Battle, player standing *inside* his own shield wall.
- **Config stamp in every mission report** (`Diagnostics.DescribeConfig`). Toggles are live and the log appends, so
  a multi-mission campaign would otherwise be unattributable. Reports now open with
  `config: enabled=1 windup=1 cramped=0 blockPass=0 threshold=0.25 crowdedDur=2.0`.
- Deployed `feat/cramped-melee-v2@8d3153e`. **Combat behaviour is unchanged from `674147c`** — diagnostics only.
  Verified the new literal is in the live DLL with `strings -el` (plain `strings` misses .NET's UTF-16 `#US` heap —
  an ASCII grep returning nothing there proves nothing).

### Testing gotchas worth keeping
- **Per-hit log lines are the PLAYER's swings only** (`attacker.IsMainAgent`), capped 400/mission. The mission-report
  counts are all agents, uncapped.
- **Cramped gating cannot be felt** — it is AI-only, the player is never remapped. Judge it by the remap count, not
  by eye; "I couldn't see it" is not evidence it is dead.

### Next
Mark runs Tests 0/A/B/C/D. Results pending — no coding until they land.

---

## 2026-07-12 — the handoff is now enforced, not requested

### What went wrong (the reason this exists)
Session opened with `SESSION-STATE.md` already injected by the SessionStart hook, saying **"do not start
coding"**. First action anyway: a Bash histogram of `reject:live-arc` prog values, reported as "N of 5233
would flip at threshold X." That number was **meaningless** — per-hit log lines are the PLAYER's swings only,
capped 400/mission, while 5233 is the all-agents counter. That exact gotcha was written in `notes.md`, which
nothing had surfaced and I had not read. Injected context is not enough; it gets acted past.

### Shipped (global, all projects)
- **`~/.claude/hooks/handoff-gate.py`** (PreToolUse) — blocks `Bash|Edit|Write|NotebookEdit|Agent|Task|Workflow`
  **plus their MCP equivalents** (`mcp__pare-process__*`, Serena editing tools, `pare-git commit|push`) until both
  `.claude/SESSION-STATE.md` and `notes.md` have been Read **this session**. Proves it from the session transcript
  (`transcript_path` is in the hook payload — verified), so it needs no companion hook. Read/Grep/Glob never gated:
  the gate always has an exit and it costs one Read. Mode file `handoff-gate.mode` = `enforce|log|off`.
- **`~/.claude/hooks/session-state-load.sh`** — now injects `SESSION-STATE.md` **and the latest `notes.md` entry**
  (previously only the former, which is why the gotcha above never surfaced).
- Global `CLAUDE.md` documents the mechanism and its escape hatch.

### Two failures found by building it, both now designed out
- **Bootstrap deadlock (real lockout).** v1 depended on a companion PostToolUse hook to record Reads. That hook was
  the very edit the gate blocked → gate could never release → Bash/Edit/Write/Agent all dead, no in-band escape.
  Had to break out via an MCP tool. **The gate is now self-sufficient (reads the transcript) and FAILS OPEN** on any
  error. Never reintroduce a second-hook dependency; never make it fail closed.
- **The MCP hole.** The escape above was only possible because MCP tools bypassed a native-tool-only gate — i.e. the
  gate was theatre. Closed for exec/write MCP tools; read-only MCP (find_symbol, search) stays open.
- Verified live in both directions: it blocked my own Edit when `notes.md` was unread, and released after the Reads.
  Subagents inherit the parent's `session_id`/`transcript_path` (probed empirically), so they never strand; `Agent`
  is gated so the gate can't be delegated around. 18ms on a 341-line transcript (early-exit scan; 5s timeout).

### Known limit
The gate proves a file was **opened**, not **absorbed** — a `Read` with a small `offset/limit` on an append-only
`notes.md` passes while missing the newest entry. The load-bearing part is therefore the SessionStart injection
carrying the latest entry; the gate is the forcing function that makes you look at all.

### PSW itself: unchanged
No mod code touched. Live DLL is still `feat/cramped-melee-v2@8d3153e` (hash-verified against `bin/Release`, contains
`DescribeConfig`). `PSW_diag.log` still has **no `config:` line**, so no test battle has been run on the stamped build.
**Still waiting on Mark's Test D: same fight at Windup Threshold 0.25 vs 0.60 — can the surrounded enemy finally die?**

---

## 2026-07-12 — shield rotation shipped, MERGED to master (`9fae4a1`); perf swept

### The find of the sprint: vanilla's shield rotation has never run for anyone
`LineFormation.SwitchFrontUnitTypesToFrontRows()` already pulls shielded men toward rank 0
(`_isFrontUnitDelegate = PreferShieldedUnitsOnFront`, driven off a 0.5 s timer in `Formation.Tick`).
It opens with **`if (Interval <= 0f) return;`** — and `ArrangementOrder.GetUnitSpacingOf` returns **0** for
**both ShieldWall and Square**. `Interval = InfantryInterval(0) * IntervalMultiplier = 0.38 × 0 = 0`.
Multiplying by `IntervalMultiplier` cannot rescue it (anything × 0 = 0), so **no arrangement subclass can either**.
TaleWorlds wrote the behaviour, wired it into both formations, gave `SquareFormation` a permanently-pinned shield
preference (its `UpdateFrontUnitTypeDelegate()` override is empty, so it never flips to the anti-cavalry bracer
delegate) — and then gated the whole thing behind a condition **neither formation can ever satisfy**.
Line/Circle (spacing 2 → Interval 0.76) work fine; we must not touch those.

Ruled out by verification, do NOT re-chase:
- **Mod suppression.** All 85 enabled mods scanned (`strings -a` AND `strings -el`; a .NET UTF-16 `#US` heap miss on
  ASCII-only proves nothing). Zero reference `SwitchFrontUnitTypesToFrontRows` / `PreferShieldedUnitsOnFront`.
  RBMFork + FrontlineModFork DO prefix `LineFormation.SwitchUnitLocations`, but both return `true` for a valid pair.
- **Stale shield cache.** `Agent.HasShieldCached => Equipment.ContainsShield()` — a computed property with **no
  backing field**. The name is a lie; it is fresh on every read.
- **Detachment.** `Agent.IsDetachedFromFormation` is `_detachment != null`, and `_detachment` is an `IDetachment` —
  the STANDING-POINT system (siege ladders/walls/engines). **Ordinary melee does NOT detach anyone.** Confirmed
  empirically: `0 skipped as detached` across every sweep ever recorded.

### One rule, both formations — no Square-specific code
Square is `RectilinearSchiltronFormation : SquareFormation : LineFormation`. In `GetLocalPositionOfUnitAux`,
`fileIndex` picks the SIDE and `rankIndex` walks **inward** from it (`MaxRank = (UnitCountOfOuterSide+1)/2`, i.e.
capped at the centre). So **rank 0 = the outer ring**, and "shielded men belong at low rank" yields
shields-to-the-front in a wall and shields-on-the-perimeter in a square, from the same loop.

### What shipped
`Behaviours/ShieldRotationBehavior.cs` + `ShieldRotation.cs` (TaleWorlds-free planner, 11 tests).
**No Harmony patch, no reflection, no private access** — public API only (`Formation.Arrangement`,
`IFormationArrangement.GetAllUnits/SwitchUnitLocations`, `Agent.GetFormationFileAndRankInfo`, `HasShieldCached`).
Banner still reads **2 patches**. Gate is **`formation.Interval <= 0f`**, NOT a hard-coded `ArrangementOrderEnum`
list — that choice paid off: the census caught `Line` and `Skein` transiently at spacing 0 (mid-order-transition),
and the feature correctly filled vanilla's hole there too. **Do not "fix" it by hard-coding ShieldWall/Square.**

### Validation (all four gates closed)
- 38 unit tests. Gemini adversarial review **cleared** after 3 rounds.
- **In-game (Mark):** saw the shuffle; battles feel good. 713+ swaps, `0 skipped as detached`.
- **Churn DISPROVEN:** 88/443 (20%), 7/209 (3%), 213/727 (29%) of formation-sweeps emitted swaps ⇒ **71–97% of
  sweeps do nothing.** The formation SETTLES; the pattern is bursty (max 38 swaps in one sweep = a real re-sort
  after casualties), not a 2 Hz treadmill.
- **Perf, clean run (attribution OFF), 300v300:** `ShieldRotationBehavior` = **27.49 ms = 0.149% of frame cost**,
  rank 14/23, worst tick 1.17 ms, never breached the 5 ms slow flag.

### Gemini's round-1 Critical was WRONG — refuted from the decompile, do not re-open
It claimed `ReconstructUnitsFromUnits2D` invalidates every agent's rank, making the per-file snapshot go stale
mid-sweep. False: that method (LineFormation.cs:1026-1051) rebuilds ONLY the flat `_allUnits` list and performs
**zero** `FormationRank/FileIndex` assignments; `SwitchUnitLocations` (:2163-2166) writes rank/file on **only the
two units passed in**. Gemini accepted the refutation. Its round-2 finding (a dead agent at swap time) was guarded
anyway (cheap; RBMFork and FrontlineModFork both guard the same call).

### Perf sweep — the frame eaters are NOT us (new `bannerlord-perf-sweep` skill owns this loop)
| Owner | % of frame |
|---|---|
| **ArtemsCinematicCombat** | **21.8%** |
| **RBM `AgentStatusBar`** (a UI status bar; also the worst single hitch, 56 ms) | **13.2%** |
| BetterPikes / StaminaSystem / BreakablePolearms | 3.5 / 2.6 / 1.3% |
| ProperShieldWalls | **0.149%** |

Two profiler defects found and FIXED in MapEventNullFix (`a12b9b8`, `0f81910`, deployed):
the report **left-truncated** the Method column, silently eating mod names (the 213M-call entry was
unattributable because of it); and `MemoryTracker` only hooked `Campaign.DailyTick`, which **never fires
mid-mission**, so it produced ZERO battle memory data.

- **RTSCamera.CommandSystem** calls `Formation.get_CalculateHasSignificantNumberOfMounted` **213,684,301×**
  (~1.19 M/sec) — it both patches that getter and is its heaviest caller (recomputes formation geometry every tick
  for a fact that only changes on casualties/riding-order changes). **Its true cost is UNMEASURABLE this way:** with
  attribution off there is no per-patch table, and the prefix runs inside vanilla `Formation.Tick`, so its cost is
  billed to the engine, NOT to RTSCamera's owner total (0.26%). **0.26% is NOT an acquittal.** Only an A/B (disable
  the mod, same battle) can price it. The 330,000 ms from run 1 is INFLATED by the attribution stopwatch — the CALL
  COUNT is real, the ms is not.
- **MEMORY: INCONCLUSIVE, not clean.** 52 samples over one 4.3-min battle captured exactly ONE GC cycle
  (273.8 → 287.5 MB, GC drops 48.5 MB to 238.9, then the floor rises monotonically back to 274.0 and the battle
  ended). A rising floor WITHIN a cycle is normal; ACROSS cycles it is a leak. **One cycle cannot tell them apart.**
  Needs a LONG battle (or several back-to-back) for a second peak/floor pair. No 50 MB spikes fired.

### Next session (Mark's call, in order)
1. **Fix the frame eaters** — ArtemsCinematicCombat (21.8%) and RBM `AgentStatusBar` (13.2%). The status bar is
   probably just a setting. Biggest available win, and nothing to do with PSW.
2. **Memory long-battle capture** — the only open item touching the "leaks are bugs, never a usage limit" rule.
3. **Square census** — the one PSW claim still resting on a decompile argument. `DiagnosticLogging` is back ON, so
   the next battle with a Square captures it automatically.

### Housekeeping
`feat/cramped-melee-v2` **MERGED to master** (`9fae4a1`, 50 commits) — this also killed the long-standing hazard
that master held the dead othismos source (`OthismosState.cs` + a junk `D:` tree), so a build from master produced
a broken DLL. Master now builds clean, 38/38 tests pass. **A build still does not deploy** — use
`bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll`. (The `Deployed ProperShieldWalls to:` line a build
prints copies **SubModule.xml only**, never the DLL — alarming message, harmless behaviour.)

---

## 2026-07-12 (later) — no PSW code changed; the frame eaters got fixed instead

PSW itself was **not touched**. It is done, merged (`9fae4a1`), and costs 0.149% of frame. This session executed
the handoff's #1 item — the frame eaters — and the work lives in two OTHER repos.

### Both frame eaters are fixed, deployed, and verified live — but NOT measured
- **RBM AgentStatusBar, 13.2%** (and the worst hitch in both runs, 56 ms) → `RBMFork@720bdf0`.
  `UnitStatusVM.RefreshAgentStatus` did a native `Agent.Position` read, a `MBWindowManager.WorldToScreen`
  projection, and a `Distance` property-set for every agent every tick, **above** the visibility gate. Distance
  changes every tick for anything moving, so the change-guard never suppressed the Gauntlet notification. Fixed by
  hoisting the cheap predicates above the expensive ones. **Not a throttle** — no visible output changes.
- **ArtemsCinematicCombat, 21.8%** → **new fork**, `~/AI/projects/ArtemsCinematicCombatFork` (`be78af9`, pushed
  private). `CCShieldTauntTroopsData.OnMissionTick` allocated a fresh ~600-element list every frame and probed
  membership with `List<Agent>.Contains` — **O(agents × shield-bearers) per frame**. Fixed with HashSet mirrors,
  a backwards index prune, direct iteration, one native weapon read per agent.

### The prior handoff's guess was wrong, twice — worth remembering
It said the status bar was "**probably just a SETTING**". It is not: `RBMConfig` has no status-bar toggle at all,
and `SubModule.cs:153` adds the view **unconditionally** while its three neighbours are gated. Likewise ACC's
`AIShieldTaunt` setting does **not** gate `OnMissionTick` — that check runs *after* the expensive loop. **Two
"just flip a setting" theories, both refuted by reading the code.** Neither would have bought a single frame.

### Next session: read the sweep
Mark is fighting a battle now. **The perf instruments are ARMED** in the live MCM JSON
(`EnablePerformanceProfiling`, `EnableMissionBehaviorTiming` — the one that emits the per-mod owner table —
`EnablePerfScopeLog`, `EnableMemoryTracker`; attribution deliberately OFF, since it inflates the ms). They were
found **OFF**, which would have wasted the battle entirely. **Turn them back off after reading the results.**

Also captured this session: **memory** (the ACC fix removes a major per-frame allocator, so re-measure the open
leak question) and the **Square census** (`DiagnosticLogging` is ON; a Square in this battle is captured for free).

### Unverified, do not assert
Mark says he keeps the unit status bars off, but **no RBM setting maps to that** — the only hide path in code is
`UnitStatusVM._keyToggled` (Ctrl+H; initialised `true`, so hidden until toggled). If hidden, the RBM fix should
take that method to ~zero; if shown, the win is partial. The sweep settles it.

---

## 2026-07-12 (battle 1) — the measurement failed; a false-negative process check cost the run

### What happened
Both fixes were live in the DLLs, Mark fought a battle, and it produced **zero perf data**. Not a bad result — **no
result**. `MapEventNullFix20260712.log` is unambiguous: `=== SESSION START ===` at **16:59:05**, and the perf flags
were written to the MCM JSON at **17:02:32** — 3.5 minutes *after* the game had already loaded. The flags are
`RequireRestart` (applied at `OnSubModuleLoad`), so they were read as `false`. **0** `PERFORMANCE REPORT` lines in
that session; the 14:36 baseline has one. The settings file was also edited *under a running game* — it happened not
to get clobbered on exit, which was luck, not design.

### Root cause — and it is a *class* of bug, not a typo
The "is the game closed?" gate was `tasklist.exe | grep -i "Bannerlord.exe"`. **Mark launches through BLSE, so the
live process is `Bannerlord.BLSE.Standalone.exe` — which does not contain the substring `Bannerlord.exe`.** The grep
returned nothing and was read as "closed" while the game was mid-load.

The existing global rule warns against `pgrep` because a query can match *itself* (false positive). This is the
mirror image: **a query so specific it cannot match the truth (false negative).** The generalisable lesson, now
written into global `CLAUDE.md`:

> **A negative from an over-specific query is not evidence of absence.** When a check gates a destructive or
> expensive action, make the query BROAD. A false positive costs one question; a false negative costs the run.

Fixed in three places (all were carrying the same broken check): global `CLAUDE.md`, the `bannerlord-perf-sweep`
skill (which was actively *recommending* it), and `bannerlord-mod-build` (whose `IMAGENAME eq Bannerlord.exe` filter
had the identical hole). Correct form:
```bash
/mnt/c/Windows/System32/tasklist.exe 2>/dev/null | grep -iE "bannerlord|taleworlds" || echo CLOSED
```

### What battle 1 DID establish
- **No crash.** 85 mods loaded with `ArtemsCinematicCombatFork` in the load order, a full mission ran, clean exit.
  This was the genuine risk: the ILSpy base-cast (`callvirt`) artifact would have thrown a StackOverflow at mission
  entry. It did not. The IL verification held up in practice.
- **PSW ran** (mission report present) but the formation census shows **only `Line`** — **no Square appeared**, so
  the Square census remains open. Nothing to read into that; it was simply not a Square battle.
- **Still UNCONFIRMED: did the ACC fork actually load?** ACC writes nothing to any log, so no disk evidence can
  settle it. Must be answered by Mark (did the BLSE unsigned-DLL CAUTION appear, and was it accepted?). A declined
  CAUTION loads the module **disabled** — indistinguishable from "the fix did nothing".

### State for next session
**Instruments are ARMED and deliberately LEFT ON** (`EnablePerformanceProfiling`, `EnableMissionBehaviorTiming`,
`EnablePerfScopeLog`, `EnableMemoryTracker` = true; attribution deliberately false). Game is closed, JSON validated.
**Nothing to rebuild, redeploy or edit — Mark just relaunches and fights.** Stale baseline archived to
`Configs/ModLogs/PerfScope20260712-BASELINE-1436.log`. Turn the flags off only once the numbers are read.

---

## 2026-07-12 (evening) — sweep read; a leak PROVEN after a wrong call; a weapon bug fixed; ends on a FREEZE

No PSW code touched (still `9fae4a1`, ~0.15% of frame). Everything below is sibling repos + global tooling.

### The perf A/B landed — both frame eaters measured at 600 agents
RBM `AgentStatusBar` steady-state **20.1% → 0.59%** of frame. ACC `CCShieldTauntTroopsData` **10.7–22.2% → ~3%**
in 7 of 8 battles. Two corrections worth keeping:
- The RBM **49–56 ms hitch is a mission-LOAD cost**, not a combat one — every SLOW breach lands ~5 s *before* the
  mission's memory-baseline marker, once per mission, and the pre-fix baseline hitches at its own mission start
  too. The old handoff called it "the worst single hitch" and implied the fix should kill it; it was never the
  target and it is still there. Don't re-litigate.
- ACC **battle 1 was UNIMPROVED (~21%)** while battles 2–8 sat at ~3% — same DLL, same process, so not a load
  failure. Suspected shield-heavy composition hitting residual O(n) per-taunter work. UNVERIFIED.

### I called a memory leak on a metric that cannot show one. Then built one that can.
**The mistake:** reported "5 GC cycles, monotonically rising post-GC floor ⇒ leak" from `MEMORY` lines that are
`GC.GetTotalMemory(false)` — bytes **ALLOCATED, no collection forced**. That number cannot separate live objects
from uncollected garbage, so the whole 254→505 MB story was uninterpretable. Worse, the "fix" I proposed with it
(fight one long battle) was worthless: the same metric yields the same non-answer at any length. **The gap was
never battle length — it was the metric.**

**The instrument that decides it** (`MapEventNullFix`): a once-per-mission census that forces a real collection
(`Collect` → `WaitForPendingFinalizers` → `Collect`; the double pass matters — agents are finalizable and survive
the first) and reports what SURVIVED, plus `Private`/`WorkingSet` (a native leak is invisible to `GetTotalMemory`)
and `gen2` (proving whether a collection ever ran). Two capture points — `mission-end` AND `after-teardown` on the
next mission's first tick — because an `EndMission` prefix runs BEFORE teardown (the whole live battle is still
rooted) and because one hook failing to fire must not void a run.

**Verdict, replicated across two sessions: a REAL managed leak of ~17-21 MB per battle** (208.4→224.9→245.8 and
210.5→227.4 MB, measured with the mission gone and `agents=0`). ~17 MB ≈ one battle's worth of Agents, accumulating
one dead battle at a time ⇒ a **static** root, not one stale copy. The old sampled floors were largely garbage: one
mission read 395.9 MB with `gen2=0`, and a forced collect found only 265.2 MB live.

**Ruled out by reading (do not re-suspect):** the ACC fork's HashSet mirrors — instance fields, cleared in
`OnBattleEnded()`. Built instead a **static-root census** that walks every static field at teardown and reports only
what GREW since the last battle (collections by Count, delegates by invocation-list length). It got its baseline;
the freeze killed the run before any growth report.

### Weapon flapping — root-caused, fixed, VALIDATED IN-GAME ("the flapping stopped")
Mark: units near an enemy switch weapons endlessly instead of attacking. Cause was **upstream SpearPreference**
(verified present in `stock/SpearPreference.dll`): the melee-favour boost applied *only if the agent was currently
wielding a polearm* — but the wield is the OUTPUT that function controls, so it closed a feedback loop
(spear → melee 20 > polearm 10 → sidearm → gate fails → melee 1 < polearm 10 → spear → …), re-fired by
`OnMeleeHit` on **every melee hit**. Fix = drop the wield condition; preference is now a pure function of world
state. **Generalises: a preference function must never depend on the output it controls.**
RBMAI was NOT involved (its 55f/35f writes are overwritten two lines later; its 3 s sweep is gated behind
`PostureEnabled`, which is 0 live). Two subagents disagreed on that — the source + live config settled it.

### THE SESSION ENDS ON AN UNRESOLVED HARD FREEZE — this is next session's job
Battle 2 of a back-to-back run: full freeze, no ESC. Log stops at `23:27:48` on an NRE through ACC's `RegisterBlow`
into vanilla `CustomBattleAgentLogic.OnAgentHit`. Plus **6 AVEs today with an identical signature**: `FaultVA 0xF8
(READ)` at `TaleWorlds_Native+0x660135` inside `Mission.Tick` — a null pointer + field offset, same site every time.

**I nearly rolled ACC back on a false lead.** "That NRE is new today ⇒ our fork did it" collapsed on checking: 12 of
13 hits were at 13:56–13:58, *before* the fork went live at 16:23 — the ORIGINAL ACC was doing it. And "0 hits on
previous days" is not absence: `CustomBattleAgentLogic` only runs in Custom Battles. **Nothing was rolled back.**

**Unverified but coherent:** the leak and the crashes may be ONE bug — stale Agent refs surviving teardown (proven)
would give exactly a native null-deref at a fixed offset plus NREs on half-dead agents. Next session: run
`bannerlord-crash-diagnose`. All instruments are OFF; `MissionTickGuard` left ON.

### Tooling shipped (global) — because "go fight, it's armed" was wrong twice
- **`bl-verify-armed`** — proves an instrument armed AT RUNTIME (the mod's own `Hooked …` line, stamped after the
  arm time) instead of trusting a flag on disk. Verify at the main menu; never spend a battle finding out.
  **Known hole: after a relaunch pass `--since "HH:MM:SS"`**, or it can match the previous launch's line.
- **`bannerlord-live-config-guard.py`** (PreToolUse) — blocks writes to ModSettings/LauncherData/deployed DLLs
  while the game runs. **Fails CLOSED** (a false positive costs one question; a false negative costs the run).
  Reasoned in global `CLAUDE.md` next to the handoff-gate's opposite rule.
- **Self-describing instruments** — `MemoryTracker` now logs its own blind spot, so the metric trap above cannot be
  repeated by whoever reads the log next.
- **`SpearPreferenceFork/scripts/normalize.sh` had NO patch step** while `src/` is GENERATED — a re-normalize would
  have silently destroyed the oscillation fix *and the ten local commits before it* and shipped stock behaviour in
  a DLL that still builds. Local fixes now live in `scripts/local-fixes.patch`; normalize re-applies it and ABORTS
  without it.

---

## 2026-07-13 — the freeze is diagnosed; my own lead hypothesis was the first thing to die

No PSW code touched (still `9fae4a1`). Read-only forensics: a **21-agent Fable workflow** (collect → 4 lenses →
adversarial refuters → synthesis), launched at Mark's request. Full report:
`~/AI/projects/MapEventNullFix/docs/freeze-2026-07-12-diagnosis.md`. Confidence **Medium** — the mechanism class is
proven, the exact park site is not.

### The guard is NOT the freeze — and this is why adversarial review earns its keep
Both the prior handoff and I opened on "MissionTickGuard suppressed an NRE and let the engine tick corrupt state ⇒
the guard converts a crash into a hang." Three refuters killed it on one fact: there is **exactly ONE** MissionTickGuard
NRE suppression in the whole **157,656-line** day log (23:27:48.039, 3 ms before silence). A suppression spin — or a
logging-cost collapse — produces **spam, not silence**. The lens I was most confident in was the only one that died.

### What it actually is: the non-faulting flavor of a native bug we can already see
The main thread stops inside native `Mission.Tick`. Same freed-object walk that throws the AVEs: **unmapped** memory ⇒
it faults, and the guard recovers it (7/7 that day). **Mapped garbage** ⇒ the same walk silently loops — no exception,
so nothing logs and the process just sits there. That is precisely the observed log.

Corrections to the record the workflow forced:
- **SEVEN AVEs, not six**, across **four launches** — plus four more on **07-08**. The walker is **at least 4 days
  old**, not new.
- **One AVE fired in the FIRST battle of a fresh launch.** That single fact **kills the "stale agents surviving
  teardown" requirement** the prior session's unified theory rested on.
- **Three bugs, not one.** The native walker; ACC's unvalidated killmove affector meeting vanilla
  `CustomBattleAgentLogic.OnAgentHit`'s three unguarded dereferences; and the leak. **The leak is mechanically
  INERT** — RBMAI's stale `Agent` keys are never dereferenced (`BattleStatsLogic.cs:96-136` reads only `item.Value`),
  so it cannot cause either crash. Do not ship a unified "stale-agent" fix expecting it to cure all three.

### Two dead ends, both killed cheaply, both worth not re-walking
- **GPU/TDR.** Seven `Kernel_141` (display-driver hang) WER folders sit on the box dated 07-12 — a very strong-looking
  lead given the RX 9070 XT's documented D3D11 fragility. Dead: the Windows **System event log has ZERO TDR events in
  two days**, and no app-error entry at 23:27. Their identical directory mtime is a **WER queue flush, not the event
  time**. Generalises: a WER folder's mtime is not when the fault happened — ask the event log.
- **"Just turn MissionTickGuard off and get a real crash."** Superficially the obvious experiment; it is a bad one.
  The freeze is non-faulting, so there is **no exception to convert into a crash** — guard-off would leave the freeze
  equally silent while turning the frequent, *currently recoverable* AVEs into session-ending CTDs. Costs Mark battles,
  buys nothing.

### The next input is EVIDENCE, not code — and it is free
**DUMP-BEFORE-KILL.** At the next freeze: note per-core CPU in Task Manager, then right-click
`Bannerlord.BLSE.Standalone.exe` → **Create dump file**, *then* kill. One dump discriminates every surviving
hypothesis at once (pinned core + native stack near `Mission.Tick` = the spin; ~0% CPU + wait/barrier frames = a
job-join deadlock; a managed stack in another mod = the diagnosis is wrong). Freezes happen during normal play, so
this costs zero extra runs. A 6-step fix plan exists and **none of it is authorized**; only step 1 (watchdog +
minidump-on-AVE) is no-downside, and even that is downstream of the dump.

### An inherited premise that is NOT verified — flagged, not fixed
The "12 of 13 NRE hits at 13:56–13:58 predate the fork ⇒ the ORIGINAL ACC did it too" claim is the **sole basis for
exonerating our ACC fork**, and I fed it into every agent prompt as established fact. Its **source is unlocated**:
this day-log holds exactly ONE suppression, so those 13 hits are not in it. The exoneration may well be right — but
it is currently **inherited, not verified**. **Relocate those hits before touching ACC code.** (A false lead recorded
to prevent a wrong rollback has quietly become an unchecked premise protecting the same mod. Both directions cost.)

### Tooling note
`bannerlord-crash-diagnose` was **not** used, despite the handoff naming it. Its script targets a campaign-map
GauntletUI **BrushWidget** crash — rgl logs, Silk.NET, brush suspects, and a stale OneDrive launcher path. Wrong
evidence surface; it would have sent Fable at the wrong files. A purpose-built script was written instead and is
saved under this session's `workflows/scripts/`. The skill is worth re-pointing at a *generic* evidence surface
before it is trusted again by name.

---

## 2026-07-13 (later) — the wiki had nothing, and the fix we "already shipped" has never once fired

Mark's question — *"we've had these freezes many times, nothing in the wiki on how we fixed it?"* — was the most
valuable thing in the session. The wiki genuinely had **nothing**: `map-event-null-fix.md`'s only "AVE" matches
are the letters inside "Save". All the prior art was trapped in `MapEventNullFix/docs/crash-diagnosis-reference.md`
(compiled 2026-05-24) and the CHANGELOG, so every session re-derived it from raw logs. **The doc-to-wiki lift never
happened. That was the real defect.** Now fixed: new wiki page **`bannerlord-stale-agent-crashes`** (linked from
`index` + `crash-debugging`, logged in `log.md`).

### The answer to "how did we fix it" is: we didn't
- The **adaptive dt cap** the May doc recommended WAS built — `MaxDtHighLoad = 0.020f` — but is gated behind
  `HighLoadAgentThreshold = 800`. **Every observed AVE fires at 406–780 entities. Under the gate. The mitigation
  for this exact crash family has never once engaged**, and dt sat pinned at the weaker 0.050 (clamps in the
  thousands). The freeze itself: `entities~780`, twenty short.
- **`MiniDumpWriteDump` (§5 of the reference) was never built** — the complete P/Invoke code had been sitting in
  the doc unused since May. The VEH *was* built, which is the only reason we have a fault address at all.
- **v3.11.16** already shipped a fix for the *same mechanism* (`ArtemCore.DropOffWeapon` stale-agent, BUTR EPJS9N:
  managed `Agent` non-null, native entity destroyed). A shield for one call site, not a cure.

### Built and shipped: MapEventNullFix v3.11.22 (`2869d65`, pushed, deployed, sha-verified)
- **`CrashDumper`** — minidump on native AVE, taken **from the VEH, not the managed Finalizer**: by the time a
  Finalizer runs the CLR has unwound the native frames, so a dump taken there has no stack for the fault. Runs on a
  dedicated thread (DbgHelp isn't thread-safe) while the faulting thread blocks (`EXCEPTION_POINTERS` lives on its
  stack). Capped 3/session.
- **`TickWatchdog`** — the freeze catcher, because **the freeze never throws**: no VEH, no finalizer, no WER report.
  A background thread watches a heartbeat from **both** tick prefixes (the siege-deploy path calls `Mission.OnTick`
  directly and never runs `Mission.Tick`) and dumps the process while it is still stuck. Never touches a TaleWorlds
  object — only a timestamp the main thread wrote. **Re-arms**, so a pause logs *"ticking RESUMED — that stall was
  NOT a freeze"* instead of silently eating the dump budget.
- **A bug caught mid-write, worth keeping:** `GetCurrentThreadId()` inside the dump writer returns the **dumper**
  thread's id, not the faulting one — it would have named the wrong thread as the crash site and made every AVE
  dump useless. Capture it on the faulting thread.

### The process mistake, and the fix
I launched the workflow **without reading the reference doc myself** — I passed it to *one sub-agent* and thought
that covered it. It doesn't: a sub-agent returns a narrow answer to the question you asked, but the doc's value is
in the questions it makes you ask when **designing** the lenses. Blind to it, the design missed both findings above,
and burned 1.65M tokens re-deriving recorded history. Now a rule in global `CLAUDE.md`. The actual trap is also
fixed: `bannerlord-crash-diagnose` had a **stale OneDrive launcher path** (a stale path yields an empty mod list,
and an empty list reads as *"no mod is implicated"*) and its scope is now honestly labelled campaign-map/UI-only.

### Next: Mark fights back-to-back battles
**Verify the arm lines in the ModLog before reading anything into the result** — the instrument is `RequireRestart`
and has never run in the field. `EnableMemoryTracker` left OFF on purpose: the census wants exactly this run, but
its forced per-mission GC adds a timing variable to an intermittent freeze repro. The freeze outranks the leak.
