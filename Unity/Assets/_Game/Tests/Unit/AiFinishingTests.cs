using System.Numerics;
using Game.Match;
using Game.Match.AI;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A4 regression (user report): near the goal line the carrier must finish or pass, not run up and down
    /// the byline. Checked for both sides (same AI, mirrored frame).</summary>
    public class AiFinishingTests
    {
        [TestCase(true, 1UL), TestCase(true, 2UL), TestCase(true, 3UL), TestCase(false, 1UL), TestCase(false, 2UL), TestCase(false, 3UL)]
        public void CarrierNearTheByline_ReleasesTheBall(bool home, ulong seed)
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
            var attack = home ? m.Home : m.Away;
            var defence = home ? m.Away : m.Home;
            var f = attack.Frame;

            var carrier = attack.Players[9];
            for (int i = 0; i < attack.Players.Length; i++)
                attack.Players[i].Body.Position = attack.Players[i] == carrier ? f.World(101f, 16f) : f.World(60f, 30f - 6f * i);
            // Defenders packed in the box between the carrier and the goal; the rest far away.
            var spots = new[] { f.World(101f, 9f), f.World(99f, 4f), f.World(100f, -2f), f.World(96f, 8f), f.World(97f, 0f) };
            for (int i = 0; i < defence.Players.Length; i++)
                defence.Players[i].Body.Position = i < spots.Length ? spots[i] : f.World(40f, 30f - 6f * i);
            m.PlaceBall(carrier);

            float startDistance = Vector3.Distance(carrier.Body.Position, f.TargetGoal);
            bool released = false;
            for (int s = 0; s < 50 * 6 && !released; s++)
            {
                var ev = m.Step();
                released = (ev == AiMatchEvent.Shot || ev == AiMatchEvent.Pass || ev == AiMatchEvent.Goal) && m.Ball.LastTouch == carrier.Global;
            }
            Assert.IsTrue(released, $"seed {seed}, {(home ? "home" : "away")}: the carrier kept the ball 6 s near the byline " +
                $"(start {startDistance:F1} m from goal, now {Vector3.Distance(carrier.Body.Position, f.TargetGoal):F1} m).");
        }
    }
}
