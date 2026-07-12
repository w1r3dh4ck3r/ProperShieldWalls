# Session State — ProperShieldWalls

## Current Task
**New sprint: SHIELD ROTATION.** Spec written + committed (`f73d62a`):
`docs/superpowers/specs/2026-07-12-shield-rotation-design.md`. Awaiting Mark's spec review, then `writing-plans`.

**The find:** vanilla ALREADY rotates shieldless men out of the front rank
(`LineFormation.SwitchFrontUnitTypesToFrontRows`, 0.5 s tick, `PreferShieldedUnitsOnFront`) — and it is
**structurally dead in exactly ShieldWall and Square**, the only two orders that need it. It opens with
`if (Interval <= 0f) return;`, and `ArrangementOrder.GetUnitSpacingOf` returns **0** for both ⇒
`Interval = 0.38 × 0 = 0` ⇒ returns on line 1, forever. It has never run for anyone. Not a mod conflict:
all 85 enabled mods scanned, none touch the path.

**Why one loop does both:** Square is `RectilinearSchiltronFormation : SquareFormation : LineFormation`, where
`fileIndex` picks the SIDE and `rankIndex` walks INWARD from it. So "shielded men belong at low rank" gives
shields-to-the-front in a wall **and** shields-on-the-perimeter in a square. No square-specific code.

## Last Action
Wrote + committed the spec and the kickoff hook harness (`.claude/settings.json`, `scripts/`, `docs/agent/`).
The agent docs encode the `[MBCallback]` rule, the diag log's two-population gotcha, and the MCM live-JSON trap —
they auto-inject when the matching file is edited.

## Next Step
1. Mark reviews the spec.
2. Then `superpowers:writing-plans` → implement `Behaviours/ShieldRotationBehavior.cs`.
   **No Harmony patch, no reflection** — pure public API (`Formation.Arrangement`,
   `IFormationArrangement.GetAllUnits/SwitchUnitLocations`, `Agent.GetFormationFileAndRankInfo`,
   `Agent.HasShieldCached`). Banner patch count stays at **2**.
3. Kickoff mandates a **blocking `gemini-review`** before the sprint can be called done.

## Files to touch next
- `Behaviours/ShieldRotationBehavior.cs` — NEW (the sweep; gate on `formation.Interval <= 0f`)
- A TaleWorlds-free rotation core (per-file partition) so the net8.0 xUnit project can source-link it, like
  `CrowdState`/`AttackRemap`
- `Settings.cs` — `ShieldRotation` (bool, default on), `RotationInterval` (0.5 s). **Hand-write both keys into the
  live MCM JSON** or they read as false in game.
- `Diagnostics.cs` — add to `DescribeConfig` + a `shield rotation : N swaps` report line (+ `FEATURE NEVER FIRED`)
- `SubModule.cs` — register the behaviour

## Notes
- **Combat work is DONE and validated.** Mark: *"feels good, the units fight correctly and use their spears."*
  **Do not touch `WindupTransparencyPatch.Classify` / the `live-arc` guard / `WindupThreshold`.** Mark's ruling:
  the constraint is a FEATURE — "allies in a shield wall are defensive and should not also be super strong on the
  attack." Test D is moot; do not run it.
- **PRIMARY RISK, only a battle answers it:** at `Interval == 0` men are shoulder-to-shoulder, so two men trading
  slots must physically walk past each other mid-melee. They may shove/clip/jitter. This may be *why* TaleWorlds
  added the guard. Fallback: only rotate men not currently in contact.
- Javelin-melee breakage: root-caused but NOT urgent — see memory `javelin-melee-breakage.md`. Nobody can melee
  with javelins at all (SpearPreferenceFork clears `WeaponFlags.MeleeWeapon`), so the bug is unobservable. Mark
  calls the current state "a half measure — acceptable for now."
- A build no longer deploys. Use `bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll`.
- Branch `feat/cramped-melee-v2` is still UNMERGED.

<!-- session-state-sync: last written by session 1566b843 at 2026-07-12 10:07:55 -0300 -->
