using System.Collections.Generic;

namespace Game.Data.Strings
{
    /// <summary>
    /// The UI texts of one language (C3, TECHNICAL_SPEC §15 "textos em tabelas de strings desde o início"; X-92: PT-BR
    /// only at launch). Keys are dotted lower-case ids ("hub.advance"); values may hold <c>{0}</c>-style placeholders.
    /// A missing key shows as <c>#key</c> instead of throwing; the UI tests fail on any key the code uses and the table
    /// lacks.
    /// </summary>
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> _values;

        public string Locale { get; }

        internal StringTable(string locale, Dictionary<string, string> values)
        {
            Locale = locale;
            _values = values;
        }

        public IEnumerable<string> Keys => _values.Keys;

        public bool Has(string key) => _values.ContainsKey(key);

        public string Get(string key) => _values.TryGetValue(key, out var v) ? v : "#" + key;

        public string Format(string key, params object[] args) =>
            _values.TryGetValue(key, out var v) ? string.Format(System.Globalization.CultureInfo.InvariantCulture, v, args) : "#" + key;
    }
}
