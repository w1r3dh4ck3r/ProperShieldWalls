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
