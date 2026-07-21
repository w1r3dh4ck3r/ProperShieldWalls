# Rank-2 Thrust Past Allies — Stage 1: Measurement

**Date:** 2026-07-21
**Repo:** ProperShieldWalls
**Status:** Design approved by Mark (approach A, measure-first). Not yet implemented.
**Scope:** Stage 1 only. Stage 2 is sketched, not committed.

---

## 1. Goal

Mark's words, 2026-07-21:

> "lets focus on forcing a little more units using their spears from rear ranks to reach the first rank
> of the enemy from above their friendlies heads"

Get more than the front rank fighting: a rank-2+ spearman should be able to thrust over the man in
front of him and land a real hit on the enemy front rank.

### Scope narrowing (decided this session)

The javelin work that preceded this is **closed**. Mark: *"They throw when they get an enemy in visual"* —
so the parked "rear-rank javelin volley" feature (#2 of the 2026-07-18 three-want reframe) is dropped,
along with javelin throw-doctrine Step 1 (#1), which the shipped melee-damage fix already satisfied.
**Only the rank-2 thrust survives.**

---

## 2. What blocks this today — two independent blockers

### Blocker 1 — the ally's body eats the thrust

`Mission.MeleeHitCallback` wraps its entire penalty block in
`if (colReaction != MeleeCollisionReaction.ContinueChecking)`. Inside that block live the attacker's
friendly-fire stun, `MissionCombatMechanicsHelper.UpdateMomentumRemaining`, and
`DecideWeaponCollisionReaction` (which returns `Bounced` for any hit with `InflictedDamage <= 0` — and a
friendly hit always lands there, because damage is zeroed just above). So a thrust into an ally is
stopped, staggered, and its momentum consumed.

Setting `colReaction = MeleeCollisionReaction.ContinueChecking` makes the original skip that whole block.
Vanilla itself uses this path to let kicks and bashes pass through friendlies.

**PSW already does exactly this — but only for the wind-up phase.** `WindupTransparencyPatch.cs:70` sets
`ContinueChecking`; `WindupTransparencyPatch.cs:110-111` declines to for a live strike:

```csharp
// A live strike arc: an ally in front still stops the blade.
if (!windup) return "live-arc";
```

That is a deliberate design ruling made to preserve shield-wall feel, **not** an engine limit. It is one
line, and it is the lever.

*Provenance:* the `Mission.cs` line numbers (5305 penalty block, 5360 damage zeroed, 5376
`DecideWeaponCollisionReaction`) are quoted from `WindupTransparencyPatch`'s own header comment, which
states it was verified against the v1.4.7 decompile. Not independently re-decompiled on 2026-07-21.

### Blocker 2 — rear ranks may not be holding a spear at all

Logged by Mark on 2026-07-10 in `project_propershieldwalls` memory and never investigated:

> back-rank units in a packed formation (not necessarily ShieldWall order) never switch to
> spears/polearms to stab past the front rank. Uninvestigated.

This matters geometrically. Rank spacing is roughly 1 m; a ~3 m spear reaches the enemy front rank from
rank 2, a ~1 m sword cannot — no matter what the collision code does. **If Blocker 2 is real, fixing
Blocker 1 alone buys nothing.**

### The crux neither can settle

Whether the native capsule sweep, once un-frozen by `ContinueChecking`, actually continues far enough to
register a second, genuine (non-friendly) `MeleeHitCallback` on the enemy standing behind. No shipping
mod does this: BetterPikes uses `Agent.RegisterBlow` for a **zero-damage** anti-cavalry brace, and
Organized Frontline sidesteps the problem entirely by widening spearman spacing to ~3.5 m so no ally sits
in the path. **Only an in-game test can answer it, and that is Stage 2's job, not Stage 1's.**

---

## 3. Why measure first

PSW has already measured, in a real battle on 2026-07-10, that **~40% of friendly contacts take
`reject:live-arc` (1071 of 1590)**. Those collisions are happening and being turned away right now. What
we do *not* know is their composition — how many are rank ≥ 1, and whether those men hold polearms.

Building the collision fix without that answer risks the exact failure this project has already paid for
twice: BreakablePolearms shipped a feature against a fence that turned out to be load-bearing, and the
javelin "reach" hypothesis was refuted only after work had been done on it. Measuring costs one battle
and cannot waste one.

---

## 4. Stage 1 design

**Zero gameplay change.** The `live-arc` guard still rejects exactly as it does today. Nothing about any
collision outcome changes. We only record what is being rejected.

### 4.0 Where the logic lives — pure vs. game-coupled

`ProperShieldWalls.Tests` source-links **only pure-logic files** (`CrowdState.cs`, `AttackRemap.cs`,
`ShieldRotation.cs`) and its csproj states they *must not reference TaleWorlds types*. `Diagnostics.cs`
references MCM and is therefore untestable by construction.

So the split follows the repo's established pattern:

- **`LiveArcCensus.cs` (NEW, pure)** — bucketing and key construction, primitives in and a `string` out.
  No TaleWorlds, no MCM. Source-linked into the test project and unit tested.
- **`Diagnostics.cs`** — owns the dictionary and the adapter that reads TaleWorlds types off the `Agent`
  and `AttackCollisionData`, then delegates key-building to `LiveArcCensus`.

`LangVersion` is **7.3** — no switch expressions, no target-typed `new`, no `is not`.

### 4.1 `Diagnostics.cs`

Add a census following the existing `_formationCensus` pattern (`Diagnostics.cs:180-189`) — aggregated by
key with a hit count, **never one line per event**. The file's own doctrine is explicit that
`MeleeHitCallback` runs at collision rate and a per-event line is a log storm.

```csharp
private static readonly Dictionary<string, int> _liveArcCensus = new Dictionary<string, int>();
```

- `RecordLiveArc(Agent attacker, ref AttackCollisionData cd)` — reads rank and wielded weapon off the
  attacker, builds the key, increments the count. Signature matches the call site in §4.2; the reads live
  inside `Diagnostics` so the patch stays a one-liner.
- Cleared in `Reset()` alongside the other collections.
- Rendered in `WriteMissionReport()` as its own block, following the formation-census layout.

**Key composition** (each field bucketed so the dictionary stays small and bounded):

| Field | Source | Buckets |
|---|---|---|
| attacker rank | `agent.GetFormationFileAndRankInfo(out file, out rank)` | `0`, `1`, `2`, `3+`, `detached` |
| wielded weapon class | `agent.WieldedWeapon.CurrentUsageItem.WeaponClass` | verbatim enum name; `unarmed` when null |
| weapon length | same `WeaponComponentData.WeaponLength` | `<120`, `120-199`, `200-279`, `280+` |
| strike type | `cd.StrikeType` | `Thrust` / `Swing` |
| attack direction | `cd.AttackDirection` | verbatim enum name |

Rank uses the repo's existing idiom from `ShieldRotationBehavior.cs:103-113`, **including its −1 guard** —
a detached unit reports −1 and must be bucketed as `detached`, not treated as rank 0.

Weapon uses the repo's existing idiom from `AttackGatePatches.cs:89`
(`agent.WieldedWeapon.CurrentUsageItem`, null when unarmed).

Including attack direction settles the separately-parked question *"Is `AttackUp` the overhead animation
and not the thrust?"* at no extra cost.

### 4.2 `WindupTransparencyPatch.cs`

One call, on the `live-arc` return path only:

```csharp
// A live strike arc: an ally in front still stops the blade.
if (!windup)
{
    Diagnostics.RecordLiveArc(attacker, ref collisionData);
    return "live-arc";
}
```

That path is reached only for friendly collisions — the `enemy` guard returns above it
(`WindupTransparencyPatch.cs:104`) — so volume is bounded to the ~1071/mission already measured.

### 4.3 Constraints on the edit

`Mission.MeleeHitCallback` carries `[MBCallback]`. Therefore:

- The new call sits **inside the existing `try/catch`**. No new control flow, no new patch, no new
  `try` block.
- **Every existing `return true` stays exactly as it is.** A prefix returning `false` would suppress
  other mods' prefixes on this method — RBMCombat, RBMAI, RealisticCombatSounds and XorberaxLegacy all
  patch it.
- No new MCM setting. The census rides on the existing `DiagnosticLogging` toggle.
- Counters increment unconditionally (matching `RecordWindup`); only the *report write* is gated on
  `Diagnostics.Enabled`.

### 4.4 Tests

Extend the existing xUnit project, adding `<Compile Include="../LiveArcCensus.cs" Link="LiveArcCensus.cs" />`
to `ProperShieldWalls.Tests.csproj`. `LiveArcCensus` is pure string/bucket logic and is fully unit
testable without the game: rank bucketing (including −1 → `detached`), length bucketing at every
boundary, and null/absent weapon → `unarmed`.

No test can cover the Harmony path or the `Agent` reads; that is what the battle is for.

---

## 5. Pre-registered decision rule

Written down **before** the battle so the result cannot be rationalised afterwards. Thresholds are
explicit percentages of the mission's total `live-arc` rejects, evaluated in this order; the first row
that matches decides.

| Observation | Conclusion | Stage 2 becomes |
|---|---|---|
| Rank ≥ 1 rejects are **< 5%** of all live-arc rejects | Rear ranks aren't attacking in the first place | Neither fix; the problem is upstream in attack initiation |
| Rank ≥ 1 **polearm Thrusts** are **≥ 20%** of all live-arc rejects | Blocker 2 is not real | The collision fix alone |
| Rank ≥ 1 rejects exist (≥ 5%) but **< 20%** carry a weapon of length ≥ 200 | Blocker 2 is real — rear ranks hold short weapons | Wielding fix **first**; collision fix alone would have been wasted |
| Rank ≥ 1 polearm rejects exist but are **majority Swing**, not Thrust | Rear ranks hold spears and swing them | A usage-direction problem, not a collision one |

If none of these rows matches cleanly, the result is **inconclusive** and the correct action is to report
that plainly and re-measure — not to pick the nearest row.

---

## 6. Operational procedure

**`DiagnosticLogging` is currently OFF** (`Settings.cs:60` defaults false, and it was deliberately turned
off on 2026-07-13 — PSW SESSION-STATE "Logging state" section). The live MCM JSON overrides the C#
default, so the code default is irrelevant; **it must be switched on in MCM**.

It is `RequireRestart = false` (`Settings.cs:57`), so no relaunch is needed — but the report is written by
`CrowdStateBehavior.OnEndMission()` and is gated on `Diagnostics.Enabled`
(`Behaviours/CrowdStateBehavior.cs:52-53`), so it must be on **before the mission ends**, and the mission
must end cleanly.

1. Build, then deploy. **Build output stays inside the repo** (`bin/$(Configuration)/`); deploy is a
   separate, deliberate `cp` — see `Directory.Build.targets`, which fixed the old build-equals-deploy
   footgun. The game must be closed for the deploy step, not the build.
2. Mark enables **MCM → Proper Shield Walls → Debug → Diagnostic Logging**.
3. Fight one battle with a spear-heavy formation, in a packed order (Shield Wall or Line), and let it end
   normally rather than quitting out.
4. Read `Documents/Mount and Blade II Bannerlord/PSW_diag.log`, `==== mission report ====` block.
5. Turn Diagnostic Logging back **off**. Leaving it on is itself a cost — see the 28 MB day-log incident.

**Do not send Mark to fight until the instrument is verified armed.** The report block must be present in
the log for a throwaway mission first; an absent census reads identically to "no rank ≥ 1 rejects exist",
which is one of the decision-rule outcomes. That ambiguity would waste the battle.

---

## 7. Non-goals

- No change to any collision outcome, damage, stun, or reaction.
- No new Harmony patch, no new MCM setting, no change to wielding.
- Not answering whether `ContinueChecking` on a live arc reaches the enemy behind. Stage 2.
- Not touching FrontlineMod (Mark works it in a parallel instance) or RBMFork.

---

## 8. Risks

| Risk | Mitigation |
|---|---|
| Editing a `[MBCallback]` patch that currently works | Edit is one call inside the existing `try/catch`; no control-flow or return-value change |
| Per-collision cost | Path is already filtered to friendly collisions (~1071/mission); reads are managed field accesses |
| Census dictionary growth | All five fields bucketed; bounded to a few dozen keys |
| Log left on after the test | Explicit teardown step, called out in the procedure |
| Instrument silently not armed | Verify the report block appears before the real battle (§6) |

---

## 9. Stage 2 sketch — not committed

Recorded so the shape is known; to be designed after Stage 1's numbers land.

Narrow the `live-arc` guard rather than removing it: allow `ContinueChecking` only for
`StrikeType == Thrust` **and** a polearm-class weapon **and** attacker rank ≥ 1 **and** a friendly victim,
behind a new MCM toggle defaulting **off**. Ungated, this lever removes friendly-fire stun and momentum
loss for *every* live swing against *every* friendly contact — weapons passing through allies globally.
It also interacts with shield blocking, which needed its own separate patch
(`FriendlyBlockPassthroughPatch` on `MissionCombatMechanicsHelper.GetDefendCollisionResults`).

If Stage 1 shows Blocker 2 is real, a wielding fix precedes this — forcing rank ≥ 1 to draw polearms —
and that likely lives in RBMFork or PickupMeleeWeapons rather than here.

---

## 10. Open questions

- Whether the native sweep continues to the enemy behind after `ContinueChecking` on a live arc.
  **UNVERIFIED, and unverifiable without an in-game test.** The single biggest unknown in the whole feature.
- Whether a rank-2 AI even selects an enemy target when boxed in. The first survey established that a blow
  *is* created against the friendly today, so the AI does initiate an attack — but which agent it believes
  it is attacking is unconfirmed.
- Whether RBM's rank-gated `Agent.UsageDirection` code (`RBMFork/Source/RBMAI/RBMAI/AgentAi.cs:535,574`,
  which gives rank 0 a different usage direction from the rest) interacts with rear-rank thrusting. It is
  shield-facing code, so probably not — but it is the only other rank-aware combat code in the stack.
