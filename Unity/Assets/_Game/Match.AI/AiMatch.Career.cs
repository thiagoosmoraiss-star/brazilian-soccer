using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Rules.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// What the career needs from a played match (C1; TECHNICAL_SPEC §10 "uma entrada e uma saída idênticas em todos os
    /// modos"; X-09 "fórmula de notas" shared): every player who played gets a rating by <see cref="Ratings"/> (the career
    /// counts a player as having played only with a rating), and a knockout tie is settled by <see cref="PenaltyShootout"/>.
    /// Both draw from their own RNG streams, rebuilt from the match seed on every call (end of match, allocations fine).
    /// </summary>
    public sealed partial class AiMatch
    {
        public const string RatingsStream = "Ratings";
        public const string ShootoutStream = "Shootout";

        private PlayerMatchStats[] RatePlayers(PlayerMatchStats[] raw)
        {
            var rng = new RngStreams(_seed).Get(RatingsStream);
            var rated = new PlayerMatchStats[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                var s = raw[i];
                int goalsFor = s.Side == MatchSide.Home ? Home.Goals : Away.Goals;
                int goalsAgainst = s.Side == MatchSide.Home ? Away.Goals : Home.Goals;
                // Slot i (and a departed player's slot) gives the role he played in.
                int global = i < Players.Length ? i : _departed[i - Players.Length].Global;
                float rating = Ratings.Compute(_rules.Definition.Ratings, new Ratings.Contribution
                {
                    Minutes = s.MinutesPlayed, Goals = s.Goals, Assists = s.Assists, ShotsOnTarget = s.ShotsOnTarget,
                    ShotsOffTarget = s.Shots - s.ShotsOnTarget, Fouls = s.FoulsCommitted, Yellows = s.YellowCards, Red = s.RedCard,
                    TeamGoalsFor = goalsFor, TeamGoalsAgainst = goalsAgainst, Role = Players[global].Slot.Role,
                }, rng);
                rated[i] = new PlayerMatchStats(s.PlayerId, s.Side, s.Started, s.MinutesPlayed, s.Goals, s.Assists, s.Shots, s.ShotsOnTarget,
                    s.FoulsCommitted, s.YellowCards, s.RedCard, rating, (float)Math.Round(s.FinalEnergy, 1), s.Injury);
            }
            return rated;
        }

        /// <summary>Knockout and level after the final whistle: penalties by the shared rules (as QuickSim).</summary>
        private (int?, int?) Shootout()
        {
            if (!Finished || !_matchSetup.RequiresWinner || Home.Goals != Away.Goals) return (null, null);
            var rng = new RngStreams(_seed).Get(ShootoutStream);
            var (h, a) = PenaltyShootout.Play(_rules, Takers(Home), KeeperAttributes(Home), Takers(Away), KeeperAttributes(Away), rng);
            return (h, a);
        }

        /// <summary>The players on the pitch, best penalty takers (lowest PenaltyAimWobble) first.</summary>
        private List<int[]> Takers(AiTeam team)
        {
            var list = new List<AiPlayer>();
            foreach (var p in team.Players) if (!p.Body.OffPitch) list.Add(p);
            list.Sort((x, y) =>
            {
                int c = _balance.Eval(Effect.PenaltyAimWobble, x.Setup.Attributes).CompareTo(_balance.Eval(Effect.PenaltyAimWobble, y.Setup.Attributes));
                return c != 0 ? c : x.Setup.PlayerId.CompareTo(y.Setup.PlayerId);
            });
            var takers = new List<int[]>(list.Count);
            foreach (var p in list) takers.Add(p.Setup.Attributes.ToArray());
            return takers;
        }

        private static int[] KeeperAttributes(AiTeam team)
        {
            foreach (var p in team.Players)
                if (p.Slot.Role == FormationRole.GK && !p.Body.OffPitch) return p.Setup.Attributes.ToArray();
            return null;
        }
    }
}
