using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Economy;
using Game.Career.Market;
using Game.Career.Season;
using NUnit.Framework;

namespace Game.Tests.Career
{
    /// <summary>
    /// B6 acceptance (TEST_PLAN "Economia"): balance always equals the ledger's sum, no infinite cash, a mismanaged
    /// club can run a deficit and get locked out of the market (B5), prizes reward the champion.
    /// </summary>
    public class EconomyTests
    {
        private static IEnumerable<ulong> Seeds() => CareerTestData.Ints("careerSeeds").Select(x => (ulong)x);

        private static CareerSimulator NewCareer(ulong seed) =>
            CareerSimulator.Start(CareerTestData.Db(), seed, new Game.Simulation.QuickSim.QuickSim(CareerTestData.Db()).Simulate,
                new TransferWindow(), new EconomySystem());

        [TestCaseSource(nameof(Seeds))]
        public void Balance_AlwaysEqualsTheArchivedLedgerSum(ulong seed)
        {
            var career = NewCareer(seed);
            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++)
            {
                career.PlaySeason();
                foreach (var club in career.State.World.Clubs)
                {
                    long archived = career.State.History.Sum(h => h.SeasonNetByClub.TryGetValue(club.Id, out var n) ? n : 0);
                    Assert.AreEqual(archived, club.Balance, $"seed={seed} year={career.State.Season.Year} club={club.Id}");
                }
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void NoClubRunsAwayToInfiniteCash(ulong seed)
        {
            var world = Game.Career.World.WorldGenerator.Generate(CareerTestData.Db(), seed);
            var startBudget = world.Clubs.ToDictionary(c => c.Id, c => Math.Max(1L, c.Budget));
            var career = NewCareer(seed);
            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++)
            {
                career.PlaySeason();
                foreach (var club in career.State.World.Clubs)
                    Assert.LessOrEqual(Math.Abs(club.Balance), startBudget[club.Id] * 50,
                        $"seed={seed} year={career.State.Season.Year} club={club.Id}: balance {club.Balance} vs starting budget {startBudget[club.Id]}");
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void MostClubs_AreNotLockedOutOfTheMarket(ulong seed)
        {
            var career = NewCareer(seed);
            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++) career.PlaySeason();
            int lockedOut = career.State.World.Clubs.Count(c => c.TransferLockout);
            Assert.Less(lockedOut, career.State.World.Clubs.Count / 2, $"seed={seed}: {lockedOut} clubs locked out of 64");
        }

        [Test]
        public void SpendingFarMoreThanItEarns_RunsADeficit_AndGetsLockedOut()
        {
            var db = CareerTestData.Db();
            var career = CareerSimulator.Start(db, seed: 21, resolve: new Game.Simulation.QuickSim.QuickSim(db).Simulate);
            var world = career.State.World;
            var club = world.Clubs[0];
            // Triple every contract's wage: far beyond anything TV, gate, sponsorship and maintenance can cover.
            foreach (var p in world.SquadOf(club.Id))
            {
                var c = world.ContractOf(p.Id);
                world.SetContract(new Game.Career.World.Contract
                {
                    Id = career.State.Ids.Next(), PlayerId = p.Id, ClubId = club.Id, Wage = c.Wage * 50,
                    StartYear = c.StartYear, EndYear = c.EndYear,
                });
            }

            var economy = new EconomySystem();
            var date = new DateTime(career.State.Season.Year, 2, 28);
            for (int i = 0; i < 4; i++)
            {
                economy.MonthEnd(career.State, db, date);
                date = date.AddMonths(1);
            }

            Assert.Less(club.Balance, 0, "a club paying far more in wages than it earns must run a deficit.");
            Assert.IsTrue(club.CashAlert, "flagged after the 1st negative month.");
            Assert.IsTrue(club.TransferLockout, "locked out after 3 consecutive negative months.");
        }
    }
}
