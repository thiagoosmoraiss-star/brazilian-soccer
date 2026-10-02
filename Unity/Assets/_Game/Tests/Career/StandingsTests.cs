using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Season;
using Game.Core.Ids;
using NUnit.Framework;

namespace Game.Tests.Career
{
    /// <summary>Tiebreakers in the GDD order: points, wins, goal difference, goals for, head-to-head, cards, draw.</summary>
    public class StandingsTests
    {
        private static readonly Id A = new Id(1), B = new Id(2), C = new Id(3), D = new Id(4);
        private static int _nextId = 100;

        private static Fixture F(Id home, Id away, int hg, int ag, int hy = 0, int hr = 0, int ay = 0, int ar = 0)
        {
            var f = (Fixture)Activator.CreateInstance(typeof(Fixture), true);
            Set(f, nameof(Fixture.Id), new Id(_nextId++));
            Set(f, nameof(Fixture.HomeClubId), home); Set(f, nameof(Fixture.AwayClubId), away);
            Set(f, nameof(Fixture.HomeGoals), hg); Set(f, nameof(Fixture.AwayGoals), ag);
            Set(f, nameof(Fixture.HomeYellows), hy); Set(f, nameof(Fixture.HomeReds), hr);
            Set(f, nameof(Fixture.AwayYellows), ay); Set(f, nameof(Fixture.AwayReds), ar);
            Set(f, nameof(Fixture.Played), true);
            return f;
        }

        private static void Set(object o, string prop, object value) => o.GetType().GetProperty(prop).SetValue(o, value);

        private static List<Id> Order(params Fixture[] fixtures) =>
            Standings.Table(CareerTestData.Db().Competitions.League, new[] { A, B, C, D }, fixtures, 7).Select(r => r.ClubId).ToList();

        [Test]
        public void Points_ThenWins()
        {
            // A: 1W 0D (3 pts) ; B: 0W 3D (3 pts) -> A ahead on wins.
            var order = Order(F(A, D, 1, 0), F(B, C, 0, 0), F(B, D, 1, 1), F(C, B, 2, 2), F(C, D, 0, 1), F(A, C, 0, 1));
            Assert.Less(order.IndexOf(A), order.IndexOf(B));
        }

        [Test]
        public void GoalDifference_ThenGoalsFor()
        {
            // A and B: same points and wins; A has the better goal difference.
            var order = Order(F(A, C, 3, 0), F(B, D, 1, 0), F(C, D, 0, 0));
            Assert.Less(order.IndexOf(A), order.IndexOf(B));
            // Same goal difference, more goals scored wins.
            order = Order(F(A, C, 3, 2), F(B, D, 1, 0), F(C, D, 0, 0));
            Assert.Less(order.IndexOf(A), order.IndexOf(B));
        }

        [Test]
        public void HeadToHead_DecidesEqualRecords_InBothDirections()
        {
            // A and B finish level on points, wins, goal difference and goals for; only the direct match differs.
            var aWins = Order(F(A, B, 2, 1), F(B, C, 1, 0), F(C, A, 1, 0));
            Assert.Less(aWins.IndexOf(A), aWins.IndexOf(B), "A beat B");
            var bWins = Order(F(B, A, 2, 1), F(A, C, 1, 0), F(C, B, 1, 0));
            Assert.Less(bWins.IndexOf(B), bWins.IndexOf(A), "B beat A");
        }

        [Test]
        public void FewerWeightedCards_ThenDraw_AreDeterministic()
        {
            // Identical results; B got a red (weight 3) vs A two yellows (weight 2).
            var order = Order(F(A, C, 1, 0, hy: 2), F(B, D, 1, 0, hr: 1), F(C, D, 0, 0));
            Assert.Less(order.IndexOf(A), order.IndexOf(B));
            // Fully identical records: the seeded draw decides, the same way every time.
            var o1 = Order(F(A, C, 1, 0), F(B, D, 1, 0));
            var o2 = Order(F(A, C, 1, 0), F(B, D, 1, 0));
            CollectionAssert.AreEqual(o1, o2);
        }
    }
}
