# Workflow — Proper Shield Walls (Othismos)

## Build & Deploy

```bash
# Build (run on Windows side or via WSL .NET CLI)
/mnt/c/Program\ Files/dotnet/dotnet.exe build -c Release

# PostBuild target auto-copies to:
# D:\...\Modules\ProperShieldWalls\bin\Win64_Shipping_Client\ProperShieldWalls.dll
# D:\...\Modules\ProperShieldWalls\SubModule.xml
```

## Testing

1. Launch Bannerlord with ProperShieldWalls enabled in launcher
2. Custom Battle mode → 10v10 infantry (same troop type on both sides)
3. Both spawned in ShieldWall formation, ~15m apart, facing each other
4. Watch `rgl_log_*.txt` for `[PSW]` prefixed log lines
5. Log location: `C:\ProgramData\Mount and Blade II Bannerlord\logs\`

## Debugging

- `Debug.Print("[PSW] ...")` for log output — appears in `rgl_log_*.txt`
- `InformationManager.DisplayMessage(...)` for in-game HUD messages (use for rare events only)
- BetterExceptionWindow will catch most managed exceptions in-game
- If crash has no in-game dialog: check Desktop for `crashreport.html`

## Load Order

ProperShieldWalls loads after: Bannerlord.Harmony, Bannerlord.ButterLib, Bannerlord.MBOptionScreen  
ProperShieldWalls loads before: RBM (if used — ensure our Harmony patches run first via Priority.High)

## Branching

- `master` — release-ready code only
- Feature branches: `feat/poc-stab-in-slot`, `feat/mvp-othismos`, etc.
- Gate rule: no merge to master without the gate item working end-to-end

## Release

- Bump version in `SubModule.xml` (format: `v1.0.0.0` — 4 parts with v prefix required)
- Tag and push
- Package for Nexus: zip the `Modules/ProperShieldWalls/` folder
- Upload to Nexus manually (no API for file upload)

## Gate Rules

1. Do not proceed to MVP code until single-agent stab-in-slot POC succeeds
2. Do not merge to master until 10v10 custom battle runs to completion with intended behavior
3. Do not release until tested with RBM in load order
