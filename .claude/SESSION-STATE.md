# Session State — ProperShieldWalls

## Current Task
**Shield rotation is BUILT, REVIEWED, GEMINI-CLEARED and DEPLOYED. The only thing left is Mark's in-game test.**

Live DLL: `feat/cramped-melee-v2@658f665` (sha `1668f93a`, verified at destination, `deployed.json` truthful).
38/38 tests pass. Harmony patch count UNCHANGED at 2 — this feature adds none.

## Last Action
Ran the full kickoff cycle: brainstorm → spec (`f73d62a`) → plan (`5ea4ab8`) → 3 subagent-implemented tasks with
review after each → blocking `gemini-review` (3 rounds, ended **NO BLOCKING ISSUES**) → deploy.

## Next Step — MARK AT THE KEYBOARD (this is the gate)
Custom Battle, infantry only, ~30v30, `Diagnostic Logging` already ON.
1. **ShieldWall.** Let the front rank's shields break (javelins help). Do shieldless men get pulled back and
   replaced by shielded men?
2. **Square.** Same fight. Do shields end up on the OUTER RING, shieldless in the interior?
3. Then read `/mnt/c/Users/w1r3d/Documents/Mount and Blade II Bannerlord/PSW_diag.log`, newest mission report:
   - `config: ... rotate=1 rotInterval=0.5` — the setting took (if `rotate=0`, the MCM key did not load)
   - `shield rotation : N swaps across M formation-sweeps (K shieldless front-rankers seen, D skipped as detached)`
   - `N > 0` = the feature fired. `<-- FEATURE NEVER FIRED` + a **large D** = melee detaches men from the
     formation, so every swap candidate is being skipped. That is a KNOWN RISK, not a bug in the sweep — the
     `skipped as detached` counter exists precisely to tell those two apart.

**PRIMARY RISK, only a battle can answer it:** at `Interval == 0` men stand shoulder-to-shoulder, so two men
trading slots must physically walk past each other mid-melee. They may shove/clip/jitter. This may be exactly WHY
TaleWorlds gated the rotation off. If it looks bad: restrict swaps to men not in contact, or raise `Rotation Interval`.

**Detachment risk — DOWNGRADED (verified from the decompile, 2026-07-12).** `Agent.IsDetachedFromFormation` is
`_detachment != null`, and `_detachment` is an **`IDetachment`** — the STANDING-POINT system (siege ladders, walls,
engines; see `detachment.IsStandingPointAvailableForAgent`, Agent.cs:1143). **Ordinary melee contact does NOT
detach an agent.** So in a field battle men keep valid file/rank while fighting and the sweep will see them.
A large `skipped as detached` count is therefore expected mainly in SIEGES, not open-field tests.

**Live everywhere, not just the test arena:** `ShieldRotation` is `true` in the live MCM JSON, so this
not-yet-in-game-validated behaviour is active in EVERY battle, in the same DLL as the validated combat work. It is
`RequireRestart=false`, so it can be switched off mid-battle from Mod Options if it misbehaves.

## Files to touch next
Only if the in-game test finds something. `Behaviours/ShieldRotationBehavior.cs` is the sweep;
`ShieldRotation.cs` is the pure planner (38 tests); `Settings.cs` / `Diagnostics.cs` hold the toggles + report.

## Notes
- **The find of this sprint:** vanilla ALREADY wrote shield rotation and wired it into both ShieldWall and Square —
  then gated it behind `if (Interval <= 0f) return;`, and `ArrangementOrder.GetUnitSpacingOf` returns **0** for both
  of those orders. `Interval = 0.38 × 0 = 0`. **It has never run for anyone, in any playthrough.** Not a mod
  conflict (all 85 enabled mods scanned, none touch the path).
- Square is `RectilinearSchiltronFormation : SquareFormation : LineFormation`, where `fileIndex` picks the SIDE and
  `rankIndex` walks INWARD — so the single rule "shielded men belong at low rank" yields shields-to-the-front in a
  wall AND shields-on-the-perimeter in a square, with no square-specific code.
- **Combat work is DONE and validated** (Mark: *"feels good, the units fight correctly and use their spears"*).
  **Do not touch `WindupTransparencyPatch.Classify` / the `live-arc` guard / `WindupThreshold`.** Mark's ruling: the
  constraint is a FEATURE — "allies in a shield wall are defensive and should not also be super strong on the attack."
- Gemini's round-1 Critical (a stale-snapshot race) was **REFUTED from the decompile** — do not re-open it.
  `ReconstructUnitsFromUnits2D` rebuilds only the flat `_allUnits` list and assigns no rank/file indices.
- Javelin-melee breakage: root-caused, NOT urgent — memory `javelin-melee-breakage.md`.
- A build no longer deploys. Use `bl-deploy ProperShieldWalls bin/Release/ProperShieldWalls.dll`.
- Branch `feat/cramped-melee-v2` is still UNMERGED and is now **8 commits ahead of origin**.

<!-- session-state-sync: last written by session 1566b843 at 2026-07-12 11:14:09 -0300 -->
