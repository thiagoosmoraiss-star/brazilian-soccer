using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Rules.Match
{
    /// <summary>
    /// Substitution rules shared by the playable match and QuickSim (TECHNICAL_SPEC §7 "Substitution | Validar e aplicar
    /// trocas", §10 "Substituições | Mesmas regras e IA"; GAME_DESIGN §27: 5 trocas em 3 paradas + intervalo): the limits,
    /// who may leave and enter, and the AI's choice (tired players replaced by the best bench option for the slot when it
    /// is not much worse). Pure functions over plain values; no allocation.
    /// </summary>
    public static class Substitutions
    {
        // ---------------- limits ----------------

        /// <summary>One more substitution fits in <see cref="MatchSetup.MaxSubstitutions"/>.</summary>
        public static bool HasSubstitutionLeft(int made, int maxSubstitutions) => made < maxSubstitutions;

        /// <summary>A stoppage may be used: half-time never counts; otherwise fewer than <c>maxWindows</c> used so far.</summary>
        public static bool HasStoppageLeft(int stoppagesUsed, bool halftime, SubstitutionRules rules) =>
            halftime || stoppagesUsed < rules.MaxWindows;

        /// <summary>Only a player on the pitch who was not sent off may leave.</summary>
        public static bool CanLeave(bool onPitch, bool sentOff) => onPitch && !sentOff;

        /// <summary>Only an unused substitute who was not sent off may enter (a player who left never comes back).</summary>
        public static bool CanEnter(bool alreadyUsed, bool sentOff) => !alreadyUsed && !sentOff;

        /// <summary>True when <paramref name="minute"/> is one of the AI's substitution windows other than half-time.</summary>
        public static bool IsWindow(int minute, SubstitutionRules rules)
        {
            var windows = rules.Windows;
            for (int i = 0; i < windows.Count; i++)
                if (windows[i] == minute && windows[i] != rules.HalftimeMinute) return true;
            return false;
        }

        // ---------------- AI choice ----------------

        /// <summary>The AI may still replace a tired player: substitutions left and fewer than <c>maxTiredSubstitutions</c>.</summary>
        public static bool AiMayReplaceTired(int made, int maxSubstitutions, int tiredMade, SubstitutionRules rules) =>
            HasSubstitutionLeft(made, maxSubstitutions) && tiredMade < rules.MaxTiredSubstitutions;

        /// <summary>Below the energy threshold: a candidate to be replaced.</summary>
        public static bool IsTired(float energy, SubstitutionRules rules) => energy < rules.EnergyThreshold;

        /// <summary>The AI replaces the most tired first; ties by player Id (determinism).</summary>
        public static bool MoreTired(float energy, Id player, float bestEnergy, Id bestPlayer) =>
            energy < bestEnergy || (energy == bestEnergy && player.CompareTo(bestPlayer) < 0);

        /// <summary>The best available substitute for a slot (<see cref="Lineups.Best"/>), or null.</summary>
        public static MatchPlayerSetup BestReplacement(MatchRules rules, IReadOnlyList<MatchPlayerSetup> available, Position slot) =>
            available.Count == 0 ? null : Lineups.Best(rules, available, slot);

        /// <summary>A tired player is replaced only if the substitute is at most <c>benchOvrMargin</c> worse than him
        /// (effective OVR in the slot, his own penalized by his energy).</summary>
        public static bool WorthReplacing(MatchRules rules, MatchPlayerSetup on, MatchPlayerSetup off, Position slot, float offEnergy) =>
            !(rules.EffectiveOvr(on, slot) < rules.EffectiveOvr(off, slot) * rules.EnergyFactor(off.Attributes, offEnergy)
              - rules.Definition.Substitutions.BenchOvrMargin);
    }
}
