using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Data.Loading;
using NUnit.Framework;
using static Game.Tests.UI.UiTestSupport;

namespace Game.Tests.UI
{
    /// <summary>C3a: the PT-BR string table (X-92) and the keys the UI code uses.</summary>
    public class StringsTests
    {
        private static readonly Regex KeyLiteral = new Regex("\"([a-z][a-z0-9_]*\\.[a-z0-9_.]*)\"");

        /// <summary>Key-shaped string literals of the UI sources (exact keys, or prefixes ending with a dot).</summary>
        private static HashSet<string> UsedKeys()
        {
            string game = Path.Combine(Path.GetDirectoryName(DataRoot()), "Unity", "Assets", "_Game");
            var used = new HashSet<string>();
            foreach (var dir in new[] { "UI", "UI.Presenters" })
            {
                string path = Path.Combine(game, dir);
                if (!Directory.Exists(path)) continue;
                foreach (var file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                    foreach (System.Text.RegularExpressions.Match m in KeyLiteral.Matches(File.ReadAllText(file)))
                        if (!m.Groups[1].Value.EndsWith(".json")) used.Add(m.Groups[1].Value);
            }
            Assert.Greater(used.Count, 50, "the scan found the UI sources");
            return used;
        }

        private static bool IsPrefix(string key) => key.EndsWith(".") || key.EndsWith("_");

        [Test]
        public void EveryKeyTheUiUses_IsInTheTable_AndEveryTableKeyIsUsed()
        {
            var table = Strings();
            var used = UsedKeys();
            // Literals ending with '.' or '_' are prefixes of keys built in code ("morale." + level).
            var prefixes = used.Where(IsPrefix).ToList();
            var missing = used.Where(k => !IsPrefix(k) && !table.Has(k)).OrderBy(k => k).ToList();
            missing.AddRange(prefixes.Where(p => !table.Keys.Any(k => k.StartsWith(p))));
            CollectionAssert.IsEmpty(missing, "keys used by the UI but missing in Data/Strings/pt-BR.json");
            var unused = table.Keys.Where(k => !used.Contains(k) && !prefixes.Any(p => k.StartsWith(p))).OrderBy(k => k).ToList();
            CollectionAssert.IsEmpty(unused, "keys in Data/Strings/pt-BR.json no screen uses");
        }

        [Test]
        public void TheTableIsPortugueseOnly_AndFormatsItsPlaceholders()
        {
            var table = Strings();
            Assert.AreEqual("pt-BR", table.Locale);
            Assert.AreEqual("Divisão D", table.Format("division.name", "D"));
            Assert.AreEqual("#nope.missing", table.Get("nope.missing"), "a missing key shows instead of throwing");
        }

        [TestCase("{\"schemaVersion\":1,\"locale\":\"pt-BR\",\"strings\":{\"Bad.Key\":\"x\"}}")]
        [TestCase("{\"schemaVersion\":1,\"locale\":\"pt-BR\",\"strings\":{\"a.b\":\"\"}}")]
        [TestCase("{\"schemaVersion\":1,\"locale\":\"pt-BR\",\"strings\":{\"a.b\":\"{x}\"}}")]
        [TestCase("{\"schemaVersion\":1,\"locale\":\"pt-BR\",\"strings\":{\"a.b\":\"aberta {0\"}}")]
        [TestCase("{\"schemaVersion\":1,\"locale\":\"pt-BR\",\"strings\":{\"a.b\":3}}")]
        [TestCase("{\"schemaVersion\":1,\"locale\":\"pt-BR\",\"strings\":{}}")]
        [TestCase("{\"schemaVersion\":2,\"locale\":\"pt-BR\",\"strings\":{\"a.b\":\"x\"}}")]
        [TestCase("{\"schemaVersion\":1,\"locale\":\"pt-BR\",\"strings\":{\"a.b\":\"x\"},\"extra\":1}")]
        public void TheReader_RejectsBrokenTables(string json)
        {
            Assert.IsFalse(StringsReader.Read(json, "Strings/test.json").IsSuccess);
        }
    }
}
