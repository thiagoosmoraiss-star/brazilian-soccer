using Game.Core.Results;
using Game.Data.Presentation;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Presentation/match_view.json.</summary>
    public static class MatchViewReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidMatchView = "INVALID_MATCH_VIEW";

        public static Result<MatchViewDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.MatchViewFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "camera", "radar", "animation", "hud");
            if (root == null) return Result<MatchViewDefinition>.Fail(j.Errors);
            var d = new MatchViewDefinition();

            var c = j.Object(root, "camera", "root");
            if (c != null)
            {
                j.Keys(c, "camera", "pitchDegrees", "farDistance", "nearDistance", "zoomInGoalDistance", "zoomOutGoalDistance",
                    "farSideExtraPitchDegrees", "followSeconds", "zoomSeconds", "lookaheadSeconds", "maxLookahead", "fieldOfView");
                d.PitchDegrees = j.Float(c, "pitchDegrees", "camera");
                d.FarDistance = j.Float(c, "farDistance", "camera");
                d.NearDistance = j.Float(c, "nearDistance", "camera");
                d.ZoomInGoalDistance = j.Float(c, "zoomInGoalDistance", "camera");
                d.ZoomOutGoalDistance = j.Float(c, "zoomOutGoalDistance", "camera");
                d.FarSideExtraPitchDegrees = j.Float(c, "farSideExtraPitchDegrees", "camera");
                d.FollowSeconds = j.Float(c, "followSeconds", "camera");
                d.ZoomSeconds = j.Float(c, "zoomSeconds", "camera");
                d.LookaheadSeconds = j.Float(c, "lookaheadSeconds", "camera");
                d.MaxLookahead = j.Float(c, "maxLookahead", "camera");
                d.FieldOfView = j.Float(c, "fieldOfView", "camera");
            }
            var r = j.Object(root, "radar", "root");
            if (r != null)
            {
                j.Keys(r, "radar", "widthFraction", "opacity", "dotSize");
                d.RadarWidthFraction = j.Float(r, "widthFraction", "radar");
                d.RadarOpacity = j.Float(r, "opacity", "radar");
                d.RadarDotSize = j.Float(r, "dotSize", "radar");
            }
            var a = j.Object(root, "animation", "root");
            if (a != null)
            {
                j.Keys(a, "animation", "strideMeters", "legSwingDegrees", "sprintLegSwingDegrees", "armSwingFraction", "moveSpeedThreshold",
                    "kickSeconds", "kickSwingDegrees");
                d.StrideMeters = j.Float(a, "strideMeters", "animation");
                d.LegSwingDegrees = j.Float(a, "legSwingDegrees", "animation");
                d.SprintLegSwingDegrees = j.Float(a, "sprintLegSwingDegrees", "animation");
                d.ArmSwingFraction = j.Float(a, "armSwingFraction", "animation");
                d.MoveSpeedThreshold = j.Float(a, "moveSpeedThreshold", "animation");
                d.KickSeconds = j.Float(a, "kickSeconds", "animation");
                d.KickSwingDegrees = j.Float(a, "kickSwingDegrees", "animation");
            }
            var h = j.Object(root, "hud", "root");
            if (h != null)
            {
                j.Keys(h, "hud", "bannerSeconds");
                d.BannerSeconds = j.Float(h, "bannerSeconds", "hud");
            }
            if (!j.Ok) return Result<MatchViewDefinition>.Fail(j.Errors);

            bool ok = d.PitchDegrees > 0f && d.PitchDegrees < 90f && d.FarDistance >= d.NearDistance && d.NearDistance > 0f
                      && d.ZoomOutGoalDistance > d.ZoomInGoalDistance && d.ZoomInGoalDistance >= 0f && d.FarSideExtraPitchDegrees >= 0f
                      && d.PitchDegrees + d.FarSideExtraPitchDegrees < 90f
                      && d.FollowSeconds > 0f && d.ZoomSeconds > 0f && d.LookaheadSeconds >= 0f && d.MaxLookahead >= 0f
                      && d.FieldOfView > 0f && d.FieldOfView < 180f
                      && d.RadarWidthFraction > 0f && d.RadarWidthFraction <= 1f && d.RadarOpacity >= 0f && d.RadarOpacity <= 1f && d.RadarDotSize > 0f
                      && d.StrideMeters > 0f && d.LegSwingDegrees >= 0f && d.SprintLegSwingDegrees >= 0f && d.ArmSwingFraction >= 0f
                      && d.MoveSpeedThreshold >= 0f && d.KickSeconds > 0f && d.KickSwingDegrees >= 0f && d.BannerSeconds > 0f;
            if (!ok)
            {
                j.Fail(InvalidMatchView, "match_view values invalid (angles 0-90, far >= near > 0, zoomOut > zoomIn, times > 0, radar 0-1).");
                return Result<MatchViewDefinition>.Fail(j.Errors);
            }
            return Result<MatchViewDefinition>.Ok(d);
        }
    }
}
