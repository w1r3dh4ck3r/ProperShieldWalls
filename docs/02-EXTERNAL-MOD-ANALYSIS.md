# External Mod Analysis Report — Proper Shield Walls (Othismos)

**Date:** 2026-05-27  
**Sources:** Nexus mod pages, RBM source code (cloned at `/tmp/rbm-source/`), BUTR API docs, web research.

All claims are attributed. Claims marked ⚠️ INFERRED are from Nexus descriptions or community notes, not verified source code.

---

## 1. Realistic Battle Mod (RBM)

**Author:** Fellow93  
**Nexus:** https://www.nexusmods.com/mountandblade2bannerlord/mods/791  
**Source:** https://github.com/Fellow93/RealisticBattleProject — **PUBLIC, cloned to `/tmp/rbm-source/`**  
**Most relevant files:**
- `/tmp/rbm-source/RealisticBattleAiModule/AiModule/Frontline.cs` (801 lines)
- `/tmp/rbm-source/RealisticBattleAiModule/AiModule/Behaviours.cs` (1803 lines)
- `/tmp/rbm-source/RealisticBattleAiModule/AiModule/AgentAi.cs` (785 lines)

### What RBM does for formation discipline

**A. Per-agent position control during contact — `Formation.GetOrderPositionOfUnit` prefix**

This is the primary mechanism. When an infantry formation is on Charge/ChargeWithTarget and a unit is not detached, RBM evaluates the unit's local neighborhood (allies front/left/right, enemies front) and picks one of five micro-decisions:

```csharp
// Frontline.cs:240 — exact signature:
[HarmonyPrefix]
[HarmonyPatch("GetOrderPositionOfUnit")]
private static bool PrefixGetOrderPositionOfUnit(
    Formation __instance,
    ref WorldPosition ____orderPosition,
    ref IFormationArrangement ____arrangement,
    ref Agent unit,
    List<Agent> ____detachedUnits,
    ref WorldPosition __result)
```

The proximity query pattern (Frontline.cs:407-411):
```csharp
MBList<Agent> alliesFront = new MBList<Agent>();
MBList<Agent> alliesLeft = new MBList<Agent>();
MBList<Agent> alliesRight = new MBList<Agent>();
MBList<Agent> enemiesFront = new MBList<Agent>();

alliesFront = mission.GetNearbyAllyAgents(unitPosition + direction * 1.35f, 1.35f, unit.Team, alliesFront);
alliesLeft  = mission.GetNearbyAllyAgents(unitPosition + leftVec * 1.35f,    1.35f, unit.Team, alliesLeft);
alliesRight = mission.GetNearbyAllyAgents(unitPosition + rightVec * 1.35f,   1.35f, unit.Team, alliesRight);
enemiesFront = mission.GetNearbyEnemyAgents(unitPosition + direction * 1.5f, 2f,    unit.Team, enemiesFront);
```

Decision weights and outcomes (Frontline.cs:462-502, simplified):
- **Attack** (return `true` = let engine pick target) — when `attack > others`
- **BackStep** — step back half a step when `fallback > others`; sets `unit.SetTargetPosition(backVec2)`, returns custom `WorldPosition`
- **FindAlly** — step toward nearest ally gap (gap in front-left or front-right); returns offset `WorldPosition`
- **FlankAllyLeft / Right** — fill a gap to the side; returns offset `WorldPosition`
- **Rest** — hold position (`__result = unit.GetWorldPosition()`, `unit.SetTargetPosition(unit.GetWorldPosition().AsVec2)`, return false)

ShieldWall arrangement adds +2 to `hasShieldAdditive`, which upweights FindAlly (fill gaps) and downweights flanking (Frontline.cs:482-487). This is what makes shield wall agents naturally "close the line" rather than pursuing individuals.

ArrangementOrder check (Frontline.cs:442-444):
```csharp
if (__instance.ArrangementOrder == ArrangementOrder.ArrangementOrderShieldWall)
{
    hasShieldAdditive += 2;
}
```

**This is the correct comparison pattern** — struct equality to the static field `ArrangementOrder.ArrangementOrderShieldWall`, confirmed in production code.

**B. Slot enforcement during Move order — `HumanAIComponent.ParallelUpdateFormationMovement` postfix**

```csharp
// Frontline.cs:707-730 — exact pattern:
[HarmonyPostfix]
[HarmonyPatch("ParallelUpdateFormationMovement")]
private static void PostfixParallelUpdateFormationMovement(
    ref HumanAIComponent __instance, ref Agent ___Agent)
{
    if (!___Agent.IsActive() || ___Agent.Formation == null) return;
    
    MovementOrderEnum orderType = ___Agent.Formation.GetReadonlyMovementOrderReference().OrderEnum;
    
    if (___Agent.Controller == AgentControllerType.AI 
        && orderType == MovementOrderEnum.Move 
        && ___Agent.Formation.ArrangementOrder != ArrangementOrder.ArrangementOrderColumn)
    {
        // Force agent to catch up to its slot:
        PropertyInfo prop = typeof(HumanAIComponent).GetProperty("ShouldCatchUpWithFormation");
        prop.SetValue(__instance, true, BindingFlags.NonPublic | BindingFlags.SetProperty, null, null, null);
        
        Vec2 slotPos = ___Agent.Formation.GetCurrentGlobalPositionOfUnit(___Agent, false);
        FormationIntegrityDataGroup integ = ___Agent.Formation.CachedFormationIntegrityData;
        ___Agent.SetFormationIntegrityData(slotPos, ___Agent.Formation.CurrentDirection,
            integ.AverageVelocityExcludeFarAgents,
            integ.AverageMaxUnlimitedSpeedExcludeFarAgents,
            integ.DeviationOfPositionsExcludeFarAgents, true);
    }
    if (orderType == MovementOrderEnum.Charge || orderType == MovementOrderEnum.ChargeToTarget)
    {
        ___Agent.SetFormationFrameDisabled();
    }
}
```

Note: RBM defines its own local `MovementOrderEnum` mirroring the native `OrderType` enum. For our use, prefer `formation.GetReadonlyMovementOrderReference().OrderType == OrderType.ChargeWithTarget` which uses the native enum directly (confirmed in Frontline.cs:220, Behaviours.cs:1411).

**C. Per-rank shield direction — `ArrangementOrder.GetShieldDirectionOfUnit` postfix**

AgentAi.cs:399-482. Rank 0 (front) → `DefendDown`. Left-edge file → `DefendLeft`. Right-edge file → `DefendRight`. Inner ranks → `AttackEnd`. Two-handed or ranged → `None`.

```csharp
// Signature confirmed (AgentAi.cs:403):
private static void Postfix(ref Agent.UsageDirection __result, Formation formation, 
    Agent unit, ArrangementOrderEnum orderEnum)
{
    // ...
    case ArrangementOrderEnum.ShieldWall:
        if (((IFormationUnit)unit).FormationRankIndex == 0)
        {
            __result = Agent.UsageDirection.DefendDown;
            return;
        }
        if (formation.Arrangement.GetNeighborUnitOfLeftSide(unit) == null)
        {
            __result = Agent.UsageDirection.DefendLeft;
            return;
        }
        if (formation.Arrangement.GetNeighborUnitOfRightSide(unit) == null)
        {
            __result = Agent.UsageDirection.DefendRight;
            return;
        }
        __result = Agent.UsageDirection.AttackEnd;
        return;
}
```

**D. Shield enforcement per tick — `Agent.UpdateFormationOrders` prefix**

Behaviours.cs:1404-1440. Called per tick. For ChargeWithTarget + ShieldWall arrangement:

```csharp
[HarmonyPrefix]
[HarmonyPatch("UpdateFormationOrders")]
private static bool PrefixUpdateFormationOrders(ref Agent __instance)
{
    if (__instance.Formation != null && __instance.IsAIControlled 
        && __instance.Formation.GetReadonlyMovementOrderReference().OrderType == OrderType.ChargeWithTarget)
    {
        if (__instance.Formation.ArrangementOrder.OrderEnum == ArrangementOrderEnum.ShieldWall)
        {
            __instance.EnforceShieldUsage(
                ArrangementOrder.GetShieldDirectionOfUnit(
                    __instance.Formation, __instance, 
                    __instance.Formation.ArrangementOrder.OrderEnum));
        }
        // ...
        return false;  // skip vanilla shield logic
    }
    return true;
}
```

**E. Behavior weight tuning — `HumanAIComponent.SetBehaviorValueSet` postfix**

Behaviours.cs:1050-1196. Tunes `AISimpleBehaviorKind` weights via `__instance.OverrideBehaviorParams(kind, w0, d0, w1, d1, w2)`. The parameter shape: `weight @ 0m, distance0, weight @ d0, distance1, weight @ d1`. For infantry charge:
- `OverrideBehaviorParams(AISimpleBehaviorKind.Melee, 5.5f, 3f, 4f, 10f, 0.01f)` — melee weight drops to near-zero beyond 10m
- `OverrideBehaviorParams(AISimpleBehaviorKind.GoToPos, 4f, 2f, 4f, 10f, 6f)` — hold-position weight stays high at range

### What we borrow from RBM

| What | Source location | Adaptation needed |
|------|-----------------|-------------------|
| `GetOrderPositionOfUnit` prefix pattern | Frontline.cs:240-625 | Simplify 5-decision to 2 (hold/attack); remove posture/stamina from RBM's own system |
| `ParallelUpdateFormationMovement` postfix + `SetFormationIntegrityData` slot lock | Frontline.cs:708-730 | Condition on our "Locked" state instead of Move order |
| `GetShieldDirectionOfUnit` postfix for rank-aware shield direction | AgentAi.cs:399-482 | Can use verbatim for ShieldWall case |
| `UpdateFormationOrders` prefix for `EnforceShieldUsage` | Behaviours.cs:1404-1440 | Condition on our "Locked" state |
| `ArrangementOrder == ArrangementOrder.ArrangementOrderShieldWall` comparison | Frontline.cs:442 | Use verbatim |
| `((IFormationUnit)unit).FormationRankIndex` for rank detection | AgentAi.cs:433 | Use verbatim |
| `formation.GetCurrentGlobalPositionOfUnit(agent, false)` | Frontline.cs:722 | Use verbatim |
| `Mission.GetNearbyEnemyAgents(Vec2, float, Team, MBList<Agent>)` | Frontline.cs:411 | Use for engagement detection |

### What RBM does NOT do (and we need to add)

- **Formation anchor translation** — RBM adjusts per-agent positions within the formation, but does not physically shove one formation into the other's space via anchor translation. Our pressure resolver is novel.
- **Binary Locked/Unlocked engagement state** — RBM applies formation discipline continuously during Charge; we apply it only during the ShieldWall+ShieldWall contact window.
- **Stamina-based wall-break** — RBM has posture/stamina for individual agents (via its own `AgentStances` dictionary); we track stamina per formation.
- **Engagement exit logic** — no equivalent in RBM; it relies on the normal battle resolution.

---

## 2. Organized Frontline (Nexus 9058)

**Author:** Ulfkarl  
**Source:** Not published publicly. Working from Nexus description and author's own conflict warnings.

### What we know (⚠️ INFERRED from public sources)

- Patches `Formation.GetOrderPositionOfUnit` and `HumanAIComponent.SetBehaviorValueSet` (author's own conflict note explicitly names these)
- Uses a per-tick advance distance and per-agent "dead zone" logic for contact
- Works during `OrderType.Charge` and `OrderType.ChargeWithTarget` in field battles only
- No XML changes, no animation changes — purely per-tick AI position redirection

### Difference from our mod

We cannot read the actual code. Based on description, it implements directional advance discipline but not the locked mutual-press mechanic or formation anchor shoving. The "contact zone" concept is the closest public precedent, but our othismos adds the pressure delta/translation layer on top.

### What we borrow

Nothing directly — source closed. The same patch surface (confirmed) is used by RBM, which we CAN read.

---

## 3. Overhead Shieldwall / Testudo (Nexus 1394)

**Type:** Asset-only mod (no C# code, no Harmony)

This mod is a **mesh/model replacement**. It changes shield models to hold correctly for overhead use. Earlier versions used Sturgian shieldwall arrangement parameters; later versions are pure asset swaps.

There is no code to borrow. Overhead shield direction can be achieved in code via `Agent.EnforceShieldUsage(Agent.UsageDirection.DefendUp)` using the RBM pattern — no new animations required.

---

## 4. Formation Tweaks (Nexus 9378)

**Source:** Not publicly available. Working from Nexus description only.

**Mechanic:** Live-state reassignment based on ammo/weapon state. Archers without ammo move to melee formations; vice versa. Uses `agent.Formation = targetFormation` setter (same pattern as FormationsPlus, confirmed working).

**What we borrow:** Nothing directly relevant to othismos. The formation reassignment API is already confirmed from FormationsPlus.

---

## 5. Save Battle Formation (Nexus 1786)

**Source:** Not publicly available.

⚠️ INFERRED from description: saves relative formation positions from `Follow Me` orders and replays them on subsequent move commands. Likely patches `Formation.SetMovementOrder`. 

**What we borrow:** Nothing. Different domain (player order persistence). `Formation.SetMovementOrder` is already in our API targets from RBM.

---

## 6. Auto Battle Formation (Nexus 4581 / 9026)

**Status:** Abandoned (original hidden 2025-11-14). Successor also hidden. No source available.

**What we borrow:** Nothing.

---

## 7. StaminaSystem (Nexus 7933)

**Source:** No GitHub or public repository found.

**What we know:** Stamina = 100 HP pool. 4th-consecutive-attack penalty. Bow-hold drain 5/s. Accuracy penalties below 30% stamina. RBM compatibility note confirms it reads `Agent` state internally.

**Public API:** **None found.** No documented `IStaminaProvider` interface, no public method for external drain.

### Decision

Implement our own formation-level fatigue float (per the MVP design brief permitting this). Do not attempt to hook StaminaSystem via reflection — that couples us to an undocumented internal structure that could break on any update.

**Our MVP stamina approach:**
```csharp
private Dictionary<Formation, float> _formationStamina = new();
// At Locked state entry: _formationStamina[formation] = 100f
// Per tick while Locked: _formationStamina[formation] -= drainRate * dt
// Exit condition: _formationStamina[formation] <= 0f
```

---

## Summary: The Four Confirmed Harmony Patch Targets

These are the patch surfaces we will use for the MVP, all confirmed from RBM source code:

| Patch target | Patch type | Purpose |
|---|---|---|
| `Formation.GetOrderPositionOfUnit` | **Prefix** | Per-agent positional discipline during contact; return `unit.GetWorldPosition()` for locked agents |
| `Agent.UpdateFormationOrders` | **Prefix** | Per-tick `EnforceShieldUsage` enforcement in ShieldWall arrangement |
| `ArrangementOrder.GetShieldDirectionOfUnit` | **Postfix** | Rank-aware shield direction (front: DefendDown, edge: DefendLeft/Right, inner: AttackEnd) |
| `HumanAIComponent.ParallelUpdateFormationMovement` | **Postfix** | Slot lock via `ShouldCatchUpWithFormation` + `SetFormationIntegrityData` |

And from our own PSW codebase (already proven):
| `SandboxAgentApplyDamageModel.CanWeaponIgnoreFriendlyFireChecks` | Prefix | Bypass friendly fire for in-wall stabs |
| `Mission.MeleeHitCallback` | Prefix | Clear shield flags for stabs through allies |
| `MissionCombatMechanicsHelper.DecideWeaponCollisionReaction` | Postfix | Override Bounced → ContinueChecking |

---

## Key Insight: "Force Thrust Only" — No Clean API Exists

RBM does not force thrust animations. Instead, it constrains per-agent positions so agents have no lateral swing clearance. The engine then naturally produces thrusts. This is the correct approach for our MVP:

- During Locked state, `GetOrderPositionOfUnit` prefix returns `unit.GetWorldPosition()` (hold in place)
- Agents have enemies directly in front at 1-2m distance
- With allies to left and right and no movement room, the engine selects thrusts/overhand attacks

We do not need to force `SetActionChannel` for attack direction — the spatial constraint does it emergently.
