# ProperShieldWalls v2 — Cramped-Melee Design

**Date:** 2026-07-09
**Game version:** Bannerlord v1.4.6
**Module Id:** `ProperShieldWalls` (unchanged)
**Repo:** `~/AI/projects/ProperShieldWalls` (`git@github.com:w1r3dh4ck3r/ProperShieldWalls.git`)
**Status:** design approved, not yet implemented

---

## 1. Problem

In a packed melee — a shield wall, a breach, a press — a soldier's swing does not reach the
enemy. It stops on a friendly. Three separate penalties fire at once, all of them in
`Mission.MeleeHitCallback`:

1. Damage is zeroed (correct, keep).
2. The attacker takes a dedicated friendly-fire stun (`StunPeriodAttackerFriendlyFire`).
3. The weapon reaction resolves to `MeleeCollisionReaction.Bounced` — **the stuck swing**.

The felt bug is that a swing is "eaten" during its **wind-up**, before the blade ever enters
the strike arc. An overhead pulls back behind the head and clips the man behind you; a thrust
retracts and clangs off a slung shield.

Community diagnosis (TaleWorlds forum thread 416945, April 2020) matches: weapon collision is
live too early in the animation. That thread could not be read directly (Cloudflare 403 on
every route, no archive snapshot exists) — the diagnosis is corroborated, not quoted.

## 2. Prior art, and why it does not apply

| Mod | What it actually does | Why it is not the answer |
|---|---|---|
| Nexus #495 (Loulou, 2020) | edits `Native/ModuleData/monsters.xml` `body_capsule` radius | dead since 2020-04-28, unresolved startup/save crash reports |
| Nexus #8392 (Oscillator, 2025) | same `body_capsule` radius edit | author-acknowledged "telekinesis bubble" push; ignores weapon reach entirely |

Stock `<Monster id="human">` has `body_capsule radius="0.37"`. Both mods enlarge it.

**We do not touch `monsters.xml`.** Enlarging collision capsules spaces units apart without
addressing friendly weapon-blocking, and its push artifact is inherent, not a bug the author
failed to fix. Multiple #8392 users report it incidentally suppresses RBM's pommel-hit spam,
which confirms the real underlying problem: AI stands inside its own weapon's minimum range.

## 3. Scope

**In scope (this spec):**
- **F1 — Windup transparency.** A friendly hit landing during the wind-up costs nothing:
  no stun, no bounce, no shield clang, no blow. The sweep continues.
- **F2 — Cramped-space attack gating.** An agent that is crowded remaps a horizontal swing to
  an overhead. Applies to player and AI alike.

**Explicitly out of scope**, deferred to `AIKickNBashFork` (already live at load-order slot 73,
already does weapon-length-keyed kick and shield-bash):
- weapon-reach-aware AI spacing
- back off / recover stamina / re-engage loop
- bull-rush and defensive stances

**Deleted from this repo** (recoverable in git history, HEAD `bd04fd0`): the unvalidated
"othismos" shield-wall shoving system — `SlotLockPatch`, `AgentAIPatch`, `RegisterBlowPatch`,
`FriendlyFireCheckPatch`, `ShieldDamagePatch`, `OthismosState`, `StaminaReader`. Five of the
seven existing Harmony patches. That work never built after its last commit and was never
validated in-game.

## 4. Verified mechanism (1.4.6 decompile)

Decompiled with `~/.dotnet/tools/ilspycmd` (WSL-native — the Windows path recorded in the
AIBrain wiki does not exist on this machine).

### 4.1 The friendly-block path

`Mission.MeleeHitCallback` (Mission.cs:5297) returns **`void`**. Its outcome travels through
`ref` params, not a return value:

```csharp
internal void MeleeHitCallback(ref AttackCollisionData collisionData, Agent attacker, Agent victim,
    GameEntity realHitEntity, ref float inOutMomentumRemaining, ref MeleeCollisionReaction colReaction,
    CrushThroughState crushThroughState, Vec3 blowDir, Vec3 swingDir,
    ref HitParticleResultData hitParticleResultData, bool crushedThroughWithoutAgentCollision)
```

`Mission.CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase` (Mission.cs:6291) always returns
true for a friendly hit in singleplayer (`!GameNetwork.IsSessionActive` alone suffices; an
AI attacker can never friendly-fire in any mode). It sets `AttackerStunPeriod` and zeroes
damage. Then `MissionCombatMechanicsHelper.DecideWeaponCollisionReaction` (line ~118):

```csharp
if (!collisionData.IsColliderAgent || registeredBlow.InflictedDamage <= 0)
{ colReaction = MeleeCollisionReaction.Bounced; return; }
```

Zero damage ⇒ `Bounced`. That is the stuck swing, and it is decided in **managed** code.

`MeleeCollisionReaction` = `Invalid=-1, SlicedThrough, ContinueChecking, Stuck, Bounced, Staggered`.

### 4.2 The windup signal

`AttackCollisionData` exposes, as public getters (no reflection):

```csharp
public CombatHitResultFlags CollisionHitResultFlags { get; private set; }
public float AttackProgress { get; }
public bool  IsColliderAgent => _isColliderAgent;
```

```csharp
[Flags] public enum CombatHitResultFlags : byte
{ NormalHit = 0, HitWithStartOfTheAnimation = 1, HitWithArm = 2, HitWithBackOfTheWeapon = 4 }
```

**Constraint.** The engine's only managed consumer of `HitWithStartOfTheAnimation` gates on
`StrikeType == 1` (Thrust):

```csharp
if (collisionData.IsColliderAgent && collisionData.StrikeType == 1
    && collisionData.CollisionHitResultFlags.HasAnyFlag(CombatHitResultFlags.HitWithStartOfTheAnimation))
{ colReaction = MeleeCollisionReaction.Staggered; return; }
```

Whether native sets the flag on *swings* is not knowable from managed code. The windup test is
therefore a union of the flag and an `AttackProgress` threshold. The threshold's default of
`0.25` is a prior, not a measurement: the nearby `0.22` figure is the lower bound of the
engine's own sweet-spot window in `DecideSweetSpotCollision` (`0.22 ≤ AttackProgress ≤ 0.55`).

### 4.3 The attack-direction lever

`Agent.AttackDirection` / `GetAttackDirection()` are read-only native wrappers — a dead end.
The live lever is `Agent.MovementFlags`, a **public get/set** property whose
`Agent.MovementControlFlag` bits *are* the directions:

```
AttackLeft=0x40  AttackRight=0x80  AttackUp=0x100  AttackDown=0x200  AttackMask=0x3C0
```

- **Player:** `MissionMainAgentController.ControlTick()` (private, in
  `TaleWorlds.MountAndBlade.View.dll`) ends with
  `mainAgent.MovementFlags |= mainAgent.AttackDirectionToMovementFlag(mainAgent.GetAttackDirection())`.
  Postfix it. `Bannerlord.FluidCombatNext` ships exactly this patch.
- **AI:** `Agent.OnAIInputSet` (internal, `[MBCallback]`), reachable via
  `BindingFlags.NonPublic`. Fires at the AI decision-tick rate, not per render frame.
  `AIKickNBashFork` already postfixes it and writes `MovementFlags` — in-game validated.

There is **no melee-attack-start event** on `MissionBehavior` (all 40 virtuals were enumerated;
combat ones fire at hit time). These two tick hooks are the only pre-commit surface.

### 4.4 Cost table

| Member | Cost |
|---|---|
| `Agent.Index`, `Agent.Team` | free (managed auto-property) |
| `Mission.CurrentTime` | free (`=> _cachedMissionTime`) |
| `Agent.WieldedWeapon.CurrentUsageItem.*` | free (managed struct + list index) |
| `Agent.MovementFlags` get/set | native passthrough |
| `Agent.IsFriendOf(Agent)` | **native call** (`MBAPI.IMBAgent.IsFriend`) |

## 5. Architecture

```
Mission.MeleeHitCallback ──Prefix──> WindupTransparencyPatch
                                          │ friendly + windup?
                                          │   ├─ colReaction = ContinueChecking
                                          │   ├─ return false  (skip stun, blow,
                                          │   │                 shield dmg, particles)
                                          │   └─ CrowdState.Stamp(attacker.Index)
                                          ▼
                                     CrowdState        float[] by Agent.Index
                                          ▲            "crowded until T"
                    ┌─────────────────────┴─────────────────────┐
MissionMainAgentController.ControlTick        Agent.OnAIInputSet
        ──Postfix──> PlayerAttackGate          ──Postfix──> AiAttackGate
```

The crowding signal is **free** because F1 already detects, as a side effect of doing its job,
the exact event that defines crowding: your windup clipped a friendly. No spatial query is ever
performed.

### Files

| File | Responsibility |
|---|---|
| `SubModule.cs` | Harmony registration, `OnMissionBehaviorInitialize` |
| `CrowdState.cs` | growable `float[]` by `Agent.Index`; `Stamp` / `IsCrowded` |
| `AttackRemap.cs` | pure function, unit-tested |
| `Patches/WindupTransparencyPatch.cs` | Prefix on `Mission.MeleeHitCallback` |
| `Patches/AttackGatePatches.cs` | the two postfixes |
| `Settings.cs` | MCM |

### 5.1 Windup transparency

```csharp
if (attacker == null || victim == null) return true;   // world hit -> vanilla
if (!collisionData.IsColliderAgent)     return true;
if (attacker == victim)                 return true;   // self-hit -> vanilla
if (!victim.IsHuman)                    return true;   // mounts -> vanilla
if (attacker.Team != victim.Team && !attacker.IsFriendOf(victim)) return true;
if (!IsWindup(in collisionData))        return true;   // live strike arc -> vanilla

colReaction = MeleeCollisionReaction.ContinueChecking;
CrowdState.Stamp(attacker.Index);
return false;
```

```csharp
static bool IsWindup(in AttackCollisionData d) =>
    d.CollisionHitResultFlags.HasAnyFlag(CombatHitResultFlags.HitWithStartOfTheAnimation)
    || d.AttackProgress < Settings.WindupThreshold;   // default 0.25
```

The `Team` compare precedes `IsFriendOf` because the common case (same team) short-circuits
before touching native. `ref inOutMomentumRemaining` is deliberately untouched — the swing keeps
its momentum through the ally. `ContinueChecking` (not `SlicedThrough`) lets the sweep go on to
reach an enemy standing behind the ally.

`victim.IsHuman` excludes mounts: windup against your own horse keeps vanilla behavior.

### 5.2 Attack remap

```csharp
var f = agent.MovementFlags;
if ((f & AttackMask) == 0) return;                     // not attacking
if ((f & (AttackLeft | AttackRight)) == 0) return;     // already overhead/thrust
if (!CanSwing(agent.WieldedWeapon.CurrentUsageItem)) return;
if (!CrowdState.IsCrowded(agent.Index, Mission.Current.CurrentTime)) return;
agent.MovementFlags = (f & ~AttackMask) | AttackUp;
```

```csharp
static bool CanSwing(WeaponComponentData w) =>
    w != null && w.IsMeleeWeapon
    && w.SwingDamageType != DamageTypes.Invalid && w.SwingSpeed > 0;
```

The `CanSwing` guard is a **correctness** requirement, not a feature: setting `AttackUp` on a
thrust-only weapon (pike) would produce the dead input that the "veto" option was rejected for.

## 6. Open questions — resolve during implementation, do not assume

1. **`AttackUp` == overhead, `AttackDown` == thrust?** Conventional reading, matches the design
   intent, but **no decompiled code proves it**. Verify in-game in thirty seconds. If inverted,
   the fix is one constant.
2. **`CanSwing` data shape.** Vanilla ships no `LowGripPolearm` item and no item id `pike` —
   pikes come from mods (`BetterPikes`, load-order slot 66). Confirm at runtime that a pike's
   `SwingDamageType` is `Invalid` and/or `SwingSpeed == 0`. Log it; do not assume.
3. **`WindupThreshold` default.** Instrument first: log `(StrikeType, CollisionHitResultFlags,
   AttackProgress)` on every friendly hit for one battle, then tune from the distribution.
   Specifically determine whether native sets `HitWithStartOfTheAnimation` on swings at all.

## 7. Harmony conflicts (both confirmed by reading the shipped DLLs)

| Method | Other patcher | Resolution |
|---|---|---|
| `Agent.OnAIInputSet` | `AIKickNBashFork` postfix clears `AttackMask\|DefendMask`, sets Kick | ours runs **first** (high priority) so a kick decision overwrites our remap; we also bail early if the Kick bit is set |
| `MissionMainAgentController.ControlTick` | `FluidCombatNextNext` postfix ORs its own direction | ours runs **last** (low priority) to get the final write to `MovementFlags` |

Harmony sorts patches of the same kind by priority descending (higher runs first). This is
standard Harmony behavior, but because the whole conflict resolution rests on it, confirm it
empirically with a log line from each postfix on first build — do not take it on faith.

Module load order does **not** resolve these conflicts; explicit `[HarmonyPriority]` does.
Load order only breaks ties between equal priorities.

Other mods referencing `MeleeHitCallback` (string-match on shipped DLLs, not necessarily
patching it): `RBMFork/RBMAI.dll`, `RBMFork/RBMCombat.dll`, `RealisticCombatSounds.dll`,
`XorberaxLegacy.dll`. `RBMCombat` was decompiled and patches damage math
(`CreateMeleeBlow`, `GetAttackCollisionResults`, `RegisterBlow`) — **not** direction, and not
`MeleeHitCallback` itself. `UnblockableThrust` postfixes
`MissionCombatMechanicsHelper.GetDefendCollisionResults`, a different method. No collision.

## 8. Accepted defects

**Index recycling.** `Agent.Index` is reused when agents die and respawn. A new agent inheriting
a live stamp prefers overheads for under two seconds. A generation counter would fix it; the
artifact is invisible in play and the code is not worth it.

**Reactive lag.** The first wide swing in a press still starts before gating engages. It passes
through the friendly harmlessly (that is F1's whole job), so the cost is one cosmetically wide
swing. If in-game play shows this is too slow, the documented zero-cost upgrade is
`Formation.QuerySystem.EstimatedIntervalReadOnly` — a live, cached (0.2 s), formation-shared
measure of actual unit spacing, amortized across the formation, free to read per agent.
Deliberately unused for now (YAGNI).

## 9. Settings (MCM)

| Setting | Default | Purpose |
|---|---|---|
| `Enabled` | true | master kill switch |
| `WindupTransparency` | true | F1 |
| `CrampedAttackGating` | true | F2 |
| `WindupThreshold` | 0.25 | `AttackProgress` below this counts as windup |
| `CrowdedDuration` | 2.0 s | stamp lifetime |
| `DiagnosticLogging` | false | dumps the triple from §6.3 |

Per `reference_mcm_settings_file_generation`: once MCM writes the settings JSON, changing a
default in `Settings.cs` alone does nothing. Delete `Configs/ModSettings/Global/ProperShieldWalls/`
when changing a default during development.

## 10. Testing

**Unit (xUnit, net8.0)** — mirrors `AIKickNBashFork`'s `ActionSelector` test shape:
- `AttackRemap.Decide(flags, canSwing, isCrowded) -> flags` across every branch
- `CrowdState` stamp/expiry/growth, including index recycling

**In-game (the real gate).** Deployed ≠ complete.
1. Custom battle, two infantry lines, shield wall, 200 a side.
2. Baseline vs patched: does a swing from inside the second rank reach an enemy?
3. Confirm no friendly-fire stun when a windup clips an ally.
4. Confirm horizontal swings become overheads in the press, and revert once free.
5. Confirm a pike-armed unit (`BetterPikes`) still thrusts and is never remapped.
6. Confirm `AIKickNBashFork` kicks still fire (priority resolution works).
7. Siege, 1000+ agents: frame time unchanged vs baseline.

## 11. Deployment

`SubModule.xml` keeps `Id="ProperShieldWalls"`, bumps to `v2.0.0`. The module is **not currently
installed** — adding it to the live load order is a `/bannerlord-add-mod` task. Place it after
`RBMFork` (slot 27), which patches the surrounding damage math. Its position relative to
`AIKickNBashFork` (73) and `FluidCombatNextNext` (69) is irrelevant: §7's conflicts are resolved
by `[HarmonyPriority]`, not by load order.

Run `/bannerlord-backup` before touching `LauncherData.xml`, and edit the **live** Windows file
(`/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/Configs/LauncherData.xml`), never
the repo copy.
