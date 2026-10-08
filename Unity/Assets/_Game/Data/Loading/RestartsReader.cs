using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/restarts.json.</summary>
    public static class RestartsReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidRestarts = "INVALID_RESTARTS";

        public static Result<RestartsDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.RestartsFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "clock", "restart");
            if (root == null) return Result<RestartsDefinition>.Fail(j.Errors);
            var d = new RestartsDefinition();

            var c = j.Object(root, "clock", "root");
            if (c != null)
            {
                j.Keys(c, "clock", "halves", "displayMinutesPerHalf");
                d.Halves = j.Int(c, "halves", "clock");
                d.DisplayMinutesPerHalf = j.Float(c, "displayMinutesPerHalf", "clock");
            }
            var r = j.Object(root, "restart", "root");
            if (r != null)
            {
                j.Keys(r, "restart", "setupSeconds", "aiTakeSeconds", "humanTimeoutSeconds", "exclusionRadius", "throwInExclusionRadius",
                    "lineInset", "goalKickDepth", "goalKickLateral", "cornerInset");
                d.SetupSeconds = j.Float(r, "setupSeconds", "restart");
                d.AiTakeSeconds = j.Float(r, "aiTakeSeconds", "restart");
                d.HumanTimeoutSeconds = j.Float(r, "humanTimeoutSeconds", "restart");
                d.ExclusionRadius = j.Float(r, "exclusionRadius", "restart");
                d.ThrowInExclusionRadius = j.Float(r, "throwInExclusionRadius", "restart");
                d.LineInset = j.Float(r, "lineInset", "restart");
                d.GoalKickDepth = j.Float(r, "goalKickDepth", "restart");
                d.GoalKickLateral = j.Float(r, "goalKickLateral", "restart");
                d.CornerInset = j.Float(r, "cornerInset", "restart");
            }
            if (!j.Ok) return Result<RestartsDefinition>.Fail(j.Errors);

            bool ok = d.Halves >= 1 && d.DisplayMinutesPerHalf > 0f && d.SetupSeconds >= 0f && d.AiTakeSeconds >= 0f
                      && d.HumanTimeoutSeconds > 0f && d.ExclusionRadius >= 0f && d.ThrowInExclusionRadius >= 0f
                      && d.LineInset >= 0f && d.GoalKickDepth > 0f && d.GoalKickLateral >= 0f && d.CornerInset >= 0f;
            if (!ok)
            {
                j.Fail(InvalidRestarts, "restarts values invalid (halves >= 1, displayMinutesPerHalf > 0, humanTimeoutSeconds > 0, "
                    + "goalKickDepth > 0, others >= 0).");
                return Result<RestartsDefinition>.Fail(j.Errors);
            }
            return Result<RestartsDefinition>.Ok(d);
        }
    }
}
