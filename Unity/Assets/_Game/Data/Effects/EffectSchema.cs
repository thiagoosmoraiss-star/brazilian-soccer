using System;
using System.Collections.Generic;

namespace Game.Data.Effects
{
    /// <summary>
    /// The set of effects code expects to exist in data, as keys indexed 0..Count-1.
    /// The game uses <see cref="FromEffectEnum"/>; tests may build their own schema.
    /// </summary>
    public sealed class EffectSchema
    {
        private readonly string[] _keys;

        public EffectSchema(IReadOnlyList<string> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            _keys = new string[keys.Count];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                if (string.IsNullOrEmpty(keys[i])) throw new ArgumentException("Effect key cannot be empty.", nameof(keys));
                if (!seen.Add(keys[i])) throw new ArgumentException("Duplicate effect key: " + keys[i], nameof(keys));
                _keys[i] = keys[i];
            }
        }

        public int Count => _keys.Length;
        public IReadOnlyList<string> Keys => _keys;

        public int IndexOf(string key) => Array.IndexOf(_keys, key);

        /// <summary>Schema of the <see cref="Effect"/> enum. Requires contiguous values 0..N-1.</summary>
        public static EffectSchema FromEffectEnum()
        {
            var values = (Effect[])Enum.GetValues(typeof(Effect));
            var keys = new string[values.Length];
            foreach (var v in values)
            {
                int i = (int)v;
                if (i < 0 || i >= values.Length)
                    throw new InvalidOperationException("Effect enum values must be contiguous from 0: " + v);
                keys[i] = v.ToString();
            }
            return new EffectSchema(keys);
        }
    }
}
