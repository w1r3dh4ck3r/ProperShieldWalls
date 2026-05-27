# Risk Register — Proper Shield Walls (Othismos)

**Date:** 2026-05-27  
**Status:** Updated 2026-05-27 — R1 resolved, R2 resolved, R5 confirmed as low.

---

## R1 — Agent AI Decision Intercept (RESOLVED — 2026-05-27)

**STATUS: RESOLVED.** RBM source code confirmed the correct approach and exact patch targets.

**Solution:** `Formation.GetOrderPositionOfUnit` prefix returning `unit.GetWorldPosition()` holds agents in slot while keeping them in an attack-eligible state. Spatial constraint (enemy 1-2m ahead, allies left/right) produces thrusts naturally — no animation forcing needed.

**Full patch target set:**
1. `Formation.GetOrderPositionOfUnit` prefix — return `unit.GetWorldPosition()` for Locked agents
2. `HumanAIComponent.ParallelUpdateFormationMovement` postfix — `SetFormationIntegrityData` slot lock
3. `Agent.UpdateFormationOrders` prefix — `EnforceShieldUsage` per tick

**Remaining risk (LOW):** Method names confirmed in RBM vs v1.3.x. Verify `HumanAIComponent.ShouldCatchUpWithFormation` still exists in installed version via ILSpy before first build.

---

## R2 — Formation Anchor API (RESOLVED — 2026-05-27)

**STATUS: RESOLVED.** Formation position write API confirmed from RBM and BUTR API docs.

**Solution:** `formation.SetMovementOrder(MovementOrder.MovementOrderMove(worldPosition))` translates the formation anchor. Combined with `formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(dir))` to maintain facing.

**For pressure resolver:** Compute new `WorldPosition` = current median + delta along engagement axis; call `SetMovementOrder` for both formations each tick.

**Remaining risk (LOW):** `MovementOrder.MovementOrderMove` requires a `WorldPosition` with valid NavMesh position. If the computed position is off-navmesh, the formation may not move. Mitigation: clamp position delta to a small value (0.05m max) to stay near the current valid position.

---

## R3 — Agents Clipping Through Each Other (MEDIUM)

**Risk:** When two formations are locked, front ranks are at ~2m separation. The physics engine may push agents through each other or generate excessive collision bounce.

**Failure modes:**
- Agents visually inside each other (clipping)
- Physics engine pushes formations apart faster than our pressure resolver pushes them together → oscillation
- Formations end up on wrong sides of each other (passed through)

**Mitigation plan:**
- Add a minimum separation clamp: never let the two formation anchors get closer than 1.0m regardless of pressure delta
- If clipping is severe: increase the engagement initiation distance from ~2m to ~3m so there's always a safe gap
- Physics collision between agents in vanilla is handled by the engine; we don't need to solve it, just avoid fighting it

---

## R4 — Pressure Formula Instability (MEDIUM)

**Risk:** The pressure resolver runs each tick and physically moves formations. If the delta is too large per tick, formations will visibly teleport rather than slide. If the delta is too small, the shoving contest is invisible.

**Failure modes:**
- One side instantly wins because a tiny pressure advantage compounds each tick
- Formations oscillate (pressure advantage switches rapidly)
- Numerical instability from large dt values during frame drops

**Mitigation plan:**
- Cap pressure delta per tick at 0.05m (regardless of the computed value)
- Use a rolling average of the last 3 ticks' pressure rather than instantaneous value
- Test with deliberately unbalanced forces (10 vs 5 agents) to ensure the winner advances visibly but not instantly

---

## R5 — Stamina Mod Coupling (MEDIUM)

**Risk:** The StaminaSystem mod may not expose a public drain API. We'd be hooking into a third-party mod with no guaranteed API stability.

**Failure modes:**
- StaminaSystem's internal structure changes in an update, breaking our integration silently
- Name collision or reflection access fails
- Mod load order issues if we declare StaminaSystem as a dependency

**Mitigation plan (already in design brief):** Use our own simple stamina float per formation. This is explicitly permitted as an MVP cheat. The hook into StaminaSystem is a v1.1 improvement after MVP proves the concept.

---

## R6 — Animation Jitter / T-Poses (MEDIUM)

**Risk:** Forcing agents into a specific action (stab animation) each tick via `SetActionChannel` may cause visible jitter if the AI immediately overrides it.

**Failure modes:**
- Agents visibly flickering between stab and idle animations
- T-pose (animation transition failure) on some agents
- `SetActionChannel` with wrong `startProgress` causing animation loop-back

**Mitigation plan:**
- Only call `SetActionChannel` when the agent's current action has changed away from the expected action (not every tick)
- Use the action restore pattern from PSW old code: check if `currentAction != expectedAction`, then restore
- If jitter persists: investigate whether the native animation system has a "lock action" API or whether we need to suppress the agent's AI more aggressively

---

## R7 — Engagement Detection False Positives (LOW-MEDIUM)

**Risk:** The ShieldWall+ShieldWall+proximity engagement trigger may fire incorrectly — two formations marching past each other, cavalry from a third formation, etc.

**Failure modes:**
- Non-opposing formations get locked (wrong teams, wrong angles)
- Formation facing angle check is too loose, triggering sideways contact
- Re-triggering during cooldown after a lock breaks

**Mitigation plan:**
- Team check: only engage opposing teams (already implicit in two-formation design)
- Facing check: dot product of formation facing vectors must be < -0.7 (roughly facing each other) to engage
- Cooldown: 3 second cooldown after Locked state exits before re-evaluation
- Formation size check: require at least 3 agents per formation (prevents edge cases with tiny remnants)

---

## R8 — Compatibility with RBM (LOW-MEDIUM)

**Risk:** RBM (Realistic Battle Mod) is in the active modlist and also patches `MissionCombatMechanicsHelper` and agent combat logic. Overlapping patches can cause incorrect behavior or crashes.

**Failure modes:**
- Our `Priority.High` on `MeleeHitCallback` runs before RBM but RBM's postfix undoes our changes
- RBM's agent AI patches conflict with ours, causing null refs or wrong state
- Execution order causes combat mechanics to be applied twice

**Mitigation plan:**
- Test with RBM enabled (it's in the modlist)
- Use `Priority.High` for our combat intercept patches to run first
- Ensure our patches return early (don't call original) only for agents in Locked state — RBM handles all other cases normally

---

## R9 — Single-Agent Test Failure (HIGH — early warning)

**Risk:** The day-1 stab-in-slot test fails and we learn the agent AI cannot be controlled at the level we need.

**If this happens:** The design must shift to **formation-level override** rather than agent-level. Instead of patching agent AI decisions, we issue formation orders each tick that effectively move the "target slot" of each agent to a fixed point. The agent then handles its own micro-attacks within that fixed slot. This is less precise but avoids the agent AI entirely.

**Fallback design impact:** Stab animations might be vanilla (not forced), press-in-place mechanic still works via slot locking. Attack rate becomes emergent rather than forced.

---

## Risk Summary Table

| ID | Risk | Likelihood | Impact | Status |
|----|------|-----------|--------|--------|
| R1 | Agent AI intercept | ~~High~~ Resolved | Critical | **RESOLVED** — confirmed from RBM source |
| R2 | Formation anchor API | ~~Medium~~ Resolved | High | **RESOLVED** — `SetMovementOrder` confirmed |
| R3 | Clipping | Medium | Medium | Clamped position bounds |
| R4 | Pressure instability | Medium | Medium | Cap delta per tick |
| R5 | Stamina mod coupling | Low | Low | MVP uses own tracking |
| R6 | Animation jitter | Medium | Medium | Conditional restore only |
| R7 | False engagement | Low | Low | Facing + cooldown checks |
| R8 | RBM compatibility | Low | Medium | Test with RBM on |
| R9 | Day-1 gate fail | Medium | High | Have formation-level fallback |
