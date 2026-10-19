using Game.Core.Results;
using Game.Save.Serialization;
using Newtonsoft.Json.Linq;

namespace Game.Save
{
    /// <summary>
    /// The migration chain (TECHNICAL_SPEC §14: "Cadeia vN → vN+1 sobre JSON bruto; fixture + teste por migração").
    /// v1 → v2 (C1): <c>matchInProgress</c> (null). v2 → v3 (C3b): <c>userPlan</c> (null). Each future bump adds a step here, a fixture under
    /// <c>dotnet/Tests.Save/Fixtures</c> and a test that migrates it (B8, X-46).
    /// </summary>
    public static class SaveMigrations
    {
        private const string UnsupportedSchemaVersion = "UNSUPPORTED_SCHEMA_VERSION";

        public static Result<JObject> MigrateToCurrent(JObject body, int fromVersion)
        {
            if (fromVersion > SaveSchema.CurrentVersion)
                return Result<JObject>.Fail(UnsupportedSchemaVersion,
                    $"save: schemaVersion {fromVersion} is newer than this build supports ({SaveSchema.CurrentVersion}).");

            int version = fromVersion;
            var current = body;
            // Each step bumps `version` by one and rewrites `current` in place.
            if (version == 1)
            {
                // v2 (C1): a match in progress can be saved with the career; a v1 save never had one.
                current["matchInProgress"] = JValue.CreateNull();
                current["schemaVersion"] = 2;
                version = 2;
            }

            if (version == 2)
            {
                // v3 (C3b): the user's tactic and lineup; a v2 career played with the automatic ones.
                current["userPlan"] = JValue.CreateNull();
                current["schemaVersion"] = 3;
                version = 3;
            }

            if (version != SaveSchema.CurrentVersion)
                return Result<JObject>.Fail(UnsupportedSchemaVersion,
                    $"save: no migration path from schemaVersion {fromVersion} to {SaveSchema.CurrentVersion}.");
            return Result<JObject>.Ok(current);
        }
    }
}
