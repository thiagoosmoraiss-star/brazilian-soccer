using Game.Core.Results;
using Game.Data.Career;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Career/facilities.json.</summary>
    public static class FacilitiesReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidFacilities = "INVALID_FACILITIES";

        public static Result<FacilitiesDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.FacilitiesFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "stadium", "trainingCenter");
            if (root == null) return Result<FacilitiesDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidFacilities, m); }

            var f = new FacilitiesDefinition();

            var st = j.Object(root, "stadium", "root");
            if (st != null)
            {
                j.Keys(st, "stadium", "upgradeCostPerLevel", "upgradeDurationMonths", "minimumLevelByDivision");
                f.Stadium = new StadiumFacilityParameters
                {
                    UpgradeCostPerLevel = j.LongList(st, "upgradeCostPerLevel", "stadium"),
                    UpgradeDurationMonths = j.Int(st, "upgradeDurationMonths", "stadium"),
                    MinimumLevelByDivision = j.IntList(st, "minimumLevelByDivision", "stadium"),
                };
                Check(f.Stadium.UpgradeCostPerLevel.Count > 0 && f.Stadium.UpgradeDurationMonths > 0 && f.Stadium.MinimumLevelByDivision.Count > 0,
                    "stadium values invalid.");
            }

            var tc = j.Object(root, "trainingCenter", "root");
            if (tc != null)
            {
                j.Keys(tc, "trainingCenter", "initialLevelByDivision", "upgradeCostPerLevel", "upgradeDurationMonths", "developmentFactorByLevel");
                f.TrainingCenter = new TrainingCenterFacilityParameters
                {
                    InitialLevelByDivision = j.IntList(tc, "initialLevelByDivision", "trainingCenter"),
                    UpgradeCostPerLevel = j.LongList(tc, "upgradeCostPerLevel", "trainingCenter"),
                    UpgradeDurationMonths = j.Int(tc, "upgradeDurationMonths", "trainingCenter"),
                    DevelopmentFactorByLevel = j.FloatList(tc, "developmentFactorByLevel", "trainingCenter"),
                };
                Check(f.TrainingCenter.InitialLevelByDivision.Count > 0 && f.TrainingCenter.UpgradeCostPerLevel.Count > 0
                      && f.TrainingCenter.UpgradeDurationMonths > 0 && f.TrainingCenter.DevelopmentFactorByLevel.Count == 5,
                    "trainingCenter values invalid (developmentFactorByLevel needs exactly 5 entries, levels 1-5).");
            }

            return j.Ok ? Result<FacilitiesDefinition>.Ok(f) : Result<FacilitiesDefinition>.Fail(j.Errors);
        }
    }
}
