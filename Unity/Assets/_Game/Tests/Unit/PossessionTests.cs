using System.Numerics;
using Game.Data.Loading;
using Game.Match;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>A2 acceptance (ROADMAP): posse só muda por evento válido.</summary>
    public class PossessionTests
    {
        private static GameDatabase _db;
        private static GameDatabase Db() => _db ?? (_db = Load());

        private static GameDatabase Load()
        {
            var r = GameDataLoader.Load(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        [Test]
        public void BallWithinRadius_IsCaptured()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = new Vector3(0.5f, 0f, 0f), Velocity = new Vector3(2f, 0f, 0f), State = BallState.Rolling };

            var ev = Possession.Step(0, body, ball, db.Movement);

            Assert.AreEqual(PossessionEvent.Captured, ev);
            Assert.AreEqual(BallState.Controlled, ball.State);
            Assert.AreEqual(Vector3.Zero, ball.Velocity, "a captured ball stops dead, it does not keep its old velocity.");
        }

        [Test]
        public void BallOutsideRadius_IsNotCaptured()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = new Vector3(50f, 0f, 0f), Velocity = new Vector3(2f, 0f, 0f), State = BallState.Rolling };

            var ev = Possession.Step(0, body, ball, db.Movement);

            Assert.AreEqual(PossessionEvent.None, ev);
            Assert.AreEqual(BallState.Rolling, ball.State, "possession must not change without a valid event.");
        }

        [Test]
        public void AlreadyControlledBall_NeverRetriggersCapture()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = new Vector3(0.1f, 0f, 0f), State = BallState.Controlled };

            var ev = Possession.Step(0, body, ball, db.Movement);

            Assert.AreEqual(PossessionEvent.None, ev, "an already-controlled ball is not a fresh capture.");
        }

        [Test]
        public void BallAboveCaptureHeight_IsNotCaptured()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = new Vector3(0.5f, 0f, db.Movement.PossessionCaptureMaxHeight + 0.5f), State = BallState.Airborne };

            Assert.AreEqual(PossessionEvent.None, Possession.Step(0, body, ball, db.Movement), "headers come in A9; feet/chest only.");
        }

        [Test]
        public void Capture_RecordsTheOwnerAndLastTouch()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = new Vector3(0.5f, 0f, 0f), State = BallState.Rolling };

            Possession.Step(3, body, ball, db.Movement);

            Assert.AreEqual(3, ball.Owner);
            Assert.AreEqual(3, ball.LastTouch);
        }

        [Test]
        public void Kicker_IgnoresTheBallUntilItHasLeftHisRadius()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = new Vector3(0.5f, 0f, 0f), State = BallState.Controlled, Owner = 0 };
            Possession.Kick(0, body, ball, new Vector3(10f, 0f, 0f));

            Assert.AreEqual(PossessionEvent.None, Possession.Step(0, body, ball, db.Movement));
            ball.Position = new Vector3(5f, 0f, 0f);
            Possession.Step(0, body, ball, db.Movement);
            ball.Position = new Vector3(0.5f, 0f, 0f);
            Assert.AreEqual(PossessionEvent.Captured, Possession.Step(0, body, ball, db.Movement), "once clear, the ball can be won back.");
        }

        [Test]
        public void KickAcrossTheBody_IsNotCaughtBackByTheKicker()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            // Dribbled ball at the edge of the radius, kicked back across the kicker (regression: the kicker caught his own shot).
            var ball = new Ball { Position = new Vector3(1.3f, 0f, 0f), State = BallState.Controlled, Owner = 0 };
            Possession.Kick(0, body, ball, new Vector3(-12f, 3f, 0f));
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(PossessionEvent.None, Possession.Step(0, body, ball, db.Movement), $"step {i}");
                ball.Position += ball.Velocity * (1f / 50f);
            }
        }

        [Test]
        public void DeadBall_IsNeverCaptured()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = Vector3.Zero, State = BallState.Dead };

            var ev = Possession.Step(0, body, ball, db.Movement);

            Assert.AreEqual(PossessionEvent.None, ev, "a dead ball stays fixed until the restart (TECHNICAL_SPEC §6).");
            Assert.AreEqual(BallState.Dead, ball.State);
        }

        [Test]
        public void Release_TurnsControlledIntoRollingOrAirborne()
        {
            var grounded = new Ball { Position = Vector3.Zero, Velocity = new Vector3(3f, 0f, 0f), State = BallState.Controlled };
            var ev = Possession.Release(grounded);
            Assert.AreEqual(PossessionEvent.Released, ev);
            Assert.AreEqual(BallState.Rolling, grounded.State);

            var lofted = new Ball { Position = Vector3.Zero, Velocity = new Vector3(3f, 0f, 5f), State = BallState.Controlled };
            Possession.Release(lofted);
            Assert.AreEqual(BallState.Airborne, lofted.State);
        }

        [Test]
        public void Release_OnANonControlledBall_IsNotAnEvent()
        {
            var ball = new Ball { State = BallState.Rolling };
            Assert.AreEqual(PossessionEvent.None, Possession.Release(ball));
            Assert.AreEqual(BallState.Rolling, ball.State);
        }
    }
}
