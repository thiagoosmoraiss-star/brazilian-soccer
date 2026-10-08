# Data/TestRanges

Acceptance ranges used by tests (TEST_PLAN: ranges live in versioned data, not in test code).

| File | Used by | Source |
| --- | --- | --- |
| `world.json` | `Tests/Career/WorldGeneratorTests`, `SeasonTests` (career seeds and number of seasons) | GAME_DESIGN §12 (squad OVR per division: D 45–55, C 55–62, B 62–70, A 70–85), §3 (22 players, 2–3 promising youths, 1–2 veterans); list of seeds for the multi-seed tests |
| `quicksim.json` | `Tests/Simulation` | Real-football references with sources inside the file (Brasileirão, Premier League); design estimates (reds, injuries, OVR curve, cross-division upsets) are flagged as such; league batch, sensitivity and performance settings |
| `development.json` | `Tests/Career/PlayerDevelopmentTests`, `SeasonTests` | TEST_PLAN Progressão and B4 acceptance; design estimates (young prospect by 24, veteran decline, 10-season OVR drift) |
| `kicking.json` | `Tests/Unit/PassScenarioTests`, `ShotScenarioTests` (A3) | ROADMAP A3 acceptance and GAME_DESIGN §19/§20 ranges; design estimates flagged inside the file |
| `ai.json` | `Tests/Unit/AiMatchTests` (A4) | TEST_PLAN §IA (oscillation, bunching, compactness, positioning) and GAME_DESIGN §24 compactness; design estimates flagged inside the file; match seeds and settings |
| `defense.json` | `Tests/Unit/ControlSwitchTests` (A5) | GAME_DESIGN §17 switching rules and TEST_PLAN acceptance; design estimates flagged inside the file; match seeds |
| `vertical_slice.json` | `Tests/Unit/VerticalSliceTests` (A7a) | TECHNICAL_SPEC §19 (teams ~70 / ~50, headless strong × weak, sensitivity); the full 200-match acceptance is printed by `dotnet/MatchReport` |
| `goalkeeper.json` | `Tests/Unit/GoalkeeperTests` (A6) | GAME_DESIGN §23 and TEST_PLAN (weak central shot saved, out of reach never touched, reaction by Reflexo); plausibility save rate is a design estimate flagged inside the file; match seeds |
