using Game.Core.Results;
using Game.Save.Serialization;
using Newtonsoft.Json.Linq;

namespace Game.Save
{
    /// <summary>
    /// The migration chain (TECHNICAL_SPEC §14: "Cadeia vN → vN+1 sobre JSON bruto; fixture + teste por migração").
    /// <see cref="SaveSchema.CurrentVersion"/> is still 1, so there is nothing to migrate yet: this is the
    /// mechanism a future schema bump plugs into. Adding v2 means adding a step here, a
    /// <c>Tests/Save/Fixtures/v1_to_v2_*.json</c> fixture and a test that migrates it (B8, X-46).
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
            // Future steps go here, each bumping `version` by one and rewriting `current` in place.

            if (version != SaveSchema.CurrentVersion)
                return Result<JObject>.Fail(UnsupportedSchemaVersion,
                    $"save: no migration path from schemaVersion {fromVersion} to {SaveSchema.CurrentVersion}.");
            return Result<JObject>.Ok(current);
        }
    }
}
