using System;
using System.Collections.Generic;

namespace Game.Data.Effects
{
    public readonly struct AttributeWeight
    {
        public readonly Attr Attribute;
        public readonly float Weight;

        public AttributeWeight(Attr attribute, float weight) { Attribute = attribute; Weight = weight; }
    }

    /// <summary>
    /// Data definition of one effect: 1–3 weighted attributes -> curve -> clamped to [Min, Max] (in <see cref="Unit"/>).
    /// </summary>
    public sealed class EffectDefinition
    {
        public string Key { get; }
        public IReadOnlyList<AttributeWeight> Inputs { get; }
        public PiecewiseLinearCurve Curve { get; }
        public float Min { get; }
        public float Max { get; }
        public string Unit { get; }
        public Monotonicity Monotonicity { get; }

        internal readonly AttributeWeight[] InputArray;
        internal readonly float WeightSum;

        public EffectDefinition(string key, IReadOnlyList<AttributeWeight> inputs, PiecewiseLinearCurve curve,
            float min, float max, string unit, Monotonicity monotonicity)
        {
            Key = key;
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            InputArray = new AttributeWeight[inputs.Count];
            float sum = 0f;
            for (int i = 0; i < inputs.Count; i++) { InputArray[i] = inputs[i]; sum += inputs[i].Weight; }
            WeightSum = sum;
            Inputs = InputArray;
            Curve = curve ?? throw new ArgumentNullException(nameof(curve));
            Min = min;
            Max = max;
            Unit = unit;
            Monotonicity = monotonicity;
        }
    }
}
