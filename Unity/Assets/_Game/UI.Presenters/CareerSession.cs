using System;
using Game.Career.Economy;
using Game.Career.Market;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Results;
using Game.Data.Loading;
using Game.Data.Strings;
using Game.Save;
using Game.Save.IO;

namespace Game.UI.Presenters
{
    /// <summary>The managed club's last match, for the post-match screen (played or simulated).</summary>
    public sealed class UserMatchReport
    {
        public Fixture Fixture { get; }
        public MatchSetup Setup { get; }
        public MatchResult Result { get; }
        public MatchSide UserSide { get; }
        public bool Played { get; }
        /// <summary>The managed club's league position before the match (null in a cup match).</summary>
        public int? PositionBefore { get; }

        public UserMatchReport(Fixture fixture, MatchSetup setup, MatchResult result, MatchSide userSide, bool played, int? positionBefore)
        {
            Fixture = fixture; Setup = setup; Result = result; UserSide = userSide; Played = played; PositionBefore = positionBefore;
        }
    }

    /// <summary>
    /// The career the Shell shows and drives (C3a; X-95): data, texts, settings, the <see cref="CareerSimulator"/> (the
    /// QuickSim for every match but the user's) and the one place that saves it — after every result, on Avançar and
    /// with the match in progress. Pure (no Unity), so the whole flow runs in <c>dotnet test</c>; the App's
    /// <c>CareerContext</c> only holds the instance across scenes.
    /// </summary>
    public sealed class CareerSession
    {
        public const string Slot = "career";

        private readonly IFileStore _store;
        private readonly Func<DateTime> _utcNow;
        private UserMatchReport _capturing;

        public GameDatabase Db { get; }
        public StringTable Strings { get; }
        public AppSettings Settings { get; }
        /// <summary>The career in play; a career whose club is still being chosen is not saved yet.</summary>
        public CareerSimulator Career { get; private set; }
        public bool HasCareer => Career != null && Career.State.ManagedClubId.HasValue;
        /// <summary>True while a new world waits for the club choice (X-93).</summary>
        public bool ChoosingClub => Career != null && !Career.State.ManagedClubId.HasValue;
        public UserMatchReport LastMatch { get; private set; }

        private CareerSession(GameDatabase db, StringTable strings, IFileStore store, Func<DateTime> utcNow)
        {
            Db = db;
            Strings = strings;
            _store = store;
            _utcNow = utcNow;
            Settings = AppSettings.Load(store, db);
        }

        /// <summary>A career save exists on disk (a new career replaces it once its club is chosen).</summary>
        public bool HasSavedCareer => _store.Exists(SaveSlotPaths.Body(Slot));

        /// <summary>Opens the saved career if there is one. A save that fails to load is reported and left on disk;
        /// <paramref name="loadCareer"/> = false opens without it (the menus still work, a new career replaces it).</summary>
        public static Result<CareerSession> Open(GameDatabase db, StringTable strings, IFileStore store, Func<DateTime> utcNow, bool loadCareer = true)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (strings == null) throw new ArgumentNullException(nameof(strings));
            if (store == null) throw new ArgumentNullException(nameof(store));
            // The real clock comes from the App (CLAUDE.md §8: no real clock in the pure zone); it only stamps the save.
            if (utcNow == null) throw new ArgumentNullException(nameof(utcNow));
            var session = new CareerSession(db, strings, store, utcNow);
            if (loadCareer && store.Exists(SaveSlotPaths.Body(Slot)))
            {
                var loaded = SaveService.Load(store, Slot);
                if (!loaded.IsSuccess) return Result<CareerSession>.Fail(loaded.Errors);
                if (loaded.Value.ManagedClubId.HasValue) session.Attach(CareerSimulator.Resume(db, loaded.Value, Resolver(db), new TransferWindow(), new EconomySystem()));
            }
            return Result<CareerSession>.Ok(session);
        }

        private static Func<MatchSetup, MatchResult> Resolver(GameDatabase db) => new Game.Simulation.QuickSim.QuickSim(db).Simulate;

        private void Attach(CareerSimulator career)
        {
            Career = career;
            LastMatch = null;
            career.MatchPlayed += OnMatchPlayed;
        }

        /// <summary>Nova carreira, step 1: generates the world; the club is chosen next (<see cref="ChooseClub"/>). The
        /// saved career, if any, is only replaced once the club is chosen.</summary>
        public void PrepareNewCareer(ulong seed) => Attach(CareerSimulator.Start(Db, seed, Resolver(Db), new TransferWindow(), new EconomySystem()));

        /// <summary>Nova carreira, step 2 (X-93: a club of the lowest division): the career starts and is saved.</summary>
        public Result ChooseClub(Id clubId)
        {
            if (!ChoosingClub) throw new InvalidOperationException("No new career is waiting for a club.");
            var club = Career.State.Club(clubId) ?? throw new ArgumentException("Unknown club.", nameof(clubId));
            if (club.DivisionIndex != LowestDivision) throw new ArgumentException("The career starts in the lowest division (X-93).", nameof(clubId));
            Career.State.ManagedClubId = clubId;
            return Save();
        }

        /// <summary>Back out of a new career before choosing the club: the saved one (if any) is reopened.</summary>
        public void CancelNewCareer()
        {
            if (!ChoosingClub) return;
            Career = null;
            if (_store.Exists(SaveSlotPaths.Body(Slot)))
            {
                var loaded = SaveService.Load(_store, Slot);
                if (loaded.IsSuccess && loaded.Value.ManagedClubId.HasValue)
                    Attach(CareerSimulator.Resume(Db, loaded.Value, Resolver(Db), new TransferWindow(), new EconomySystem()));
            }
        }

        /// <summary>Index of the lowest division of the career's world (where a new career starts).</summary>
        public int LowestDivision => CountDivisions() - 1;

        private int CountDivisions()
        {
            int max = 0;
            foreach (var c in Career.State.World.Clubs) max = Math.Max(max, c.DivisionIndex);
            return max + 1;
        }

        public Club ManagedClub => HasCareer ? Career.State.Club(Career.State.ManagedClubId.Value) : null;

        public CareerSimulator.PendingMatch Pending => HasCareer ? Career.PendingUserMatch : null;

        public Result Save()
        {
            var state = Career.State;
            bool seasonStart = state.Season.NextEntry == 0;
            return SaveService.Save(_store, Slot, state, _utcNow(), seasonStart);
        }

        public void SaveSettings() => Settings.Save(_store);

        /// <summary>"Avançar": to the managed club's next match, or to the end of the season. Saves.</summary>
        public CareerSimulator.PendingMatch AdvanceToNextMatch()
        {
            var pending = Career.AdvanceToUserMatch();
            Save();
            return pending;
        }

        /// <summary>The setup the Match scene plays: the career's, with the duration chosen in the settings.</summary>
        public MatchSetup PlaySetup(CareerSimulator.PendingMatch pending)
        {
            var s = pending.Setup;
            return s.DurationMinutes == Settings.MatchMinutes ? s
                : new MatchSetup(s.Home, s.Away, s.NeutralVenue, Settings.MatchMinutes, s.MaxSubstitutions, s.Seed, s.RequiresWinner);
        }

        /// <summary>"Simular" (the QuickSim): resolved, applied, saved; <see cref="LastMatch"/> holds the report.</summary>
        public UserMatchReport SimulateMatch()
        {
            var pending = Pending ?? throw new InvalidOperationException("No managed-club match is pending.");
            Career.State.MatchInProgress = null;
            _capturing = new UserMatchReport(pending.Fixture, pending.Setup, null, pending.UserSide, false, LeaguePosition(pending.Fixture));
            try { Career.SimulateUserMatch(); }
            finally { _capturing = null; }
            Save();
            return LastMatch;
        }

        /// <summary>The played match is over (Match scene): applied like a simulated result, saved.</summary>
        public UserMatchReport FinishPlayedMatch(MatchResult result)
        {
            var pending = Pending ?? throw new InvalidOperationException("No managed-club match is pending.");
            Career.State.MatchInProgress = null;
            _capturing = new UserMatchReport(pending.Fixture, PlaySetup(pending), null, pending.UserSide, true, LeaguePosition(pending.Fixture));
            try { Career.CompleteUserMatch(result); }
            finally { _capturing = null; }
            Save();
            return LastMatch;
        }

        /// <summary>The managed club's tactic (C3b); saved.</summary>
        public void SetTactic(TacticSetup tactic)
        {
            Career.SetUserTactic(tactic);
            Save();
        }

        /// <summary>The user's starters (slot order) and bench (C3b); saved.</summary>
        public void SetLineup(System.Collections.Generic.IReadOnlyList<Id> starters, System.Collections.Generic.IReadOnlyList<Id> bench)
        {
            Career.SetUserLineup(starters, bench);
            Save();
        }

        /// <summary>Back to the automatic lineup (C3b); saved.</summary>
        public void SetAutomaticLineup()
        {
            Career.SetAutomaticLineup();
            Save();
        }

        /// <summary>The app goes to the background mid-match: the match goes into the career save (C1).</summary>
        public void SuspendMatch(Id fixtureId, byte[] snapshot)
        {
            Career.State.MatchInProgress = new MatchInProgress(fixtureId, snapshot);
            Save();
        }

        private void OnMatchPlayed(Fixture fixture, MatchSetup setup, MatchResult result)
        {
            var c = _capturing;
            if (c == null || fixture != c.Fixture) return;
            LastMatch = new UserMatchReport(fixture, c.Setup, result, c.UserSide, c.Played, c.PositionBefore);
        }

        /// <summary>The managed club's position in its league table (1-based), or null for a cup fixture.</summary>
        public int? LeaguePosition(Fixture fixture)
        {
            if (fixture.Kind != CompetitionKind.League) return null;
            return LeaguePosition();
        }

        public int? LeaguePosition()
        {
            var club = ManagedClub;
            if (club == null) return null;
            foreach (var league in Career.State.Season.Leagues)
                if (league.DivisionIndex == club.DivisionIndex)
                {
                    var table = Standings.Table(Db.Competitions.League, league);
                    for (int i = 0; i < table.Count; i++)
                        if (table[i].ClubId == club.Id) return i + 1;
                }
            return null;
        }
    }
}
