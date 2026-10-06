using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Board;
using Game.Career.Market;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using NUnit.Framework;

namespace Game.Tests.Career
{
    /// <summary>
    /// B7 acceptance (ROADMAP): every club gets a season objective, confidence moves with the outcome and a
    /// dismissal gives the club a fresh manager (never ends the automatic simulation), facilities queue and
    /// complete within their cost/duration, staff stays filled, and the stadium requirement actually gates access.
    /// </summary>
    public class BoardTests
    {
        private static IEnumerable<ulong> Seeds() => CareerTestData.Ints("careerSeeds").Select(x => (ulong)x);

        private static CareerSimulator NewCareer(ulong seed) =>
            CareerSimulator.Start(CareerTestData.Db(), seed, new Game.Simulation.QuickSim.QuickSim(CareerTestData.Db()).Simulate,
                new TransferWindow(), new Game.Career.Economy.EconomySystem());

        [TestCaseSource(nameof(Seeds))]
        public void EveryClub_GetsASeasonObjective_ConsistentWithItsDivision(ulong seed)
        {
            var db = CareerTestData.Db();
            var career = NewCareer(seed);
            var world = career.State.World;
            Assert.AreEqual(world.Clubs.Count, career.State.Season.Objectives.Count, $"seed={seed}");
            foreach (var club in world.Clubs)
            {
                var objective = career.State.Season.Objectives[club.Id];
                if (club.DivisionIndex == 0) Assert.AreNotEqual(ObjectiveType.Promote, objective.Type, $"seed={seed}: division A cannot be promoted.");
                if (club.DivisionIndex == 3) Assert.AreNotEqual(ObjectiveType.AvoidRelegation, objective.Type, $"seed={seed}: division D cannot be relegated.");
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void Confidence_StaysInRange_AndADismissalResetsIt(ulong seed)
        {
            var db = CareerTestData.Db();
            var career = NewCareer(seed);
            bool sawADismissal = false;
            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++)
            {
                var summary = career.PlaySeason();
                foreach (var manager in career.State.World.Managers)
                    Assert.That(manager.Confidence, Is.InRange(0, 100), $"seed={seed} year={summary.Year} club={manager.ClubId}");
                foreach (var clubId in summary.Dismissed)
                {
                    sawADismissal = true;
                    var manager = career.State.World.ManagerOf(clubId);
                    Assert.AreEqual(db.Board.Confidence.ResetAfterDismissal, manager.Confidence, $"seed={seed} club={clubId}: fresh manager.");
                    Assert.GreaterOrEqual(manager.Dismissals, 1, $"seed={seed} club={clubId}");
                }
            }
            Assert.IsTrue(sawADismissal, $"seed={seed}: {CareerTestData.Int("careerSeasons")} seasons across 64 clubs should include at least one dismissal.");
        }

        [TestCaseSource(nameof(Seeds))]
        public void StaffStaysFilled_AllThreeRoles_EveryClub(ulong seed)
        {
            var career = NewCareer(seed);
            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++) career.PlaySeason();
            var world = career.State.World;
            foreach (var club in world.Clubs)
                foreach (var role in new[] { StaffRole.Physio, StaffRole.Assistant, StaffRole.Scout })
                {
                    var member = world.StaffOf(club.Id, role);
                    Assert.IsNotNull(member, $"seed={seed} club={club.Id} role={role}");
                    Assert.That(member.Level, Is.InRange(1, 5), $"seed={seed} club={club.Id} role={role}");
                }
        }

        [Test]
        public void FacilityUpgrade_QueuesAndCompletes_WithinItsDurationAndCost()
        {
            var db = CareerTestData.Db();
            var career = CareerSimulator.Start(db, seed: 31, resolve: new Game.Simulation.QuickSim.QuickSim(db).Simulate);
            var world = career.State.World;
            var club = world.Clubs.First(c => c.TrainingCenterLevel < 5);
            club.Balance = 999_999_999;
            club.Budget = 999_999_999;
            int startLevel = club.TrainingCenterLevel;
            var date = new DateTime(career.State.Season.Year, 1, 31);

            Facilities.MonthEnd(db, career.State, date); // queues the upgrade
            Assert.IsNotNull(world.ActiveProjectOf(club.Id, FacilityType.Stadium) ?? world.ActiveProjectOf(club.Id, FacilityType.TrainingCenter));
            Assert.AreEqual(startLevel, club.TrainingCenterLevel, "not yet complete.");

            var project = world.ActiveProjectOf(club.Id, FacilityType.TrainingCenter);
            Assert.IsNotNull(project);
            Assert.AreEqual(startLevel + 1, project.TargetLevel);

            // Before completion: still the old level. After: bumped, and the project is gone.
            Facilities.MonthEnd(db, career.State, project.CompletesOn.AddDays(-1));
            Assert.AreEqual(startLevel, club.TrainingCenterLevel, "still under construction.");
            Facilities.MonthEnd(db, career.State, project.CompletesOn);
            Assert.AreEqual(startLevel + 1, club.TrainingCenterLevel);
            Assert.IsNull(world.ActiveProjectOf(club.Id, FacilityType.TrainingCenter));
        }

        [Test]
        public void StadiumRequirement_BlocksPromotion_ForAClubThatDoesNotMeetIt()
        {
            var db = CareerTestData.Db();
            // Market injected (so squads stay replenished over 10 seasons) but not the economy, so nothing re-syncs
            // Budget from Balance: it stays exactly 0, as set below.
            var career = CareerSimulator.Start(db, seed: 41, resolve: new Game.Simulation.QuickSim.QuickSim(db).Simulate, market: new TransferWindow());
            var world = career.State.World;
            // Division B (index 1): drop one club's stadium below A's requirement, and its budget to zero so the
            // automatic facilities policy (which would otherwise fix this) can never afford to build it up.
            var club = world.Clubs.First(c => c.DivisionIndex == 1);
            club.Stadium.Level = 1;
            club.Budget = 0;
            int minForA = Game.Rules.Board.BoardRules.StadiumMinimumLevel(db.Facilities, 0);
            Assert.Less(club.Stadium.Level, minForA, "fixture must actually be below the requirement.");

            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++)
            {
                var summary = career.PlaySeason();
                Assert.IsFalse(summary.Promoted.Contains(club.Id), $"year={summary.Year}: an under-built stadium must never be promoted.");
                if (summary.Promoted.Count > 0 || summary.Relegated.Count > 0)
                {
                    var league = db.Competitions.League;
                    Assert.AreEqual(league.Promoted * (summary.FinalTables.Count - 1), summary.Promoted.Count, $"year={summary.Year}: club count per division must stay exact.");
                }
            }
        }
    }
}
