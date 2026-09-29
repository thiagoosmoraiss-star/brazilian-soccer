using System.Collections.Generic;
using System.Linq;
using Game.Core.Results;
using Game.Data.Effects;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>
    /// Balance mechanism tests with a test-only schema and synthetic definitions (not production balance values).
    /// </summary>
    public class BalanceTests
    {
        private static readonly EffectSchema Schema = new EffectSchema(new[] { "Alpha", "Beta" });

        private static EffectDefinition Def(string key, Attr[] attrs, float[] weights = null,
            float[] curve = null, float min = 0f, float max = 100f, Monotonicity mono = Monotonicity.None, string unit = "u")
        {
            var inputs = attrs.Select((a, i) => new AttributeWeight(a, weights?[i] ?? 1f)).ToArray();
            curve = curve ?? new[] { 1f, 0f, 99f, 100f };
            var pts = new CurvePoint[curve.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new CurvePoint(curve[2 * i], curve[2 * i + 1]);
            return new EffectDefinition(key, inputs, new PiecewiseLinearCurve(pts), min, max, unit, mono);
        }

        /// <summary>A valid catalog: Alpha uses attributes 0..8, Beta uses 9..17 (3 each would exceed; use split).</summary>
        private static List<EffectDefinition> ValidDefinitions()
        {
            // Every attribute must feed at least one effect; each effect takes 1-3 attributes.
            var defs = new List<EffectDefinition>
            {
                Def("Alpha", new[] { Attr.Speed }, mono: Monotonicity.NonDecreasing),
                Def("Beta", new[] { Attr.Passing, Attr.Vision }, new[] { 3f, 1f }),
            };
            return defs;
        }

        /// <summary>Schema with enough effects so all 18 attributes can be covered (6 effects x 3 attributes).</summary>
        private static EffectSchema FullSchema() => new EffectSchema(new[] { "E0", "E1", "E2", "E3", "E4", "E5" });

        private static List<EffectDefinition> FullDefinitions()
        {
            var defs = new List<EffectDefinition>();
            for (int e = 0; e < 6; e++)
                defs.Add(Def("E" + e, new[] { (Attr)(3 * e), (Attr)(3 * e + 1), (Attr)(3 * e + 2) }));
            return defs;
        }

        private static IEnumerable<string> Codes(IReadOnlyList<Error> errors) => errors.Select(e => e.Code);

        [Test]
        public void ValidCatalog_PassesValidation()
        {
            var errors = BalanceValidator.Validate(FullSchema(), FullDefinitions());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Rejects_EffectWithoutDefinition()
        {
            var defs = FullDefinitions();
            var schema = new EffectSchema(new[] { "E0", "E1", "E2", "E3", "E4", "E5", "Missing" });
            var errors = BalanceValidator.Validate(schema, defs);
            Assert.Contains(BalanceValidator.EffectWithoutDefinition, Codes(errors).ToList());
            Assert.That(errors.Single().Message, Does.Contain("Missing"));
        }

        [Test]
        public void Rejects_AttributeWithoutEffect()
        {
            var errors = BalanceValidator.Validate(Schema, ValidDefinitions());
            var missing = errors.Where(e => e.Code == BalanceValidator.AttributeWithoutEffect).ToList();
            // Speed, Passing, Vision are used; the other 15 are not.
            Assert.AreEqual(15, missing.Count);
            Assert.IsFalse(missing.Any(e => e.Message.Contains("'Speed'")));
        }

        [Test]
        public void Rejects_UnknownAndDuplicateEffects()
        {
            var defs = FullDefinitions();
            defs.Add(Def("E0", new[] { Attr.Speed }));
            defs.Add(Def("Ghost", new[] { Attr.Speed }));
            var codes = Codes(BalanceValidator.Validate(FullSchema(), defs)).ToList();
            Assert.Contains(BalanceValidator.DuplicateEffect, codes);
            Assert.Contains(BalanceValidator.UnknownEffect, codes);
        }

        [Test]
        public void Rejects_InvalidInputs()
        {
            var defs = FullDefinitions();
            defs[0] = Def("E0", new[] { Attr.Speed, Attr.Agility, Attr.Stamina, Attr.Strength }); // 4 inputs
            defs[1] = Def("E1", new Attr[0]);                                                     // 0 inputs
            defs[2] = Def("E2", new[] { Attr.Crossing, Attr.Crossing });                          // duplicate
            defs[3] = Def("E3", new[] { Attr.Heading }, new[] { 0f });                            // weight 0
            defs[4] = Def("E4", new[] { (Attr)99 });                                              // unknown attribute
            var codes = Codes(BalanceValidator.Validate(FullSchema(), defs)).ToList();
            Assert.Contains(BalanceValidator.InvalidInputCount, codes);
            Assert.Contains(BalanceValidator.DuplicateInput, codes);
            Assert.Contains(BalanceValidator.InvalidWeight, codes);
            Assert.Contains(BalanceValidator.InvalidAttribute, codes);
        }

        [Test]
        public void Rejects_InvalidCurvesAndRanges()
        {
            var defs = FullDefinitions();
            defs[0] = Def("E0", new[] { Attr.Speed, Attr.Agility, Attr.Stamina }, curve: new[] { 1f, 0f });                // 1 point
            defs[1] = Def("E1", new[] { Attr.Strength, Attr.Passing, Attr.Crossing }, curve: new[] { 50f, 0f, 50f, 1f }); // x not increasing
            defs[2] = Def("E2", new[] { Attr.Dribbling, Attr.Finishing, Attr.LongShots }, min: 10f, max: 5f);            // min > max
            defs[3] = Def("E3", new[] { Attr.Heading, Attr.Tackling, Attr.Vision }, curve: new[] { 1f, 0f, 99f, 500f }); // y outside range
            defs[4] = Def("E4", new[] { Attr.Positioning, Attr.Composure, Attr.GkReflexes }, unit: " ");                // no unit
            defs[5] = Def("E5", new[] { Attr.GkPositioning, Attr.GkAerial, Attr.GkHandling }, curve: new[] { 1f, float.NaN, 99f, 1f });
            var codes = Codes(BalanceValidator.Validate(FullSchema(), defs)).ToList();
            Assert.Contains(BalanceValidator.CurveTooFewPoints, codes);
            Assert.Contains(BalanceValidator.CurveXNotIncreasing, codes);
            Assert.Contains(BalanceValidator.InvalidRange, codes);
            Assert.Contains(BalanceValidator.CurveOutsideRange, codes);
            Assert.Contains(BalanceValidator.MissingUnit, codes);
            Assert.Contains(BalanceValidator.CurveNotFinite, codes);
        }

        [Test]
        public void Rejects_DeclaredMonotonicityViolation()
        {
            var defs = FullDefinitions();
            defs[0] = Def("E0", new[] { Attr.Speed, Attr.Agility, Attr.Stamina },
                curve: new[] { 1f, 0f, 50f, 60f, 99f, 40f }, mono: Monotonicity.NonDecreasing);
            defs[1] = Def("E1", new[] { Attr.Strength, Attr.Passing, Attr.Crossing },
                curve: new[] { 1f, 90f, 50f, 20f, 99f, 30f }, mono: Monotonicity.NonIncreasing);
            var errors = BalanceValidator.Validate(FullSchema(), defs);
            Assert.AreEqual(2, errors.Count(e => e.Code == BalanceValidator.MonotonicityViolated), string.Join("\n", errors));
        }

        [Test]
        public void Catalog_Create_FailsWithErrors_WhenInvalid()
        {
            var result = BalanceCatalog.Create(Schema, ValidDefinitions());
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotEmpty(result.Errors);
        }

        private static int[] Attributes(int value)
        {
            var a = new int[AttrInfo.Count];
            for (int i = 0; i < a.Length; i++) a[i] = value;
            return a;
        }

        private static BalanceCatalog FullCatalog(List<EffectDefinition> defs)
        {
            var result = BalanceCatalog.Create(FullSchema(), defs);
            Assert.IsTrue(result.IsSuccess, result.ToString());
            return result.Value;
        }

        [Test]
        public void Eval_UsesWeightedAttributes_ThroughCurve()
        {
            var defs = FullDefinitions();
            // E0: Speed x3, Agility x1 -> x = (3*80 + 1*40)/4 = 70; curve y = x (identity over 0..100).
            defs[0] = Def("E0", new[] { Attr.Speed, Attr.Agility, Attr.Stamina }, new[] { 3f, 1f, 0.0001f },
                curve: new[] { 0f, 0f, 100f, 100f });
            var catalog = FullCatalog(defs);
            var attrs = Attributes(0);
            attrs[(int)Attr.Speed] = 80;
            attrs[(int)Attr.Agility] = 40;
            attrs[(int)Attr.Stamina] = 0;
            Assert.AreEqual(70f, catalog.Eval(0, attrs), 0.01f);
        }

        [Test]
        public void Eval_ClampsToDefinitionRange()
        {
            var defs = FullDefinitions();
            defs[0] = Def("E0", new[] { Attr.Speed, Attr.Agility, Attr.Stamina }, curve: new[] { 1f, 10f, 99f, 20f }, min: 10f, max: 20f);
            var catalog = FullCatalog(defs);
            var post = new[] { new Modifier(ModifierSource.Morale, ModifierStage.PostCurve, ModifierOp.Add, 1000f) };
            Assert.AreEqual(20f, catalog.Eval(0, Attributes(50), post));
        }

        [Test]
        public void Eval_AppliesModifiers_InFixedOrder_RegardlessOfInputOrder()
        {
            var defs = FullDefinitions();
            defs[0] = Def("E0", new[] { Attr.Speed, Attr.Agility, Attr.Stamina }, curve: new[] { 0f, 0f, 1000f, 1000f }, max: 1000f);
            var catalog = FullCatalog(defs);
            var attrs = Attributes(50);

            // Fixed order: Energy (x0.5) then Morale (+10) then Form (x2) => ((50*0.5)+10)*2 = 70.
            var energy = new Modifier(ModifierSource.Energy, ModifierStage.PreCurve, ModifierOp.Multiply, 0.5f);
            var morale = new Modifier(ModifierSource.Morale, ModifierStage.PreCurve, ModifierOp.Add, 10f);
            var form = new Modifier(ModifierSource.Form, ModifierStage.PreCurve, ModifierOp.Multiply, 2f);

            Assert.AreEqual(70f, catalog.Eval(0, attrs, new[] { energy, morale, form }), 1e-4f);
            Assert.AreEqual(70f, catalog.Eval(0, attrs, new[] { form, morale, energy }), 1e-4f);
            Assert.AreEqual(70f, catalog.Eval(0, attrs, new[] { morale, form, energy }), 1e-4f);
        }

        [Test]
        public void Eval_PreCurveModifiersActOnInput_PostCurveOnOutput()
        {
            var defs = FullDefinitions();
            // Curve maps 1..99 -> 0..10, so pre (input) and post (output) multiplications differ measurably.
            defs[0] = Def("E0", new[] { Attr.Speed, Attr.Agility, Attr.Stamina }, curve: new[] { 0f, 0f, 100f, 10f }, max: 100f);
            var catalog = FullCatalog(defs);
            var attrs = Attributes(50);
            var pre = new[] { new Modifier(ModifierSource.OutOfPosition, ModifierStage.PreCurve, ModifierOp.Multiply, 0.9f) };
            var post = new[] { new Modifier(ModifierSource.OutOfPosition, ModifierStage.PostCurve, ModifierOp.Add, 1f) };
            Assert.AreEqual(4.5f, catalog.Eval(0, attrs, pre), 1e-4f);
            Assert.AreEqual(6f, catalog.Eval(0, attrs, post), 1e-4f);
        }

        [Test]
        public void GameEffectEnum_ProducesValidSchema()
        {
            var schema = EffectSchema.FromEffectEnum();
            Assert.AreEqual(System.Enum.GetValues(typeof(Effect)).Length, schema.Count);
        }

        [Test]
        public void AttrEnum_Has18ContiguousAttributes()
        {
            var values = (Attr[])System.Enum.GetValues(typeof(Attr));
            Assert.AreEqual(AttrInfo.Count, values.Length);
            for (int i = 0; i < values.Length; i++) Assert.AreEqual(i, (int)values[i]);
        }
    }
}
