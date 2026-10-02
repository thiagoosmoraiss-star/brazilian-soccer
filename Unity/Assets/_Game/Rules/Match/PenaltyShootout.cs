using System;
using System.Collections.Generic;
using Game.Core.Random;
using Game.Data.Effects;

namespace Game.Rules.Match
{
    /// <summary>
    /// Penalty shootout shared by both engines (X-41): kicks alternate, best takers first (PenaltyAimWobble effect),
    /// 5 each then sudden death. Keeper quality uses the GkReactionTime effect. Coefficients in match_rules.json.
    /// </summary>
    public static class PenaltyShootout
    {
        /// <summary>Taker attribute arrays in kicking order; keeper attributes may be null (no keeper on the pitch).</summary>
        public static (int Home, int Away) Play(MatchRules rules, IReadOnlyList<int[]> homeTakers, int[] homeKeeper,
            IReadOnlyList<int[]> awayTakers, int[] awayKeeper, Rng rng)
        {
            if (homeTakers.Count == 0 || awayTakers.Count == 0) throw new ArgumentException("Both teams need takers.");
            var d = rules.Definition.PenaltyShootout;
            int home = 0, away = 0;
            for (int kick = 0; kick < d.KicksPerTeam; kick++)
            {
                if (Kick(rules, homeTakers[kick % homeTakers.Count], awayKeeper, rng)) home++;
                if (Decided(home, away, kick + 1, kick, d.KicksPerTeam)) return (home, away);
                if (Kick(rules, awayTakers[kick % awayTakers.Count], homeKeeper, rng)) away++;
                if (Decided(home, away, kick + 1, kick + 1, d.KicksPerTeam)) return (home, away);
            }
            for (int round = 0; round < d.MaxSuddenDeathRounds && home == away; round++)
            {
                int k = d.KicksPerTeam + round;
                bool h = Kick(rules, homeTakers[k % homeTakers.Count], awayKeeper, rng);
                bool a = Kick(rules, awayTakers[k % awayTakers.Count], homeKeeper, rng);
                if (h) home++;
                if (a) away++;
            }
            // Extremely long shootouts are settled by a coin toss to guarantee termination.
            if (home == away) { if (rng.NextInt(0, 2) == 0) home++; else away++; }
            return (home, away);
        }

        private static bool Kick(MatchRules rules, int[] taker, int[] keeper, Rng rng)
        {
            var d = rules.Definition.PenaltyShootout;
            double p = d.ScoreChance * Math.Pow(1.0 / rules.EffectRatio(Effect.PenaltyAimWobble, taker), d.TakerExponent);
            if (keeper != null) p *= Math.Pow(rules.EffectRatio(Effect.GkReactionTime, keeper), d.KeeperExponent);
            return rng.NextDouble() < Math.Min(d.MaxScoreChance, p);
        }

        /// <summary>True when one side can no longer be caught within the regulation kicks.</summary>
        private static bool Decided(int home, int away, int homeTaken, int awayTaken, int perTeam) =>
            home > away + (perTeam - awayTaken) || away > home + (perTeam - homeTaken);
    }
}
