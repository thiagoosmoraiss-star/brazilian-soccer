using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Effects;
using Game.Data.Loading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>
    /// Production effect catalog (D-20, baseline v1) checked against TECHNICAL_SPEC.md §8.
    /// Checks structure and direction only - not specific balance numbers, which are recalibrated in data.
    /// </summary>
    public class EffectCatalogTests
    {
        /// <summary>
        /// Attribute -> effects, transcribed from TECHNICAL_SPEC.md §8 (context-qualified rows as variants).
        /// Kept independent of effects.json so the data is checked against the spec, not against itself.
        /// </summary>
        private static readonly Dictionary<Attr, string[]> Spec = new Dictionary<Attr, string[]>
        {
            [Attr.Speed] = new[] { "SprintSpeed", "JogSpeed" },
            [Attr.Agility] = new[] { "AccelTime", "DecelTime", "TurnSpeedLoss", "TurnRecoverTime", "LooseBallReaction" },
            [Attr.Stamina] = new[] { "EnergyDrainMult", "LateMatchPenalty", "InjuryChance" },
            [Attr.Strength] = new[] { "BodyDuelWin", "ShieldStrength", "AerialDuel" },
            [Attr.Passing] = new[] { "PassAngleError", "PassPowerError", "PassBallSpeed" },
            [Attr.Crossing] = new[] { "CrossAngleError", "CrossQuality" },
            [Attr.Dribbling] = new[] { "DribbleTouchDistance", "FeintSuccess", "FirstTouchError", "TurnSpeedLossWithBall" },
            [Attr.Finishing] = new[] { "ShotAngleErrorInBox", "FinesseAccuracy" },
            [Attr.LongShots] = new[] { "ShotAngleErrorOutOfBox", "ShotPowerMax", "FreeKickAccuracy" },
            [Attr.Heading] = new[] { "HeaderAccuracy", "HeaderPower", "AerialDuel" },
            [Attr.Tackling] = new[] { "TackleWinChance", "FoulChance", "ShotBlockChance" },
            [Attr.Vision] = new[] { "ThroughBallError", "LeadCalcError", "AiPassOptionsCount", "RunTriggerThreshold" },
            [Attr.Positioning] = new[] { "AiTargetError", "AiCorrectionDelay" },
            [Attr.Composure] = new[] { "PressureErrorMult", "BigMatchErrorMult", "PenaltyAimWobble" },
            [Attr.GkReflexes] = new[] { "GkReactionTime", "GkDiveReach" },
            [Attr.GkPositioning] = new[] { "GkAngleError", "GkRushDecisionQuality" },
            [Attr.GkAerial] = new[] { "GkCrossClaimRange", "GkCrossDecisionQuality" },
            [Attr.GkHandling] = new[] { "GkCatchChance", "GkDistributionError" },
        };

        /// <summary>Effects where a higher attribute must lower the value (error, time, penalty, risk).</summary>
        private static readonly HashSet<string> Inverse = new HashSet<string>
        {
            "AccelTime", "DecelTime", "TurnSpeedLoss", "TurnRecoverTime", "LooseBallReaction",
            "EnergyDrainMult", "LateMatchPenalty", "InjuryChance",
            "PassAngleError", "PassPowerError", "CrossAngleError",
            "DribbleTouchDistance", "FirstTouchError", "TurnSpeedLossWithBall",
            "ShotAngleErrorInBox", "ShotAngleErrorOutOfBox", "HeaderAccuracy", "FoulChance",
            "ThroughBallError", "LeadCalcError", "RunTriggerThreshold",
            "AiTargetError", "AiCorrectionDelay",
            "PressureErrorMult", "BigMatchErrorMult", "PenaltyAimWobble",
            "GkReactionTime", "GkAngleError", "GkDistributionError",
        };

        private static IReadOnlyList<EffectDefinition> _definitions;

        private static IReadOnlyList<EffectDefinition> Definitions()
        {
            if (_definitions == null)
            {
                var result = GameDataLoader.LoadEffectDefinitions(new DirectoryDataSource(TestPaths.DataRoot()));
                Assert.IsTrue(result.IsSuccess, result.ToString());
                _definitions = result.Value;
            }
            return _definitions;
        }

        private static EffectDefinition Find(string key) => Definitions().Single(d => d.Key == key);

        [Test]
        public void EffectEnum_ContainsExactlyTheSpecifiedEffects()
        {
            var expected = Spec.Values.SelectMany(v => v).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var actual = Enum.GetNames(typeof(Effect)).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void EveryEnumEffect_IsDefinedOnce_InRealData()
        {
            var keys = Definitions().Select(d => d.Key).ToList();
            CollectionAssert.AllItemsAreUnique(keys);
            CollectionAssert.AreEquivalent(Enum.GetNames(typeof(Effect)), keys);
        }

        [Test]
        public void EverySpecifiedAttributeEffectPair_IsWiredInRealData()
        {
            foreach (var kv in Spec)
                foreach (var effect in kv.Value)
                    Assert.IsTrue(Find(effect).Inputs.Any(i => i.Attribute == kv.Key),
                        $"{effect} must use {kv.Key} (TECHNICAL_SPEC §8).");
        }

        [Test]
        public void All18Attributes_AreCovered()
        {
            var used = new HashSet<Attr>(Definitions().SelectMany(d => d.Inputs).Select(i => i.Attribute));
            Assert.AreEqual(AttrInfo.Count, used.Count);
        }

        [Test]
        public void Weights_AreValid()
        {
            foreach (var d in Definitions())
            {
                Assert.That(d.Inputs.Count, Is.InRange(1, 3), d.Key);
                foreach (var i in d.Inputs)
                    Assert.That(i.Weight, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f), $"{d.Key}/{i.Attribute}");
            }
        }

        [Test]
        public void Curves_HaveValidPoints_CoveringTheAttributeScale_AndAreNotLinear()
        {
            foreach (var d in Definitions())
            {
                var p = d.Curve.Points;
                Assert.GreaterOrEqual(p.Count, 3, d.Key + ": at least 3 points (non-linear)");
                Assert.AreEqual(AttrInfo.MinValue, p[0].X, d.Key);
                Assert.AreEqual(AttrInfo.MaxValue, p[p.Count - 1].X, d.Key);
                for (int i = 1; i < p.Count; i++) Assert.Greater(p[i].X, p[i - 1].X, d.Key);
                foreach (var pt in p) Assert.That(pt.Y, Is.InRange(d.Min, d.Max), d.Key);

                // Non-linear: slopes differ between segments.
                var slopes = Enumerable.Range(1, p.Count - 1)
                    .Select(i => Math.Round((p[i].Y - p[i - 1].Y) / (p[i].X - p[i - 1].X), 6)).Distinct().Count();
                Assert.Greater(slopes, 1, d.Key + " is linear");
            }
        }

        [Test]
        public void Curves_DeclareAndRespectMonotonicity_InTheSpecifiedDirection()
        {
            foreach (var d in Definitions())
            {
                var expected = Inverse.Contains(d.Key) ? Monotonicity.NonIncreasing : Monotonicity.NonDecreasing;
                Assert.AreEqual(expected, d.Monotonicity, d.Key);

                // Sample the curve itself, not only the declared flag.
                float prev = d.Curve.Evaluate(AttrInfo.MinValue);
                for (int x = AttrInfo.MinValue + 1; x <= AttrInfo.MaxValue; x++)
                {
                    float y = d.Curve.Evaluate(x);
                    if (expected == Monotonicity.NonDecreasing) Assert.GreaterOrEqual(y, prev, $"{d.Key} x={x}");
                    else Assert.LessOrEqual(y, prev, $"{d.Key} x={x}");
                    prev = y;
                }
                Assert.AreNotEqual(d.Curve.Evaluate(AttrInfo.MinValue), d.Curve.Evaluate(AttrInfo.MaxValue), d.Key + " is flat");
            }
        }

        [Test]
        public void RealData_InitializesBalance_AndEvaluatesEveryEffect()
        {
            var db = GameDataLoader.Load(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(db.IsSuccess, db.ToString());

            var average = Enumerable.Repeat(50, AttrInfo.Count).ToArray();
            var elite = Enumerable.Repeat(90, AttrInfo.Count).ToArray();
            foreach (Effect e in Enum.GetValues(typeof(Effect)))
            {
                var def = Find(e.ToString());
                float a = db.Value.Balance.Eval(e, average);
                float b = db.Value.Balance.Eval(e, elite);
                Assert.IsFalse(float.IsNaN(a) || float.IsInfinity(a), e.ToString());
                Assert.That(a, Is.InRange(def.Min, def.Max), e.ToString());
                // Better attributes move the effect in its declared direction.
                if (Inverse.Contains(e.ToString())) Assert.Less(b, a, e.ToString());
                else Assert.Greater(b, a, e.ToString());
            }
        }

        [Test]
        public void RealData_MatchesTheSchemaFile()
        {
            // The JSON Schema file documents the format; EffectsJsonReader enforces it (strict keys and types).
            // Newtonsoft.Json.Schema is not used: it is AGPL/commercial (cost rule). Here we check that the
            // schema file and the code agree, and that the real file is accepted by the strict reader.
            var schema = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "effects.schema.json")));
            var enumAttrs = schema["$defs"]["attribute"]["enum"].Values<string>().ToArray();
            CollectionAssert.AreEqual(Enum.GetNames(typeof(Attr)), enumAttrs);

            var effect = schema["$defs"]["effect"];
            CollectionAssert.AreEquivalent(new[] { "key", "inputs", "curve", "min", "max", "unit" },
                effect["required"].Values<string>().ToArray());
            Assert.AreEqual(BalanceValidator.MinInputs, effect["properties"]["inputs"]["minItems"].Value<int>());
            Assert.AreEqual(BalanceValidator.MaxInputs, effect["properties"]["inputs"]["maxItems"].Value<int>());
            Assert.AreEqual(EffectsJsonReader.SupportedSchemaVersion, schema["properties"]["schemaVersion"]["const"].Value<int>());
            CollectionAssert.AreEquivalent(Enum.GetNames(typeof(Monotonicity)),
                effect["properties"]["monotonicity"]["enum"].Values<string>().ToArray());

            var real = GameDataLoader.LoadEffectDefinitions(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(real.IsSuccess, real.ToString());
        }
    }
}
