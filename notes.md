# ProperShieldWalls — AI Handoff Log

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
- Old PSW friendly-fire bypass code kept intact; new othismos system added alongside it

**Build environment note:**
- Development has been on the Linux server (no D: drive DLLs accessible, no ILSpy)
- All research done; next step is BUILD + RUN on PC where game DLLs, ILSpy, and the game itself are available
- The D: symlink in the project root only has `Modules/` — game binaries are not accessible from the server

**Next steps on PC:**
1. `git pull` to get the POC commit
2. Run ILSpy BEFORE building: `ilspycmd.exe TaleWorlds.MountAndBlade.dll -t HumanAIComponent | findstr "ParallelUpdateFormationMovement ShouldCatchUpWithFormation"`
3. Build: `dotnet build` or open in VS on Windows
4. Watch `rgl_log.txt` for `[PSW TEST]` lines — expect `dist < 1.5m` and HP changing on enemy
5. If `SetTargetPosition` fails to compile: remove that one line in `SlotLockPatch.cs` (the `__result = unit.GetWorldPosition()` line is what matters)
6. If POC passes (dist stable, HP dropping, no crash at 60s): delete `OthismosTestBehaviour.cs` and `Patches/SlotLockPatch.cs`, begin full MVP

**Open risk:**
- `unit.SetTargetPosition(unit.GetWorldPosition().AsVec2)` in SlotLockPatch.cs — method may be internal; remove if compile fails
- `HumanAIComponent.ParallelUpdateFormationMovement` / `ShouldCatchUpWithFormation` — verify names match installed version via ILSpy before adding the slot enforcement postfix

---

## 2026-06-03 — Status check, no changes

**What changed:** Nothing — session was a status review only. Repo clean and up to date with origin/master.

**Current state:** POC code committed, waiting on PC build and test. Next steps unchanged from 2026-05-27 handoff above.
