using System;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>
    /// A2 acceptance (ROADMAP): tempos de aceleração e perdas em curva dentro das curvas do catálogo de efeitos;
    /// zero alocação no passo.
    /// </summary>
    public class MovementTests
    {
        private const float Dt = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz
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

        [TestCase(30)]
        [TestCase(50)]
        [TestCase(80)]
        public void Acceleration_ReachesSprintSpeed_WithinTheAttributesAccelTime(int ovr)
        {
            var db = Db();
            var player = Player(ovr);
            float sprintSpeed = db.Balance.Eval(Effect.SprintSpeed, player.Attributes);
            float accelTime = db.Balance.Eval(Effect.AccelTime, player.Attributes);

            var body = new PlayerBody();
            float elapsed = 0f;
            while (body.Velocity.Length() < sprintSpeed * 0.99f && elapsed < 5f)
            {
                Movement.Step(body, db.Balance, player, new Vector2(1f, 0f), sprintIntent: true, hasBall: false,
                    longTouch: false, db.Movement, db.Fatigue, Dt);
                elapsed += Dt;
            }

            Assert.LessOrEqual(elapsed, accelTime + 0.3f, $"ovr={ovr}: took longer than the attribute's AccelTime ({accelTime:F2}s) to reach sprint speed.");
        }

        [TestCase(30)]
        [TestCase(80)]
        public void Deceleration_StopsWithinTheAttributesDecelTime(int ovr)
        {
            var db = Db();
            var player = Player(ovr);
            float decelTime = db.Balance.Eval(Effect.DecelTime, player.Attributes);
            float sprintSpeed = db.Balance.Eval(Effect.SprintSpeed, player.Attributes);

            var body = new PlayerBody { Velocity = new Vector3(sprintSpeed, 0f, 0f) };
            float elapsed = 0f;
            while (body.Velocity.Length() > sprintSpeed * 0.01f && elapsed < 5f)
            {
                Movement.Step(body, db.Balance, player, Vector2.Zero, sprintIntent: false, hasBall: false,
                    longTouch: false, db.Movement, db.Fatigue, Dt);
                elapsed += Dt;
            }

            Assert.LessOrEqual(elapsed, decelTime + 0.3f, $"ovr={ovr}: took longer than the attribute's DecelTime ({decelTime:F2}s) to stop.");
        }

        [Test]
        public void SharpTurn_LosesSpeedByTheAttributesTurnSpeedLoss()
        {
            var db = Db();
            var player = Player(50);
            float sprintSpeed = db.Balance.Eval(Effect.SprintSpeed, player.Attributes);
            float turnLoss = db.Balance.Eval(Effect.TurnSpeedLoss, player.Attributes);

            var body = new PlayerBody { Velocity = new Vector3(sprintSpeed, 0f, 0f), Facing = Vector3.UnitX };
            float speedBefore = body.Velocity.Length();
            Movement.Step(body, db.Balance, player, new Vector2(-1f, 0f), sprintIntent: true, hasBall: false,
                longTouch: false, db.Movement, db.Fatigue, Dt);

            float expected = speedBefore * (1f - turnLoss);
            Assert.That(body.Velocity.Length(), Is.LessThan(speedBefore), "a 180° turn must cost speed.");
            Assert.That(body.Velocity.Length(), Is.EqualTo(expected).Within(0.05f * speedBefore),
                $"expected close to {expected:F2} (speed x (1 - TurnSpeedLoss {turnLoss:F2})).");
            Assert.Greater(body.TurnStunRemaining, 0f, "a sharp turn stuns briefly (TurnRecoverTime).");
        }

        [Test]
        public void MediumTurn_LosesOnlyTheFlatFraction_NotTheSharpOne()
        {
            var db = Db();
            var player = Player(50);
            float sprintSpeed = db.Balance.Eval(Effect.SprintSpeed, player.Attributes);
            float turnLoss = db.Balance.Eval(Effect.TurnSpeedLoss, player.Attributes);

            var body = new PlayerBody { Velocity = new Vector3(sprintSpeed, 0f, 0f), Facing = Vector3.UnitX };
            // 60°: between turnNoLossMaxDegrees (45) and turnMediumLossMaxDegrees (90).
            float rad = 60f * MathF.PI / 180f;
            var intent = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
            Movement.Step(body, db.Balance, player, intent, sprintIntent: true, hasBall: false, longTouch: false,
                db.Movement, db.Fatigue, Dt);

            float expectedFlat = sprintSpeed * (1f - db.Movement.MediumTurnSpeedLossFlat);
            float expectedSharp = sprintSpeed * (1f - turnLoss);
            Assert.That(body.Velocity.Length(), Is.EqualTo(expectedFlat).Within(0.05f * sprintSpeed));
            Assert.That(body.Velocity.Length(), Is.Not.EqualTo(expectedSharp).Within(0.001f),
                "a 45-90° turn must use the flat medium loss, not the attribute-scaled sharp one.");
            Assert.AreEqual(0f, body.TurnStunRemaining, "a medium turn does not stun.");
        }

        [Test]
        public void SmallTurn_LosesNoSpeed()
        {
            var db = Db();
            var player = Player(50);
            float sprintSpeed = db.Balance.Eval(Effect.SprintSpeed, player.Attributes);

            var body = new PlayerBody { Velocity = new Vector3(sprintSpeed, 0f, 0f), Facing = Vector3.UnitX };
            float rad = 20f * MathF.PI / 180f;
            var intent = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
            Movement.Step(body, db.Balance, player, intent, sprintIntent: true, hasBall: false, longTouch: false,
                db.Movement, db.Fatigue, Dt);

            Assert.That(body.Velocity.Length(), Is.EqualTo(sprintSpeed).Within(0.01f * sprintSpeed), "a turn under 45° must not cost speed.");
        }

        [Test]
        public void WithBall_IsSlowerThanWithoutIt()
        {
            var db = Db();
            var player = Player(50);
            var withBall = new PlayerBody();
            var withoutBall = new PlayerBody();
            for (int i = 0; i < 150; i++)
            {
                Movement.Step(withBall, db.Balance, player, new Vector2(1f, 0f), true, hasBall: true, longTouch: false, db.Movement, db.Fatigue, Dt);
                Movement.Step(withoutBall, db.Balance, player, new Vector2(1f, 0f), true, hasBall: false, longTouch: false, db.Movement, db.Fatigue, Dt);
            }
            Assert.Less(withBall.Velocity.Length(), withoutBall.Velocity.Length());
        }

        [Test]
        public void Step_AllocatesNoMemory()
        {
            var db = Db();
            var player = Player(50);
            var body = new PlayerBody();
            // Warm-up: first call pays for JIT/static-init allocations unrelated to the steady-state step.
            Movement.Step(body, db.Balance, player, new Vector2(1f, 0f), true, false, false, db.Movement, db.Fatigue, Dt);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                Movement.Step(body, db.Balance, player, new Vector2(1f, 0.3f), true, false, false, db.Movement, db.Fatigue, Dt);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "Movement.Step allocated memory (CLAUDE.md §10: zero alocação por frame).");
        }
    }
}
