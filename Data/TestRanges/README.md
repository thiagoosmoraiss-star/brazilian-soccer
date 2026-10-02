# Data/TestRanges

Acceptance ranges used by tests (TEST_PLAN: ranges live in versioned data, not in test code).

| File | Used by | Source |
| --- | --- | --- |
| `world.json` | `Tests/Career/WorldGeneratorTests` | GAME_DESIGN §12 (squad OVR per division: D 45–55, C 55–62, B 62–70, A 70–85), §3 (22 players, 2–3 promising youths, 1–2 veterans); list of seeds for the multi-seed tests |
| `quicksim.json` | `Tests/Simulation` | Real-football references with sources inside the file (Brasileirão, Premier League); design estimates (reds, injuries, OVR curve, cross-division upsets) are flagged as such; league batch, sensitivity and performance settings |
