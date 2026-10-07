using System;
using System.IO;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A5 (ROADMAP: "desarme só com bola alcançável"; X-53: no dice): standing tackle geometry, Desarme reach,
    /// Força shielding, no ping-pong, contact separation.</summary>
    public class DefenseTests
    {
        private static bool Tackle(Vector3 carrierPos, Vector3 ballPos, Vector3 defenderPos, MatchPlayerSetup carrier = null, MatchPlayerSetup defender = null)
        {
            var db = Db();
            var c = new PlayerBody { Position = carrierPos };
            var d = new PlayerBody { Position = defenderPos };
            var ball = new Ball { Position = ballPos, State = BallState.Controlled, Owner = 0 };
            bool won = DefenseSystem.TryStandingTackle(1, d, defender ?? Player(), c, carrier ?? Player(), ball, db.Balance, db.Defense);
            if (won) Assert.AreEqual(1, ball.Owner, "the tackler owns the ball.");
            else Assert.AreEqual(0, ball.Owner, "a failed tackle changes nothing.");
            return won;
        }

        [Test]
        public void ExposedBallWithinReach_IsWon()
        {
            Assert.IsTrue(Tackle(Vector3.Zero, new Vector3(1.3f, 0f, 0f), new Vector3(2.0f, 0f, 0f)),
                "the carrier ran the ball into the defender: it is closer to him and within reach.");
        }

        [Test]
        public void BallOutOfReach_IsNeverWon()
        {
            Assert.IsFalse(Tackle(Vector3.Zero, new Vector3(1.3f, 0f, 0f), new Vector3(2.6f, 0f, 0f)), "1.3 m away is beyond a 1.0 m reach.");
        }

        [Test]
        public void BallCoveredByTheCarrier_IsNeverWon()
        {
            Assert.IsFalse(Tackle(Vector3.Zero, new Vector3(0.5f, 0f, 0f), new Vector3(0.9f, 0.6f, 0f)),
                "the ball is tight to the carrier's feet: within reach but not exposed.");
            Assert.IsFalse(Tackle(Vector3.Zero, new Vector3(1.3f, 0f, 0f), new Vector3(-0.9f, 0f, 0f)), "from behind the carrier.");
        }

        [Test]
        public void BetterTackling_ReachesFurther()
        {
            var weakCarrier = Player(70, overrides: (Attr.Strength, 30));
            var poor = Player(70, overrides: (Attr.Tackling, 30));
            var good = Player(70, overrides: (Attr.Tackling, 90));
            var ball = new Vector3(1.3f, 0f, 0f);
            var defenderAt = new Vector3(2.25f, 0f, 0f); // 0.95 m from the ball
            Assert.IsFalse(Tackle(Vector3.Zero, ball, defenderAt, weakCarrier, poor), "Desarme 30: reach 0.8 m.");
            Assert.IsTrue(Tackle(Vector3.Zero, ball, defenderAt, weakCarrier, good), "Desarme 90: reach 1.12 m.");
        }

        [Test]
        public void StrongerCarrier_ShieldsTheBallBetter()
        {
            var ball = new Vector3(1.3f, 0f, 0f);
            var defenderAt = new Vector3(2.15f, 0f, 0f);
            Assert.IsTrue(Tackle(Vector3.Zero, ball, defenderAt, Player(70, overrides: (Attr.Strength, 30))), "Força 30 shields little.");
            Assert.IsFalse(Tackle(Vector3.Zero, ball, defenderAt, Player(70, overrides: (Attr.Strength, 90))), "Força 90 holds him off.");
        }

        [Test]
        public void Dispossessed_CannotWinItStraightBack_AndStumbles()
        {
            var db = Db();
            var a = new PlayerBody { Position = Vector3.Zero };
            var b = new PlayerBody { Position = new Vector3(2.0f, 0f, 0f) };
            var ball = new Ball { Position = new Vector3(1.3f, 0f, 0f), State = BallState.Controlled, Owner = 0 };
            Assert.IsTrue(DefenseSystem.TryStandingTackle(1, b, Player(), a, Player(), ball, db.Balance, db.Defense));
            Assert.Greater(a.TurnStunRemaining, 0f, "the dispossessed carrier stumbles.");

            ball.Position = new Vector3(0.7f, 0f, 0f); // now exposed to the old carrier
            Assert.IsFalse(DefenseSystem.TryStandingTackle(0, a, Player(), b, Player(), ball, db.Balance, db.Defense), "no tackle ping-pong.");
            for (int i = 0; i < 100; i++) DefenseSystem.Tick(a, Dt);
            Assert.IsTrue(DefenseSystem.TryStandingTackle(0, a, Player(), b, Player(), ball, db.Balance, db.Defense), "after the cooldown he may.");
        }

        [Test]
        public void ReachCurve_IsAboutOneMetre()
        {
            var b = Db().Balance;
            Assert.That(b.Eval(Effect.TackleReach, Player(70).Attributes), Is.InRange(0.8f, 1.2f), "GAME_DESIGN §22: bola exposta a < ~1 m.");
        }

        [Test]
        public void RealDefenseFile_Loads()
        {
            var r = GameDataLoader.LoadDefense(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(0.25f, r.Value.SwitchHysteresis, 1e-5f, "GAME_DESIGN §17: histerese (~25% melhor).");
            Assert.AreEqual(1f, r.Value.PostManualLockSeconds, 1e-5f, "GAME_DESIGN §17: trava pós-manual 1 s.");
            Assert.AreEqual(2f, r.Value.BeatenDistance, 1e-5f, "GAME_DESIGN §17: batido/2 m atrás.");
        }

        [Test]
        public void HysteresisOfOne_IsRejected() =>
            Assert.IsFalse(DefenseReader.Read(ReadDefense().Replace("\"switchHysteresis\": 0.25", "\"switchHysteresis\": 1.0")).IsSuccess);

        [Test]
        public void DefenseUnknownKey_IsRejected() =>
            Assert.IsFalse(DefenseReader.Read(ReadDefense().Replace("\"shieldMeters\"", "\"shield\"")).IsSuccess);

        private static string ReadDefense() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "defense.json"));

        [Test]
        public void Contact_SeparatesOverlappingPlayers()
        {
            float r = Db().Movement.PlayerRadius;
            var bodies = new[]
            {
                new PlayerBody { Position = new Vector3(0f, 0f, 0f) },
                new PlayerBody { Position = new Vector3(0.3f, 0f, 0f) },
                new PlayerBody { Position = new Vector3(0.3f, 0f, 0f) }, // exactly on top of the previous one
                new PlayerBody { Position = new Vector3(10f, 0f, 0f) },
            };
            for (int pass = 0; pass < 10; pass++) Contact.Separate(bodies, bodies.Length, r);
            for (int i = 0; i < bodies.Length; i++)
            for (int j = i + 1; j < bodies.Length; j++)
                Assert.GreaterOrEqual(Vector3.Distance(bodies[i].Position, bodies[j].Position), 2f * r - 1e-3f, $"players {i} and {j} overlap.");
            Assert.AreEqual(new Vector3(10f, 0f, 0f), bodies[3].Position, "a player nobody touches does not move.");
        }
    }
}
