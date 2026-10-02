using System;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Rules.Match
{
    /// <summary>Match rating 0-10 with one decimal (X-38), same formula for both engines; weights in match_rules.json.</summary>
    public static class Ratings
    {
        public struct Contribution
        {
            public int Minutes;
            public int Goals;
            public int Assists;
            public int ShotsOnTarget;
            public int ShotsOffTarget;
            public int Fouls;
            public int Yellows;
            public bool Red;
            public int TeamGoalsFor;
            public int TeamGoalsAgainst;
            public FormationRole Role;
        }

        public static float Compute(RatingRules r, Contribution c, Rng rng)
        {
            bool defensive = false;
            foreach (var role in r.DefensiveRoles) if (role == c.Role) defensive = true;

            // Team-level terms scale with time on the pitch; individual actions count in full.
            float presence = Math.Min(1f, c.Minutes / (float)r.FullImpactMinutes);
            float team = c.TeamGoalsFor > c.TeamGoalsAgainst ? r.Win : (c.TeamGoalsFor < c.TeamGoalsAgainst ? r.Loss : 0f);
            if (defensive)
                team += c.TeamGoalsAgainst == 0 ? r.CleanSheet : r.GoalConceded * c.TeamGoalsAgainst;

            float value = r.Base
                          + c.Goals * r.Goal + c.Assists * r.Assist
                          + c.ShotsOnTarget * r.ShotOnTarget + c.ShotsOffTarget * r.ShotOffTarget
                          + c.Fouls * r.Foul + c.Yellows * r.Yellow + (c.Red ? r.Red : 0f)
                          + team * presence
                          + ((float)rng.NextDouble() * 2f - 1f) * r.Noise * presence;
            value = value < r.Min ? r.Min : (value > r.Max ? r.Max : value);
            return (float)Math.Round(value, 1, MidpointRounding.AwayFromZero);
        }
    }
}
