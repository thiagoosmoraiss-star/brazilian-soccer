using System;
using System.Collections.Generic;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Loading;
using Game.Rules.Match;

namespace Game.Career.Match
{
    /// <summary>
    /// Career side of the match contract (ARCHITECTURE §4: "Carreira monta MatchSetup"): converts a club's squad
    /// into a <see cref="MatchTeamSetup"/> using the shared lineup AI. Condition defaults to fresh players until
    /// B4 (condition, morale, form) exists.
    /// </summary>
    public static class MatchSetupFactory
    {
        public const float FreshEnergy = 100f;
        public const int NeutralMorale = 3;

        public static MatchPlayerSetup ToMatchPlayer(Player p)
        {
            var secondary = new int[p.SecondaryPositions.Count];
            for (int i = 0; i < secondary.Length; i++) secondary[i] = (int)p.SecondaryPositions[i];
            return new MatchPlayerSetup(p.Id, p.Attributes, (int)p.MainPosition, secondary, FreshEnergy, NeutralMorale, null);
        }

        public static MatchTeamSetup Team(WorldState world, MatchRules rules, GameDatabase db, Id clubId, TacticSetup tactic, int benchSize)
        {
            var formation = db.Formation(tactic.FormationId) ?? throw new ArgumentException("Unknown formation " + tactic.FormationId);
            var candidates = new List<MatchPlayerSetup>();
            foreach (var p in world.SquadOf(clubId)) candidates.Add(ToMatchPlayer(p));
            var (starters, bench) = Lineups.Pick(rules, formation, candidates, benchSize);
            return new MatchTeamSetup(clubId, tactic, starters, bench);
        }
    }
}
