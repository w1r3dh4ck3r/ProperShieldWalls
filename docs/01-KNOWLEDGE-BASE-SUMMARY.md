# Knowledge Base Summary — Proper Shield Walls (Othismos)

**Date:** 2026-05-27  
**Game version:** v1.3.14 (verified from dev memory; working modlist is v1.3.15 — confirm before shipping)  
**Source files:** `~/AI/knowledge/game_dev/bannerlord_modding_research.md`, `bannerlord-api.md`, `bannerlord-memory.md`, `prisoner-transport-arch.md`, plus two local projects: ProperShieldWalls (old, being discarded) and FormationsPlus (confirmed working).

---

## What We Already Know

### Infrastructure (All Confirmed in Production Code)

| API | Source | Status |
|-----|--------|--------|
| `MissionBehavior.BehaviorType => MissionBehaviorType.Other` | PSW old code | Confirmed |
| `MissionBehavior.OnMissionTick(float dt)` | PSW old code | Confirmed |
| `MissionBehavior.OnAgentBuild(Agent agent, Banner banner)` | FormationsPlus | Confirmed |
| `MissionBehavior.OnDeploymentFinished()` | FormationsPlus | Confirmed |
| `MissionBehavior.AfterStart()` | FormationsPlus | Confirmed |
| `Mission.Current.GetMissionBehavior<T>()` | PSW old code | Confirmed |
| `Mission.PlayerTeam` | FormationsPlus | Confirmed |
| `mission.AddMissionBehavior(new T())` | PSW SubModule | Confirmed |
| `SubModule.OnMissionBehaviorInitialize(Mission)` | PSW old code | Confirmed |
| Harmony `PatchAll()` + individual `CreateClassProcessor(type).Patch()` | PSW old code | Confirmed |

### Formation Enumeration (Confirmed in FormationsPlus)

| API | Notes |
|-----|-------|
| `Team.FormationsIncludingEmpty` | IEnumerable<Formation>, iterates all 8 slots |
| `formation.Index` | int, 0-7 |
| `formation.Arrangement.GetAllUnits()` | Enumerates agents (returns IFormationUnit cast to Agent) |
| `agent.Formation` (get/set) | Assignment via setter works; triggers reassignment |
| `formation.Team.TriggerOnFormationsChanged(formation)` | Required after reassignment to notify AI |
| `agent.StopUsingGameObject()` | Must call before reassignment |
| `OrderController.IsFormationSelectable(Formation)` | Harmony-patchable |

### Agent Combat State (Confirmed in PSW Old Code)

| API | Notes |
|-----|-------|
| `Agent.WieldedWeapon` → `MissionWeapon` | |
| `MissionWeapon.Item.PrimaryWeapon.WeaponClass` → `WeaponClass` | Can be null — guard it |
| `Agent.GetCurrentAction(0)` → `ActionIndexCache` | Channel 0 = main action |
| `Agent.GetCurrentActionProgress(0)` → float [0..1] | |
| `Agent.SetActionChannel(0, ActionIndexCache, startProgress: float)` | Tested working for attack restore |
| `Agent.IsActive()` | Null-safe check before any agent ops |
| `Agent.IsPlayerControlled` | |
| `Agent.Position` → `Vec3` | |
| `Agent.LookDirection` → `Vec3` | |
| `Agent.Team` → `Team` | |
| `Agent.IsHuman` | Confirmed in FormationsPlus |
| `Agent.HasMount`, `Agent.MountAgent` | Confirmed in FormationsPlus |
| `Agent.Equipment[EquipmentIndex.X]` → `EquipmentElement` | |
| `WeaponClass.SmallShield`, `WeaponClass.LargeShield` | Confirmed in FormationsPlus |

### Combat Mechanics Harmony Targets (Confirmed in PSW Old Code)

| Target | How Used |
|--------|----------|
| `Mission.MeleeHitCallback(ref AttackCollisionData, Agent, Agent, ref MeleeCollisionReaction)` | Prefix — intercept all melee hits |
| `AttackCollisionData.AttackProgress` | Float property, readable in prefix |
| `AttackCollisionData._attackBlockedWithShield` | Private bool field — writable via `__makeref` + `SetValueDirect` |
| `AttackCollisionData._collidedWithShieldOnBack` | Same |
| `MissionCombatMechanicsHelper.DecideWeaponCollisionReaction` | Postfix — override `ref colReaction` |
| `SandboxAgentApplyDamageModel.CanWeaponIgnoreFriendlyFireChecks(WeaponComponentData, ref bool)` | Prefix — can force `__result = true` |
| `SandboxAgentApplyDamageModel.CalculateShieldDamage` | Prefix — force `__result = 0` |
| `Mission.RegisterBlow(Agent, Agent)` | Prefix — can skip blow registration |
| `MeleeCollisionReaction.Bounced`, `.ContinueChecking` | Enum values |

### Agent Equipment Detection (Confirmed in FormationsPlus)

```csharp
for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
{
    var element = agent.Equipment[i];
    if (element.IsEmpty) continue;
    var weaponClass = element.Item?.PrimaryWeapon?.WeaponClass;
    // WeaponClass.SmallShield / LargeShield = has shield
    // WeaponClass.OneHandedPolearm / TwoHandedPolearm / LowGripPolearm = has polearm
}
```

### Harmony Patching Infrastructure

- Harmony ID in use: `"com.propershieldwalls.patch"` (existing, will reuse)
- Pattern: patch each type individually in a loop, catch failures, log but don't crash
- Guard against double-patching: existing code doesn't use static flag — add one
- `HarmonyPriority.High` on `MeleeHitCallback` prefix — needed to run before other combat mods (RBM)

### Project Infrastructure

- **Build system:** .csproj targeting .NET 4.7.2, auto-deploys to `D:\...\Modules\ProperShieldWalls\`
- **Game folder:** `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\`
- **WSL module path:** `/mnt/d/...` (D drive not accessible from current WSL session; build must run on Windows side)
- **References confirmed:** 0Harmony, MCMv5, TaleWorlds.Core, TaleWorlds.MountAndBlade, TaleWorlds.Engine, TaleWorlds.Library, SandBox

### Stamina Mod

- Identified as **"StaminaSystem"** in the working modlist (v1.3.15, 104 mods)
- Listed alongside ImmersiveCombat and UnblockableThrust — likely the "Stamina System" mod on Nexus
- **API surface: UNKNOWN.** Need external research to confirm whether it exposes a drain API or is read-only.
- **MVP fallback:** if no public drain API exists, implement our own simple stamina tracking (float per formation, drain per tick while Locked). This is explicitly permitted as an MVP cheat per the design brief.

---

## What We Learned From the Old "ProperShieldWalls" Codebase

The old mod solved a different problem (friendly melee bypass through shield walls). It is being discarded as a feature, but the following are **reusable**:

1. **Infrastructure:** `SubModule.cs` pattern, `MissionBehaviorType.Other`, per-type Harmony patching loop — all clean and reusable.

2. **`FriendlyFireCheckPatch`:** Patching `CanWeaponIgnoreFriendlyFireChecks` to allow friendly hits — **directly needed** in our mod. During locked othismos, front-rank stabbers will hit their own rear ranks. We want those attacks to pass through without doing friendly damage. The angle-based "behind" check is wrong for our use case; we'll replace it with a "Locked state" check.

3. **`MeleeHitFriendlyBypassPatch`:** The `__makeref` + `SetValueDirect` pattern for clearing `AttackCollisionData` shield flags is confirmed working and we'll need it.

4. **`ShieldWallBehaviour.SetActionChannel` restore pattern:** Attack animation restore via `OnMissionTick` — useful if we need to hold agents in attack animation.

5. **`WeaponBypassConfig`:** The weapon-class-to-settings lookup pattern is fine to keep.

---

## Confirmed Gaps (What We Don't Know Yet)

These require either external research or DLL inspection:

1. **`Formation.ArrangementOrder`** — exact type/enum for reading ShieldWall vs other arrangements. Likely `ArrangementOrder.ArrangementOrderEnum == ArrangementOrder.ArrangementOrderEnum.ShieldWall` but NOT confirmed.

2. **Agent rank/rank-position in formation** — how to determine an agent is in rank 1 vs rank 2 vs rank 3. Formation arrangement exposes per-unit positions but the rank-indexing API is unknown.

3. **Formation anchor position** — how to read and write the formation's world-space anchor/center. This is what we translate during pressure resolution.

4. **Agent melee AI decision intercept** — the single biggest unknown. What method(s) determine that an AI agent will leave formation to pursue an enemy? What method picks the attack direction? These are the core Harmony patch targets for the "locked in place, stab only" behavior.

5. **Stamina mod (StaminaSystem) API** — public API surface, whether drain can be triggered from outside.

6. **Shield-raised state** — whether an API exists to query `agent.IsShieldRaised` or equivalent, or whether we have to infer it from the current animation action.

---

## Summary Assessment

**What we can proceed on without external research:**
- SubModule setup, MCM settings, Harmony patching infrastructure
- Formation enumeration (ranks require more research)
- Friendly fire bypass mechanics (confirmed working)
- Mission behavior lifecycle

**What we cannot proceed on without resolving the core unknown:**
- The stab-in-slot proof-of-concept (needs Harmony target for agent AI)
- Formation anchor translation (needs `Formation` anchor API)
- Rank enumeration for pressure formula

**The critical unknown has not changed:** we do not yet have a confirmed Harmony patch target for suppressing free-swing behavior in AI agents. This is the gate item.
