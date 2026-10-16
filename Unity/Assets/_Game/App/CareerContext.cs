using System.Collections.Generic;
using System.IO;
using Game.Career.Season;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Results;
using Game.Data.Loading;
using Game.Data.Strings;
using Game.Save.IO;
using Game.UI.Presenters;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Holds the <see cref="CareerSession"/> across the Boot, Shell and Match scenes (C1, C3a). The session (pure) owns
    /// the career, the settings and the saving; this class only opens it with Unity's save folder and gives the Match
    /// scene the names it shows.
    /// </summary>
    public static class CareerContext
    {
        public static CareerSession Session { get; private set; }
        public static bool Ready => Session != null;
        public static CareerSimulator Career => Session?.Career;
        public static GameDatabase Db => Session?.Db;
        /// <summary>Set when the Match scene hands a finished match back: the Shell opens on the post-match screen.</summary>
        public static bool ReturningFromMatch { get; set; }
        /// <summary>Why the saved career could not be opened (it stays on disk), or null.</summary>
        public static string LoadError { get; private set; }

        private static Dictionary<int, string> _playerNames;

        public static Result Open(GameDatabase db, StringTable strings)
        {
            var store = new FileSystemFileStore(Path.Combine(Application.persistentDataPath, "saves"));
            var opened = CareerSession.Open(db, strings, store, () => System.DateTime.UtcNow);
            LoadError = null;
            if (!opened.IsSuccess)
            {
                LoadError = opened.ToString();
                Debug.LogWarning("CareerContext: the saved career could not be loaded (kept on disk): " + LoadError);
                opened = CareerSession.Open(db, strings, store, () => System.DateTime.UtcNow, loadCareer: false);
                if (!opened.IsSuccess) return Result.Fail(opened.Errors);
            }
            Session = opened.Value;
            _playerNames = null;
            return Result.Success;
        }

        public static string ClubName(Id id) => Career.State.Club(id)?.Name ?? ("#" + id.Value);

        public static string PlayerName(int id)
        {
            if (_playerNames == null)
            {
                _playerNames = new Dictionary<int, string>();
                foreach (var p in Career.State.World.Players) _playerNames[p.Id.Value] = p.FirstName + " " + p.LastName;
            }
            return _playerNames.TryGetValue(id, out var n) ? n : "#" + id;
        }

        /// <summary>The app goes to the background mid-match: the match goes into the career save (C1).</summary>
        public static void SuspendMatch(Id fixtureId, byte[] snapshot) => Session.SuspendMatch(fixtureId, snapshot);

        /// <summary>The played match is over: applied to the career like a simulated one, saved; the Shell shows the report.</summary>
        public static void FinishMatch(MatchResult result)
        {
            Session.FinishPlayedMatch(result);
            _playerNames = null;
            ReturningFromMatch = true;
        }
    }
}
