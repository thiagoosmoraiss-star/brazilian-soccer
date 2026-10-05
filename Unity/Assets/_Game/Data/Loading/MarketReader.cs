using Game.Core.Results;
using Game.Data.Career;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Career/market.json.</summary>
    public static class MarketReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidMarket = "INVALID_MARKET";

        public static Result<MarketDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.MarketFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "value", "wage", "interest", "seller", "negotiation",
                "contracts", "squad", "budget", "ai", "youthForeignSale");
            if (root == null) return Result<MarketDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidMarket, m); }
            bool Prob(float p) => p >= 0f && p <= 1f;
            int Pts(Effects.PiecewiseLinearCurve c) => c?.Points.Count ?? 0;

            var m = new MarketDefinition();

            var v = j.Object(root, "value", "root");
            if (v != null)
            {
                j.Keys(v, "value", "ovrCurve", "ageMultiplier", "potentialBonusPerPoint", "contractYearsLeftMultiplier",
                    "formMultiplier", "divisionScale");
                m.Value = new ValueParameters
                {
                    OvrCurve = j.Curve(v, "ovrCurve", "value"),
                    AgeMultiplier = j.Curve(v, "ageMultiplier", "value"),
                    PotentialBonusPerPoint = j.Float(v, "potentialBonusPerPoint", "value"),
                    ContractYearsLeftMultiplier = j.Curve(v, "contractYearsLeftMultiplier", "value"),
                    FormMultiplier = j.Curve(v, "formMultiplier", "value"),
                    DivisionScale = j.FloatList(v, "divisionScale", "value"),
                };
                Check(m.Value.PotentialBonusPerPoint >= 0f, "value.potentialBonusPerPoint must be >= 0.");
                Check(m.Value.DivisionScale.Count > 0, "value.divisionScale cannot be empty.");
                Check(Pts(m.Value.OvrCurve) > 0 && Pts(m.Value.AgeMultiplier) > 0 && Pts(m.Value.ContractYearsLeftMultiplier) > 0
                      && Pts(m.Value.FormMultiplier) > 0, "value curves cannot be empty.");
            }

            var w = j.Object(root, "wage", "root");
            if (w != null)
            {
                j.Keys(w, "wage", "ovrCurve", "reputationMultiplier", "divisionScale");
                m.Wage = new WageParameters
                {
                    OvrCurve = j.Curve(w, "ovrCurve", "wage"),
                    ReputationMultiplier = j.Curve(w, "reputationMultiplier", "wage"),
                    DivisionScale = j.FloatList(w, "divisionScale", "wage"),
                };
                Check(m.Wage.DivisionScale.Count > 0, "wage.divisionScale cannot be empty.");
                Check(Pts(m.Wage.OvrCurve) > 0 && Pts(m.Wage.ReputationMultiplier) > 0, "wage curves cannot be empty.");
            }

            var inter = j.Object(root, "interest", "root");
            if (inter != null)
            {
                j.Keys(inter, "interest", "reputationWeight", "divisionWeight", "wageWeight", "minutesWeight", "acceptThreshold");
                m.Interest = new InterestParameters
                {
                    ReputationWeight = j.Float(inter, "reputationWeight", "interest"),
                    DivisionWeight = j.Float(inter, "divisionWeight", "interest"),
                    WageWeight = j.Float(inter, "wageWeight", "interest"),
                    MinutesWeight = j.Float(inter, "minutesWeight", "interest"),
                    AcceptThreshold = j.Float(inter, "acceptThreshold", "interest"),
                };
                Check(Prob(m.Interest.AcceptThreshold), "interest.acceptThreshold must be within 0-1.");
            }

            var se = j.Object(root, "seller", "root");
            if (se != null)
            {
                j.Keys(se, "seller", "importanceWeight", "contractYearsLeftWeight", "financialWeight");
                m.Seller = new SellerParameters
                {
                    ImportanceWeight = j.Float(se, "importanceWeight", "seller"),
                    ContractYearsLeftWeight = j.Float(se, "contractYearsLeftWeight", "seller"),
                    FinancialWeight = j.Float(se, "financialWeight", "seller"),
                };
            }

            var ne = j.Object(root, "negotiation", "root");
            if (ne != null)
            {
                j.Keys(ne, "negotiation", "maxRounds", "clubRaisePerRound", "wageRaisePerRound");
                m.Negotiation = new NegotiationParameters
                {
                    MaxRounds = j.Int(ne, "maxRounds", "negotiation"),
                    ClubRaisePerRound = j.Float(ne, "clubRaisePerRound", "negotiation"),
                    WageRaisePerRound = j.Float(ne, "wageRaisePerRound", "negotiation"),
                };
                Check(m.Negotiation.MaxRounds > 0, "negotiation.maxRounds must be > 0.");
            }

            var co = j.Object(root, "contracts", "root");
            if (co != null)
            {
                j.Keys(co, "contracts", "minYears", "maxYears", "newSigningYears", "renewalYears", "renewalImportanceRank");
                m.Contracts = new ContractParameters
                {
                    MinYears = j.Int(co, "minYears", "contracts"),
                    MaxYears = j.Int(co, "maxYears", "contracts"),
                    NewSigningYears = j.IntRange(co, "newSigningYears", "contracts"),
                    RenewalYears = j.IntRange(co, "renewalYears", "contracts"),
                    RenewalImportanceRank = j.Int(co, "renewalImportanceRank", "contracts"),
                };
                Check(m.Contracts.MinYears >= 1 && m.Contracts.MaxYears >= m.Contracts.MinYears, "contracts.minYears/maxYears invalid.");
                Check(m.Contracts.NewSigningYears.IsValid && m.Contracts.RenewalYears.IsValid, "contracts year ranges invalid.");
                Check(m.Contracts.RenewalImportanceRank > 0, "contracts.renewalImportanceRank must be > 0.");
            }

            var sq = j.Object(root, "squad", "root");
            if (sq != null)
            {
                j.Keys(sq, "squad", "min", "max");
                m.Squad = new SquadSizeParameters { Min = j.Int(sq, "min", "squad"), Max = j.Int(sq, "max", "squad") };
                Check(m.Squad.Min >= 11 && m.Squad.Max >= m.Squad.Min, "squad.min/max invalid.");
            }

            var bu = j.Object(root, "budget", "root");
            if (bu != null)
            {
                j.Keys(bu, "budget", "transferFundShare", "wageShareOfBudget");
                m.Budget = new MarketBudgetParameters
                {
                    TransferFundShare = j.Float(bu, "transferFundShare", "budget"),
                    WageShareOfBudget = j.Float(bu, "wageShareOfBudget", "budget"),
                };
                Check(Prob(m.Budget.TransferFundShare) && Prob(m.Budget.WageShareOfBudget), "budget shares must be within 0-1.");
            }

            var ai = j.Object(root, "ai", "root");
            if (ai != null)
            {
                j.Keys(ai, "ai", "maxSigningsPerClubPerWindow", "maxReleasesPerClubPerWindow", "maxProposalsToManagedClubPerWindow");
                m.Ai = new MarketAiParameters
                {
                    MaxSigningsPerClubPerWindow = j.Int(ai, "maxSigningsPerClubPerWindow", "ai"),
                    MaxReleasesPerClubPerWindow = j.Int(ai, "maxReleasesPerClubPerWindow", "ai"),
                    MaxProposalsToManagedClubPerWindow = j.Int(ai, "maxProposalsToManagedClubPerWindow", "ai"),
                };
                Check(m.Ai.MaxSigningsPerClubPerWindow > 0 && m.Ai.MaxReleasesPerClubPerWindow > 0, "ai signing/release limits must be > 0.");
            }

            var yf = j.Object(root, "youthForeignSale", "root");
            if (yf != null)
            {
                j.Keys(yf, "youthForeignSale", "minPotential", "maxAge", "chancePerWindow", "valueMultiplier");
                m.YouthForeignSale = new YouthForeignSaleParameters
                {
                    MinPotential = j.Int(yf, "minPotential", "youthForeignSale"),
                    MaxAge = j.Int(yf, "maxAge", "youthForeignSale"),
                    ChancePerWindow = j.Float(yf, "chancePerWindow", "youthForeignSale"),
                    ValueMultiplier = j.Float(yf, "valueMultiplier", "youthForeignSale"),
                };
                Check(Prob(m.YouthForeignSale.ChancePerWindow), "youthForeignSale.chancePerWindow must be within 0-1.");
            }

            return j.Ok ? Result<MarketDefinition>.Ok(m) : Result<MarketDefinition>.Fail(j.Errors);
        }
    }
}
