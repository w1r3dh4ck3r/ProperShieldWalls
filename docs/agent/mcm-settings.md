# Editing `Settings.cs`

## A new setting is NOT live until it exists in the live JSON

MCM writes a settings file once and then **reads it**. Adding a new property to `Settings.cs` does not add the key
to an already-existing settings file — the C# default is silently NOT applied, and the feature reads as
`false`/`0` in game while looking correct in source. This is the trap recorded in
`reference_mcm_settings_file_generation`.

When you add a setting, **write the key into the live MCM JSON by hand** (that is how
`Friendly Block Passthrough` was shipped), or verify in-game via the mission report's `config:` line.

## Every toggle is `RequireRestart = false` — keep it that way

This is load-bearing for the whole test method. It means a feature can be flipped **mid-battle**, so a fight can
be A/B'd inside a single mission (swing 10 times, toggle, swing 10 more) and a design question can often be
answered by **dragging a slider instead of writing code**.

Before you change a default or a range, ask whether the question can be settled with the existing slider first.
`WindupThreshold` (0f–0.6f) is the live example: the whole "should `live-arc` be broadened?" design argument is
answerable in-game at zero code cost.

## The live JSON overrides the C# default, always

Changing a default in `Settings.cs` does **nothing** for an install that already has a settings file. Never
conclude "the default is 0.25, therefore the game is running 0.25" — read the `config:` line in the mission
report, which stamps the **actual** values in force for that run.

## Any new setting must appear in `Diagnostics.DescribeConfig`

Otherwise mission reports stop being self-labelling and a multi-run campaign becomes unattributable — which is
exactly the hole the config stamp was added to close.
