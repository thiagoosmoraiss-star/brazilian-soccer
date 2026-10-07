using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Effects;
using Game.Match;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A3 scenario tests (ROADMAP): chute do meio-campo raramente é preciso; atributos alteram o erro na
    /// direção esperada; chute a gol vazio. Rates are over a fixed seed range, so they are reproducible.</summary>
    public class ShotScenarioTests
    {
        private static int Samples => Ranges()["shot"]["samples"].Value<int>();
        private static float HalfLength => Db().Ball.Pitch.Length * 0.5f;

        [Test]
        public void ShotFromThePenaltySpot_ByAGoodFinisher_UsuallyScoresInAnEmptyGoal()
        {
            int goals = Goals(Player(70, overrides: (Attr.Finishing, 70)), new Vector3(HalfLength - 11f, 0f, 0f), 0.6f, Vector2.Zero);
            Assert.GreaterOrEqual(goals, Samples * Ranges()["shot"]["minPenaltySpotGoalFraction"].Value<float>(), $"{goals}/{Samples} on target from 11 m.");
        }

        [Test]
        public void ShotFromMidfield_IsRarelyOnTarget_AtAnyPower()
        {
            foreach (float power in new[] { 0.5f, 0.75f, 0.9f, 1f })
            {
                int goals = Goals(Player(70, overrides: (Attr.LongShots, 50)), new Vector3(0f, 0f, 0f), power, Vector2.Zero);
                Assert.LessOrEqual(goals, Samples * Ranges()["shot"]["maxMidfieldGoalFraction"].Value<float>(), $"ROADMAP A3: chute do meio-campo raramente é preciso (power {power}: {goals}/{Samples}).");
            }
        }

        [Test]
        public void BetterFinishing_ScoresMoreFromInsideTheBox()
        {
            var spot = new Vector3(HalfLength - 12f, 4f, 0f);
            int low = Goals(Player(70, overrides: (Attr.Finishing, 30)), spot, 0.6f, Vector2.Zero);
            int high = Goals(Player(70, overrides: (Attr.Finishing, 90)), spot, 0.6f, Vector2.Zero);
            Assert.Greater(high, low, $"Finalização 90 ({high}) must beat Finalização 30 ({low}) inside the box.");
        }

        [Test]
        public void BetterLongShots_ScoresMoreFromOutsideTheBox()
        {
            var spot = new Vector3(HalfLength - 25f, 3f, 0f);
            int low = Goals(Player(70, overrides: (Attr.LongShots, 30)), spot, 0.7f, Vector2.Zero);
            int high = Goals(Player(70, overrides: (Attr.LongShots, 90)), spot, 0.7f, Vector2.Zero);
            Assert.Greater(high, low, $"Chute de longe 90 ({high}) must beat Chute de longe 30 ({low}) from 25 m.");
        }

        [Test]
        public void OverHittingTheBar_SendsMoreShotsOver()
        {
            var spot = new Vector3(HalfLength - 18f, 0f, 0f);
            int ideal = Goals(Player(70), spot, 0.7f, Vector2.Zero);
            int full = Goals(Player(70), spot, 1f, Vector2.Zero);
            Assert.Greater(ideal, full, "GAME_DESIGN §20: ideal 40-75%; acima cresce erro vertical.");
        }

        [Test]
        public void WeakFootAndPressure_ReduceShotSpeed()
        {
            var db = Db();
            var k = db.Kicking;
            var player = Player(70, weakFoot: 1);
            var clean = new Game.Rules.Match.KickContext(float.PositiveInfinity, 0f, false, false, 100f, 11f);
            var weak = new Game.Rules.Match.KickContext(float.PositiveInfinity, 0f, true, false, 100f, 11f);
            var pressed = new Game.Rules.Match.KickContext(1f, 0f, false, false, 100f, 11f);
            Assert.AreEqual(1f, Game.Rules.Match.KickErrorRules.ShotPowerMultiplier(player, k, clean));
            Assert.AreEqual(1f - k.Shot.WeakFootPowerLossMax, Game.Rules.Match.KickErrorRules.ShotPowerMultiplier(player, k, weak), 1e-5f);
            Assert.AreEqual(1f - k.Shot.PressurePowerLoss, Game.Rules.Match.KickErrorRules.ShotPowerMultiplier(player, k, pressed), 1e-5f);
        }

        [Test]
        public void NeutralStick_AimsAtTheFarCorner()
        {
            var db = Db();
            var pitch = Pitch.From(db.Ball.Pitch);
            float fromRight = ShotSystem.ChooseCornerY(new Vector2(HalfLength - 15f, -8f), Vector2.UnitX, Vector2.Zero, pitch.AwayGoalLineX, pitch, db.Kicking.Shot);
            float fromLeft = ShotSystem.ChooseCornerY(new Vector2(HalfLength - 15f, 8f), Vector2.UnitX, Vector2.Zero, pitch.AwayGoalLineX, pitch, db.Kicking.Shot);
            Assert.Greater(fromRight, 0f, "shooter on the -y side: far corner is +y.");
            Assert.Less(fromLeft, 0f, "shooter on the +y side: far corner is -y.");
        }

        [Test]
        public void StickSideways_PicksThatCorner()
        {
            var db = Db();
            var pitch = Pitch.From(db.Ball.Pitch);
            var from = new Vector2(HalfLength - 15f, -8f);
            Assert.Greater(ShotSystem.ChooseCornerY(from, Vector2.UnitX, new Vector2(1f, 1f), pitch.AwayGoalLineX, pitch, db.Kicking.Shot), 0f);
            Assert.Less(ShotSystem.ChooseCornerY(from, Vector2.UnitX, new Vector2(1f, -1f), pitch.AwayGoalLineX, pitch, db.Kicking.Shot), 0f,
                "stick to the right picks the near (-y) corner even though the far corner would be the default.");
        }

        private static int Goals(MatchPlayerSetup player, Vector3 spot, float power, Vector2 aim)
        {
            int goals = 0;
            for (ulong seed = 1; seed <= (ulong)Samples; seed++)
            {
                var s = Session(seed, new[] { player }, new[] { spot });
                TakeControl(s);
                Assert.AreEqual(PracticeEvent.Shot, s.Step(Vector2.Zero, false, new ActionCommand(ActionKind.Shot, power), aim, Dt));
                if (RunUntilSettled(s, 8f) == PracticeEvent.Goal) goals++;
            }
            return goals;
        }
    }
}
