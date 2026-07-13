# ProperShieldWalls — AI Handoff Log

---


## 2026-07-13 (evening) — the census finally RAN. Leak replicated 3×, narrowed to ~15 roots, root still unnamed.

No PSW code touched (still `9fae4a1`). Work is `MapEventNullFix@858fad7`.

### It worked. 77,612 static fields → ~15 roots that grow EVERY battle.
Armed `EnableMemoryTracker` while the game was closed, Mark fought 4 back-to-back battles in one launch, and the
static-root census produced its **first-ever growth data** (it had only ever had a baseline; the 07-12 freeze
killed its one prior run).

**Leak replicated a THIRD independent time.** `MEMORY(retained)` at `after-teardown` (forced collect, mission gone,
`agents=0`): **262.8 → 282.3 → 298.0 MB = +19.5, +15.7 MB/battle** — squarely in the established 17–21 MB band.

### The trap in the output, and I nearly walked into it
**The census reports ENTRY COUNTS, not BYTES.** The top of its list is not the leak. Its own bookkeeping dict
(`MemoryTracker._prevStaticCounts`, +636) topped the very first report it ever produced — **an instrument that
reports itself as the leak is a defect**, now excluded.

### My lead hypothesis died to arithmetic — from data I had already collected
I leaned hard on **RBMAI's Formation-keyed statics** (`advanceScaleStartStorage` et al). They *are* a real leak:
**verified in source, never cleared** — only `OverrideMovementOrder.positionsStorage` is (`Tactics.cs:192`); the
other five are written per-tick and never touched again, so every dead `Formation` is pinned forever.

**But they cannot be the 17 MB, and the numbers were already in front of me.** A dead agent leaves its formation
(`Agent.Formation = null` on removal, vanilla `Agent.cs:15529` — I verified this, it is the load-bearing fact), so
a retained dead `Formation` roots only its **survivors**. Those two battles ended with **52** and **105** agents,
while `DotNetObject.DotnetObjectReferences` grew **+321** and **+280**. **Formations cannot root ~300 objects when
only 52 were left to root.** Real leak, wrong scale. The advisor caught this; I had the timestamps and the agent
counts and had not multiplied them together.

Also dead: **the prior session's prime suspect (RBMAI's *Agent*-keyed statics) does not appear in the census at
all.** Two sessions carried it forward as "the" suspect. It was never checked against data until now.

### New prime suspect — a different mechanism entirely
Two **static events that gain subscribers every battle and never lose them**:
`Input.OnGamepadActiveStateChanged` **+13/battle**, `HotKeyManager.OnKeybindsChanged` **+12/battle**.
A static event's publisher lives forever and **pins each subscriber's `Target`**. If even one Target is
mission-scoped, it roots that entire dead Mission — one battle's agents ≈ 17 MB. The **+300 magnitude fits "a whole
dead mission retained"** and nothing smaller does. **This is still a suspicion. A count cannot name a culprit.**

### So the instrument got upgraded rather than the fix shipped
For any grown `[event]` root the census now walks the invocation list and logs **each subscriber's `Target` type**
(static handlers are labelled — they pin nothing, so they are excluded by construction). Deployed and
**live-verified** (hashes match; `x{n} subscriber:` literals confirmed in the shipped DLL via `strings -el`).
Next back-to-back run turns `+13 anonymous subscribers` into a mod name and a class name.

> **The rule that held:** *don't ship a leak fix before the census names the root.* Twice now this project has
> over-claimed on this exact bug. The RBMAI-Formation fix is one line and very tempting — and it would have
> "fixed" ~0 MB while we declared victory.

### Separate real bug, logged NOT fixed
`HarmonySharedState.originals` grows **+230/battle** ⇒ **something re-patches Harmony every mission.** Small bytes,
so it is not the leak — but it is a genuine bug and it should not get conflated with one.

### In-game result + what is NOT yet validated (do not launder this)
Mark, live: **"the fix to the killmoves worked"** — the ACC camera no longer snaps into a killmove on his
AI-driven character in RTS free-cam. **Camera half: VALIDATED.**
**The slow-motion half is NOT separately confirmed.** He reported the camera only; a masterstrike in free-cam
without the 0.2× drop has not been observed. It is deployed and IL-verified, nothing more. **Ask him.**

### Doc rot found by the wrap-up sweep — the wiki was RIGHT and the project doc was WRONG for three days
`bannerlord-modding-overview.md` corrected the ILSpy path on **2026-07-10**. `ProperShieldWalls/CLAUDE.md` still
carried the dead `/mnt/c/Users/Mark Lewis/.dotnet/tools/ilspycmd.exe` — and **the project-local doc is the one you
read first**, so the stale layer won and silently no-op'd two decompile attempts today. Worse,
`wiki/build-system.md` was still handing out a *runnable command* using it. Three other pages said "that path does
not exist" without saying where the real one **is** — a negative result that doesn't hand you the positive still
costs the next reader a hunt. All fixed; all now name `~/.dotnet/tools/ilspycmd`.

### Durable knowledge lifted OUT of this journal
New wiki page **`bannerlord-memory-leak-census`** (linked from `index`, logged in `log.md`) now owns the leak: the
two metric traps, what the census is, the ~15 recurring roots, the arithmetic that refuted the Formation suspect,
the static-event suspect, and the standing rule *don't ship a leak fix before the census names the root*. It was
only ever recorded in PSW's notes — the wrong home for a Bannerlord-wide finding.

---

## 2026-07-13 (night) — the census named one root, refuted three more of my own, and Mark called the method

No PSW code touched (still `9fae4a1`). Work is `MapEventNullFix` v3.11.24 → v3.11.26.

### The finding of the session is Mark's, not mine: we were chasing our tail — in METHOD
*"Many battles and no answer. See if there is a better way — maybe a debugger."* He was right. Six sessions of
hand-built in-process probes were extracting **one bit per battle he fought**. A heap profiler hands over the
entire object graph — every object, its retained size, and the reference chain keeping it alive — in **one
snapshot, zero code, zero battles**. I should have reached for one three runs earlier. **PerfView** (Microsoft,
free, portable, no installer) and **VMMap** (Sysinternals) are now downloaded, CLI-tested from WSL, and live in
`C:\Users\w1r3d\Tools\`. `BannerlordAIDebugger` was checked and is the **wrong tool** — it is a crash-telemetry
gateway, blind to the heap.

### What the probes DID settle before being retired
- **A whole dead `Mission` is retained every battle** — `MISSION-RETENTION` climbs 1,2,3 across three launches
  (`WeakReference` + forced 2-pass collect). And **`Retained` tracks that count 1:1 ⇒ each husk is ~26 MB**, which
  IS the managed leak. `FreeResources()` nulls `_allAgents`, but `MissionBehaviors` is **never cleared** — that is
  where the weight lives.
- **NAMED: `TacticalPosition`, a registration bug in the engine's own ledger.** Every `DotNetObject` strong-registers
  itself in static `DotnetObjectReferences` with `ReferenceCount = 0`, and is removed **only** inside
  `DecreaseReferenceCount` when a decrement reaches 0. The refcount split is the proof: **`rc=0` grows 864 → 1182
  while `rc>0` stays frozen at 44.** Nothing increments them ⇒ no decrement ever fires ⇒ **they can never be
  removed.** Monotonic (+420, +534, +318). Its managed bytes are trivial; whether it drags native scene data with
  it is **UNVERIFIED**.

### Four suspects died — every one to an instrument, never to an argument
The order-UI handler (`mission=null` — vanilla nulls `MissionBehavior.Mission` at teardown, so it pins an *empty
husk*), FormationFilter's OoB VMs (same nulled field), `InputKeyItemVM` (2 strings and a `TextObject`), and
**`DotnetObjectReferences` itself** — the direct membership test I built to check my own lead hypothesis came back
*"the stranded Mission is NOT in it"*. **Building the membership test instead of asserting the mechanism is the
only reason none of those shipped as "the fix".** The order-handler unsubscribe was a one-line, genuine, *tempting*
vanilla bug that would have recovered kilobytes while we declared victory.

### And I made the same mistake I kept warning about — on the native side
I asserted, twice, and wrote into the handoff: *"native `Private` grows +150–200 MB/battle, ~6–10× the managed —
we are chasing the tip of the iceberg."* **Refuted by the next run:** `Private` went `7306.4 → 7616.2 → 7563.3` —
up, then **down**. It is noisy; two runs happened to rise and I read a leak into them. **A plausible mechanism plus
two rising samples is not a leak.** Corrected in `SESSION-STATE.md` rather than left to be inherited as fact — a
correction that lives only in chat is a mistake laundered into a durable one.

### Doc bug found live
The game process is **`Bannerlord.BLSE.LauncherEx.exe`** (verified: PID 9604, window title *Mount and Blade II
Bannerlord - Singleplayer*). Multiple docs — including the manual-dump instructions for the freeze — name
`Bannerlord.BLSE.Standalone.exe`, **which does not exist on this box**. Match the family, never one exe name.

### Next
**One profiler run.** Main menu → vmmap baseline → 3 back-to-back battles → back to main menu → **leave the game
RUNNING** (it was closed last time, and the snapshot was lost) → `PerfView HeapSnapshot` + vmmap #2. That answers
"what roots the dead Mission" outright, and prices the native side honestly for the first time.

---

## 2026-07-13 (late) — the leak is ROOTED. One profiler snapshot did what six sessions of probes could not.

No PSW code touched (still `9fae4a1`). Work is in `RBMFork` and `MapEventNullFix` v3.11.27.

### Mark's method call was the whole session
He said it last session — *"are we not chasing our own tail? maybe a debugger"* — and this session proves it.
**One PerfView heap snapshot, taken at the main menu after 3 battles, cost ZERO extra battles and named the roots
outright.** Six sessions of hand-built in-process probes had been extracting roughly one bit per battle he fought.
The whole hunt collapsed in about forty minutes of tool work.

### The root, and why we had already "refuted" it
**`RBMAI.OverrideBehaviorAdvance.advanceScaleStartStorage`** — a `static Dictionary<Formation,float>`, never
cleared. **The edge that carries the weight is `Formation.Team` -> `Team.Mission`.** One retained Formation roots
the *entire* dead Mission, so **2 leftover dict keys per battle cost ~17-26 MB**. Snapshot: 3 retained Missions,
63 Formations = exactly 20 per Mission x 3 battles.

**Last session refuted this exact suspect by arithmetic — and the arithmetic was done on the wrong graph.** It
argued a dead agent leaves its formation, so a retained Formation roots only its *survivors* (52, 105 agents),
"arithmetically incapable" of 17 MB. It never checked `Formation.Team`. A confident, quantitative, *wrong*
refutation nearly buried the answer for good. **Rank a suspect by what it can REACH on the real object graph —
not by what you reason it ought to hold, and not by how fast it grows.**

### The trap that would have shipped a wrong fix AGAIN
`SpanningTree` (PerfView's path-to-root) shows **one parent per node**, and it fingered **`advanceTimerStorage`**.
I wrote that into the handoff and told Mark. It is **wrong** — following the static's *own child edge* with
`RefGraph` shows `advanceTimerStorage` reaches ONE Mission; the sibling `advanceScaleStartStorage` reaches all
three. The source agreed independently: `advanceTimerStorage.Remove(...)` exists at the old repo's `:1693`,
`advanceScaleStartStorage` has **no `.Remove()`/`.Clear()` anywhere**. Heap and source converged from opposite
directions. **Never name a root from a spanning tree alone.**

### Two more roots, one of them ours, one of them vanilla
- **Ours (4th time an instrument was part of the bug):** `MemoryTracker._lastMission` was a **strong** static
  `Mission`. It also **biased its own measurement** — the retention counter uses `WeakReference`s, so the newest
  Mission always *looked* retained because the tracker held it. The same file wraps `_seenMissions` in a
  `WeakReference` with a comment saying the list *"can never be the thing keeping a Mission alive"*, then holds
  the scalar strongly. The list was protected; the scalar leaked.
- **Vanilla:** `CustomBattleBannerBearersModel._missionSpawnLogic` is a private static assigned **only when null**
  and **never reset** (0 hits for `= null` in the whole assembly). It pins battle #1's Mission for the life of the
  process — **and every later battle reads a stale spawn logic belonging to a dead mission.** A correctness bug we
  found while chasing memory.

### The near-miss that would have been the worst outcome
I sent the fix agent to **`RealisticBattleAiPerf`** — a **retired** repo. The game loads **`RBMFork`**; `Modules/`
has no `RBM` directory at all. The agent caught it. Had it not, we would have shipped a build that changed
**nothing**, and "validated" it against a leak that was never touched. **Check which repo is LIVE before fixing it.**

### The fix, and the bar it must clear
`RBMFork`: `Utilities.ClearAllFormationCaches()` (20 Formation-/Agent-keyed statics) from
`RBMAIPatchLogic.OnRemoveBehavior()`. **Inventory finding worth keeping:** many of those caches were being cleared
at mission **START**, not END — which reads as correct in source and is useless, because it leaves every cache full
while sitting at the **main menu between battles**, which is exactly the window a snapshot samples.

**The validation bar is NOT "0 retained Missions".** The two single-slot roots each keep pinning one Mission
regardless, so a *working* fix still leaves ~1-2. **The bar: the retained count must stop tracking the battle
count.** Expecting zero would make a working fix look broken — and that misread was one advisor call away from
being the plan.

### Next
Mark is fighting 5-6 battles now. Next session: snapshot at the main menu (leave the game RUNNING), read the
`Mission` count, and take the same-launch `vmmap` pair — the native side (90 MB managed out of an 8.28 GB process)
is still unpriced, and a native leak remains **unproven**, not disproven.

---

## 2026-07-13 (night, 2nd) — VALIDATION: the fixes work. The leak survives. And the bytes were never there.

No PSW code touched (still `9fae4a1`). Post-fix snapshot: 6 battles, `psw_after2.gcdump`.

### The fixes are good. All three roots are GONE from the heap.
`advanceScaleStartStorage`/`advanceTimerStorage`, `MemoryTracker._lastMission` and vanilla `_missionSpawnLogic` no
longer root anything. RBMFork `ed216a3` and MapEventNullFix `7f6a3b2` do exactly what they were written to do.

### And 6 Missions were still retained after 6 battles. Still 1:1.
Three MORE roots were hiding underneath: **`[StrongHandle]`** (a GC handle — native interop holding a
`List<Formation>`; dominant, 3 of 5 sampled), `FormationFilter...CustomFormationItemVM._mixinReverseDictionary`,
and `ArtemsCinematicCharges.SprintMixin.<Instance>`.

**A multi-rooted leak hides its own roots.** A spanning tree shows ONE parent per node, so while RBMAI held every
Mission the other holders were redundant and *invisible*. Fix the top layer, the next appears. **Expect to peel a
leak, never to one-shot it** — and never promise "this fixes it" from a single root.

The pre-registered success bar (*"the count must stop tracking the battle count"*, NOT "zero") is the only reason
this got called honestly instead of as a partial win. Setting the bar **before** the run is what made it binding.

### The finding that ends the hunt: THE BYTES WERE NEVER THERE
| retained Missions | total managed heap |
|---|---|
| 3 (3 battles) | 86.5 MB |
| 6 (6 battles) | 86.9 MB |

**Doubling the retained Missions cost 0.4 MB.** The husks are near-empty shells (~22 Agents each). This **refutes
the "each husk weighs ~26 MB" claim that this project carried as ESTABLISHED FACT for five sessions** — it came
from `MemoryTracker`'s forced-collect `GC.GetTotalMemory`, and it never reconciled with PerfView's ~87 MB total
heap (a 3x gap the advisor flagged this morning and I did not chase). **Even a forced-collect counter is an
in-process guess; the heap dump is ground truth.**

Native plateaus too: **6.64 GB fresh menu → 8.28 GB after 3 battles → 8.40 GB after 6.** Three more battles cost
**+0.12 GB**. A ~500 MB/battle native leak would have put the 6-battle run near 9.8 GB. It is a one-time
first-mission warm-up (~1.6 GB of scene/asset cache), then flat. *(Caveat: three different launches. Strong, not
airtight.)*

### The real root cause was the QUESTION, not the code
I asked Mark what user-visible symptom started all this. **"I really don't know the symptom that started all
this!"** Six sessions. Many battles he had to fight. Several bespoke in-process instruments. A 21-agent workflow.
For an object leak that costs **hundreds of KB against an 8.4 GB process**.

**A hunt with no symptom has no magnitude test — so nothing can ever refute it, and it cannot terminate.** Every
suspect stays alive, every growing counter looks damning, and each "root" yields a tempting one-line fix that
recovers nothing. That is the whole story of this bug, and it outranks every technique lesson learned along the
way (the metric traps, counts-vs-bytes, get-a-profiler) — all of those were only *needed* because the hunt could
not end. **Write down the symptom before you hunt. If nobody can name one, don't start.**

### Kept anyway (they are real bugs, just not THE bug)
The three fixes stay: unbounded object retention is a genuine defect, and the vanilla `_missionSpawnLogic` one is a
**correctness** bug independent of memory — assigned only-when-null and never reset, so every battle after the
first reads a stale spawn logic belonging to a dead mission.

### Next
**Nothing. The hunt is closed** — do not reopen without a symptom. Turn `EnableMemoryTracker` OFF (its forced
per-mission GC is not free). The **2026-07-12 hard freeze remains unresolved and is a separate bug**; it has not
recurred in ~23 battles and still has no dump.

---

## 2026-07-13 (late night) — the weapon-flapping residual fixed, and a 28 MB/day log storm found in our own crash mod

No PSW code touched (still `9fae4a1`). Work is `SpearPreferenceFork@10f2e06` and `MapEventNullFix@ff9e4ee` (v3.11.28).

### The feature Mark asked for was one I had recommended AGAINST — correctly, and it came due
Last session I fixed the weapon oscillation (a preference function that read its own output) and **deliberately
left a single hard threshold at 2.0 m**, arguing the residual only bites when a *lone* enemy hovers exactly on the
line, and that "adding machinery for a symptom you're not seeing means new per-agent state and a new way to be
wrong." Mark then saw it. That is the system working: the residual was **named, priced, and left open in writing**,
so when the symptom appeared the fix was already designed and took one edit. **Say what you are NOT fixing and why
— then it is a decision, not an omission.**

### The fix is a Schmitt trigger on the DECISION, not on the distance
The naive read is "add hysteresis to the 2.0 m radius". Wrong target. The weapon flips wherever `num > num2`
crosses, and an agent in contact crosses that comparison on **two** knife-edges: the distance line (footwork —
step in to stab, step back to guard) and the **foot-count itself, because men are dying mid-melee**. Latching the
*boolean* covers both; latching the distance covers one. `num2` (cavalry) stays in both comparisons on purpose, so
a charge still pulls the unit back onto its spear immediately — **that is why hysteresis and not a commit-timer**,
which would hold him on a sword while he was ridden down.

Per-agent state lives in a **`ConditionalWeakTable`** (weak keys). A `Dictionary<Agent,_>` on that game-scoped
model would pin `Agent -> Team -> Mission` and leak a Mission per battle — **the exact bug class the last six
sessions were spent closing.** The leak hunt paid for itself here, in a mod that never had the leak.

### A slider that promised something and did nothing
`HoldFireHysteresisGap` was **already in the MCM menu and wired to zero lines of code** — shipped in the DLL,
orphaned when the Hold-Fire sweep machinery was stripped in `e71e2c6`. Mark could drag it and nothing happened.
Renamed `SidearmHysteresisGap` and it now does what its hint text always claimed. **A stripped feature leaves its
settings behind; grep the settings class when you delete machinery.**

### Then Mark asked the question that found the real problem: "any logging still active?"
**94% of a 28 MB single-day log was ONE line.** `SpawnedItemEntityFix: Initialize() fired` — 175,359 of 186,482
lines — logged **unconditionally on the battle hot path** for every legitimate dropped weapon. Not a diagnostic
flag; a **production crash-fix logging its own normal operation**. And `SubModule.Log` does three things per call:
file write, `Debug.Print`, **and a UDP datagram**. Gated behind `EnableMissionTickDiagnostics` in v3.11.28.

**The audit method that worked: don't read the flags, read the LOG FILES.** Every diag flag in MapEventNullFix was
already `false` — the storm came from code no flag governs. `find -mtime -3` on `*.log` found in seconds what
reading the config would never have shown. **A config audit answers "what did we ask for"; the artifacts on disk
answer "what is actually happening."**

### Honest limit on that finding
The **UDP send per call is INFERRED, not observed.** `UdpLogger`'s own init line goes through `LogLocal`, which
only calls `Debug.Print` — **and `Debug.Print` is captured by nothing on this machine**, so the sender's liveness
cannot be confirmed from any log. The file-write and `Debug.Print` costs are certain; the datagram is very likely
(the ctor and `IPAddress.Parse` on a literal address cannot fail) but unproven. Labelled as such in the CHANGELOG
rather than left to be inherited as fact.

### Next: the dt clamp, and why it is probably NOT a bug
`MissionTickGuard` clamped dt **62,000 times in a single launch** today. Before anyone hunts that: **the clamp
fires whenever a frame exceeds the cap, and above 800 agents the cap is `MaxDtHighLoad = 0.020f` — 50 fps.** A
1000-agent battle almost certainly runs below 50 fps, so the guard clamps **nearly every frame by construction.**
The count is expected; it is not evidence of a fault.

**The real question, and it is a good one:** clamping dt below the true frame time makes the simulation advance
less game-time than wall-clock — i.e. **heavy battles may be running in slow motion.** That is a gameplay symptom
Mark can confirm or refute in one battle, and it costs nothing to ask. **Do not start from the counter; start
from whether he can feel it.** (This is the same trap as the leak: a growing number with no named symptom.)

**Awaiting Mark's in-game verdict on the hysteresis**, and the discriminator that decides whether it is complete:
**were the flapping units spearmen toggling spear↔sidearm?** That is all `SpearPreferenceFork` can explain — its
block only runs for polearm carriers. Sword-only troops or archers flapping ⇒ a second cause, outside this mod.
