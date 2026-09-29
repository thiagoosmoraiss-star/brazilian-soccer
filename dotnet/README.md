# dotnet/

Parallel .NET solution that compiles the pure sources **in place** from `Unity/Assets/_Game/<Assembly>/` (no copies) and runs the pure tests.

- Libraries target `netstandard2.1` with C# 9 (Unity 6 constraints); Unity is not referenced, so any `UnityEngine` usage in a pure assembly fails the build.
- `ProjectReference`s mirror the asmdefs and the dependency table in `docs/ARCHITECTURE.md` (checked by `ArchitectureTests`).

```bash
dotnet test dotnet/Game.sln
```
