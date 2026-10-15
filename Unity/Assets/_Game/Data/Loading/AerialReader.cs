using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/aerial.json.</summary>
    public static class AerialReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidAerial = "INVALID_AERIAL";

        public static Result<AerialDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.AerialFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "high", "header", "ai");
            if (root == null) return Result<AerialDefinition>.Fail(j.Errors);
            var d = new AerialDefinition();

            var h = j.Object(root, "high", "root");
            if (h != null)
            {
                j.Keys(h, "high", "longElevationDegrees", "crossElevationDegrees", "crossArrivalHeight", "minDistance", "maxDistance",
                    "maxTargetDistance", "spaceDistance", "maxLaunchSpeed", "longErrorMultiplier", "powerErrorScale", "crossZoneDepth",
                    "crossZoneMinLateral", "crossDefaultDepth", "throwInMaxDistance");
                d.LongElevationDegrees = j.Float(h, "longElevationDegrees", "high");
                d.CrossElevationDegrees = j.Float(h, "crossElevationDegrees", "high");
                d.CrossArrivalHeight = j.Float(h, "crossArrivalHeight", "high");
                d.MinDistance = j.Float(h, "minDistance", "high");
                d.MaxDistance = j.Float(h, "maxDistance", "high");
                d.MaxTargetDistance = j.Float(h, "maxTargetDistance", "high");
                d.SpaceDistance = j.Float(h, "spaceDistance", "high");
                d.MaxLaunchSpeed = j.Float(h, "maxLaunchSpeed", "high");
                d.LongErrorMultiplier = j.Float(h, "longErrorMultiplier", "high");
                d.PowerErrorScale = j.Float(h, "powerErrorScale", "high");
                d.CrossZoneDepth = j.Float(h, "crossZoneDepth", "high");
                d.CrossZoneMinLateral = j.Float(h, "crossZoneMinLateral", "high");
                d.CrossDefaultDepth = j.Float(h, "crossDefaultDepth", "high");
                d.ThrowInMaxDistance = j.Float(h, "throwInMaxDistance", "high");
            }
            var e = j.Object(root, "header", "root");
            if (e != null)
            {
                j.Keys(e, "header", "reachHeight", "radius", "closenessWeight", "shotRange", "shotTargetHeight", "verticalErrorFraction",
                    "passSpeedFactor", "passElevationDegrees", "clearanceElevationDegrees", "clearanceSpreadDegrees");
                d.HeaderReachHeight = j.Float(e, "reachHeight", "header");
                d.HeaderRadius = j.Float(e, "radius", "header");
                d.HeaderClosenessWeight = j.Float(e, "closenessWeight", "header");
                d.HeaderShotRange = j.Float(e, "shotRange", "header");
                d.HeaderShotTargetHeight = j.Float(e, "shotTargetHeight", "header");
                d.HeaderVerticalErrorFraction = j.Float(e, "verticalErrorFraction", "header");
                d.HeaderPassSpeedFactor = j.Float(e, "passSpeedFactor", "header");
                d.HeaderPassElevationDegrees = j.Float(e, "passElevationDegrees", "header");
                d.ClearanceElevationDegrees = j.Float(e, "clearanceElevationDegrees", "header");
                d.ClearanceSpreadDegrees = j.Float(e, "clearanceSpreadDegrees", "header");
            }
            var a = j.Object(root, "ai", "root");
            if (a != null)
            {
                j.Keys(a, "ai", "crossValue", "longBallMinDistance", "longBallValue", "dropZonePlayers", "cornerCrossChance", "goalKickLongChance");
                d.AiCrossValue = j.Float(a, "crossValue", "ai");
                d.AiLongBallMinDistance = j.Float(a, "longBallMinDistance", "ai");
                d.AiLongBallValue = j.Float(a, "longBallValue", "ai");
                d.AiDropZonePlayers = j.Int(a, "dropZonePlayers", "ai");
                d.AiCornerCrossChance = j.Float(a, "cornerCrossChance", "ai");
                d.AiGoalKickLongChance = j.Float(a, "goalKickLongChance", "ai");
            }
            if (!j.Ok) return Result<AerialDefinition>.Fail(j.Errors);

            bool ok = Angle(d.LongElevationDegrees) && Angle(d.CrossElevationDegrees) && d.CrossArrivalHeight >= 0f
                      && d.MinDistance > 0f && d.MaxDistance > d.MinDistance && d.MaxTargetDistance > 0f && d.SpaceDistance > 0f
                      && d.MaxLaunchSpeed > 0f && d.LongErrorMultiplier >= 0f && d.PowerErrorScale >= 0f
                      && d.CrossZoneDepth > 0f && d.CrossZoneMinLateral >= 0f && d.CrossDefaultDepth > 0f && d.ThrowInMaxDistance > 0f
                      && d.HeaderReachHeight > 0f && d.HeaderRadius > 0f && d.HeaderClosenessWeight >= 0f && d.HeaderClosenessWeight <= 1f
                      && d.HeaderShotRange >= 0f && d.HeaderShotTargetHeight >= 0f && d.HeaderVerticalErrorFraction >= 0f
                      && d.HeaderPassSpeedFactor > 0f && d.HeaderPassSpeedFactor <= 1f
                      && Angle(d.HeaderPassElevationDegrees) && Angle(d.ClearanceElevationDegrees)
                      && d.ClearanceSpreadDegrees >= 0f && d.ClearanceSpreadDegrees <= 90f
                      && d.AiCrossValue >= 0f && d.AiLongBallMinDistance >= 0f && d.AiLongBallValue >= 0f && d.AiDropZonePlayers >= 0
                      && Chance(d.AiCornerCrossChance) && Chance(d.AiGoalKickLongChance);
            if (!ok)
            {
                j.Fail(InvalidAerial, "aerial values invalid (elevations in (0, 80], maxDistance > minDistance, closeness and "
                    + "chances in [0, 1], passSpeedFactor in (0, 1], spread in [0, 90]).");
                return Result<AerialDefinition>.Fail(j.Errors);
            }
            return Result<AerialDefinition>.Ok(d);
        }

        private static bool Angle(float deg) => deg > 0f && deg <= 80f;
        private static bool Chance(float p) => p >= 0f && p <= 1f;
    }
}
