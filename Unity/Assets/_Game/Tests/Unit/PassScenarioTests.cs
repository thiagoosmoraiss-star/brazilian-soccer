using System;
using System.Collections.Generic;
using System.Numerics;
using Game.Core.Random;
using Game.Data.Effects;
using Game.Match;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A3 scenario tests (ROADMAP): passe de 20 m sem pressão chega; erro cresce com pressão e pé ruim;
    /// atributos alteram o erro na direção esperada. Failing assertions list the seeds involved.</summary>
    public class PassScenarioTests
    {
        private static readonly Vector3 Origin = Vector3.Zero;
        private static readonly ActionCommand TapPass = new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower);

        [Test]
        public void TwentyMetrePass_WithoutPressure_Arrives()
        {
            var r = Ranges()["pass"];
            int n = r["samples"].Value<int>();
            var missed = new List<ulong>();
            for (ulong seed = 1; seed <= (ulong)n; seed++)
            {
                var s = Session(seed, new[] { Player(70), Player(70) }, new[] { Origin, new Vector3(20f, 0f, 0f) });
                TakeControl(s);
                s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);
                var ev = RunUntilSettled(s, 6f);
                if (!(ev == PracticeEvent.Captured && s.Ball.Owner == 1)) missed.Add(seed);
            }
            Assert.LessOrEqual(missed.Count, n * r["maxTwentyMetreMissFraction"].Value<float>(), "a 20 m pass by an average passer must almost always reach a static receiver. Missed seeds: " + string.Join(",", missed));
        }

        [Test]
        public void Pass_HandsControlToTheReceiver_AsTheBallLeavesTheFoot()
        {
            var s = Session(7, new[] { Player(), Player() }, new[] { Origin, new Vector3(15f, 0f, 0f) });
            TakeControl(s);
            Assert.AreEqual(0, s.Control.Controlled);

            var ev = s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);

            Assert.AreEqual(PracticeEvent.Pass, ev);
            Assert.AreEqual(1, s.LastPass.Target);
            Assert.AreEqual(1, s.Control.Controlled, "GAME_DESIGN §17: ao passar, controle vai ao recebedor quando a bola sai do pé.");
        }

        [Test]
        public void Kicker_NeverCatchesHisOwnPass_OnTheKickStep()
        {
            var s = Session(3, new[] { Player(), Player() }, new[] { Origin, new Vector3(20f, 0f, 0f) });
            TakeControl(s);
            s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);
            for (int i = 0; i < 10; i++)
            {
                s.Step(Vector2.Zero, false, ActionCommand.None, Vector2.Zero, Dt);
                Assert.AreNotEqual(0, s.Ball.Owner, "the ball still sits inside the kicker's radius right after the kick.");
            }
        }

        [Test]
        public void Cone_PicksTheTeammateInside_AndIgnoresOneOutside()
        {
            var db = Db();
            var bodies = new[]
            {
                new PlayerBody { Position = Origin },
                new PlayerBody { Position = new Vector3(15f, 10f, 0f) }, // ~34°, outside ±25°
                new PlayerBody { Position = new Vector3(15f, 4f, 0f) },  // ~15°, inside
            };
            Assert.AreEqual(2, PassSystem.FindTarget(0, bodies, 3, Vector2.UnitX, db.Kicking.Pass));

            var onlyOutside = new[] { bodies[0], bodies[1] };
            Assert.AreEqual(-1, PassSystem.FindTarget(0, onlyOutside, 2, Vector2.UnitX, db.Kicking.Pass),
                "GAME_DESIGN §19: sem ninguém no cone, vai ao espaço.");
        }

        [Test]
        public void PassIntoSpace_GoesAlongTheStick_AndControlGoesToTheNearestTeammate()
        {
            var s = Session(11, new[] { Player(90), Player(), Player() },
                new[] { Origin, new Vector3(0f, 20f, 0f), new Vector3(0f, -20f, 0f) });
            TakeControl(s);
            s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);

            Assert.AreEqual(-1, s.LastPass.Target);
            Assert.Greater(s.LastPass.PredictedStop.X, 5f, "the ball goes where the stick points, not to a teammate outside the cone.");
            int expected = MathF.Abs(s.LastPass.PredictedStop.Y - 20f) < MathF.Abs(s.LastPass.PredictedStop.Y + 20f) ? 1 : 2;
            Assert.AreEqual(expected, s.Control.Controlled);
        }

        [Test]
        public void ThroughBall_IsPlayedIntoTheSpaceAheadOfTheReceiver()
        {
            var s = Session(5, new[] { Player(), Player() }, new[] { Origin, new Vector3(15f, 0f, 0f) });
            TakeControl(s);
            s.Step(Vector2.Zero, false, new ActionCommand(ActionKind.Through, ActionCommand.AutoPower), Vector2.UnitX, Dt);

            Assert.AreEqual(1, s.LastPass.Target);
            Assert.Greater(s.LastPass.IntendedPoint.X, 15f + 2f, "enfiada: into the space ahead of a static receiver, along the attack.");
        }

        [Test]
        public void Passing90_ErrsAboutAQuarterOfPassing40()
        {
            float e40 = MeanAbsAngleError(Player(70, overrides: (Attr.Passing, 40)), 400);
            float e90 = MeanAbsAngleError(Player(70, overrides: (Attr.Passing, 90)), 400);
            float ratio = e90 / e40;
            var range = Ranges()["pass"]["passing90To40ErrorRatio"];
            Assert.That(ratio, Is.InRange(range[0].Value<float>(), range[1].Value<float>()), $"GAME_DESIGN §19: Passe 90 erra ~¼ do Passe 40 (got {e90:F2}° vs {e40:F2}°).");
        }

        [Test]
        public void BetterPassing_MeansSmallerError_AtEveryStep()
        {
            float previous = float.MaxValue;
            foreach (int passing in new[] { 30, 50, 70, 90 })
            {
                float e = MeanAbsAngleError(Player(70, overrides: (Attr.Passing, passing)), 300);
                Assert.Less(e, previous, $"Passing {passing} must err less than the level below.");
                previous = e;
            }
        }

        [Test]
        public void Error_GrowsWithPressure_AndWithTheWeakFoot()
        {
            var player = Player(70, weakFoot: 2);
            float clean = MeanAbsAngleError(player, 400);
            float pressured = MeanAbsAngleError(player, 400, nearestOpponent: 1f);
            Assert.Greater(pressured, clean * Ranges()["pass"]["minPressureErrorGrowth"].Value<float>(), "GAME_DESIGN §19: pressão < 2 m (+30-80%).");

            // A right-footer passing hard to his right (beyond 45°) uses the weak foot (X-50).
            float strongSide = MeanAbsAngleError(player, 400, aim: Rotate(Vector2.UnitX, 60f));
            float weakSide = MeanAbsAngleError(player, 400, aim: Rotate(Vector2.UnitX, -60f));
            Assert.Greater(weakSide, strongSide * Ranges()["pass"]["minWeakFootErrorGrowth"].Value<float>(), "pé ruim: +0-50% error.");
        }

        [Test]
        public void SameSeed_SamePass()
        {
            var a = Session(42, new[] { Player(), Player() }, new[] { Origin, new Vector3(20f, 0f, 0f) });
            var b = Session(42, new[] { Player(), Player() }, new[] { Origin, new Vector3(20f, 0f, 0f) });
            TakeControl(a);
            TakeControl(b);
            a.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);
            b.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);
            Assert.AreEqual(a.LastPass.AngleErrorDegrees, b.LastPass.AngleErrorDegrees);
            Assert.AreEqual(a.Ball.Velocity, b.Ball.Velocity);
        }

        [Test]
        public void PassPressedWhileTheBallIsArriving_IsPlayedFirstTime()
        {
            var s = Session(9, new[] { Player(80), Player(80), Player(80) },
                new[] { Origin, new Vector3(15f, 0f, 0f), new Vector3(15f, 15f, 0f) });
            TakeControl(s);
            s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);
            Assert.AreEqual(1, s.Control.Controlled);

            // Press (and release) pass towards the third player shortly before the ball reaches the receiver.
            bool pressed = false, firstTimePass = false;
            for (int i = 0; i < 300 && !firstTimePass; i++)
            {
                var receiver = s.Bodies[1].Position;
                float d = Vector2.Distance(new Vector2(s.Ball.Position.X, s.Ball.Position.Y), new Vector2(receiver.X, receiver.Y));
                var command = ActionCommand.None;
                if (!pressed && s.Ball.State != BallState.Controlled && d < 1.2f + s.Ball.Velocity.Length() * 0.08f)
                {
                    command = TapPass;
                    pressed = true;
                }
                var ev = s.Step(Vector2.Zero, false, command, Vector2.UnitY, Dt);
                if (ev == PracticeEvent.Pass) firstTimePass = s.LastKickFirstTime;
            }
            Assert.IsTrue(pressed, "the ball never approached the receiver.");
            Assert.IsTrue(firstTimePass, "GAME_DESIGN §19: de primeira = passe com bola chegando (buffer).");
            Assert.AreEqual(2, s.LastPass.Target);
        }

        [Test]
        public void ExpiredBuffer_DoesNothing()
        {
            var s = Session(1, new[] { Player(), Player() }, new[] { Origin, new Vector3(30f, 0f, 0f) });
            TakeControl(s);
            s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt);
            s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt); // receiver controlled, ball still ~30 m away
            var ev = RunUntilSettled(s, 6f);
            Assert.AreEqual(PracticeEvent.Captured, ev);
            Assert.AreEqual(BallState.Controlled, s.Ball.State, "a press far older than the ~150 ms buffer must not fire on arrival.");
        }

        [Test]
        public void Step_WithKicks_AllocatesNoMemory()
        {
            var s = Session(2, new[] { Player(), Player() }, new[] { Origin, new Vector3(15f, 0f, 0f) });
            TakeControl(s);
            s.Step(Vector2.Zero, false, TapPass, Vector2.UnitX, Dt); // warm-up (JIT)
            RunUntilSettled(s, 4f);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                var command = i % 100 == 0 ? TapPass : i % 100 == 50 ? new ActionCommand(ActionKind.Shot, 0.6f) : ActionCommand.None;
                s.Step(new Vector2(1f, 0.2f), i % 3 == 0, command, new Vector2(-1f, 0f), Dt);
                if (s.Ball.State == BallState.Dead) s.PlaceBallAtFeet(s.Control.Controlled);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(before, after, "CLAUDE.md §10: zero allocation per step in the match loop.");
        }

        private static float MeanAbsAngleError(Game.Core.Contracts.Match.MatchPlayerSetup player, int samples,
            float nearestOpponent = float.PositiveInfinity, Vector2? aim = null)
        {
            var db = Db();
            var rng = new Rng(123);
            var dir = aim ?? Vector2.UnitX;
            var bodies = new[] { new PlayerBody { Position = Origin, Facing = Vector3.UnitX }, new PlayerBody { Position = new Vector3(dir.X * 20f, dir.Y * 20f, 0f) } };
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                bodies[0].IgnoreBallUntilClear = false;
                var ball = new Ball { Position = new Vector3(0.6f, 0f, 0f), State = BallState.Controlled, Owner = 0 };
                var o = PassSystem.Execute(0, bodies, 2, ball, player, db.Balance, db.Kicking, db.Ball.Ball, PassKind.Ground, dir,
                    ActionCommand.AutoPower, false, nearestOpponent, Vector2.UnitX, rng);
                sum += Math.Abs(o.AngleErrorDegrees);
            }
            return (float)(sum / samples);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * MathF.PI / 180f;
            return new Vector2(v.X * MathF.Cos(r) - v.Y * MathF.Sin(r), v.X * MathF.Sin(r) + v.Y * MathF.Cos(r));
        }
    }
}
