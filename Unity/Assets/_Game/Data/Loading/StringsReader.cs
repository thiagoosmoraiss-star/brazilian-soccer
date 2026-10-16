using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core.Results;
using Game.Data.Strings;
using Newtonsoft.Json.Linq;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + checks for Data/Strings/&lt;locale&gt;.json (C3, X-92, X-95).</summary>
    public static class StringsReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidStrings = "INVALID_STRINGS";

        private static readonly Regex KeyPattern = new Regex(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$");
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}");

        public static Result<StringTable> Read(string json, string file)
        {
            var j = new StrictJson(file);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "locale", "strings");
            if (root == null) return Result<StringTable>.Fail(j.Errors);
            string locale = j.String(root, "locale", "root");
            var strings = j.Object(root, "strings", "root");
            var values = new Dictionary<string, string>();
            if (strings != null)
                foreach (var prop in strings.Properties())
                {
                    string path = "strings." + prop.Name;
                    if (!KeyPattern.IsMatch(prop.Name)) j.Fail(InvalidStrings, $"{path}: key must be dotted lower-case ids (a.b_c).");
                    if (prop.Value.Type != JTokenType.String) { j.Fail(InvalidStrings, $"{path}: must be a string."); continue; }
                    string value = prop.Value.Value<string>();
                    if (string.IsNullOrWhiteSpace(value)) j.Fail(InvalidStrings, $"{path}: empty text.");
                    else if (!PlaceholdersValid(value)) j.Fail(InvalidStrings, $"{path}: braces must be {{0}}-style placeholders.");
                    values[prop.Name] = value;
                }
            if (values.Count == 0) j.Fail(InvalidStrings, "strings: the table is empty.");
            return j.Ok ? Result<StringTable>.Ok(new StringTable(locale, values)) : Result<StringTable>.Fail(j.Errors);
        }

        /// <summary>Every brace belongs to a {n} placeholder (string.Format would throw at runtime otherwise).</summary>
        private static bool PlaceholdersValid(string value)
        {
            string stripped = Placeholder.Replace(value, "");
            return stripped.IndexOf('{') < 0 && stripped.IndexOf('}') < 0;
        }
    }
}
