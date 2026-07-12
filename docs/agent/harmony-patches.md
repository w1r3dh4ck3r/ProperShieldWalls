# Editing anything in `Patches/`

All of this was paid for in bugs. None of it is style advice.

## The `[MBCallback]` rule — this one folds characters into spikes

**Never Harmony-patch a method carrying `[MBCallback]` unless it is already proven safe.** These are native C++
callbacks invoked with `ref` params. Patching `Agent.OnAIInputSet` folded every character into a vertical spike
("meat bullet") **merely by being installed** — the postfix body never ran. No exception, no log line, empty
`rgl_log_errors`.

- If a `[MBCallback]` wrapper delegates to a **plain static helper**, patch the helper instead. That is exactly
  why `FriendlyBlockPassthroughPatch` targets the static
  `MissionCombatMechanicsHelper.GetDefendCollisionResults` and NOT `Mission.GetDefendCollisionResults`.
  Watch the signature: the helper carries an extra `ref bool chamber` the wrapper lacks.
- If you need to influence AI input, ride the sanctioned extension point: subclass `AgentComponent` and override
  `OnAIInputSet` (see `Behaviours/AttackGateComponent.cs`), mutating the `ref movementFlag` the engine reads back
  **this tick**. Do not round-trip through `Agent.MovementFlags`.
- `Mission.MeleeHitCallback` is ALSO `[MBCallback]`. We patch it and it has not folded anything — but that is an
  observation, not a guarantee. If hit reactions misbehave, suspect it first.

## Prefix return value

**A prefix on `Mission.MeleeHitCallback` must `return true`. Never `return false`.**
Returning false suppresses OTHER mods' prefixes on the same method — RBMCombat, RBMAI, RealisticCombatSounds and
XorberaxLegacy all patch it — and skips the method's trailing sound-alarm block. The method returns `void`; its
outcome travels through `ref` params, so you influence it by writing `ref colReaction`, not by returning.

## Priorities are load-order-independent — use them

Conflicts are resolved with explicit `[HarmonyPriority]`, never by a launcher load-order slot.
Both prefixes and postfixes sort **DESCENDING** by priority (verified in `0Harmony.dll`'s `PatchSorter`).

- `WindupTransparencyPatch`: `Priority.High` (600) — sorts ahead of RBMCombat's prefix (Normal=400), which
  rewrites `collisionData` for `CollidedWithShieldOnBack`.
- `FriendlyBlockPassthroughPatch`: `Priority.Last` — `UnblockableThrust`, `RealisticCombatAdjustments` and
  `StaminaSystemFork` all postfix the same static. We need the final write.

## Two independent attack-stop paths, not one

A friendly melee contact halts an attack through **two** native callbacks. Patching one leaves the other running;
the symptom is the weapon passing through the ally while the character's **hand** freezes on his shield.

| Path | Callback | Mechanism |
|---|---|---|
| A | `Mission.MeleeHitCallback` | friendly-fire stun, `Bounced`, `Staggered` — all inside `if (colReaction != ContinueChecking)` (Mission.cs:5305) |
| B | static `MissionCombatMechanicsHelper.GetDefendCollisionResults` | `attackerStunPeriod` + `crushedThrough`, whenever native calls the collision `Blocked`/`Parried`/`ChamberBlocked` |

Path B fires whenever an ally holds a shield up — ubiquitous in a shield wall — and never enters path A's block.

## Never let a per-tick catch block log unthrottled

These run on every melee collision / every AI decision tick. Use `SubModule.LogErrorThrottled`, keyed on
`"<PatchName>:<ExceptionType>"` (NOT `ex.Message`), or a repeating fault logs forever and allocates per collision.

## Verify before you assert

Do not "fix" a guard by reasoning about decompiled code. A dead-code argument once proved `IsColliderAgent` could
be false for shield hits; the battle log then showed `collider=1` on **every** friendly hit, shields included. The
"fix" would have loosened a guard that was already correct, and would have looked like success.
**Instrument first, read the log, then change the guard.**
