# PSW — Feature Isolation Test Plan

Battles 1–3 ran with **all three features on at once**, so no result can be attributed to any single one.
This plan turns one feature on at a time.

Live DLL for these tests: `feat/cramped-melee-v2@8d3153e`
(verify any time by reading `Modules/ProperShieldWalls/bin/Win64_Shipping_Client/deployed.json`)

---

## Before you start

**Every toggle is live — no restart, no relaunch.** Change them at `Esc → Mod Options → Proper Shield Walls`,
even mid-battle. This means you can A/B a feature inside a single fight: swing 10 times, toggle, swing 10 more.

**Keep `Diagnostic Logging` ON** (Debug group). It is already on.

**Each mission now stamps its own config into the log**, so you never have to remember which run was which:

```
[PSW] ==== mission report ====
[PSW]  config: enabled=1 windup=1 cramped=0 blockPass=0 threshold=0.25 crowdedDur=2.0
[PSW]  windup transparency : 12378 friendly hits made transparent
...
```

Log: `Documents/Mount and Blade II Bannerlord/PSW_diag.log` (appends; one report block per mission).

**Two things to know about the log or you will misread it:**
- **Per-hit lines are YOUR swings only.** They are logged only when the attacker is the player. Capped at 400/mission.
- **The mission-report counts are everyone's** — your troops included, uncapped.

---

## The test arena (use the same one every time)

Custom Battle → your culture vs any → **infantry only, ~30 v 30, no cavalry/archers.**
Order your men to **Shield Wall**, then **stand INSIDE your own line**, one rank back, and try to kill the enemy
your front-rankers are already fighting. That is the exact situation complaint #2 is about; an open-field duel
will not reproduce any of this.

Repeat the same fight for each config so the runs are comparable.

---

## Test 0 — Baseline (vanilla)

**Config:** `Enabled = OFF`
**Do:** Fight from inside your own line. Deliberately swing into an enemy behind an ally.
**Feel for:** the swing dying — a clang, an arm-jerk, your character freezing mid-attack; an enemy your men
surround being effectively unkillable by you.
**Log:** no `[PSW]` per-hit lines at all (the patches early-out).

This is the "before" picture. **Everything below is judged against it.** Don't skip it — the whole campaign is
meaningless without it.

---

## Test A — Windup Transparency alone

**Config:** `Enabled=ON`, `Windup Transparency=ON`, `Cramped Gating=OFF`, `Friendly Block Passthrough=OFF`, threshold `0.25`
**What it does:** if your blade clips an ally in the *first 25%* of the swing, that contact costs nothing —
no stun, no bounce, no clang. The swing keeps going.
**What it does NOT do:** a contact **later** than 25% through the swing still stops you. That is by design (see Test D).

**Feel for:** starting a swing while shoulder-to-shoulder no longer kills the attack before it begins.
**Log:** `windup transparency : N ... transparent`, N > 0. Your own per-hit lines end in `-> BYPASS`.
The ones that still stop you read `-> reject:live-arc`.

---

## Test B — Friendly Block Passthrough alone

**Config:** `Enabled=ON`, `Windup Transparency=OFF`, `Cramped Gating=OFF`, `Friendly Block Passthrough=ON`
**What it does:** an ally's **raised shield** no longer blocks or parries your swing, at any point in the swing.

**Feel for:** swinging past/over a shielded ally in front of you. In vanilla his shield eats your attack like an
enemy's would. This should now pass through.
**Log:** `friendly blocks : N neutralised`, N > 0. Per-hit lines with `blockedShield=1` and `result=Blocked/Parried`.

This is the patch that fired 3524× in battle 3, so it is definitely *working* — the question is only whether you
can *feel* it.

---

## Test C — Cramped Attack Gating

**Config:** `Enabled=ON`, `Windup Transparency=ON`, `Cramped Gating=ON`, `Friendly Block Passthrough=OFF`
**Compare against:** Test A (the only difference is cramped gating).
**Cramped gating cannot be felt — it never touches you.** It is AI-only; the player is never remapped.

**So don't try to feel it — WATCH your troops.** In a packed shield wall, do they **stab/chop overhead** instead of
winding up horizontal swings that would carve into their neighbours?
**Log:** `cramped gating (AI) : N swings remapped across M agents`. N and M > 0.

Best viewed with the free camera / RTS camera looking down your own front rank.

---

## Test D — The one that answers complaint #2

This is the real question, and **it needs no code change.** The `live-arc` reject is literally
`AttackProgress >= WindupThreshold`. `Windup Threshold` is a slider you can drag to **0.60**.

**Config:** `Enabled=ON`, `Windup=ON`, `Block Passthrough=ON`, `Cramped=ON` — then run the same fight twice:
1. threshold **0.25** (today's default)
2. threshold **0.60** (max)

**Do:** stand behind your own front rank and try to kill an enemy your men are surrounding.
**The question:** at 0.60, can you *finally kill him*?

**Log:** compare `rejected live-arc xN` between the two reports. It should fall sharply at 0.60.

**What each outcome means:**
- **0.60 fixes it** → broadening `live-arc` is the right fix. I make it the default (and can go further in code).
- **0.60 helps but doesn't fix it** → contacts past 60% of the swing still bounce. The guard has to be removed
  entirely in code, not just widened — a bigger call, because it fully reverses *"an ally in front still stops
  the blade."*
- **0.60 changes nothing** → the live-arc path is NOT the cause of complaint #2, and I have been chasing the
  wrong mechanism. That is a genuinely useful result; it sends me back to the collision data.

---

## Reporting back

Just tell me which tests you ran and what you felt. **Don't summarise the log — leave it alone and I'll read it**;
every report is now self-labelling, so I can attribute every number without you tracking anything.

The one thing only you can give me is the **feel**: did the swing land, did the arm catch, did the surrounded man die.
