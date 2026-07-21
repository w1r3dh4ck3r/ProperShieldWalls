# Rank-2 Thrust Measurement (Stage 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record what kind of agent is being turned away by ProperShieldWalls' `live-arc` guard — rank, weapon, reach, strike type — so we know whether rear-rank spearmen can be made to thrust past their allies, without changing any gameplay behaviour.

**Architecture:** A new pure-logic file `LiveArcCensus.cs` builds a bucketed census key from primitives and is unit tested. `Diagnostics.cs` owns the counter dictionary and an adapter that reads TaleWorlds types off the `Agent`, delegating key construction to `LiveArcCensus`. `WindupTransparencyPatch` gains exactly one call on its existing `live-arc` reject path. Nothing about any collision outcome changes.

**Tech Stack:** C# 7.3, .NET Framework 4.7.2 (mod) / net8.0 (tests), xUnit 2.9.2, HarmonyLib, MCMv5, Bannerlord v1.4.7.

**Spec:** `docs/superpowers/specs/2026-07-21-rank2-thrust-measurement-design.md`

## Global Constraints

- **`LangVersion` is 7.3.** No switch expressions, no target-typed `new`, no `is not`, no range operators. Use `switch` statements and explicit types.
- **Pure-logic files must not reference TaleWorlds or MCM types.** `ProperShieldWalls.Tests.csproj` source-links them and will fail to compile if they do. This applies to `LiveArcCensus.cs`.
- **`WindupTransparencyPatch.Prefix` must return `true` on every path.** Returning `false` suppresses other mods' prefixes on `Mission.MeleeHitCallback` — RBMCombat, RBMAI, RealisticCombatSounds and XorberaxLegacy all patch it.
- **No new Harmony patch, no new MCM setting, no change to any collision outcome.** The census rides on the existing `DiagnosticLogging` toggle.
- **Do not alter the `live-arc` guard's behaviour.** It still returns `"live-arc"`. This stage only observes.
- **Build never writes into the game folder.** Output goes to `bin/$(Configuration)/`; deploy is a separate `cp`.
- **Build command is `~/.dotnet/dotnet`** — do not probe for a dotnet install.

---

### Task 1: `LiveArcCensus` — pure bucketing and key construction

**Files:**
- Create: `LiveArcCensus.cs`
- Modify: `ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`
- Test: `ProperShieldWalls.Tests/LiveArcCensusTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `internal static class LiveArcCensus` with one entry point:
  `internal static string BuildKey(int rankIndex, string weaponClassName, int weaponLength, int strikeType, string attackDirection)` returning a formatted census key string.
  Also `internal static string RankBucket(int rankIndex)`, `internal static string LengthBucket(int weaponLength)` and `internal static string StrikeLabel(int strikeType)` as separately testable helpers.

  `strikeType` is passed as the raw `int` and mapped **three ways** (`Thrust` / `Swing` / `Invalid`), matching the existing `Describe()` mapping in `WindupTransparencyPatch`. A two-way bool would fold `Invalid` into `Swing`, and "majority Swing" is a decision-rule row in the spec — a mislabel there would change the conclusion.

- [ ] **Step 1: Add the source-link to the test csproj**

In `ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`, inside the first `<ItemGroup>` that holds the other `<Compile Include>` entries, add:

```xml
    <Compile Include="../LiveArcCensus.cs" Link="LiveArcCensus.cs" />
```

- [ ] **Step 2: Write the failing tests**

Create `ProperShieldWalls.Tests/LiveArcCensusTests.cs`:

```csharp
using ProperShieldWalls;
using Xunit;

public class LiveArcCensusTests
{
    [Fact]
    public void RankBucket_Detached_ForNegativeIndex()
    {
        Assert.Equal("detached", LiveArcCensus.RankBucket(-1));
    }

    [Fact]
    public void RankBucket_Detached_ForAnyNegative()
    {
        Assert.Equal("detached", LiveArcCensus.RankBucket(-99));
    }

    [Fact]
    public void RankBucket_ExactForFirstThree()
    {
        Assert.Equal("0", LiveArcCensus.RankBucket(0));
        Assert.Equal("1", LiveArcCensus.RankBucket(1));
        Assert.Equal("2", LiveArcCensus.RankBucket(2));
    }

    [Fact]
    public void RankBucket_CollapsesDeepRanks()
    {
        Assert.Equal("3+", LiveArcCensus.RankBucket(3));
        Assert.Equal("3+", LiveArcCensus.RankBucket(17));
    }

    [Fact]
    public void LengthBucket_Boundaries()
    {
        Assert.Equal("<120", LiveArcCensus.LengthBucket(0));
        Assert.Equal("<120", LiveArcCensus.LengthBucket(119));
        Assert.Equal("120-199", LiveArcCensus.LengthBucket(120));
        Assert.Equal("120-199", LiveArcCensus.LengthBucket(199));
        Assert.Equal("200-279", LiveArcCensus.LengthBucket(200));
        Assert.Equal("200-279", LiveArcCensus.LengthBucket(279));
        Assert.Equal("280+", LiveArcCensus.LengthBucket(280));
        Assert.Equal("280+", LiveArcCensus.LengthBucket(9999));
    }

    [Fact]
    public void LengthBucket_NegativeIsTreatedAsShortest()
    {
        // An absent weapon reports length 0 via the adapter, but guard the negative case
        // so a surprising native value cannot produce an unbucketed key.
        Assert.Equal("<120", LiveArcCensus.LengthBucket(-5));
    }

    [Fact]
    public void StrikeLabel_MapsAllThreeCases()
    {
        // Must be three-way. Folding Invalid into Swing would corrupt the "majority Swing"
        // decision-rule row in the spec.
        Assert.Equal("Swing", LiveArcCensus.StrikeLabel(0));
        Assert.Equal("Thrust", LiveArcCensus.StrikeLabel(1));
        Assert.Equal("Invalid", LiveArcCensus.StrikeLabel(-1));
        Assert.Equal("Invalid", LiveArcCensus.StrikeLabel(7));
    }

    [Fact]
    public void BuildKey_ContainsEveryField()
    {
        string key = LiveArcCensus.BuildKey(2, "OneHandedPolearm", 250, 1, "Up");

        Assert.Contains("rank=2", key);
        Assert.Contains("wpn=OneHandedPolearm", key);
        Assert.Contains("len=200-279", key);
        Assert.Contains("strike=Thrust", key);
        Assert.Contains("dir=Up", key);
    }

    [Fact]
    public void BuildKey_SwingIsLabelledSwing()
    {
        string key = LiveArcCensus.BuildKey(0, "OneHandedSword", 100, 0, "Left");
        Assert.Contains("strike=Swing", key);
    }

    [Fact]
    public void BuildKey_IsStableForIdenticalInput()
    {
        // The key IS the dictionary identity. Two identical events must collapse to one entry.
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        string b = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildKey_DiffersWhenRankDiffers()
    {
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        string b = LiveArcCensus.BuildKey(2, "TwoHandedPolearm", 300, 1, "Up");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BuildKey_DiffersWhenStrikeTypeDiffers()
    {
        string a = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 1, "Up");
        string b = LiveArcCensus.BuildKey(1, "TwoHandedPolearm", 300, 0, "Up");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void BuildKey_NullWeaponClassBecomesUnarmed()
    {
        string key = LiveArcCensus.BuildKey(1, null, 0, 0, "Left");
        Assert.Contains("wpn=unarmed", key);
    }

    [Fact]
    public void BuildKey_EmptyWeaponClassBecomesUnarmed()
    {
        string key = LiveArcCensus.BuildKey(1, "", 0, 0, "Left");
        Assert.Contains("wpn=unarmed", key);
    }

    [Fact]
    public void BuildKey_NullDirectionBecomesUnknown()
    {
        string key = LiveArcCensus.BuildKey(1, "OneHandedSword", 100, 0, null);
        Assert.Contains("dir=?", key);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `cd ~/AI/projects/ProperShieldWalls && ~/.dotnet/dotnet test ProperShieldWalls.Tests -v q`
Expected: FAIL — compile error, `The name 'LiveArcCensus' does not exist in the current context` (or `CS0234`).

- [ ] **Step 4: Write the implementation**

Create `LiveArcCensus.cs` at the repo root:

```csharp
using System.Globalization;

namespace ProperShieldWalls
{
    /// <summary>
    /// Bucketing and key construction for the live-arc census.
    ///
    /// Pure by design: no TaleWorlds and no MCM types, because ProperShieldWalls.Tests source-links
    /// this file and its csproj requires source-linked files to stay game-free. The adapter that
    /// reads Agent/AttackCollisionData lives in Diagnostics, which cannot be unit tested.
    ///
    /// Everything is bucketed rather than recorded raw. The key IS the dictionary identity, so an
    /// unbucketed field (a raw weapon length, a raw rank) would produce a near-unique key per event
    /// and turn an aggregate census back into the per-event log storm it exists to avoid.
    /// </summary>
    internal static class LiveArcCensus
    {
        /// <summary>
        /// Rank 0/1/2 are reported exactly because they are the ones the feature is about; deeper
        /// ranks collapse. A detached unit reports -1 from GetFormationFileAndRankInfo and must NOT
        /// be silently bucketed as rank 0 — that would invent front-rankers that do not exist.
        /// </summary>
        internal static string RankBucket(int rankIndex)
        {
            if (rankIndex < 0) return "detached";
            if (rankIndex == 0) return "0";
            if (rankIndex == 1) return "1";
            if (rankIndex == 2) return "2";
            return "3+";
        }

        /// <summary>
        /// Reach buckets. The boundary that matters is ~200: below it a weapon cannot plausibly
        /// reach the enemy front rank from rank 2 over roughly 1 m of rank spacing.
        /// </summary>
        internal static string LengthBucket(int weaponLength)
        {
            if (weaponLength < 120) return "<120";
            if (weaponLength < 200) return "120-199";
            if (weaponLength < 280) return "200-279";
            return "280+";
        }

        /// <summary>
        /// Mirrors the three-way mapping already used by WindupTransparencyPatch.Describe. It must
        /// stay three-way: folding Invalid into Swing would corrupt the spec's "majority Swing"
        /// decision-rule row, which is one of the four outcomes that decide what Stage 2 becomes.
        /// </summary>
        internal static string StrikeLabel(int strikeType)
        {
            if (strikeType == 1) return "Thrust";
            if (strikeType == 0) return "Swing";
            return "Invalid";
        }

        internal static string BuildKey(
            int rankIndex, string weaponClassName, int weaponLength, int strikeType, string attackDirection)
        {
            string weapon = string.IsNullOrEmpty(weaponClassName) ? "unarmed" : weaponClassName;
            string direction = string.IsNullOrEmpty(attackDirection) ? "?" : attackDirection;

            return string.Format(
                CultureInfo.InvariantCulture,
                "rank={0,-8} wpn={1,-20} len={2,-8} strike={3,-7} dir={4}",
                RankBucket(rankIndex),
                weapon,
                LengthBucket(weaponLength),
                StrikeLabel(strikeType),
                direction);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd ~/AI/projects/ProperShieldWalls && ~/.dotnet/dotnet test ProperShieldWalls.Tests -v q`
Expected: PASS. All prior tests still pass — the suite total should be the previous count plus 15 (this file adds 15 `[Fact]`s).

- [ ] **Step 6: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add LiveArcCensus.cs ProperShieldWalls.Tests/LiveArcCensusTests.cs ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj
git commit -m "feat(diag): pure bucketing + key construction for the live-arc census"
```

---

### Task 2: Wire the census into `Diagnostics`

**Files:**
- Modify: `Diagnostics.cs`

**Interfaces:**
- Consumes: `LiveArcCensus.BuildKey(int, string, int, bool, string)` from Task 1.
- Produces: `internal static void RecordLiveArc(Agent attacker, ref AttackCollisionData cd)` — called by Task 3. Also extends `Reset()` and `WriteMissionReport()`.

- [ ] **Step 1: Add the required usings**

`Diagnostics.cs` currently opens with `System`, `System.Collections.Generic`, `System.Globalization`, `System.IO`, `System.Text`, `MCM.Abstractions.Base.Global`. Add the two game namespaces needed for the adapter:

```csharp
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
```

- [ ] **Step 2: Add the dictionary field**

Next to the other feature-scoped fields, after the `// --- Wind-up transparency ---` block's `_windupRejects`, add a new section:

```csharp
        // --- Live-arc census (Stage 1 measurement: who is the live-arc guard turning away?) ---
        private static readonly Dictionary<string, int> _liveArcCensus = new Dictionary<string, int>();
```

- [ ] **Step 3: Clear it in `Reset()`**

Inside `Reset()`, alongside `_windupRejects.Clear();`, add:

```csharp
            _liveArcCensus.Clear();
```

- [ ] **Step 4: Add the adapter**

Add this method after `RecordWindup`:

```csharp
        /// <summary>
        /// One live-arc rejection. Reads rank and wielded weapon off the attacker here rather than at
        /// the call site, so the [MBCallback] patch stays a one-liner.
        ///
        /// Counted unconditionally, matching RecordWindup: only the report WRITE is gated on the
        /// DiagnosticLogging setting. The path is already filtered to friendly collisions by the
        /// patch's own `enemy` guard, so the volume is bounded (~1071/mission as measured 2026-07-10).
        /// </summary>
        internal static void RecordLiveArc(Agent attacker, ref AttackCollisionData cd)
        {
            if (attacker == null) return;

            int rankIndex = -1;
            string weaponClassName = null;
            int weaponLength = 0;

            try
            {
                // Same idiom (and the same -1 detached contract) as ShieldRotationBehavior.
                // File index is discarded: the census buckets by rank only.
                int fileIndex;
                attacker.GetFormationFileAndRankInfo(out fileIndex, out rankIndex);

                // Same idiom as AttackGatePatches.CanSwing: null when unarmed.
                WeaponComponentData weapon = attacker.WieldedWeapon.CurrentUsageItem;
                if (weapon != null)
                {
                    weaponClassName = weapon.WeaponClass.ToString();
                    // WeaponLength is an int on v1.4.7. If the build reports a type mismatch here,
                    // wrap it: (int)weapon.WeaponLength — do not change the census signature.
                    weaponLength = weapon.WeaponLength;
                }
            }
            catch
            {
                // A diagnostic must never take the game down, and this runs per collision. Record
                // what we managed to read rather than dropping the sample entirely; a key with
                // wpn=unarmed is still a countable event.
            }

            string key = LiveArcCensus.BuildKey(
                rankIndex,
                weaponClassName,
                weaponLength,
                cd.StrikeType,               // raw; LiveArcCensus.StrikeLabel maps it three ways
                cd.AttackDirection.ToString());

            int n;
            _liveArcCensus.TryGetValue(key, out n);
            _liveArcCensus[key] = n + 1;
        }
```

- [ ] **Step 5: Render it in `WriteMissionReport()`**

Immediately before the closing `Append("[PSW] ========================");` line, add:

```csharp
            Append("[PSW]      live-arc census (who the guard turned away):");
            if (_liveArcCensus.Count == 0)
            {
                Append("[PSW]        (no live-arc rejections seen at all)");
            }
            else
            {
                foreach (var kv in _liveArcCensus)
                    Append(string.Format(CultureInfo.InvariantCulture,
                        "[PSW]        {0}  x{1}", kv.Key, kv.Value));
            }
```

- [ ] **Step 6: Build to verify it compiles**

Run: `cd ~/AI/projects/ProperShieldWalls && ~/.dotnet/dotnet build -c Release`
Expected: `Build succeeded.` with 0 errors. Warnings unchanged from before the task.

- [ ] **Step 7: Run the tests**

Run: `cd ~/AI/projects/ProperShieldWalls && ~/.dotnet/dotnet test ProperShieldWalls.Tests -v q`
Expected: PASS, same count as end of Task 1. (`Diagnostics.cs` is not source-linked, so this proves Task 1 still builds — it does not exercise the adapter.)

- [ ] **Step 8: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add Diagnostics.cs
git commit -m "feat(diag): count and report live-arc rejections by rank, weapon, reach and strike"
```

---

### Task 3: Call it from the `live-arc` reject path

**Files:**
- Modify: `Patches/WindupTransparencyPatch.cs`

**Interfaces:**
- Consumes: `Diagnostics.RecordLiveArc(Agent, ref AttackCollisionData)` from Task 2.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Thread the attacker into `Classify`**

`Classify` already takes `attacker`, so no signature change is needed. In `Patches/WindupTransparencyPatch.cs`, replace this (currently at lines 110-111):

```csharp
            // A live strike arc: an ally in front still stops the blade.
            if (!windup) return "live-arc";
```

with:

```csharp
            // A live strike arc: an ally in front still stops the blade.
            //
            // Stage 1 measurement (2026-07-21): before deciding whether to let rank-2 spearmen thrust
            // past their allies, record WHO is being turned away here — rank, weapon, reach, strike
            // type. Observation only; the rejection below is unchanged.
            if (!windup)
            {
                Diagnostics.RecordLiveArc(attacker, ref collisionData);
                return "live-arc";
            }
```

- [ ] **Step 2: Verify nothing else changed**

Run: `cd ~/AI/projects/ProperShieldWalls && git diff Patches/WindupTransparencyPatch.cs`

Expected: exactly one hunk, adding the `Diagnostics.RecordLiveArc` call and the comment. Confirm by inspection that **no `return true` was altered, no `return false` was introduced, and no new `try`/`catch` was added** — the call sits inside the `Prefix`'s existing `try`.

- [ ] **Step 3: Build**

Run: `cd ~/AI/projects/ProperShieldWalls && ~/.dotnet/dotnet build -c Release`
Expected: `Build succeeded.` with 0 errors.

- [ ] **Step 4: Run the tests**

Run: `cd ~/AI/projects/ProperShieldWalls && ~/.dotnet/dotnet test ProperShieldWalls.Tests -v q`
Expected: PASS, same count as Task 2.

- [ ] **Step 5: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add Patches/WindupTransparencyPatch.cs
git commit -m "feat(diag): record every live-arc rejection into the census"
```

---

### Task 4: Deploy and arm the instrument

**Files:**
- Modify: none (deploy + verification only)

**Interfaces:**
- Consumes: the Release build from Task 3.
- Produces: a verified-armed instrument, and the go/no-go for asking Mark to fight.

- [ ] **Step 1: Confirm the game is closed**

Run:

```bash
/mnt/c/Windows/System32/tasklist.exe 2>/dev/null | grep -iE "bannerlord|taleworlds" || echo "not running"
```

Expected: `not running`. **Match the family, never one exe name** — Mark launches through BLSE, so the process is `Bannerlord.BLSE.Standalone.exe` and a grep for `Bannerlord.exe` is a false negative. If anything matches, stop and ask Mark to close the game.

- [ ] **Step 2: Deploy**

Build output stays in the repo, so deploy is a deliberate copy:

```bash
cd ~/AI/projects/ProperShieldWalls
cp bin/Release/ProperShieldWalls.dll \
   "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/bin/Win64_Shipping_Client/ProperShieldWalls.dll"
```

- [ ] **Step 3: Prove the deployed binary is the new one**

```bash
cd ~/AI/projects/ProperShieldWalls
sha256sum bin/Release/ProperShieldWalls.dll \
  "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/bin/Win64_Shipping_Client/ProperShieldWalls.dll"
strings -a "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/bin/Win64_Shipping_Client/ProperShieldWalls.dll" | grep -c RecordLiveArc
```

Expected: the two hashes match, and the `grep -c` returns a non-zero count.
**Use `strings -a`, never `strings -el`** — .NET metadata names live in the UTF-8 `#Strings` heap, so `-el` reports a present symbol as absent.

- [ ] **Step 4: Ask Mark to arm and smoke-test it**

Tell Mark, in these terms:

1. Launch the game.
2. **MCM → Proper Shield Walls → Debug → Diagnostic Logging → ON.** It is `RequireRestart = false`, so no relaunch is needed — but MCM's saved JSON overrides the C# default, and that default is `false`, so this step is mandatory.
3. Fight **any** quick throwaway battle and let it **end normally** (do not quit out — the report is written by `OnEndMission`).

- [ ] **Step 5: Verify the instrument is actually armed BEFORE the real battle**

Read the tail of the log:

```bash
tail -60 "/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/PSW_diag.log"
```

Expected: a `==== mission report ====` block containing the line `live-arc census (who the guard turned away):` followed by either census rows or `(no live-arc rejections seen at all)`.

**If that block is absent, do NOT ask Mark to fight the real battle.** An absent census is indistinguishable from "no rank ≥ 1 rejections exist", which is one of the spec's decision-rule outcomes — the ambiguity would waste the battle. Diagnose first: most likely `DiagnosticLogging` is still off, or the mission was quit rather than ended.

- [ ] **Step 6: Commit nothing; record the state**

No code changes in this task. Update `.claude/SESSION-STATE.md` to say the instrument is deployed and armed, and that the next step is Mark's measurement battle.

---

### Task 5: Run the measurement and apply the decision rule

**Files:**
- Modify: `.claude/SESSION-STATE.md`, `notes.md`

**Interfaces:**
- Consumes: the armed instrument from Task 4.
- Produces: the Stage 2 decision.

- [ ] **Step 1: Ask Mark for the measurement battle**

A **spear-heavy** force in a **packed order** (Shield Wall or Line), fought to a normal end. This is the run the numbers come from.

- [ ] **Step 2: Read the census**

```bash
tail -80 "/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/PSW_diag.log"
```

- [ ] **Step 3: Apply the pre-registered decision rule**

From the spec §5, evaluated **in order** — the first row that matches decides. Percentages are of the mission's total `live-arc` rejects.

| Observation | Conclusion | Stage 2 becomes |
|---|---|---|
| Rank ≥ 1 rejects are **< 5%** | Rear ranks aren't attacking at all | Neither fix; problem is upstream in attack initiation |
| Rank ≥ 1 **polearm Thrusts** are **≥ 20%** | Blocker 2 is not real | The collision fix alone |
| Rank ≥ 1 rejects ≥ 5% but **< 20%** carry length ≥ 200 | Blocker 2 is real | Wielding fix first |
| Rank ≥ 1 polearm rejects are **majority Swing** | Usage-direction problem | Not a collision fix |

If no row matches cleanly, **report "inconclusive" and re-measure.** Do not pick the nearest row.

- [ ] **Step 4: Turn the logging back off**

Ask Mark to set **MCM → Proper Shield Walls → Debug → Diagnostic Logging → OFF**. Leaving it on is itself a cost — this project has already shipped a 28 MB day-log from an unconditional diagnostic.

- [ ] **Step 5: Record the result**

Append a dated entry to `notes.md` with the raw census block, the row the decision rule selected, and what Stage 2 therefore is. Update `.claude/SESSION-STATE.md` `## Next Step`.

- [ ] **Step 6: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add notes.md .claude/SESSION-STATE.md
git commit -m "docs: rank-2 thrust Stage 1 measurement result"
```

---

## Out of scope for this plan

Stage 2 — narrowing the `live-arc` guard so a rank ≥ 1 polearm thrust sets `MeleeCollisionReaction.ContinueChecking` — is **not** in this plan. It is designed only after Task 5's numbers land, and it carries the unresolved question no code can answer: whether the native capsule sweep, once un-frozen, actually reaches the enemy behind. See spec §9 and §10.
