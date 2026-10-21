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
| `substitutions` | AI windows (half-time + stoppages), max stoppages, energy threshold, max tired subs, bench margin. Read by `Rules.Match.Substitutions` for both engines (A9c, X-77): QuickSim by its minutes, the played match by the displayed clock (the AI substitutes at the first dead ball after a window); the user's changes use the same limits (half-time does not count as a stoppage) | Increasing windows |

## quicksim.json — QuickSim coefficients (B2, baseline v1, X-38)

Minute slices; possession by Creation strength; dangerous-attack chance by Attack vs Defense; play types (open play, cross, long shot) with on-target and goal-given-on-target chances; blocks, corners, penalties; selection weights per position for shooters, headers, assists, crossers and foulers; tactic multipliers (mentality, line, pressure); home advantage; probability caps. Calibrated so league-like batches hit `Data/TestRanges/quicksim.json`; fouls and penalties follow the game rhythm of X-65 (~10 fouls, ~2 yellows per match, as in the played match). Inspect with `dotnet run --project dotnet/QuickSimReport -- --matches 10000`.

**Calibration against the played match (C2, X-84–X-87):** the same matchups in both engines, compared with the tolerances of `Data/TestRanges/calibration.json` (`Tests/Simulation/CalibrationTests`; side-by-side table with `dotnet run --project dotnet/MatchReport -c Release -- --calibration [--groups-only | --sensitivity]`). `composureExponent` is 0.23 since X-87, so the steeper `PressureErrorMult` keeps the same Compostura effect in the QuickSim.

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
| `interceptReferenceSpeed` | A ball last kicked by the other side is caught within the full capture radius only up to this speed (m/s); faster, the radius shrinks in proportion (A7a, X-57) | > 0 |
| `interceptMinRadiusFraction` | Floor of that shrinking, as a fraction of the capture radius | 0 < x ≤ 1 |
| `turnNoLossMaxDegrees` / `turnMediumLossMaxDegrees` | Turn-angle bands (GAME_DESIGN §18: ≤45° no loss, 45-90° medium, >90° sharp) | medium > no-loss |
| `mediumTurnSpeedLossFlat` | Flat speed loss for a 45-90° turn (15-30%); sharper turns use `Effect.TurnSpeedLoss`/`TurnSpeedLossWithBall` (40-60%) instead | 0–1 |
| `withBallSpeedPenaltyShortTouch` / `withBallSpeedPenaltyLongTouch` | Dribble speed penalty, short vs. long touch (GAME_DESIGN §18: -10% to -5%) | 0–1 |
| `sprintBurstTouchMultiplier` | How much farther the ball sits ahead during a sprint burst ("arrancada, toque longo") | ≥ 1 |
| `feintPulseMaxSeconds` | Body-feint input window (GAME_DESIGN §21: < 0.25 s) | > 0 |
| `sprintMemorySeconds` | Sprint keeps going this long after release if the stick is still pushed (GAME_DESIGN §17: 0.4 s) | ≥ 0 |
| `inputBufferSeconds` | Input buffer (GAME_DESIGN §17: ~150 ms) | ≥ 0 |
| `stopSpeedEpsilon` | Speed below which a player is considered stopped | > 0 |
| `turnStumbleMinSpeed` | A sharp turn only stumbles (speed loss + recovery time) a player moving at least this fast (m/s); a player all but stopped turns freely (X-69). The step a stumble ends it has absorbed the turn: no second stumble on the same turn (X-73) | ≥ 0 |

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
| `pressureRadius` | Opponent closer than this = pressure (GAME_DESIGN: < 2 m; × `PressureErrorMult` 1.3–1.8, steepest between 50 and 80, X-87) | > 0 |
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

TECHNICAL_SPEC §9 "Parâmetros afetados (`tactics.json`)". The **default** block is the tactic (3, 2, 2); `modifiers` (A9c, X-78) changes it by level: mentality moves the block and the depth and the on-ball risk, the line moves the defensive line, pressure sets pressers, trigger distance and counter-press; `changeSmoothSeconds` eases a change made in play; `formationRolePenalty` weighs position fit when a formation change re-maps the players. Distances are meters in the team's own frame (from its own goal line along its attack).

| Field | Controls | Valid values |
| --- | --- | --- |
| `default.phases.<build/attack/transitionAttack/transitionDefense/defend>.depth` | Defensive line → most advanced line (compactação; GAME_DESIGN §24: 10–15 m between lines defending, 20–25 m attacking) | > 0 |
| `….lineBehindBall` | How far behind the ball the defensive line sits | ≥ 0 |
| `….maxLine` | Highest the defensive line may push | > `minLine` |
| `….widthScale` | Lateral spread of the formation (1 = full width) | 0–1 |
| `default.minLine` | Deepest the defensive line may drop | > 0 |
| `default.lateralShift` | Fraction of the ball's lateral position the block follows (GAME_DESIGN §24: ~40–60%) | 0–1 |
| `default.pressers` / `pressTriggerDistance` | Players who press the carrier, and from how far | ≥ 0 / > 0 |
| `modifiers.mentality.pressedOutletValue` | D-37: added to a lançamento's value when the carrier is pressed in his own third (the defensive mentality gets it long instead of losing it near its goal); 0 = no preference | 5 values ≥ 0 |
| `modifiers.mentality.boxAttackersOffset` | D-37: box attackers added to `ai.json` `individual.boxAttackers` (the offensive mentality commits more players forward, the defensive fewer) | 5 integers |

## ai.json — match AI coefficients (A4, baseline v1, X-52)

TECHNICAL_SPEC §7 / GAME_DESIGN §24. Attribute-dependent values come from `effects.json`: Posicionamento → `AiTargetError`, `AiCorrectionDelay`; Visão → `AiPassOptionsCount`; Agilidade → `LooseBallReaction`; Compostura → `PressureErrorMult` (decision noise under pressure).

| Section | Fields | Controls |
| --- | --- | --- |
| `rates` | `teamHz`, `roleHz`, `individualHz` | Layer frequencies (TECHNICAL_SPEC §7: 5 / 10 / 10 Hz) |
| `phases` | `transitionAttackSeconds`, `transitionDefenseSeconds`, `buildMaxBallFraction` | Transition lengths (GAME_DESIGN §24: 2–4 s / 2–3 s); in possession the team builds while the ball is below this fraction of the pitch, then attacks |
| `role` | `offsideMargin`, `goalkeeperDistance`, `goalkeeperLateralShift`, `sidelineMargin`, `errorResampleSeconds`, `correctionThreshold` | Offside limit, the goalkeeper's simple slot (real goalkeeper AI is A6), how often the positioning error is redrawn, which target jumps wait for `AiCorrectionDelay` |
| `individual` | `supportPlayers`, `supportCandidates`, `supportRadius`, `supportOpenness*`, `supportForwardWeight`, `supportShapeWeight`, `supportCommitSeconds`, `markZoneRadius`, `markGoalSideDistance`, `containDistance`, `minCommitSeconds`, `assignmentHysteresis`, `interceptHorizonSeconds`, `interceptStepSeconds`, `boxAttackers`, `boxAttackFraction`, `boxAttackDepth`, `boxAttackHalfWidth` | **Atacar a área (F1b, X-115):** with the ball past `boxAttackFraction` of the pitch, the `boxAttackers` most advanced slots (never the carrier) go into the box, `boxAttackDepth` m from the goal line (never offside) and within `boxAttackHalfWidth` m of the middle, instead of supporting; 0 attackers = off. Apoio (points on a ring around the carrier scored by openness, progress, staying near the shape), marcação por zona (goal-side), contenção without tackling (A5), anti-oscillation (minimum commitment, ~25% hysteresis on job assignments and on the marking zone); a player going for a moving ball runs to where he meets it, searched this far ahead in these steps (A7a) |
| `onBall` | `decisionIntervalSeconds`, `minDribbleSeconds`, `dribbleStep`, `dribbleHeadings`, `dribbleSpreadDegrees`, `dribbleClearance`, `passMinDistance`, `passMaxDistance`, `passLaneClearance`, `passDistanceRisk`, `receiverPressureRadius`, `shotRange`, `shotPower`, `shootChanceThreshold`, `lateralValuePenalty`, `progressWeight`, `dribbleRiskFactor`, `decisionNoise`, `possessionValue` | **`possessionValue` (F1b, X-115):** the value of still having the ball, added to the situation value of every option that keeps it (pass, dribble, through ball, lançamento) and so weighted by its chance of success; 0 = the A4 rule. "Decisão com bola: chance de sucesso × valor da situação". Situation value = the clear-shot chance from that point, or a capped value of progress up the pitch; a chance at least `shootChanceThreshold` is shot at once |
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

## restarts.json — clock and simple restarts (A7a, baseline v1, X-57)

TECHNICAL_SPEC §19: "Reinícios simples: saída, lateral, tiro de meta, escanteio curto"; "placar, relógio, 2×2 min". The real duration comes from `MatchSetup.DurationMinutes` (the vertical slice uses 4 = 2×2 min); the clock shows the accelerated 90 minutes.

| Field | Controls | Valid values |
| --- | --- | --- |
| `clock.halves` | Number of halves (the other side kicks off each new half) | ≥ 1 |
| `clock.displayMinutesPerHalf` | Minutes the clock shows per half | > 0 |
| `restart.setupSeconds` | Dead ball → taker at the ball (he runs there meanwhile; whoever is still on the way is placed) | ≥ 0 |
| `restart.aiTakeSeconds` | An AI taker plays the ball this long after he is ready | ≥ 0 |
| `restart.humanTimeoutSeconds` | A user who does nothing for this long has the restart taken for him (never stalls) | > 0 |
| `restart.exclusionRadius` | Opponents keep this far from the ball until it is played (kick-off, goal kick, corner) (m) | ≥ 0 |
| `restart.throwInExclusionRadius` | Same for a throw-in (m) | ≥ 0 |
| `restart.lineInset` | A throw-in is taken this far inside the touchline (m); players leaving the circle never cross a line | ≥ 0 |
| `restart.goalKickDepth` / `goalKickLateral` | Goal-kick spot: this far from the goal line, this far to the side the ball went out (m) | > 0 / ≥ 0 |
| `restart.cornerInset` | Corner spot inset from both lines (m) | ≥ 0 |

## referee.json — referee and full set pieces (A8a, baseline v1, X-64/X-65/X-66)

Fouls on contact before the ball, cards, offside at the pass, free kicks with an automatic wall and penalties (`Game.Data.Match.RefereeDefinition`). The per-foul card chances are the shared `match_rules.json` `discipline` (same as QuickSim). Target rhythm (X-65): ~10 fouls and ~2 yellows per played match, whatever its duration; ranges in `Data/TestRanges/referee.json`; inspect with `dotnet run --project dotnet/MatchReport -- --baseline-only` (and `--penalties N` for isolated penalties).

| Field | Controls | Valid values |
| --- | --- | --- |
| `foul.strictness` | The referee's strictness: multiplies every foul chance | ≥ 0 |
| `foul.foulScale` | Calibrates the FoulChance effect (per challenge) to the rhythm of X-65; 2.4 → 3.0 in F1b (X-115), consolidated by D-38 (X-117; do not raise it again without new evidence): at the new default duration (4 min) the played match had ~6.9 fouls, below X-65 (the per-challenge chance saturates when it is scaled up for short matches) | ≥ 0 |
| `foul.contactDistance` | A defender this close to the carrier (centre to centre, m), still closing in, makes a challenge | > 0 |
| `foul.minClosingSpeed` | Only a defender closing at least this fast (m/s) challenges; a jockeying one never fouls | ≥ 0 |
| `foul.speedReference` / `speedMultiplier` | Closing speed above the minimum (m/s) that adds the whole multiplier to the foul chance | > 0 / ≥ 0 |
| `foul.behindAngleDegrees` / `behindMultiplier` | A challenge from more than this angle off the carrier's facing is from behind: chance × multiplier | (0, 180] / ≥ 1 |
| `foul.challengeCooldownSeconds` | After a challenge the defender cannot challenge again for this long (s) | ≥ 0 |
| `foul.minPlayersOnPitch` | A side is never reduced below this many players by dismissals (Law 3) | 1–11 |
| `cards.denialDistance` | A foul by the last defender this close to his goal (m) denies a clear chance: straight red outside the area, penalty + yellow inside | ≥ 0 |
| `offside.tolerance` | How far ahead of the second-last defender and the ball a player may be and still be level (m) | ≥ 0 |
| `offside.closeCallWindow` / `closeCallRunSpeed` | Close call (X-68): an attacker up to this far behind the second-last defender (m), running forward at least this fast (m/s) as the ball is played, may have gone too early | ≥ 0 |
| `offside.closeCallChance` | …with this chance per metre of his AiTargetError (Posicionamento), normalised by the match duration | ≥ 0 |
| `freeKick.setupSeconds` | Set-up time of a free kick (s) | ≥ 0 |
| `freeKick.directShotDistance` / `directShotMaxAngleDegrees` | The AI shoots a direct free kick this close to goal (m) and within this angle of the goal's centre line; otherwise passes | ≥ 0 |
| `freeKick.shotPower` | Power of the AI's free-kick shot | (0, 1] |
| `freeKick.wallDistanceMargin` / `wallSpacing` | The wall stands at the exclusion radius + margin, its players this far apart (m) | ≥ 0 / > 0 |
| `freeKick.wall` | Wall size by distance to goal: rows `{maxDistance, players}`, ascending; beyond the last row, no wall | 0–10 players |
| `penalty.setupSeconds` | Set-up time of a penalty (s) | ≥ 0 |
| `penalty.spotDistance` | Penalty spot distance from the goal line (m) | > 0 |
| `penalty.shotPower` | Power of the AI's penalty | (0, 1] |
| `penalty.keeperStayChance` | The AI keeper stays in the middle with this chance; otherwise he dives to a side as the ball is struck | 0–1 |
| `penalty.keeperDiveLateral` | A diving keeper aims this far from the goal's centre (m) | ≥ 0 |
| `penalty.areaClearance` | Players wait this far outside the penalty area (m) | ≥ 0 |
| `penalty.userDiveDeadzone` | Defending a penalty, the user's stick must point at least this far across the goal (0-1) for his keeper to dive that way; less, he stays in the middle (A8b) | 0 ≤ x < 1 |

## aerial.json — the high ball (A9a, baseline v1, X-73)

"Alto" (lançamento and cruzamento alto, GAME_DESIGN §19), the header and its aerial duel (§20), the keeper and crosses (§23) and the AI's use of high balls. Attribute effects: CrossAngleError, PassAngleError, PassPowerError (the flight), AerialDuel (Cabeceio 0.7 + Força 0.3, exponential curve: +10 points ≈ ×1.43 in the duel weight, X-87), HeaderAccuracy, HeaderPower, CrossQuality (AI), GkCrossClaimRange, GkCrossDecisionQuality (keeper). The launch speed is solved in closed form under the same gravity and linear air drag the ball physics integrates (`HighBallSystem`). Ranges in `Data/TestRanges/aerial.json`.

| Field | Controls | Valid values |
| --- | --- | --- |
| `high.longElevationDegrees` / `crossElevationDegrees` | Launch elevation of a lançamento (lands at the target's feet) and of a cross (arrives at `crossArrivalHeight`) (deg) | (0, 80] |
| `high.crossArrivalHeight` | Height a cross arrives at its target (m) | ≥ 0 |
| `high.minDistance` / `maxDistance` | A held Alto sends the ball this far between empty and full bar (m) | max > min > 0 |
| `high.maxTargetDistance` | The Semi cone looks this far for a high-ball target (m) | > 0 |
| `high.spaceDistance` | A tapped Alto with nobody in the cone goes this far into space (m) | > 0 |
| `high.maxLaunchSpeed` | Cap on the launch speed (m/s) | > 0 |
| `high.longErrorMultiplier` | A lançamento's direction error = PassAngleError × this (a cross uses CrossAngleError) | ≥ 0 |
| `high.powerErrorScale` | PassPowerError applied to the launch speed, times this | ≥ 0 |
| `high.crossZoneDepth` / `crossZoneMinLateral` | Cross zone: within this far of the goal line (m) and at least this far from the centre line (m) | > 0 / ≥ 0 |
| `high.crossDefaultDepth` | A cross with nobody in the cone aims at the centre of the goal this far out (m) | > 0 |
| `high.throwInMaxDistance` | A long throw-in goes no farther (m) | > 0 |
| `header.reachHeight` | Highest ball a jumping player meets with his head (m); below the capture height it is feet | > 0 |
| `header.radius` | Horizontal distance within which a player contests a dropping ball (m) | > 0 |
| `header.closenessWeight` | In a duel, being closer adds up to this much to the AerialDuel weight | 0–1 |
| `header.shotRange` | An attacker this close to the goal heads at it (m) | ≥ 0 |
| `header.shotTargetHeight` / `verticalErrorFraction` | Header shots aim at this height; vertical error = HeaderAccuracy × fraction | ≥ 0 |
| `header.passSpeedFactor` / `passElevationDegrees` | A headed pass leaves at HeaderPower × factor, at this elevation | (0, 1] / (0, 80] |
| `header.clearanceElevationDegrees` / `clearanceSpreadDegrees` | A defender's clearance: elevation, and up to this far off straight away from his goal | (0, 80] / 0–90 |
| `ai.crossValue` | Weight of a cross in the AI's on-ball decision (× CrossQuality × teammates in the box; a quarter when nobody is there and the crosser is pressed) | ≥ 0 |
| `ai.longBallMinDistance` / `longBallValue` | The AI plays a lançamento only to a teammate at least this far away and further up, with this weight (× openness × threat) | ≥ 0 |
| `ai.dropZonePlayers` | Besides the receiver/chaser, this many of each side's nearest players go for a high ball's drop zone | ≥ 0 |
| `ai.cornerCrossChance` / `goalKickLongChance` | The AI crosses this share of its corners and goes long from this share of its goal kicks | 0–1 |


## duels.json — defence and attack duels (A9b, baseline v1, X-75)

**Shot block** (`shotBlock`, F1b, X-116): a shot in flight passing within `reach` m of an outfield defender, below `maxHeight` and faster than `minSpeed`, is blocked with his `ShotBlockChance` (Desarme) × `chanceScale` × closeness, once per defender per shot; the ball leaves at `speedFactorMin`–`speedFactorMax` of its speed, turned by up to `maxDeflectDegrees` and up to `maxUpSpeed` m/s upwards (a corner if it goes over the goal line). `chanceScale` 0 = no physical blocks; 1 = the effect curve as it is.

**First touch** (`firstTouch`, D-36): a player capturing a ball that arrives faster than `minSpeed` m/s makes a touch error of `FirstTouchError` (Drible 0,7 + Compostura 0,3, `effects.json`) × `scale` × the speed factor (ball speed / `referenceSpeed`, clamped to `speedFactorMin`–`speedFactorMax`) × `PressureErrorMult` (Compostura) when an opponent is within `pressureRadius` m × |triangular|; above `threshold` m the touch escapes: the ball runs on along its line, turned by up to `maxEscapeDegrees`, about error × `escapeDistanceScale` m, loose for anyone. The same for the user's player and the AI; goalkeepers use their hands. `scale` 0 = every reception clean (the rule before D-36). Measured (4 min, 1 200 matches): 1,9% of receptions escape on average, ~4,4% for sides of OVR 45–50 and ≤0,3% from OVR 75 up.

Carrinho (`slide`: distance, speed, leg reach = TackleReach × reachFactor, poke speed, time on the ground, contact distance for the "contato antes da bola" foul, user aim assist, booking chance from behind, AI hazard and approach angle), the second defender "Pressionar" (`secondDefender`), the basic body duel (`body`: loose-ball contest by BodyDuelWin and closeness, separation by strength), cover behind the presser (`cover`), runs in behind (`runs`: space needed, space reference against the carrier's RunTriggerThreshold/Visão, depth, duration, onside margin, cooldowns, value of the through ball) and the keeper's 1×1 rush (`keeperRush`: range, smother reach = GkDiveReach × factor, hesitation after a wrong GkRushDecisionQuality call, through-ball time margin). Ranges in `Data/TestRanges/duels.json`.

## difficulty.json — match difficulty (F1c, D-23)

Three levels, easiest first: `easy` (Fácil), `normal` (Normal) and `hard` (Difícil); `reference` is the level that reproduces the AI without difficulty and must have every factor exactly 1 (D-23a; the reader rejects anything else); `default` is the level of a new career. A level acts only on the **user's opponent**, never on attributes (GAME_DESIGN §24): `match` multiplies the carrier's decision noise (`decisionNoiseScale` × `ai.json` onBall.decisionNoise) and decision interval (`decisionIntervalScale`), the loose-ball reaction and the delay before a big positional correction (`reactionScale` × LooseBallReaction and AiCorrectionDelay), the positioning error (`positionErrorScale` × AiTargetError) and the goalkeeper's reaction to a shot (`keeperReactionScale`); `quickSim.opponentStrengthScale` multiplies the opponent's sector strengths in the QuickSim of the user's match only (D-23b). Every other match of the career, the calibration and the tools run without difficulty. Initial values (F1c, X-120) measured on 400 matches at 4 min, the user's side the reference AI: the user wins 37% / 22% / 14,5% at Fácil / Normal / Difícil. Ranges in `Data/TestRanges/difficulty.json`.
