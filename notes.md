# ProperShieldWalls — AI Handoff Log

---

## 2026-06-01 — POC passed, MVP implemented

**What changed:**

- POC test run confirmed: Nord Spear Warrior held at 0.65 m for ~50 s, broke only on formation rout. Gate passed.
- Deleted all old friendly-fire-bypass code: `OthismosTestBehaviour.cs`, `Patches/SlotLockPatch.cs` (POC), `Patches/MeleeHitFriendlyBypassPatch.cs`, `WeaponBypassConfig.cs`, `ShieldWallBehaviour.cs`
- Built full MVP architecture:

```
OthismosState.cs               ← central lock registry + per-agent slot positions
Models/EngagementPair.cs       ← pair state machine data (Idle/PreLock/Locked/Breaking)
Models/AgentSlot.cs            ← per-agent rank tracking (pressure formula)
Behaviours/OthismosBehaviour.cs ← mission orchestrator
Behaviours/EngagementDetector.cs ← ShieldWall proximity + facing detection
Behaviours/LockStateManager.cs  ← state transitions, 1 s debounce, stamina drain
Behaviours/SlotEnforcer.cs      ← slot registration/unregistration on lock/break
Behaviours/StabForcer.cs        ← EnforceShieldUsage(DefendDown) for front rank
Behaviours/PressureResolver.cs  ← per-tick slot nudge based on rank pressure delta
Patches/SlotLockPatch.cs        ← GetOrderPositionOfUnit prefix (primary slot lock)
Patches/AgentAIPatch.cs         ← HumanAIComponent.ParallelUpdateFormationMovement postfix
Patches/MeleeHitCallbackPatch.cs ← friendly pass-through flag + shield-flag clearing
Patches/FriendlyFireCheckPatch.cs ← bypasses CanWeaponIgnoreFriendlyFireChecks
Patches/DecideCollisionReactionPatch.cs ← prevents Bounced override
Patches/RegisterBlowPatch.cs    ← skips blow registration for friendly pass-throughs
Patches/ShieldDamagePatch.cs    ← zeroes shield damage for friendly pass-throughs
Settings.cs                     ← new MCM settings (EngagementDistance, MinAgentsPerSide, StaminaDrainRate, EnableDebug)
```

**API verification (all confirmed via DLL strings extraction on installed game):**

- `Formation.GetOrderPositionOfUnit` ✓ (also confirmed by POC)
- `WorldPosition.SetVec2` ✓
- `Agent.SetTargetPosition` ✓
- `HumanAIComponent.ParallelUpdateFormationMovement` ✓
- `HumanAIComponent.SetShouldCatchUpWithFormation` ✓ (direct method, not property)
- `Agent.EnforceShieldUsage` ✓
- `Agent.HasShieldCached` ✓
- `Formation.CountOfUnitsWithoutDetachedOnes` ✓
- `Formation.CurrentDirection` ✓
- `AttackCollisionData._attackBlockedWithShield` / `_collidedWithShieldOnBack` ✓
- `ArrangementOrderEnum.ShieldWall` ✓ (from RBM source, confirmed in ARCHITECTURE)

**One unresolved item:**

- `AgentAIPatch._agentProp` accesses `HumanAIComponent.Agent` via reflection. The exact property name on `HumanAIComponent` (or its base class) is not confirmed — the patch logs "MISSING" at startup if not found. If it shows MISSING in the first test run log, the patch silently does nothing (secondary enforcement only; primary slot lock still works).

**Next steps:**

1. Build: `/mnt/c/Program\ Files/dotnet/dotnet.exe build -c Release`
2. Run Custom Battle: 10v10, both sides in ShieldWall order, infantry only
3. Watch `rgl_log_XXXXX.txt` for:
   - `[PSW] Locked: formation X vs Y` — engagement triggered
   - `[PSW] Breaking (stamina exhausted)` — clean exit
   - Any `FAILED to patch` lines — compile/reflection issues
4. Gate: no crash, both walls lock on contact, break on stamina exhaustion

**Settings defaults (in MCM "Proper Shield Walls"):**

- Engagement Distance: 5 m
- Min Agents Per Side: 3
- Stamina Drain Rate: 5/s (engagement lasts ~20 s at equal strength)
- Enable Debug: false (set true to see state transitions in-game)

---

## 2026-05-27 — Research phase complete + day-one POC committed

**What changed:**
- Full research phase completed across two sessions on the Linux server
- 5 deliverable docs written: knowledge base, external mod analysis (RBM source), API targets, risk register, day-one plan
- Gate item R1 (agent AI intercept) resolved: `Formation.GetOrderPositionOfUnit` prefix confirmed from RBM `Frontline.cs:240`
- Day-one POC committed and pushed: `OthismosTestBehaviour.cs` + `Patches/SlotLockPatch.cs` + SubModule wire-up

**Key decisions:**
- Spatial constraint approach: return `unit.GetWorldPosition()` from `GetOrderPositionOfUnit` prefix — no animation forcing needed
- MVP stamina: own `Dictionary<Formation, float>`, not StaminaSystem (no public API)
- Old PSW friendly-fire bypass code kept at the time; fully removed in 2026-06-01 session

---

## 2026-07-09/10 — v2.0.0: othismos stripped, cramped-melee shipped (awaiting in-game validation)

**Branch `feat/cramped-melee-v2`, 13 commits, unmerged. Deployed to the live game; NOT yet validated in-game.**

### What the mod is now
Two features, three Harmony patches, five source files.

1. **Windup transparency** — a friendly hit landing during an attack's wind-up costs nothing: no friendly-fire
   stun, no `Bounced` weapon reaction, no shield clang, no blow. Prefix on `Mission.MeleeHitCallback` sets
   `colReaction = MeleeCollisionReaction.ContinueChecking` and **returns `true`**.
2. **Cramped attack gating** — that same interception stamps the attacker "crowded" for 2s in a `float[]` keyed by
   `Agent.Index`. Postfixes on `MissionMainAgentController.ControlTick` (player) and `Agent.OnAIInputSet` (AI)
   rewrite a horizontal swing into an overhead via `Agent.MovementFlags`. **No spatial query is ever performed** —
   the crowding signal is a free by-product of feature 1.

### The mechanism (verified against the v1.4.6 decompile, not inferred)
`Mission.MeleeHitCallback` returns `void`; its outcome travels through `ref` params. It wraps its ENTIRE penalty
block in `if (colReaction != MeleeCollisionReaction.ContinueChecking)` (Mission.cs:5305) — the same path vanilla
itself uses to let kicks/bashes (`IsAlternativeAttack`) pass through friendlies. So setting the flag and returning
`true` makes the original skip its own penalties.

**Never change that `return true` to `return false`.** A Harmony prefix returning false suppresses OTHER mods'
prefixes on the same method; `RealisticCombatSounds` and `XorberaxLegacy` both reference `MeleeHitCallback`. It
would also skip the method's trailing sound-alarm block.

`DecideWeaponCollisionReaction` is deliberately NOT patched: it has exactly one call site in the whole game
(Mission.cs:5376), inside `MeleeHitCallback`, inside the block that guard already skips. The old POC's "safety net"
postfix on it guarded a path that cannot fire.

### Key decisions
- **Deleted 6 of the 7 old othismos patches** (`SlotLockPatch`, `AgentAIPatch`, `RegisterBlowPatch`,
  `FriendlyFireCheckPatch`, `ShieldDamagePatch`, `DecideCollisionReactionPatch`) plus `OthismosState` and
  `StaminaReader`. That system never built after `bd04fd0` and was never validated. Recoverable in history.
- **Did NOT touch `Native/ModuleData/monsters.xml` `body_capsule`** (stock `0.37`). That is the lever both prior
  Nexus mods use (#495 Loulou 2020, dead; #8392 Oscillator 2025). Its author concedes in comments it cannot scale
  with weapon reach and produces an unavoidable "telekinesis bubble" push. Wrong tool; do not revisit.
- **Conflicts resolved by explicit `[HarmonyPriority]`, not load-order slot.** `High`(600) on the AI gate so
  `AIKickNBashFork`'s postfix (defaults to `Normal`=400) runs after and its kick overwrites our remap; `Low`(200)
  on the player gate so we write `MovementFlags` last, after `FluidCombatNextNext` (also 400). Verified from
  `0Harmony.dll`: `PatchSorter` sorts postfixes descending by priority, same as prefixes.
- **`CrowdState` / `AttackRemap` reference ZERO TaleWorlds types** so the net8.0 xUnit project can source-link them.
  27 tests. The three patch files and `CrowdStateBehavior` have no unit tests by design (native-populated structs /
  live `Agent`); their gate is in-game validation.

### Bugs the review process caught (worth remembering)
- A commit shipped `AttackRemap.cs` on disk with its `<Compile>` entry left UNCOMMITTED. Legacy non-SDK csproj has
  no globbing → the file would never have compiled. The "green build" that proved it was built against the
  uncommitted working-tree edit.
- `CrowdStateBehavior.OnBehaviorInitialize()` was dead code. `Mission.AfterStart()` calls `OnBehaviorInitialize()`
  on the behavior list BEFORE calling `MBSubModuleBase.OnMissionBehaviorInitialize()`, which is where we add the
  behavior. Fixed by overriding `OnCreated()`, which `Mission.AddMissionBehavior()` invokes directly on the
  instance being added.
- Unthrottled `Debug.Print` in two per-tick catch blocks → `SubModule.LogErrorThrottled` (3 logs + 1 suppression
  notice per `"<PatchName>:<ExceptionType>"` key, then silent; reset per mission).
- **Nothing on this machine captures `Debug.Print`.** A silently-failed Harmony bind would have been invisible, so
  a main-menu banner was added: green `[PSW] Proper Shield Walls v2.0.0 — 3 patches OK.` / RED on any failure.

### Refuted risk (do not re-investigate)
Writing `agent.MovementFlags` in an `OnAIInputSet` postfix is NOT clobbered by the native caller reading back the
`ref movementFlag` param. `OnAIInputSet` only fans out to `AgentComponent.OnAIInputSet`; the `MovementFlags` setter
IS itself the native call (`MBAPI.IMBAgent.SetMovementFlags`). Corroborated by `AIKickNBashFork`, which uses the
identical technique and is validated in-game.

### Next steps (needs Mark at the keyboard)
1. Accept BLSE's one-time unsigned-DLL CAUTION (trust cache holds the old 2026-05-31 POC hash), else the mod loads
   disabled and every check below is a false negative.
2. Confirm the green main-menu banner reads `3 patches OK`. Red/absent ⇒ load failure, not a gameplay bug.
3. Resolve the three open questions in `docs/superpowers/specs/2026-07-09-cramped-melee-design.md` §6:
   - Is `AttackUp` the overhead (not the thrust)? Unproven — no decompiled code settles it. One constant if inverted.
   - Does `CanSwing` exclude a `BetterPikes` pike (`SwingDamageType != Invalid && SwingSpeed > 0`)?
   - Tune `WindupThreshold` (default `0.25` is a prior, NOT a measurement). Enable MCM `Diagnostic Logging`, fight one
     200-a-side infantry battle, and check whether native sets `HitWithStartOfTheAnimation` on swings at all — the
     engine's only managed use of that flag gates on `StrikeType == 1` (Thrust).
4. Expected, not a bug: F2 self-limits. An agent is stamped only when a horizontal windup clips a friendly; once
   remapped to overhead it stops clipping, so ~one wide swing leaks through every `CrowdedDuration` (2s).
5. Merge to `master` only after the in-game pass. **Until then, do not rebuild from `master`** — its source is the
   old othismos code and `OutputPath` deploys straight into the live game folder.
