using System;
using System.Collections.Generic;
using Game.Core.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Data.Loading
{
    /// <summary>
    /// Strict, reflection-free JSON reading helpers (Newtonsoft LINQ to JSON, D-11) shared by the data readers.
    /// Every accessor records an INVALID_STRUCTURE error with the JSON path instead of throwing.
    /// </summary>
    internal sealed class StrictJson
    {
        public const string InvalidJson = "INVALID_JSON";
        public const string InvalidStructure = "INVALID_STRUCTURE";
        public const string UnsupportedSchemaVersion = "UNSUPPORTED_SCHEMA_VERSION";

        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Ignore,
        };

        public readonly List<Error> Errors = new List<Error>();
        public readonly string File;

        public StrictJson(string file) { File = file; }

        public bool Ok => Errors.Count == 0;

        /// <summary>Parses the document and checks the root object and its schemaVersion.</summary>
        public JObject ParseRoot(string json, int supportedVersion, params string[] rootKeys)
        {
            JToken root;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)))
                {
                    root = JToken.ReadFrom(reader, LoadSettings);
                    if (reader.Read() && reader.TokenType != JsonToken.Comment)
                    {
                        Fail(InvalidJson, "Unexpected content after the root value.");
                        return null;
                    }
                }
            }
            catch (JsonReaderException e)
            {
                Fail(InvalidJson, e.Message);
                return null;
            }

            if (!(root is JObject obj))
            {
                Fail(InvalidStructure, "Root must be an object.");
                return null;
            }

            var keys = new List<string>(rootKeys) { "$schema", "schemaVersion" };
            Keys(obj, "root", keys.ToArray());
            var version = obj["schemaVersion"];
            if (version == null || version.Type != JTokenType.Integer)
                Fail(InvalidStructure, "'schemaVersion' must be an integer.");
            else if (version.Value<long>() != supportedVersion)
                Fail(UnsupportedSchemaVersion, $"schemaVersion {version} is not supported (expected {supportedVersion}).");
            return Ok ? obj : null;
        }

        public void Fail(string code, string message) => Errors.Add(new Error(code, File + ": " + message));

        public void Keys(JObject obj, string path, params string[] allowed)
        {
            foreach (var prop in obj.Properties())
                if (System.Array.IndexOf(allowed, prop.Name) < 0)
                    Fail(InvalidStructure, $"{path}: unknown property '{prop.Name}'.");
        }

        public JObject Object(JToken parent, string name, string path)
        {
            var t = parent?[name];
            if (t is JObject o) return o;
            Fail(InvalidStructure, $"{path}.{name} must be an object.");
            return null;
        }

        public JArray Array(JToken parent, string name, string path)
        {
            var t = parent?[name];
            if (t is JArray a) return a;
            Fail(InvalidStructure, $"{path}.{name} must be an array.");
            return null;
        }

        public JObject AsObject(JToken t, string path)
        {
            if (t is JObject o) return o;
            Fail(InvalidStructure, path + " must be an object.");
            return null;
        }

        public string String(JToken parent, string name, string path) => AsString(parent?[name], $"{path}.{name}");

        public string AsString(JToken t, string path)
        {
            if (t != null && t.Type == JTokenType.String) return t.Value<string>();
            Fail(InvalidStructure, path + " must be a string.");
            return null;
        }

        public int Int(JToken parent, string name, string path) => AsInt(parent?[name], $"{path}.{name}");

        public int AsInt(JToken t, string path)
        {
            if (t != null && t.Type == JTokenType.Integer)
            {
                long v = t.Value<long>();
                if (v >= int.MinValue && v <= int.MaxValue) return (int)v;
            }
            Fail(InvalidStructure, path + " must be an integer.");
            return 0;
        }

        public long Long(JToken parent, string name, string path)
        {
            var t = parent?[name];
            if (t != null && t.Type == JTokenType.Integer) return t.Value<long>();
            Fail(InvalidStructure, $"{path}.{name} must be an integer.");
            return 0;
        }

        public float Float(JToken parent, string name, string path) => AsFloat(parent?[name], $"{path}.{name}");

        public float AsFloat(JToken t, string path)
        {
            if (t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float)) return (float)t.Value<double>();
            Fail(InvalidStructure, path + " must be a number.");
            return 0f;
        }

        public bool Bool(JToken parent, string name, string path)
        {
            var t = parent?[name];
            if (t != null && t.Type == JTokenType.Boolean) return t.Value<bool>();
            Fail(InvalidStructure, $"{path}.{name} must be a boolean.");
            return false;
        }

        /// <summary>Reads an integer range written as [min, max].</summary>
        public IntRange IntRange(JToken parent, string name, string path) => AsIntRange(parent?[name], $"{path}.{name}");

        public IntRange AsIntRange(JToken t, string path)
        {
            if (t is JArray a && a.Count == 2)
                return new IntRange(AsInt(a[0], path + "[0]"), AsInt(a[1], path + "[1]"));
            Fail(InvalidStructure, path + " must be an [min, max] integer pair.");
            return default;
        }

        public List<string> StringList(JToken parent, string name, string path)
        {
            var list = new List<string>();
            var arr = Array(parent, name, path);
            if (arr == null) return list;
            for (int i = 0; i < arr.Count; i++)
            {
                var s = AsString(arr[i], $"{path}.{name}[{i}]");
                if (s != null) list.Add(s);
            }
            return list;
        }

        public List<int> IntList(JToken parent, string name, string path)
        {
            var list = new List<int>();
            var arr = Array(parent, name, path);
            if (arr == null) return list;
            for (int i = 0; i < arr.Count; i++) list.Add(AsInt(arr[i], $"{path}.{name}[{i}]"));
            return list;
        }

        /// <summary>Exact, case-sensitive enum name (no numeric aliases).</summary>
        public bool TryEnum<T>(string text, string path, out T value) where T : struct, Enum
        {
            value = default;
            if (text != null && System.Array.Exists(Enum.GetNames(typeof(T)), n => n == text))
            {
                value = (T)Enum.Parse(typeof(T), text);
                return true;
            }
            if (text != null) Fail(InvalidStructure, $"{path}: unknown {typeof(T).Name} '{text}'.");
            return false;
        }
    }

    /// <summary>Inclusive integer range [Min, Max].</summary>
    public readonly struct IntRange
    {
        public readonly int Min;
        public readonly int Max;

        public IntRange(int min, int max) { Min = min; Max = max; }

        public bool IsValid => Min <= Max;
        public bool Contains(int v) => v >= Min && v <= Max;
        public override string ToString() => $"[{Min}, {Max}]";
    }
}
