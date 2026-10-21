using System;
using System.Linq;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>A2 acceptance (ROADMAP), extended by F1a (D-22: 3/4/5/6/10 min): fadiga total igual em todas as durações
    /// de <c>competitions.json</c> <c>matchRules.durationOptions</c> (TECHNICAL_SPEC: "gasto normalizado pela duração
    /// configurada").</summary>
    public class FatigueTests
    {
        private const float Dt = 1f / 50f;
        private static GameDatabase _db;
        private static GameDatabase Db() => _db ?? (_db = Load());

        private static GameDatabase Load()
        {
            var r = GameDataLoader.Load(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        private static MatchPlayerSetup Player(int ovr) =>
            new MatchPlayerSetup(new Id(1), Enumerable.Repeat(ovr, AttrInfo.Count).ToArray(), 0, Array.Empty<int>(), 100f, 3, null);

        private static float TotalDrain(float durationMinutes)
        {
            var db = Db();
            var player = Player(50);
            var body = new PlayerBody();
            float totalSeconds = durationMinutes * 60f;
            for (float t = 0f; t < totalSeconds; t += Dt)
                Fatigue.Drain(body, db.Balance, player, sprinting: true, db.Fatigue, durationMinutes, Dt);
            return 100f - body.Energy;
        }

        [Test]
        public void TotalEnergySpent_IsTheSame_AtEveryDurationOption()
        {
            var options = Db().Competitions.MatchRules.DurationOptions;
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 10 }, options, "D-22 (X-111): 3/4/5/6/10 min");
            float drain6 = TotalDrain(6f);
            foreach (int minutes in options)
                Assert.That(TotalDrain(minutes), Is.EqualTo(drain6).Within(1.0f), $"{minutes} vs 6 min: total fadiga deve ser igual.");
            Assert.Greater(drain6, 5f, "fixture must actually drain a meaningful amount (not just hit the 0 clamp).");
            Assert.Less(drain6, 95f, "fixture must not bottom out at 0 (would hide the comparison behind the clamp).");
        }

        [Test]
        public void SprintingDrainsFasterThanJogging()
        {
            var db = Db();
            var player = Player(50);
            var sprinter = new PlayerBody();
            var jogger = new PlayerBody();
            for (int i = 0; i < 300; i++)
            {
                Fatigue.Drain(sprinter, db.Balance, player, sprinting: true, db.Fatigue, 6f, Dt);
                Fatigue.Drain(jogger, db.Balance, player, sprinting: false, db.Fatigue, 6f, Dt);
            }
            Assert.Less(sprinter.Energy, jogger.Energy);
        }

        [Test]
        public void BetterStamina_DrainsLess()
        {
            var db = Db();
            var weak = Player(1);
            var strong = Player(99);
            var weakBody = new PlayerBody();
            var strongBody = new PlayerBody();
            for (int i = 0; i < 300; i++)
            {
                Fatigue.Drain(weakBody, db.Balance, weak, sprinting: true, db.Fatigue, 6f, Dt);
                Fatigue.Drain(strongBody, db.Balance, strong, sprinting: true, db.Fatigue, 6f, Dt);
            }
            Assert.Greater(strongBody.Energy, weakBody.Energy);
        }

        [Test]
        public void LowEnergy_PenalizesSpeed()
        {
            var db = Db();
            var tired = new PlayerBody { Energy = 10f };
            var fresh = new PlayerBody { Energy = 100f };
            Assert.Less(Fatigue.SpeedMultiplier(tired, db.Fatigue), Fatigue.SpeedMultiplier(fresh, db.Fatigue));
            Assert.AreEqual(1f, Fatigue.SpeedMultiplier(fresh, db.Fatigue));
        }
    }
}
