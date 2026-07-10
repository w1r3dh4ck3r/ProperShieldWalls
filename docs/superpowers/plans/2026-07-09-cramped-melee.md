# ProperShieldWalls v2 — Cramped Melee Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Friendly troops stop eating each other's wind-up swings, and agents boxed in by allies swing overhead instead of horizontally.

**Architecture:** A Harmony prefix on `Mission.MeleeHitCallback` detects a friendly hit landing during the attack wind-up, sets `colReaction = MeleeCollisionReaction.ContinueChecking`, and returns `true` — the original method's own `if (colReaction != ContinueChecking)` guard then skips its entire penalty block (stun, blow, shield damage, particles). That same interception stamps the attacker as "crowded" for a few seconds in a flat `float[]` indexed by `Agent.Index`. Two postfixes (`MissionMainAgentController.ControlTick` for the player, `Agent.OnAIInputSet` for AI) read that stamp and rewrite `Agent.MovementFlags`, replacing a horizontal swing with an overhead. No spatial query is ever performed.

**Tech Stack:** C# 7.3, .NET Framework 4.7.2, legacy (non-SDK) csproj, Harmony 2.x (`0Harmony.dll`), MCMv5, Bannerlord v1.4.6. Build from WSL with `~/.dotnet/dotnet` (v8.0.421). Tests: xUnit on `net8.0`, compiling the pure-logic `.cs` files by source link.

**Spec:** `docs/superpowers/specs/2026-07-09-cramped-melee-design.md` — read it before starting.

## Global Constraints

- **Game version:** Bannerlord **v1.4.6**. Every API in this plan was verified against the shipped DLLs at `/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`.
- **Language level:** `LangVersion` 7.3. No `switch` expressions, no `is not`, no target-typed `new`, no nullable reference types.
- **Module Id stays `ProperShieldWalls`.** Do not rename the Id, folder, DLL, or `AssemblyName`.
- **MCM `FolderName` stays the literal string `"ProperShieldWalls"`.** Changing it orphans the user's saved settings JSON.
- **Never call `Debug.Print` / MCM settings from a static constructor.** MCM's `GlobalSettings<T>.Instance` is null until MCM initializes; every read must null-check it.
- **`Agent.MovementFlags` and `Agent.Formation` are unsynchronized — main thread only.** Do not touch them from `OnTickParallel`.
- **Build command (always this exact form):**
  ```bash
  cd ~/AI/projects/ProperShieldWalls && ~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release -nologo
  ```
  On WSL, `Directory.Build.targets` redirects `OutputPath` to the live game folder — **a successful build deploys the DLL**. Confirm the game is not running first.
- **New `.cs` files MUST get an explicit `<Compile Include="..." />` entry in `ProperShieldWalls.csproj`.** This is a legacy csproj; there is no globbing. A missing entry means the file silently is not compiled.
- **Never `return false` from the `MeleeHitCallback` prefix.** It would suppress other mods' prefixes on the same method (`RealisticCombatSounds`, `XorberaxLegacy` both reference it).

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `CrowdState.cs` | Create | `float[]` by `Agent.Index`; `Stamp` / `IsCrowded` / `Reset`. **Zero TaleWorlds types** (so tests can compile it). |
| `AttackRemap.cs` | Create | Pure `Decide(uint flags, bool canSwing, bool isCrowded) -> uint`. **Zero TaleWorlds types.** |
| `Patches/WindupTransparencyPatch.cs` | Create | Prefix on `Mission.MeleeHitCallback`. |
| `Patches/AttackGatePatches.cs` | Create | Postfix on `MissionMainAgentController.ControlTick` (player) and `Agent.OnAIInputSet` (AI). |
| `Behaviours/CrowdStateBehavior.cs` | Create | `MissionBehavior` that resets `CrowdState` on mission start/end. |
| `Settings.cs` | Rewrite | MCM settings for v2. |
| `SubModule.cs` | Modify | Drop `OthismosBehaviour`, register `CrowdStateBehavior`. |
| `SubModule.xml` | Modify | `v1.1.0` → `v2.0.0`; drop the `StaminaSystem` optional dependency. |
| `ProperShieldWalls.csproj` | Modify | Rewrite `<Compile>` list; add `TaleWorlds.MountAndBlade.View` reference. |
| `Directory.Build.targets` | Modify | Fix the wrong `TaleWorlds.MountAndBlade.View` HintPath. |
| `Directory.Build.props` / `.targets` | Modify | Guard against applying to the net8.0 test project. |
| `ProperShieldWalls.Tests/` | Create | xUnit net8.0 project, source-links the two pure files. |
| `OthismosState.cs`, `StaminaReader.cs`, `Behaviours/{Othismos,Engagement,LockState,SlotEnforcer,StabForcer,PressureResolver}*.cs`, `Models/*.cs`, `Patches/{SlotLock,AgentAI,FriendlyFireCheck,DecideCollisionReaction,RegisterBlow,ShieldDamage}Patch.cs`, `Patches/MeleeHitCallbackPatch.cs` | **Delete** | The unvalidated othismos system. Recoverable at `bd04fd0`. |

---

## Task 1: Strip the othismos system to a green skeleton

The repo currently builds clean at HEAD (verified). This task removes six of the seven patches and everything they depend on, leaving a module that loads and does nothing. Ending state must still build.

**Files:**
- Delete: `OthismosState.cs`, `StaminaReader.cs`
- Delete: `Behaviours/OthismosBehaviour.cs`, `Behaviours/EngagementDetector.cs`, `Behaviours/LockStateManager.cs`, `Behaviours/SlotEnforcer.cs`, `Behaviours/StabForcer.cs`, `Behaviours/PressureResolver.cs`
- Delete: `Models/EngagementPair.cs`, `Models/AgentSlot.cs`
- Delete: `Patches/SlotLockPatch.cs`, `Patches/AgentAIPatch.cs`, `Patches/FriendlyFireCheckPatch.cs`, `Patches/DecideCollisionReactionPatch.cs`, `Patches/RegisterBlowPatch.cs`, `Patches/ShieldDamagePatch.cs`, `Patches/MeleeHitCallbackPatch.cs`
- Delete: the stray tracked path `D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/SubModule.xml` (a WSL build artifact accidentally committed)
- Modify: `SubModule.cs`, `Settings.cs`, `SubModule.xml`, `ProperShieldWalls.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: a `SubModule` class with `Log(string)`; a `Settings` class with `Enabled` and `DiagnosticLogging`. Tasks 4–7 rely on both.

- [ ] **Step 1: Verify the baseline builds before you change anything**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release -nologo 2>&1 | tail -5
```

Expected: `Build succeeded.` with `0 Error(s)`. If it fails, stop — something changed outside this plan.

- [ ] **Step 2: Delete the othismos sources**

```bash
cd ~/AI/projects/ProperShieldWalls
git rm -q OthismosState.cs StaminaReader.cs
git rm -q Behaviours/OthismosBehaviour.cs Behaviours/EngagementDetector.cs Behaviours/LockStateManager.cs \
          Behaviours/SlotEnforcer.cs Behaviours/StabForcer.cs Behaviours/PressureResolver.cs
git rm -q Models/EngagementPair.cs Models/AgentSlot.cs
git rm -q Patches/SlotLockPatch.cs Patches/AgentAIPatch.cs Patches/FriendlyFireCheckPatch.cs \
          Patches/DecideCollisionReactionPatch.cs Patches/RegisterBlowPatch.cs Patches/ShieldDamagePatch.cs \
          Patches/MeleeHitCallbackPatch.cs
git rm -q --cached 'D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/SubModule.xml'
rm -rf './D:'
echo 'D:/' >> .gitignore
```

- [ ] **Step 3: Replace the `<Compile>` list in `ProperShieldWalls.csproj`**

Replace the entire `<ItemGroup>` that begins with `<!-- Entry point -->` (lines 104–130) with:

```xml
  <ItemGroup>
    <!-- Entry point -->
    <Compile Include="SubModule.cs" />
    <Compile Include="Settings.cs" />
    <!-- Pure logic (also compiled by ProperShieldWalls.Tests) -->
    <Compile Include="CrowdState.cs" />
    <Compile Include="AttackRemap.cs" />
    <!-- Behaviours -->
    <Compile Include="Behaviours\CrowdStateBehavior.cs" />
    <!-- Patches -->
    <Compile Include="Patches\WindupTransparencyPatch.cs" />
    <Compile Include="Patches\AttackGatePatches.cs" />
    <!-- Metadata -->
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
```

Those five new files do not exist yet — the project will not build until Task 7. That is expected and is why Step 8 below temporarily comments them out.

- [ ] **Step 4: Add the `TaleWorlds.MountAndBlade.View` reference to `ProperShieldWalls.csproj`**

`MissionMainAgentController` lives in that assembly. Insert into the `<ItemGroup>` of `<Reference>` elements, immediately after the `TaleWorlds.MountAndBlade` reference (line 92):

```xml
    <Reference Include="TaleWorlds.MountAndBlade.View">
      <HintPath>$(GameFolder)\Modules\Native\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.View.dll</HintPath>
      <Private>False</Private>
    </Reference>
```

- [ ] **Step 5: Fix the wrong HintPath in `Directory.Build.targets`**

The existing entry points at a path where the DLL **does not exist** (verified: `ls` returns "No such file or directory"). Replace lines 48–50:

```xml
    <Reference Update="TaleWorlds.MountAndBlade.View">
      <HintPath>$(GameFolder)/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll</HintPath>
    </Reference>
```

with:

```xml
    <Reference Update="TaleWorlds.MountAndBlade.View">
      <HintPath>$(GameFolder)/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll</HintPath>
    </Reference>
```

- [ ] **Step 6: Rewrite `SubModule.cs`**

Drop the `OthismosBehaviour` registration and the `StaminaSystem` coupling. Full new contents:

```csharp
using System;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using ProperShieldWalls.Behaviours;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _harmony = new Harmony("com.propershieldwalls.patch");
            int applied = 0, failed = 0;

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttribute<HarmonyPatch>() == null) continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    applied++;
                    Log($"[PSW] Patched: {type.Name}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"[PSW] FAILED to patch {type.Name}: {ex.Message}");
                }
            }

            Log($"[PSW] Proper Shield Walls v2.0.0 loaded. Patches: {applied} OK, {failed} failed.");
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new CrowdStateBehavior());
        }

        protected override void OnSubModuleUnloaded()
        {
            _harmony?.UnpatchAll("com.propershieldwalls.patch");
            base.OnSubModuleUnloaded();
        }

        internal static void Log(string message)
        {
            Debug.Print(message);
            var settings = GlobalSettings<Settings>.Instance;
            if (settings != null && settings.DiagnosticLogging)
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Cyan));
        }
    }
}
```

Note `OnBeforeInitialModuleScreenSetAsRoot` is gone — the old green "othismos enabled" banner was advertising a feature that no longer exists.

- [ ] **Step 7: Rewrite `Settings.cs`**

`FolderName` must stay the literal `"ProperShieldWalls"` (Global Constraints).

```csharp
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace ProperShieldWalls
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id          => "ProperShieldWalls";
        public override string DisplayName => "Proper Shield Walls";
        public override string FolderName  => "ProperShieldWalls";
        public override string FormatType  => "json";

        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false,
            HintText = "Master switch. Turn off to restore vanilla melee collision entirely.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("Windup Transparency", Order = 1, RequireRestart = false,
            HintText = "A friendly hit during your attack's wind-up costs nothing: no stun, no bounce, no shield clang. The swing continues.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool WindupTransparency { get; set; } = true;

        [SettingPropertyBool("Cramped Attack Gating", Order = 2, RequireRestart = false,
            HintText = "When packed in among friendlies, horizontal swings become overheads. Requires Windup Transparency to be on.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool CrampedAttackGating { get; set; } = true;

        [SettingPropertyFloatingInteger("Windup Threshold", 0f, 0.6f, "#0.00",
            Order = 3, RequireRestart = false,
            HintText = "Attack progress (0-1) below which a friendly hit counts as wind-up. Higher = more of the swing passes through allies.")]
        [SettingPropertyGroup("Tuning", GroupOrder = 1)]
        public float WindupThreshold { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Crowded Duration", 0.5f, 6f, "#0.0",
            Order = 4, RequireRestart = false,
            HintText = "Seconds an agent stays flagged as crowded after its wind-up clips a friendly.")]
        [SettingPropertyGroup("Tuning", GroupOrder = 1)]
        public float CrowdedDuration { get; set; } = 2f;

        [SettingPropertyBool("Diagnostic Logging", Order = 0, RequireRestart = false,
            HintText = "Log every friendly hit: strike type, hit-result flags, attack progress. Use to tune Windup Threshold. Very noisy.")]
        [SettingPropertyGroup("Debug", GroupOrder = 99)]
        public bool DiagnosticLogging { get; set; } = false;
    }
}
```

**Important:** MCM writes its settings JSON on first in-game save, and that JSON then **overrides these C# defaults**. When you change a default during development, delete
`/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/ProperShieldWalls/` or the change will not take effect.

- [ ] **Step 8: Temporarily comment out the not-yet-written Compile entries, then build**

In `ProperShieldWalls.csproj`, wrap these five `<Compile>` lines in an XML comment — `CrowdState.cs`, `AttackRemap.cs`, `Behaviours\CrowdStateBehavior.cs`, `Patches\WindupTransparencyPatch.cs`, `Patches\AttackGatePatches.cs`. Each is uncommented again by the task that creates it (Tasks 2, 3, 6, 4, 5 respectively).

Also in `SubModule.cs`, comment out `using ProperShieldWalls.Behaviours;` and the `mission.AddMissionBehavior(new CrowdStateBehavior());` line inside `OnMissionBehaviorInitialize` (leave `base.OnMissionBehaviorInitialize(mission);`). Task 6 restores both.

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release -nologo 2>&1 | tail -5
```

Expected: `Build succeeded.` `0 Error(s)`.

- [ ] **Step 9: Bump `SubModule.xml` to v2.0.0 and drop the StaminaSystem dependency**

Change `<Version value="v1.1.0"/>` to `<Version value="v2.0.0"/>` and delete the line
`<DependedModule Id="StaminaSystem" Optional="true"/>`.

- [ ] **Step 10: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add -A
git commit -m "refactor!: strip othismos system to a green skeleton

Deletes 6 of 7 Harmony patches plus the behaviours, models, and state they
depended on. The othismos shield-wall shoving system never built after bd04fd0
and was never validated in-game; it remains recoverable in history.

Fixes the TaleWorlds.MountAndBlade.View HintPath in Directory.Build.targets,
which pointed at a path where that DLL does not exist, and untracks the stray
D:/ build artifact.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Test project + `CrowdState`

**Files:**
- Create: `CrowdState.cs`
- Create: `ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`
- Create: `ProperShieldWalls.Tests/CrowdStateTests.cs`
- Modify: `Directory.Build.props`, `Directory.Build.targets` (guard against the test project)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `internal static class ProperShieldWalls.CrowdState`
  - `static void Stamp(int agentIndex, float now, float duration)`
  - `static bool IsCrowded(int agentIndex, float now)`
  - `static void Reset()`

`CrowdState.cs` must reference **no TaleWorlds type** — the test project compiles it directly, and the game DLLs are .NET Framework assemblies that a `net8.0` test host cannot load.

- [ ] **Step 1: Guard `Directory.Build.props` and `Directory.Build.targets` against the test project**

Both files apply to every csproj beneath the repo root, including the net8.0 test project. `FrameworkPathOverride` and the game-DLL `HintPath` rewrites must not touch it. `$(MSBuildProjectName)` is known at import time.

In `Directory.Build.props`, change the opening `<PropertyGroup>` condition (line 9) from:

```xml
  <PropertyGroup Condition="'$(OS)' != 'Windows_NT'">
```

to:

```xml
  <PropertyGroup Condition="'$(OS)' != 'Windows_NT' AND '$(MSBuildProjectName)' != 'ProperShieldWalls.Tests'">
```

In `Directory.Build.targets`, apply the same additional condition to **both** the `<PropertyGroup>` (line 9) and the `<ItemGroup>` (line 14).

- [ ] **Step 2: Write the failing tests**

Create `ProperShieldWalls.Tests/CrowdStateTests.cs`:

```csharp
using ProperShieldWalls;
using Xunit;

public class CrowdStateTests
{
    public CrowdStateTests() => CrowdState.Reset();

    [Fact]
    public void NotCrowded_WhenNeverStamped()
    {
        Assert.False(CrowdState.IsCrowded(7, now: 0f));
    }

    [Fact]
    public void Crowded_WithinDuration()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(7, now: 11.9f));
    }

    [Fact]
    public void NotCrowded_AtExactExpiry()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.False(CrowdState.IsCrowded(7, now: 12f));
    }

    [Fact]
    public void NotCrowded_AfterExpiry()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.False(CrowdState.IsCrowded(7, now: 12.1f));
    }

    [Fact]
    public void StampIsPerAgent()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(7, now: 11f));
        Assert.False(CrowdState.IsCrowded(8, now: 11f));
    }

    [Fact]
    public void GrowsBeyondInitialCapacity()
    {
        CrowdState.Stamp(5000, now: 10f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(5000, now: 11f));
    }

    [Fact]
    public void IsCrowded_BeyondCapacity_IsFalseNotOutOfRange()
    {
        Assert.False(CrowdState.IsCrowded(99999, now: 11f));
    }

    [Fact]
    public void NegativeIndex_IsIgnored()
    {
        CrowdState.Stamp(-1, now: 10f, duration: 2f);
        Assert.False(CrowdState.IsCrowded(-1, now: 11f));
    }

    [Fact]
    public void Reset_ClearsStamps()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        CrowdState.Reset();
        Assert.False(CrowdState.IsCrowded(7, now: 11f));
    }

    [Fact]
    public void Restamp_ExtendsExpiry()
    {
        CrowdState.Stamp(7, now: 10f, duration: 2f);
        CrowdState.Stamp(7, now: 11f, duration: 2f);
        Assert.True(CrowdState.IsCrowded(7, now: 12.5f));
    }
}
```

Create `ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <AssemblyName>ProperShieldWalls.Tests</AssemblyName>
    <RootNamespace>ProperShieldWalls.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Source-link the pure-logic files. They must not reference TaleWorlds types. -->
    <Compile Include="../CrowdState.cs" Link="CrowdState.cs" />
    <Compile Include="../AttackRemap.cs" Link="AttackRemap.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="ProperShieldWalls.Tests" />
  </ItemGroup>
</Project>
```

`AttackRemap.cs` does not exist yet, so the test project cannot build until Task 3. Temporarily comment out its `<Compile Include="../AttackRemap.cs" ... />` line; Task 3 Step 1 restores it.

`CrowdState` and `AttackRemap` are `internal`. Because the test project compiles the sources directly rather than referencing the assembly, `internal` members are visible without `InternalsVisibleTo` — the entry above is belt-and-braces for a future switch to a project reference.

- [ ] **Step 3: Run the tests to verify they fail**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj -v q --nologo 2>&1 | tail -15
```

Expected: build failure. Because the csproj source-links a file that does not exist yet, the
error is `error CS2001: Source file '../CrowdState.cs' could not be found` — not a test failure.
That *is* the red state for this task; the file is created in Step 4.

- [ ] **Step 4: Write the minimal implementation**

Create `CrowdState.cs` at the repo root:

```csharp
using System;

namespace ProperShieldWalls
{
    /// <summary>
    /// Tracks which agents recently had an attack wind-up clip a friendly, keyed by Agent.Index.
    /// Main-thread only. Deliberately free of TaleWorlds types so the test project can compile it.
    /// </summary>
    internal static class CrowdState
    {
        private const int InitialCapacity = 256;

        private static float[] _crowdedUntil = new float[InitialCapacity];

        internal static void Reset()
        {
            _crowdedUntil = new float[InitialCapacity];
        }

        internal static void Stamp(int agentIndex, float now, float duration)
        {
            if (agentIndex < 0) return;
            EnsureCapacity(agentIndex);
            _crowdedUntil[agentIndex] = now + duration;
        }

        internal static bool IsCrowded(int agentIndex, float now)
        {
            if (agentIndex < 0 || agentIndex >= _crowdedUntil.Length) return false;
            return now < _crowdedUntil[agentIndex];
        }

        private static void EnsureCapacity(int index)
        {
            if (index < _crowdedUntil.Length) return;

            int newSize = _crowdedUntil.Length;
            while (newSize <= index) newSize *= 2;

            var grown = new float[newSize];
            Array.Copy(_crowdedUntil, grown, _crowdedUntil.Length);
            _crowdedUntil = grown;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj -v q --nologo 2>&1 | tail -8
```

Expected: `Passed!` with `Passed: 10`.

- [ ] **Step 6: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add CrowdState.cs ProperShieldWalls.Tests Directory.Build.props Directory.Build.targets
git commit -m "feat: add CrowdState with xUnit coverage

Flat float[] keyed by Agent.Index, grown on demand. No TaleWorlds types, so
the net8.0 test project source-links it directly.

Guards Directory.Build.props/.targets against applying net472 overrides to the
test project.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: `AttackRemap` pure function

**Files:**
- Create: `AttackRemap.cs`
- Create: `ProperShieldWalls.Tests/AttackRemapTests.cs`
- Modify: `ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj` (uncomment the `AttackRemap.cs` link)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `internal static class ProperShieldWalls.AttackRemap`
  - `internal const uint AttackLeft = 0x40u, AttackRight = 0x80u, AttackUp = 0x100u, AttackDown = 0x200u, AttackMask = 0x3C0u`
  - `internal static uint Decide(uint flags, bool canSwing, bool isCrowded)`

The constants mirror `Agent.MovementControlFlag` (verified in the 1.4.6 decompile). They are re-declared as raw `uint` so this file stays free of TaleWorlds types; Task 5 casts at the boundary.

- [ ] **Step 1: Restore the `AttackRemap.cs` link in the test csproj**

Uncomment `<Compile Include="../AttackRemap.cs" Link="AttackRemap.cs" />`.

- [ ] **Step 2: Write the failing tests**

Create `ProperShieldWalls.Tests/AttackRemapTests.cs`:

```csharp
using ProperShieldWalls;
using Xunit;

public class AttackRemapTests
{
    private const uint MoveForward = 0x1u;   // an unrelated bit, must be preserved

    [Fact]
    public void NoOp_WhenNotCrowded()
    {
        uint flags = AttackRemap.AttackLeft | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: false));
    }

    [Fact]
    public void NoOp_WhenWeaponCannotSwing()
    {
        uint flags = AttackRemap.AttackLeft | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: false, isCrowded: true));
    }

    [Fact]
    public void NoOp_WhenNotAttacking()
    {
        uint flags = MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: true));
    }

    [Fact]
    public void NoOp_WhenAlreadyOverhead()
    {
        uint flags = AttackRemap.AttackUp | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: true));
    }

    [Fact]
    public void NoOp_WhenThrusting()
    {
        uint flags = AttackRemap.AttackDown | MoveForward;
        Assert.Equal(flags, AttackRemap.Decide(flags, canSwing: true, isCrowded: true));
    }

    [Fact]
    public void RemapsLeftSwingToOverhead()
    {
        uint result = AttackRemap.Decide(AttackRemap.AttackLeft, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp, result);
    }

    [Fact]
    public void RemapsRightSwingToOverhead()
    {
        uint result = AttackRemap.Decide(AttackRemap.AttackRight, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp, result);
    }

    [Fact]
    public void PreservesNonAttackBits()
    {
        uint result = AttackRemap.Decide(AttackRemap.AttackRight | MoveForward, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp | MoveForward, result);
    }

    [Fact]
    public void ClearsAllOtherAttackBits()
    {
        // Left+Down set simultaneously must collapse to Up alone.
        uint result = AttackRemap.Decide(AttackRemap.AttackLeft | AttackRemap.AttackDown, canSwing: true, isCrowded: true);
        Assert.Equal(AttackRemap.AttackUp, result);
    }

    [Fact]
    public void ConstantsMatchMovementControlFlag()
    {
        // Verified against Agent.MovementControlFlag in the v1.4.6 decompile.
        Assert.Equal(0x40u,  AttackRemap.AttackLeft);
        Assert.Equal(0x80u,  AttackRemap.AttackRight);
        Assert.Equal(0x100u, AttackRemap.AttackUp);
        Assert.Equal(0x200u, AttackRemap.AttackDown);
        Assert.Equal(0x3C0u, AttackRemap.AttackMask);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj -v q --nologo 2>&1 | tail -15
```

Expected: `error CS2001: Source file '../AttackRemap.cs' could not be found`.

- [ ] **Step 4: Write the minimal implementation**

Create `AttackRemap.cs` at the repo root:

```csharp
namespace ProperShieldWalls
{
    /// <summary>
    /// Decides whether a crowded agent's horizontal swing becomes an overhead.
    /// Values mirror Agent.MovementControlFlag (v1.4.6). Kept free of TaleWorlds
    /// types so the test project can compile it; callers cast at the boundary.
    /// </summary>
    internal static class AttackRemap
    {
        internal const uint AttackLeft  = 0x40u;
        internal const uint AttackRight = 0x80u;
        internal const uint AttackUp    = 0x100u;
        internal const uint AttackDown  = 0x200u;
        internal const uint AttackMask  = 0x3C0u;

        internal static uint Decide(uint flags, bool canSwing, bool isCrowded)
        {
            if (!isCrowded) return flags;
            if (!canSwing) return flags;

            // Only a horizontal swing is remapped. Overhead and thrust are already legal
            // in a press, and a weapon that cannot swing must keep whatever it was doing.
            if ((flags & (AttackLeft | AttackRight)) == 0) return flags;

            return (flags & ~AttackMask) | AttackUp;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj -v q --nologo 2>&1 | tail -8
```

Expected: `Passed!` with `Passed: 20`.

- [ ] **Step 6: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add AttackRemap.cs ProperShieldWalls.Tests
git commit -m "feat: add AttackRemap pure decision function with xUnit coverage

Decide(flags, canSwing, isCrowded) collapses a horizontal swing to an overhead,
preserving non-attack bits. Constants mirror Agent.MovementControlFlag.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: `WindupTransparencyPatch`

The core of the mod. A friendly hit landing during the wind-up sets `colReaction = ContinueChecking` and **returns `true`** — `Mission.MeleeHitCallback` then skips its own penalty block via its existing `if (colReaction != MeleeCollisionReaction.ContinueChecking)` guard.

**Files:**
- Create: `Patches/WindupTransparencyPatch.cs`
- Modify: `ProperShieldWalls.csproj` (uncomment its `<Compile>` entry)

**Interfaces:**
- Consumes: `CrowdState.Stamp(int, float, float)` (Task 2); `Settings.Enabled`, `Settings.WindupTransparency`, `Settings.WindupThreshold`, `Settings.CrowdedDuration`, `Settings.DiagnosticLogging` (Task 1); `SubModule.Log(string)` (Task 1).
- Produces: nothing consumed by later tasks.

Verified 1.4.6 signature — Harmony matches prefix parameters **by name**, so a subset is legal:

```csharp
internal void MeleeHitCallback(ref AttackCollisionData collisionData, Agent attacker, Agent victim,
    GameEntity realHitEntity, ref float inOutMomentumRemaining, ref MeleeCollisionReaction colReaction,
    CrushThroughState crushThroughState, Vec3 blowDir, Vec3 swingDir,
    ref HitParticleResultData hitParticleResultData, bool crushedThroughWithoutAgentCollision)
```

- [ ] **Step 1: Uncomment the `<Compile>` entry**

In `ProperShieldWalls.csproj`, uncomment `<Compile Include="Patches\WindupTransparencyPatch.cs" />`.

- [ ] **Step 2: Write the implementation**

There is no unit test for this file — it is Harmony glue over native-populated structs that cannot be constructed in a test host. Its logic gates are one-liners; its correctness is established by the in-game validation in Task 7. Create `Patches/WindupTransparencyPatch.cs`:

```csharp
using System;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// A friendly hit landing during an attack's wind-up costs nothing: no friendly-fire stun,
    /// no Bounced weapon reaction, no shield clang, no blow. The sweep continues past the ally.
    ///
    /// Mechanism (verified, Mission.cs:5297-5397, v1.4.6): MeleeHitCallback wraps its entire
    /// penalty block in `if (colReaction != MeleeCollisionReaction.ContinueChecking)`. Vanilla
    /// itself uses this to let kicks and bashes (IsAlternativeAttack) pass through friendlies.
    /// Setting ContinueChecking and returning true makes the original skip that block for us.
    ///
    /// DO NOT return false. That would suppress other mods' prefixes on this same method —
    /// RealisticCombatSounds and XorberaxLegacy both reference it.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class WindupTransparencyPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            ref MeleeCollisionReaction colReaction)
        {
            try
            {
                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled || !settings.WindupTransparency) return true;

                if (attacker == null || victim == null) return true;   // world hit
                if (!collisionData.IsColliderAgent) return true;
                if (ReferenceEquals(attacker, victim)) return true;    // self-hit
                if (!victim.IsHuman) return true;                      // mounts keep vanilla behaviour

                // Team is a free managed field; IsFriendOf is a native call. Short-circuit on the
                // common case before paying for the interop.
                if (attacker.Team != victim.Team && !attacker.IsFriendOf(victim)) return true;

                bool windup =
                    (collisionData.CollisionHitResultFlags & CombatHitResultFlags.HitWithStartOfTheAnimation) != 0
                    || collisionData.AttackProgress < settings.WindupThreshold;

                if (settings.DiagnosticLogging)
                {
                    SubModule.Log(string.Format(
                        "[PSW] friendly hit strike={0} flags={1} progress={2:0.000} windup={3}",
                        collisionData.StrikeType, collisionData.CollisionHitResultFlags,
                        collisionData.AttackProgress, windup));
                }

                if (!windup) return true;   // live strike arc: an ally in front still stops the blade

                colReaction = MeleeCollisionReaction.ContinueChecking;
                CrowdState.Stamp(attacker.Index, Mission.Current.CurrentTime, settings.CrowdedDuration);
                return true;
            }
            catch (Exception ex)
            {
                SubModule.Log("[PSW] WindupTransparencyPatch error: " + ex.Message);
                return true;
            }
        }
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release -nologo 2>&1 | tail -5
```

Expected: `Build succeeded.` `0 Error(s)`.

If you get `CS0117: 'CombatHitResultFlags' does not contain a definition for 'HitWithStartOfTheAnimation'`, you are building against the wrong game version. The enum is `NormalHit=0, HitWithStartOfTheAnimation=1, HitWithArm=2, HitWithBackOfTheWeapon=4` in v1.4.6.

- [ ] **Step 4: Verify the patch target actually resolves**

A `[HarmonyPatch]` naming a method that does not exist throws at patch time, and `SubModule.OnSubModuleLoad` catches it and logs `FAILED to patch`. Confirm the method exists in the shipped DLL before trusting the build:

```bash
~/.dotnet/tools/ilspycmd -t TaleWorlds.MountAndBlade.Mission \
  "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" \
  2>/dev/null | grep -c "void MeleeHitCallback"
```

Expected: `1`.

- [ ] **Step 5: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add Patches/WindupTransparencyPatch.cs ProperShieldWalls.csproj
git commit -m "feat: windup transparency — friendly wind-up hits pass through

Prefix on Mission.MeleeHitCallback sets colReaction = ContinueChecking and
returns true, letting the original skip its own penalty block (stun, blow,
shield damage, particles) via the guard vanilla already uses for kicks/bashes.

Stamps the attacker as crowded, which is the input to the attack gate.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: `AttackGatePatches` — player and AI

**Files:**
- Create: `Patches/AttackGatePatches.cs`
- Modify: `ProperShieldWalls.csproj` (uncomment its `<Compile>` entry)

**Interfaces:**
- Consumes: `AttackRemap.Decide(uint, bool, bool)` (Task 3); `CrowdState.IsCrowded(int, float)` (Task 2); `Settings` (Task 1).
- Produces: nothing consumed by later tasks.

**Harmony priority is load-bearing here, not decorative.** Harmony sorts same-kind patches by priority descending — higher runs first.

| Target | Other patcher | Our priority | Why |
|---|---|---|---|
| `Agent.OnAIInputSet` | `AIKickNBashFork` postfix clears `AttackMask\|DefendMask`, sets `Kick` (0x8000) | `Priority.High` (runs **first**) | theirs runs after and overwrites, so a kick decision always beats our remap |
| `MissionMainAgentController.ControlTick` | `FluidCombatNextNext` postfix ORs its own direction | `Priority.Low` (runs **last**) | we need the final write to `MovementFlags` to veto a wide swing |

`Agent.OnAIInputSet` is `internal`, so `[HarmonyPatch(typeof(Agent), "OnAIInputSet")]` will not bind it. Use a `TargetMethod()`.

- [ ] **Step 1: Uncomment the `<Compile>` entry**

In `ProperShieldWalls.csproj`, uncomment `<Compile Include="Patches\AttackGatePatches.cs" />`.

- [ ] **Step 2: Write the implementation**

Create `Patches/AttackGatePatches.cs`:

```csharp
using System;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace ProperShieldWalls.Patches
{
    internal static class AttackGate
    {
        /// <summary>
        /// If the agent is currently flagged as crowded and is swinging horizontally with a
        /// weapon that can swing, rewrite the swing into an overhead. Main thread only.
        /// </summary>
        internal static void Apply(Agent agent)
        {
            try
            {
                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled || !settings.CrampedAttackGating) return;

                if (agent == null || !agent.IsActive()) return;

                // A kick in flight (AIKickNBashFork) always wins — do not fight it.
                if ((agent.EventControlFlags & Agent.EventControlFlag.Kick) != 0) return;

                var mission = Mission.Current;
                if (mission == null) return;

                if (!CrowdState.IsCrowded(agent.Index, mission.CurrentTime)) return;

                uint flags = (uint)agent.MovementFlags;
                uint next = AttackRemap.Decide(flags, CanSwing(agent), isCrowded: true);
                if (next != flags) agent.MovementFlags = (Agent.MovementControlFlag)next;
            }
            catch (Exception ex)
            {
                SubModule.Log("[PSW] AttackGate error: " + ex.Message);
            }
        }

        /// <summary>
        /// A thrust-only weapon (pike) must never be remapped to an overhead — that would be
        /// the dead input the design explicitly rejects. All reads here are managed, no interop.
        /// </summary>
        internal static bool CanSwing(Agent agent)
        {
            WeaponComponentData weapon = agent.WieldedWeapon.CurrentUsageItem;   // null when unarmed
            if (weapon == null) return false;
            if (!weapon.IsMeleeWeapon) return false;
            return weapon.SwingDamageType != DamageTypes.Invalid && weapon.SwingSpeed > 0;
        }
    }

    /// <summary>
    /// AI agents. Runs FIRST (high priority) so AIKickNBashFork's postfix, which runs after,
    /// can overwrite our remap with its kick.
    /// </summary>
    [HarmonyPatch]
    internal static class AiAttackGatePatch
    {
        // Agent.OnAIInputSet is internal; the typeof/string attribute form will not bind it.
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Agent), "OnAIInputSet");
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        public static void Postfix(Agent __instance)
        {
            AttackGate.Apply(__instance);
        }
    }

    /// <summary>
    /// The player. Runs LAST (low priority) so we get the final write to MovementFlags,
    /// after FluidCombatNextNext's postfix has OR'd in its own direction.
    /// </summary>
    [HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
    internal static class PlayerAttackGatePatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        public static void Postfix()
        {
            var mission = Mission.Current;
            if (mission == null) return;
            AttackGate.Apply(mission.MainAgent);
        }
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release -nologo 2>&1 | tail -5
```

Expected: `Build succeeded.` `0 Error(s)`.

If you get `CS0234: The type or namespace name 'View' does not exist`, the `TaleWorlds.MountAndBlade.View` reference from Task 1 Step 4 or the HintPath fix from Task 1 Step 5 was not applied. That DLL lives at `Modules/Native/bin/Win64_Shipping_Client/`, **not** in the game's `bin/`.

- [ ] **Step 4: Verify both patch targets resolve in the shipped DLLs**

`AccessTools.Method` returns `null` for a missing method, and Harmony then throws at patch time. Confirm both exist:

```bash
G="/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord"
~/.dotnet/tools/ilspycmd -t TaleWorlds.MountAndBlade.Agent "$G/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" 2>/dev/null | grep -c "void OnAIInputSet"
~/.dotnet/tools/ilspycmd -t TaleWorlds.MountAndBlade.View.MissionViews.MissionMainAgentController "$G/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll" 2>/dev/null | grep -c "private void ControlTick"
```

Expected: `1` then `1`.

- [ ] **Step 5: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add Patches/AttackGatePatches.cs ProperShieldWalls.csproj
git commit -m "feat: cramped attack gating for player and AI

Postfixes MissionMainAgentController.ControlTick (player, low priority so we
write MovementFlags last, after FluidCombatNextNext) and Agent.OnAIInputSet
(AI, high priority so AIKickNBashFork's kick overwrites our remap).

Guards against remapping a thrust-only weapon, which would produce a dead input.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: `CrowdStateBehavior` — per-mission state lifecycle

`CrowdState` is static. Without a reset, stamps leak from one battle into the next, and `Agent.Index` values from a dead mission alias live agents in the new one.

**Files:**
- Create: `Behaviours/CrowdStateBehavior.cs`
- Modify: `ProperShieldWalls.csproj` (uncomment its `<Compile>` entry), `SubModule.cs` (restore the registration)

**Interfaces:**
- Consumes: `CrowdState.Reset()` (Task 2).
- Produces: `ProperShieldWalls.Behaviours.CrowdStateBehavior`, registered by `SubModule.OnMissionBehaviorInitialize`.

Verified on `MissionBehavior` (v1.4.6): `public abstract MissionBehaviorType BehaviorType { get; }`, `public virtual void OnBehaviorInitialize()`, `protected virtual void OnEndMission()`.

- [ ] **Step 1: Uncomment the `<Compile>` entry and restore the registration**

In `ProperShieldWalls.csproj`, uncomment `<Compile Include="Behaviours\CrowdStateBehavior.cs" />`.

In `SubModule.cs`, uncomment `using ProperShieldWalls.Behaviours;` and restore the body of `OnMissionBehaviorInitialize` so it reads:

```csharp
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new CrowdStateBehavior());
        }
```

- [ ] **Step 2: Write the implementation**

Create `Behaviours/CrowdStateBehavior.cs`:

```csharp
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    /// <summary>
    /// Owns the lifetime of CrowdState's static buffer. Agent.Index is recycled between
    /// missions, so a stale stamp would alias a fresh agent in the next battle.
    /// </summary>
    internal sealed class CrowdStateBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType
        {
            get { return MissionBehaviorType.Other; }
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            CrowdState.Reset();
        }

        protected override void OnEndMission()
        {
            CrowdState.Reset();
            base.OnEndMission();
        }
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release -nologo 2>&1 | tail -5
```

Expected: `Build succeeded.` `0 Error(s)`.

- [ ] **Step 4: Re-run the unit tests to confirm nothing regressed**

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet test ProperShieldWalls.Tests/ProperShieldWalls.Tests.csproj -v q --nologo 2>&1 | tail -8
```

Expected: `Passed!` with `Passed: 20`.

- [ ] **Step 5: Commit**

```bash
cd ~/AI/projects/ProperShieldWalls
git add Behaviours/CrowdStateBehavior.cs SubModule.cs ProperShieldWalls.csproj
git commit -m "feat: reset CrowdState per mission

Agent.Index is recycled between missions; a leaked stamp would alias a fresh
agent in the next battle.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: Deploy, verify the deploy, and validate in-game

Code analysis is not evidence. Per the project's standing rule, **deployed ≠ complete** — a DLL mod is done when it is observed working in a live battle.

**Files:**
- Modify: `notes.md` (handoff entry)

**Interfaces:**
- Consumes: everything above.
- Produces: a validated, installed module.

- [ ] **Step 1: Back up before touching the live game**

Invoke the `bannerlord-backup` skill. This is mandatory before any change to `LauncherData.xml` or the Modules folder.

- [ ] **Step 2: Confirm the game is not running, then build/deploy**

On WSL, `Directory.Build.targets` redirects `OutputPath` into the live game folder, so the build *is* the deploy.

```bash
cd ~/AI/projects/ProperShieldWalls
~/.dotnet/dotnet build ProperShieldWalls.csproj -c Release -nologo 2>&1 | tail -5
ls -l "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/bin/Win64_Shipping_Client/ProperShieldWalls.dll"
ls -l "/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/SubModule.xml"
```

Expected: `Build succeeded.`, and both files present with a timestamp from the last minute.

- [ ] **Step 3: Verify the deployed DLL actually contains the new patches**

Do not trust timestamps — check the metal. The deployed assembly must contain the new type names and must **not** contain the deleted ones.

```bash
D="/mnt/d/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/ProperShieldWalls/bin/Win64_Shipping_Client/ProperShieldWalls.dll"
for sym in WindupTransparencyPatch AiAttackGatePatch PlayerAttackGatePatch CrowdState AttackRemap; do
  printf '%-24s ' "$sym"; strings "$D" | grep -qx "$sym" && echo PRESENT || echo MISSING
done
for sym in OthismosState SlotLockPatch DecideCollisionReactionPatch; do
  printf '%-30s ' "$sym"; strings "$D" | grep -qx "$sym" && echo "STILL PRESENT (BAD)" || echo "gone (good)"
done
```

Expected: all five `PRESENT`; all three `gone (good)`.

Then run the `bannerlord-mod-verify-deploy` skill for the full deploy-chain check.

- [ ] **Step 4: Add the module to the live load order**

Invoke the `bannerlord-add-mod` skill. It must edit the **live** Windows file — `/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/Configs/LauncherData.xml` — never the repo copy. Place `ProperShieldWalls` after `RBMFork` (currently slot 27). Position relative to `FluidCombatNextNext` (69) and `AIKickNBashFork` (73) does not matter: the conflicts are resolved by `[HarmonyPriority]`, not load order.

BLSE appends new modules at the bottom with `IsSelected=false`; make sure it ends up enabled.

- [ ] **Step 5: Resolve the three open questions from the spec (§6)**

These are the assumptions the design deliberately refused to make. Each must be answered from observation, not reasoning.

1. **Is `AttackUp` the overhead?** Launch a custom battle. Turn on `Diagnostic Logging` in MCM. Stand in a friendly crowd and swing horizontally. If the resulting animation is a thrust rather than an overhead, swap `AttackUp` for `AttackDown` in `AttackRemap.Decide` and in `AttackRemapTests.RemapsLeftSwingToOverhead` / `RemapsRightSwingToOverhead`.

2. **Does the `CanSwing` guard correctly identify a pike?** With `BetterPikes` (slot 66) active, spawn a pike-armed unit and confirm it is never remapped — its thrust must be untouched. If a pike *is* being remapped, `SwingDamageType != DamageTypes.Invalid || SwingSpeed > 0` is true for it, and the guard needs a `WeaponClass` check instead.

3. **What is the right `WindupThreshold`?** With `Diagnostic Logging` on, fight one 200-a-side infantry battle. Collect the `[PSW] friendly hit strike=… flags=… progress=…` lines from the log. Determine whether `HitWithStartOfTheAnimation` ever appears on swings (`strike=0`) or only on thrusts (`strike=1`). If only on thrusts, the `AttackProgress` fallback carries all swings, and its threshold should be set to the observed knee of the progress distribution rather than the placeholder `0.25`.

Bannerlord logs land under `/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/logs/`.

- [ ] **Step 6: Run the in-game validation checklist**

Custom battle, two infantry lines, shield wall arrangement, 200 per side.

1. A swing from the second rank reaches an enemy instead of stopping on the front rank.
2. No friendly-fire stun when a wind-up clips an ally (you keep swinging; no stagger).
3. Horizontal swings become overheads in the press, and revert to normal once you break free.
4. A pike-armed unit still thrusts and is never remapped (open question 2).
5. `AIKickNBashFork` kicks still fire — the AI priority resolution works.
6. `RealisticCombatSounds` and `XorberaxLegacy` still behave; the `return true` prefix shape exists to keep their prefixes alive.
7. Siege, 1000+ agents: frame time unchanged versus a run with `Enabled` toggled off.
8. **Test the §8 coupling.** With `Diagnostic Logging` on, count friendly hits where `windup=False`. If that count is non-trivial, agents are being blocked mid-arc without ever being stamped as crowded — `CrowdState.Stamp` must then move above the `if (!windup) return true;` guard in `WindupTransparencyPatch`.

Toggling `Enabled` off in MCM must restore vanilla behavior exactly. If it does not, a patch is missing its settings gate.

- [ ] **Step 7: Write the handoff and commit**

Append a dated entry to `notes.md` covering what shipped, which of the three open questions resolved which way, and the measured `WindupThreshold`.

```bash
cd ~/AI/projects/ProperShieldWalls
git add notes.md AttackRemap.cs Settings.cs ProperShieldWalls.Tests
git commit -m "docs: v2 in-game validation results and tuned WindupThreshold

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

Do not mark this task complete on a clean build alone. It is complete when Mark confirms the behavior in a live battle, or explicitly waives the check.

---

## Notes for the implementer

**Why the prefix returns `true`.** It is counterintuitive: we want to suppress the original's behavior, and the reflex is `return false`. Read §4.3 of the spec. `MeleeHitCallback` already guards its whole penalty block behind `colReaction != ContinueChecking`, which is how vanilla lets kicks and bashes pass through friendlies. Setting the flag and returning `true` reuses that. Returning `false` would suppress other mods' prefixes and skip the method's trailing sound-alarm block.

**Why `DecideWeaponCollisionReaction` is not patched.** The POC had a "safety net" postfix on it. It is unnecessary: that method has exactly one call site in the entire game — `Mission.cs:5376` — which sits inside `MeleeHitCallback`, inside the block the guard already skips. The two `AgentApplyDamageModel` overrides that also name it are only reached from that same line.

**Why the pure files avoid TaleWorlds types.** The test project targets `net8.0`. The game assemblies are .NET Framework and cannot be loaded by that host. Source-linking `CrowdState.cs` and `AttackRemap.cs` keeps them testable. If you find yourself wanting `Agent` inside either file, put that logic in `AttackGate.Apply` instead.

**A negative result worth not re-deriving.** Do not "fix" spacing by editing `Modules/Native/ModuleData/monsters.xml` `body_capsule radius` (stock `0.37`). Both Nexus mods that tried it (#495, #8392) produce an unavoidable "telekinesis bubble" push and ignore weapon reach. The author of #8392 says so himself.
