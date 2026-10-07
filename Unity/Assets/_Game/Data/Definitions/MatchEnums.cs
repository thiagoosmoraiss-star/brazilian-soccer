namespace Game.Data.Definitions
{
    /// <summary>Tactical role of a formation slot (TECHNICAL_SPEC §9).</summary>
    public enum FormationRole
    {
        GK = 0,
        CB = 1,
        FB = 2,
        DM = 3,
        CM = 4,
        AM = 5,
        W = 6,
        ST = 7,
    }

    /// <summary>Team strength sectors shared by MatchEngine and QuickSim (TECHNICAL_SPEC §10).</summary>
    public enum Sector
    {
        Attack = 0,
        Creation = 1,
        Defense = 2,
        Aerial = 3,
        Goalkeeping = 4,
    }

    public static class SectorInfo
    {
        public const int Count = 5;
    }

    /// <summary>Team-layer phase of the match AI (TECHNICAL_SPEC §7; SetPiece arrives with restarts in A7).</summary>
    public enum TeamPhase
    {
        Build = 0,
        Attack = 1,
        TransitionAttack = 2,
        TransitionDefense = 3,
        Defend = 4,
    }

    public static class TeamPhaseInfo
    {
        public const int Count = 5;
        /// <summary>JSON key of each phase, indexed by <see cref="TeamPhase"/>.</summary>
        public static readonly string[] Keys = { "build", "attack", "transitionAttack", "transitionDefense", "defend" };
        public static bool InPossession(TeamPhase p) => p == TeamPhase.Build || p == TeamPhase.Attack || p == TeamPhase.TransitionAttack;
    }

    public static class FormationRoleInfo
    {
        public const int Count = 8;
    }
}
