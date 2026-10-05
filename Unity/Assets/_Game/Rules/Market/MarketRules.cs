using System;
using Game.Data.Career;

namespace Game.Rules.Market
{
    /// <summary>
    /// Pure transfer-market math (TECHNICAL_SPEC §12, GAME_DESIGN §10, B5, X-43): market value, reference wage,
    /// player interest and the seller's minimum acceptable offer. Coefficients live in Data/Career/market.json.
    /// </summary>
    public static class MarketRules
    {
        /// <summary>Market value in fictional R$ (GAME_DESIGN §10: OVR, age, potential, contract, form, division).</summary>
        public static long Value(MarketDefinition m, int divisionIndex, int ovr, int age, int potential,
            int contractYearsLeft, float form)
        {
            var v = m.Value;
            float baseValue = v.OvrCurve.Evaluate(ovr);
            float ageMul = v.AgeMultiplier.Evaluate(age);
            float potentialMul = 1f + v.PotentialBonusPerPoint * Math.Max(0, potential - ovr);
            float yearsMul = v.ContractYearsLeftMultiplier.Evaluate(contractYearsLeft);
            float formMul = v.FormMultiplier.Evaluate(form);
            float divisionScale = ScaleFor(v.DivisionScale, divisionIndex);
            return Math.Max(0L, (long)Math.Round(baseValue * ageMul * potentialMul * yearsMul * formMul * divisionScale));
        }

        /// <summary>Reference season wage in fictional R$ (OVR and club reputation, by division).</summary>
        public static long WageReference(MarketDefinition m, int divisionIndex, int clubReputation, int ovr)
        {
            var w = m.Wage;
            float baseWage = w.OvrCurve.Evaluate(ovr);
            float reputationMul = w.ReputationMultiplier.Evaluate(clubReputation);
            float divisionScale = ScaleFor(w.DivisionScale, divisionIndex);
            return Math.Max(0L, (long)Math.Round(baseWage * reputationMul * divisionScale));
        }

        /// <summary>
        /// Player interest in the move (0-1, GAME_DESIGN §10: relative reputation, division, salary, minutes).
        /// Below <see cref="InterestParameters.AcceptThreshold"/> the player refuses.
        /// </summary>
        public static float Interest(MarketDefinition m, int buyerReputation, int sellerReputation, int buyerDivisionIndex,
            int sellerDivisionIndex, long offeredWage, long referenceWage, float promisedMinutesShare)
        {
            var i = m.Interest;
            float reputationScore = Clamp01(0.5f + 0.5f * (float)Math.Tanh((buyerReputation - sellerReputation) / 300.0));
            float divisionScore = buyerDivisionIndex <= sellerDivisionIndex
                ? 1f : Clamp01(1f - 0.25f * (buyerDivisionIndex - sellerDivisionIndex));
            float wageScore = referenceWage <= 0 ? 1f : Clamp01(offeredWage / (float)referenceWage);
            float minutesScore = Clamp01(promisedMinutesShare);
            float weightSum = Math.Max(0.0001f, i.ReputationWeight + i.DivisionWeight + i.WageWeight + i.MinutesWeight);
            float score = i.ReputationWeight * reputationScore + i.DivisionWeight * divisionScore
                        + i.WageWeight * wageScore + i.MinutesWeight * minutesScore;
            return Clamp01(score / weightSum);
        }

        /// <summary>
        /// Factor applied to <see cref="Value"/> for the seller's minimum acceptable offer (GAME_DESIGN §10:
        /// importance, remaining contract, finances). <paramref name="financialHealth01"/> is neutral (0.5) until
        /// the economy (B6) supplies a real reading.
        /// </summary>
        public static float SellerAcceptFactor(MarketDefinition m, float importance01, int contractYearsLeft, float financialHealth01)
        {
            var s = m.Seller;
            float factor = 1f + s.ImportanceWeight * Clamp01(importance01)
                              + s.ContractYearsLeftWeight * Clamp01(contractYearsLeft / 5f)
                              + s.FinancialWeight * (0.5f - Clamp01(financialHealth01)) * 2f;
            return Math.Max(0.5f, factor);
        }

        /// <summary>
        /// Scouted potential range (GAME_DESIGN §10: "faixas de atributo que estreitam"), a design estimate: the
        /// band narrows as the observing club's reputation grows, always containing the true potential.
        /// </summary>
        public static (int Min, int Max) ScoutPotentialRange(int observerReputation, int truePotential)
        {
            int half = Math.Max(2, 12 - observerReputation / 100);
            return (Math.Max(40, truePotential - half), Math.Min(99, truePotential + half));
        }

        private static float ScaleFor(System.Collections.Generic.IReadOnlyList<float> scale, int divisionIndex)
        {
            if (scale == null || scale.Count == 0) return 1f;
            int i = Math.Max(0, Math.Min(divisionIndex, scale.Count - 1));
            return scale[i];
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
