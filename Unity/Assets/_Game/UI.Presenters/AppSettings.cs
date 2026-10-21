using System;
using System.Text;
using Game.Data.Loading;
using Game.Save.IO;
using Newtonsoft.Json.Linq;

namespace Game.UI.Presenters
{
    /// <summary>
    /// App settings (C3a, TECHNICAL_SPEC §15 "Settings | Modal"): kept apart from the career save (a new career keeps
    /// them). MVP: the played-match duration (D-22: 3/4/5/6/10 min, 4 by default; the options come from
    /// <c>competitions.json</c> <c>matchRules.durationOptions</c>). A missing or unreadable file means the defaults.
    /// </summary>
    public sealed class AppSettings
    {
        public const string File = "settings.json";
        private const int SchemaVersion = 1;

        public int MatchMinutes { get; private set; }

        private AppSettings(int matchMinutes) { MatchMinutes = matchMinutes; }

        public static AppSettings Defaults(GameDatabase db) => new AppSettings(db.Competitions.MatchRules.DurationMinutes);

        public static AppSettings Load(IFileStore store, GameDatabase db)
        {
            var settings = Defaults(db);
            if (!store.Exists(File)) return settings;
            try
            {
                var root = JObject.Parse(Encoding.UTF8.GetString(store.ReadAllBytes(File)));
                var minutes = root["matchMinutes"];
                if (root.Value<int?>("schemaVersion") == SchemaVersion && minutes != null && minutes.Type == JTokenType.Integer
                    && db.Competitions.MatchRules.DurationOptions.Contains(minutes.Value<int>()))
                    settings.MatchMinutes = minutes.Value<int>();
            }
            catch (Exception e) when (e is Newtonsoft.Json.JsonException || e is System.IO.IOException || e is InvalidCastException)
            {
                // Unreadable settings: the defaults (the file is rewritten on the next change).
            }
            return settings;
        }

        public void SetMatchMinutes(int minutes, GameDatabase db)
        {
            if (!db.Competitions.MatchRules.DurationOptions.Contains(minutes))
                throw new ArgumentOutOfRangeException(nameof(minutes), "Not one of competitions.json matchRules.durationOptions.");
            MatchMinutes = minutes;
        }

        public void Save(IFileStore store)
        {
            var root = new JObject { ["schemaVersion"] = SchemaVersion, ["matchMinutes"] = MatchMinutes };
            store.WriteAllBytes(File, Encoding.UTF8.GetBytes(root.ToString()));
        }
    }
}
