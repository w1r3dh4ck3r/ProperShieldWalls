# Session State — ProperShieldWalls

## Current Task
**PSW itself is DONE and MERGED** (`9fae4a1`, master, 38/38 tests, 0.149% of frame). No PSW code touched
this session. The job is the **frame eaters** — and both are now FIXED IN CODE but **NOT YET MEASURED**.

## Next Step — Mark runs ONE battle, then we read the numbers
Both fixes are built, deployed, and verified present in the live DLLs. What is missing is the A/B proof.

Use the **`bannerlord-perf-sweep`** skill (it owns enable → battle → evaluate → **turn the flags back off**).
Fight a 300v300 comparable to the baseline run, then compare against these baseline figures:

| Owner | BEFORE | Expect AFTER |
|---|---|---|
| `RBM AgentStatusBar.UnitStatusMissionView.OnMissionTick` | **13.2%** (+56 ms worst hitch) | near-zero IF the bars are hidden; a partial drop if shown (see hypothesis below) |
| `ArtemsCinematicCombat` (`CCShieldTauntTroopsData.OnMissionTick` 17.2% + `CinematicCombatMissionLogic` 4.5%) | **21.8%** | large drop on the taunt half; the 4.5% MissionLogic half is UNTOUCHED |
| ProperShieldWalls | 0.149% | unchanged |

**BLSE will show a one-time unsigned-DLL CAUTION for `ArtemsCinematicCombatFork.dll` (a new, untrusted DLL
name).** Accept it. If it is declined the module loads DISABLED and every number above is a false negative.

**Perf instruments are ARMED for this battle** (set in the live MCM JSON, `MapEventNullFix_v1.json`):
`EnablePerformanceProfiling`, `EnableMissionBehaviorTiming` (this is the one that emits the per-mod owner table),
`EnablePerfScopeLog` (persists the report to disk), `EnableMemoryTracker` (5 s samples) = **true**;
`EnableHarmonyPatchAttribution` deliberately **false** — attribution inflates the ms and that is exactly why the
baseline run was trustworthy. **TURN THESE BACK OFF after reading the results** — leaving them on is itself a
hitch risk. The `bannerlord-perf-sweep` skill owns that teardown.

### UNVERIFIED HYPOTHESIS — do not treat as fact (it sets the expected size of the RBM win)
Mark says he keeps the unit status bars off ("disabled for realism, minimal UI"). But **RBMConfig contains NO
status-bar/health-bar setting at all** (verified — and `UnitStatusMissionView` is added UNCONDITIONALLY at
`SubModule.cs:153`, unlike its gated neighbours). So the setting he disabled does not map to anything found in
RBM. The only in-code path that hides the bars is `UnitStatusVM._keyToggled` (Ctrl+H; initialised `true`, and the
show-branch requires `!_keyToggled` — i.e. **hidden until toggled**), or `_escapeMenuOpened`.
**Most likely** he simply never pressed the toggle. If so the old code was doing ~600 `WorldToScreen` projections
per frame to render nothing, and the fix takes that method to ~zero. If the bars ARE somehow shown, the fix still
helps (far agents no longer project) but the win is partial. **The sweep settles it — don't assert either way.**

## What shipped (two repos, neither is PSW)
- **RBMFork `720bdf0`** — `UnitStatusVM.RefreshAgentStatus` did `Agent.Position` + `MBWindowManager.WorldToScreen`
  (native interop) + a `Distance` property-set **above** the visibility gate, for every agent every tick. Distance
  changes every tick for a moving agent, so the change-guard never saved the `OnPropertyChanged` into Gauntlet.
  Hoisted the cheap predicates above the expensive ones. **Not a throttle** — nothing visible is delayed or skipped;
  it just stops computing screen coords for bars that were about to be hidden. (Matters because RBM Sprint C was
  reverted as a throttle regression.) Deployed: `RBM.dll` sha `4f69c3e1…`, decompile-verified live.
- **ArtemsCinematicCombatFork `be78af9`** (NEW repo, `~/AI/projects/ArtemsCinematicCombatFork`) — see below.

## The ACC find (this is the big one)
`CCShieldTauntTroopsData.OnMissionTick`, **every frame**: `Mission.Current.Agents.ToList()` (a fresh ~600-element
list) + two more `ToList()` copies, then `ShieldTauntLogicAfterStart` on **every agent**, which probed membership
with `List<Agent>.Contains` — an **O(n) linear scan**. Net: **O(agents × shield-bearers) per frame** plus 600+
allocations/frame. That is the 17.2% and very likely a chunk of the GC churn in the open memory thread.

**The `AIShieldTaunt` MCM setting does NOT gate any of it** — that check sits in `StartShieldTauntingEnemy`, which
is called *after* the expensive loop. Turning the setting off buys ~nothing. Do not re-suggest it.

Fix: HashSet membership mirrors (lists stay — `ShieldTauntSoundLogic`/`GetRandomElement` need `IReadOnlyList`),
backwards index prune, direct iteration of `Mission.Agents`, one offhand-weapon read per agent. Behaviour-preserving.

## Key facts (durable)
- **`src/ArtemsCinematicCombat.cs` is GENERATED — never hand-edit it.** `scripts/normalize.sh` = ILSpy 9.1 +
  mechanical artifact transforms + `scripts/perf-fixes.patch`. Edits made only in `src/` are destroyed on the next
  normalize. normalize.sh now ABORTS if the patch is missing (it used to skip it *silently*, which would ship a
  stock-behaviour DLL that looked fine — that bug bit once already this session).
- **The ILSpy `((Base)this).M()` artifact is a RUNTIME StackOverflow, not a compile error.** It emits `callvirt`, so
  inside `M()`'s own override it re-enters itself forever. **A green build proves nothing.** IL-verified on the
  deployed fork DLL: 0 self-recursive `callvirt`, 76 base calls correctly non-virtual.
- Nothing depends on ACC in either direction (SubModule + assembly-string greps), so the Id/DLL rename is safe.
- Upstream `Modules/ArtemsCinematicCombat/` is **left on disk, unselected** — rollback is a one-line LauncherData revert.
- Fresh full backup taken this session (84 mods + Configs → `D:\Backup\Bannerlord BKP`).
- **`gh` auth: FIXED PERMANENTLY.** A dead `ghp_` token exported from `~/.secrets/secrets.txt` shadowed the good
  `gho_` credential in `hosts.yml` — `gh` prefers `$GITHUB_TOKEN` and never falls back, so every API call 401'd
  while git-over-SSH kept working. The export is deleted; clean login shell verified (`gh api user` → `w1r3dh4ck3r`).
  Written up in global `CLAUDE.md`. **Never re-add a `GITHUB_TOKEN` export.**
- ACC fork **pushed**: https://github.com/w1r3dh4ck3r/ArtemsCinematicCombatFork (private).

## Still open (unchanged from last session)
- **Memory long-battle capture** — INCONCLUSIVE, not clean. One 4.3-min battle caught exactly ONE GC cycle; a rising
  floor within a cycle is normal, across cycles it is a leak, and one cycle cannot tell them apart. Needs a LONG
  battle. The ACC fix above removes a major per-frame allocator, so **re-measure memory after it lands.**
- **Square census** — the last PSW claim resting on a decompile argument. `DiagnosticLogging` is ON; the next battle
  with a Square captures it automatically. Look for `Square spacing=0 interval=0.000 eligible=1` + a swap count.
- **RTSCamera.CommandSystem** calls `Formation.get_CalculateHasSignificantNumberOfMounted` **213,684,301×** per battle.
  Cost is UNMEASURABLE the way we measured (billed to the engine, not its owner). 0.26% is NOT an acquittal. Only an
  A/B (disable the mod, same battle) prices it.

## Files to touch next
Not PSW. `~/AI/projects/ArtemsCinematicCombatFork/scripts/{normalize.sh,perf-fixes.patch}` and
`~/AI/projects/RBMFork/Source/RBM/RBM.AgentStatusBar/UnitStatusVM.cs` if the sweep says the fixes underdeliver.
Skill docs for the fork (wiki page, project-docs CLAUDE/ARCHITECTURE/STACK/WORKFLOW) were NOT written — outstanding.

<!-- session-state-sync: last written by session 5a48b925 at 2026-07-12 16:57:21 -0300 -->
