using System;
using System.Text;
using Game.Career.Season;
using Game.Core.Results;
using Game.Save.IO;
using Game.Save.Serialization;
using Newtonsoft.Json.Linq;

namespace Game.Save
{
    /// <summary>
    /// Save/load for one slot (TECHNICAL_SPEC §14): atomic write (temp → validate → rename), 3 backups,
    /// corruption falls back to the most recent backup that still validates. The caller supplies
    /// <paramref name="savedAtUtc"/> (see <see cref="SaveHeader"/>: no real clock in the pure zone).
    /// </summary>
    public static class SaveService
    {
        private const string NotFound = "SAVE_NOT_FOUND";
        private const string Corrupt = "CORRUPT_SAVE";

        /// <param name="isSeasonStart">True right after a season transition (AutosavePolicy.IsSeasonStart):
        /// also refreshes the season-start backup.</param>
        public static Result Save(IFileStore store, string slot, CareerState state, DateTime savedAtUtc, bool isSeasonStart)
        {
            var bodyJson = CareerStateWriter.Write(state);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson.ToString(Newtonsoft.Json.Formatting.None));
            byte[] compressed = Gzip.Compress(bodyBytes);
            string checksum = SaveChecksum.Compute(bodyBytes);
            var header = new SaveHeader(SaveSchema.CurrentVersion, savedAtUtc, state.Seed, state.Season?.Year ?? 0, state.ManagedClubId, checksum);

            string metaPath = SaveSlotPaths.Meta(slot), bodyPath = SaveSlotPaths.Body(slot);
            string tmpMeta = metaPath + ".tmp", tmpBody = bodyPath + ".tmp";
            store.WriteAllBytes(tmpBody, compressed);
            store.WriteAllBytes(tmpMeta, header.ToJsonBytes());

            var validated = TryLoadPaths(store, tmpMeta, tmpBody);
            if (!validated.IsSuccess)
            {
                store.Delete(tmpBody);
                store.Delete(tmpMeta);
                return Result.Fail(validated.Errors);
            }

            RotateBackups(store, slot);
            store.Move(tmpBody, bodyPath);
            store.Move(tmpMeta, metaPath);

            if (isSeasonStart)
            {
                store.Copy(bodyPath, SaveSlotPaths.BodyBackup(slot, "season"));
                store.Copy(metaPath, SaveSlotPaths.MetaBackup(slot, "season"));
            }
            return Result.Success;
        }

        /// <summary>Loads the current slot; falls back to "last", then "previous", then "season" if it is missing
        /// or fails validation (corrupted or interrupted write). Reports the primary attempt's errors if every
        /// copy fails.</summary>
        public static Result<CareerState> Load(IFileStore store, string slot)
        {
            var primary = TryLoadPaths(store, SaveSlotPaths.Meta(slot), SaveSlotPaths.Body(slot));
            if (primary.IsSuccess) return primary;
            foreach (var suffix in new[] { "last", "previous", "season" })
            {
                var fallback = TryLoadPaths(store, SaveSlotPaths.MetaBackup(slot, suffix), SaveSlotPaths.BodyBackup(slot, suffix));
                if (fallback.IsSuccess) return fallback;
            }
            return primary;
        }

        private static void RotateBackups(IFileStore store, string slot)
        {
            if (store.Exists(SaveSlotPaths.BodyBackup(slot, "last")))
            {
                store.Move(SaveSlotPaths.BodyBackup(slot, "last"), SaveSlotPaths.BodyBackup(slot, "previous"));
                store.Move(SaveSlotPaths.MetaBackup(slot, "last"), SaveSlotPaths.MetaBackup(slot, "previous"));
            }
            if (store.Exists(SaveSlotPaths.Body(slot)))
            {
                store.Copy(SaveSlotPaths.Body(slot), SaveSlotPaths.BodyBackup(slot, "last"));
                store.Copy(SaveSlotPaths.Meta(slot), SaveSlotPaths.MetaBackup(slot, "last"));
            }
        }

        private static Result<CareerState> TryLoadPaths(IFileStore store, string metaPath, string bodyPath)
        {
            if (!store.Exists(metaPath) || !store.Exists(bodyPath))
                return Result<CareerState>.Fail(NotFound, $"{metaPath}/{bodyPath}: not found.");

            var headerResult = SaveHeader.FromJsonBytes(store.ReadAllBytes(metaPath));
            if (!headerResult.IsSuccess) return Result<CareerState>.Fail(headerResult.Errors);

            byte[] bodyBytes;
            try { bodyBytes = Gzip.Decompress(store.ReadAllBytes(bodyPath)); }
            catch (Exception e) { return Result<CareerState>.Fail(Corrupt, $"{bodyPath}: {e.Message}"); }

            string checksum = SaveChecksum.Compute(bodyBytes);
            if (checksum != headerResult.Value.Checksum)
                return Result<CareerState>.Fail(Corrupt, $"{bodyPath}: checksum does not match {metaPath}.");

            JObject bodyJson;
            try { bodyJson = SaveJson.Parse(Encoding.UTF8.GetString(bodyBytes)); }
            catch (Exception e) { return Result<CareerState>.Fail(Corrupt, $"{bodyPath}: {e.Message}"); }
            if (bodyJson == null) return Result<CareerState>.Fail(Corrupt, $"{bodyPath}: root must be an object.");

            int fromVersion = bodyJson["schemaVersion"]?.Type == JTokenType.Integer ? bodyJson["schemaVersion"].Value<int>() : -1;
            var migrated = SaveMigrations.MigrateToCurrent(bodyJson, fromVersion);
            if (!migrated.IsSuccess) return Result<CareerState>.Fail(migrated.Errors);

            var stateResult = CareerStateReader.Read(migrated.Value);
            if (!stateResult.IsSuccess) return Result<CareerState>.Fail(stateResult.Errors);

            var validation = SaveValidator.Validate(stateResult.Value);
            if (!validation.IsSuccess) return Result<CareerState>.Fail(validation.Errors);

            return stateResult;
        }
    }
}
