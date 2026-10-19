using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Season;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using NUnit.Framework;
using static Game.Tests.Career.CareerTestData;

namespace Game.Tests.Career
{
    /// <summary>C3b (X-97): the user's tactic and lineup for the managed club go into the match setup.</summary>
    public class UserPlanTests
    {
        private static CareerSimulator NewCareer(ulong seed = 21)
        {
            var db = Db();
            var career = CareerSimulator.Start(db, seed, new Game.Simulation.QuickSim.QuickSim(db).Simulate);
            int lowest = career.State.World.Clubs.Max(c => c.DivisionIndex);
            career.State.ManagedClubId = career.State.World.Clubs.Where(c => c.DivisionIndex == lowest).OrderBy(c => c.Id).First().Id;
            Assert.IsNotNull(career.AdvanceToUserMatch());
            return career;
        }

        private static MatchTeamSetup UserSide(CareerSimulator career) => career.PendingUserMatch.Setup.Team(career.PendingUserMatch.UserSide);

        [Test]
        public void WithoutAPlan_TheManagedClubPlaysLikeAnAiClub()
        {
            var career = NewCareer();
            var mr = Db().Competitions.MatchRules;
            var team = UserSide(career);
            Assert.AreEqual(career.State.Season.ClubFormations[career.State.ManagedClubId.Value], team.Tactic.FormationId);
            Assert.AreEqual(mr.AiMentality, team.Tactic.Mentality);
            Assert.AreEqual(team.Tactic.FormationId, career.UserTactic.FormationId);
        }

        [Test]
        public void TheUsersTactic_IsTheMatchTactic_AndRebuildsThePendingMatch()
        {
            var career = NewCareer();
            var before = career.PendingUserMatch;
            string other = Db().Formations.Select(f => f.Id).First(id => id != career.UserTactic.FormationId);
            career.SetUserTactic(new TacticSetup(other, 5, 3, 1));
            var team = UserSide(career);
            Assert.AreNotSame(before, career.PendingUserMatch, "the pending match is rebuilt");
            Assert.AreEqual(other, team.Tactic.FormationId);
            Assert.AreEqual(5, team.Tactic.Mentality);
            Assert.AreEqual(3, team.Tactic.DefensiveLine);
            Assert.AreEqual(1, team.Tactic.Pressure);
            Assert.IsFalse(career.State.UserPlan.ManualLineup, "the lineup stays automatic");
            Assert.Throws<ArgumentException>(() => career.SetUserTactic(new TacticSetup("3-5-2", 3, 2, 2)));
        }

        [Test]
        public void TheUsersLineup_IsPlayedAsChosen_SlotBySlot()
        {
            var career = NewCareer();
            var auto = UserSide(career);
            // Reverse the automatic starters' order and pick the bench from the end of the squad.
            var starters = auto.Starters.Select(p => p.PlayerId).Reverse().ToList();
            var squad = career.State.World.SquadOf(career.State.ManagedClubId.Value).Select(p => p.Id).Where(id => !starters.Contains(id)).ToList();
            var bench = squad.Take(3).ToList();
            career.SetUserLineup(starters, bench);
            var team = UserSide(career);
            CollectionAssert.AreEqual(starters, team.Starters.Select(p => p.PlayerId));
            CollectionAssert.AreEqual(bench, team.Bench.Take(3).Select(p => p.PlayerId), "the chosen bench first");
            Assert.AreEqual(Db().Competitions.MatchRules.BenchSize, team.Bench.Count, "completed automatically");
            Assert.AreEqual(team.Bench.Count, team.Bench.Select(p => p.PlayerId).Distinct().Count());
            Assert.IsEmpty(team.Bench.Select(p => p.PlayerId).Intersect(starters));
        }

        [Test]
        public void AChosenStarterWhoCannotPlay_IsReplacedForThatMatchOnly()
        {
            var career = NewCareer();
            var starters = UserSide(career).Starters.Select(p => p.PlayerId).ToList();
            career.SetUserLineup(starters, new List<Id>());
            var injured = career.State.World.Players.First(p => p.Id == starters[5]);
            injured.Condition.InjuryMatchesLeft = 2;
            career.SetUserLineup(starters, new List<Id>()); // rebuild the pending match with the new condition
            var team = UserSide(career);
            Assert.IsFalse(team.Starters.Any(p => p.PlayerId == injured.Id), "the injured player does not play");
            for (int i = 0; i < starters.Count; i++)
                if (i != 5) Assert.AreEqual(starters[i], team.Starters[i].PlayerId, $"slot {i} keeps the user's choice");
            CollectionAssert.AreEqual(starters, career.State.UserPlan.Starters, "the plan itself is kept");
        }

        [Test]
        public void ALineup_MustBeElevenDistinctSquadPlayers_AndABenchWithinTheLimit()
        {
            var career = NewCareer();
            var starters = UserSide(career).Starters.Select(p => p.PlayerId).ToList();
            var squad = career.State.World.SquadOf(career.State.ManagedClubId.Value).Select(p => p.Id).ToList();
            var outsider = career.State.World.Players.First(p => !squad.Contains(p.Id)).Id;
            Assert.Throws<ArgumentException>(() => career.SetUserLineup(starters.Take(10).ToList(), null));
            Assert.Throws<ArgumentException>(() => career.SetUserLineup(starters.Take(10).Append(starters[0]).ToList(), null));
            Assert.Throws<ArgumentException>(() => career.SetUserLineup(starters.Take(10).Append(outsider).ToList(), null));
            Assert.Throws<ArgumentException>(() => career.SetUserLineup(starters, new List<Id> { starters[0] }));
            var tooMany = squad.Where(id => !starters.Contains(id)).Take(Db().Competitions.MatchRules.BenchSize + 1).ToList();
            Assert.Throws<ArgumentException>(() => career.SetUserLineup(starters, tooMany));
            Assert.IsNull(career.State.UserPlan, "nothing changed");
        }

        [Test]
        public void BackToAutomatic_KeepsTheTactic_AndPicksLikeTheAi()
        {
            var career = NewCareer();
            var auto = UserSide(career).Starters.Select(p => p.PlayerId).ToList();
            career.SetUserLineup(auto.AsEnumerable().Reverse().ToList(), null);
            career.SetUserTactic(new TacticSetup(career.UserTactic.FormationId, 4, 2, 3));
            career.SetAutomaticLineup();
            var team = UserSide(career);
            CollectionAssert.AreEqual(auto, team.Starters.Select(p => p.PlayerId));
            Assert.AreEqual(4, team.Tactic.Mentality);
        }

        [Test]
        public void ASeasonWithAFixedLineup_RunsToTheEnd_AsPlayersComeAndGo()
        {
            var career = NewCareer(22);
            var starters = UserSide(career).Starters.Select(p => p.PlayerId).ToList();
            career.SetUserLineup(starters, null);
            int year = career.State.Season.Year, matches = 0;
            while (career.State.Season.Year == year)
            {
                if (career.PendingUserMatch == null) { career.AdvanceToUserMatch(); continue; }
                var team = UserSide(career);
                Assert.AreEqual(11, team.Starters.Select(p => p.PlayerId).Distinct().Count(), $"seed 22, match {matches}");
                career.SimulateUserMatch();
                matches++;
            }
            Assert.Greater(matches, 25);
        }
    }
}
