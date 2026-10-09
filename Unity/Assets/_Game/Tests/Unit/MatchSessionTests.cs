using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Match;
using Game.Match.AI;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A7b: the app-side session — fixed step decoupled from the frame rate, pause, resume from a snapshot
    /// (TECHNICAL_SPEC §5 "Snapshot e pausa"), one-shot actions, interpolation, zero allocation.</summary>
    public class MatchSessionTests
    {
        private static MatchSession NewSession(ulong seed = 1, MatchSide? human = MatchSide.Home) =>
            new MatchSession(Db(), AiMatchTests.Setup(seed), Dt, human);

        private static void Run(MatchSession s, float seconds, float frame)
        {
            for (float t = 0f; t < seconds; t += frame) s.Advance(frame, new Vector2(0.4f, 0.8f), false, false);
        }

        [Test]
        public void TheMatch_DoesNotDependOnTheFrameRate()
        {
            var a = NewSession();
            var b = NewSession();
            Run(a, 20f, 1f / 60f);
            Run(b, 20f, 1f / 30f);
            // Same number of fixed steps reached (within one), then compare at the same step.
            while (a.Match.ElapsedSeconds < b.Match.ElapsedSeconds) a.Advance(a.Match.StepSeconds, new Vector2(0.4f, 0.8f), false, false);
            while (b.Match.ElapsedSeconds < a.Match.ElapsedSeconds) b.Advance(b.Match.StepSeconds, new Vector2(0.4f, 0.8f), false, false);
            CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot(), "60 fps and 30 fps must play the same match.");
        }

        [Test]
        public void Paused_NothingMoves_AndTheTimeIsNotCaughtUpOnResume()
        {
            var s = NewSession();
            Run(s, 2f, 1f / 60f);
            var before = s.Snapshot();
            float clock = s.Match.ElapsedSeconds;
            s.Pause();
            Assert.AreEqual(0, s.Advance(5f, Vector2.One, true, false));
            CollectionAssert.AreEqual(before, s.Snapshot());
            s.Resume();
            s.Advance(1f / 60f, Vector2.Zero, false, false);
            Assert.LessOrEqual(s.Match.ElapsedSeconds - clock, 1f / 60f + s.Match.StepSeconds, "the pause is not caught up.");
        }

        [Test]
        public void RestoredSession_ComesBackPaused_AndPlaysOnIdentically()
        {
            var a = NewSession(3);
            Run(a, 15f, 1f / 50f);
            var b = MatchSession.Restore(Db(), AiMatchTests.Setup(3), Dt, a.Snapshot());
            Assert.IsTrue(b.Paused, "TECHNICAL_SPEC §5: restaura pausado.");
            b.Resume();
            for (int i = 0; i < 500; i++)
            {
                a.Advance(Dt, Vector2.UnitX, i % 3 == 0, false);
                b.Advance(Dt, Vector2.UnitX, i % 3 == 0, false);
            }
            CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot());
        }

        [Test]
        public void AQueuedAction_ReachesExactlyOneStep()
        {
            var s = NewSession();
            while (!s.Match.RestartReady) s.Advance(Dt, Vector2.Zero, false, false); // kick-off: the user's taker is ready
            s.Queue(new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false);
            int passes = 0;
            for (int i = 0; i < 5; i++)
            {
                s.Advance(Dt * 3f, -Vector2.UnitX, false, false); // several steps in one frame
                if (s.SecondsSince(AiMatchEvent.Pass) < Dt * 16f) passes = 1;
            }
            Assert.AreEqual(1, passes);
            Assert.AreEqual(RestartKind.None, s.Match.Restart, "the queued pass took the kick-off.");
            Assert.Less(s.SecondsSinceKick(s.Match.Ball.LastTouch), 1f, "the kicker's kick is remembered for the animation.");
        }

        [Test]
        public void ALongFrame_RunsAtMostTheCap_AndAlphaStaysInRange()
        {
            var s = NewSession();
            Assert.AreEqual(MatchSession.MaxStepsPerFrame, s.Advance(10f, Vector2.Zero, false, false));
            for (int i = 0; i < 200; i++)
            {
                s.Advance(0.013f, Vector2.UnitY, false, false);
                Assert.That(s.Alpha, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Advance_AllocatesNoMemory()
        {
            var s = NewSession(2);
            Run(s, 30f, 1f / 60f);
            long allocated = 0;
            for (int i = 0; i < 60 * 60; i++)
            {
                if (i % 120 == 0) s.Queue(new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), i % 240 == 0);
                long before = GC.GetAllocatedBytesForCurrentThread();
                s.Advance(1f / 60f, new Vector2(MathF.Cos(i * 0.01f), MathF.Sin(i * 0.02f)), i % 5 == 0, i % 400 < 50);
                var p = s.PlayerPosition(i % 22);
                var bp = s.BallPosition;
                float k = s.SecondsSinceKick(i % 22) + s.SecondsSince(AiMatchEvent.Goal);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                if (float.IsNaN(p.X + bp.X + k)) Assert.Fail("NaN");
            }
            Assert.AreEqual(0, allocated, "CLAUDE.md §10: zero allocation per frame.");
        }
    }
}
