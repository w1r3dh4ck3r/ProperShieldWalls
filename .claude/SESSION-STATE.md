# Session State — ProperShieldWalls

## Current Task
**PSW itself is DONE and MERGED** (`9fae4a1`, master, 38/38 tests, 0.10–0.19% of frame). No PSW code touched for
five sessions. All live work is in sibling repos (`MapEventNullFix`, `ArtemsCinematicCombatFork`).

Open: **(1) the per-battle memory leak** — one root NAMED this session, the main one still unnamed; **(2) the
2026-07-12 hard freeze** — still needs one manual process dump.

## ⚠ METHOD CHANGE (Mark's call, 2026-07-13). READ THIS BEFORE BUILDING ANYTHING.
Mark: *"Are we not chasing our own tail? Many battles and no answer. See if there is a better way — maybe a
debugger."* **He was right, and the critique is about METHOD.** Six sessions of hand-built in-process probes
extracted roughly **one bit of information per battle Mark fought**. A heap profiler returns the whole object graph
— every object, its retained size, and the exact reference chain keeping it alive — in **one snapshot, zero code,
zero extra battles**.

**DO NOT build another in-process probe.** The next move is a profiler, and it is already installed and tested.

### The tools are DOWNLOADED and VERIFIED WORKING, in `C:\Users\w1r3d\Tools\`
- **`PerfView.exe`** (Microsoft, free, portable, no installer, v3.2.4). Its *Goto callers (F10)* view "summarizes
  **paths to the GC roots**, which indicate why the object is still alive" (MS User's Guide) — literally our
  unanswered question. **CLI verified driveable from WSL:**
  ```bash
  cd /mnt/c/Users/w1r3d/Tools && ./PerfView.exe -noGui -AcceptEula HeapSnapshot <PID> out.gcdump
  ```
  The output file is **POSITIONAL** — `-o` is rejected (`Unexpected qualifier`). May need **admin**. It briefly
  **freezes the target**, so snapshot at the **MAIN MENU, never mid-battle**.
- **`vmmap64.exe`** (Sysinternals, free, attaches live, no restart). Splits memory into Heap / Private Data /
  Mapped File / Image and diffs snapshots. `./vmmap64.exe -accepteula -p <PID> "C:\path\out.csv"`.

### ⚠ THE GAME PROCESS IS `Bannerlord.BLSE.LauncherEx.exe`, NOT `...Standalone.exe`
Verified live 2026-07-13: PID 9604, window title `Mount and Blade II Bannerlord - Singleplayer`. **Several docs
(incl. the manual-dump instructions) name `Bannerlord.BLSE.Standalone.exe` — that process does not exist on this
box.** Always match the FAMILY: `tasklist.exe | grep -iE "bannerlord|taleworlds"`.

## Next Step — ONE run with the profiler. This should end the managed hunt.
1. Mark launches, reaches the **main menu**, says so. → take `vmmap` baseline.
2. Mark fights **3 back-to-back battles**.
3. Mark returns to the **main menu** and **LEAVES THE GAME RUNNING** (this is the step that failed last time —
   he closed it, and the snapshot was lost).
4. → take `PerfView HeapSnapshot <PID>` + a second `vmmap`. Then find what roots the dead `Mission`.

A baseline `vmmap` at the main menu already exists at `C:\Users\w1r3d\Tools\vm_before.csv` (2026-07-13 17:22, KB
committed): **Total 6.64 GB · Private Data (native VirtualAlloc) 5.28 GB · native Heap 0.31 GB · Managed Heap
0.26 GB.** It has **no matching "after"** — the game was closed first. Retake both halves in one launch.

## What is ESTABLISHED (measured, replicated — do not re-derive)
- **A whole dead `Mission` is retained every battle.** `MISSION-RETENTION` climbs `1,2,3` in three separate
  launches, measured with `WeakReference`s after a forced 2-pass collect. It is real.
- **Each retained husk weighs ~26 MB**, and that IS the managed leak: `Retained` tracks the retention count 1:1
  (`ret=1 → 209.7 MB`, `ret=2 → 235.5 MB`, `ret=3 → 252.2 MB`). `Mission.FreeResources()` nulls `_allAgents` but
  the Mission still holds **`MissionBehaviors`, which is never cleared** — that is where the weight sits.
- **NAMED ROOT #1 — `TacticalPosition`, a registration bug in the engine's own ledger.** Every `DotNetObject`
  registers itself in the static `DotNetObject.DotnetObjectReferences` dict by a **strong** ref, with
  `ReferenceCount = 0`. Removal happens in **exactly one place** — `DecreaseReferenceCount`, and only when a
  decrement reaches 0. The refcount split proves the mechanism: **`rc=0` grows (864 → 1182) while `rc>0` stays
  frozen at 44.** Nothing ever increments them ⇒ no decrement ever fires ⇒ **they can never be removed.**
  Monotonic across 3 battles (+420, +534, +318). **Its managed bytes are trivial** — its significance is that it
  may hold `GameEntity` wrappers and thus native scene data. **THAT CHAIN IS UNVERIFIED. Do not assert it.**

## ⚠ CORRECTION — I over-claimed a native leak. Do not inherit it as fact.
Two prior handoffs said *"native `Private` grows +150–200 MB/battle, ~6–10× the managed leak — we are chasing the
tip of the iceberg."* **That is NOT supported.** This run: after-teardown `Private` went **7306.4 → 7616.2 →
7563.3 MB** — **up, then DOWN**. `Private` is noisy and includes reserved/uncommitted regions; two earlier runs
happened to rise monotonically and I read a leak into them. **A native leak is UNPROVEN.** The VMMap before/after
diff is the only thing that can settle it — that is why step 3 above matters.

## RULED OUT as the Mission's rooter — do not re-suspect (each killed by an instrument, not an argument)
- **`DotnetObjectReferences`** — the stranded Mission is **NOT in it** (direct membership test, 3 launches).
- **`MissionGauntletSingleplayerOrderUIHandler`** — reports `mission=null`. Vanilla nulls `MissionBehavior.Mission`
  at teardown (`RemoveMissionBehavior`). It pins an empty husk. Its unsubscribe bug is real (`GauntletOrderUIHandler`
  subscribes in `OnMissionScreenActivate`, unsubscribes only in `OnMissionScreenDeactivate`, no finalize override)
  but is worth **kilobytes**. **Do not ship it as a leak fix.**
- **`FormationFilter`'s OoB VMs** — reach a Mission only via `_bannerBearerLogic.Mission`, the same nulled field.
- **`InputKeyItemVM`** (+12/battle) — fields are 2 strings, a `TextObject`, bools. Kilobytes. Refuted by arithmetic.
- **`RBMAI` Formation-keyed statics** — real never-cleared leak, arithmetically far too small.
- **`TacticalPosition`** — `MissionObject.Mission` is a computed `=> Mission.Current`, not a field.

## Still open (named so they are not silently dropped)
- **`HarmonySharedState.originals` +230/battle** ⇒ something re-patches Harmony every mission. Small bytes, genuine
  bug. **Likely US:** `MapEventNullFix.SubModule.OnMissionBehaviorInitialize` calls `TryApplyPatch` every mission by
  design. **UNVERIFIED** — check before blaming another mod.
- **`Agent.Clear()` has still never been read** (two greps hit `DetachmentManager.Clear()` instead). It matters for
  what a retained husk actually weighs.
- **The 07-12 freeze.** Has not recurred in ~14 battles ⇒ intermittent, not load-gated. Still **no dump**. At the
  next freeze: note per-core CPU (a pinned core = spin, ~0% = deadlock), then Task Manager → right-click the
  **`Bannerlord.BLSE.LauncherEx.exe`** process → *Create dump file*, **THEN** kill. **Never automate this
  in-process** — a self-dump is what froze the game once already.
- **ACC slow-motion gate: NOT validated.** Mark confirmed the *camera* fix ("the fix to the killmoves worked");
  he has never confirmed the masterstrike 0.2× drop is gone in free-cam. It must also STILL work in first/third
  person — a pass on one half with a regression in the other is a fail. **Ask him.**
- **Square census** (PSW) — never captured; `DiagnosticLogging` is ON, so a Square battle captures it free.
- **`EnableMemoryTracker` is still ON.** Turn it OFF once the root is named — its forced per-mission GC is not free.

## Files to touch next
**Nothing is queued for edit, deliberately — the next input is a PROFILER SNAPSHOT, not code.** Re-Read
`~/AI/projects/MapEventNullFix/MapEventNullFix/Patches/MemoryTracker.cs` before touching it (compaction wipes
read-state). Live: `MapEventNullFix@872b4c1` (v3.11.26), deployed + sha-verified.

## Key facts (durable)
- **Count trap:** the census reports **ENTRY COUNTS, NOT BYTES.** Rank suspects by what they can HOLD, never by how
  fast they GROW. This project has now nearly shipped **four** wrong "roots" by ranking on growth.
- **Metric trap:** `MEMORY`/`MEMORY(mission)` lines are `GC.GetTotalMemory(false)` = bytes **ALLOCATED**. Only
  `MEMORY(retained)` (forced collect) can prove a leak. And **`Private` is noisy — see the correction above.**
- **`BannerlordAIDebugger` is a CRASH-telemetry gateway** (`AppDomain.UnhandledException` → MCP). It cannot see the
  heap or native memory. **Wrong tool for a leak.**
- **Never send Mark to fight on an unverified instrument:** `bl-verify-armed MapEventNullFix --expect "…"`; after a
  RELAUNCH pass `--since "HH:MM:SS"`.
- **ACC rollback:** original module archived at `D:\Backup\Bannerlord BKP\Removed_ArtemsCinematicCombat_original_20260712`.
