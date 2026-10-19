using System.Collections.Generic;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Game.Core.Results;

namespace Game.Save
{
    /// <summary>
    /// Post-load sanity checks (TECHNICAL_SPEC §14: "Validação: ... integridade referencial, invariantes
    /// (contrato único, elenco, saldo = livro, tabelas)"). A save that fails this never reaches the caller as a
    /// successful load: <see cref="SaveService"/> treats it exactly like a decompression or checksum failure
    /// and falls back to a backup.
    /// </summary>
    public static class SaveValidator
    {
        private const string Code = "INVALID_SAVE";

        public static Result Validate(CareerState state)
        {
            var errors = new List<Error>();
            var world = state.World;
            var clubIds = new HashSet<Id>();
            foreach (var c in world.Clubs) clubIds.Add(c.Id);
            var playerIds = new HashSet<Id>();
            foreach (var p in world.Players) playerIds.Add(p.Id);

            CheckClubRef(errors, clubIds, state.ManagedClubId, "managedClubId");

            var contractedPlayers = new HashSet<Id>();
            foreach (var c in world.Contracts)
            {
                CheckClubRef(errors, clubIds, c.ClubId, $"contract {c.Id}");
                if (!playerIds.Contains(c.PlayerId)) errors.Add(new Error(Code, $"contract {c.Id}: playerId {c.PlayerId} does not exist."));
                if (!contractedPlayers.Add(c.PlayerId)) errors.Add(new Error(Code, $"player {c.PlayerId} has more than one active contract."));
            }

            foreach (var m in world.Managers) CheckClubRef(errors, clubIds, m.ClubId, $"manager {m.Id}");
            foreach (var s in world.StaffMembers) CheckClubRef(errors, clubIds, s.ClubId, $"staff {s.Id}");
            foreach (var p in world.FacilityProjects) CheckClubRef(errors, clubIds, p.ClubId, $"facility project {p.Id}");
            foreach (var p in state.Proposals)
            {
                CheckClubRef(errors, clubIds, p.FromClubId, $"proposal {p.Id}");
                if (!playerIds.Contains(p.PlayerId)) errors.Add(new Error(Code, $"proposal {p.Id}: playerId {p.PlayerId} does not exist."));
            }

            if (state.Season != null) ValidateSeason(errors, clubIds, state.Season);
            if (state.MatchInProgress != null) ValidateMatchInProgress(errors, state);
            if (state.UserPlan != null) ValidateUserPlan(errors, state);
            foreach (var summary in state.History) ValidateSummary(errors, world.Clubs.Count, summary);
            ValidateBalances(errors, state);

            return Result.From(errors);
        }

        /// <summary>C1: a saved match in progress is an unplayed fixture of the managed club in the current season.</summary>
        private static void ValidateMatchInProgress(List<Error> errors, CareerState state)
        {
            var id = state.MatchInProgress.FixtureId;
            Fixture found = null;
            if (state.Season != null)
            {
                foreach (var l in state.Season.Leagues) foreach (var f in l.Fixtures) if (f.Id == id) found = f;
                foreach (var round in state.Season.Cup.Rounds) foreach (var f in round) if (f.Id == id) found = f;
            }
            if (found == null) { errors.Add(new Error(Code, $"matchInProgress: fixture {id} is not in the current season.")); return; }
            if (found.Played) errors.Add(new Error(Code, $"matchInProgress: fixture {id} was already played."));
            var managed = state.ManagedClubId;
            if (!managed.HasValue || (found.HomeClubId != managed.Value && found.AwayClubId != managed.Value))
                errors.Add(new Error(Code, $"matchInProgress: fixture {id} is not the managed club's."));
            if (state.MatchInProgress.Snapshot.Length == 0) errors.Add(new Error(Code, "matchInProgress: empty snapshot."));
        }

        /// <summary>C3b: a user plan belongs to a managed club; a chosen lineup has 11 distinct starters and a distinct bench.
        /// Ids of players who have since left are allowed (ignored when the team is built).</summary>
        private static void ValidateUserPlan(List<Error> errors, CareerState state)
        {
            var plan = state.UserPlan;
            if (!state.ManagedClubId.HasValue) errors.Add(new Error(Code, "userPlan without a managed club."));
            if (plan.Starters.Count != 0 && plan.Starters.Count != Game.Core.Contracts.Match.MatchTeamSetup.StarterCount)
                errors.Add(new Error(Code, $"userPlan: {plan.Starters.Count} starters (0 or {Game.Core.Contracts.Match.MatchTeamSetup.StarterCount})."));
            if (plan.Starters.Count == 0 && plan.Bench.Count > 0) errors.Add(new Error(Code, "userPlan: a bench without starters."));
            var seen = new HashSet<Id>();
            foreach (var id in plan.Starters) if (!seen.Add(id)) errors.Add(new Error(Code, $"userPlan: player {id} twice."));
            foreach (var id in plan.Bench) if (!seen.Add(id)) errors.Add(new Error(Code, $"userPlan: player {id} twice."));
        }

        private static void ValidateSeason(List<Error> errors, HashSet<Id> clubIds, Season season)
        {
            foreach (var l in season.Leagues)
                foreach (var f in l.Fixtures)
                    CheckFixture(errors, clubIds, f);
            foreach (var round in season.Cup.Rounds)
                foreach (var f in round) CheckFixture(errors, clubIds, f);
            foreach (var entry in season.Ledger) CheckClubRef(errors, clubIds, entry.ClubId, $"ledger entry {entry.Id}");
            foreach (var clubId in season.ClubFormations.Keys) CheckClubRef(errors, clubIds, clubId, "clubFormations");
            foreach (var clubId in season.ClubMatches.Keys) CheckClubRef(errors, clubIds, clubId, "clubMatches");
            foreach (var kv in season.Objectives)
            {
                CheckClubRef(errors, clubIds, kv.Key, "objectives");
                if (kv.Value.ClubId != kv.Key) errors.Add(new Error(Code, $"objectives[{kv.Key}]: stored under a different clubId ({kv.Value.ClubId})."));
            }
        }

        private static void CheckFixture(List<Error> errors, HashSet<Id> clubIds, Fixture f)
        {
            CheckClubRef(errors, clubIds, f.HomeClubId, $"fixture {f.Id}");
            CheckClubRef(errors, clubIds, f.AwayClubId, $"fixture {f.Id}");
        }

        private static void ValidateSummary(List<Error> errors, int clubCount, SeasonSummary summary)
        {
            var seen = new HashSet<Id>();
            int total = 0;
            foreach (var table in summary.FinalTables)
                foreach (var id in table)
                {
                    total++;
                    if (!seen.Add(id)) errors.Add(new Error(Code, $"history {summary.Year}: club {id} appears twice across the final tables."));
                }
            if (total != clubCount)
                errors.Add(new Error(Code, $"history {summary.Year}: final tables have {total} clubs, expected {clubCount}."));
        }

        private static void ValidateBalances(List<Error> errors, CareerState state)
        {
            var net = new Dictionary<Id, long>();
            foreach (var summary in state.History)
                foreach (var kv in summary.SeasonNetByClub)
                    net[kv.Key] = (net.TryGetValue(kv.Key, out var n) ? n : 0) + kv.Value;
            if (state.Season != null)
                foreach (var entry in state.Season.Ledger)
                    net[entry.ClubId] = (net.TryGetValue(entry.ClubId, out var n) ? n : 0) + entry.Amount;

            foreach (var club in state.World.Clubs)
            {
                long expected = net.TryGetValue(club.Id, out var n) ? n : 0;
                if (club.Balance != expected)
                    errors.Add(new Error(Code, $"club {club.Id}: balance {club.Balance} does not match the ledger sum {expected}."));
            }
        }

        private static void CheckClubRef(List<Error> errors, HashSet<Id> clubIds, Id? clubId, string context)
        {
            if (clubId.HasValue) CheckClubRef(errors, clubIds, clubId.Value, context);
        }

        private static void CheckClubRef(List<Error> errors, HashSet<Id> clubIds, Id clubId, string context)
        {
            if (!clubIds.Contains(clubId)) errors.Add(new Error(Code, $"{context}: clubId {clubId} does not exist."));
        }
    }
}
