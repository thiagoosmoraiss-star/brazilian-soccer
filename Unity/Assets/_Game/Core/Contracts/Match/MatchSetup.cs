using System;
using System.Collections.Generic;
using Game.Core.Ids;

namespace Game.Core.Contracts.Match
{
    /// <summary>
    /// Single input of any match (D-19): MatchEngine and QuickSim both consume it. Plain data only
    /// (Ids, attribute values, tactic parameters, condition, seed); no reference to Data, Rules or Career.
    /// Immutable after construction.
    /// </summary>
    public sealed class MatchSetup
    {
        public MatchTeamSetup Home { get; }
        public MatchTeamSetup Away { get; }
        /// <summary>True when neither side has home advantage (e.g. a neutral venue).</summary>
        public bool NeutralVenue { get; }
        /// <summary>Configured real-time duration (4/6/10 min). Used by the MatchEngine; QuickSim ignores it.</summary>
        public int DurationMinutes { get; }
        public int MaxSubstitutions { get; }
        public ulong Seed { get; }

        public MatchSetup(MatchTeamSetup home, MatchTeamSetup away, bool neutralVenue, int durationMinutes, int maxSubstitutions, ulong seed)
        {
            Home = home ?? throw new ArgumentNullException(nameof(home));
            Away = away ?? throw new ArgumentNullException(nameof(away));
            if (maxSubstitutions < 0) throw new ArgumentOutOfRangeException(nameof(maxSubstitutions));
            NeutralVenue = neutralVenue;
            DurationMinutes = durationMinutes;
            MaxSubstitutions = maxSubstitutions;
            Seed = seed;
        }

        public MatchTeamSetup Team(MatchSide side) => side == MatchSide.Home ? Home : Away;
    }

    public enum MatchSide
    {
        Home = 0,
        Away = 1,
    }

    /// <summary>A team for one match: 11 starters (each in a formation slot), bench and tactic.</summary>
    public sealed class MatchTeamSetup
    {
        public const int StarterCount = 11;

        public Id ClubId { get; }
        public TacticSetup Tactic { get; }
        /// <summary>Starters in formation slot order (slot i of the formation).</summary>
        public IReadOnlyList<MatchPlayerSetup> Starters { get; }
        public IReadOnlyList<MatchPlayerSetup> Bench { get; }

        public MatchTeamSetup(Id clubId, TacticSetup tactic, IReadOnlyList<MatchPlayerSetup> starters, IReadOnlyList<MatchPlayerSetup> bench)
        {
            Tactic = tactic ?? throw new ArgumentNullException(nameof(tactic));
            if (starters == null || starters.Count != StarterCount)
                throw new ArgumentException($"A team needs exactly {StarterCount} starters.", nameof(starters));
            ClubId = clubId;
            Starters = Copy(starters);
            Bench = Copy(bench ?? Array.Empty<MatchPlayerSetup>());
        }

        private static MatchPlayerSetup[] Copy(IReadOnlyList<MatchPlayerSetup> source)
        {
            var a = new MatchPlayerSetup[source.Count];
            for (int i = 0; i < a.Length; i++) a[i] = source[i] ?? throw new ArgumentException("Null player.");
            return a;
        }
    }

    /// <summary>Tactic parameters of the MVP (TECHNICAL_SPEC §9): formation, mentality 1-5, line 1-3, pressure 1-3.</summary>
    public sealed class TacticSetup
    {
        public string FormationId { get; }
        public int Mentality { get; }
        public int DefensiveLine { get; }
        public int Pressure { get; }

        public TacticSetup(string formationId, int mentality, int defensiveLine, int pressure)
        {
            if (string.IsNullOrEmpty(formationId)) throw new ArgumentException("Formation is required.", nameof(formationId));
            if (mentality < 1 || mentality > 5) throw new ArgumentOutOfRangeException(nameof(mentality));
            if (defensiveLine < 1 || defensiveLine > 3) throw new ArgumentOutOfRangeException(nameof(defensiveLine));
            if (pressure < 1 || pressure > 3) throw new ArgumentOutOfRangeException(nameof(pressure));
            FormationId = formationId;
            Mentality = mentality;
            DefensiveLine = defensiveLine;
            Pressure = pressure;
        }
    }

    /// <summary>
    /// A player as seen by the match: attribute values (18, indexed like the attribute enum), positions as
    /// integer codes (indices of the position enum), and condition at kick-off.
    /// </summary>
    public sealed class MatchPlayerSetup
    {
        public Id PlayerId { get; }
        private readonly int[] _attributes;
        public ReadOnlySpan<int> Attributes => _attributes;
        public int MainPosition { get; }
        private readonly int[] _secondaryPositions;
        public ReadOnlySpan<int> SecondaryPositions => _secondaryPositions;
        /// <summary>Energy at kick-off, 0-100.</summary>
        public float Energy { get; }
        /// <summary>Morale level 1-5 (3 = neutral).</summary>
        public int Morale { get; }
        /// <summary>Form = average of the last ratings (0-10); null when the player has no recent ratings.</summary>
        public float? Form { get; }

        public MatchPlayerSetup(Id playerId, IReadOnlyList<int> attributes, int mainPosition, IReadOnlyList<int> secondaryPositions,
            float energy, int morale, float? form)
        {
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));
            if (energy < 0f || energy > 100f) throw new ArgumentOutOfRangeException(nameof(energy));
            if (morale < 1 || morale > 5) throw new ArgumentOutOfRangeException(nameof(morale));
            PlayerId = playerId;
            _attributes = new int[attributes.Count];
            for (int i = 0; i < _attributes.Length; i++) _attributes[i] = attributes[i];
            MainPosition = mainPosition;
            _secondaryPositions = new int[secondaryPositions?.Count ?? 0];
            for (int i = 0; i < _secondaryPositions.Length; i++) _secondaryPositions[i] = secondaryPositions[i];
            Energy = energy;
            Morale = morale;
            Form = form;
        }
    }
}
