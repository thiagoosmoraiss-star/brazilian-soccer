using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/kicking.json.</summary>
    public static class KickingReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidKicking = "INVALID_KICKING";

        public static Result<KickingDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.KickingFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "common", "pass", "shot");
            if (root == null) return Result<KickingDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidKicking, m); }

            var def = new KickingDefinition();

            var c = j.Object(root, "common", "root");
            if (c != null)
            {
                j.Keys(c, "common", "tapMaxSeconds", "weakFootAngleDegrees", "orientationFreeAngleDegrees", "lowEnergyThreshold");
                def.Common = new KickingCommon
                {
                    TapMaxSeconds = j.Float(c, "tapMaxSeconds", "common"),
                    WeakFootAngleDegrees = j.Float(c, "weakFootAngleDegrees", "common"),
                    OrientationFreeAngleDegrees = j.Float(c, "orientationFreeAngleDegrees", "common"),
                    LowEnergyThreshold = j.Float(c, "lowEnergyThreshold", "common"),
                };
                var x = def.Common;
                Check(x.TapMaxSeconds > 0f && x.WeakFootAngleDegrees > 0f && x.WeakFootAngleDegrees < 180f
                      && x.OrientationFreeAngleDegrees >= 0f && x.OrientationFreeAngleDegrees < 180f
                      && x.LowEnergyThreshold > 0f && x.LowEnergyThreshold <= 100f,
                    "common values invalid.");
            }

            var p = j.Object(root, "pass", "root");
            if (p != null)
            {
                j.Keys(p, "pass", "coneHalfAngleDegrees", "coneDistanceWeightDegPerMeter", "maxTargetDistance", "spacePassDistance",
                    "arrivalSpeed", "minSpeed", "maxSpeed", "powerBarSeconds", "throughLeadDistance", "throughArrivalSpeed",
                    "pressureRadius", "orientationMaxErrorPenalty", "weakFootMaxErrorPenalty", "firstTimeErrorPenalty",
                    "lowEnergyMaxErrorPenalty", "distanceReference", "distanceErrorPerMeter");
                def.Pass = new PassParameters
                {
                    ConeHalfAngleDegrees = j.Float(p, "coneHalfAngleDegrees", "pass"),
                    ConeDistanceWeightDegPerMeter = j.Float(p, "coneDistanceWeightDegPerMeter", "pass"),
                    MaxTargetDistance = j.Float(p, "maxTargetDistance", "pass"),
                    SpacePassDistance = j.Float(p, "spacePassDistance", "pass"),
                    ArrivalSpeed = j.Float(p, "arrivalSpeed", "pass"),
                    MinSpeed = j.Float(p, "minSpeed", "pass"),
                    MaxSpeed = j.Float(p, "maxSpeed", "pass"),
                    PowerBarSeconds = j.Float(p, "powerBarSeconds", "pass"),
                    ThroughLeadDistance = j.Float(p, "throughLeadDistance", "pass"),
                    ThroughArrivalSpeed = j.Float(p, "throughArrivalSpeed", "pass"),
                    PressureRadius = j.Float(p, "pressureRadius", "pass"),
                    OrientationMaxErrorPenalty = j.Float(p, "orientationMaxErrorPenalty", "pass"),
                    WeakFootMaxErrorPenalty = j.Float(p, "weakFootMaxErrorPenalty", "pass"),
                    FirstTimeErrorPenalty = j.Float(p, "firstTimeErrorPenalty", "pass"),
                    LowEnergyMaxErrorPenalty = j.Float(p, "lowEnergyMaxErrorPenalty", "pass"),
                    DistanceReference = j.Float(p, "distanceReference", "pass"),
                    DistanceErrorPerMeter = j.Float(p, "distanceErrorPerMeter", "pass"),
                };
                var x = def.Pass;
                Check(x.ConeHalfAngleDegrees > 0f && x.ConeHalfAngleDegrees <= 90f && x.ConeDistanceWeightDegPerMeter >= 0f
                      && x.MaxTargetDistance > 0f && x.SpacePassDistance > 0f && x.ArrivalSpeed > 0f
                      && x.MinSpeed > 0f && x.MaxSpeed > x.MinSpeed && x.PowerBarSeconds > 0f
                      && x.ThroughLeadDistance >= 0f && x.ThroughArrivalSpeed > 0f && x.PressureRadius > 0f
                      && x.OrientationMaxErrorPenalty >= 0f && x.WeakFootMaxErrorPenalty >= 0f && x.FirstTimeErrorPenalty >= 0f
                      && x.LowEnergyMaxErrorPenalty >= 0f && x.DistanceReference >= 0f && x.DistanceErrorPerMeter >= 0f,
                    "pass values invalid (maxSpeed must exceed minSpeed, cone 0-90°).");
            }

            var s = j.Object(root, "shot", "root");
            if (s != null)
            {
                j.Keys(s, "shot", "powerBarSeconds", "minSpeed", "idealPowerMax", "overPowerVerticalErrorDegrees",
                    "verticalErrorFraction", "targetHeight", "cornerInset", "aimNeutralThreshold", "pressureRadius",
                    "pressureErrorScale", "pressurePowerLoss", "orientationMaxErrorPenalty", "weakFootMaxErrorPenalty",
                    "weakFootPowerLossMin", "weakFootPowerLossMax", "firstTimeErrorPenalty", "lowEnergyMaxErrorPenalty",
                    "distanceReference", "distanceErrorPerMeter");
                def.Shot = new ShotParameters
                {
                    PowerBarSeconds = j.Float(s, "powerBarSeconds", "shot"),
                    MinSpeed = j.Float(s, "minSpeed", "shot"),
                    IdealPowerMax = j.Float(s, "idealPowerMax", "shot"),
                    OverPowerVerticalErrorDegrees = j.Float(s, "overPowerVerticalErrorDegrees", "shot"),
                    VerticalErrorFraction = j.Float(s, "verticalErrorFraction", "shot"),
                    TargetHeight = j.Float(s, "targetHeight", "shot"),
                    CornerInset = j.Float(s, "cornerInset", "shot"),
                    AimNeutralThreshold = j.Float(s, "aimNeutralThreshold", "shot"),
                    PressureRadius = j.Float(s, "pressureRadius", "shot"),
                    PressureErrorScale = j.Float(s, "pressureErrorScale", "shot"),
                    PressurePowerLoss = j.Float(s, "pressurePowerLoss", "shot"),
                    OrientationMaxErrorPenalty = j.Float(s, "orientationMaxErrorPenalty", "shot"),
                    WeakFootMaxErrorPenalty = j.Float(s, "weakFootMaxErrorPenalty", "shot"),
                    WeakFootPowerLossMin = j.Float(s, "weakFootPowerLossMin", "shot"),
                    WeakFootPowerLossMax = j.Float(s, "weakFootPowerLossMax", "shot"),
                    FirstTimeErrorPenalty = j.Float(s, "firstTimeErrorPenalty", "shot"),
                    LowEnergyMaxErrorPenalty = j.Float(s, "lowEnergyMaxErrorPenalty", "shot"),
                    DistanceReference = j.Float(s, "distanceReference", "shot"),
                    DistanceErrorPerMeter = j.Float(s, "distanceErrorPerMeter", "shot"),
                };
                var x = def.Shot;
                Check(x.PowerBarSeconds > 0f && x.MinSpeed > 0f && x.IdealPowerMax > 0f && x.IdealPowerMax < 1f
                      && x.OverPowerVerticalErrorDegrees >= 0f && x.VerticalErrorFraction >= 0f && x.TargetHeight > 0f
                      && x.CornerInset >= 0f && x.AimNeutralThreshold >= 0f && x.AimNeutralThreshold < 1f
                      && x.PressureRadius > 0f && x.PressureErrorScale >= 0f && x.PressurePowerLoss >= 0f && x.PressurePowerLoss < 1f
                      && x.OrientationMaxErrorPenalty >= 0f && x.WeakFootMaxErrorPenalty >= 0f
                      && x.WeakFootPowerLossMin >= 0f && x.WeakFootPowerLossMax >= x.WeakFootPowerLossMin && x.WeakFootPowerLossMax < 1f
                      && x.FirstTimeErrorPenalty >= 0f && x.LowEnergyMaxErrorPenalty >= 0f
                      && x.DistanceReference >= 0f && x.DistanceErrorPerMeter >= 0f,
                    "shot values invalid (idealPowerMax 0-1, weakFootPowerLossMin <= weakFootPowerLossMax < 1).");
            }

            return j.Ok ? Result<KickingDefinition>.Ok(def) : Result<KickingDefinition>.Fail(j.Errors);
        }
    }
}
