using System.Numerics;
using Game.Data.Loading;
using Game.Match;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>
    /// A1 acceptance (ROADMAP): previsão = integração, energia não aumenta sem impulso, nunca atravessa
    /// trave/travessão, gol só com cruzamento completo da linha, saída detectada, determinismo.
    /// </summary>
    public class BallPhysicsTests
    {
        private const float Dt = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz

        private static (Pitch Pitch, Data.Match.BallParameters Cfg) Load()
        {
            var r = GameDataLoader.LoadBall(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            var def = r.Value;
            var pitch = new Pitch(def.Pitch.Length, def.Pitch.Width, def.Pitch.GoalWidth, def.Pitch.GoalHeight, def.Pitch.PostRadius);
            return (pitch, def.Ball);
        }

        [Test]
        public void RollingBall_DeceleratesToAStop_WithoutGainingSpeed()
        {
            var (pitch, cfg) = Load();
            var ball = new Ball { Position = Vector3.Zero };
            ball.Kick(new Vector3(10f, 0f, 0f), spin: 0f);
            Assert.AreEqual(BallState.Rolling, ball.State);

            float previousSpeed = ball.Velocity.Length();
            for (int i = 0; i < 300; i++)
            {
                BallPhysics.Step(ball, pitch, cfg, Dt);
                float speed = ball.Velocity.Length();
                Assert.LessOrEqual(speed, previousSpeed + 1e-5f, $"step {i}: rolling speed increased without a kick.");
                previousSpeed = speed;
                Assert.AreEqual(0f, ball.Position.Y, 1e-4f, "a straight roll must not drift sideways.");
                Assert.AreEqual(0f, ball.Position.Z, 1e-4f, "a rolling ball stays on the ground.");
            }
            Assert.AreEqual(0f, ball.Velocity.Length(), 1e-4f, "friction must fully stop the ball within 6 seconds.");
        }

        [Test]
        public void AirborneBall_NeverGainsMechanicalEnergyWithoutAKick()
        {
            var (pitch, cfg) = Load();
            var ball = new Ball { Position = new Vector3(0f, 0f, 0f) };
            ball.Kick(new Vector3(8f, 0f, 6f), spin: 0f);
            Assert.AreEqual(BallState.Airborne, ball.State);

            float previousEnergy = Energy(ball, cfg);
            int bounces = 0;
            for (int i = 0; i < 500 && ball.State != BallState.Dead; i++)
            {
                var ev = BallPhysics.Step(ball, pitch, cfg, Dt);
                if (ball.Position.Z <= 1e-4f && ball.State == BallState.Airborne) bounces++;
                float energy = Energy(ball, cfg);
                Assert.LessOrEqual(energy, previousEnergy + 1e-3f, $"step {i}: mechanical energy increased without an impulse (event={ev}).");
                previousEnergy = energy;
            }
            Assert.Greater(bounces, 0, "fixture must actually bounce at least once.");
        }

        private static float Energy(Ball ball, Data.Match.BallParameters cfg) =>
            0.5f * ball.Velocity.LengthSquared() + cfg.Gravity * ball.Position.Z;

        [Test]
        public void Prediction_MatchesTheActualIntegration_Exactly()
        {
            var (pitch, cfg) = Load();
            var real = new Ball { Position = new Vector3(-10f, 2f, 0f) };
            real.Kick(new Vector3(12f, -1f, 7f), spin: 3f);
            var predicted = real.Clone();

            for (int i = 0; i < 200; i++)
            {
                BallPhysics.Step(real, pitch, cfg, Dt);
                BallPhysics.Step(predicted, pitch, cfg, Dt);
                Assert.AreEqual(real.Position.X, predicted.Position.X, 1e-6f, $"step {i}");
                Assert.AreEqual(real.Position.Y, predicted.Position.Y, 1e-6f, $"step {i}");
                Assert.AreEqual(real.Position.Z, predicted.Position.Z, 1e-6f, $"step {i}");
                Assert.AreEqual(real.State, predicted.State, $"step {i}");
                if (real.State == BallState.Dead) break;
            }
        }

        [Test]
        public void SameInitialState_AlwaysProducesTheSameTrajectory()
        {
            var (pitch, cfg) = Load();
            Vector3 PositionAfter(int steps)
            {
                var ball = new Ball { Position = new Vector3(5f, -3f, 0f) };
                ball.Kick(new Vector3(-6f, 4f, 5f), spin: -2f);
                for (int i = 0; i < steps; i++) BallPhysics.Step(ball, pitch, cfg, Dt);
                return ball.Position;
            }

            Vector3 a = PositionAfter(80);
            Vector3 b = PositionAfter(80);
            Assert.AreEqual(a.X, b.X, 1e-9f);
            Assert.AreEqual(a.Y, b.Y, 1e-9f);
            Assert.AreEqual(a.Z, b.Z, 1e-9f);
        }

        [Test]
        public void ShotThroughTheOpenGoal_Scores()
        {
            var (pitch, cfg) = Load();
            var ball = new Ball { Position = new Vector3(pitch.AwayGoalLineX - 5f, 0f, 1f) };
            ball.Kick(new Vector3(25f, 0f, 0f), spin: 0f);

            BallEvent last = BallEvent.None;
            for (int i = 0; i < 100 && ball.State != BallState.Dead; i++) last = BallPhysics.Step(ball, pitch, cfg, Dt);

            Assert.AreEqual(BallEvent.Goal, last);
            Assert.AreEqual(BallState.Dead, ball.State);
        }

        [Test]
        public void ShotAimedAtTheCrossbar_NeverPassesThrough_BouncesBackInstead()
        {
            var (pitch, cfg) = Load();
            // Aimed dead-center at the crossbar (y=0, z=goalHeight), close enough that gravity's pull during the
            // short flight stays well inside the bar's collision radius: without collision handling, a fast
            // enough shot would otherwise tunnel straight through it.
            var ball = new Ball { Position = new Vector3(pitch.AwayGoalLineX - 1.5f, 0f, pitch.GoalHeight) };
            ball.Kick(new Vector3(60f, 0f, 0f), spin: 0f);

            bool hitBar = false;
            for (int i = 0; i < 50 && ball.State != BallState.Dead; i++)
            {
                var ev = BallPhysics.Step(ball, pitch, cfg, Dt);
                if (ev == BallEvent.PostHit) { hitBar = true; break; }
                Assert.Less(ball.Position.X, pitch.AwayGoalLineX + pitch.PostRadius + cfg.Radius,
                    $"step {i}: the ball passed the goal line height {ball.Position.Z:F2} without hitting the crossbar.");
            }
            Assert.IsTrue(hitBar, "a shot aimed straight at the crossbar must register a PostHit, never tunnel through.");
            Assert.AreNotEqual(BallState.Dead, ball.State, "a crossbar hit deflects the ball; it does not end the play.");
            Assert.Less(ball.Velocity.X, 0f, "the reflected ball must now move away from the goal.");
        }

        [Test]
        public void BallRollingPastTheSideline_IsDetectedAsOut()
        {
            var (pitch, cfg) = Load();
            var ball = new Ball { Position = new Vector3(0f, pitch.HalfWidth - 1f, 0f) };
            ball.Kick(new Vector3(0f, 10f, 0f), spin: 0f);

            BallEvent last = BallEvent.None;
            for (int i = 0; i < 50 && ball.State != BallState.Dead; i++) last = BallPhysics.Step(ball, pitch, cfg, Dt);

            Assert.AreEqual(BallEvent.Out, last);
            Assert.AreEqual(BallState.Dead, ball.State);
            Assert.GreaterOrEqual(ball.Position.Y, pitch.HalfWidth - 1e-3f, "must stop at (or just past) the touchline, not far beyond it.");
        }

        [Test]
        public void ShotWideOfTheGoal_IsDetectedAsOut_NotGoal()
        {
            var (pitch, cfg) = Load();
            var ball = new Ball { Position = new Vector3(pitch.AwayGoalLineX - 5f, pitch.HalfGoalWidth + 3f, 1f) };
            ball.Kick(new Vector3(25f, 0f, 0f), spin: 0f);

            BallEvent last = BallEvent.None;
            for (int i = 0; i < 100 && ball.State != BallState.Dead; i++) last = BallPhysics.Step(ball, pitch, cfg, Dt);

            Assert.AreEqual(BallEvent.Out, last, "wide of the post is out, never a goal.");
        }
    }
}
