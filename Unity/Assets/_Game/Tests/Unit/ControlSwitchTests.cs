using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A5 (ROADMAP / TEST_PLAN: "troca nunca durante contenção; histerese evita alternância") on the selection
    /// rules alone and inside full matches with a user-controlled side. Ranges in Data/TestRanges/defense.json.</summary>
    public class ControlSwitchTests
    {
        private static readonly Vector3 OwnGoal = new Vector3(-52.5f, 0f, 0f);
        private static readonly Vector3 BallAt = Vector3.Zero;

        private static JObject _ranges;
        private static JObject Ranges() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "defense.json"))));

        private static (PlayerBody[] Bodies, MatchPlayerSetup[] Setups, bool[] Eligible) Squad(params float[] xs)
        {
            var bodies = xs.Select(x => new PlayerBody { Position = new Vector3(x, 0f, 0f) }).ToArray();
            var setups = xs.Select(_ => Player()).ToArray();
            return (bodies, setups, xs.Select(_ => true).ToArray());
        }

        private static bool Update(ControlSelection cs, (PlayerBody[] Bodies, MatchPlayerSetup[] Setups, bool[] Eligible) s, bool trigger,
            bool containing = false, Vector2 move = default) =>
            cs.UpdateWithoutBall(s.Bodies, s.Setups, s.Eligible, s.Bodies.Length, BallAt, OwnGoal, move, containing, trigger, Db().Balance, Db().Defense);

        [Test]
        public void Trigger_SwitchesToTheFastestInterceptor_AndTheRingShowsIt()
        {
            var s = Squad(-20f, -5f, -30f);
            var cs = new ControlSelection(0);
            Update(cs, s, trigger: false);
            Assert.AreEqual(1, cs.Next, "the ring marks the best candidate.");
            Assert.AreEqual(0, cs.Controlled, "no trigger, no switch.");
            Assert.IsTrue(Update(cs, s, trigger: true));
            Assert.AreEqual(1, cs.Controlled);
        }

        [Test]
        public void NeverSwitches_WhileContaining()
        {
            var s = Squad(-20f, -5f);
            var cs = new ControlSelection(0);
            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(Update(cs, s, trigger: true, containing: true), $"step {i}");
                cs.Tick(Dt);
            }
            Assert.AreEqual(0, cs.Controlled, "GAME_DESIGN §17: nunca durante contenção.");
        }

        [Test]
        public void Hysteresis_IgnoresASlightlyBetterCandidate()
        {
            var cs = new ControlSelection(0);
            Assert.IsFalse(Update(cs, Squad(-20f, -18f), trigger: true), "10% better is not worth a switch (~25% hysteresis).");
            Assert.IsTrue(Update(cs, Squad(-20f, -10f), trigger: true), "50% better is.");
        }

        [Test]
        public void ManualSwitch_GoesToTheRing_ThenLocksAutoSwitchForASecond()
        {
            var db = Db();
            var s = Squad(-20f, -30f, -40f);
            var cs = new ControlSelection(2);
            Update(cs, s, trigger: false);
            Assert.IsTrue(cs.ManualSwitch(db.Defense));
            Assert.AreEqual(0, cs.Controlled, "the ring was player 0 (best placed other than the controlled one).");

            // Player 1 is now far better placed, but the user just chose: no auto-switch for PostManualLockSeconds.
            s.Bodies[1].Position = new Vector3(-5f, 0f, 0f);
            int lockSteps = (int)(db.Defense.PostManualLockSeconds / Dt);
            for (int i = 0; i < lockSteps - 1; i++)
            {
                Assert.IsFalse(Update(cs, s, trigger: true), $"step {i}: inside the post-manual lock.");
                cs.Tick(Dt);
            }
            cs.Tick(Dt * 2);
            Assert.IsTrue(Update(cs, s, trigger: true), "after the lock, the auto-switch resumes.");
        }

        [Test]
        public void UserSteeringTowardsTheBall_KeepsHisPlayer()
        {
            var cs = new ControlSelection(0);
            Assert.IsFalse(Update(cs, Squad(-20f, -5f), trigger: true, move: Vector2.UnitX), "trava de intenção.");
        }

        [Test]
        public void ConsecutiveTriggers_DoNotFlipBackAndForth()
        {
            var cs = new ControlSelection(0);
            var a = Squad(-20f, -10f); // 1 clearly better
            var b = Squad(-10f, -20f); // then 0 clearly better
            Assert.IsTrue(Update(cs, a, trigger: true));
            Assert.IsFalse(Update(cs, b, trigger: true), "the auto-switch cooldown stops an immediate switch back.");
        }

        // ---- inside full matches with the user on the home side ----

        private static AiMatch HumanMatch(ulong seed)
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
            m.EnableHuman(MatchSide.Home);
            return m;
        }

        [Test]
        public void InAMatch_ControlNeverMovesWhileTheUserContains()
        {
            var contain = new HumanInput(Vector2.Zero, false, ActionCommand.None, true, false);
            foreach (var seed in Ranges()["match"]["seeds"].Values<ulong>())
            {
                var m = HumanMatch(seed);
                while (!m.Finished)
                {
                    bool opponentHadBall = m.Ball.State == BallState.Controlled && m.Ball.Owner >= 0 && !m.Home.Owns(m.Ball.Owner);
                    var before = m.Controlled;
                    m.Step(contain);
                    bool opponentStillHasBall = m.Ball.State == BallState.Controlled && m.Ball.Owner >= 0 && !m.Home.Owns(m.Ball.Owner);
                    if (opponentHadBall && opponentStillHasBall)
                        Assert.AreSame(before, m.Controlled, $"seed {seed}, t={m.ElapsedSeconds:F2}: control moved during containment.");
                }
            }
        }

        [Test]
        public void InAMatch_AutoSwitchDoesNotAlternate()
        {
            var r = Ranges()["switching"];
            float window = r["reversalWindowSeconds"].Value<float>();
            foreach (var seed in Ranges()["match"]["seeds"].Values<ulong>())
            {
                var m = HumanMatch(seed);
                var switches = new List<(AiPlayer From, AiPlayer To, float Time)>();
                while (!m.Finished)
                {
                    bool weHadIt = m.Ball.State == BallState.Controlled && m.Ball.Owner >= 0 && m.Home.Owns(m.Ball.Owner);
                    var before = m.Controlled;
                    m.Step(HumanInput.None);
                    bool weHaveIt = m.Ball.State == BallState.Controlled && m.Ball.Owner >= 0 && m.Home.Owns(m.Ball.Owner);
                    if (!weHadIt && !weHaveIt && m.Controlled != before) switches.Add((before, m.Controlled, m.ElapsedSeconds));
                }
                int reversals = 0;
                for (int i = 1; i < switches.Count; i++)
                    if (switches[i].To == switches[i - 1].From && switches[i].Time - switches[i - 1].Time < window) reversals++;
                float perMinute = switches.Count / (m.ElapsedSeconds / 60f);
                Assert.LessOrEqual(reversals, r["maxReversalsPerMatch"].Value<int>(), $"seed {seed}: {reversals} A→B→A switches within {window} s.");
                Assert.LessOrEqual(perMinute, r["maxAutoSwitchesPerMinute"].Value<float>(), $"seed {seed}: {perMinute:F1} automatic switches per minute.");
            }
        }

        [Test]
        public void InAMatch_UserPass_HandsControlToTheReceiver()
        {
            var m = HumanMatch(1);
            for (int i = 0; i < 500 && !m.RestartReady; i++) m.Step(HumanInput.None);
            Assert.AreEqual(RestartKind.Kickoff, m.Restart);
            Assert.AreEqual(m.Controlled.Global, m.Ball.Owner, "the kick-off taker has the ball and is the controlled player.");
            var passer = m.Controlled;
            var ev = m.Step(new HumanInput(-Vector2.UnitX, false, new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false, false));
            Assert.AreEqual(AiMatchEvent.Pass, ev);
            Assert.AreNotSame(passer, m.Controlled, "GAME_DESIGN §17: ao passar, controle vai ao recebedor.");
        }

        [Test]
        public void AllAiMatches_PossessionDoesNotPingPong()
        {
            var r = Ranges()["tackles"];
            float window = r["quickRetackleWindowSeconds"].Value<float>();
            foreach (var seed in Ranges()["match"]["seeds"].Values<ulong>())
            {
                var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
                int tackles = 0, quick = 0;
                float last = float.NegativeInfinity;
                while (!m.Finished)
                {
                    if (m.Step() != AiMatchEvent.Tackle) continue;
                    tackles++;
                    if (m.ElapsedSeconds - last < window) quick++;
                    last = m.ElapsedSeconds;
                }
                Assert.Greater(tackles, 0, $"seed {seed}: nobody ever won the ball with a tackle.");
                Assert.LessOrEqual(quick / (float)tackles, r["maxQuickRetackleFraction"].Value<float>(), $"seed {seed}: {quick}/{tackles} tackles within {window} s of the previous one.");
            }
        }

        [Test]
        public void HumanStep_AllocatesNoMemory()
        {
            var m = HumanMatch(2);
            var input = new HumanInput(new Vector2(0.3f, 1f), true, ActionCommand.None, true, false);
            for (int i = 0; i < 50 * 20; i++) m.Step(input);
            long allocated = 0;
            for (int i = 0; i < 50 * 60; i++)
            {
                var step = i % 97 == 0 ? new HumanInput(Vector2.UnitX, false, new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false, i % 2 == 0) : input;
                long before = GC.GetAllocatedBytesForCurrentThread();
                m.Step(step);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Assert.AreEqual(0, allocated, "CLAUDE.md §10: zero allocation per step.");
        }
    }
}
