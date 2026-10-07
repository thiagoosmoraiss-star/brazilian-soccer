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
| `possessionCaptureMaxHeight` | A ball above this height can't be controlled with feet/chest (A3; headers are A9) | > 0 |
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

## ball.json — pitch additions (A3, X-50)

| Field | Controls | Valid values |
| --- | --- | --- |
| `penaltyAreaDepth` / `penaltyAreaWidth` | Penalty box (real-world 16.5 × 40.32 m); decides `ShotAngleErrorInBox` (Finalização) vs `ShotAngleErrorOutOfBox` (Chute de longe) | goalWidth < width < pitch width; depth < half length |

## kicking.json — passes and shots (A3, baseline v1, X-50)

Used by `Game.Match.PassSystem`/`ShotSystem`, `Game.Rules.Match.KickErrorRules` and (timings) the `InputAdapter`. Attribute-dependent values (`PassAngleError`, `PassPowerError`, `PassBallSpeed`, `ThroughBallError`, `LeadCalcError`, `ShotAngleErrorInBox/OutOfBox`, `ShotPowerMax`, `PressureErrorMult`) come from `effects.json`. Error is the product of the factors below (GAME_DESIGN §19/§20: "Erro = produto de fatores"), sampled from a bell-shaped (triangular) distribution with a per-system RNG stream.

**common**

| Field | Controls | Valid values |
| --- | --- | --- |
| `tapMaxSeconds` | Release within this = tap (automatic force) | > 0 |
| `weakFootAngleDegrees` | Kick aimed beyond this toward the preferred-foot side uses the weak foot (X-50) | 0–180 |
| `orientationFreeAngleDegrees` | No orientation/balance penalty within this angle of the facing | 0–180 |
| `lowEnergyThreshold` | Energy (0–100) below which precision drops (GAME_DESIGN §18: < 40%) | 0–100 |

**pass**

| Field | Controls | Valid values |
| --- | --- | --- |
| `coneHalfAngleDegrees` | Semi assistance cone (GAME_DESIGN §19: ±25°) | 0–90 |
| `coneDistanceWeightDegPerMeter` | Target score = angle + distance × this (lowest wins) | ≥ 0 |
| `maxTargetDistance` | Teammates farther than this are not targeted | > 0 |
| `spacePassDistance` | Tapped pass into space (nobody in the cone) | > 0 |
| `arrivalSpeed` / `throughArrivalSpeed` | Speed left when the ball reaches the receiver / lead point (× `PassBallSpeed`) | > 0 |
| `minSpeed` / `maxSpeed` | Launch speed range; a held pass maps the bar onto it | max > min > 0 |
| `powerBarSeconds` | Hold time for 100% on a held pass | > 0 |
| `throughLeadDistance` | Lead ahead of a static receiver on a through ball (× `LeadCalcError`) | ≥ 0 |
| `pressureRadius` | Opponent closer than this = pressure (GAME_DESIGN: < 2 m; × `PressureErrorMult` 1.3–1.8) | > 0 |
| `orientationMaxErrorPenalty` | Body orientation, up to +60% | ≥ 0 |
| `weakFootMaxErrorPenalty` | Weak foot 1 → +50%, weak foot 5 → 0 | ≥ 0 |
| `firstTimeErrorPenalty` | De primeira +25% | ≥ 0 |
| `lowEnergyMaxErrorPenalty` | Up to +10% at 0 energy | ≥ 0 |
| `distanceReference` / `distanceErrorPerMeter` | Error grows linearly beyond the reference distance | ≥ 0 |

**shot**

| Field | Controls | Valid values |
| --- | --- | --- |
| `powerBarSeconds` | Full bar (GAME_DESIGN §20: ~0.8 s) | > 0 |
| `minSpeed` | Speed at 0% power; 100% = `Effect.ShotPowerMax` (Chute de longe) | > 0 |
| `idealPowerMax` | Above this the vertical error grows (ideal 40–75%) | 0–1 |
| `overPowerVerticalErrorDegrees` | Extra upward error at 100% | ≥ 0 |
| `verticalErrorFraction` | Vertical error = horizontal error × this | ≥ 0 |
| `targetHeight` / `cornerInset` | Corner aimed at: this high, this far inside the post | > 0 / ≥ 0 |
| `aimNeutralThreshold` | Stick sideways component below this = neutral → assistance picks the far corner | 0–1 |
| `pressureRadius` / `pressureErrorScale` / `pressurePowerLoss` | GAME_DESIGN §20: < 1.5 m, +40–100% error, −10% power | > 0 / ≥ 0 / 0–1 |
| `orientationMaxErrorPenalty` | "Equilíbrio" | ≥ 0 |
| `weakFootMaxErrorPenalty` | +0–60% | ≥ 0 |
| `weakFootPowerLossMin` / `weakFootPowerLossMax` | −10% (weak foot 4) to −25% (weak foot 1) | min ≤ max < 1 |
| `firstTimeErrorPenalty` | De primeira +20% | ≥ 0 |
| `lowEnergyMaxErrorPenalty` | Up to +10% at 0 energy | ≥ 0 |
| `distanceReference` / `distanceErrorPerMeter` | Error grows beyond ~the box edge (X-50: keeps a shot from midfield rarely on target) | ≥ 0 |

## tactics.json — tactic-dependent AI parameters (A4, baseline v1, X-52)

TECHNICAL_SPEC §9 "Parâmetros afetados (`tactics.json`)". A4 has only the **default** tactic; per-level values for mentalidade / linha / pressão arrive with TacticsRuntime (A9). Distances are meters in the team's own frame (from its own goal line along its attack).

| Field | Controls | Valid values |
| --- | --- | --- |
| `default.phases.<build/attack/transitionAttack/transitionDefense/defend>.depth` | Defensive line → most advanced line (compactação; GAME_DESIGN §24: 10–15 m between lines defending, 20–25 m attacking) | > 0 |
| `….lineBehindBall` | How far behind the ball the defensive line sits | ≥ 0 |
| `….maxLine` | Highest the defensive line may push | > `minLine` |
| `….widthScale` | Lateral spread of the formation (1 = full width) | 0–1 |
| `default.minLine` | Deepest the defensive line may drop | > 0 |
| `default.lateralShift` | Fraction of the ball's lateral position the block follows (GAME_DESIGN §24: ~40–60%) | 0–1 |
| `default.pressers` / `pressTriggerDistance` | Players who press the carrier, and from how far | ≥ 0 / > 0 |

## ai.json — match AI coefficients (A4, baseline v1, X-52)

TECHNICAL_SPEC §7 / GAME_DESIGN §24. Attribute-dependent values come from `effects.json`: Posicionamento → `AiTargetError`, `AiCorrectionDelay`; Visão → `AiPassOptionsCount`; Agilidade → `LooseBallReaction`; Compostura → `PressureErrorMult` (decision noise under pressure).

| Section | Fields | Controls |
| --- | --- | --- |
| `rates` | `teamHz`, `roleHz`, `individualHz` | Layer frequencies (TECHNICAL_SPEC §7: 5 / 10 / 10 Hz) |
| `phases` | `transitionAttackSeconds`, `transitionDefenseSeconds`, `buildMaxBallFraction` | Transition lengths (GAME_DESIGN §24: 2–4 s / 2–3 s); in possession the team builds while the ball is below this fraction of the pitch, then attacks |
| `role` | `offsideMargin`, `goalkeeperDistance`, `goalkeeperLateralShift`, `sidelineMargin`, `errorResampleSeconds`, `correctionThreshold` | Offside limit, the goalkeeper's simple slot (real goalkeeper AI is A6), how often the positioning error is redrawn, which target jumps wait for `AiCorrectionDelay` |
| `individual` | `supportPlayers`, `supportCandidates`, `supportRadius`, `supportOpenness*`, `supportForwardWeight`, `supportShapeWeight`, `supportCommitSeconds`, `markZoneRadius`, `markGoalSideDistance`, `containDistance`, `minCommitSeconds`, `assignmentHysteresis` | Apoio (points on a ring around the carrier scored by openness, progress, staying near the shape), marcação por zona (goal-side), contenção without tackling (A5), anti-oscillation (minimum commitment, ~25% hysteresis on job assignments and on the marking zone) |
| `onBall` | `decisionIntervalSeconds`, `minDribbleSeconds`, `dribbleStep`, `dribbleHeadings`, `dribbleSpreadDegrees`, `dribbleClearance`, `passMinDistance`, `passMaxDistance`, `passLaneClearance`, `passDistanceRisk`, `receiverPressureRadius`, `shotRange`, `shotPower`, `shootChanceThreshold`, `lateralValuePenalty`, `progressWeight`, `dribbleRiskFactor`, `decisionNoise` | "Decisão com bola: chance de sucesso × valor da situação". Situation value = the clear-shot chance from that point, or a capped value of progress up the pitch; a chance at least `shootChanceThreshold` is shot at once |
| `motion` | `arriveRadius`, `restartRadius`, `sprintDistance` | Stop/start hysteresis around a target; sprint when far or pressing/chasing |

## defense.json — defense and control switching (A5, baseline v1, X-53)

Standing tackle **without dice** (GAME_DESIGN §21/§22: "1×1 sem dado: desarme só acerta se alcança a bola exposta"; user decision X-53): a defender wins the ball when it is within his reach (`Effect.TackleReach`, Desarme — replaces the former `TackleWinChance`) and he is closer to it than the carrier, who gets a head start from his body (`Effect.ShieldStrength`, Força). How far the carrier lets the ball run (`Effect.DribbleTouchDistance`, Drible) is what exposes it.

| Field | Controls | Valid values |
| --- | --- | --- |
| `tackle.shieldMeters` | Head start the carrier's body gives at full ShieldStrength (m) | ≥ 0 |
| `tackle.reTackleCooldownSeconds` | The dispossessed player cannot win it straight back for this long | ≥ 0 |
| `tackle.dispossessedStunSeconds` | The dispossessed carrier stumbles this long | ≥ 0 |
| `tackle.possessionSecureSeconds` | Right after winning the ball (tackle or first touch) the new carrier cannot be tackled for this long | ≥ 0 |
| `control.switchHysteresis` | Auto-switch only to a candidate this much better (GAME_DESIGN §17: ~25%) | 0–1 |
| `control.postManualLockSeconds` | No auto-switch after a manual one (GAME_DESIGN §17: 1 s) | ≥ 0 |
| `control.beatenDistance` | Controlled player "batido" once this far behind the carrier (GAME_DESIGN §17: 2 m) | > 0 |
| `control.behindBallPenalty` | Time-to-intercept penalty for a candidate on the wrong side of the ball ("ponderado por posição") | ≥ 0 |
| `control.autoSwitchCooldownSeconds` | Minimum time between two automatic switches | ≥ 0 |
| `control.intentionAlignment` | "Trava de intenção": no auto-switch while the stick points this close (cosine) to the ball | −1–1 |

## goalkeeper.json — goalkeepers (A6, baseline v1, X-54)

GAME_DESIGN §23. The attribute-dependent values come from the Effect catalog: `GkReactionTime` and `GkDiveReach` (Reflexo), `GkAngleError` (Posicionamento GK), `GkCatchChance` (Mãos). The keeper predicts the ball with the same `BallPhysics.Step` the ball uses, so he never dives the wrong way without a deflection.

| Field | Controls | Valid values |
| --- | --- | --- |
| `positioning.minDepth` | Closest the keeper ever stands to his goal line (m): never walks into the goal | > 0 |
| `positioning.maxDepth` | Depth on the bisector with the ball far away (m) | ≥ minDepth |
| `positioning.nearBallDistance` / `farBallDistance` | Ball distance to goal (m) where the depth is minDepth / full depth ("avança/recua com a bola") | far > near ≥ 0 |
| `positioning.sweeperLineFraction` / `sweeperMaxDepth` | Líbero: full depth grows to this fraction of the own defensive line's distance from goal, up to this many meters | ≥ 0; max ≥ maxDepth |
| `positioning.lateralLimit` | The target stays within the posts widened by this (m) | ≥ 0 |
| `positioning.errorResampleSeconds` | How often the GkAngleError offset is re-sampled | > 0 |
| `positioning.arriveRadius` / `sprintDistance` | Stops within / sprints beyond this distance of his spot (m) | > 0 |
| `reaction.predictionHorizonSeconds` | How far ahead a loose ball's path is predicted | > 0 |
| `reaction.threatMargin` | A ball predicted to miss by less than this still makes him react (m) | ≥ 0 |
| `reaction.unsetSpeed` / `unsetPenaltySeconds` | Moving faster than this when the shot leaves = not set: reacts this much later ("+ penalidades") | > 0 / ≥ 0 |
| `save.handReach` | Hands' horizontal reach from the body (m); GkDiveReach is the total, the dive moves the body the rest | > 0 |
| `save.reachHeight` | Highest ball he can touch (m) | > 0 |
| `save.diveSpeed` | Body speed during a dive (m/s): "alcance limitado, sem teletransporte" | > 0 |
| `save.getUpMinSeconds` / `getUpMaxSeconds` / `getUpPerReactionSecond` | Time on the ground after a dive = factor × GkReactionTime, clamped (GAME_DESIGN: 0.6–1.0 s) | ≥ 0, max ≥ min |
| `save.catchReferenceSpeed` / `catchSpeedSlope` / `catchSpeedMultMin` | Catch multiplier = 1 + (reference − ball speed) × slope, at least the minimum ("força") | > 0 / ≥ 0 / ≥ 0 |
| `save.diveCatchFactor` | Catch multiplier when the save needed a dive | 0–1 |
| `save.highBallHeight` / `highCatchFactor` | Above this height a catch is harder by this factor | ≥ 0 / 0–1 |
| `save.spinCatchPenalty` | Catch chance lost per unit of spin ("efeito") | ≥ 0 |
| `save.maxCatchChance` | Cap on the catch chance | 0–1 |
| `save.parryRestitution` | A parried ball keeps this fraction of its speed | 0 ≤ x < 1 |
| `save.parryMinAngleDegrees` / `parryMaxAngleDegrees` | Parry direction away from goal, angled to the side the ball was going | ordered, < 90 |
| `save.parryLift` | Upward speed of a parried ball (m/s) | ≥ 0 |
| `save.holdSeconds` | A caught ball is held this long before he rolls it to a teammate (placeholder until restarts, A7) | ≥ 0 |
