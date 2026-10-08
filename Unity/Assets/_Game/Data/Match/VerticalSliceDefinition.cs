using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;

namespace Game.Data.Match
{
    /// <summary>The vertical slice's two fictional teams (Data/VerticalSlice/teams.json, A7a; TECHNICAL_SPEC §19: "dois
    /// times 11×11 de JSON: forte (~70) e fraco (~50)"). Demo/test data, not part of the career database.</summary>
    public sealed class VerticalSliceDefinition
    {
        public IReadOnlyList<VerticalSliceTeam> Teams { get; internal set; }

        public VerticalSliceTeam Team(string key)
        {
            for (int i = 0; i < Teams.Count; i++)
                if (Teams[i].Key == key) return Teams[i];
            return null;
        }
    }

    public sealed class VerticalSliceTeam
    {
        public string Key { get; internal set; }
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public string Formation { get; internal set; }
        /// <summary>In formation slot order.</summary>
        public IReadOnlyList<VerticalSlicePlayer> Players { get; internal set; }

        /// <summary>The team as a match setup (default tactic, full energy, neutral morale). <paramref name="attributeDelta"/>
        /// optionally adds to some attributes of every outfield player (headless sensitivity reports).</summary>
        public MatchTeamSetup ToSetup(IReadOnlyList<(Effects.Attr Attr, int Delta)> attributeDelta = null)
        {
            var starters = new MatchPlayerSetup[Players.Count];
            for (int i = 0; i < starters.Length; i++)
            {
                var p = Players[i];
                var attrs = new int[p.Attributes.Count];
                for (int a = 0; a < attrs.Length; a++) attrs[a] = p.Attributes[a];
                if (attributeDelta != null && p.Role != FormationRole.GK)
                    foreach (var (attr, delta) in attributeDelta)
                        attrs[(int)attr] = Math.Max(1, Math.Min(99, attrs[(int)attr] + delta));
                starters[i] = new MatchPlayerSetup(new Id(p.Id), attrs, 0, Array.Empty<int>(), 100f, 3, null, p.LeftFooted, p.WeakFoot);
            }
            return new MatchTeamSetup(new Id(Id), new TacticSetup(Formation, 3, 2, 2), starters, Array.Empty<MatchPlayerSetup>());
        }
    }

    public sealed class VerticalSlicePlayer
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public FormationRole Role { get; internal set; }
        public bool LeftFooted { get; internal set; }
        public int WeakFoot { get; internal set; }
        /// <summary>One value 1-99 per <see cref="Effects.Attr"/>, in enum order.</summary>
        public IReadOnlyList<int> Attributes { get; internal set; }
    }
}
