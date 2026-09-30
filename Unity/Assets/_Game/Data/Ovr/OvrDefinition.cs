using System;
using Game.Data.Definitions;
using Game.Data.Effects;

namespace Game.Data.Ovr
{
    /// <summary>
    /// OVR weights per position (GAME_DESIGN §9: "OVR por posição: média ponderada (pesos em dados)"),
    /// loaded from Data/Balance/ovr.json. Weights are normalized to sum 1 per position.
    /// </summary>
    public sealed class OvrDefinition
    {
        private readonly float[][] _weights; // [position][attribute], normalized

        public float SecondaryPositionFactor { get; }
        public float OutOfPositionFactor { get; }

        public OvrDefinition(float[][] rawWeights, float secondaryPositionFactor, float outOfPositionFactor)
        {
            if (rawWeights == null || rawWeights.Length != PositionInfo.Count)
                throw new ArgumentException("Weights are required for every position.", nameof(rawWeights));
            _weights = new float[PositionInfo.Count][];
            for (int p = 0; p < PositionInfo.Count; p++)
            {
                var w = rawWeights[p];
                if (w == null || w.Length != AttrInfo.Count) throw new ArgumentException("Invalid weights for " + (Position)p);
                float sum = 0f;
                foreach (var x in w) sum += x;
                if (!(sum > 0f)) throw new ArgumentException("Weights must sum to > 0 for " + (Position)p);
                _weights[p] = new float[AttrInfo.Count];
                for (int a = 0; a < AttrInfo.Count; a++) _weights[p][a] = w[a] / sum;
            }
            SecondaryPositionFactor = secondaryPositionFactor;
            OutOfPositionFactor = outOfPositionFactor;
        }

        /// <summary>Normalized weight of <paramref name="attr"/> for <paramref name="position"/>.</summary>
        public float Weight(Position position, Attr attr) => _weights[(int)position][(int)attr];
    }
}
