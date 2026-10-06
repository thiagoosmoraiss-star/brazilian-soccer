using Game.Match;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>A1: field geometry used by the ball (GAME_DESIGN §25, TECHNICAL_SPEC §6).</summary>
    public class PitchTests
    {
        private static Pitch NewPitch() => new Pitch(length: 105f, width: 68f, goalWidth: 7.32f, goalHeight: 2.44f, postRadius: 0.06f);

        [Test]
        public void GoalLines_AreAtHalfLength()
        {
            var p = NewPitch();
            Assert.AreEqual(-52.5f, p.HomeGoalLineX, 1e-4f);
            Assert.AreEqual(52.5f, p.AwayGoalLineX, 1e-4f);
        }

        [Test]
        public void WithinGoalMouth_TrueBetweenPostsBelowCrossbar()
        {
            var p = NewPitch();
            Assert.IsTrue(p.WithinGoalMouth(0f, 1f));
            Assert.IsFalse(p.WithinGoalMouth(0f, 2.5f), "above the crossbar is not a goal.");
            Assert.IsFalse(p.WithinGoalMouth(4f, 1f), "outside the posts is not a goal.");
        }

        [Test]
        public void GoalBars_SpanFromGroundToCrossbarHeight()
        {
            var p = NewPitch();
            Assert.AreEqual(0f, p.HomeLeftPost.A.Z);
            Assert.AreEqual(p.GoalHeight, p.HomeLeftPost.B.Z);
            Assert.AreEqual(-p.HalfGoalWidth, p.HomeLeftPost.A.Y, 1e-4f);
            Assert.AreEqual(p.HalfGoalWidth, p.HomeRightPost.A.Y, 1e-4f);
        }
    }
}
