using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/goalkeeper.json.</summary>
    public static class GoalkeeperReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidGoalkeeper = "INVALID_GOALKEEPER";

        public static Result<GoalkeeperDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.GoalkeeperFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "positioning", "reaction", "save");
            if (root == null) return Result<GoalkeeperDefinition>.Fail(j.Errors);
            var d = new GoalkeeperDefinition();

            var p = j.Object(root, "positioning", "root");
            if (p != null)
            {
                j.Keys(p, "positioning", "minDepth", "maxDepth", "nearBallDistance", "farBallDistance", "sweeperLineFraction",
                    "sweeperMaxDepth", "lateralLimit", "errorResampleSeconds", "arriveRadius", "sprintDistance");
                d.MinDepth = j.Float(p, "minDepth", "positioning");
                d.MaxDepth = j.Float(p, "maxDepth", "positioning");
                d.NearBallDistance = j.Float(p, "nearBallDistance", "positioning");
                d.FarBallDistance = j.Float(p, "farBallDistance", "positioning");
                d.SweeperLineFraction = j.Float(p, "sweeperLineFraction", "positioning");
                d.SweeperMaxDepth = j.Float(p, "sweeperMaxDepth", "positioning");
                d.LateralLimit = j.Float(p, "lateralLimit", "positioning");
                d.ErrorResampleSeconds = j.Float(p, "errorResampleSeconds", "positioning");
                d.ArriveRadius = j.Float(p, "arriveRadius", "positioning");
                d.SprintDistance = j.Float(p, "sprintDistance", "positioning");
            }
            var r = j.Object(root, "reaction", "root");
            if (r != null)
            {
                j.Keys(r, "reaction", "predictionHorizonSeconds", "threatMargin", "unsetSpeed", "unsetPenaltySeconds");
                d.PredictionHorizonSeconds = j.Float(r, "predictionHorizonSeconds", "reaction");
                d.ThreatMargin = j.Float(r, "threatMargin", "reaction");
                d.UnsetSpeed = j.Float(r, "unsetSpeed", "reaction");
                d.UnsetPenaltySeconds = j.Float(r, "unsetPenaltySeconds", "reaction");
            }
            var s = j.Object(root, "save", "root");
            if (s != null)
            {
                j.Keys(s, "save", "handReach", "reachHeight", "diveSpeed", "getUpMinSeconds", "getUpMaxSeconds", "getUpPerReactionSecond", "catchReferenceSpeed",
                    "catchSpeedSlope", "catchSpeedMultMin", "diveCatchFactor", "highBallHeight", "highCatchFactor", "spinCatchPenalty",
                    "maxCatchChance", "parryRestitution", "parryMinAngleDegrees", "parryMaxAngleDegrees", "parryLift", "holdSeconds");
                d.HandReach = j.Float(s, "handReach", "save");
                d.ReachHeight = j.Float(s, "reachHeight", "save");
                d.DiveSpeed = j.Float(s, "diveSpeed", "save");
                d.GetUpMinSeconds = j.Float(s, "getUpMinSeconds", "save");
                d.GetUpMaxSeconds = j.Float(s, "getUpMaxSeconds", "save");
                d.GetUpPerReactionSecond = j.Float(s, "getUpPerReactionSecond", "save");
                d.CatchReferenceSpeed = j.Float(s, "catchReferenceSpeed", "save");
                d.CatchSpeedSlope = j.Float(s, "catchSpeedSlope", "save");
                d.CatchSpeedMultMin = j.Float(s, "catchSpeedMultMin", "save");
                d.DiveCatchFactor = j.Float(s, "diveCatchFactor", "save");
                d.HighBallHeight = j.Float(s, "highBallHeight", "save");
                d.HighCatchFactor = j.Float(s, "highCatchFactor", "save");
                d.SpinCatchPenalty = j.Float(s, "spinCatchPenalty", "save");
                d.MaxCatchChance = j.Float(s, "maxCatchChance", "save");
                d.ParryRestitution = j.Float(s, "parryRestitution", "save");
                d.ParryMinAngleDegrees = j.Float(s, "parryMinAngleDegrees", "save");
                d.ParryMaxAngleDegrees = j.Float(s, "parryMaxAngleDegrees", "save");
                d.ParryLift = j.Float(s, "parryLift", "save");
                d.HoldSeconds = j.Float(s, "holdSeconds", "save");
            }
            if (!j.Ok) return Result<GoalkeeperDefinition>.Fail(j.Errors);

            bool ok = d.MinDepth > 0f && d.MaxDepth >= d.MinDepth && d.NearBallDistance >= 0f && d.FarBallDistance > d.NearBallDistance
                      && d.SweeperLineFraction >= 0f && d.SweeperMaxDepth >= d.MaxDepth && d.LateralLimit >= 0f
                      && d.ErrorResampleSeconds > 0f && d.ArriveRadius > 0f && d.SprintDistance > 0f
                      && d.PredictionHorizonSeconds > 0f && d.ThreatMargin >= 0f && d.UnsetSpeed > 0f && d.UnsetPenaltySeconds >= 0f
                      && d.HandReach > 0f && d.ReachHeight > 0f && d.DiveSpeed > 0f
                      && d.GetUpMinSeconds >= 0f && d.GetUpMaxSeconds >= d.GetUpMinSeconds && d.GetUpPerReactionSecond >= 0f
                      && d.CatchReferenceSpeed > 0f && d.CatchSpeedSlope >= 0f && d.CatchSpeedMultMin >= 0f
                      && d.DiveCatchFactor >= 0f && d.DiveCatchFactor <= 1f && d.HighBallHeight >= 0f
                      && d.HighCatchFactor >= 0f && d.HighCatchFactor <= 1f && d.SpinCatchPenalty >= 0f
                      && d.MaxCatchChance > 0f && d.MaxCatchChance <= 1f
                      && d.ParryRestitution >= 0f && d.ParryRestitution < 1f
                      && d.ParryMinAngleDegrees >= 0f && d.ParryMaxAngleDegrees >= d.ParryMinAngleDegrees && d.ParryMaxAngleDegrees < 90f
                      && d.ParryLift >= 0f && d.HoldSeconds >= 0f;
            if (!ok)
            {
                j.Fail(InvalidGoalkeeper, "goalkeeper values invalid (minDepth > 0, maxDepth >= minDepth, farBallDistance > nearBallDistance, "
                    + "sweeperMaxDepth >= maxDepth, get-up max >= min, factors and chances 0-1, parryRestitution < 1, parry angles ordered and < 90).");
                return Result<GoalkeeperDefinition>.Fail(j.Errors);
            }
            return Result<GoalkeeperDefinition>.Ok(d);
        }
    }
}
