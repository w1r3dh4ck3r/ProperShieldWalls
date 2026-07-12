# Editing `Diagnostics.cs` (or anything that logs)

## Nothing on this machine captures `Debug.Print`

A silently-failed Harmony bind would be invisible. That is why `SubModule` shows a main-menu banner
(green `N patches OK` / RED on failure) and why the diagnostic writes to a **file**:
`<Documents>/Mount and Blade II Bannerlord/PSW_diag.log`.

Real path: `/mnt/c/Users/w1r3d/Documents/...` — note **`w1r3d`, not `Mark Lewis`**. It sits at depth 5, so a
`find -maxdepth 4` misses it and "no log exists" becomes a false negative. Verify with a direct `ls -l`.

## The log has TWO line-classes with DIFFERENT populations

This is the single most misreadable thing in the project and it has already burned one analysis:

- **Per-hit lines** (`t=… dir=… prog=… -> reject:X`) are the **PLAYER's swings only** (`attacker.IsMainAgent`),
  capped at **400 per mission**.
- **Mission-report counters** (`rejected live-arc xN`) are **ALL agents, uncapped**.

The two numbers **do not share a denominator**. Never compute "N of 5233 would flip if the threshold moved" from
the per-hit lines. If you histogram per-hit lines, say out loud that the sample is Mark's own swings.

The file **appends across missions**, so a bare grep spans every battle ever run. Since `8d3153e` every report
opens with a `config:` line (`Diagnostics.DescribeConfig`) — **filter on it** so runs are attributable.

## Log the rejecting guard's NAME, not just the inputs

The original instrument logged *after* the early-return guards, so a rejected hit left no trace: "we never saw the
collision" and "we saw it and declined it" produced identical (empty) output. `Classify()` now returns the name of
the guard that turned the hit away (`world-hit` / `not-collider-agent` / `self-hit` / `victim-not-human` / `enemy`
/ `live-arc`), or null for BYPASS — and the outcome is logged **before** acting.

## A raw per-tick counter measures tick rate, not behaviour

`OnAIInputSet` fires every AI decision tick. The same battle read **13230 "remaps"** per-tick versus **216 actual
swing events across 96 agents** once de-duplicated with a 0.5 s per-agent gap. Count **events**, not ticks — and
report both if in doubt.

## Report which features never fired

A successful AI remap emits no log line, so a dead feature looks exactly like a working one. Every mission report
flags any feature whose counter is zero with `<-- FEATURE NEVER FIRED`. Keep that.

## Synchronous file IO on the main thread is a hitch

Do not add a per-agent-per-tick `File.AppendAllText`. The sibling repo `SpearPreferenceFork` shipped exactly that
and produced a game-wide ~2×/sec stall: 50,682 per-sweep lines in one battle, each a synchronous open/write/close
bunched into 0.34 s frames. Per-hit lines are capped (400/mission) and scoped to the player for this reason.
