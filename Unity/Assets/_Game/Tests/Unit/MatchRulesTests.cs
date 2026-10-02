using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Rules.Match;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Shared match rules (TECHNICAL_SPEC §10) with the real data files.</summary>
    public class MatchRulesTests
    {
        private static GameDatabase _db;
        private static MatchRules _rules;

        private static GameDatabase Db()
        {
            if (_db != null) return _db;
            var r = GameDataLoader.Load(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return _db = r.Value;
        }

        private static MatchRules Rules() => _rules ?? (_rules = new MatchRules(Db()));

        private static MatchPlayerSetup Player(int id, Position main, int value, float energy = 100f, int morale = 3, float? form = null, params Position[] secondary) =>
            new MatchPlayerSetup(new Id(id), Enumerable.Repeat(value, AttrInfo.Count).ToArray(), (int)main,
                secondary.Select(p => (int)p).ToArray(), energy, morale, form);

        private static List<FieldPlayer> Natural442(int value, float energy = 100f)
        {
            var f = Db().Formation("4-4-2");
            return f.Slots.Select((s, i) => new FieldPlayer(Player(i + 1, s.Position, value, energy), s, energy)).ToList();
        }

        [Test]
        public void SectorStrength_OfANatural442_EqualsThePlayersRating()
        {
            var result = new float[SectorInfo.Count];
            Rules().SectorStrengths(Natural442(70), result);
            for (int s = 0; s < SectorInfo.Count; s++) Assert.AreEqual(70f, result[s], 0.01f, ((Sector)s).ToString());
        }

        [Test]
        public void FormationsShiftStrengthBetweenSectors()
        {
            var f = Db().Formation("4-3-3");
            var players = f.Slots.Select((s, i) => new FieldPlayer(Player(i + 1, s.Position, 70), s, 100f)).ToList();
            var r433 = new float[SectorInfo.Count];
            Rules().SectorStrengths(players, r433);
            Assert.AreNotEqual(70f, r433[(int)Sector.Creation], "4-3-3 differs from the 4-4-2 reference");
            Assert.AreEqual(70f, r433[(int)Sector.Goalkeeping], 0.01f);
        }

        [Test]
        public void PositionFit_Morale_Form_AndEnergy_ModifyStrength()
        {
            var r = Rules();
            Assert.AreEqual(1f, r.FitFactor(Player(1, Position.MC, 60), Position.MC));
            Assert.AreEqual(Db().Ovr.SecondaryPositionFactor, r.FitFactor(Player(1, Position.MC, 60, secondary: Position.VOL), Position.VOL));
            Assert.AreEqual(Db().Ovr.OutOfPositionFactor, r.FitFactor(Player(1, Position.MC, 60), Position.ZAG));

            Assert.AreEqual(1f, r.ConditionFactor(Player(1, Position.MC, 60, morale: 3)), 1e-6f);
            Assert.Greater(r.ConditionFactor(Player(1, Position.MC, 60, morale: 5)), 1f);
            Assert.Less(r.ConditionFactor(Player(1, Position.MC, 60, morale: 1)), 1f);
            Assert.Greater(r.ConditionFactor(Player(1, Position.MC, 60, form: 8f)), r.ConditionFactor(Player(1, Position.MC, 60, form: 5f)));

            var attrs = Enumerable.Repeat(60, AttrInfo.Count).ToArray();
            Assert.AreEqual(1f, r.EnergyFactor(attrs, 100f));
            Assert.Less(r.EnergyFactor(attrs, 20f), r.EnergyFactor(attrs, 50f));
        }

        [Test]
        public void Stamina_ReducesEnergyDrain_AndHighPressureIncreasesIt()
        {
            var r = Rules();
            var low = Enumerable.Repeat(40, AttrInfo.Count).ToArray();
            var high = Enumerable.Repeat(40, AttrInfo.Count).ToArray();
            high[(int)Attr.Stamina] = 90;
            var tactic = new TacticSetup("4-4-2", 3, 2, 2);
            Assert.Less(r.EnergyDrainPerMinute(high, tactic), r.EnergyDrainPerMinute(low, tactic));
            Assert.Greater(r.EnergyDrainPerMinute(low, new TacticSetup("4-4-2", 3, 2, 3)), r.EnergyDrainPerMinute(low, tactic));
        }

        [Test]
        public void Tackling_ReducesFoulPropensity_AndBookedPlayersAreCarded_LessOften()
        {
            var r = Rules();
            var clumsy = Enumerable.Repeat(30, AttrInfo.Count).ToArray();
            var clean = Enumerable.Repeat(30, AttrInfo.Count).ToArray();
            clean[(int)Attr.Tackling] = 90;
            Assert.Less(r.FoulPropensity(clean), r.FoulPropensity(clumsy));

            var rng1 = new Rng(3); var rng2 = new Rng(3);
            int fresh = 0, booked = 0;
            for (int i = 0; i < 20000; i++)
            {
                if (r.CardForFoul(rng1, false) != MatchRules.Card.None) fresh++;
                if (r.CardForFoul(rng2, true) != MatchRules.Card.None) booked++;
            }
            Assert.Greater(fresh, booked, "seed=3");
        }

        [Test]
        public void Ratings_StayWithin0To10_WithOneDecimal_AndRewardGoals()
        {
            var rr = Db().MatchRules.Ratings;
            var rng = new Rng(11);
            var baseC = new Ratings.Contribution { Minutes = 90, Role = FormationRole.ST, TeamGoalsFor = 1, TeamGoalsAgainst = 1 };
            var scorer = baseC; scorer.Goals = 3; scorer.TeamGoalsFor = 3;
            float a = Ratings.Compute(rr, baseC, new Rng(11)), b = Ratings.Compute(rr, scorer, new Rng(11));
            Assert.Greater(b, a);
            for (int i = 0; i < 1000; i++)
            {
                var c = baseC; c.Goals = rng.NextInt(0, 6); c.Red = rng.NextInt(0, 2) == 1; c.TeamGoalsAgainst = rng.NextInt(0, 8);
                float v = Ratings.Compute(rr, c, rng);
                Assert.That(v, Is.InRange(0f, 10f), "seed=11");
                Assert.AreEqual(Math.Round(v, 1), v, 1e-5, "seed=11");
            }
        }

        [Test]
        public void Lineup_PutsGoalkeeperInGoal_AndNaturalPlayersInTheirPositions()
        {
            var f = Db().Formation("4-2-3-1");
            var squad = new List<MatchPlayerSetup>();
            int id = 1;
            foreach (var s in f.Slots) squad.Add(Player(id++, s.Position, 60));
            squad.Add(Player(id++, Position.GOL, 80));                 // better keeper on the list
            squad.Add(Player(id++, Position.ATA, 40));
            var (starters, bench) = Lineups.Pick(Rules(), f, squad, 5);
            Assert.AreEqual(11, starters.Length);
            Assert.AreEqual((int)Position.GOL, starters[0].MainPosition);
            Assert.AreEqual(80, starters[0].Attributes[0], "best goalkeeper starts");
            for (int i = 1; i < 11; i++) Assert.AreEqual((int)f.Slots[i].Position, starters[i].MainPosition, $"slot {i}");
            Assert.IsTrue(bench.Any(p => p.MainPosition == (int)Position.GOL), "a goalkeeper is kept on the bench");
            CollectionAssert.AllItemsAreUnique(starters.Concat(bench).Select(p => p.PlayerId));
        }
    }
}
