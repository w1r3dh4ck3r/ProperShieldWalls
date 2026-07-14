# ProperShieldWalls — AI Handoff Log

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

---

## 2026-07-13 (night, 3rd) — the +230/battle Harmony counter got a root AND a park, in the same session

No PSW code touched (still `9fae4a1`). The only edit is a **comment** in `RBMFork/Source/RBMAI/RBMAI/RBMAiPatcher.cs`.
Nothing built, nothing deployed.

### The item nobody had chased: `HarmonySharedState.originals +230/battle`
The census logged it three sessions ago as "something re-patches Harmony every mission — real bug, not THE bug" and
it sat there. It is **RBMAI, in our own fork**. `RBM.RBMAIPatchLogic` is a `MissionLogic`, and its `EarlyStart()`
calls `RBMAiPatcher.DoPatching()` — which does `UnpatchAll("com.rbmai")` and then re-patches **every type in the
RBMAI assembly**. Once per mission.

**Why that costs anything at all — from the live DLL, not from memory.** Decompiled `0Harmony.dll` **2.4.2**: both
`Patch` and `Unpatch` route through `PatchFunctions.UpdateWrapper`, which calls `MethodCreator.CreateReplacement()`
**unconditionally, with no caching** — a brand-new `DynamicMethod` every time. `HarmonySharedState.UpdatePatchInfo`
then does `originals[replacement.Identifiable()] = original;`, and `originals` is keyed by the **replacement** with
**no `.Remove()` anywhere in the assembly**. Write-only. Every superseded detour is pinned for the life of the
process, two per patched original per battle.

Heap measurement and DLL source converged from opposite directions. That is the bar this project settled on after
the leak hunt, and it is the reason this one is trustworthy in a single session.

### And then it got PARKED, which is the actual point of the session
**It is not a memory leak** — the bytes are the same trivial class as the leak that was closed last session, and
filing it as one would have reopened the exact symptomless hunt that burned six sessions. The only cost that could
be *felt* is IL-emit + JIT of ~200 detours at every battle load. So I asked Mark the one question that would make
it real — *does battle loading stall, and does it get worse the longer you play?* — and he said **loading feels
fine**. No symptom, no hunt. Logged, not fixed.

The `[[no-symptom-no-hunt]]` rule got its first live test and it **stopped work rather than started it**. That is
what it is for. A rule that only ever ratifies what you already wanted to do is not a rule.

### The suspect I refuted before it could cost anything
**`AIKickNBash` looks exactly like the culprit and is not.** It genuinely patches on mission start and `UnpatchAll`s
on mission end (`AIKickNBashMissionBehavior.cs:108`) — a textbook per-mission cycle. But it patches exactly **one**
method (`Agent.OnAIInputSet`). ~2 entries a battle. It cannot be 230. Same failure the Formation suspect nearly
caused: **a mechanism that matches the *shape* is not a root until the arithmetic matches the *magnitude*.** Doing
the multiplication took one grep, and it is written into `SESSION-STATE.md` so nobody re-suspects it.

### The comment that would have rebuilt the wrong belief every session
Our own fork carried: *"Harmony dedups re-applied patches, so re-running per mission is **free**."* True
behaviourally — no patch is ever applied twice — and **false on cost**, which is precisely why it survived: it is
the kind of wrong that reads as right. It has been replaced with the mechanism, the measurement, and an explicit
**"DO NOT fix this without a symptom"** plus the reason (no felt stall; and nobody knows why upstream re-patches
per mission, so hoisting it to `SubModule` is a behaviour change that would cost Mark a battle to validate, bought
with nothing). Same family as the self-dump P/Invoke that sat in a reference doc labelled *"Recommended"*:
**fixing code fixes it once; a doc that teaches the bug rebuilds it every session.**

### Next
Unchanged, and still gated on Mark, not on code: **did the weapon flapping stop — and were the flappers spearmen
toggling spear↔sidearm?** (sword-only troops or archers flapping ⇒ a second cause outside SpearPreferenceFork), and
**do heavy battles feel like slow motion?** (the dt-clamp question — same start-from-the-symptom discipline).

### One open lead, found by accident, deliberately NOT chased tonight
Correcting the wiki dropped me next to a section titled **"NEVER Patch `Agent.OnAIInputSet`"** — it is an
`[MBCallback]` the C++ engine calls with three `ref` params, and *merely installing* a postfix on it folds every
character into a spike (confirmed on PSW v2.0.0). **`AIKickNBash` installs exactly that postfix**, is active in the
load order, and applies it **via reflection** — which is why no grep for `[HarmonyPatch]` in four months ever
surfaced it. I only saw it because I was reading its patcher for an unrelated reason.

**It is a code-shape match, not an observed symptom** — Mark sees no folded characters, so it may simply not
reproduce this way. Written into `SESSION-STATE.md` as an open lead with the caveat attached, because the place it
could matter is the **unresolved 2026-07-12 hard freeze and the native AVEs**: a corrupted native thunk is exactly
the shape that produces a faulting address with no managed stack. The cheap test (disable AIKickNBash for a few
battles, see if the AVEs stop) costs nothing and is not being run tonight. **Do not upgrade this to a cause without
evidence** — that is the mistake this project has made repeatedly, and naming it as unverified is the point.
