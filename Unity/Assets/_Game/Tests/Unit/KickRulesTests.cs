using System.IO;
using System.Numerics;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using Game.Rules.Match;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A3 unit tests: error factors (GAME_DESIGN §19/§20), weak-foot side (X-50), action buttons, control
    /// selection and Data/Balance/kicking.json.</summary>
    public class KickRulesTests
    {
        private static KickContext Clean => new KickContext(float.PositiveInfinity, 0f, false, false, 100f, 10f);

        [Test]
        public void WeakFoot_IsTheOneOnThePreferredFootSide_BeyondTheThreshold()
        {
            var c = Db().Kicking.Common;
            var facing = Vector2.UnitX;
            var hardRight = new Vector2(0.3f, -1f);  // ~73° to the right
            var hardLeft = new Vector2(0.3f, 1f);
            var slightRight = new Vector2(1f, -0.3f); // ~17°
            Assert.IsTrue(KickErrorRules.UsesWeakFoot(facing, hardRight, leftFooted: false, c), "right-footer kicking hard to his right opens up with the left.");
            Assert.IsFalse(KickErrorRules.UsesWeakFoot(facing, hardLeft, leftFooted: false, c));
            Assert.IsFalse(KickErrorRules.UsesWeakFoot(facing, slightRight, leftFooted: false, c));
            Assert.IsTrue(KickErrorRules.UsesWeakFoot(facing, hardLeft, leftFooted: true, c));
            Assert.IsFalse(KickErrorRules.UsesWeakFoot(facing, hardRight, leftFooted: true, c));
        }

        [Test]
        public void WeakFootPenalty_FallsWithTheWeakFootRating()
        {
            var db = Db();
            var ctx = new KickContext(float.PositiveInfinity, 0f, true, false, 100f, 10f);
            float wf1 = KickErrorRules.PassErrorMultiplier(db.Balance, Player(weakFoot: 1), db.Kicking, ctx);
            float wf3 = KickErrorRules.PassErrorMultiplier(db.Balance, Player(weakFoot: 3), db.Kicking, ctx);
            float wf5 = KickErrorRules.PassErrorMultiplier(db.Balance, Player(weakFoot: 5), db.Kicking, ctx);
            Assert.Greater(wf1, wf3);
            Assert.Greater(wf3, wf5);
            Assert.AreEqual(1f, wf5, 1e-5f, "weak foot 5 = two-footed, no penalty.");
            Assert.AreEqual(1f + db.Kicking.Pass.WeakFootMaxErrorPenalty, wf1, 1e-5f, "GAME_DESIGN §19: pé ruim +0-50%.");
        }

        [Test]
        public void Pressure_IsReducedByComposure()
        {
            var db = Db();
            var pressed = new KickContext(1f, 0f, false, false, 100f, 10f);
            float calm = KickErrorRules.PassErrorMultiplier(db.Balance, Player(70, overrides: (Attr.Composure, 90)), db.Kicking, pressed);
            float nervous = KickErrorRules.PassErrorMultiplier(db.Balance, Player(70, overrides: (Attr.Composure, 30)), db.Kicking, pressed);
            Assert.Less(calm, nervous);
            Assert.That(calm, Is.InRange(1.3f, 1.8f), "GAME_DESIGN §19: pressão < 2 m (+30-80%).");
            Assert.That(nervous, Is.InRange(1.3f, 1.8f));
            Assert.AreEqual(1f, KickErrorRules.PassErrorMultiplier(db.Balance, Player(), db.Kicking,
                new KickContext(3f, 0f, false, false, 100f, 10f)), 1e-5f, "no pressure beyond 2 m.");
        }

        [Test]
        public void ShotPressure_IsHarsherThanPassPressure()
        {
            var db = Db();
            var pressed = new KickContext(1f, 0f, false, false, 100f, 10f);
            float pass = KickErrorRules.PassErrorMultiplier(db.Balance, Player(), db.Kicking, pressed);
            float shot = KickErrorRules.ShotErrorMultiplier(db.Balance, Player(), db.Kicking, pressed);
            Assert.Greater(shot, pass, "GAME_DESIGN §20: pressão < 1,5 m (+40-100%) vs passe (+30-80%).");
        }

        [Test]
        public void Orientation_FirstTime_LowEnergyAndDistance_EachAddError()
        {
            var db = Db();
            var p = Player();
            float clean = KickErrorRules.PassErrorMultiplier(db.Balance, p, db.Kicking, Clean);
            Assert.AreEqual(1f, clean, 1e-5f);
            Assert.Greater(KickErrorRules.PassErrorMultiplier(db.Balance, p, db.Kicking, new KickContext(float.PositiveInfinity, 150f, false, false, 100f, 10f)), clean);
            Assert.AreEqual(1f + db.Kicking.Pass.FirstTimeErrorPenalty,
                KickErrorRules.PassErrorMultiplier(db.Balance, p, db.Kicking, new KickContext(float.PositiveInfinity, 0f, false, true, 100f, 10f)), 1e-5f);
            Assert.Greater(KickErrorRules.PassErrorMultiplier(db.Balance, p, db.Kicking, new KickContext(float.PositiveInfinity, 0f, false, false, 10f, 10f)), clean);
            Assert.AreEqual(clean, KickErrorRules.PassErrorMultiplier(db.Balance, p, db.Kicking, new KickContext(float.PositiveInfinity, 0f, false, false, 50f, 10f)), 1e-5f,
                "GAME_DESIGN §18: precision only drops below 40% energy.");
            Assert.Greater(KickErrorRules.PassErrorMultiplier(db.Balance, p, db.Kicking, new KickContext(float.PositiveInfinity, 0f, false, false, 100f, 40f)), clean);
        }

        [Test]
        public void TapPass_IsAutoForce_HeldPass_UsesTheBar()
        {
            var t = Timings();
            var f = new ActionInputFilter();
            f.Step(true, false, false, t, Dt);
            var tap = Release(f, ActionKind.Pass, 0.1f, t);
            Assert.AreEqual(ActionKind.Pass, tap.Kind);
            Assert.IsTrue(tap.IsAuto, "GAME_DESIGN §19: toque = o jogo resolve a força.");

            f.Step(true, false, false, t, Dt);
            var held = Release(f, ActionKind.Pass, 0.5f, t);
            Assert.IsFalse(held.IsAuto);
            Assert.AreEqual(0.5f / t.PassBarSeconds, held.Power, 0.05f, "segurar = você resolve.");
        }

        [Test]
        public void Shot_PowerComesFromTheHoldTime_AndCapsAtFull()
        {
            var t = Timings();
            var f = new ActionInputFilter();
            f.Step(false, false, true, t, Dt);
            var half = Release(f, ActionKind.Shot, t.ShotBarSeconds * 0.5f, t);
            Assert.AreEqual(ActionKind.Shot, half.Kind);
            Assert.AreEqual(0.5f, half.Power, 0.05f);

            f.Step(false, false, true, t, Dt);
            Assert.AreEqual(1f, Release(f, ActionKind.Shot, t.ShotBarSeconds * 3f, t).Power, 1e-5f);
        }

        [Test]
        public void OtherButtons_AreIgnored_WhileOneIsCharging()
        {
            var t = Timings();
            var f = new ActionInputFilter();
            f.Step(false, false, true, t, Dt);
            Assert.AreEqual(ActionKind.None, f.Step(true, true, true, t, Dt).Kind);
            var c = f.Step(true, true, false, t, Dt);
            Assert.AreEqual(ActionKind.Shot, c.Kind, "the shot that was charging fires; pass/through presses meanwhile are ignored.");
        }

        [Test]
        public void ControlSelection_FollowsCaptures()
        {
            var cs = new ControlSelection(0);
            cs.OnCaptured(2);
            Assert.AreEqual(2, cs.Controlled);
        }

        [Test]
        public void RealKickingFile_Loads()
        {
            var r = GameDataLoader.LoadKicking(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(25f, r.Value.Pass.ConeHalfAngleDegrees, "GAME_DESIGN §19: Semi ±25°.");
            Assert.AreEqual(0.8f, r.Value.Shot.PowerBarSeconds, 1e-5f, "GAME_DESIGN §20: barra ~0,8 s.");
        }

        [Test]
        public void IdealPowerAtOrAboveFull_IsRejected() =>
            Assert.IsFalse(KickingReader.Read(ReadKicking().Replace("\"idealPowerMax\": 0.75", "\"idealPowerMax\": 1.0")).IsSuccess);

        [Test]
        public void MaxPassSpeedBelowMin_IsRejected() =>
            Assert.IsFalse(KickingReader.Read(ReadKicking().Replace("\"maxSpeed\": 26.0", "\"maxSpeed\": 2.0")).IsSuccess);

        [Test]
        public void WeakFootLossRangeInverted_IsRejected() =>
            Assert.IsFalse(KickingReader.Read(ReadKicking().Replace("\"weakFootPowerLossMax\": 0.25", "\"weakFootPowerLossMax\": 0.05")).IsSuccess);

        [Test]
        public void UnknownKey_IsRejected() =>
            Assert.IsFalse(KickingReader.Read(ReadKicking().Replace("\"tapMaxSeconds\"", "\"tapMaxSecs\"")).IsSuccess);

        private static string ReadKicking() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "kicking.json"));

        private static KickTimings Timings()
        {
            var k = Db().Kicking;
            return new KickTimings(k.Common.TapMaxSeconds, k.Pass.PowerBarSeconds, k.Shot.PowerBarSeconds);
        }

        /// <summary>Keeps <paramref name="kind"/> held for <paramref name="seconds"/>, then releases it.</summary>
        private static ActionCommand Release(ActionInputFilter f, ActionKind kind, float seconds, KickTimings t)
        {
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps; i++)
                f.Step(kind == ActionKind.Pass, kind == ActionKind.Through, kind == ActionKind.Shot, t, Dt);
            return f.Step(false, false, false, t, Dt);
        }
    }
}
