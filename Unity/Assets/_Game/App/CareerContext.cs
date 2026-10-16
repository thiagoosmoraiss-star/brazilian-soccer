using System;
using System.Collections.Generic;
using System.IO;
using Game.Career.Economy;
using Game.Career.Market;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Results;
using Game.Data.Loading;
using Game.Save;
using Game.Save.IO;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// The career shared by the Boot, Shell and Match scenes (C1; ARCHITECTURE §15; TECHNICAL_SPEC §15 "Pre-Match: Jogar
    /// carrega Match; Simular fica na Shell; Post-Match na Shell após descarregar Match"). Holds the data, the save store and
    /// the <see cref="CareerSimulator"/> (QuickSim for every match but the managed club's), and is the one place the scenes
    /// save the career: after every result, and with the match in progress when the app goes to the background.
    /// </summary>
    public static class CareerContext
    {
        public const string Slot = "career";

        public static GameDatabase Db { get; private set; }
        public static CareerSimulator Career { get; private set; }
        public static bool Ready => Career != null;
        /// <summary>The last result of the managed club, for the Shell ("Pós-jogo" until C3).</summary>
        public static string LastResult { get; set; }

        private static IFileStore _store;
        private static Dictionary<int, string> _playerNames;

        /// <summary>Loads the saved career, or starts a new one (the user manages a club of the lowest division, X-90).</summary>
        public static Result Open(GameDatabase db)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            _store = new FileSystemFileStore(Path.Combine(Application.persistentDataPath, "saves"));
            var quickSim = new Game.Simulation.QuickSim.QuickSim(db);
            if (_store.Exists(SaveSlotPaths.Body(Slot)))
            {
                var loaded = SaveService.Load(_store, Slot);
                if (loaded.IsSuccess)
                {
                    Career = CareerSimulator.Resume(db, loaded.Value, quickSim.Simulate, new TransferWindow(), new EconomySystem());
                    _playerNames = null;
                    return Result.Success;
                }
                Debug.LogWarning("CareerContext: the saved career could not be loaded, starting a new one: " + loaded);
            }
            return NewCareer((ulong)DateTime.UtcNow.Ticks);
        }

        public static Result NewCareer(ulong seed)
        {
            var quickSim = new Game.Simulation.QuickSim.QuickSim(Db);
            Career = CareerSimulator.Start(Db, seed, quickSim.Simulate, new TransferWindow(), new EconomySystem());
            Club managed = null;
            foreach (var c in Career.State.World.Clubs)
                if (managed == null || c.DivisionIndex > managed.DivisionIndex || (c.DivisionIndex == managed.DivisionIndex && c.Id.CompareTo(managed.Id) < 0))
                    managed = c;
            Career.State.ManagedClubId = managed.Id;
            LastResult = null;
            _playerNames = null;
            return Save();
        }

        public static Result Save()
        {
            var r = SaveService.Save(_store, Slot, Career.State, DateTime.UtcNow, false);
            if (!r.IsSuccess) Debug.LogWarning("CareerContext: save failed: " + r);
            return r;
        }

        public static Club ManagedClub => Career.State.Club(Career.State.ManagedClubId.Value);

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
        public static void SuspendMatch(Id fixtureId, byte[] snapshot)
        {
            Career.State.MatchInProgress = new MatchInProgress(fixtureId, snapshot);
            Save();
        }

        /// <summary>The played match is over: applied to the career like a simulated one, then saved.</summary>
        public static void FinishMatch(MatchResult result)
        {
            var pending = Career.PendingUserMatch;
            Career.State.MatchInProgress = null;
            Career.CompleteUserMatch(result);
            LastResult = Describe(pending, result, "jogado");
            _playerNames = null;
            Save();
        }

        /// <summary>"Simular" in the Shell.</summary>
        public static void SimulateMatch()
        {
            var pending = Career.PendingUserMatch;
            if (pending == null) return;
            Career.State.MatchInProgress = null;
            var fixture = pending.Fixture;
            Career.SimulateUserMatch();
            LastResult = $"{ClubName(fixture.HomeClubId)} {fixture.HomeGoals} x {fixture.AwayGoals} {ClubName(fixture.AwayClubId)}"
                         + Penalties(fixture.HomePenalties, fixture.AwayPenalties) + " (simulado)";
            _playerNames = null;
            Save();
        }

        private static string Describe(CareerSimulator.PendingMatch pending, MatchResult r, string how)
        {
            var f = pending.Fixture;
            return $"{ClubName(f.HomeClubId)} {r.HomeGoals} x {r.AwayGoals} {ClubName(f.AwayClubId)}" + Penalties(r.HomePenalties, r.AwayPenalties) + $" ({how})";
        }

        private static string Penalties(int? home, int? away) => home.HasValue ? $" ({home} x {away} nos pênaltis)" : "";
    }
}
