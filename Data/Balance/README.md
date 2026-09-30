# Data/Balance

Balance data (D-09: this repository-root `Data/` is the single source of truth, read by Unity and by the `dotnet/` solution).

| File | Controls | Valid values |
| --- | --- | --- |
| `effects.schema.json` | Format of the effect catalog (JSON Schema 2020-12) | — |
| `ovr.json` | OVR per position (GAME_DESIGN §9): attribute weights per position (normalized on load), secondary-position factor (0.97 = −3%) and out-of-position factor (0.90 = −10%) | Every position has ≥ 1 weight > 0; 0 < outOfPositionFactor ≤ secondaryPositionFactor ≤ 1. Weights are baseline v1 (X-37) |
| `effects.json` | Effect catalog used by `Balance.Eval(Effect, attributes)`: per effect, 1–3 weighted attributes, piecewise linear curve (`[x, y]` points, x on the 1–99 attribute scale, strictly increasing), output range `min`–`max`, `unit`, declared `monotonicity` | Checked on load by `EffectsJsonReader` (structure) and `BalanceValidator`: every `Effect` enum member defined, every one of the 18 attributes feeds at least one effect, weights > 0, ≥ 2 points, Y inside `[min, max]`, declared monotonicity respected |

## effects.json — baseline v1 (D-20)

- 49 effects derived from the attribute → effect table in `docs/TECHNICAL_SPEC.md` §8; all 18 attributes covered.
- Curves use points at 1, 30, 50, 70, 80, 90, 99: 50 ≈ average player, 70–80 clearly above average, 90–99 elite; steeper in the middle, flatter at the extremes (5 points subtle, 15+ clearly visible).
- `min`/`max` are hard clamps for the final value after modifiers, wider than the curve itself.
- These are **starting values, not final balance**. Recalibrate by editing this file only; no code change is needed. Adding an effect requires a new `Effect` enum member plus its definition here (the validator fails otherwise).
