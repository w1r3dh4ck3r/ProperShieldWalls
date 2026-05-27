# Architecture — Proper Shield Walls (Othismos)

## System Overview

```
SubModule.cs
└── OnMissionBehaviorInitialize → adds OthismosBehaviour to every mission

OthismosBehaviour (MissionBehavior)
├── OnDeploymentFinished() → scan all formations for ShieldWall state
├── OnMissionTick(dt)
│   ├── EngagementDetector.Tick() → detect when two ShieldWall formations are within ~2m
│   ├── LockStateManager.Tick() → manage per-pair Locked state lifecycle
│   ├── SlotEnforcer.Tick() → enforce per-agent position within Locked formations
│   ├── StabForcer.Tick() → suppress free-swing, force stab-only attacks
│   └── PressureResolver.Tick() → translate formation anchors based on pressure delta
└── OnAgentRemoved() → handle agents that die/flee during Locked state

Harmony Patches (static, applied at SubModule load):
├── AgentAIPatch → suppress movement-out-of-slot for Locked agents [⚠️ target unconfirmed]
├── FriendlyFireCheckPatch → bypass friendly hit check within Locked formations
├── MeleeHitCallbackPatch → clear shield flags for in-wall stabs
└── CollisionReactionPatch → override Bounced → ContinueChecking for in-wall hits

Data Model:
├── EngagementPair { FormationA, FormationB, State, TimeSinceLock }
└── AgentSlot { Agent, LockedPosition, LockedFacing, RankIndex }
```

## State Machine (per FormationPair)

```
Idle → [ShieldWall + ShieldWall + distance < 2m + facing each other] → PreLock
PreLock → [1-tick debounce] → Locked
Locked → [stamina <= 0 OR agent count < 3 per side OR 3s cooldown] → Breaking
Breaking → [0.5s transition] → Idle (vanilla combat resumes)
```

## Pressure Formula (MVP)

```
pressure(formation) = front_rank_count * 1.0 + second_rank * 0.5 + third_rank * 0.25
delta = pressure(A) - pressure(B)
translate_A += clamp(delta * 0.01 * dt, -0.05, 0.05) meters  [per tick cap]
translate_B -= same
```

## Files (planned)

```
SubModule.cs                    ← entry point, Harmony init, behavior injection
Settings.cs                     ← MCM settings (engagement distance, stamina drain rate, etc.)
Behaviours/
  OthismosBehaviour.cs          ← main MissionBehavior, orchestrator
  EngagementDetector.cs         ← proximity + facing detection
  LockStateManager.cs           ← state transitions, pair tracking
  SlotEnforcer.cs               ← per-agent position locking
  StabForcer.cs                 ← animation forcing, AI suppression
  PressureResolver.cs           ← formation anchor translation
Patches/
  AgentAIPatch.cs               ← suppress free-swing (target TBD from research)
  FriendlyFireCheckPatch.cs     ← from old codebase, adapted
  MeleeHitCallbackPatch.cs      ← from old codebase, adapted
  CollisionReactionPatch.cs     ← from old codebase, adapted
Models/
  EngagementPair.cs             ← pair state data
  AgentSlot.cs                  ← per-agent slot data
```

## Key Dependencies

- **TaleWorlds.MountAndBlade** — Mission, Agent, Formation, MissionBehavior
- **TaleWorlds.Core** — WeaponClass, ActionIndexCache
- **0Harmony** — Harmony patching
- **MCMv5** — settings screen (optional dependency)
- **StaminaSystem** — stamina drain hook (optional; MVP uses own tracking)
