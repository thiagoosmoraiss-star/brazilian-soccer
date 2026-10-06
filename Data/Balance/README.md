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

## match_rules.json — shared match rules (B2, baseline v1, X-38)

Used by both engines through `Rules.Match.MatchRules` (TECHNICAL_SPEC §10: shared sector strength, fouls/cards/injuries, fatigue, substitutions, ratings).

| Section | Controls | Valid values |
| --- | --- | --- |
| `sectors` | Attribute weights of each sector (Attack, Creation, Defense, Aerial, Goalkeeping), normalized on load | Every sector present; every one of the 18 attributes feeds at least one sector |
| `roleSectorWeights` | How much each formation role (GK, CB, FB, DM, CM, AM, W, ST) contributes to each sector | Every role present |
| `sectorReferenceWeights` | Divisor per sector so a natural 4-4-2 of players rated R has sector strength R | > 0 |
| `condition` | Morale factors 1–5 (±5%, GAME_DESIGN §9), form factor around a neutral rating, low-energy threshold (60%) | 5 morale values; form range brackets 1 |
| `fatigue` | Energy drain per minute (× EnergyDrainMult effect × mentality/pressure intensity), half-time recovery (+15–20) | Drain > 0 |
| `discipline` | Yellow / straight red per foul; card factor for already-booked players | Probabilities 0–1 |
| `injuries` | Hazard per player-minute (× InjuryChance effect, × low-energy multiplier) and severity weights (light/medium/severe) | Hazard < 0.05; weights ≥ 0 |
| `ratings` | Rating formula 0–10 (X-38): base, per-action weights, result/clean-sheet terms for defensive roles, noise | min ≥ 0, max ≤ 10 |
| `substitutions` | AI windows (half-time + stoppages), max stoppages, energy threshold, max tired subs, bench margin | Increasing windows |

## quicksim.json — QuickSim coefficients (B2, baseline v1, X-38)

Minute slices; possession by Creation strength; dangerous-attack chance by Attack vs Defense; play types (open play, cross, long shot) with on-target and goal-given-on-target chances; blocks, corners, penalties; selection weights per position for shooters, headers, assists, crossers and foulers; tactic multipliers (mentality, line, pressure); home advantage; probability caps. Calibrated so league-like batches hit `Data/TestRanges/quicksim.json`. Inspect with `dotnet run --project dotnet/QuickSimReport -- --matches 10000`.

## ball.json — field geometry + ball physics (A1, baseline v1, X-47)

Used by `Game.Match.Pitch` and `Game.Match.BallPhysics` (TECHNICAL_SPEC §6, GAME_DESIGN §25: own deterministic physics, no PhysX).

| Section | Controls | Valid values |
| --- | --- | --- |
| `pitch` | Field length/width (105×68 m), goal width/height (7.32×2.44 m, real-world fact, not a balance number), post radius | `goalWidth` < `width`; all > 0 |
| `ball` | Radius (0.11 m); gravity; rolling friction deceleration and the speed below which it is treated as stopped; air drag while airborne ("arrasto leve"); bounce vertical restitution (GAME_DESIGN: 0.45–0.60) and horizontal retention (~0.85), and the vertical speed below which a bounce settles into rolling; post/crossbar restitution; net damping; spin's lateral acceleration coefficient and decay per second (only applied while airborne — colocado, cruzamento, falta); the step distance that triggers sub-stepping ("sub-passos só em chute forte") and its cap | 0 < restitution/retention/damping < 1; the rest > 0 |

## movement.json — non-attribute movement coefficients (A2, baseline v1, X-49)

Used by `Game.Match.Movement`/`Possession`/`DribbleSystem`. Attribute-dependent values (sprint speed, jog speed, accel/decel time, turn loss, touch distance, feint success...) come from the `Effect` catalog (`effects.json`, already baseline v1 since Stage 0) via `Balance.Eval`, not from this file.

| Field | Controls | Valid values |
| --- | --- | --- |
| `playerRadius` | Player circle radius (GAME_DESIGN §25: ~0.4 m), real-world fact not a balance number | > 0 |
| `possessionCaptureRadius` | Distance within which a player picks up a loose ball | > 0 |
| `turnNoLossMaxDegrees` / `turnMediumLossMaxDegrees` | Turn-angle bands (GAME_DESIGN §18: ≤45° no loss, 45-90° medium, >90° sharp) | medium > no-loss |
| `mediumTurnSpeedLossFlat` | Flat speed loss for a 45-90° turn (15-30%); sharper turns use `Effect.TurnSpeedLoss`/`TurnSpeedLossWithBall` (40-60%) instead | 0–1 |
| `withBallSpeedPenaltyShortTouch` / `withBallSpeedPenaltyLongTouch` | Dribble speed penalty, short vs. long touch (GAME_DESIGN §18: -10% to -5%) | 0–1 |
| `sprintBurstTouchMultiplier` | How much farther the ball sits ahead during a sprint burst ("arrancada, toque longo") | ≥ 1 |
| `feintPulseMaxSeconds` | Body-feint input window (GAME_DESIGN §21: < 0.25 s) | > 0 |
| `sprintMemorySeconds` | Sprint keeps going this long after release if the stick is still pushed (GAME_DESIGN §17: 0.4 s) | ≥ 0 |
| `inputBufferSeconds` | Input buffer (GAME_DESIGN §17: ~150 ms) | ≥ 0 |
| `stopSpeedEpsilon` | Speed below which a player is considered stopped | > 0 |

## fatigue.json — fatigue coefficients (A2, baseline v1, X-49)

Used by `Game.Match.Fatigue`. The attribute-dependent part (Resistência → `Effect.EnergyDrainMult`) is already baseline v1 since Stage 0.

| Field | Controls | Valid values |
| --- | --- | --- |
| `referenceDurationMinutes` | The duration `jogDrainPerMinuteAt6Min` is calibrated for (GAME_DESIGN §18: 6 min) | > 0 |
| `jogDrainPerMinuteAt6Min` | Energy spent per minute jogging at that reference duration, before the Resistência multiplier | > 0 |
| `sprintCostMultiplier` | Sprinting drains this many times the jog rate (GAME_DESIGN §18: 5-6x) | ≥ 1 |
| `lowEnergyThreshold` | Energy fraction below which speed/acceleration are penalized (GAME_DESIGN §18: < 60%) | 0–1 |
| `lowEnergySpeedPenalty` | Maximum penalty at 0 energy, scaling to 0 at the threshold (GAME_DESIGN §18: up to -12%) | 0–1 |
| `halftimeRecovery` | Energy restored at half-time (GAME_DESIGN §18: +15-20) | ≥ 0 |

Drain is normalized by the configured match duration (`referenceDurationMinutes / durationMinutes`), so total energy spent playing the same way for the whole match is equal at 4, 6 or 10 minutes (TECHNICAL_SPEC: "Gasto normalizado pela duração configurada").
