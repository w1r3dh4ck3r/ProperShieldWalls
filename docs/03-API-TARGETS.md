# API Target Document — Proper Shield Walls (Othismos)

**Status:** Partial. Confirmed entries are from production code. Unconfirmed entries are from community research / training data — flagged explicitly. All unconfirmed entries must be verified against game DLLs (ILSpy) before use.

**Game version target:** v1.3.14 (verify against installed version before shipping)

---

## 1. MissionBehavior Lifecycle (CONFIRMED)

All confirmed from FormationsPlus and PSW old code running in the same game version.

```csharp
public class OthismosBehaviour : MissionBehavior
{
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    // Called once after mission setup, before first tick
    public override void AfterStart() { }

    // Called when all units finish spawning and deploy
    public override void OnDeploymentFinished() { }

    // Called when a new agent spawns (reinforcements)
    public override void OnAgentBuild(Agent agent, Banner banner) { }

    // Called every game tick (dt = seconds since last tick)
    public override void OnMissionTick(float dt) { }
}
```

Registration in SubModule:
```csharp
public override void OnMissionBehaviorInitialize(Mission mission)
{
    base.OnMissionBehaviorInitialize(mission);
    mission.AddMissionBehavior(new OthismosBehaviour());
}
```

---

## 2. Formation Enumeration (CONFIRMED)

From FormationsPlus production code:

```csharp
// All 8 formation slots (including empty ones)
foreach (Formation f in Mission.PlayerTeam.FormationsIncludingEmpty)
{
    int idx = f.Index;  // 0-7
    int count = f.CountOfUnits;  // (UNCONFIRMED — may be CountOfUnitsWithoutDetachedOnes)

    // Enumerate all agents in formation
    foreach (IFormationUnit unit in f.Arrangement.GetAllUnits())
    {
        if (unit is Agent agent)
        {
            // agent confirmed accessible this way
        }
    }
}

// Formation reassignment
agent.StopUsingGameObject();
agent.Formation = targetFormation;
formation.Team.TriggerOnFormationsChanged(formation);
```

---

## 3. Formation Arrangement Order (CONFIRMED — from RBM source)

Two equivalent patterns confirmed in RBM production code:

```csharp
// Pattern A — struct equality to static field (Frontline.cs:442)
bool isShieldWall = formation.ArrangementOrder == ArrangementOrder.ArrangementOrderShieldWall;

// Pattern B — enum comparison (Behaviours.cs:1415)  
bool isShieldWall = formation.ArrangementOrder.OrderEnum == ArrangementOrderEnum.ShieldWall;
```

Other static instances (confirmed from BUTR API + RBM usage):
```csharp
ArrangementOrder.ArrangementOrderLine
ArrangementOrder.ArrangementOrderColumn
ArrangementOrder.ArrangementOrderLoose
ArrangementOrder.ArrangementOrderScatter
ArrangementOrder.ArrangementOrderSquare
ArrangementOrder.ArrangementOrderCircle
ArrangementOrder.ArrangementOrderSkein
ArrangementOrder.ArrangementOrderShieldWall
```

Set arrangement:
```csharp
formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderShieldWall);
```

---

## 4. Formation Anchor / Position (CONFIRMED — from RBM source)

```csharp
// Read formation's current global direction
Vec2 dir = formation.CurrentDirection;

// Read cached average/median positions
WorldPosition median = formation.QuerySystem.Formation.CachedMedianPosition;
Vec2 average = formation.QuerySystem.Formation.CachedAveragePosition;  // (may not exist — use median)

// Read/get per-agent slot position within formation
Vec2 slotPos = formation.GetCurrentGlobalPositionOfUnit(agent, false);

// Read slot position as WorldPosition (confirmed in GetOrderPositionOfUnit prefix context)
WorldPosition unitWP = formation.GetOrderPositionOfUnit(agent);

// Set formation movement (moves the anchor):
formation.SetMovementOrder(MovementOrder.MovementOrderMove(worldPosition));
formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(direction));

// Set formation sizing:
formation.SetFormOrder(FormOrder.FormOrderCustom(unitsCount / 4.5f));  // unit spacing

// Read movement order type (native enum preferred):
OrderType orderType = formation.GetReadonlyMovementOrderReference().OrderType;
// OrderType.Charge, OrderType.ChargeWithTarget, OrderType.Move, OrderType.Advance, etc.

// QuerySystem (cached per-tick data):
formation.QuerySystem.IsInfantryFormation   // bool
formation.QuerySystem.IsCavalryFormation    // bool  
formation.QuerySystem.IsRangedFormation     // bool
formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation  // FormationQuerySystem
```

**For pressure resolver:** Use `MovementOrder.MovementOrderMove(newWorldPosition)` to translate formation anchor each tick. Compute `newWorldPosition` from current median + delta along engagement axis.

---

## 5. Agent Rank/File Position (CONFIRMED — from RBM source)

```csharp
// Cast Agent to IFormationUnit to access rank/file info:
int rankIndex = ((IFormationUnit)unit).FormationRankIndex;  // 0 = front rank
int fileIndex = ((IFormationUnit)unit).FormationFileIndex;  // 0 = leftmost file

// Confirmed usage in AgentAi.cs:433:
if (((IFormationUnit)unit).FormationRankIndex == 0)
    __result = Agent.UsageDirection.DefendDown;

// Neighbor detection (confirms unit is at formation edge):
bool noLeftNeighbor  = formation.Arrangement.GetNeighborUnitOfLeftSide(unit) == null;
bool noRightNeighbor = formation.Arrangement.GetNeighborUnitOfRightSide(unit) == null;
```

**For pressure formula ranks:**
```csharp
// Rank 0 = front, 1 = second, 2 = third, etc.
int frontCount  = formation.GetUnitsWithoutDetachedOnes().Count(u => ((IFormationUnit)u).FormationRankIndex == 0);
int secondCount = formation.GetUnitsWithoutDetachedOnes().Count(u => ((IFormationUnit)u).FormationRankIndex == 1);
int thirdCount  = formation.GetUnitsWithoutDetachedOnes().Count(u => ((IFormationUnit)u).FormationRankIndex == 2);
// MVP pressure: frontCount * 1.0 + secondCount * 0.5 + thirdCount * 0.25
```

---

## 6. Melee AI Decision Intercept (CONFIRMED — gate item resolved)

**The gate item is resolved.** The correct patch surfaces are confirmed from RBM source code.

### 6a. Per-agent position control — `Formation.GetOrderPositionOfUnit` prefix (PRIMARY)

This is the canonical solution used by both RBM and Organized Frontline. By returning a custom `WorldPosition` from this method, we control exactly where the engine thinks each agent's slot is.

```csharp
// From RBM Frontline.cs:240 — confirmed signature:
[HarmonyPatch(typeof(Formation), "GetOrderPositionOfUnit")]
[HarmonyPrefix]
public static bool Prefix(Formation __instance, ref Agent unit, ref WorldPosition __result)
{
    if (unit == null || !unit.IsActive() || !unit.IsAIControlled) return true;
    
    // If this formation is in our Locked state:
    if (OthismosState.IsLocked(__instance))
    {
        // Hold in current position (no chase, but not frozen — engine still handles attack)
        __result = unit.GetWorldPosition();
        unit.SetTargetPosition(unit.GetWorldPosition().AsVec2);
        return false;
    }
    return true;  // vanilla for all other cases
}
```

**Key insight:** Returning `false` with `__result = unit.GetWorldPosition()` tells the engine the agent's slot IS the agent's current position. The engine then selects "attack target in place" rather than "chase target to their location". This is what keeps agents stabbing in slot.

### 6b. Slot enforcement — `HumanAIComponent.ParallelUpdateFormationMovement` postfix

For formations in Move order (which we'll use for the "locked" movement phase):

```csharp
[HarmonyPatch(typeof(HumanAIComponent), "ParallelUpdateFormationMovement")]
[HarmonyPostfix]
public static void Postfix(HumanAIComponent __instance, Agent ___Agent)
{
    if (!___Agent.IsActive() || ___Agent.Formation == null) return;
    if (!OthismosState.IsLocked(___Agent.Formation)) return;
    
    // Force agent back to its formation slot:
    PropertyInfo prop = typeof(HumanAIComponent).GetProperty("ShouldCatchUpWithFormation");
    prop.SetValue(__instance, true, BindingFlags.NonPublic | BindingFlags.SetProperty, null, null, null);
    
    Vec2 slotPos = ___Agent.Formation.GetCurrentGlobalPositionOfUnit(___Agent, false);
    var integ = ___Agent.Formation.CachedFormationIntegrityData;
    ___Agent.SetFormationIntegrityData(slotPos, ___Agent.Formation.CurrentDirection,
        integ.AverageVelocityExcludeFarAgents,
        integ.AverageMaxUnlimitedSpeedExcludeFarAgents,
        integ.DeviationOfPositionsExcludeFarAgents, true);
}
```

### 6c. Shield enforcement — `Agent.UpdateFormationOrders` prefix

```csharp
[HarmonyPatch(typeof(Agent), "UpdateFormationOrders")]
[HarmonyPrefix]
public static bool Prefix(Agent __instance)
{
    if (__instance.Formation == null || !__instance.IsAIControlled) return true;
    if (!OthismosState.IsLocked(__instance.Formation)) return true;
    
    if (__instance.Formation.ArrangementOrder.OrderEnum == ArrangementOrderEnum.ShieldWall)
    {
        __instance.EnforceShieldUsage(
            ArrangementOrder.GetShieldDirectionOfUnit(
                __instance.Formation, __instance,
                __instance.Formation.ArrangementOrder.OrderEnum));
        return false;
    }
    return true;
}
```

### 6d. Attack direction — no clean API, spatial constraint is correct approach

There is **no `ForceAttackDirection` API**. RBM confirms this and does not force thrust animations. The correct approach:
- `GetOrderPositionOfUnit` prefix returns `unit.GetWorldPosition()` → agent fights in place
- With enemies 1-2m ahead and allies left/right (no lateral swing room), engine naturally selects thrusts
- No `SetActionChannel` needed for attack direction forcing

**IMPORTANT:** `SetActionChannel` can still be used for other purposes (e.g., forcing `DefendDown` animation when no target visible) but should not be called per-tick for attack direction.

---

## 7. Shield State Query (CONFIRMED — from RBM source)

```csharp
// Check if agent has a shield equipped (any slot) — RBM Frontline.cs:438:
bool hasShieldEquipped = !unit.WieldedOffhandWeapon.IsEmpty 
    && unit.WieldedOffhandWeapon.IsShield();

// Cached version (faster, used in AgentAi.cs:411):
bool hasShieldCached = unit.HasShieldCached;

// Check if shield usage is encouraged by formation:
bool encouraged = ((IFormationUnit)unit).IsShieldUsageEncouraged;

// Force shield posture:
agent.EnforceShieldUsage(Agent.UsageDirection direction);
// Direction values: DefendUp, DefendDown, DefendLeft, DefendRight, AttackEnd, None
```

---

## 8. Combat Mechanics Harmony Targets (CONFIRMED)

All confirmed from PSW old code:

```csharp
// Bypass friendly fire check — required for rear-rank stabbers hitting through friendlies
[HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CanWeaponIgnoreFriendlyFireChecks")]
// Prefix: set __result = true; return false; for locked formation agents

// Intercept melee hit — read AttackProgress, clear shield flags
[HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
// Prefix with Priority.High

// Override collision reaction — prevent Bounced from triggering
[HarmonyPatch(typeof(MissionCombatMechanicsHelper), "DecideWeaponCollisionReaction")]  
// Postfix: override colReaction = ContinueChecking

// Prevent friendly damage during bypass
[HarmonyPatch(typeof(Mission), "RegisterBlow")]
// Prefix: return false for friendly hits in Locked state

// Prevent shield damage from friendly hits in locked state
[HarmonyPatch(typeof(SandboxAgentApplyDamageModel), "CalculateShieldDamage")]
// Prefix: return 0 in Locked state
```

---

## 9. Agent Equipment Detection (CONFIRMED)

```csharp
// Check if agent has a shield equipped (any slot)
bool HasShield(Agent agent)
{
    for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
    {
        var elem = agent.Equipment[i];
        if (elem.IsEmpty) continue;
        var wc = elem.Item?.PrimaryWeapon?.WeaponClass;
        if (wc == WeaponClass.SmallShield || wc == WeaponClass.LargeShield)
            return true;
    }
    return false;
}
```

---

## 10. Stamina Mod Integration (UNCONFIRMED — needs external research)

**Mod identity:** "StaminaSystem" in working modlist. Likely "Stamina System" on Nexus.

**MVP approach A (preferred if API exists):**
```csharp
// Hypothetical — depends on what StaminaSystem exposes
StaminaSystem.AgentStamina.DrainStamina(agent, drainAmount);
float current = StaminaSystem.AgentStamina.GetStamina(agent);
```

**MVP approach B (fallback — own tracking):**
```csharp
private Dictionary<Formation, float> _formationStamina = new();
// Start: 100f per formation
// Per tick while Locked: -drainRate * dt
// Exit condition: stamina <= 0
```

Approach B is explicitly permitted in the design brief and requires no external dependency. Recommend starting with B and adding A later if the stamina mod exposes a usable API.

---

## Version Fragility Notes

The following are known to break across patches:
- Private field names in `AttackCollisionData` (`_attackBlockedWithShield`, `_collidedWithShieldOnBack`) — if TaleWorlds renames these in v1.3.15+, our `__makeref` access will fail silently (null FieldInfo). Current code logs this at startup.
- `MissionCombatMechanicsHelper.DecideWeaponCollisionReaction` — method body changes can break Harmony postfix
- `SandboxAgentApplyDamageModel.*` — sandbox-specific, may move to different class in updates

**Mitigation:** Log all FieldInfo lookups at startup. If a field is null, log a warning and skip that behavior rather than crashing.
