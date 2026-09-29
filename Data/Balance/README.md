# Data/Balance

Balance data (D-09: this repository-root `Data/` is the single source of truth, read by Unity and by the `dotnet/` solution).

| File | Controls | Valid values |
| --- | --- | --- |
| `effects.schema.json` | Format of the effect catalog (JSON Schema 2020-12) | — |
| `effects.json` | Effect catalog used by `Balance.Eval(Effect, attributes)`: per effect, 1–3 weighted attributes, piecewise linear curve (`[x, y]` points, x on the 1–99 attribute scale, strictly increasing), output range `min`–`max`, `unit`, optional declared `monotonicity` | Checked by `BalanceValidator`: every `Effect` enum member defined, every one of the 18 attributes feeds at least one effect, weights > 0, ≥ 2 points, Y inside `[min, max]`, declared monotonicity respected |

Stage 0 state: `effects` is empty. The production effect catalog and its curves are not defined in the documentation (DECISIONS.md D-20), JSON is parsed with Newtonsoft JSON (D-11) and validated by `BalanceValidator`; with the empty catalog the validator reports the 18 attributes without effect.
