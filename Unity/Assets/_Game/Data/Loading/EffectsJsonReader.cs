using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Results;
using Game.Data.Effects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Data.Loading
{
    /// <summary>
    /// Parses Data/Balance/effects.json (format: effects.schema.json) into <see cref="EffectDefinition"/>s.
    /// Uses Newtonsoft JSON (D-11) through LINQ to JSON: explicit, strict and reflection-free (IL2CPP-safe).
    /// Only syntax and structure are checked here; semantic rules live in <see cref="BalanceValidator"/>.
    /// </summary>
    public static class EffectsJsonReader
    {
        public const int SupportedSchemaVersion = 1;

        public const string InvalidJson = "INVALID_JSON";
        public const string InvalidStructure = "INVALID_STRUCTURE";
        public const string UnsupportedSchemaVersion = "UNSUPPORTED_SCHEMA_VERSION";

        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load,
        };

        private static readonly HashSet<string> RootKeys = new HashSet<string> { "$schema", "schemaVersion", "effects" };
        private static readonly HashSet<string> EffectKeys = new HashSet<string> { "key", "inputs", "curve", "min", "max", "unit", "monotonicity" };
        private static readonly HashSet<string> InputKeys = new HashSet<string> { "attribute", "weight" };

        public static Result<IReadOnlyList<EffectDefinition>> Read(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            JToken root;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { FloatParseHandling = FloatParseHandling.Double })
                {
                    root = JToken.ReadFrom(reader, LoadSettings);
                    if (reader.Read() && reader.TokenType != JsonToken.Comment)
                        return Fail(InvalidJson, "Unexpected content after the root value.");
                }
            }
            catch (JsonReaderException e)
            {
                return Fail(InvalidJson, e.Message);
            }

            var errors = new List<Error>();
            if (!(root is JObject obj))
                return Fail(InvalidStructure, "Root must be an object.");

            CheckKeys(obj, RootKeys, "root", errors);

            var version = obj["schemaVersion"];
            if (version == null || version.Type != JTokenType.Integer)
                errors.Add(new Error(InvalidStructure, "'schemaVersion' must be an integer."));
            else if (version.Value<long>() != SupportedSchemaVersion)
                return Fail(UnsupportedSchemaVersion, $"schemaVersion {version} is not supported (expected {SupportedSchemaVersion}).");

            var definitions = new List<EffectDefinition>();
            if (!(obj["effects"] is JArray effects))
            {
                errors.Add(new Error(InvalidStructure, "'effects' must be an array."));
            }
            else
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var def = ReadEffect(effects[i], "effects[" + i + "]", errors);
                    if (def != null) definitions.Add(def);
                }
            }

            return errors.Count > 0
                ? Result<IReadOnlyList<EffectDefinition>>.Fail(errors)
                : Result<IReadOnlyList<EffectDefinition>>.Ok(definitions);
        }

        private static EffectDefinition ReadEffect(JToken token, string path, List<Error> errors)
        {
            if (!(token is JObject e))
            {
                errors.Add(new Error(InvalidStructure, path + " must be an object."));
                return null;
            }
            int before = errors.Count;
            CheckKeys(e, EffectKeys, path, errors);

            string key = ReadString(e, "key", path, errors, required: true);
            string unit = ReadString(e, "unit", path, errors, required: true);
            float min = ReadNumber(e["min"], path + ".min", errors);
            float max = ReadNumber(e["max"], path + ".max", errors);

            var mono = Monotonicity.None;
            string monoText = ReadString(e, "monotonicity", path, errors, required: false);
            if (monoText != null && !TryParseEnum(monoText, out mono))
                errors.Add(new Error(InvalidStructure, $"{path}.monotonicity: unknown value '{monoText}'."));

            var inputs = new List<AttributeWeight>();
            if (!(e["inputs"] is JArray inputArray))
                errors.Add(new Error(InvalidStructure, path + ".inputs must be an array."));
            else
                for (int i = 0; i < inputArray.Count; i++)
                    ReadInput(inputArray[i], $"{path}.inputs[{i}]", inputs, errors);

            var points = new List<CurvePoint>();
            if (!(e["curve"] is JArray curve))
                errors.Add(new Error(InvalidStructure, path + ".curve must be an array."));
            else
                for (int i = 0; i < curve.Count; i++)
                {
                    string p = $"{path}.curve[{i}]";
                    if (!(curve[i] is JArray pair) || pair.Count != 2)
                    {
                        errors.Add(new Error(InvalidStructure, p + " must be an [x, y] pair."));
                        continue;
                    }
                    points.Add(new CurvePoint(ReadNumber(pair[0], p + "[0]", errors), ReadNumber(pair[1], p + "[1]", errors)));
                }

            if (errors.Count > before) return null;
            return new EffectDefinition(key, inputs, new PiecewiseLinearCurve(points), min, max, unit, mono);
        }

        private static void ReadInput(JToken token, string path, List<AttributeWeight> inputs, List<Error> errors)
        {
            if (!(token is JObject input))
            {
                errors.Add(new Error(InvalidStructure, path + " must be an object."));
                return;
            }
            CheckKeys(input, InputKeys, path, errors);
            string attrText = ReadString(input, "attribute", path, errors, required: true);
            float weight = ReadNumber(input["weight"], path + ".weight", errors);
            if (attrText == null) return;
            if (!TryParseEnum(attrText, out Attr attr))
            {
                errors.Add(new Error(InvalidStructure, $"{path}.attribute: unknown attribute '{attrText}'."));
                return;
            }
            inputs.Add(new AttributeWeight(attr, weight));
        }

        private static void CheckKeys(JObject obj, HashSet<string> allowed, string path, List<Error> errors)
        {
            foreach (var prop in obj.Properties())
                if (!allowed.Contains(prop.Name))
                    errors.Add(new Error(InvalidStructure, $"{path}: unknown property '{prop.Name}'."));
        }

        private static string ReadString(JObject obj, string name, string path, List<Error> errors, bool required)
        {
            var token = obj[name];
            if (token == null)
            {
                if (required) errors.Add(new Error(InvalidStructure, $"{path}.{name} is required."));
                return null;
            }
            if (token.Type != JTokenType.String)
            {
                errors.Add(new Error(InvalidStructure, $"{path}.{name} must be a string."));
                return null;
            }
            return token.Value<string>();
        }

        private static float ReadNumber(JToken token, string path, List<Error> errors)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float))
            {
                errors.Add(new Error(InvalidStructure, path + " must be a number."));
                return 0f;
            }
            return (float)token.Value<double>();
        }

        // Exact, case-sensitive names only (no numeric values), so data cannot silently alias enum members.
        private static bool TryParseEnum<T>(string text, out T value) where T : struct, Enum
        {
            value = default;
            if (string.IsNullOrEmpty(text) || !Array.Exists(Enum.GetNames(typeof(T)), n => n == text)) return false;
            value = (T)Enum.Parse(typeof(T), text);
            return true;
        }

        private static Result<IReadOnlyList<EffectDefinition>> Fail(string code, string message) =>
            Result<IReadOnlyList<EffectDefinition>>.Fail(code, message);
    }
}
