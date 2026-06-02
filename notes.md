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
