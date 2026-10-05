using System;
using System.Collections.Generic;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Career;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Rules.Ovr;

namespace Game.Career.Players
{
    /// <summary>
    /// Development (TECHNICAL_SPEC §11: weekly + annual). Growth = age-curve rate x potential gap x minutes x staff
    /// (CT/assistant neutral until B7); decline after 31, physical attributes first. Never above potential.
    /// Progress is kept in OVR points: one point raises every role attribute of the main position by 1, which raises
    /// the OVR by exactly 1 (weights are normalized).
    /// </summary>
    public static class Development
    {
        public static float RatePerYear(DevelopmentDefinition d, int age)
        {
            foreach (var r in d.AgeCurve) if (age <= r.MaxAge) return r.OvrPerYear;
            return d.AgeCurve[d.AgeCurve.Count - 1].OvrPerYear;
        }

        public static float MinutesFactor(DevelopmentDefinition d, float minutesShare)
        {
            float t = Math.Min(1f, Math.Max(0f, minutesShare) / d.FullMinutesShare);
            return d.NoMinutesFactor + (1f - d.NoMinutesFactor) * t;
        }

        /// <summary>One week of development for one player.</summary>
        public static void Week(GameDatabase db, Player p, int age, float minutesShare, Rng rng)
        {
            var d = db.Development;
            float rate = RatePerYear(d, age);
            float delta;
            if (rate > 0f)
            {
                int ovr = OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition);
                float gap = p.Potential - ovr;
                if (gap <= 0f) { p.Condition.DevelopmentProgress = 0f; return; }
                delta = rate * Math.Min(1f, gap / d.PotentialGapForFullGrowth) * MinutesFactor(d, minutesShare)
                        * d.TrainingCenterFactor * d.AssistantCoachFactor / d.WeeksPerYear;
            }
            else delta = rate / d.WeeksPerYear;
            AddProgress(db, p, delta, rng);
        }

        /// <summary>Annual adjustment (December): good seasons add a bonus (still capped by potential).</summary>
        public static void Annual(GameDatabase db, Player p, Rng rng)
        {
            var d = db.Development;
            var c = p.Condition;
            if (c.SeasonAppearances >= d.AnnualMinAppearances && c.SeasonRatingSum / c.SeasonAppearances >= d.AnnualGoodRating)
                AddProgress(db, p, d.AnnualGoodRatingBonus, rng);
        }

        internal static void AddProgress(GameDatabase db, Player p, float delta, Rng rng)
        {
            var c = p.Condition;
            c.DevelopmentProgress += delta;
            while (c.DevelopmentProgress >= 1f)
            {
                c.DevelopmentProgress -= 1f;
                if (!Grow(db, p)) { c.DevelopmentProgress = 0f; break; }
            }
            while (c.DevelopmentProgress <= -1f)
            {
                c.DevelopmentProgress += 1f;
                Decline(db, p, rng);
            }
        }

        /// <summary>+1 to every role attribute; reverted when it would exceed the potential. False when blocked.</summary>
        private static bool Grow(GameDatabase db, Player p)
        {
            var before = (int[])p.AttributeValues.Clone();
            bool changed = false;
            for (int a = 0; a < AttrInfo.Count; a++)
                if (db.Ovr.Weight(p.MainPosition, (Attr)a) > 0f && p.AttributeValues[a] < AttrInfo.MaxValue)
                {
                    p.AttributeValues[a]++;
                    changed = true;
                }
            if (!changed || OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition) > p.Potential)
            {
                Array.Copy(before, p.AttributeValues, before.Length);
                return false;
            }
            return true;
        }

        /// <summary>-1 to every physical attribute; other role attributes decline with a chance (GDD: physical first).</summary>
        private static void Decline(GameDatabase db, Player p, Rng rng)
        {
            var d = db.Development;
            foreach (var a in d.PhysicalAttributes)
                if (p.AttributeValues[(int)a] > AttrInfo.MinValue) p.AttributeValues[(int)a]--;
            for (int a = 0; a < AttrInfo.Count; a++)
            {
                var attr = (Attr)a;
                if (db.Ovr.Weight(p.MainPosition, attr) <= 0f || Contains(d.PhysicalAttributes, attr)) continue;
                if (rng.NextDouble() < d.OtherAttributeDeclineChance && p.AttributeValues[a] > AttrInfo.MinValue) p.AttributeValues[a]--;
            }
        }

        private static bool Contains(IReadOnlyList<Attr> list, Attr a)
        {
            foreach (var x in list) if (x == a) return true;
            return false;
        }
    }

    /// <summary>Condition between matches: energy recovery, availability, and the effects of a played match.</summary>
    public static class ConditionSystem
    {
        public static float EnergyAt(DevelopmentDefinition d, Player p, DateTime date)
        {
            var c = p.Condition;
            if (!c.EnergyDate.HasValue) return c.Energy;
            double days = Math.Max(0, (date - c.EnergyDate.Value).TotalDays);
            return (float)Math.Min(100.0, c.Energy + days * d.EnergyRecoveryPerDay);
        }

        public static bool Available(Player p, CompetitionKind kind, DateTime date) =>
            !p.Condition.IsInjured(date) && p.Condition.SuspendedMatches((int)kind) == 0;

        /// <summary>
        /// Applies a played match to the club's squad: suspensions served and injury matches counted for absent players,
        /// then minutes, ratings, energy, cards (yellows per competition, red-card bans), new injuries and morale.
        /// </summary>
        public static void AfterMatch(GameDatabase db, IReadOnlyList<Player> squad, MatchSide side, MatchResult result,
            CompetitionKind kind, DateTime date, Rng rng)
        {
            var d = db.Development;
            int k = (int)kind;
            var stats = new Dictionary<Id, PlayerMatchStats>();
            foreach (var s in result.PlayerStats) if (s.Side == side) stats[s.PlayerId] = s;
            int goalsFor = result.Goals(side), goalsAgainst = result.Goals(side == MatchSide.Home ? MatchSide.Away : MatchSide.Home);

            foreach (var p in squad)
            {
                var c = p.Condition;
                bool inSquad = stats.TryGetValue(p.Id, out var st);
                bool played = inSquad && st.Rating.HasValue;

                // Absences served (evaluated before this match's new bans and injuries).
                if (!inSquad && c.SuspendedMatchCount[k] > 0) c.SuspendedMatchCount[k]--;
                if (!inSquad && c.InjuryMatchesLeft > 0 && --c.InjuryMatchesLeft == 0 && !c.InjuredUntil.HasValue)
                    c.Injury = InjurySeverity.None;
                if (c.InjuredUntil.HasValue && date >= c.InjuredUntil.Value)
                {
                    c.InjuredUntil = null;
                    if (c.InjuryMatchesLeft == 0) c.Injury = InjurySeverity.None;
                }
                if (!played) continue;

                c.Energy = st.FinalEnergy;
                c.EnergyDate = date;
                c.SeasonAppearances++;
                c.SeasonMinutes += st.MinutesPlayed;
                c.SeasonGoals += st.Goals;
                c.SeasonRatingSum += st.Rating.Value;
                c.RecentRatingValues.Add(st.Rating.Value);
                while (c.RecentRatingValues.Count > d.FormMatches) c.RecentRatingValues.RemoveAt(0);

                // Cards (GAME_DESIGN §9: 3 yellows = 1 match; red = 1-3 matches; per competition).
                if (st.RedCard && st.YellowCards >= 2) c.SuspendedMatchCount[k] += d.SecondYellowMatches;
                else if (st.RedCard) c.SuspendedMatchCount[k] += 1 + PickIndex(d.StraightRedMatchWeights, rng);
                else if (st.YellowCards > 0)
                {
                    c.YellowCount[k] += st.YellowCards;
                    while (c.YellowCount[k] >= d.YellowsPerSuspension)
                    {
                        c.YellowCount[k] -= d.YellowsPerSuspension;
                        c.SuspendedMatchCount[k]++;
                    }
                }

                // Injuries (GAME_DESIGN §9: light 1-2 dates, medium 3-6, severe 2-6 months).
                if (st.Injury != InjurySeverity.None)
                {
                    c.Injury = st.Injury;
                    switch (st.Injury)
                    {
                        case InjurySeverity.Light: c.InjuryMatchesLeft = rng.NextInt(d.LightInjuryMatches.Min, d.LightInjuryMatches.Max + 1); break;
                        case InjurySeverity.Medium: c.InjuryMatchesLeft = rng.NextInt(d.MediumInjuryMatches.Min, d.MediumInjuryMatches.Max + 1); break;
                        default: c.InjuredUntil = date.AddDays(rng.NextInt(d.SevereInjuryDays.Min, d.SevereInjuryDays.Max + 1)); break;
                    }
                }

                // Simple morale (5 levels).
                if (goalsFor > goalsAgainst) { if (rng.NextDouble() < d.MoraleUpOnWin && c.Morale < 5) c.Morale++; }
                else if (goalsFor < goalsAgainst) { if (rng.NextDouble() < d.MoraleDownOnLoss && c.Morale > 1) c.Morale--; }
                else if (rng.NextDouble() < d.MoraleToNeutralOnDraw && c.Morale != 3) c.Morale += c.Morale < 3 ? 1 : -1;
            }
        }

        /// <summary>Season reset: minutes share kept for next season's development, counters cleared.</summary>
        public static void NewSeason(DevelopmentDefinition d, Player p, int clubMatchesPlayed)
        {
            var c = p.Condition;
            c.PreviousMinutesShare = clubMatchesPlayed > 0 ? Math.Min(1f, c.SeasonMinutes / (90f * clubMatchesPlayed)) : c.PreviousMinutesShare;
            c.SeasonAppearances = 0;
            c.SeasonMinutes = 0;
            c.SeasonGoals = 0;
            c.SeasonRatingSum = 0f;
            if (d.ResetSuspensionsEachSeason)
                for (int k = 0; k < PlayerCondition.Competitions; k++) { c.YellowCount[k] = 0; c.SuspendedMatchCount[k] = 0; }
        }

        internal static int PickIndex(IReadOnlyList<int> weights, Rng rng)
        {
            int total = 0;
            foreach (var w in weights) total += w;
            int roll = rng.NextInt(0, total);
            for (int i = 0; i < weights.Count; i++)
            {
                if (roll < weights[i]) return i;
                roll -= weights[i];
            }
            return weights.Count - 1;
        }
    }

    /// <summary>Retirements at the season end (GAME_DESIGN §9: 34+ steep decline, retirement).</summary>
    public static class Retirement
    {
        public static float Chance(DevelopmentDefinition d, int age)
        {
            float chance = 0f;
            foreach (var r in d.RetirementByAge) if (age >= r.Age) chance = r.Chance;
            return chance;
        }

        /// <summary>Removes retiring players (and their contracts). Returns the retired player Ids.</summary>
        public static List<Id> Apply(GameDatabase db, WorldState world, DateTime date, Rng rng)
        {
            var retired = new List<Id>();
            foreach (var p in world.Players)
            {
                int age = p.BirthDate.AgeOn(date.Year, date.Month, date.Day);
                float chance = Chance(db.Development, age);
                if (chance > 0f && rng.NextDouble() < chance) retired.Add(p.Id);
            }
            foreach (var id in retired) world.RemovePlayer(id);
            return retired;
        }
    }

    /// <summary>
    /// Youth intake (MVP: 3 per year for every club, X-42) at the start of the year, plus a squad floor (minimum squad
    /// size and goalkeepers) until the market (B5) maintains squads.
    /// </summary>
    public static class Youth
    {
        public static List<Player> Intake(GameDatabase db, WorldState world, IdAllocator ids, int year, bool regularIntake, Rng rng)
        {
            var created = new List<Player>();
            var d = db.Development;
            var slots = db.World.Generation.Squad.Slots;
            foreach (var club in world.Clubs)
            {
                if (regularIntake)
                    for (int i = 0; i < d.YouthPerClubPerYear; i++)
                        created.Add(Create(db, world, ids, club, slots[rng.NextInt(0, slots.Count)].Position, year, rng));

                int keepers = 0;
                foreach (var p in world.SquadOf(club.Id)) if (p.MainPosition == Position.GOL) keepers++;
                while (keepers < d.MinGoalkeepers) { created.Add(Create(db, world, ids, club, Position.GOL, year, rng)); keepers++; }
                while (world.SquadOf(club.Id).Count < d.MinSquadSize)
                    created.Add(Create(db, world, ids, club, slots[rng.NextInt(0, slots.Count)].Position, year, rng));
            }
            return created;
        }

        private static Player Create(GameDatabase db, WorldState world, IdAllocator ids, Club club, Position position, int year, Rng rng)
        {
            var d = db.Development;
            double mean = 0; int n = 0;
            foreach (var p in world.SquadOf(club.Id)) { mean += OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition); n++; }
            float squadMean = n == 0 ? (float)db.World.Generation.Divisions[club.DivisionIndex].SquadOvrMean.Min : (float)(mean / n);
            float target = squadMean + rng.NextInt(d.YouthOvrOffsetFromSquadMean.Min, d.YouthOvrOffsetFromSquadMean.Max + 1);
            int age = rng.NextInt(d.YouthAge.Min, d.YouthAge.Max + 1);

            var player = WorldGenerator.CreatePlayer(db.Ovr, db.World, position, target, age, false, rng, ids, year);
            int ovr = OvrCalculator.Rating(db.Ovr, player.AttributeSpan, player.MainPosition);
            var pot = db.World.Generation.Potential;
            int potential = ovr + rng.NextInt(d.YouthPotentialAboveOvr.Min, d.YouthPotentialAboveOvr.Max + 1);
            player.Potential = Math.Max(Math.Max(ovr, pot.Min), Math.Min(pot.Max, potential));
            world.AddPlayer(player, WorldGenerator.NewContract(db, club, player, year, rng, ids));
            return player;
        }
    }
}
