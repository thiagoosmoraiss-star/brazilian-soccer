using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Season;
using Game.Core.Contracts.Match;
using NUnit.Framework;
using static Game.Tests.Career.CareerTestData;

namespace Game.Tests.Career
{
    /// <summary>F1c (D-23b, D-23c, D-23d): the career's difficulty goes into the managed club's match only, played or simulated;
    /// every other match of the career is resolved without one.</summary>
    public class DifficultyCareerTests
    {
        private static (CareerSimulator Career, List<MatchSetup> Resolved) NewCareer(ulong seed = 21)
        {
            var db = Db();
            var sim = new Game.Simulation.QuickSim.QuickSim(db);
            var resolved = new List<MatchSetup>();
            var career = CareerSimulator.Start(db, seed, s => { resolved.Add(s); return sim.Simulate(s); });
            int lowest = career.State.World.Clubs.Max(c => c.DivisionIndex);
            career.State.ManagedClubId = career.State.World.Clubs.Where(c => c.DivisionIndex == lowest).OrderBy(c => c.Id).First().Id;
            Assert.IsNotNull(career.AdvanceToUserMatch());
            resolved.Clear();
            return (career, resolved);
        }

        [Test]
        public void ANewCareer_PlaysTheDefaultLevel_AndAnOldOneWithoutALevelToo()
        {
            var (career, _) = NewCareer();
            Assert.AreEqual(Db().Difficulty.DefaultLevel, career.State.Difficulty);
            career.State.Difficulty = null; // a save from before F1c
            Assert.AreEqual(Db().Difficulty.DefaultLevel, career.DifficultyLevel);
            Assert.AreEqual(Db().Difficulty.DefaultLevel, career.UserSetup(career.PendingUserMatch).Difficulty.LevelId);
        }

        [Test]
        public void TheSimulatedUserMatch_IsResolvedAtTheCareersLevel_EveryOtherMatchWithoutOne()
        {
            var (career, resolved) = NewCareer();
            career.SetDifficulty("hard");
            var pending = career.PendingUserMatch;
            career.SimulateUserMatch();
            var withLevel = resolved.Where(s => s.Difficulty != null).ToList();
            Assert.AreEqual(1, withLevel.Count, "D-23b: only the managed club's match");
            Assert.AreEqual("hard", withLevel[0].Difficulty.LevelId);
            Assert.AreEqual(pending.UserSide, withLevel[0].Difficulty.UserSide);
            Assert.AreEqual(pending.Setup.Seed, withLevel[0].Seed);
            Assert.Greater(resolved.Count, 1, "the rest of the matchday was resolved too");
        }

        [Test]
        public void TheLevel_CanChangeAtAnyTime_AndActsFromTheNextMatchOn()
        {
            var (career, _) = NewCareer();
            var pending = career.PendingUserMatch;
            career.SetDifficulty("easy");
            Assert.AreEqual("easy", career.UserSetup(pending).Difficulty.LevelId);
            career.SetDifficulty("hard");
            Assert.AreEqual("hard", career.UserSetup(pending).Difficulty.LevelId, "D-23c: Configurações a qualquer momento");
            Assert.IsNull(pending.Setup.Difficulty, "the pending setup itself stays without one");
            Assert.Throws<ArgumentException>(() => career.SetDifficulty("nightmare"));
            Assert.AreEqual("hard", career.State.Difficulty);
        }
    }
}
