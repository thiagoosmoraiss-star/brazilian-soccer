using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Effects;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>
    /// JSON -> EffectDefinition -> validator, with inline test documents (test schema, not production balance values).
    /// </summary>
    public class EffectsJsonTests
    {
        private static readonly string[] AllAttributes =
        {
            "Speed", "Agility", "Stamina", "Strength", "Passing", "Crossing", "Dribbling", "Finishing", "LongShots",
            "Heading", "Tackling", "Vision", "Positioning", "Composure", "GkReflexes", "GkPositioning", "GkAerial", "GkHandling",
        };

        private static EffectSchema TestSchema => new EffectSchema(new[] { "E0", "E1", "E2", "E3", "E4", "E5" });

        /// <summary>Six effects of three attributes each, covering all 18 attributes.</summary>
        private static string ValidDocument()
        {
            var effects = new List<string>();
            for (int e = 0; e < 6; e++)
            {
                var inputs = string.Join(",", Enumerable.Range(3 * e, 3).Select(i => $"{{\"attribute\":\"{AllAttributes[i]}\",\"weight\":1}}"));
                string mono = e == 0 ? ",\"monotonicity\":\"NonDecreasing\"" : "";
                effects.Add($"{{\"key\":\"E{e}\",\"inputs\":[{inputs}],\"curve\":[[1,0],[50,40.5],[99,100]],\"min\":0,\"max\":100,\"unit\":\"u\"{mono}}}");
            }
            return "{\"$schema\":\"./effects.schema.json\",\"schemaVersion\":1,\"effects\":[" + string.Join(",", effects) + "]}";
        }

        [Test]
        public void ValidJson_IsDeserializedIntoDefinitions()
        {
            var result = EffectsJsonReader.Read(ValidDocument());
            Assert.IsTrue(result.IsSuccess, result.ToString());
            Assert.AreEqual(6, result.Value.Count);

            var e0 = result.Value[0];
            Assert.AreEqual("E0", e0.Key);
            Assert.AreEqual(Monotonicity.NonDecreasing, e0.Monotonicity);
            Assert.AreEqual(Attr.Speed, e0.Inputs[0].Attribute);
            Assert.AreEqual(3, e0.Inputs.Count);
            Assert.AreEqual(3, e0.Curve.Points.Count);
            Assert.AreEqual(40.5f, e0.Curve.Points[1].Y);
            Assert.AreEqual(Monotonicity.None, result.Value[1].Monotonicity);
        }

        [Test]
        public void DeserializedDefinitions_PassTheValidator_AndEvaluate()
        {
            var defs = EffectsJsonReader.Read(ValidDocument()).Value;
            var catalog = BalanceCatalog.Create(TestSchema, defs);
            Assert.IsTrue(catalog.IsSuccess, catalog.ToString());

            var attrs = Enumerable.Repeat(50, AttrInfo.Count).ToArray();
            Assert.AreEqual(40.5f, catalog.Value.Eval(0, attrs), 1e-4f);
        }

        [Test]
        public void DeserializedDefinitions_WithSemanticProblems_AreRejectedByTheValidator()
        {
            // Structurally valid JSON, but one effect only and a curve violating its declared monotonicity.
            const string json = "{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"Speed\",\"weight\":1}]," +
                                "\"curve\":[[1,50],[99,10]],\"min\":0,\"max\":100,\"unit\":\"u\",\"monotonicity\":\"NonDecreasing\"}]}";
            var defs = EffectsJsonReader.Read(json);
            Assert.IsTrue(defs.IsSuccess, defs.ToString());
            var codes = BalanceValidator.Validate(TestSchema, defs.Value).Select(e => e.Code).ToList();
            Assert.Contains(BalanceValidator.MonotonicityViolated, codes);
            Assert.Contains(BalanceValidator.EffectWithoutDefinition, codes);
            Assert.Contains(BalanceValidator.AttributeWithoutEffect, codes);
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[}")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[]} trailing")]
        [TestCase("{\"schemaVersion\":1,\"schemaVersion\":1,\"effects\":[]}")]
        public void InvalidJson_IsRejected(string json)
        {
            var result = EffectsJsonReader.Read(json);
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(EffectsJsonReader.InvalidJson, result.Errors[0].Code, result.ToString());
        }

        [TestCase("[]", Description = "root is not an object")]
        [TestCase("{\"schemaVersion\":1}", Description = "missing effects")]
        [TestCase("{\"effects\":[]}", Description = "missing schemaVersion")]
        [TestCase("{\"schemaVersion\":\"1\",\"effects\":[]}", Description = "schemaVersion not an integer")]
        [TestCase("{\"schemaVersion\":1,\"effects\":{}}", Description = "effects not an array")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[],\"extra\":true}", Description = "unknown root property")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[42]}", Description = "effect not an object")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\"}]}", Description = "effect missing fields")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"Pace\",\"weight\":1}],\"curve\":[[1,0],[99,1]],\"min\":0,\"max\":1,\"unit\":\"u\"}]}", Description = "unknown attribute")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"speed\",\"weight\":1}],\"curve\":[[1,0],[99,1]],\"min\":0,\"max\":1,\"unit\":\"u\"}]}", Description = "attribute case mismatch")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"0\",\"weight\":1}],\"curve\":[[1,0],[99,1]],\"min\":0,\"max\":1,\"unit\":\"u\"}]}", Description = "numeric attribute")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"Speed\",\"weight\":\"1\"}],\"curve\":[[1,0],[99,1]],\"min\":0,\"max\":1,\"unit\":\"u\"}]}", Description = "weight as string")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"Speed\",\"weight\":1}],\"curve\":[[1,0,5],[99,1]],\"min\":0,\"max\":1,\"unit\":\"u\"}]}", Description = "curve point with 3 values")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"Speed\",\"weight\":1}],\"curve\":[[1,0],[99,1]],\"min\":0,\"max\":1,\"unit\":\"u\",\"monotonicity\":\"Up\"}]}", Description = "unknown monotonicity")]
        [TestCase("{\"schemaVersion\":1,\"effects\":[{\"key\":\"E0\",\"inputs\":[{\"attribute\":\"Speed\",\"weight\":1}],\"curve\":[[1,0],[99,1]],\"min\":0,\"max\":1,\"unit\":\"u\",\"typo\":1}]}", Description = "unknown effect property")]
        public void IncompatibleStructure_IsRejected(string json)
        {
            var result = EffectsJsonReader.Read(json);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.Errors.All(e => e.Code == EffectsJsonReader.InvalidStructure), result.ToString());
        }

        [Test]
        public void UnsupportedSchemaVersion_IsRejected()
        {
            var result = EffectsJsonReader.Read("{\"schemaVersion\":2,\"effects\":[]}");
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(EffectsJsonReader.UnsupportedSchemaVersion, result.Errors[0].Code);
        }

        [Test]
        public void Pipeline_FromDataSource_LoadsAndValidates()
        {
            string dir = Path.Combine(Path.GetTempPath(), "data-json-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "Balance"));
            try
            {
                File.WriteAllText(Path.Combine(dir, "Balance", "effects.json"), ValidDocument());
                var catalog = GameDataLoader.LoadBalanceCatalog(new DirectoryDataSource(dir), TestSchema);
                Assert.IsTrue(catalog.IsSuccess, catalog.ToString());

                File.WriteAllText(Path.Combine(dir, "Balance", "effects.json"), "{ not json");
                var broken = GameDataLoader.LoadBalanceCatalog(new DirectoryDataSource(dir), TestSchema);
                Assert.IsFalse(broken.IsSuccess);
                Assert.AreEqual(EffectsJsonReader.InvalidJson, broken.Errors[0].Code);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
