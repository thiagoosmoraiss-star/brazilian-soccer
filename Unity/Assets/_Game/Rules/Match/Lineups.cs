using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Rules.Match
{
    /// <summary>
    /// Automatic lineup and substitution choices shared by the AI of both engines: best effective OVR for each slot
    /// (position fit included). Ties are broken by player Id for determinism.
    /// </summary>
    public static class Lineups
    {
        /// <summary>
        /// Picks 11 starters (in slot order) and a bench of up to <paramref name="benchSize"/> players from the candidates.
        /// Slots are filled from the scarcest (fewest natural candidates) to the most common.
        /// </summary>
        public static (MatchPlayerSetup[] Starters, List<MatchPlayerSetup> Bench) Pick(MatchRules rules, FormationDefinition formation,
            IReadOnlyList<MatchPlayerSetup> candidates, int benchSize)
        {
            if (candidates.Count < MatchTeamSetup.StarterCount)
                throw new ArgumentException($"At least {MatchTeamSetup.StarterCount} players are required.", nameof(candidates));

            var available = new List<MatchPlayerSetup>(candidates);
            var starters = new MatchPlayerSetup[formation.Slots.Count];
            var order = new List<int>();
            for (int i = 0; i < formation.Slots.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int na = Natural(available, formation.Slots[a].Position), nb = Natural(available, formation.Slots[b].Position);
                return na != nb ? na.CompareTo(nb) : a.CompareTo(b);
            });

            foreach (int slot in order)
            {
                var best = Best(rules, available, formation.Slots[slot].Position);
                starters[slot] = best;
                available.Remove(best);
            }

            // Bench: best remaining by OVR at their own main position, keeping a goalkeeper if one is left.
            available.Sort((a, b) =>
            {
                int c = rules.EffectiveOvr(b, (Position)b.MainPosition).CompareTo(rules.EffectiveOvr(a, (Position)a.MainPosition));
                return c != 0 ? c : a.PlayerId.CompareTo(b.PlayerId);
            });
            var bench = new List<MatchPlayerSetup>();
            var keeper = available.Find(p => p.MainPosition == (int)Position.GOL);
            if (keeper != null && benchSize > 0) { bench.Add(keeper); available.Remove(keeper); }
            for (int i = 0; i < available.Count && bench.Count < benchSize; i++) bench.Add(available[i]);
            return (starters, bench);
        }

        /// <summary>Best available player for a slot position, or null when none is available.</summary>
        public static MatchPlayerSetup Best(MatchRules rules, IReadOnlyList<MatchPlayerSetup> available, Position slotPosition)
        {
            MatchPlayerSetup best = null;
            float bestValue = float.MinValue;
            foreach (var p in available)
            {
                // A goalkeeper only plays in goal and only a goalkeeper plays in goal, when one exists.
                bool keeperSlot = slotPosition == Position.GOL, isKeeper = p.MainPosition == (int)Position.GOL;
                // Effective OVR, penalized below the low-energy threshold (tired players are rested).
                float value = rules.EffectiveOvr(p, slotPosition) * rules.EnergyFactor(p.Attributes, p.Energy)
                              - (keeperSlot != isKeeper ? 100f : 0f);
                if (value > bestValue || (value == bestValue && best != null && p.PlayerId.CompareTo(best.PlayerId) < 0))
                {
                    best = p;
                    bestValue = value;
                }
            }
            return best;
        }

        private static int Natural(List<MatchPlayerSetup> players, Position position)
        {
            int n = 0;
            foreach (var p in players) if (p.MainPosition == (int)position) n++;
            return n;
        }
    }
}
