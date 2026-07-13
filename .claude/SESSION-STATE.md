# Session State — ProperShieldWalls

## Current Task
**PSW itself is DONE and MERGED** (`9fae4a1`, master, 38/38 tests). No PSW code touched for six sessions; this repo
is now just the handoff home.

**THE MEMORY-LEAK HUNT IS CLOSED — not because it was solved, but because it was MEASURED and is not worth more of
Mark's time.** Read the wiki page **`bannerlord-memory-leak-census`** before reopening anything here.

## ⚠ THE TWO FINDINGS THAT END THIS
1. **NOBODY EVER NAMED THE SYMPTOM.** Asked point-blank (2026-07-13), Mark: *"I really don't know the symptom that
   started all this!"* Six sessions and many played battles went into a leak with **no complaint driving it**.
   A hunt with no symptom has **no magnitude test**, so nothing can refute it and it never terminates.
2. **THE BYTES WERE NEVER THERE.** Measured with PerfView heap dumps:
   **3 retained Missions → 86.5 MB managed heap. 6 retained Missions → 86.9 MB.** Doubling the retained Missions
   cost **0.4 MB**. The husks are near-empty (~22 Agents each), **not the "~26 MB each" this project asserted as
   fact for five sessions.** That figure came from `MemoryTracker`'s forced-collect `GC.GetTotalMemory` and never
   reconciled with the dump's ~87 MB total heap (**3x gap, never explained**).
   **Native plateaus too:** fresh menu **6.64 GB** → 3 battles **8.28 GB** → 6 battles **8.40 GB**. Three more
   battles = **+0.12 GB**. That is a one-time first-mission warm-up (~1.6 GB), then flat — **not a per-battle leak.**
   *(Caveat, stated honestly: those three VMMap readings are from three different launches. Strong, not airtight.)*

**What IS true:** dead `Mission` objects accumulate 1:1 with battles and are never collected. A genuine unbounded
object-retention bug — just not where the gigabytes are.

## Last Action — fixes SHIPPED and VALIDATED as effective; the leak SURVIVES (more roots beneath)
Deployed, sha-verified, committed, pushed. A 6-battle post-fix snapshot proves **all three fixed roots are GONE
from the heap** — the changes do exactly what they were meant to do:
- **RBMFork `ed216a3`** — `Utilities.ClearAllFormationCaches()` (20 Formation-/Agent-keyed statics) called from
  `RBMAIPatchLogic.OnRemoveBehavior()`. The root was `advanceScaleStartStorage`.
- **MapEventNullFix `7f6a3b2` (v3.11.27)** — `_lastMission`/`_mission` → weak; new
  `CustomBattleBannerBearersSpawnLogicLeakFixPatch` nulls vanilla `_missionSpawnLogic` at `Mission.EndMission`.
  That vanilla static is a **correctness** bug too: assigned only-when-null and never reset, so every battle after
  the first read a **stale spawn logic belonging to a dead mission**.

**But 6 Missions were still retained after 6 battles — still 1:1.** Three MORE roots were hiding underneath:

| Remaining root | Chain |
|---|---|
| **`[StrongHandle]`** (dominant, 3 of 5 sampled) | `-> Object[] -> List<Formation> -> Formation -> Team -> Mission` |
| `FormationFilter...CustomFormationItemVM._mixinReverseDictionary` | static dict keyed by a mission-scoped VM |
| `ArtemsCinematicCharges.SprintMixin.<Instance>` | `-> MissionAgentStatusVM -> Mission` |

**A multi-rooted leak HIDES ITS OWN ROOTS** — a spanning tree shows one parent per node, so while RBMAI held every
Mission the others were redundant and invisible. Expect to **peel** a leak, never to one-shot it. (The
pre-registered bar — *"the count must stop tracking the battle count"*, NOT "zero" — is what caught this honestly
instead of letting a partial win be declared.)

## Next Step
**NOTHING. Do not reopen this without a symptom.** If a real one ever appears (OOM, degradation over hours, a
crash), the next target is **`[StrongHandle]`** — a GC handle held by native interop, a different bug class from
the static dictionaries — and the two **mixin statics** (`MissionSharedLibrary`'s framework, shared by RTSCamera /
ACC / FormationFilter, keeps static registries keyed by mission-scoped ViewModels: a framework-wide pattern, not
one mod's bug).

**Turn `EnableMemoryTracker` OFF** — its forced per-mission GC is not free and the hunt is over.

## Key facts (durable — the rest is in the wiki)
- **The LIVE RBM fork is `~/AI/projects/RBMFork`. `RealisticBattleAiPerf` is RETIRED** (its own `SUPERSEDED.md`);
  the game's `Modules/` has no `RBM` dir. A fix deployed there is a **silent no-op**. `RBM_WS_Fork` and
  `SmartRBMpatch` are enabled but ship **no DLLs** (XML only) — no duplicate-assembly conflict.
- **Snapshots kept, re-analyzable offline forever, no game needed:** `C:\Users\w1r3d\Tools\psw_after.gcdump`
  (3 battles, pre-fix) and `psw_after2.gcdump` (6 battles, post-fix), plus `vm_before/after/after2.csv`.
- **PerfView needs Admin** (UAC via `Start-Process -Verb RunAs`); the output path is **POSITIONAL**; it **freezes
  the target** ⇒ snapshot at the **main menu**. The process is **`Bannerlord.BLSE.LauncherEx.exe`**.
- **Reading a gcdump headlessly:** `dotnet-gcdump report` prints ZERO type rows for a PerfView dump — use the
  `GCHeapDump`/`RefGraph`/`SpanningTree` API *inside* `dotnet-gcdump.dll` from a .NET 8 console app. The wiki has
  the recipe (namespaces, the `ForEach`-before-`Parent` gotcha, the referrer idiom).
- **The 07-12 hard freeze is STILL UNRESOLVED and is a SEPARATE bug** — not recurred in ~23 battles. Still **no
  dump**. At the next freeze: note per-core CPU (pinned core = spin, ~0% = deadlock), then Task Manager →
  right-click **`Bannerlord.BLSE.LauncherEx.exe`** → *Create dump file*, **THEN** kill. **Never automate this
  in-process** — a self-dump froze the game once already.
- **Square census** (PSW) — never captured; `DiagnosticLogging` is ON, so a Square battle captures it free.
- **ACC:** camera fix VALIDATED in-game. Slow-mo gate CLOSED (Mark's call).

## Files to touch next
**Nothing is queued.** The leak work is closed. If it reopens:
`RBMFork/Source/RBMAI/RBMAI/Utilities.cs`, `RBMFork/Source/RBM/RBM/RBMAIPatchLogic.cs`.
Re-Read before editing — compaction wipes read-state.
