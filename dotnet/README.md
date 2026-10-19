# dotnet/

Parallel .NET solution that compiles the pure sources **in place** from `Unity/Assets/_Game/<Assembly>/` (no copies) and runs the pure tests.

- Libraries target `netstandard2.1` with C# 9 (Unity 6 constraints); Unity is not referenced, so any `UnityEngine` usage in a pure assembly fails the build.
- `ProjectReference`s mirror the asmdefs and the dependency table in `docs/ARCHITECTURE.md` (checked by `ArchitectureTests`).

```bash
dotnet test dotnet/Game.sln
```

## Calibration QuickSim × played match (C2)

`Tests.Simulation/CalibrationTests` (category `Slow`) runs the same matchups in both engines and fails outside the tolerances of `Data/TestRanges/calibration.json` (DECISIONS X-84–X-86). It is part of `dotnet test` and blocks merge. Alone (~15 min in Debug on 12 cores, most of it the 400 + 6 × 400 played matches):

```bash
dotnet test dotnet/Tests.Simulation --filter "FullyQualifiedName~CalibrationTests"
```

Side-by-side table (Release, ~1 min; `--groups-only` adds the attribute groups, ~3 min; `--sensitivity` also every attribute, slow):

```bash
dotnet run --project dotnet/MatchReport -c Release -- --calibration [--played N] [--quick N] [--groups-only | --sensitivity]
```

## UI (C3a)

`UI.Presenters` is the pure half of the Shell (career session, navigation, screen presenters; DECISIONS X-96). `Tests.UI` drives the screens through their commands, as a player would: new career and club choice, a whole season with at most three taps from one match to the next, a played match coming back as a post-match report, the duration setting, every screen's texts present in `Data/Strings/pt-BR.json`; C3b: squad list, player profiles, lineup and tactic edited by taps and used by the next match (X-97).

```
dotnet test dotnet/Tests.UI
```
