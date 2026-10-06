using Game.Core.Results;
using Game.Data.Board;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Board/board.json.</summary>
    public static class BoardReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidBoard = "INVALID_BOARD";

        public static Result<BoardDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.BoardFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "objectives", "confidence");
            if (root == null) return Result<BoardDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidBoard, m); }

            var b = new BoardDefinition();
            var o = j.Object(root, "objectives", "root");
            if (o != null)
            {
                j.Keys(o, "objectives", "promoteTopPositions", "avoidRelegationBottomPositions");
                b.Objectives = new ObjectiveParameters
                {
                    PromoteTopPositions = j.Int(o, "promoteTopPositions", "objectives"),
                    AvoidRelegationBottomPositions = j.Int(o, "avoidRelegationBottomPositions", "objectives"),
                };
                Check(b.Objectives.PromoteTopPositions > 0 && b.Objectives.AvoidRelegationBottomPositions > 0, "objectives values must be > 0.");
            }

            var c = j.Object(root, "confidence", "root");
            if (c != null)
            {
                j.Keys(c, "confidence", "initial", "objectiveMetBonus", "objectiveMissedPenalty", "dismissalThreshold", "resetAfterDismissal");
                b.Confidence = new ConfidenceParameters
                {
                    Initial = j.Int(c, "initial", "confidence"),
                    ObjectiveMetBonus = j.Int(c, "objectiveMetBonus", "confidence"),
                    ObjectiveMissedPenalty = j.Int(c, "objectiveMissedPenalty", "confidence"),
                    DismissalThreshold = j.Int(c, "dismissalThreshold", "confidence"),
                    ResetAfterDismissal = j.Int(c, "resetAfterDismissal", "confidence"),
                };
                Check(InRange(b.Confidence.Initial) && InRange(b.Confidence.DismissalThreshold) && InRange(b.Confidence.ResetAfterDismissal),
                    "confidence values must be within 0-100.");
                Check(b.Confidence.ResetAfterDismissal > b.Confidence.DismissalThreshold, "resetAfterDismissal must clear the dismissal threshold.");
            }

            return j.Ok ? Result<BoardDefinition>.Ok(b) : Result<BoardDefinition>.Fail(j.Errors);
        }

        private static bool InRange(int v) => v >= 0 && v <= 100;
    }
}
