# Shield Rotation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Revive vanilla's dead shield rotation in ShieldWall and Square formations, so shieldless men are pulled out of the front rank (wall) / off the perimeter (square) and replaced by shielded men.

**Architecture:** A `MissionBehavior` sweeps every 0.5 s. For each formation where vanilla bailed (`formation.Interval <= 0f` — true only for ShieldWall and Square, which are defined as spacing 0), it buckets agents by file, partitions each file so shielded men hold the low ranks, and emits swaps via the **public** `IFormationArrangement.SwitchUnitLocations`. **No Harmony patch, no reflection.** The swap-planning core is TaleWorlds-free so xUnit can source-link it.

**Tech Stack:** C# 7.3, .NET Framework 4.7.2 (mod), net8.0 + xUnit (tests), MCM v5 settings, Bannerlord v1.4.7.

## Global Constraints

- **The csproj has NO globbing.** Every new `.cs` file MUST get an explicit `<Compile Include="..." />` entry in `ProperShieldWalls.csproj`, or it silently never compiles. A past commit shipped `AttackRemap.cs` on disk with its `<Compile>` entry uncommitted and the "green build" was against the uncommitted working tree.
- **A build no longer deploys.** Deploy is a separate, deliberate step: `bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll`.
- **This feature adds ZERO Harmony patches.** The main-menu banner must still read `2 patches OK` afterwards. A count of 3 means something was patched by mistake.
- **Files source-linked into the test project must reference NO TaleWorlds types** (`ProperShieldWalls.Tests.csproj` compiles them against net8.0, where those assemblies do not exist).
- **A new MCM setting is not live until its key exists in the live settings JSON.** Adding a property to `Settings.cs` does NOT add the key to an already-written settings file; it silently reads as `false`/`0` in game. Hand-write the keys (Task 5).
- Gate on `formation.Interval > 0f` → `continue`. **Do NOT hard-code the `ArrangementOrderEnum` list** — `Interval` is vanilla's own guard, so it tracks any future spacing change automatically.
- Every per-tick catch block uses `SubModule.LogErrorThrottled(key, message)`, keyed on `"<Name>:<ExceptionType>"` — never `ex.Message`.
- Build command: `~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release`
- Test command: `~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`

---

### Task 1: The TaleWorlds-free swap-planning core

**Files:**
- Create: `ShieldRotation.cs`
- Modify: `ProperShieldWalls.csproj` (add `<Compile>`), `ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj` (source-link it)
- Test: `ProperShieldWalls.Tests/ShieldRotationTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `internal struct ShieldRotation.Swap` with `internal readonly int A; internal readonly int B;` and ctor `Swap(int a, int b)`.
  - `internal static List<Swap> ShieldRotation.PlanFileSwaps(bool[] hasShield)` — `hasShield` is one file, ordered by **rank ascending** (index 0 = rank 0 = front rank / outer ring). Returns the swaps to apply **in order** so that every shielded man ends up at a lower index than every shieldless man. Returns an empty list when the file is already partitioned (idempotent).

- [ ] **Step 1: Write the failing tests**

Create `ProperShieldWalls.Tests/ShieldRotationTests.cs`:

```csharp
using System.Collections.Generic;
using ProperShieldWalls;
using Xunit;

namespace ProperShieldWalls.Tests
{
    public class ShieldRotationTests
    {
        /// <summary>Applies the planned swaps to a copy and returns the resulting layout.</summary>
        private static bool[] Apply(bool[] input)
        {
            var result = (bool[])input.Clone();
            foreach (var swap in ShieldRotation.PlanFileSwaps(input))
            {
                bool tmp = result[swap.A];
                result[swap.A] = result[swap.B];
                result[swap.B] = tmp;
            }
            return result;
        }

        [Fact]
        public void AlreadySorted_EmitsNoSwaps()
        {
            var plan = ShieldRotation.PlanFileSwaps(new[] { true, true, false, false });
            Assert.Empty(plan);
        }

        [Fact]
        public void ShieldlessAtFront_IsSwappedWithShieldedBehind()
        {
            // rank0 shieldless, rank1 shielded -> they must trade places.
            Assert.Equal(new[] { true, false }, Apply(new[] { false, true }));
        }

        [Fact]
        public void FullyReversed_PartitionsCompletely()
        {
            Assert.Equal(new[] { true, true, false, false }, Apply(new[] { false, false, true, true }));
        }

        [Fact]
        public void ShieldedManDeepInFile_ReachesFrontInOnePlan()
        {
            // The whole point: vanilla bubbles one rank per 0.5s tick. We do it in one pass.
            Assert.Equal(new[] { true, false, false, false }, Apply(new[] { false, false, false, true }));
        }

        [Fact]
        public void AllShielded_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new[] { true, true, true }));
        }

        [Fact]
        public void NoneShielded_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new[] { false, false, false }));
        }

        [Fact]
        public void SingleUnit_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new[] { false }));
        }

        [Fact]
        public void Empty_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new bool[0]));
        }

        [Fact]
        public void Null_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(null));
        }

        [Fact]
        public void PlanIsIdempotent_ReplanningAfterApplyEmitsNothing()
        {
            var settled = Apply(new[] { false, true, false, true });
            Assert.Empty(ShieldRotation.PlanFileSwaps(settled));
        }

        [Fact]
        public void EmitsMinimalSwaps_OneSwapPerMisplacedShieldedMan()
        {
            // Two shielded men behind two shieldless -> exactly two swaps, not four.
            var plan = ShieldRotation.PlanFileSwaps(new[] { false, false, true, true });
            Assert.Equal(2, plan.Count);
        }
    }
}
```

- [ ] **Step 2: Source-link the file and run the tests to verify they FAIL**

In `ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`, inside the existing source-link `<ItemGroup>`, add after the `AttackRemap.cs` line:

```xml
    <Compile Include="../ShieldRotation.cs" Link="ShieldRotation.cs" />
```

Run: `~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`
Expected: **build failure** — `ShieldRotation.cs` does not exist yet (`CS2001: Source file not found`).

- [ ] **Step 3: Write the implementation**

Create `ShieldRotation.cs`:

```csharp
using System.Collections.Generic;

namespace ProperShieldWalls
{
    /// <summary>
    /// Plans the slot swaps that put shielded men at the low ranks of a file.
    ///
    /// Vanilla already does this (LineFormation.SwitchFrontUnitTypesToFrontRows) but opens with
    /// `if (Interval &lt;= 0f) return;` — and ArrangementOrder.GetUnitSpacingOf returns 0 for BOTH
    /// ShieldWall and Square, so Interval is exactly 0 and the rotation returns on its first line,
    /// forever. It has never run in either formation.
    ///
    /// One rule covers both, because rank means different things per arrangement:
    ///   ShieldWall (LineFormation)              rank 0 = the front rank  -> shields to the front
    ///   Square (RectilinearSchiltronFormation)  rank 0 = the outer ring  -> shields on the perimeter
    ///                                           (fileIndex picks the side; rank walks inward from it)
    ///
    /// Vanilla also only ever swaps ADJACENT ranks, one pair per 0.5 s tick, so a shieldless man
    /// bubbles rearward over several seconds. This partitions the whole file in a single pass, so a
    /// shieldless front-ranker is replaced on the next sweep rather than four sweeps later.
    ///
    /// Deliberately free of TaleWorlds types so the net8.0 test project can source-link it.
    /// </summary>
    internal static class ShieldRotation
    {
        internal struct Swap
        {
            internal readonly int A;
            internal readonly int B;

            internal Swap(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        /// <summary>
        /// <paramref name="hasShield"/> is one file, ordered by rank ascending (index 0 = rank 0).
        /// Returns the swaps to apply IN ORDER. Empty when the file is already partitioned, so a
        /// settled formation costs nothing and cannot churn.
        /// </summary>
        internal static List<Swap> PlanFileSwaps(bool[] hasShield)
        {
            var swaps = new List<Swap>();
            if (hasShield == null) return swaps;

            // Stable partition by selection: `next` is the lowest rank not yet holding a shielded
            // man. Walk front-to-back; each shielded man found below `next` is swapped up into it.
            // One swap per misplaced shielded man — the minimum possible.
            int next = 0;
            for (int i = 0; i < hasShield.Length; i++)
            {
                if (!hasShield[i]) continue;

                if (i != next)
                {
                    swaps.Add(new Swap(next, i));

                    // Mirror the swap locally so `next` keeps meaning "lowest free slot" for the
                    // rest of the walk. Without this, a file with several gaps plans nonsense.
                    bool tmp = hasShield[next];
                    hasShield[next] = hasShield[i];
                    hasShield[i] = tmp;
                }

                next++;
            }

            return swaps;
        }
    }
}
```

> **NOTE:** `PlanFileSwaps` mutates the array it is given. That is intentional and load-bearing (the mirror above). The caller in Task 3 passes a throwaway array built for the purpose, and the test helper clones before applying. Do not "fix" this by copying internally — the mirror is what makes multi-gap files plan correctly.

- [ ] **Step 4: Run the tests to verify they PASS**

Run: `~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`
Expected: PASS — 37 tests total (27 existing + 10 new).

- [ ] **Step 5: Add the `<Compile>` entry to the MOD csproj**

In `ProperShieldWalls.csproj`, in the `<ItemGroup>` containing the other `<Compile>` entries, add after the `AttackRemap.cs` line:

```xml
    <Compile Include="ShieldRotation.cs" />
```

Run: `~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release`
Expected: Build succeeded.

Verify the type actually made it into the DLL (the csproj-globbing trap):
Run: `strings -a bin/Release/ProperShieldWalls.dll | grep -c ShieldRotation`
Expected: a non-zero count.

- [ ] **Step 6: Commit**

```bash
git add ShieldRotation.cs ProperShieldWalls.csproj ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj ProperShieldWalls.Tests/ShieldRotationTests.cs
git commit -m "feat(rotation): TaleWorlds-free swap planner for shield rotation"
```

---

### Task 2: Settings + diagnostics

**Files:**
- Modify: `Settings.cs`, `Diagnostics.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `Settings.ShieldRotation` (`bool`, default `true`)
  - `Settings.RotationInterval` (`float`, default `0.5f`, range `0.1f`–`2.0f`)
  - `Diagnostics.RecordShieldSwap()`, `Diagnostics.RecordShieldlessFront()`, `Diagnostics.RecordRotationSkippedDetached()`, `Diagnostics.RecordRotationFormation()` — all `internal static void`, no args.

- [ ] **Step 1: Add the two settings**

In `Settings.cs`, after the `FriendlyBlockPassthrough` property:

```csharp
        [SettingPropertyBool("Shield Rotation", Order = 4, RequireRestart = false,
            HintText = "In a Shield Wall or Square, men who lose their shield are pulled back and a shielded man takes their place. Vanilla's own rotation is dead in these two formations (it is gated on unit spacing, which both define as zero).")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool ShieldRotation { get; set; } = true;
```

And after the `CrowdedDuration` property:

```csharp
        [SettingPropertyFloatingInteger("Rotation Interval", 0.1f, 2f, "#0.0",
            Order = 5, RequireRestart = false,
            HintText = "Seconds between shield-rotation sweeps. Vanilla's equivalent runs every 0.5s.")]
        [SettingPropertyGroup("Tuning", GroupOrder = 1)]
        public float RotationInterval { get; set; } = 0.5f;
```

- [ ] **Step 2: Add the counters to Diagnostics**

In `Diagnostics.cs`, after the `_friendlyBlocksNeutralised` field:

```csharp
        // --- Shield rotation ---
        private static int _rotationSwaps;
        private static int _rotationShieldlessFront;
        private static int _rotationFormations;
        private static int _rotationSkippedDetached;
```

In `Reset()`, after `_friendlyBlocksNeutralised = 0;`:

```csharp
            _rotationSwaps = 0;
            _rotationShieldlessFront = 0;
            _rotationFormations = 0;
            _rotationSkippedDetached = 0;
```

After `RecordFriendlyBlockNeutralised()`:

```csharp
        internal static void RecordShieldSwap()
        {
            _rotationSwaps++;
        }

        /// <summary>A man holding rank 0 (front rank / outer ring) with no shield — the thing we exist to fix.</summary>
        internal static void RecordShieldlessFront()
        {
            _rotationShieldlessFront++;
        }

        /// <summary>
        /// Counted separately because it is the feature's most likely silent failure: if melee detaches
        /// men from their formation, every candidate is skipped and the result is indistinguishable from
        /// "the feature never fired". This number tells the two apart.
        /// </summary>
        internal static void RecordRotationSkippedDetached()
        {
            _rotationSkippedDetached++;
        }

        internal static void RecordRotationFormation()
        {
            _rotationFormations++;
        }
```

- [ ] **Step 3: Add the report line + config stamp**

In `WriteMissionReport()`, immediately before `Append("[PSW] ========================");`:

```csharp
            Append(string.Format(CultureInfo.InvariantCulture,
                "[PSW]  shield rotation     : {0} swaps across {1} formation-sweeps ({2} shieldless front-rankers seen, {3} skipped as detached){4}",
                _rotationSwaps, _rotationFormations, _rotationShieldlessFront, _rotationSkippedDetached,
                _rotationSwaps == 0 ? "   <-- FEATURE NEVER FIRED" : ""));
```

In `DescribeConfig()`, change the format string and args to add the two new values:

```csharp
            return string.Format(CultureInfo.InvariantCulture,
                "enabled={0} windup={1} cramped={2} blockPass={3} threshold={4:0.00} crowdedDur={5:0.0} rotate={6} rotInterval={7:0.0}",
                s.Enabled ? 1 : 0,
                s.WindupTransparency ? 1 : 0,
                s.CrampedAttackGating ? 1 : 0,
                s.FriendlyBlockPassthrough ? 1 : 0,
                s.WindupThreshold,
                s.CrowdedDuration,
                s.ShieldRotation ? 1 : 0,
                s.RotationInterval);
```

- [ ] **Step 4: Build**

Run: `~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add Settings.cs Diagnostics.cs
git commit -m "feat(rotation): shield-rotation settings, counters and mission-report line"
```

---

### Task 3: The sweep behaviour

**Files:**
- Create: `Behaviours/ShieldRotationBehavior.cs`
- Modify: `ProperShieldWalls.csproj` (add `<Compile>`), `SubModule.cs` (register the behaviour)

**Interfaces:**
- Consumes: `ShieldRotation.PlanFileSwaps(bool[])` + `ShieldRotation.Swap` (Task 1); `Settings.ShieldRotation`, `Settings.RotationInterval`, and the four `Diagnostics.Record*` methods (Task 2); `SubModule.LogErrorThrottled(string, string)` (existing).
- Produces: `ProperShieldWalls.Behaviours.ShieldRotationBehavior` — an `internal sealed class : MissionBehavior` with a parameterless constructor.

- [ ] **Step 1: Write the behaviour**

Create `Behaviours/ShieldRotationBehavior.cs`:

```csharp
using System;
using System.Collections.Generic;
using MCM.Abstractions.Base.Global;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    /// <summary>
    /// Revives vanilla's shield rotation in the two formations where it is structurally dead.
    ///
    /// LineFormation.SwitchFrontUnitTypesToFrontRows() already pulls shielded men toward rank 0 —
    /// but it opens with `if (Interval &lt;= 0f) return;`, and ArrangementOrder.GetUnitSpacingOf
    /// returns 0 for BOTH ShieldWall and Square. Interval = 0.38f * 0 = 0, so it returns on its
    /// first line every tick, forever, in exactly the two formations built around shields.
    ///
    /// No Harmony patch: every member used here is public. We call the SAME method vanilla's own
    /// loop calls (IFormationArrangement.SwitchUnitLocations), so RBMFork's and FrontlineModFork's
    /// prefixes on it still run on our swaps — both return true for a valid active pair.
    /// </summary>
    internal sealed class ShieldRotationBehavior : MissionBehavior
    {
        private float _sinceLastSweep;

        /// <summary>Reused across files and sweeps: the sweep runs 2x/second, and this would otherwise churn the GC.</summary>
        private readonly List<Agent> _column = new List<Agent>();
        private readonly Dictionary<int, List<Agent>> _files = new Dictionary<int, List<Agent>>();

        public override MissionBehaviorType BehaviorType
        {
            get { return MissionBehaviorType.Other; }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            var settings = GlobalSettings<Settings>.Instance;
            if (settings == null || !settings.Enabled || !settings.ShieldRotation) return;

            _sinceLastSweep += dt;
            if (_sinceLastSweep < settings.RotationInterval) return;
            _sinceLastSweep = 0f;

            try
            {
                Sweep();
            }
            catch (Exception ex)
            {
                // Runs 2x/second for the whole battle: an unthrottled log here is a storm.
                SubModule.LogErrorThrottled(
                    "ShieldRotationBehavior:" + ex.GetType().Name,
                    "[PSW] ShieldRotationBehavior error: " + ex.Message);
            }
        }

        private void Sweep()
        {
            var mission = Mission.Current;
            if (mission == null) return;

            foreach (Team team in mission.Teams)
            {
                if (team == null) continue;

                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation == null) continue;

                    var arrangement = formation.Arrangement;
                    if (arrangement == null || arrangement.UnitCount < 2) continue;

                    // Vanilla's OWN guard, inverted. It bails when Interval <= 0, which today means
                    // exactly ShieldWall and Square. Testing Interval rather than hard-coding the
                    // ArrangementOrderEnum list means we track any future spacing change for free,
                    // and we never touch Line/Circle, where vanilla's rotation already works.
                    if (formation.Interval > 0f) continue;

                    RotateFormation(formation, arrangement);
                }
            }
        }

        private void RotateFormation(Formation formation, IFormationArrangement arrangement)
        {
            _files.Clear();

            foreach (IFormationUnit unit in arrangement.GetAllUnits())
            {
                var agent = unit as Agent;
                if (agent == null || !agent.IsActive()) continue;

                int fileIndex, rankIndex;
                agent.GetFormationFileAndRankInfo(out fileIndex, out rankIndex);

                // LineFormation.SwitchUnitLocations — the overload vanilla's loop uses, and the one
                // we call — has NO detachment guard (only Formation's Agent-typed overload does).
                // An unpositioned unit reports -1 and must not be swapped, so we guard it ourselves.
                if (fileIndex < 0 || rankIndex < 0)
                {
                    Diagnostics.RecordRotationSkippedDetached();
                    continue;
                }

                List<Agent> column;
                if (!_files.TryGetValue(fileIndex, out column))
                {
                    column = new List<Agent>();
                    _files[fileIndex] = column;
                }
                column.Add(agent);
            }

            Diagnostics.RecordRotationFormation();

            foreach (var entry in _files)
                RotateFile(arrangement, entry.Value);
        }

        private void RotateFile(IFormationArrangement arrangement, List<Agent> unordered)
        {
            if (unordered.Count < 2) return;

            _column.Clear();
            _column.AddRange(unordered);
            _column.Sort(CompareByRank);

            if (!_column[0].HasShieldCached)
                Diagnostics.RecordShieldlessFront();

            var hasShield = new bool[_column.Count];
            for (int i = 0; i < _column.Count; i++)
                hasShield[i] = _column[i].HasShieldCached;

            // PlanFileSwaps mutates `hasShield` (it mirrors each swap so it can keep planning).
            // That is fine: the array is built here purely to be consumed.
            List<ShieldRotation.Swap> plan = ShieldRotation.PlanFileSwaps(hasShield);

            foreach (ShieldRotation.Swap swap in plan)
            {
                Agent a = _column[swap.A];
                Agent b = _column[swap.B];

                arrangement.SwitchUnitLocations(a, b);
                Diagnostics.RecordShieldSwap();

                // Mirror the swap in our local view so later swaps in the same plan address the
                // right agents — the plan's indices are slot positions, not agent identities.
                _column[swap.A] = b;
                _column[swap.B] = a;
            }
        }

        private static int CompareByRank(Agent left, Agent right)
        {
            int leftFile, leftRank, rightFile, rightRank;
            left.GetFormationFileAndRankInfo(out leftFile, out leftRank);
            right.GetFormationFileAndRankInfo(out rightFile, out rightRank);
            return leftRank.CompareTo(rightRank);
        }
    }
}
```

- [ ] **Step 2: Register the behaviour**

In `SubModule.cs`, in `OnMissionBehaviorInitialize`, after the existing `AddMissionBehavior` line:

```csharp
            mission.AddMissionBehavior(new ShieldRotationBehavior());
```

- [ ] **Step 3: Add the `<Compile>` entry**

In `ProperShieldWalls.csproj`, after the `Behaviours\AttackGateComponent.cs` line:

```xml
    <Compile Include="Behaviours\ShieldRotationBehavior.cs" />
```

- [ ] **Step 4: Build and verify the patch count did NOT change**

Run: `~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release`
Expected: Build succeeded.

Run: `~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`
Expected: PASS, 37 tests.

This feature adds no Harmony patches. Confirm the DLL still declares exactly 3 patch classes' worth of `[HarmonyPatch]` — i.e. the banner will still say **2 patches OK** (`AttackGatePatches` holds the player/AI gate; the two live patch targets are `MeleeHitCallback` and `GetDefendCollisionResults`).
Run: `strings -a bin/Release/ProperShieldWalls.dll | grep -c HarmonyPatch`
Expected: unchanged from before this task. If it went **up**, something got patched by mistake — stop and investigate.

- [ ] **Step 5: Commit**

```bash
git add Behaviours/ShieldRotationBehavior.cs SubModule.cs ProperShieldWalls.csproj
git commit -m "feat(rotation): sweep behaviour reviving vanilla shield rotation in ShieldWall and Square"
```

---

### Task 4: Deploy

**Files:** none (build artefacts only)

- [ ] **Step 1: Confirm the game is not running, then deploy**

`bl-deploy` refuses while Bannerlord is running (the game locks the DLL) and refuses a dirty worktree (the deployed DLL would then correspond to no commit).

Run: `git status --short`
Expected: clean.

Run: `bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll`
Expected: copies, verifies the destination sha256 matches the source, writes `deployed.json`.

- [ ] **Step 2: Verify what is actually live**

Run: `cat "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/bin/Win64_Shipping_Client/deployed.json"`
Expected: `branch` and `commit` match `git rev-parse --short HEAD`, `"dirty": false`.

Confirm the new code is in the deployed binary. Use `strings -el` as well as `strings -a`: .NET stores string literals in a UTF-16 `#US` heap, so an ASCII-only grep returning nothing proves nothing.
Run: `strings -el "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/bin/Win64_Shipping_Client/ProperShieldWalls.dll" | grep -i "shield rotation"`
Expected: the mission-report line and/or the MCM setting name appear.

---

### Task 5: Make the settings live (the MCM JSON trap)

**Files:**
- Modify: the live MCM settings JSON (path resolved in Step 1)

A new property in `Settings.cs` does **not** add its key to an existing settings file. MCM writes the file once and thereafter reads it. Without this task, `ShieldRotation` reads as `false` in game and the feature is silently dead while looking perfect in source.

- [ ] **Step 1: Locate the live settings file**

Run:
```bash
find "/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/Configs" -iname "*ProperShieldWalls*" 2>/dev/null
```
Expected: a `.json` under `Configs/ModSettings/Global/ProperShieldWalls/`.

- [ ] **Step 2: Read it, then add the two missing keys**

Read the file. It will contain `Enabled`, `WindupTransparency`, `CrampedAttackGating`, `FriendlyBlockPassthrough`, `WindupThreshold`, `CrowdedDuration`, `DiagnosticLogging` — and **not** the two new ones.

Add, matching the file's existing formatting exactly:
```json
  "ShieldRotation": true,
  "RotationInterval": 0.5,
```

- [ ] **Step 3: Verify**

Run: `grep -E "ShieldRotation|RotationInterval" <the json path>`
Expected: both keys present.

The real confirmation comes from the game: the next mission report's `config:` line must read `rotate=1 rotInterval=0.5`. If it reads `rotate=0`, the key did not take.

---

### Task 6: In-game validation (Mark at the keyboard)

**Files:** none

This is the gate. Deployed ≠ working; a silent no-op looks identical to success, which is why the report exists.

- [ ] **Step 1: ShieldWall**

Custom Battle, infantry only, ~30v30. Order your men to **Shield Wall**. Let the front rank take shield damage until shields break (javelins help). Watch whether shieldless men are pulled back and replaced.

- [ ] **Step 2: Square**

Same fight, order **Square**. Shields should end up on the **outer ring**, shieldless men in the interior.

- [ ] **Step 3: Read the report**

Read `/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/PSW_diag.log` — the newest `==== mission report ====` block.

| Line | Meaning |
|---|---|
| `config: ... rotate=1` | the setting took (if `rotate=0`, Task 5 failed) |
| `shield rotation : N swaps ...`, N > 0 | the feature fired |
| `<-- FEATURE NEVER FIRED` **and** `skipped as detached` is large | melee detaches men from the formation — the swap candidates are all being skipped. This is risk #3 in the spec, and it is a real possibility, not a bug in the sweep. |
| `<-- FEATURE NEVER FIRED` **and** `skipped as detached` is 0 and `shieldless front-rankers seen` is 0 | no shield ever actually broke — rerun with more javelins before concluding anything |

- [ ] **Step 4: The visual judgement — only Mark can make it**

**PRIMARY RISK:** at `Interval == 0` men are shoulder-to-shoulder, so two men trading slots must physically walk past each other mid-melee. They may shove, clip, or jitter. This may be exactly *why* TaleWorlds gated the rotation. If it looks bad, the fallback is to restrict swaps to men not currently in contact, or to raise `Rotation Interval`.

Report: did shieldless men get pulled back, and did it *look* right?

---

### Task 7: Gemini review gate (blocking — kickoff mandates it)

**Files:** none

- [ ] **Step 1: Package and send**

Assemble the spec (`docs/superpowers/specs/2026-07-12-shield-rotation-design.md`), this plan, and the full content of every changed file (`ShieldRotation.cs`, `Behaviours/ShieldRotationBehavior.cs`, `Settings.cs`, `Diagnostics.cs`, `SubModule.cs`, both csprojs, `ProperShieldWalls.Tests/ShieldRotationTests.cs`) into the adversarial prompt from the `kickoff` skill, then:

```bash
gemini-review "$(cat /tmp/gemini-review-prompt.txt)"
```

- [ ] **Step 2: Address every finding, re-send, repeat until clear**

The sprint is not done until Gemini reports no blocking issues. Do not self-certify.
