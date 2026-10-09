using System;
using System.IO;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Loading;
using Game.Match;
using Game.Match.AI;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A7a (TECHNICAL_SPEC §19: "reinícios simples: saída, lateral, tiro de meta, escanteio curto"; "placar,
    /// relógio, 2×2 min"; X-55: tiro de meta só curto). Restart awarding, set-up, taking, and the two-half clock.</summary>
    public class RestartTests
    {
        /// <summary>Open play with the ball rolling from <paramref name="from"/> at <paramref name="velocity"/>, last
        /// touched by <paramref name="toucher"/>; steps until the ball goes out (or 5 s).</summary>
        private static AiMatch BallOut(int toucher, Vector3 from, Vector3 velocity, bool human = false)
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(1), Dt);
            if (human) m.EnableHuman(MatchSide.Home);
            m.CancelRestart();
            foreach (var p in m.Players) p.Body.Position = new Vector3(p.Body.Position.X * 0.3f, p.Body.Position.Y * 0.3f, 0f); // away from the lines
            m.Ball.Position = from;
            m.Ball.Kick(velocity, 0f);
            m.Ball.LastTouch = toucher;
            for (int i = 0; i < 250; i++)
                if (m.Step() == AiMatchEvent.Out) return m;
            Assert.Fail("the ball never went out.");
            return m;
        }

        [Test]
        public void OverTheTouchline_IsAThrowInToTheOtherSide_OnTheLine()
        {
            var m = BallOut(3, new Vector3(10f, 30f, 0f), new Vector3(0f, 12f, 0f)); // home player 3 touched it last
            Assert.AreEqual(RestartKind.ThrowIn, m.Restart);
            Assert.AreSame(m.Away, m.RestartTeam);
            Assert.AreEqual(m.Pitch.HalfWidth - Db().Restarts.LineInset, m.RestartSpot.Y, 1e-4f);
            Assert.IsFalse(m.Taker.IsGoalkeeper);
            Assert.AreEqual(BallState.Dead, m.Ball.State, "dead until the taker is ready.");
        }

        [Test]
        public void OverTheGoalLine_OffAnAttacker_IsAShortGoalKickByTheKeeper()
        {
            var m = BallOut(10, new Vector3(45f, 10f, 0f), new Vector3(15f, 0f, 0f)); // home attacker, away goal (+x)
            Assert.AreEqual(RestartKind.GoalKick, m.Restart);
            Assert.AreSame(m.Away, m.RestartTeam);
            Assert.AreSame(m.AwayKeeper.Player, m.Taker);
            Assert.AreEqual(m.Pitch.HalfLength - Db().Restarts.GoalKickDepth, m.RestartSpot.X, 1e-3f);
        }

        [Test]
        public void OverTheGoalLine_OffADefender_IsACornerForTheAttackers()
        {
            var m = BallOut(13, new Vector3(45f, 10f, 0f), new Vector3(15f, 0f, 0f)); // away defender, own goal (+x)
            Assert.AreEqual(RestartKind.Corner, m.Restart);
            Assert.AreSame(m.Home, m.RestartTeam);
            Assert.AreEqual(m.Pitch.HalfLength - Db().Restarts.CornerInset, m.RestartSpot.X, 1e-3f);
            Assert.AreEqual(m.Pitch.HalfWidth - Db().Restarts.CornerInset, m.RestartSpot.Y, 1e-3f);
            Assert.AreEqual(1, m.HomeStats.Corners);
        }

        [Test]
        public void AnAiRestart_IsTakenAfterTheSetUp_WithAShortPass()
        {
            var r = Db().Restarts;
            var m = BallOut(10, new Vector3(45f, 10f, 0f), new Vector3(15f, 0f, 0f));
            var taker = m.Taker;
            int steps = 0;
            AiMatchEvent ev;
            do { ev = m.Step(); steps++; } while (ev != AiMatchEvent.Pass && steps < 500);
            Assert.AreEqual(AiMatchEvent.Pass, ev);
            Assert.AreEqual(taker.Global, m.Ball.LastTouch);
            Assert.AreEqual(RestartKind.None, m.Restart);
            Assert.AreEqual(BallState.Rolling, m.Ball.State, "X-55: the goal kick is a ground pass.");
            Assert.That(steps * Dt, Is.EqualTo(r.SetupSeconds + r.AiTakeSeconds).Within(3 * Dt));
        }

        [Test]
        public void TheUser_AimsAndTakesHisRestart_AndControlGoesToTheReceiver()
        {
            var m = BallOut(13, new Vector3(45f, 10f, 0f), new Vector3(15f, 0f, 0f), human: true); // home corner
            Assert.AreEqual(RestartKind.Corner, m.Restart);
            for (int i = 0; i < 500 && !m.RestartReady; i++) m.Step(HumanInput.None);
            Assert.AreSame(m.Taker, m.Controlled, "the user takes his own restart.");
            var taker = m.Taker;
            var pass = new HumanInput(new Vector2(-1f, -0.3f), false, new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false, false);
            Assert.AreEqual(AiMatchEvent.Pass, m.Step(pass));
            Assert.AreNotSame(taker, m.Controlled, "control goes to the receiver.");
        }

        [Test]
        public void AUserWhoWaits_HasTheRestartTakenForHim()
        {
            var r = Db().Restarts;
            var m = BallOut(13, new Vector3(45f, 10f, 0f), new Vector3(15f, 0f, 0f), human: true);
            int steps = 0;
            while (m.Restart != RestartKind.None && steps < 2000) { m.Step(HumanInput.None); steps++; }
            Assert.AreEqual(RestartKind.None, m.Restart);
            Assert.That(steps * Dt, Is.EqualTo(r.SetupSeconds + r.HumanTimeoutSeconds).Within(3 * Dt));
        }

        [Test]
        public void InMatches_RestartsNeverStall_AndOpponentsKeepTheirDistance()
        {
            var r = Db().Restarts;
            float maxSeconds = r.SetupSeconds + Math.Max(r.AiTakeSeconds, r.HumanTimeoutSeconds) + 2 * Dt;
            int restarts = 0, crowded = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
                if (seed % 2 == 0) m.EnableHuman(MatchSide.Home);
                float since = 0f;
                while (!m.Finished)
                {
                    var kind = m.Restart;
                    var spot = m.RestartSpot;
                    var team = m.RestartTeam;
                    var ev = m.Step(HumanInput.None);
                    if (kind == RestartKind.None) { since = 0f; continue; }
                    since += Dt;
                    Assert.LessOrEqual(since, maxSeconds, $"seed {seed}, t={m.ElapsedSeconds:F2}: {kind} stalled.");
                    if (m.Restart != RestartKind.None || ev == AiMatchEvent.HalfTime) continue;
                    restarts++;
                    float radius = kind == RestartKind.ThrowIn ? r.ThrowInExclusionRadius : r.ExclusionRadius;
                    foreach (var o in m.Opponents(team).Players)
                        if (Vector3.Distance(o.Body.Position, spot) < 0.5f * radius) { crowded++; break; }
                    since = 0f;
                }
            }
            Assert.Greater(restarts, 20, "too few restarts to judge.");
            // Opponents walk out of the circle during the set-up; one caught deep inside when it is taken is rare, not never.
            Assert.LessOrEqual(crowded, restarts / 10, $"{crowded} of {restarts} restarts taken with an opponent well inside the circle.");
        }

        [Test]
        public void Clock_RunsTwoHalves_AndTheOtherSideKicksOffTheSecond()
        {
            var r = Db().Restarts;
            var m = new AiMatch(Db(), AiMatchTests.Setup(1), Dt);
            Assert.AreEqual(1, m.Half);
            Assert.AreEqual(RestartKind.Kickoff, m.Restart);
            Assert.AreSame(m.Home, m.RestartTeam);
            int halfTimes = 0;
            float clockAtHalf = -1f;
            while (!m.Finished)
            {
                if (m.Step() != AiMatchEvent.HalfTime) continue;
                halfTimes++;
                clockAtHalf = m.ClockMinutes;
                Assert.AreEqual(2, m.Half);
                Assert.AreEqual(RestartKind.Kickoff, m.Restart);
                Assert.AreSame(m.Away, m.RestartTeam, "the other side kicks off the second half.");
            }
            Assert.AreEqual(r.Halves - 1, halfTimes);
            Assert.AreEqual(r.DisplayMinutesPerHalf, clockAtHalf, 1e-3f);
            Assert.AreEqual(r.Halves * r.DisplayMinutesPerHalf, m.ClockMinutes, 1e-3f);
            Assert.AreEqual(m.DurationSeconds, m.ElapsedSeconds, Dt);
        }

        [Test]
        public void AfterAGoal_TheConcedingSideKicksOff()
        {
            // Several seeds: whether a given seed produces a goal differs between runtimes (.NET vs Unity float math).
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
                while (!m.Finished)
                {
                    int away = m.Away.Goals;
                    if (m.Step() != AiMatchEvent.Goal) continue;
                    Assert.AreEqual(RestartKind.Kickoff, m.Restart, $"seed {seed}");
                    Assert.AreSame(m.Away.Goals > away ? m.Home : m.Away, m.RestartTeam, $"seed {seed}");
                    Assert.AreEqual(Vector3.Zero, m.RestartSpot, $"seed {seed}");
                    return;
                }
            }
            Assert.Fail("no goal in 12 matches.");
        }

        [Test]
        public void RealRestartsFile_Loads() =>
            Assert.IsTrue(GameDataLoader.LoadRestarts(new DirectoryDataSource(TestPaths.DataRoot())).IsSuccess);

        [Test]
        public void ZeroHalves_IsRejected() =>
            Assert.IsFalse(RestartsReader.Read(ReadRestarts().Replace("\"halves\": 2", "\"halves\": 0")).IsSuccess);

        [Test]
        public void RestartsUnknownKey_IsRejected() =>
            Assert.IsFalse(RestartsReader.Read(ReadRestarts().Replace("\"cornerInset\"", "\"corner\"")).IsSuccess);

        private static string ReadRestarts() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "restarts.json"));
    }
}
