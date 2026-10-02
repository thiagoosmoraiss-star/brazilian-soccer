using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Career;
using Game.Data.Effects;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Career/development.json.</summary>
    public static class DevelopmentReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidDevelopment = "INVALID_DEVELOPMENT";

        public static Result<DevelopmentDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.DevelopmentFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "weeksPerYear", "ageCurve", "potentialGapForFullGrowth", "minutes",
                "staff", "decline", "annual", "condition", "injuries", "suspensions", "retirement", "youth");
            if (root == null) return Result<DevelopmentDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidDevelopment, m); }
            bool Prob(float p) => p >= 0f && p <= 1f;

            var d = new DevelopmentDefinition
            {
                WeeksPerYear = j.Int(root, "weeksPerYear", "root"),
                PotentialGapForFullGrowth = j.Float(root, "potentialGapForFullGrowth", "root"),
            };

            var curve = new List<AgeRate>();
            var ca = j.Array(root, "ageCurve", "root");
            if (ca != null)
                for (int i = 0; i < ca.Count; i++)
                {
                    string p = $"ageCurve[{i}]";
                    var o = j.AsObject(ca[i], p);
                    if (o == null) continue;
                    j.Keys(o, p, "maxAge", "ovrPerYear");
                    curve.Add(new AgeRate { MaxAge = j.Int(o, "maxAge", p), OvrPerYear = j.Float(o, "ovrPerYear", p) });
                    if (i > 0) Check(curve[i].MaxAge > curve[i - 1].MaxAge, "ageCurve must be ordered by maxAge.");
                }
            d.AgeCurve = curve;

            var m = j.Object(root, "minutes", "root");
            if (m != null)
            {
                j.Keys(m, "minutes", "noMinutesFactor", "fullMinutesShare");
                d.NoMinutesFactor = j.Float(m, "noMinutesFactor", "minutes");
                d.FullMinutesShare = j.Float(m, "fullMinutesShare", "minutes");
            }
            var st = j.Object(root, "staff", "root");
            if (st != null)
            {
                j.Keys(st, "staff", "trainingCenterFactor", "assistantCoachFactor");
                d.TrainingCenterFactor = j.Float(st, "trainingCenterFactor", "staff");
                d.AssistantCoachFactor = j.Float(st, "assistantCoachFactor", "staff");
            }
            var de = j.Object(root, "decline", "root");
            if (de != null)
            {
                j.Keys(de, "decline", "physicalAttributes", "otherAttributeChance");
                var phys = new List<Attr>();
                foreach (var name in j.StringList(de, "physicalAttributes", "decline"))
                    if (j.TryEnum(name, "decline.physicalAttributes", out Attr a)) phys.Add(a);
                d.PhysicalAttributes = phys;
                d.OtherAttributeDeclineChance = j.Float(de, "otherAttributeChance", "decline");
            }
            var an = j.Object(root, "annual", "root");
            if (an != null)
            {
                j.Keys(an, "annual", "minAppearances", "goodRating", "goodRatingBonus");
                d.AnnualMinAppearances = j.Int(an, "minAppearances", "annual");
                d.AnnualGoodRating = j.Float(an, "goodRating", "annual");
                d.AnnualGoodRatingBonus = j.Float(an, "goodRatingBonus", "annual");
            }
            var co = j.Object(root, "condition", "root");
            if (co != null)
            {
                j.Keys(co, "condition", "energyRecoveryPerDay", "moraleUpOnWin", "moraleDownOnLoss", "moraleToNeutralOnDraw", "formMatches");
                d.EnergyRecoveryPerDay = j.Float(co, "energyRecoveryPerDay", "condition");
                d.MoraleUpOnWin = j.Float(co, "moraleUpOnWin", "condition");
                d.MoraleDownOnLoss = j.Float(co, "moraleDownOnLoss", "condition");
                d.MoraleToNeutralOnDraw = j.Float(co, "moraleToNeutralOnDraw", "condition");
                d.FormMatches = j.Int(co, "formMatches", "condition");
            }
            var inj = j.Object(root, "injuries", "root");
            if (inj != null)
            {
                j.Keys(inj, "injuries", "lightMatches", "mediumMatches", "severeDays");
                d.LightInjuryMatches = j.IntRange(inj, "lightMatches", "injuries");
                d.MediumInjuryMatches = j.IntRange(inj, "mediumMatches", "injuries");
                d.SevereInjuryDays = j.IntRange(inj, "severeDays", "injuries");
            }
            var su = j.Object(root, "suspensions", "root");
            if (su != null)
            {
                j.Keys(su, "suspensions", "yellowsPerSuspension", "secondYellowMatches", "straightRedMatchWeights", "resetEachSeason");
                d.YellowsPerSuspension = j.Int(su, "yellowsPerSuspension", "suspensions");
                d.SecondYellowMatches = j.Int(su, "secondYellowMatches", "suspensions");
                d.StraightRedMatchWeights = j.IntList(su, "straightRedMatchWeights", "suspensions");
                d.ResetSuspensionsEachSeason = j.Bool(su, "resetEachSeason", "suspensions");
            }
            var re = j.Object(root, "retirement", "root");
            var ret = new List<RetirementChance>();
            if (re != null)
            {
                j.Keys(re, "retirement", "chanceByAge");
                var arr = j.Array(re, "chanceByAge", "retirement");
                if (arr != null)
                    for (int i = 0; i < arr.Count; i++)
                    {
                        string p = $"retirement.chanceByAge[{i}]";
                        var o = j.AsObject(arr[i], p);
                        if (o == null) continue;
                        j.Keys(o, p, "age", "chance");
                        ret.Add(new RetirementChance { Age = j.Int(o, "age", p), Chance = j.Float(o, "chance", p) });
                    }
            }
            d.RetirementByAge = ret;
            var y = j.Object(root, "youth", "root");
            if (y != null)
            {
                j.Keys(y, "youth", "perClubPerYear", "age", "ovrOffsetFromSquadMean", "potentialAboveOvr", "minSquadSize", "minGoalkeepers");
                d.YouthPerClubPerYear = j.Int(y, "perClubPerYear", "youth");
                d.YouthAge = j.IntRange(y, "age", "youth");
                d.YouthOvrOffsetFromSquadMean = j.IntRange(y, "ovrOffsetFromSquadMean", "youth");
                d.YouthPotentialAboveOvr = j.IntRange(y, "potentialAboveOvr", "youth");
                d.MinSquadSize = j.Int(y, "minSquadSize", "youth");
                d.MinGoalkeepers = j.Int(y, "minGoalkeepers", "youth");
            }

            Check(d.WeeksPerYear > 0, "weeksPerYear must be > 0.");
            Check(curve.Count > 0, "ageCurve cannot be empty.");
            Check(d.PotentialGapForFullGrowth > 0f, "potentialGapForFullGrowth must be > 0.");
            Check(Prob(d.NoMinutesFactor) && d.FullMinutesShare > 0f && d.FullMinutesShare <= 1f, "minutes factors invalid.");
            Check(d.TrainingCenterFactor > 0f && d.AssistantCoachFactor > 0f, "staff factors must be > 0.");
            Check(Prob(d.OtherAttributeDeclineChance), "decline.otherAttributeChance must be within 0-1.");
            Check(d.EnergyRecoveryPerDay > 0f && Prob(d.MoraleUpOnWin) && Prob(d.MoraleDownOnLoss) && Prob(d.MoraleToNeutralOnDraw) && d.FormMatches > 0,
                "condition values invalid.");
            Check(d.LightInjuryMatches.IsValid && d.LightInjuryMatches.Min >= 1 && d.MediumInjuryMatches.IsValid && d.SevereInjuryDays.IsValid,
                "injury ranges invalid.");
            Check(d.YellowsPerSuspension > 0 && d.SecondYellowMatches > 0 && d.StraightRedMatchWeights.Count > 0, "suspension values invalid.");
            for (int i = 1; i < ret.Count; i++) Check(ret[i].Age > ret[i - 1].Age, "retirement ages must increase.");
            Check(ret.Count > 0 && ret[ret.Count - 1].Chance == 1f, "the last retirement age must have chance 1 (nobody plays forever).");
            Check(d.YouthPerClubPerYear >= 0 && d.YouthAge.IsValid && d.YouthPotentialAboveOvr.IsValid && d.YouthPotentialAboveOvr.Min >= 0
                  && d.MinSquadSize >= 11 && d.MinGoalkeepers >= 1, "youth values invalid.");
            return j.Ok ? Result<DevelopmentDefinition>.Ok(d) : Result<DevelopmentDefinition>.Fail(j.Errors);
        }
    }
}
