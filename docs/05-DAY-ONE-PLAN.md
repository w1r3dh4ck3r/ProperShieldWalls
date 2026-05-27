# Day-One Implementation Plan — Single-Agent Stab-in-Slot POC

**Date:** 2026-05-27  
**Prerequisite:** External mod analysis report (doc 02) must be complete and the Harmony patch target for agent AI confirmed before starting this plan.

**Goal:** Prove that a single AI agent can be placed in a fixed slot, facing an enemy, and made to repeatedly stab without leaving the slot. Success means we can build the full MVP. Failure requires a design pivot (documented in R9 of the risk register).

---

## Step 0 — Before Writing Any Code (SIMPLIFIED — research resolved the unknowns)

**Update 2026-05-27:** External mod analysis confirmed all Harmony targets from RBM source. Steps 0a and 0b are already resolved. Step 0c (action index) is no longer needed — the correct approach is spatial constraint, not forced animation.

### 0a. ~~Verify Formation.ArrangementOrder API~~ — DONE
Confirmed from RBM Frontline.cs:442:
```csharp
formation.ArrangementOrder == ArrangementOrder.ArrangementOrderShieldWall  // struct equality
// OR
formation.ArrangementOrder.OrderEnum == ArrangementOrderEnum.ShieldWall    // enum comparison
```

### 0b. ~~Find Agent AI Decision Method~~ — DONE
Confirmed from RBM. The three patch targets:
1. `Formation.GetOrderPositionOfUnit` prefix — primary position control (returns `unit.GetWorldPosition()` to hold in place)
2. `HumanAIComponent.ParallelUpdateFormationMovement` postfix — slot enforcement via `SetFormationIntegrityData`
3. `Agent.UpdateFormationOrders` prefix — shield enforcement via `EnforceShieldUsage`

### 0c. ~~Locate stab action index~~ — NOT NEEDED
Spatial constraint (returning `unit.GetWorldPosition()` from `GetOrderPositionOfUnit`) produces attacks-in-place without forcing animation state. Engine selects thrusts naturally.

### What remains in Step 0: check for v1.3.14 vs v1.3.15 API drift
- Check if game is on v1.3.14 or v1.3.15 (check installer or file version)
- Confirm `HumanAIComponent.ShouldCatchUpWithFormation` property still exists (reflection-based in RBM — could be renamed)
- Confirm `HumanAIComponent.ParallelUpdateFormationMovement` method name (could be `ParallelUpdateFormationMovement` or similar)
- Use ILSpy CLI to confirm: `ilspycmd.exe TaleWorlds.MountAndBlade.dll -t HumanAIComponent`

**[STEP 0] → verify: `ilspycmd` output shows `HumanAIComponent` has both `ParallelUpdateFormationMovement` and `ShouldCatchUpWithFormation`**

---

## Step 1 — Create the Test Behavior

Create `OthismosTestBehaviour.cs` (a temporary file, to be deleted after POC):

```csharp
// TEST ONLY — proves stab-in-slot concept before building full MVP
internal class OthismosTestBehaviour : MissionBehavior
{
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    private Agent _lockedAgent;         // the agent we're controlling
    private Vec3 _slotPosition;         // world-space position it must hold
    private ActionIndexCache _stabAction;
    private bool _initialized;

    public override void AfterStart()
    {
        // Find first enemy infantry agent
        // Lock it to its spawn position
        // Record a stab action index to force
    }

    public override void OnMissionTick(float dt)
    {
        // Every tick: if agent left slot, move it back
        // If agent's current action is not _stabAction and it has a target, force the stab
        // Log what's happening so we can see the behavior
    }
}
```

**[STEP 1] → verify: Behavior compiles and is added to mission via SubModule.OnMissionBehaviorInitialize**

---

## Step 2 — Lock One Agent to a Slot

In `AfterStart()`:
1. Find a player-team infantry agent who has a shield and a one-handed weapon
2. Record `_slotPosition = agent.Position`
3. Set `_lockedAgent = agent`
4. Log: `[PSW TEST] Locked agent {agent.Name} at {_slotPosition}`

In `OnMissionTick()`:
1. If `_lockedAgent == null || !_lockedAgent.IsActive()` → return
2. Compute `distFromSlot = (_lockedAgent.Position - _slotPosition).Length`
3. If `distFromSlot > 1.5f` → teleport agent back: `_lockedAgent.TeleportToPosition(_slotPosition)` [⚠️ confirm API exists]
4. Log slot distance every 3 seconds

**[STEP 2] → verify: `Debug.Print` output shows agent holding within 1.5m of spawn position even when an enemy is 2m away**

---

## Step 3 — Verify Attack in Place (No Animation Forcing)

**Update:** No action forcing needed. The spatial constraint via `GetOrderPositionOfUnit` returning `unit.GetWorldPosition()` produces in-place attacks naturally. Step 3 is now about verifying the engine selects attacks (not idles) when slot is fixed with enemy in range.

In `OnMissionTick()`:
1. Log `_lockedAgent.GetCurrentActionType(0)` every 3 seconds — should be in `AttackForward`, `ReadyMelee`, or similar combat states
2. Log `_lockedAgent.Health` to confirm enemy HP is dropping (attacks are landing)
3. If agent is idle for > 5 seconds with enemy in range: something is wrong — log and flag

**[STEP 3] → verify: Log shows agent current action cycling through melee combat states, not idle. Enemy HP decreasing.**

---

## Step 4 — Verify No Formation Break

The agent must not leave its assigned formation slot regardless of stab animation.

1. Confirm `_lockedAgent.Formation` doesn't change during the test
2. Confirm no other agents from the same formation break out due to our manipulation
3. Log `agent.Formation?.Index` each tick

**[STEP 4] → verify: Formation index stays constant in log; agent remains in formation; other agents in same formation unaffected**

---

## Step 5 — Enemy Agent as Well

Repeat the lock+stab procedure for one enemy agent (team index 1):
1. Find one enemy infantry agent
2. Lock them at their spawn
3. Force stab toward the player-team agent

With both agents locked in place ~2m apart, both stabbing:
- Do they attack each other? (AttackProgress registering)
- Are hits landing? (HP changing)
- Is anything crashing?

**[STEP 5] → verify: Both agents stab repeatedly, HP changes on both sides, no crash after 60 seconds of combat**

---

## Step 6 — Evaluate Results

### Success criteria (all must be true):
- Agent stays within 1.5m of slot for entire 60-second test
- Agent performs stab action at least once per 3 seconds (not idle-locked)
- Agent's HP changes (attacks are landing — not just animations with no collision)
- No crash
- No other agents in the mission are visibly affected

### Expected log output at success:
```
[PSW TEST] Locked agent Infantry at (x, y, z)
[PSW TEST] Stab action: 42 (act_strike_middle_thrust)  [index number will vary]
[PSW TEST] Agent slot distance: 0.3m  [every 3 seconds]
[PSW TEST] Agent slot distance: 0.4m
[PSW TEST] Forced stab action  [once per attack cycle]
[PSW TEST] Agent HP: 95/100  [enemy taking damage]
```

### If test fails (and why):

**Failure A: Agent slot distance grows > 5m persistently**
Cause: Teleport API not working, or agent is overriding our position each tick.
Fix: Check if `TeleportToPosition` exists; if not, try `agent.SetMovementOrder(...)`.

**Failure B: Agent action never shows stab (stays at idle)**
Cause: `SetActionChannel` being overridden by agent AI immediately.
Fix: Investigate `HumanAIComponent` patch — we need to suppress the AI entirely for locked agents.

**Failure C: Agent stabs but leaves slot**
Cause: Stab animation has built-in movement (lunge). The animation itself moves the agent.
Fix: Shorten the stab animation by forcing `startProgress` to 0.3f (mid-thrust, past the lunge).

**Failure D: Agent idles after first stab, never attacks again**
Cause: After `SetActionChannel` forces a stab, the AI state machine sees "already attacking" and waits for a cooldown that never resolves.
Fix: Track `lastStabTime`; only force stab if `Time.Now - lastStabTime > 1.0f`.

**Failure E: Crash on `SetActionChannel` or `TeleportToPosition`**
Cause: API signature wrong or agent state invalid.
Fix: Check ILSpy for actual signature. Wrap in try/catch and log.

---

## Step 7 — After Successful POC

If the test succeeds, document:
1. The exact Harmony patch target for suppressing formation-break (from external research)
2. The exact action index(es) used for stab
3. Whether `TeleportToPosition` was stable or required an alternative

Then delete `OthismosTestBehaviour.cs` and begin the full MVP implementation plan.

---

## Estimated Day-One Timeline

| Step | Expected time | Gate? |
|------|--------------|-------|
| 0: API verification via ILSpy | 2h | YES — don't skip |
| 1: Test behavior skeleton | 30m | — |
| 2: Slot locking | 30m | — |
| 3: Force stab | 1h | — |
| 4: Formation break check | 30m | — |
| 5: Two-agent test | 30m | — |
| 6: Evaluation + log review | 30m | YES — gate to MVP |

Total: ~5.5h. If steps 0+6 succeed, we're unblocked for the full MVP.
