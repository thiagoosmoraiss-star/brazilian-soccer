using System;
using System.Globalization;
using Game.Core.Ids;
using Game.Core.Results;
using Game.Data.Loading;
using Newtonsoft.Json.Linq;

namespace Game.Save
{
    /// <summary>
    /// <c>slot.meta.json</c> (TECHNICAL_SPEC §14): a small, quick-to-read header kept apart from the compressed
    /// body, carrying the checksum that guards it. <see cref="SavedAtUtc"/> is supplied by the caller rather than
    /// read from the clock here: the save code stays in the pure zone, which may never call
    /// <c>DateTime.Now</c> (CLAUDE.md §8).
    /// </summary>
    public sealed class SaveHeader
    {
        public int SchemaVersion { get; }
        public DateTime SavedAtUtc { get; }
        public ulong Seed { get; }
        public int Year { get; }
        public Id? ManagedClubId { get; }
        /// <summary>SHA-256 (hex) of the decompressed body's UTF-8 bytes.</summary>
        public string Checksum { get; }

        public SaveHeader(int schemaVersion, DateTime savedAtUtc, ulong seed, int year, Id? managedClubId, string checksum)
        {
            SchemaVersion = schemaVersion;
            SavedAtUtc = savedAtUtc;
            Seed = seed;
            Year = year;
            ManagedClubId = managedClubId;
            Checksum = checksum ?? throw new ArgumentNullException(nameof(checksum));
        }

        public byte[] ToJsonBytes()
        {
            var root = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["savedAtUtc"] = SavedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                ["seed"] = Seed.ToString(CultureInfo.InvariantCulture),
                ["year"] = Year,
                ["managedClubId"] = ManagedClubId.HasValue ? new JValue(ManagedClubId.Value.Value) : JValue.CreateNull(),
                ["checksum"] = Checksum,
            };
            return System.Text.Encoding.UTF8.GetBytes(root.ToString(Newtonsoft.Json.Formatting.None));
        }

        public static Result<SaveHeader> FromJsonBytes(byte[] bytes)
        {
            var sj = new StrictJson("meta");
            JObject root;
            try
            {
                root = SaveJson.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                return Result<SaveHeader>.Fail(StrictJson.InvalidJson, "meta: " + e.Message);
            }
            if (root == null) return Result<SaveHeader>.Fail(StrictJson.InvalidStructure, "meta: root must be an object.");

            sj.Keys(root, "root", "schemaVersion", "savedAtUtc", "seed", "year", "managedClubId", "checksum");
            int schemaVersion = sj.Int(root, "schemaVersion", "root");
            string savedAtText = sj.String(root, "savedAtUtc", "root");
            DateTime savedAt = DateTime.MinValue;
            if (savedAtText != null && !DateTime.TryParseExact(savedAtText, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out savedAt))
                sj.Fail(StrictJson.InvalidStructure, $"root.savedAtUtc: '{savedAtText}' is not a yyyy-MM-ddTHH:mm:ssZ timestamp.");
            string seedText = sj.String(root, "seed", "root");
            ulong seed = 0;
            if (seedText != null && !ulong.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                sj.Fail(StrictJson.InvalidStructure, $"root.seed: '{seedText}' is not a valid unsigned 64-bit integer.");
            int year = sj.Int(root, "year", "root");
            var managedToken = root["managedClubId"];
            Id? managedClubId = managedToken == null || managedToken.Type == Newtonsoft.Json.Linq.JTokenType.Null
                ? (Id?)null
                : new Id(Math.Max(0, sj.AsInt(managedToken, "root.managedClubId")));
            string checksum = sj.String(root, "checksum", "root");

            if (!sj.Ok) return Result<SaveHeader>.Fail(sj.Errors);
            return Result<SaveHeader>.Ok(new SaveHeader(schemaVersion, savedAt, seed, year, managedClubId, checksum));
        }
    }
}
