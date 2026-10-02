using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Data.Match;
using Game.Rules.Ovr;

namespace Game.Rules.Match
{
    /// <summary>A player currently on the pitch: setup, the formation slot he occupies and his live energy.</summary>
    public sealed class FieldPlayer
    {
        public MatchPlayerSetup Setup { get; }
        public FormationSlot Slot { get; internal set; }
        public float Energy { get; set; }

        public FieldPlayer(MatchPlayerSetup setup, FormationSlot slot, float energy)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            Energy = energy;
        }
    }

    /// <summary>
    /// Rules shared by MatchEngine and QuickSim (TECHNICAL_SPEC §10): sector strength, condition, fatigue,
    /// discipline, injuries. Attribute effects go through <see cref="Balance"/> only; coefficients come from data.
    /// </summary>
    public sealed class MatchRules
    {
        private readonly GameDatabase _db;
        private readonly float[] _effectReference; // Balance value of each effect for an all-50 player
        private static readonly int[] Average = Fill(50);

        public MatchRulesDefinition Definition => _db.MatchRules;
        public Balance Balance => _db.Balance;

        public MatchRules(GameDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            var effects = (Effect[])Enum.GetValues(typeof(Effect));
            _effectReference = new float[effects.Length];
            foreach (var e in effects) _effectReference[(int)e] = db.Balance.Eval(e, Average);
        }

        /// <summary>Effect value relative to an average (all-50) player: &gt; 1 means a larger value than average.</summary>
        public float EffectRatio(Effect effect, ReadOnlySpan<int> attributes)
        {
            float reference = _effectReference[(int)effect];
            return reference == 0f ? 1f : _db.Balance.Eval(effect, attributes) / reference;
        }

        public float Eval(Effect effect, ReadOnlySpan<int> attributes) => _db.Balance.Eval(effect, attributes);

        // ---------------- Condition ----------------

        /// <summary>Morale (±5%) x form factor (data).</summary>
        public float ConditionFactor(MatchPlayerSetup p)
        {
            var c = Definition.Condition;
            float morale = c.MoraleFactors[p.Morale - 1];
            float form = 1f;
            if (p.Form.HasValue)
            {
                form = 1f + (p.Form.Value - c.FormNeutral) * c.FormFactorPerPoint;
                form = form < c.FormFactorMin ? c.FormFactorMin : (form > c.FormFactorMax ? c.FormFactorMax : form);
            }
            return morale * form;
        }

        /// <summary>Low-energy penalty: below the threshold, performance drops up to the LateMatchPenalty effect.</summary>
        public float EnergyFactor(ReadOnlySpan<int> attributes, float energy)
        {
            float threshold = Definition.Condition.LowEnergyThreshold;
            if (energy >= threshold) return 1f;
            float lowness = (threshold - energy) / threshold;
            return 1f - Eval(Effect.LateMatchPenalty, attributes) * lowness;
        }

        /// <summary>Position fit: main 1.0, secondary / out of position factors from ovr.json.</summary>
        public float FitFactor(MatchPlayerSetup p, Position slotPosition)
        {
            if (p.MainPosition == (int)slotPosition) return 1f;
            var secondary = p.SecondaryPositions;
            for (int i = 0; i < secondary.Length; i++)
                if (secondary[i] == (int)slotPosition) return _db.Ovr.SecondaryPositionFactor;
            return _db.Ovr.OutOfPositionFactor;
        }

        /// <summary>Effective OVR of a player in a slot (used by lineup and substitution AI).</summary>
        public float EffectiveOvr(MatchPlayerSetup p, Position slotPosition) =>
            OvrCalculator.Exact(_db.Ovr, p.Attributes, slotPosition) * FitFactor(p, slotPosition);

        // ---------------- Sector strength ----------------

        /// <summary>Rating of one player in one sector (weighted attributes, 1-99 scale).</summary>
        public float SectorRating(ReadOnlySpan<int> attributes, Sector sector)
        {
            var w = Definition.SectorAttributeWeights[(int)sector];
            float sum = 0f;
            for (int a = 0; a < w.Length; a++) sum += w[a] * attributes[a];
            return sum;
        }

        /// <summary>
        /// Team strength per sector (TECHNICAL_SPEC §10: single function shared by both engines). A 4-4-2 of players
        /// rated R in every sector, fully fit, scores R; formations shift strength between sectors.
        /// </summary>
        public void SectorStrengths(IReadOnlyList<FieldPlayer> onField, float[] result)
        {
            Array.Clear(result, 0, result.Length);
            var d = Definition;
            for (int i = 0; i < onField.Count; i++)
            {
                var fp = onField[i];
                var attrs = fp.Setup.Attributes;
                float factor = FitFactor(fp.Setup, fp.Slot.Position) * ConditionFactor(fp.Setup) * EnergyFactor(attrs, fp.Energy);
                var roleWeights = d.RoleSectorWeights[(int)fp.Slot.Role];
                for (int s = 0; s < SectorInfo.Count; s++)
                    if (roleWeights[s] > 0f) result[s] += roleWeights[s] * SectorRating(attrs, (Sector)s) * factor;
            }
            for (int s = 0; s < SectorInfo.Count; s++) result[s] /= d.SectorReferenceWeights[s];
        }

        // ---------------- Fatigue ----------------

        /// <summary>Energy lost per match minute (0-100 scale), scaled by Stamina (EnergyDrainMult) and tactical intensity.</summary>
        public float EnergyDrainPerMinute(ReadOnlySpan<int> attributes, TacticSetup tactic)
        {
            var f = Definition.Fatigue;
            return f.BaseDrainPerMinute * Eval(Effect.EnergyDrainMult, attributes)
                   * f.MentalityIntensity[tactic.Mentality - 1] * f.PressureIntensity[tactic.Pressure - 1];
        }

        // ---------------- Discipline ----------------

        /// <summary>Relative chance that this player commits a foul when defending (FoulChance effect, inverse of Tackling).</summary>
        public float FoulPropensity(ReadOnlySpan<int> attributes) => EffectRatio(Effect.FoulChance, attributes);

        public enum Card { None, Yellow, StraightRed }

        public Card CardForFoul(Rng rng, bool alreadyBooked)
        {
            var d = Definition.Discipline;
            float factor = alreadyBooked ? d.BookedCardFactor : 1f;
            double roll = rng.NextDouble();
            if (roll < d.StraightRedPerFoul * factor) return Card.StraightRed;
            if (roll < (d.StraightRedPerFoul + d.YellowPerFoul) * factor) return Card.Yellow;
            return Card.None;
        }

        // ---------------- Injuries ----------------

        public float InjuryHazardPerMinute(ReadOnlySpan<int> attributes, float energy)
        {
            var d = Definition.Injuries;
            float hazard = d.HazardPerPlayerMinute * Eval(Effect.InjuryChance, attributes);
            return energy < d.LowEnergyThreshold ? hazard * d.LowEnergyMultiplier : hazard;
        }

        public InjurySeverity InjurySeverityRoll(Rng rng)
        {
            var d = Definition.Injuries;
            int roll = rng.NextInt(0, d.LightWeight + d.MediumWeight + d.SevereWeight);
            if (roll < d.LightWeight) return InjurySeverity.Light;
            return roll < d.LightWeight + d.MediumWeight ? InjurySeverity.Medium : InjurySeverity.Severe;
        }

        private static int[] Fill(int v)
        {
            var a = new int[AttrInfo.Count];
            for (int i = 0; i < a.Length; i++) a[i] = v;
            return a;
        }
    }
}
