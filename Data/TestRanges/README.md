# Data/TestRanges

Acceptance ranges used by tests (TEST_PLAN: ranges live in versioned data, not in test code).

| File | Used by | Source |
| --- | --- | --- |
| `world.json` | `Tests/Career/WorldGeneratorTests`, `SeasonTests` (career seeds and number of seasons) | GAME_DESIGN §12 (squad OVR per division: D 45–55, C 55–62, B 62–70, A 70–85), §3 (22 players, 2–3 promising youths, 1–2 veterans); list of seeds for the multi-seed tests |
| `quicksim.json` | `Tests/Simulation` | Real-football references with sources inside the file (Brasileirão, Premier League); fouls and cards follow the game rhythm of X-65; design estimates (reds, injuries, OVR curve, cross-division upsets) are flagged as such; league batch, sensitivity and performance settings |
| `development.json` | `Tests/Career/PlayerDevelopmentTests`, `SeasonTests` | TEST_PLAN Progressão and B4 acceptance; design estimates (young prospect by 24, veteran decline, 10-season OVR drift) |
| `kicking.json` | `Tests/Unit/PassScenarioTests`, `ShotScenarioTests` (A3) | ROADMAP A3 acceptance and GAME_DESIGN §19/§20 ranges; design estimates flagged inside the file |
| `ai.json` | `Tests/Unit/AiMatchTests` (A4) | TEST_PLAN §IA (oscillation, bunching, compactness, positioning) and GAME_DESIGN §24 compactness; design estimates flagged inside the file; match seeds and settings |
| `defense.json` | `Tests/Unit/ControlSwitchTests` (A5) | GAME_DESIGN §17 switching rules and TEST_PLAN acceptance; design estimates flagged inside the file; match seeds |
| `vertical_slice.json` | `Tests/Unit/VerticalSliceTests` (A7a) | TECHNICAL_SPEC §19 (teams ~70 / ~50, headless strong × weak, sensitivity); the full 200-match acceptance is printed by `dotnet/MatchReport` |
| `goalkeeper.json` | `Tests/Unit/GoalkeeperTests` (A6) | GAME_DESIGN §23 and TEST_PLAN (weak central shot saved, out of reach never touched, reaction by Reflexo); plausibility save rate is a design estimate flagged inside the file; match seeds |
| `referee.json` | `Tests/Unit/RefereeTests` (A8a) | DECISIONS X-65 (game rhythm ~10 fouls / ~2 yellows per played match, same as QuickSim); penalty conversion and penalties per match are estimates flagged inside the file; the test-only strict discipline |
| `aerial.json` | `Tests/Unit/AerialTests` (A9a) | Closed-form flight vs the integrated ball (design), the aerial duel rule (GAME_DESIGN §20, design estimate for "most"), headless crosses/headers (design estimates) |
| `substitutions.json` | `Tests/Unit/SubstitutionTests` (A9c) | GAME_DESIGN §27 (5 trocas em 3 paradas + intervalo) and TEST_PLAN (11 or fewer on the pitch, substitutions within the limit); the substitute's own attributes and the headless AI substitutions per match are design estimates flagged inside the file; seeds searched for a dismissal |
| `duels.json` | `Tests/Unit/DuelsTests` (A9b) | GAME_DESIGN §22/§23 (carrinho 2-3 m, foul on contact before the ball, booked from behind, 1 s on the ground; keeper rushes 1×1 and comes for a through ball only if first); body duel and headless slides/runs/rushes are design estimates flagged inside the file |
