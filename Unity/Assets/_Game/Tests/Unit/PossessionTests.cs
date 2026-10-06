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

            var ev = Possession.Step(body, ball, db.Movement);

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

            var ev = Possession.Step(body, ball, db.Movement);

            Assert.AreEqual(PossessionEvent.None, ev);
            Assert.AreEqual(BallState.Rolling, ball.State, "possession must not change without a valid event.");
        }

        [Test]
        public void AlreadyControlledBall_NeverRetriggersCapture()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = new Vector3(0.1f, 0f, 0f), State = BallState.Controlled };

            var ev = Possession.Step(body, ball, db.Movement);

            Assert.AreEqual(PossessionEvent.None, ev, "an already-controlled ball is not a fresh capture.");
        }

        [Test]
        public void DeadBall_IsNeverCaptured()
        {
            var db = Db();
            var body = new PlayerBody { Position = Vector3.Zero };
            var ball = new Ball { Position = Vector3.Zero, State = BallState.Dead };

            var ev = Possession.Step(body, ball, db.Movement);

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
