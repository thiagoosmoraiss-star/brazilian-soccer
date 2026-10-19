using System;
using System.IO;
using System.Linq;
using Game.Career.Economy;
using Game.Career.Market;
using Game.Career.Season;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Competitions;
using Game.Match.AI;
using Game.Save;
using Game.Save.IO;
using Game.Save.Serialization;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>
    /// C1 — Integração (ROADMAP: "pré-jogo → partida jogável → resultado aplicado na carreira"; testes: "resultado jogado
    /// aplica tabela, cartões, lesões e energia igual a um resultado simulado; retomar partida após fechar o app"; aceite:
    /// "temporada com partidas jogadas e simuladas misturadas"). The played match is the headless match engine (the same
    /// one the Match scene drives), without the user's input.
    /// </summary>
    [Category("Slow")]
    public class CareerIntegrationTests
    {
        private static CareerSimulator NewCareer(ulong seed)
        {
            var db = Db();
            var career = CareerSimulator.Start(db, seed, new Game.Simulation.QuickSim.QuickSim(db).Simulate, new TransferWindow(), new EconomySystem());
            // The user manages a club of the lowest division (GAME_DESIGN: the career starts in Série D).
            int lowest = career.State.World.Clubs.Max(c => c.DivisionIndex);
            career.State.ManagedClubId = career.State.World.Clubs.Where(c => c.DivisionIndex == lowest).OrderBy(c => c.Id).First().Id;
            return career;
        }

        /// <summary>Plays a pending match to the end in the match engine, the user's side driven by the AI.</summary>
        private static MatchResult Play(CareerSimulator.PendingMatch pending)
        {
            var m = new AiMatch(Db(), pending.Setup, Dt);
            m.RunToEnd();
            return m.Result();
        }

        [Test]
        public void ASeason_WithPlayedAndSimulatedMatchesMixed_RunsToTheEnd()
        {
            var career = NewCareer(11);
            int year = career.State.Season.Year;
            int played = 0, simulated = 0;
            var managed = career.State.ManagedClubId.Value;
            while (career.State.Season.Year == year)
            {
                var pending = career.AdvanceToUserMatch();
                if (pending == null) continue; // the season ended on its own
                Assert.IsTrue(pending.Fixture.HomeClubId == managed || pending.Fixture.AwayClubId == managed);
                Assert.Throws<InvalidOperationException>(() => career.Advance(), "the career waits for the user's match");
                if ((played + simulated) % 2 == 0)
                {
                    var fixture = pending.Fixture;
                    var r = Play(pending);
                    career.CompleteUserMatch(r);
                    Assert.IsTrue(fixture.Played);
                    Assert.AreEqual(r.HomeGoals, fixture.HomeGoals);
                    Assert.AreEqual(r.AwayGoals, fixture.AwayGoals);
                    if (fixture.Kind == CompetitionKind.Cup) Assert.IsFalse(fixture.Winner.IsNone, "a played cup tie always has a winner");
                    played++;
                }
                else
                {
                    career.SimulateUserMatch();
                    simulated++;
                }
            }
            TestContext.WriteLine($"season {year}: {played} played, {simulated} simulated");
            Assert.Greater(played, 5);
            Assert.Greater(simulated, 5);
            Assert.AreEqual(1, career.State.History.Count, "the season closed normally");
            var final = career.State.History[0].FinalTables.SelectMany(t => t).ToList();
            CollectionAssert.Contains(final, managed);
        }

        [Test]
        public void APlayedResult_IsAppliedLikeASimulatedOne_TableCardsAndEnergy()
        {
            var career = NewCareer(12);
            var pending = career.AdvanceToUserMatch();
            var f = pending.Fixture;
            var r = Play(pending);
            Assert.IsTrue(r.PlayerStats.All(p => p.Rating.HasValue), "every player who played has a rating (the career counts him as played)");
            var before = career.State.World.Players.ToDictionary(p => p.Id, p => (p.Condition.SeasonAppearances, p.Condition.SeasonMinutes));
            career.CompleteUserMatch(r);

            var world = career.State.World;
            foreach (var s in r.PlayerStats)
            {
                var p = world.Players.First(x => x.Id == s.PlayerId);
                Assert.AreEqual(s.FinalEnergy, p.Condition.Energy, 1e-3f, $"energy of {p.Id}");
                Assert.AreEqual(before[p.Id].SeasonAppearances + 1, p.Condition.SeasonAppearances, $"appearance of {p.Id}");
                Assert.AreEqual(before[p.Id].SeasonMinutes + s.MinutesPlayed, p.Condition.SeasonMinutes, $"minutes of {p.Id}");
                if (s.RedCard) Assert.Greater(p.Condition.SuspendedMatches((int)f.Kind), 0, $"red card of {p.Id} → suspension");
            }
            Assert.AreEqual(r.HomeStats.YellowCards, f.HomeYellows);
            Assert.AreEqual(r.AwayStats.RedCards, f.AwayReds);
            // The table counts it as any other match.
            var league = career.State.Season.Leagues.First(l => l.Fixtures.Contains(f));
            var table = Game.Career.Season.Standings.Table(Db().Competitions.League, league).ToList();
            var home = table.First(row => row.ClubId == f.HomeClubId);
            Assert.AreEqual(1, home.Played);
            Assert.AreEqual(r.HomeGoals, home.GoalsFor);
        }

        /// <summary>X-91: an injury in the played match reaches the career by the simulated path (PlayerSystems: severity,
        /// matches or days out from development.json) and the player misses the next match.</summary>
        [Test]
        public void AnInjuryInThePlayedMatch_IsAppliedLikeASimulatedOne_AndThePlayerMissesTheNextMatch()
        {
            var career = NewCareer(15);
            var pending = career.AdvanceToUserMatch();
            var m = new AiMatch(Db(), pending.Setup, Dt);
            for (int i = 0; i < 3000; i++) m.Step();
            var side = pending.UserSide;
            var team = side == MatchSide.Home ? m.Home : m.Away;
            var injured = team.Players.First(p => !p.IsGoalkeeper && !m.Injured(p) && !p.Body.OffPitch);
            var id = injured.Setup.PlayerId;
            Assert.IsTrue(m.Injure(side, injured.Local, InjurySeverity.Medium));
            m.RunToEnd();
            var r = m.Result();
            Assert.AreEqual(InjurySeverity.Medium, r.PlayerStats.Single(s => s.PlayerId == id).Injury);
            career.CompleteUserMatch(r);

            var player = career.State.World.Players.First(p => p.Id == id);
            var medium = Db().Development.MediumInjuryMatches;
            Assert.AreEqual(InjurySeverity.Medium, player.Condition.Injury);
            Assert.That(player.Condition.InjuryMatchesLeft, Is.InRange(medium.Min, medium.Max), "medium: matches out as in development.json");

            var next = career.AdvanceToUserMatch();
            if (next == null) Assert.Inconclusive("the season ended");
            var mine = next.Setup.Team(next.UserSide);
            Assert.IsFalse(mine.Starters.Concat(mine.Bench).Any(p => p.PlayerId == id), "the injured player is out of the next match");
        }

        [Test]
        public void AMatchInterruptedByClosingTheApp_ResumesFromTheSave_AndEndsAsIfNeverStopped()
        {
            var db = Db();
            var career = NewCareer(13);
            var pending = career.AdvanceToUserMatch();

            // Uninterrupted reference.
            var reference = new AiMatch(db, pending.Setup, Dt);
            reference.RunToEnd();
            var expected = reference.Result();

            // Half the match, then the app closes: the match goes into the career save.
            var m = new AiMatch(db, pending.Setup, Dt);
            while (m.Half == 1 && !m.Finished) m.Step();
            for (int i = 0; i < 100; i++) m.Step();
            career.State.MatchInProgress = new MatchInProgress(pending.Fixture.Id, m.Snapshot());
            var store = new FileSystemFileStore(Path.Combine(Path.GetTempPath(), "acesso-c1-" + Guid.NewGuid().ToString("N")));
            Assert.IsTrue(SaveService.Save(store, "career", career.State, new DateTime(2026, 3, 1), false).IsSuccess);

            // A new app run: load, resume the career, rebuild the same match and restore it.
            var loaded = SaveService.Load(store, "career");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            var resumed = CareerSimulator.Resume(db, loaded.Value, new Game.Simulation.QuickSim.QuickSim(db).Simulate, new TransferWindow(), new EconomySystem());
            var again = resumed.PendingUserMatch;
            Assert.IsNotNull(again);
            Assert.AreEqual(resumed.State.MatchInProgress.FixtureId, again.Fixture.Id);
            var restored = AiMatch.Restore(db, again.Setup, Dt, resumed.State.MatchInProgress.Snapshot);
            restored.RunToEnd();
            var result = restored.Result();
            Assert.AreEqual(expected.HomeGoals, result.HomeGoals);
            Assert.AreEqual(expected.AwayGoals, result.AwayGoals);
            Assert.AreEqual(expected.Events.Count, result.Events.Count);

            resumed.State.MatchInProgress = null;
            resumed.CompleteUserMatch(result);
            Assert.IsTrue(again.Fixture.Played);
            Assert.IsTrue(SaveValidator.Validate(resumed.State).IsSuccess);
        }

        [Test]
        public void ASaveWithAMatchInProgress_ThatIsNotTheManagedClubsUnplayedFixture_IsInvalid()
        {
            var career = NewCareer(14);
            var pending = career.AdvanceToUserMatch();
            career.State.MatchInProgress = new MatchInProgress(new Id(int.MaxValue - 7), new byte[] { 1 });
            Assert.IsFalse(SaveValidator.Validate(career.State).IsSuccess, "unknown fixture");
            career.State.MatchInProgress = new MatchInProgress(pending.Fixture.Id, new byte[] { 1 });
            Assert.IsTrue(SaveValidator.Validate(career.State).IsSuccess);
            var json = CareerStateWriter.Write(career.State);
            var back = CareerStateReader.Read(json);
            Assert.IsTrue(back.IsSuccess, back.ToString());
            CollectionAssert.AreEqual(new byte[] { 1 }, back.Value.MatchInProgress.Snapshot);
        }
    }
}
