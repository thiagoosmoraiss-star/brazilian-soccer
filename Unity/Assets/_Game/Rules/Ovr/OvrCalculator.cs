using System;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Ovr;

namespace Game.Rules.Ovr
{
    /// <summary>
    /// OVR by position: weighted average of the 18 attributes with weights from Data/Balance/ovr.json
    /// (GAME_DESIGN §9). Secondary positions and out-of-position play apply the factors from the same file.
    /// </summary>
    public static class OvrCalculator
    {
        /// <summary>Exact (unrounded) OVR of <paramref name="attributes"/> at <paramref name="position"/>.</summary>
        public static float Exact(OvrDefinition def, ReadOnlySpan<int> attributes, Position position)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (attributes.Length != AttrInfo.Count)
                throw new ArgumentException($"Expected {AttrInfo.Count} attribute values, got {attributes.Length}.", nameof(attributes));
            float sum = 0f;
            for (int a = 0; a < AttrInfo.Count; a++) sum += def.Weight(position, (Attr)a) * attributes[a];
            return sum;
        }

        /// <summary>OVR rounded to the 1-99 integer scale.</summary>
        public static int Rating(OvrDefinition def, ReadOnlySpan<int> attributes, Position position) =>
            Round(Exact(def, attributes, position));

        /// <summary>
        /// Effective OVR of a player at <paramref name="position"/>: main position = full rating,
        /// secondary = rating x secondary factor, otherwise = rating x out-of-position factor.
        /// </summary>
        public static int Effective(OvrDefinition def, ReadOnlySpan<int> attributes, Position main,
            ReadOnlySpan<Position> secondary, Position position)
        {
            float exact = Exact(def, attributes, position);
            if (position == main) return Round(exact);
            for (int i = 0; i < secondary.Length; i++)
                if (secondary[i] == position) return Round(exact * def.SecondaryPositionFactor);
            return Round(exact * def.OutOfPositionFactor);
        }

        private static int Round(float value)
        {
            int r = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            return r < AttrInfo.MinValue ? AttrInfo.MinValue : (r > AttrInfo.MaxValue ? AttrInfo.MaxValue : r);
        }
    }
}
