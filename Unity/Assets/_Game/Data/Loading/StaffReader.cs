using Game.Core.Results;
using Game.Data.Career;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Career/staff.json.</summary>
    public static class StaffReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidStaff = "INVALID_STAFF";

        public static Result<StaffDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.StaffFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "initialLevelByDivision", "wagePerLevel", "upgradeCostPerLevel",
                "physio", "assistant", "scout");
            if (root == null) return Result<StaffDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidStaff, m); }

            var s = new StaffDefinition
            {
                InitialLevelByDivision = j.IntList(root, "initialLevelByDivision", "root"),
                WagePerLevel = j.LongList(root, "wagePerLevel", "root"),
                UpgradeCostPerLevel = j.LongList(root, "upgradeCostPerLevel", "root"),
            };
            Check(s.InitialLevelByDivision.Count > 0, "initialLevelByDivision cannot be empty.");
            Check(s.WagePerLevel.Count == 5, "wagePerLevel needs exactly 5 entries, levels 1-5.");
            Check(s.UpgradeCostPerLevel.Count == 4, "upgradeCostPerLevel needs exactly 4 entries, levels 1-5.");

            var ph = j.Object(root, "physio", "root");
            if (ph != null)
            {
                j.Keys(ph, "physio", "energyRecoveryMultiplierByLevel");
                s.Physio = new PhysioParameters { EnergyRecoveryMultiplierByLevel = j.FloatList(ph, "energyRecoveryMultiplierByLevel", "physio") };
                Check(s.Physio.EnergyRecoveryMultiplierByLevel.Count == 5, "physio.energyRecoveryMultiplierByLevel needs exactly 5 entries.");
            }

            var asst = j.Object(root, "assistant", "root");
            if (asst != null)
            {
                j.Keys(asst, "assistant", "developmentFactorByLevel");
                s.Assistant = new AssistantParameters { DevelopmentFactorByLevel = j.FloatList(asst, "developmentFactorByLevel", "assistant") };
                Check(s.Assistant.DevelopmentFactorByLevel.Count == 5, "assistant.developmentFactorByLevel needs exactly 5 entries.");
            }

            var sc = j.Object(root, "scout", "root");
            if (sc != null)
            {
                j.Keys(sc, "scout", "potentialRangeNarrowingByLevel");
                s.Scout = new ScoutParameters { PotentialRangeNarrowingByLevel = j.FloatList(sc, "potentialRangeNarrowingByLevel", "scout") };
                Check(s.Scout.PotentialRangeNarrowingByLevel.Count == 5, "scout.potentialRangeNarrowingByLevel needs exactly 5 entries.");
            }

            return j.Ok ? Result<StaffDefinition>.Ok(s) : Result<StaffDefinition>.Fail(j.Errors);
        }
    }
}
