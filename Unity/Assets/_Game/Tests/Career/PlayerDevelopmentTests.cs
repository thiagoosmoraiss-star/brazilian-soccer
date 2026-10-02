using System;
using System.Linq;
using Game.Career.Players;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Rules.Ovr;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Career
{
    /// <summary>B4 unit tests: development curve, potential cap, decline, condition (cards, injuries, energy, morale, form).</summary>
    public class PlayerDevelopmentTests
    {
        private static JObject Ranges() => JObject.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(CareerTestData.DataRoot(), "TestRanges", "development.json")));

        /// <summary>A generated player of the given position reshaped to the target OVR and potential.</summary>
        private static Player MakePlayer(Position pos, int targetOvr, int potential, int age, ulong seed = 5)
        {
            var db = CareerTestData.Db();
            var p = WorldGenerator.CreatePlayer(db.Ovr, db.World, pos, targetOvr, age, false, new Rng(seed), new Core.Ids.IdAllocator(), 2027);
            p.Potential = potential;
            return p;
        }

        private static int Ovr(Player p) => OvrCalculator.Rating(CareerTestData.Db().Ovr, p.AttributeSpan, p.MainPosition);

        private static void Years(Player p, int startAge, int years, float minutesShare, Rng rng)
        {
            var db = CareerTestData.Db();
            for (int y = 0; y < years; y++)
                for (int w = 0; w < db.Development.WeeksPerYear; w++)
                    Development.Week(db, p, startAge + y, minutesShare, rng);
        }

        [Test]
        public void YoungHighPotentialPlayer_WithMinutes_ReachesTheExpectedRangeBy24_WithoutPassingPotential()
        {
            var r = Ranges()["youngProspect"];
            int age = r["age"].Value<int>(), pot = r["potential"].Value<int>();
            var p = MakePlayer(Position.MC, r["ovr"].Value<int>(), pot, age);
            Years(p, age, r["byAge"].Value<int>() - age, r["minutesShare"].Value<float>(), new Rng(1));
            Assert.LessOrEqual(Ovr(p), pot, "never above potential");
            Assert.GreaterOrEqual(Ovr(p), pot - r["maxGapAtTarget"].Value<int>(), $"OVR {Ovr(p)} by age {r["byAge"]}");
        }

        [Test]
        public void Minutes_SpeedUpDevelopment()
        {
            var with = MakePlayer(Position.ATA, 55, 85, 18);
            var without = MakePlayer(Position.ATA, 55, 85, 18);
            Years(with, 18, 2, 1f, new Rng(2));
            Years(without, 18, 2, 0f, new Rng(2));
            Assert.Greater(Ovr(with), Ovr(without));
        }

        [Test]
        public void PlayerAtPotential_DoesNotGrow()
        {
            var p = MakePlayer(Position.ZAG, 70, 70, 19);
            p.Potential = Ovr(p);
            int before = Ovr(p);
            Years(p, 19, 3, 1f, new Rng(3));
            Assert.AreEqual(before, Ovr(p));
        }

        [Test]
        public void Veterans_Decline_PhysicalAttributesFirst()
        {
            var r = Ranges()["veteran"];
            int age = r["age"].Value<int>();
            var p = MakePlayer(Position.ZAG, r["ovr"].Value<int>(), 99, age);
            var before = p.Attributes.ToArray();
            int ovrBefore = Ovr(p);
            Years(p, age, r["years"].Value<int>(), 1f, new Rng(4));
            Assert.GreaterOrEqual(ovrBefore - Ovr(p), r["minDecline"].Value<int>(), "veterans decline");
            var physical = CareerTestData.Db().Development.PhysicalAttributes.Select(a => (int)a).ToList();
            double physicalDrop = physical.Average(a => before[a] - p.Attributes[a]);
            double otherDrop = Enumerable.Range(0, AttrInfo.Count).Where(a => !physical.Contains(a) && CareerTestData.Db().Ovr.Weight(p.MainPosition, (Attr)a) > 0)
                .Average(a => before[a] - p.Attributes[a]);
            Assert.Greater(physicalDrop, otherDrop, "physical attributes decline first (GAME_DESIGN §9)");
        }

        // ---------------- Condition ----------------

        private static readonly DateTime Day = new DateTime(2027, 5, 1);

        private static MatchResult Result(Player p, MatchSide side, int yellows = 0, bool red = false, InjurySeverity injury = InjurySeverity.None,
            int goalsFor = 1, int goalsAgainst = 0, float energy = 70f, float rating = 7f)
        {
            var stats = new[] { new PlayerMatchStats(p.Id, side, true, 90, 0, 0, 0, 0, 0, yellows, red, rating, energy, injury) };
            var empty = new TeamMatchStats(0, 0, 50, 0, 0, 0, 0, 0);
            int hg = side == MatchSide.Home ? goalsFor : goalsAgainst, ag = side == MatchSide.Home ? goalsAgainst : goalsFor;
            return new MatchResult(hg, ag, new MatchEvent[0], empty, empty, stats);
        }

        private static MatchResult Absent() =>
            new MatchResult(0, 0, new MatchEvent[0], new TeamMatchStats(0, 0, 50, 0, 0, 0, 0, 0), new TeamMatchStats(0, 0, 50, 0, 0, 0, 0, 0), new PlayerMatchStats[0]);

        private static void After(Player p, MatchResult r, CompetitionKind kind, DateTime date, ulong seed = 1) =>
            ConditionSystem.AfterMatch(CareerTestData.Db(), new[] { p }, MatchSide.Home, r, kind, date, new Rng(seed));

        [Test]
        public void ThreeYellows_SuspendForOneMatch_InThatCompetitionOnly()
        {
            var p = MakePlayer(Position.VOL, 60, 70, 25);
            for (int i = 0; i < 3; i++) After(p, Result(p, MatchSide.Home, yellows: 1), CompetitionKind.League, Day.AddDays(7 * i));
            Assert.IsFalse(ConditionSystem.Available(p, CompetitionKind.League, Day.AddDays(21)), "suspended in the league");
            Assert.IsTrue(ConditionSystem.Available(p, CompetitionKind.Cup, Day.AddDays(21)), "free in the cup");
            After(p, Absent(), CompetitionKind.League, Day.AddDays(21));
            Assert.IsTrue(ConditionSystem.Available(p, CompetitionKind.League, Day.AddDays(28)), "ban served");
        }

        [Test]
        public void RedCards_Suspend_SecondYellowOneMatch_StraightRedOneToThree()
        {
            var d = CareerTestData.Db().Development;
            var p = MakePlayer(Position.ZAG, 60, 70, 25);
            After(p, Result(p, MatchSide.Home, yellows: 2, red: true), CompetitionKind.League, Day);
            Assert.AreEqual(d.SecondYellowMatches, p.Condition.SuspendedMatches((int)CompetitionKind.League));
            Assert.AreEqual(0, p.Condition.Yellows((int)CompetitionKind.League), "a second yellow does not count towards accumulation");

            for (ulong seed = 1; seed <= 50; seed++)
            {
                var q = MakePlayer(Position.ZAG, 60, 70, 25);
                After(q, Result(q, MatchSide.Home, red: true), CompetitionKind.Cup, Day, seed);
                Assert.That(q.Condition.SuspendedMatches((int)CompetitionKind.Cup), Is.InRange(1, d.StraightRedMatchWeights.Count), $"seed={seed}");
            }
        }

        [Test]
        public void Injuries_KeepPlayersOut_ForTheirDuration()
        {
            var d = CareerTestData.Db().Development;
            var p = MakePlayer(Position.ATA, 60, 70, 25);
            After(p, Result(p, MatchSide.Home, injury: InjurySeverity.Medium), CompetitionKind.League, Day);
            int left = p.Condition.InjuryMatchesLeft;
            Assert.That(left, Is.InRange(d.MediumInjuryMatches.Min, d.MediumInjuryMatches.Max));
            for (int i = 1; i <= left; i++)
            {
                Assert.IsFalse(ConditionSystem.Available(p, CompetitionKind.League, Day.AddDays(7 * i)), $"match {i}");
                After(p, Absent(), CompetitionKind.League, Day.AddDays(7 * i));
            }
            Assert.IsTrue(ConditionSystem.Available(p, CompetitionKind.League, Day.AddDays(7 * (left + 1))));
            Assert.AreEqual(InjurySeverity.None, p.Condition.Injury);

            var s = MakePlayer(Position.ATA, 60, 70, 25);
            After(s, Result(s, MatchSide.Home, injury: InjurySeverity.Severe), CompetitionKind.League, Day);
            Assert.That((s.Condition.InjuredUntil.Value - Day).TotalDays, Is.InRange(d.SevereInjuryDays.Min, d.SevereInjuryDays.Max));
            Assert.IsFalse(ConditionSystem.Available(s, CompetitionKind.Cup, Day.AddDays(d.SevereInjuryDays.Min - 1)));
            Assert.IsTrue(ConditionSystem.Available(s, CompetitionKind.Cup, Day.AddDays(d.SevereInjuryDays.Max)));
        }

        [Test]
        public void Energy_RecoversWithDays_AndFormIsTheAverageOfTheLastRatings()
        {
            var d = CareerTestData.Db().Development;
            var p = MakePlayer(Position.MC, 60, 70, 25);
            After(p, Result(p, MatchSide.Home, energy: 60f), CompetitionKind.League, Day);
            Assert.AreEqual(60f, ConditionSystem.EnergyAt(d, p, Day));
            Assert.AreEqual(Math.Min(100f, 60f + 2 * d.EnergyRecoveryPerDay), ConditionSystem.EnergyAt(d, p, Day.AddDays(2)), 1e-4f);
            Assert.AreEqual(100f, ConditionSystem.EnergyAt(d, p, Day.AddDays(30)));

            float[] ratings = { 5f, 6f, 7f, 8f, 9f, 10f };
            foreach (var r in ratings) After(p, Result(p, MatchSide.Home, rating: r), CompetitionKind.League, Day);
            Assert.AreEqual(ratings.Skip(ratings.Length - d.FormMatches).Average(), p.Condition.Form.Value, 1e-4, "form = average of the last ratings");
        }

        [Test]
        public void Morale_StaysWithin1And5()
        {
            var p = MakePlayer(Position.MC, 60, 70, 25);
            for (ulong i = 1; i <= 60; i++) After(p, Result(p, MatchSide.Home, goalsFor: 3, goalsAgainst: 0), CompetitionKind.League, Day, i);
            Assert.AreEqual(5, p.Condition.Morale);
            for (ulong i = 1; i <= 60; i++) After(p, Result(p, MatchSide.Home, goalsFor: 0, goalsAgainst: 3), CompetitionKind.League, Day, i);
            Assert.AreEqual(1, p.Condition.Morale);
        }

        [Test]
        public void RetirementChance_IsZeroBeforeTheFirstAge_AndOneAtTheLast()
        {
            var d = CareerTestData.Db().Development;
            Assert.AreEqual(0f, Retirement.Chance(d, d.RetirementByAge[0].Age - 1));
            Assert.AreEqual(1f, Retirement.Chance(d, d.RetirementByAge[d.RetirementByAge.Count - 1].Age));
        }
    }
}
