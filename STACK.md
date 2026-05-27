# Stack — Proper Shield Walls (Othismos)

## Core

| Component | Version | Notes |
|-----------|---------|-------|
| .NET Framework | 4.7.2 | Required by Bannerlord v1.3.x |
| C# | 7.3 (LangVersion in csproj) | Limited by .NET 4.7.2 |
| Bannerlord | v1.3.14 target (v1.3.15 in active modlist) | Confirm before shipping |
| 0Harmony | Bundled with Bannerlord.Harmony mod | `ExcludeAssets runtime` in csproj |
| MCM v5 | Bannerlord.MBOptionScreen | Optional dependency |

## Game DLL References (all `Private=False`)

- `TaleWorlds.MountAndBlade.dll` — Mission, Agent, Formation, MissionBehavior, etc.
- `TaleWorlds.Core.dll` — WeaponClass, ActionIndexCache, ItemObject
- `TaleWorlds.Library.dll` — Vec3, MathF, Debug
- `TaleWorlds.Engine.dll` — low-level engine types
- `TaleWorlds.ObjectSystem.dll` — MBObjectManager
- `TaleWorlds.CampaignSystem.dll` — campaign-mode only (not needed for mission behavior)
- `SandBox.dll` — SandboxAgentApplyDamageModel (for friendly fire patches)

## Build Environment

- **Build target:** Windows (msbuild on the Windows side of WSL)
- **WSL build command:** `/mnt/c/Program Files/dotnet/dotnet.exe build -c Release`
- **Auto-deploy:** PostBuild target copies DLL + SubModule.xml to game Modules folder
- **Game folder:** `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\`

## Decompilation Tool

- ILSpy CLI: `/mnt/c/Users/Mark Lewis/.dotnet/tools/ilspycmd.exe`
- Usage: copy target DLL to `/mnt/c/Temp/`, run with `-r C:\\Temp` for reference resolution
