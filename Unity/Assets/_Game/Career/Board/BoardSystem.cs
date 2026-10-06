using System;
using System.Collections.Generic;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Game.Data.Loading;
using Game.Rules.Board;

namespace Game.Career.Board
{
    /// <summary>
    /// Season objectives and manager confidence (GAME_DESIGN §6, B7, X-45): one objective per club per season, set
    /// from the previous season's table position (or squad OVR for the first season, X-40's pattern); evaluated at
    /// season end, where confidence moves and a club under the threshold gets a new manager (automatic simulation,
    /// X-45) instead of ending the career.
    /// </summary>
    public static class Objectives
    {
        public static void Assign(GameDatabase db, CareerState state, Game.Career.Season.Season season)
        {
            var o = db.Board.Objectives;
            var world = state.World;
            int divisionSize = db.World.Generation.ClubsPerDivision;
            var previousTables = state.History.Count > 0 ? state.History[state.History.Count - 1].FinalTables : null;

            IReadOnlyDictionary<Id, int> positions = previousTables != null ? PositionsFromTables(previousTables) : PositionsFromSquadOvr(db, world);

            foreach (var club in world.Clubs)
            {
                int position = positions.TryGetValue(club.Id, out var p) ? p : divisionSize / 2;
                ObjectiveType type;
                if (club.DivisionIndex > 0 && position <= o.PromoteTopPositions) type = ObjectiveType.Promote;
                else if (club.DivisionIndex < 3 && position > divisionSize - o.AvoidRelegationBottomPositions) type = ObjectiveType.AvoidRelegation;
                else type = ObjectiveType.BreakEven;
                season.Objectives[club.Id] = new Objective { ClubId = club.Id, Year = season.Year, Type = type };
            }
        }

        /// <summary>Confidence per objective, dismissals (new manager, same club). Returns the dismissed clubs.</summary>
        public static List<Id> Evaluate(GameDatabase db, CareerState state, Game.Career.Season.Season endedSeason, SeasonSummary summary)
        {
            var c = db.Board.Confidence;
            var dismissed = new List<Id>();
            foreach (var kv in endedSeason.Objectives)
            {
                var obj = kv.Value;
                var manager = state.World.ManagerOf(kv.Key);
                if (manager == null) continue;
                bool achieved = Achieved(obj, summary);
                obj.Achieved = achieved;
                manager.Confidence = Math.Max(0, Math.Min(100, manager.Confidence + (achieved ? c.ObjectiveMetBonus : -c.ObjectiveMissedPenalty)));
                if (manager.Confidence < c.DismissalThreshold)
                {
                    manager.Dismissals++;
                    manager.Confidence = c.ResetAfterDismissal;
                    dismissed.Add(kv.Key);
                }
            }
            return dismissed;
        }

        private static bool Achieved(Objective obj, SeasonSummary summary)
        {
            switch (obj.Type)
            {
                case ObjectiveType.Promote: return Contains(summary.Promoted, obj.ClubId);
                case ObjectiveType.AvoidRelegation: return !Contains(summary.Relegated, obj.ClubId);
                default: return !summary.SeasonNetByClub.TryGetValue(obj.ClubId, out var net) || net >= 0;
            }
        }

        private static bool Contains(IReadOnlyList<Id> list, Id id) { foreach (var x in list) if (x == id) return true; return false; }

        private static IReadOnlyDictionary<Id, int> PositionsFromTables(IReadOnlyList<IReadOnlyList<Id>> tables)
        {
            var positions = new Dictionary<Id, int>();
            foreach (var table in tables)
                for (int i = 0; i < table.Count; i++) positions[table[i]] = i + 1;
            return positions;
        }

        /// <summary>First season (no history yet): rank by squad OVR within the division, like X-40's cup qualifiers.</summary>
        private static IReadOnlyDictionary<Id, int> PositionsFromSquadOvr(GameDatabase db, WorldState world)
        {
            var positions = new Dictionary<Id, int>();
            for (int d = 0; d < world.DivisionNames.Count; d++)
            {
                var clubs = new List<(Id Id, double Ovr)>();
                foreach (var c in world.Clubs)
                    if (c.DivisionIndex == d) clubs.Add((c.Id, WorldValidator.SquadAverageOvr(world, db, c.Id)));
                clubs.Sort((a, b) => a.Ovr != b.Ovr ? b.Ovr.CompareTo(a.Ovr) : a.Id.CompareTo(b.Id));
                for (int i = 0; i < clubs.Count; i++) positions[clubs[i].Id] = i + 1;
            }
            return positions;
        }
    }

    /// <summary>
    /// Stadium and Training Center (GAME_DESIGN §8, B7, X-45): one upgrade project at a time per facility, with a
    /// cost and a duration (data); the market AI policy queues one whenever affordable and below target.
    /// </summary>
    public static class Facilities
    {
        public static void MonthEnd(GameDatabase db, CareerState state, DateTime date)
        {
            var world = state.World;
            var f = db.Facilities;
            foreach (var club in world.Clubs)
            {
                CompleteIfDue(db, world, club, FacilityType.Stadium, date);
                CompleteIfDue(db, world, club, FacilityType.TrainingCenter, date);
                TryQueue(f, world, state, club, FacilityType.Stadium, date);
                TryQueue(f, world, state, club, FacilityType.TrainingCenter, date);
            }
        }

        /// <summary>MVP_SCOPE's stadium requirement by division (B7, X-45): can this club play here?</summary>
        public static bool MeetsStadiumRequirement(GameDatabase db, Club club, int divisionIndex) =>
            club.Stadium.Level >= BoardRules.StadiumMinimumLevel(db.Facilities, divisionIndex);

        private static void CompleteIfDue(GameDatabase db, WorldState world, Club club, FacilityType type, DateTime date)
        {
            var project = world.ActiveProjectOf(club.Id, type);
            if (project == null || date < project.CompletesOn) return;
            if (type == FacilityType.Stadium)
            {
                club.Stadium.Level = project.TargetLevel;
                club.Stadium.Capacity = db.World.Generation.StadiumCapacityByLevel[project.TargetLevel - 1];
            }
            else club.TrainingCenterLevel = project.TargetLevel;
            world.FacilityProjectList.Remove(project);
        }

        private static void TryQueue(Data.Career.FacilitiesDefinition f, WorldState world, CareerState state, Club club, FacilityType type, DateTime date)
        {
            if (world.ActiveProjectOf(club.Id, type) != null) return;
            int current = type == FacilityType.Stadium ? club.Stadium.Level : club.TrainingCenterLevel;
            int max = type == FacilityType.Stadium ? 10 : 5;
            if (current >= max) return;
            long cost = type == FacilityType.Stadium ? BoardRules.StadiumUpgradeCost(f, current) : BoardRules.TrainingCenterUpgradeCost(f, current);
            if (club.Budget < cost) return;
            int durationMonths = type == FacilityType.Stadium ? f.Stadium.UpgradeDurationMonths : f.TrainingCenter.UpgradeDurationMonths;
            LedgerBook.Post(state, club.Id, date, LedgerCategory.FacilityInvestment, -cost);
            world.FacilityProjectList.Add(new FacilityProject
            {
                Id = state.Ids.Next(), ClubId = club.Id, Type = type, TargetLevel = current + 1, CompletesOn = date.AddMonths(durationMonths),
            });
        }
    }

    /// <summary>
    /// Technical staff (GAME_DESIGN §7, B7, X-45): every club keeps its physio, assistant coach and scout filled,
    /// hiring at level 1 if missing and upgrading when it can afford the division's starting level.
    /// </summary>
    public static class Staff
    {
        private static readonly StaffRole[] Roles = { StaffRole.Physio, StaffRole.Assistant, StaffRole.Scout };

        public static void MonthEnd(GameDatabase db, CareerState state, DateTime date)
        {
            var world = state.World;
            var s = db.Staff;
            foreach (var club in world.Clubs)
                foreach (var role in Roles)
                {
                    var member = world.StaffOf(club.Id, role);
                    if (member == null)
                    {
                        member = new StaffMember { Id = state.Ids.Next(), ClubId = club.Id, Role = role, Level = 1 };
                        world.StaffList.Add(member);
                    }
                    LedgerBook.Post(state, club.Id, date, LedgerCategory.StaffWages, -(BoardRules.StaffWage(s, member.Level) / 12));

                    int targetLevel = BoardRules.LevelForDivision(s.InitialLevelByDivision, club.DivisionIndex);
                    if (member.Level < targetLevel && club.Budget >= BoardRules.StaffUpgradeCost(s, member.Level))
                    {
                        LedgerBook.Post(state, club.Id, date, LedgerCategory.StaffWages, -BoardRules.StaffUpgradeCost(s, member.Level));
                        member.Level++;
                    }
                }
        }

        /// <summary>Training factor from the club's assistant coach, for Development.Week (replaces the B4 neutral
        /// AssistantCoachFactor).</summary>
        public static float AssistantFactor(GameDatabase db, WorldState world, Id clubId)
        {
            var member = world.StaffOf(clubId, StaffRole.Assistant);
            return member == null ? 1f : BoardRules.AssistantDevelopmentFactor(db.Staff, member.Level);
        }

        /// <summary>Energy-recovery multiplier from the club's physio (GAME_DESIGN §7).</summary>
        public static float PhysioEnergyMultiplier(GameDatabase db, WorldState world, Id clubId)
        {
            var member = world.StaffOf(clubId, StaffRole.Physio);
            return member == null ? 1f : BoardRules.PhysioEnergyMultiplier(db.Staff, member.Level);
        }

        /// <summary>Narrowing factor from the club's scout, for MarketRules.ScoutPotentialRange (B5).</summary>
        public static float ScoutNarrowing(GameDatabase db, WorldState world, Id clubId)
        {
            var member = world.StaffOf(clubId, StaffRole.Scout);
            return member == null ? 1f : BoardRules.ScoutNarrowing(db.Staff, member.Level);
        }
    }
}
