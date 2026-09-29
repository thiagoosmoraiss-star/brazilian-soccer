using System;
using System.Collections.Generic;
using Game.Core.Results;

namespace Game.Data.Effects
{
    /// <summary>
    /// Validates effect definitions against the schema: every expected effect defined, every attribute
    /// feeding at least one effect, 1–3 inputs with positive weights, valid curves, ranges and declared monotonicity.
    /// </summary>
    public static class BalanceValidator
    {
        public const string EffectWithoutDefinition = "EFFECT_WITHOUT_DEFINITION";
        public const string UnknownEffect = "UNKNOWN_EFFECT";
        public const string DuplicateEffect = "DUPLICATE_EFFECT";
        public const string AttributeWithoutEffect = "ATTRIBUTE_WITHOUT_EFFECT";
        public const string InvalidInputCount = "INVALID_INPUT_COUNT";
        public const string InvalidAttribute = "INVALID_ATTRIBUTE";
        public const string DuplicateInput = "DUPLICATE_INPUT";
        public const string InvalidWeight = "INVALID_WEIGHT";
        public const string CurveTooFewPoints = "CURVE_TOO_FEW_POINTS";
        public const string CurveNotFinite = "CURVE_NOT_FINITE";
        public const string CurveXNotIncreasing = "CURVE_X_NOT_INCREASING";
        public const string InvalidRange = "INVALID_RANGE";
        public const string CurveOutsideRange = "CURVE_OUTSIDE_RANGE";
        public const string MonotonicityViolated = "MONOTONICITY_VIOLATED";
        public const string MissingUnit = "MISSING_UNIT";
        public const string MissingKey = "MISSING_KEY";

        public const int MinInputs = 1;
        public const int MaxInputs = 3;

        public static IReadOnlyList<Error> Validate(EffectSchema schema, IReadOnlyList<EffectDefinition> definitions)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var errors = new List<Error>();
            var defined = new HashSet<string>(StringComparer.Ordinal);
            var attributeUsed = new bool[AttrInfo.Count];

            foreach (var def in definitions)
            {
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.Key))
                {
                    errors.Add(new Error(MissingKey, "Effect definition without key."));
                    continue;
                }
                string k = def.Key;
                if (!defined.Add(k)) errors.Add(new Error(DuplicateEffect, $"Effect '{k}' defined more than once."));
                if (schema.IndexOf(k) < 0) errors.Add(new Error(UnknownEffect, $"Effect '{k}' is not part of the effect catalog."));
                if (string.IsNullOrWhiteSpace(def.Unit)) errors.Add(new Error(MissingUnit, $"Effect '{k}' has no unit."));

                ValidateInputs(k, def.Inputs, attributeUsed, errors);
                ValidateCurve(k, def, errors);
            }

            foreach (var key in schema.Keys)
                if (!defined.Contains(key))
                    errors.Add(new Error(EffectWithoutDefinition, $"Effect '{key}' has no definition."));

            for (int i = 0; i < AttrInfo.Count; i++)
                if (!attributeUsed[i])
                    errors.Add(new Error(AttributeWithoutEffect, $"Attribute '{(Attr)i}' does not feed any effect."));

            return errors;
        }

        private static void ValidateInputs(string key, IReadOnlyList<AttributeWeight> inputs, bool[] attributeUsed, List<Error> errors)
        {
            if (inputs.Count < MinInputs || inputs.Count > MaxInputs)
                errors.Add(new Error(InvalidInputCount, $"Effect '{key}' has {inputs.Count} inputs; expected {MinInputs}-{MaxInputs}."));

            var seen = new HashSet<Attr>();
            foreach (var input in inputs)
            {
                if (!AttrInfo.IsValid(input.Attribute))
                {
                    errors.Add(new Error(InvalidAttribute, $"Effect '{key}' references unknown attribute {(int)input.Attribute}."));
                    continue;
                }
                if (!seen.Add(input.Attribute))
                    errors.Add(new Error(DuplicateInput, $"Effect '{key}' uses attribute '{input.Attribute}' twice."));
                if (!(input.Weight > 0f) || float.IsInfinity(input.Weight))
                    errors.Add(new Error(InvalidWeight, $"Effect '{key}' has invalid weight {input.Weight} for '{input.Attribute}'."));
                else
                    attributeUsed[(int)input.Attribute] = true;
            }
        }

        private static void ValidateCurve(string key, EffectDefinition def, List<Error> errors)
        {
            if (!IsFinite(def.Min) || !IsFinite(def.Max) || def.Min > def.Max)
                errors.Add(new Error(InvalidRange, $"Effect '{key}' has invalid range [{def.Min}, {def.Max}]."));

            var pts = def.Curve.Points;
            if (pts.Count < 2)
            {
                errors.Add(new Error(CurveTooFewPoints, $"Effect '{key}' curve needs at least 2 points."));
                return;
            }

            for (int i = 0; i < pts.Count; i++)
            {
                if (!IsFinite(pts[i].X) || !IsFinite(pts[i].Y))
                {
                    errors.Add(new Error(CurveNotFinite, $"Effect '{key}' curve point {i} is not finite."));
                    return;
                }
            }

            for (int i = 1; i < pts.Count; i++)
                if (!(pts[i].X > pts[i - 1].X))
                    errors.Add(new Error(CurveXNotIncreasing, $"Effect '{key}' curve X must be strictly increasing (point {i})."));

            for (int i = 0; i < pts.Count; i++)
                if (pts[i].Y < def.Min || pts[i].Y > def.Max)
                    errors.Add(new Error(CurveOutsideRange, $"Effect '{key}' curve point {i} (Y={pts[i].Y}) is outside [{def.Min}, {def.Max}]."));

            if (def.Monotonicity != Monotonicity.None)
            {
                for (int i = 1; i < pts.Count; i++)
                {
                    float dy = pts[i].Y - pts[i - 1].Y;
                    bool violates = def.Monotonicity == Monotonicity.NonDecreasing ? dy < 0f : dy > 0f;
                    if (violates)
                    {
                        errors.Add(new Error(MonotonicityViolated, $"Effect '{key}' curve is declared {def.Monotonicity} but changes direction at point {i}."));
                        break;
                    }
                }
            }
        }

        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
