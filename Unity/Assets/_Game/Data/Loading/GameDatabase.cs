namespace Game.Data.Loading
{
    /// <summary>
    /// Immutable game definitions loaded from Data/: effect catalog (<see cref="Effects.Balance"/>),
    /// OVR weights, world-generation definitions, formations, shared match rules and QuickSim coefficients. Other definitions are added by the stages that need them.
    /// </summary>
    public sealed class GameDatabase
    {
        public string SourceDescription { get; }
        public Effects.Balance Balance { get; }
        public Ovr.OvrDefinition Ovr { get; }
        public World.WorldDefinition World { get; }
        public System.Collections.Generic.IReadOnlyList<Match.FormationDefinition> Formations { get; }
        public Match.MatchRulesDefinition MatchRules { get; }
        public Match.QuickSimDefinition QuickSim { get; }
        public Competitions.CompetitionsDefinition Competitions { get; }
        public Competitions.CalendarDefinition Calendar { get; }
        public Career.DevelopmentDefinition Development { get; }
        public Career.MarketDefinition Market { get; }
        public Career.EconomyDefinition Economy { get; }

        internal GameDatabase(string sourceDescription, Effects.Balance balance, Ovr.OvrDefinition ovr, World.WorldDefinition world,
            System.Collections.Generic.IReadOnlyList<Match.FormationDefinition> formations, Match.MatchRulesDefinition matchRules,
            Match.QuickSimDefinition quickSim, Competitions.CompetitionsDefinition competitions, Competitions.CalendarDefinition calendar,
            Career.DevelopmentDefinition development, Career.MarketDefinition market, Career.EconomyDefinition economy)
        {
            Economy = economy;
            Market = market;
            Development = development;
            Competitions = competitions;
            Calendar = calendar;
            Formations = formations;
            MatchRules = matchRules;
            QuickSim = quickSim;
            SourceDescription = sourceDescription;
            Balance = balance;
            Ovr = ovr;
            World = world;
        }

        /// <summary>Formation by id, or null.</summary>
        public Match.FormationDefinition Formation(string id)
        {
            foreach (var f in Formations) if (f.Id == id) return f;
            return null;
        }
    }
}
