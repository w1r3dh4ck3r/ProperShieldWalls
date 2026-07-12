# Shield Rotation — Design Spec

**Date:** 2026-07-12
**Branch:** `feat/cramped-melee-v2` (still unmerged) → new work branches from it
**Status:** Design approved by Mark, not yet implemented

---

## 1. The problem, in Mark's words

> "If I have 100 men in a shield wall and the front rank loses their shields to hits or javs or anything,
> they should be shuffled back and a shielded unit should take their place."

And, on generalising it:

> "In any formation for shielded troops — for example in a Square I want the shielded troops outside the
> square and the no-shield inside."

Observed in game: **this never happens.** Front-rank men lose their shields and stand there and die.

---

## 2. Root cause — a vanilla bug, not a mod conflict

Vanilla **already implements** this feature, and it is **structurally dead in exactly the two formations that
need it**. Everything below is quoted from the decompiled shipping `TaleWorlds.MountAndBlade.dll` (v1.4.7).

`LineFormation` rotates shielded men toward the front:

```csharp
protected Func<Agent, bool> _isFrontUnitDelegate;              // LineFormation.cs:69
_isFrontUnitDelegate = PreferShieldedUnitsOnFront;             // LineFormation.cs:227 (ctor)
protected bool PreferShieldedUnitsOnFront(Agent agent) => agent.HasShieldCached;

private void SwitchFrontUnitTypesToFrontRows()                 // LineFormation.cs:2566
{
    if (Interval <= 0f)
        return;                                                // ← THE BUG
    ...  // swaps a shielded man in rank i with a shieldless man in rank i-1, same file
}

public void OnTickOccasionally()                               // LineFormation.cs:2611
{
    SwitchFrontUnitTypesToFrontRows();                         // called unconditionally
    UpdateFrontUnitTypeDelegate();
}
```

`Formation.Tick` drives `Arrangement.OnTickOccasionally()` off a plain 0.5 s timer with **no** combat/movement
gating. So the loop *is* invoked constantly — it just returns on its first line.

Why it always returns:

```csharp
// Formation.cs
public float Interval => InfantryInterval(UnitSpacing) * Arrangement.IntervalMultiplier;   // :520
public static float InfantryInterval(int unitSpacing) => 0.38f * unitSpacing;              // :2856
public const int MinimumUnitSpacing = 0;                                                   // :88

// ArrangementOrder.cs
public static int GetUnitSpacingOf(ArrangementOrderEnum a) => a switch
{
    ArrangementOrderEnum.Loose      => 6,
    ArrangementOrderEnum.ShieldWall => 0,     // ←
    ArrangementOrderEnum.Square     => 0,     // ←
    _                               => 2,
};
```

**ShieldWall ⇒ `UnitSpacing = 0` ⇒ `Interval = 0.38 × 0 = 0` ⇒ the rotation returns on line 1, every tick,
forever.** `IntervalMultiplier` cannot rescue it: anything × 0 is 0, so no arrangement subclass can either.

`Square` maps to `RectilinearSchiltronFormation : SquareFormation : LineFormation` and has the **same spacing 0**,
so it is dead for the same reason. Square also does this:

```csharp
// SquareFormation
protected override void UpdateFrontUnitTypeDelegate() { }      // empty — deliberate
```

i.e. it keeps `PreferShieldedUnitsOnFront` permanently (never flipping to the anti-cavalry `PreferBracerUnitsOnFront`).
TaleWorlds wrote the behaviour, wired it into both formations, then gated it behind a condition **neither can ever
satisfy**. It has never run for anyone.

Line (spacing 2) and Circle (`CircularFormation : LineFormation`, spacing 2) have `Interval = 0.76 > 0`, so vanilla's
rotation **does** work there. We must not touch those.

### Ruled out by verification (do not re-investigate)

- **Mod suppression — RULED OUT.** All 85 enabled mods were scanned (`strings -a` *and* `strings -el`; a .NET UTF-16
  `#US` heap miss on ASCII-only proves nothing). **Zero** reference `SwitchFrontUnitTypesToFrontRows`,
  `OnTickOccasionally`, `_isFrontUnitDelegate`, or `PreferShieldedUnitsOnFront`. `RBMFork` and `FrontlineModFork`
  (both ours) do prefix `LineFormation.SwitchUnitLocations`, but both return `true` for any valid active
  in-formation pair — they only block null/inactive/detached edge cases.
- **Stale shield cache — RULED OUT.** `Agent.HasShieldCached => Equipment.ContainsShield()` (Agent.cs:903) is a
  computed expression-bodied property with **no backing field**. The name is a lie; it is fresh on every read.
- **Detachment guard — NOT in the path.** `Formation.SwitchUnitLocations(Agent, Agent)` *does* check
  `IsDetachedFromFormation`, but vanilla's loop never calls it — it calls `LineFormation`'s own
  `SwitchUnitLocations(IFormationUnit, IFormationUnit)` overload, which has **no** detachment check. We must
  therefore supply our own guard (see §4).

---

## 3. Why one loop gives both behaviours

Rank means different things in the two arrangements, and that difference does exactly what Mark asked for.

```csharp
// SquareFormation.GetLocalPositionOfUnitAux
float num3 = rankIndex * (Distance + UnitDiameter);
case Side.Front: return vec + new Vec2(num2, -num3);   // rank walks INWARD from the front edge
case Side.Rear:  return vec + new Vec2(-num2, +num3);  // inward from the rear edge
case Side.Right: return vec + new Vec2(-num3, -num2);  // inward from the right edge
case Side.Left:  return vec + new Vec2(+num3,  num2);  // inward from the left edge
private int MaxRank => (UnitCountOfOuterSide + 1) / 2; // capped at the square's centre
```

`fileIndex` selects **which side** of the square; `rankIndex` walks **inward from that side**.

| Formation | `rank 0` means | "Shielded to rank 0" therefore means |
|---|---|---|
| ShieldWall (`LineFormation`) | the front rank | shields to the **front**, shieldless to the **rear** |
| Square (`RectilinearSchiltronFormation`) | the outer ring | shields on the **perimeter**, shieldless in the **interior** |

One rule — *shielded men belong at low rank* — produces both. No square-specific code.

---

## 4. The design

**`Behaviours/ShieldRotationBehavior.cs`** — a `MissionBehavior`. **No Harmony patch, no reflection, no private
member access.** This is the first feature in PSW that needs none, and that is a deliberate goal.

Every `RotationInterval` seconds (default **0.5 s**, matching vanilla's cadence), for each `Formation` in the
mission where vanilla's rotation is dead:

**Gate:** `formation.Interval <= 0f`. This is the precise, self-maintaining test for "vanilla bailed" — it is
literally the guard vanilla itself checks. Today it selects exactly ShieldWall and Square; if TaleWorlds ever
changes a spacing, we track it automatically. (Do **not** hard-code the `ArrangementOrderEnum` list.)

**Per formation:**
1. `formation.Arrangement.GetAllUnits()` → for each `Agent`, `agent.GetFormationFileAndRankInfo(out file, out rank)`.
2. **Skip** any agent with `file == -1 || rank == -1` (detached / unpositioned). This is our own detachment guard,
   replacing the one `LineFormation.SwitchUnitLocations` lacks.
3. Bucket agents by `file`.
4. Per file: **stable partition** — shielded men (`agent.HasShieldCached`) to the low ranks, shieldless to the high
   ranks, preserving relative order within each group. Emit the swaps via
   `formation.Arrangement.SwitchUnitLocations(a, b)` — the exact call vanilla's own loop makes.

**Deliberate improvement over vanilla:** vanilla only ever swaps *adjacent* ranks, one pair per 0.5 s tick, so a
shieldless man in rank 3 bubbles rearward over several seconds. A per-file partition replaces a shieldless
front-ranker in **one** tick. That is the "immediate" behaviour Mark asked for.

### Public API used (all confirmed public in the v1.4.7 decompile)
| Member | Type |
|---|---|
| `Formation.Arrangement` | `IFormationArrangement` |
| `Formation.Interval` | `float` |
| `IFormationArrangement.GetAllUnits()` | `MBReadOnlyList<IFormationUnit>` |
| `IFormationArrangement.SwitchUnitLocations(IFormationUnit, IFormationUnit)` | `void` |
| `Agent.GetFormationFileAndRankInfo(out int, out int)` | `void` |
| `Agent.HasShieldCached` | `bool` (computed fresh) |
| `Mission.Teams` | `TeamCollection` |
| `Team.FormationsIncludingEmpty` | `MBList<Formation>` |

Formation enumeration is `Mission.Current.Teams` → `team.FormationsIncludingEmpty` (both verified public in the
v1.4.7 decompile: `Mission.cs:1285`, `Team.cs:34`).

---

## 5. Settings (MCM)

| Setting | Default | Range | Notes |
|---|---|---|---|
| `ShieldRotation` | `true` | bool | Master toggle for the feature. |
| `RotationInterval` | `0.5` | 0.1–2.0 s | Sweep cadence. |

`RequireRestart = false`, like every other PSW setting — so it can be toggled **mid-battle** for an A/B.

Both keys **must be hand-written into the live MCM JSON**, or they read as `false`/`0` in game while looking correct
in source (see `docs/agent/mcm-settings.md`). Both must be added to `Diagnostics.DescribeConfig` so the mission
report stays self-labelling.

---

## 6. Diagnostics (required — this project's standing rule)

Add to the mission report:
```
shield rotation     : N swaps across M formations (K shieldless front-rankers seen)
```
Flag `<-- FEATURE NEVER FIRED` when `N == 0`, exactly as the other three features do. A successful swap is otherwise
silent, so a dead feature and a working one produce identical output.

Count **events**, not ticks (`docs/agent/diagnostics.md`) — the sweep runs 2×/s per formation, so a raw counter would
measure the timer, not the behaviour.

---

## 7. Risks / open questions

1. **Can two packed men physically trade places mid-melee?** (PRIMARY RISK, unverifiable from code.) At `Interval == 0`
   men are shoulder-to-shoulder; a slot swap only reassigns indices, and the men must then *walk* to their new
   positions. They may shove, clip, or jitter. This is my honest suspicion for **why TaleWorlds added the guard in the
   first place** — in which case the guard is a rendering/physics decision, not an oversight. Only a battle answers it.
   **Fallback if it looks bad:** restrict swaps to men not currently in contact, or lower the cadence.
2. **Churn.** A man whose shield breaks *and* who is then swapped may swap back if `HasShieldCached` flickers. The
   partition is idempotent (a sorted file emits no swaps), so this should be self-limiting — verify in the log.
3. **Detached units in melee.** Unverified whether ordinary melee detaches agents from the formation. If most men
   detach on contact, step 2's skip could make the feature a no-op exactly when it matters. **The diagnostic must
   count skipped-as-detached** so this shows up immediately rather than looking like "feature never fired".
4. **Interaction with RBMFork / FrontlineModFork.** Both prefix `LineFormation.SwitchUnitLocations`. We call
   `IFormationArrangement.SwitchUnitLocations`, which resolves to that same method — so their prefixes **will** run on
   our swaps. Both return `true` for valid active pairs, so this should be benign, but it is a real coupling: if
   swaps mysteriously do not happen, suspect these two first.

---

## 8. Out of scope

- Line / Circle / Loose / Column / Skein / Scatter formations — vanilla's rotation already works there
  (`Interval > 0`). Do not touch.
- Reviving vanilla's loop via a transpiler on `SwitchFrontUnitTypesToFrontRows`. Rejected: it is private, and a
  transpiler that strips a guard is far more fragile than 40 lines of public-API code we own.
- Any change to `WindupTransparencyPatch.Classify` / the `live-arc` guard. Mark's ruling (2026-07-12): the constraint
  is a **feature** — "allies in a shield wall are defensive and should not also be super strong on the attack."
- Shield HP thresholds / partial-damage rotation. Binary `HasShieldCached` only.

---

## 9. Definition of done

1. Builds clean; existing 27 tests still pass.
2. Unit-testable rotation core (the per-file partition) references **zero** TaleWorlds types, so the net8.0 xUnit
   project can source-link it — same pattern as `CrowdState` / `AttackRemap`. Tests cover: already-sorted file
   (no swaps), fully-reversed file, all-shielded, none-shielded, single unit, detached (-1) units skipped.
3. Deployed via `bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll` (a build no longer deploys).
4. Main-menu banner still reads the correct patch count (this feature adds **no** patches — the count stays at 2).
5. **In-game gate (Mark):** form a shield wall, let the front rank take shield damage until shields break, and watch
   whether shieldless men are pulled back and replaced. Then repeat in Square and confirm shields end up on the
   perimeter. The mission report must show `shield rotation : N swaps`, N > 0.
