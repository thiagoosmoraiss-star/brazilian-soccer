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
    /// a candidate list with the current condition is given (B4).
    /// </summary>
    public static class MatchSetupFactory
    {
        public const float FreshEnergy = 100f;
        public const int NeutralMorale = 3;

        public static MatchPlayerSetup ToMatchPlayer(Player p) => ToMatchPlayer(p, FreshEnergy, NeutralMorale, null);

        /// <summary>Player with his current condition (B4: energy at the match date, morale, form).</summary>
        public static MatchPlayerSetup ToMatchPlayer(Player p, float energy, int morale, float? form)
        {
            var secondary = new int[p.SecondaryPositions.Count];
            for (int i = 0; i < secondary.Length; i++) secondary[i] = (int)p.SecondaryPositions[i];
            return new MatchPlayerSetup(p.Id, p.Attributes, (int)p.MainPosition, secondary, energy, morale, form,
                p.PreferredFoot == Foot.Left, p.WeakFoot);
        }

        /// <summary>Team from an explicit candidate list (available players with their condition).</summary>
        public static MatchTeamSetup Team(MatchRules rules, GameDatabase db, Id clubId, TacticSetup tactic,
            IReadOnlyList<MatchPlayerSetup> candidates, int benchSize)
        {
            var formation = db.Formation(tactic.FormationId) ?? throw new ArgumentException("Unknown formation " + tactic.FormationId);
            var (starters, bench) = Lineups.Pick(rules, formation, candidates, benchSize);
            return new MatchTeamSetup(clubId, tactic, starters, bench);
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
