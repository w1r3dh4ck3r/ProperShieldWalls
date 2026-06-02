# Proper Shield Walls — Othismos

## Purpose

A Bannerlord combat mod implementing historical shield wall contact mechanics (othismos). When two ShieldWall formations make contact, they lock in place and fight as a cohesive unit — front ranks stab in slot with shields up, rear ranks add pressure, and the engagement is decided by aggregate stamina attrition rather than individual duels.

## Current Status

**MVP code complete.** POC gate passed (2026-06-01). Full MVP implemented and committed. Gate item: run 10v10 Custom Battle — no crash, both shield walls lock on contact and disengage on stamina exhaustion.

Key deliverables in `docs/`:
- `01-KNOWLEDGE-BASE-SUMMARY.md` — what we know from local codebase
- `02-EXTERNAL-MOD-ANALYSIS.md` — external mod analysis (in progress)
- `03-API-TARGETS.md` — confirmed and unconfirmed API targets
- `04-RISK-REGISTER.md` — risk register with mitigations
- `05-DAY-ONE-PLAN.md` — single-agent POC plan

## Key URLs

- Game: `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\`
- Modules: `D:\...\Modules\ProperShieldWalls\`
- ILSpy CLI: `/mnt/c/Users/Mark Lewis/.dotnet/tools/ilspycmd.exe`

## AI Instructions

- Game version: v1.3.14 (confirm vs installed before shipping)
- Target namespace: `ProperShieldWalls`
- Harmony ID: `"com.propershieldwalls.patch"`
- NO code from training data without DLL verification — Bannerlord API churns
- Stop at gates: don't proceed past POC until it works, don't proceed past MVP until it runs 10v10

## Out of Scope (MVP)

- Sectioning, flanking, gap-breakthroughs
- AI commander integration
- New animations or player orders (engagement is automatic)
- Shield HP tracking (stamina-only exit condition)
- Second-rank spear logic (front rank only)
- Pressure formula tuning (rough formula only)
