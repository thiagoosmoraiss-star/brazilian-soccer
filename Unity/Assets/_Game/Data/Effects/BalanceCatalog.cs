using System;
using System.Collections.Generic;
using Game.Core.Results;

namespace Game.Data.Effects
{
    /// <summary>
    /// Validated, immutable set of effect definitions indexed by schema position.
    /// Evaluation: weighted attribute input -> pre-curve modifiers -> curve -> post-curve modifiers -> clamp.
    /// </summary>
    public sealed class BalanceCatalog
    {
        private static readonly ModifierSource[] ApplicationOrder =
            (ModifierSource[])Enum.GetValues(typeof(ModifierSource));

        private readonly EffectDefinition[] _byIndex;

        public EffectSchema Schema { get; }

        private BalanceCatalog(EffectSchema schema, EffectDefinition[] byIndex)
        {
            Schema = schema;
            _byIndex = byIndex;
        }

        public static Result<BalanceCatalog> Create(EffectSchema schema, IReadOnlyList<EffectDefinition> definitions)
        {
            var errors = BalanceValidator.Validate(schema, definitions);
            if (errors.Count > 0) return Result<BalanceCatalog>.Fail(errors);

            var byIndex = new EffectDefinition[schema.Count];
            foreach (var def in definitions) byIndex[schema.IndexOf(def.Key)] = def;
            return Result<BalanceCatalog>.Ok(new BalanceCatalog(schema, byIndex));
        }

        public EffectDefinition Get(int effectIndex) => _byIndex[effectIndex];

        /// <param name="effectIndex">Index of the effect in <see cref="Schema"/>.</param>
        /// <param name="attributes">Attribute values indexed by <see cref="Attr"/> (length <see cref="AttrInfo.Count"/>).</param>
        /// <param name="modifiers">Modifiers in any order; applied in the fixed <see cref="ModifierSource"/> order.</param>
        public float Eval(int effectIndex, ReadOnlySpan<int> attributes, ReadOnlySpan<Modifier> modifiers)
        {
            if (attributes.Length != AttrInfo.Count)
                throw new ArgumentException($"Expected {AttrInfo.Count} attribute values, got {attributes.Length}.", nameof(attributes));

            var def = _byIndex[effectIndex];
            var inputs = def.InputArray;

            float weighted = 0f;
            for (int i = 0; i < inputs.Length; i++)
                weighted += attributes[(int)inputs[i].Attribute] * inputs[i].Weight;
            float x = weighted / def.WeightSum;

            x = Apply(x, modifiers, ModifierStage.PreCurve);
            float y = def.Curve.Evaluate(x);
            y = Apply(y, modifiers, ModifierStage.PostCurve);

            return y < def.Min ? def.Min : (y > def.Max ? def.Max : y);
        }

        public float Eval(int effectIndex, ReadOnlySpan<int> attributes) =>
            Eval(effectIndex, attributes, ReadOnlySpan<Modifier>.Empty);

        private static float Apply(float value, ReadOnlySpan<Modifier> modifiers, ModifierStage stage)
        {
            if (modifiers.Length == 0) return value;
            for (int s = 0; s < ApplicationOrder.Length; s++)
            {
                var source = ApplicationOrder[s];
                for (int i = 0; i < modifiers.Length; i++)
                {
                    var m = modifiers[i];
                    if (m.Stage != stage || m.Source != source) continue;
                    value = m.Op == ModifierOp.Multiply ? value * m.Value : value + m.Value;
                }
            }
            return value;
        }
    }
}
