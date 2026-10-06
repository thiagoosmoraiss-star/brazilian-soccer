using System.Runtime.CompilerServices;

// Save reuses the reflection-free JSON helpers (StrictJson, D-11) to read a save body (B8); no other assembly can.
[assembly: InternalsVisibleTo("Save")]
// Tests.Save loads the real GameDatabase and uses StrictJson-backed fixtures the same way; no other assembly can.
[assembly: InternalsVisibleTo("Tests.Save")]
