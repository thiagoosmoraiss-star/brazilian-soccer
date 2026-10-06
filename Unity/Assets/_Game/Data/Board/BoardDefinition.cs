namespace Game.Data.Board
{
    public sealed class ObjectiveParameters
    {
        /// <summary>Previous-season top N positions in the division get the Promote objective.</summary>
        public int PromoteTopPositions { get; internal set; }
        /// <summary>Previous-season bottom N positions in the division get the AvoidRelegation objective.</summary>
        public int AvoidRelegationBottomPositions { get; internal set; }
    }

    public sealed class ConfidenceParameters
    {
        public int Initial { get; internal set; }
        public int ObjectiveMetBonus { get; internal set; }
        public int ObjectiveMissedPenalty { get; internal set; }
        /// <summary>Confidence below this at a season end (GAME_DESIGN §6) means dismissal.</summary>
        public int DismissalThreshold { get; internal set; }
        /// <summary>The new manager's starting confidence after a dismissal (B7, X-45).</summary>
        public int ResetAfterDismissal { get; internal set; }
    }

    /// <summary>Board: season objectives and manager confidence (Data/Board/board.json, B7, baseline v1, X-45).</summary>
    public sealed class BoardDefinition
    {
        public ObjectiveParameters Objectives { get; internal set; }
        public ConfidenceParameters Confidence { get; internal set; }
    }
}
