using System.Collections.Generic;
using System.Globalization;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Newtonsoft.Json.Linq;

namespace Game.Save.Serialization
{
    /// <summary>
    /// Everything the save persists (TECHNICAL_SPEC §14, principle 4: <see cref="CareerState"/> only — game
    /// data/definitions come with the app, never from here). Reflection-free (D-11), hand-written like the
    /// <c>Data</c> loaders, so there is nothing for IL2CPP to strip. Mirrored by <see cref="CareerStateReader"/>.
    /// </summary>
    public static class CareerStateWriter
    {
        public static JObject Write(CareerState state)
        {
            var root = new JObject
            {
                ["schemaVersion"] = SaveSchema.CurrentVersion,
                ["seed"] = ULong(state.Seed),
                ["ids"] = new JObject { ["lastIssued"] = state.Ids.LastIssued },
                ["managedClubId"] = state.ManagedClubId.HasValue ? new JValue(state.ManagedClubId.Value.Value) : JValue.CreateNull(),
                ["proposals"] = Array(state.Proposals, WriteProposal),
                ["world"] = WriteWorld(state.World),
                ["season"] = WriteSeason(state.Season),
                ["history"] = Array(state.History, WriteSeasonSummary),
            };
            return root;
        }

        // ---------------- World ----------------

        private static JObject WriteWorld(WorldState world) => new JObject
        {
            ["seed"] = ULong(world.Seed),
            ["startYear"] = world.StartYear,
            ["lastIssuedId"] = world.LastIssuedId,
            ["divisionNames"] = Array(world.DivisionNames, n => (JToken)n),
            ["clubs"] = Array(world.Clubs, WriteClub),
            ["players"] = Array(world.Players, WritePlayer),
            ["contracts"] = Array(world.Contracts, WriteContract),
            ["managers"] = Array(world.Managers, WriteManager),
            ["staff"] = Array(world.StaffMembers, WriteStaff),
            ["facilityProjects"] = Array(world.FacilityProjects, WriteFacilityProject),
        };

        private static JObject WriteClub(Club c) => new JObject
        {
            ["id"] = c.Id.Value,
            ["name"] = c.Name,
            ["shortName"] = c.ShortName,
            ["city"] = c.City,
            ["uf"] = c.Uf,
            ["colors"] = new JObject
            {
                ["primaryId"] = c.Colors.PrimaryId,
                ["primaryHex"] = c.Colors.PrimaryHex,
                ["secondaryId"] = c.Colors.SecondaryId,
                ["secondaryHex"] = c.Colors.SecondaryHex,
            },
            ["crest"] = new JObject
            {
                ["shapeId"] = c.Crest.ShapeId,
                ["symbolId"] = c.Crest.SymbolId,
                ["primaryColorId"] = c.Crest.PrimaryColorId,
                ["secondaryColorId"] = c.Crest.SecondaryColorId,
            },
            ["divisionIndex"] = c.DivisionIndex,
            ["divisionName"] = c.DivisionName,
            ["reputation"] = c.Reputation,
            ["stars"] = c.Stars,
            ["budget"] = c.Budget,
            ["stadium"] = new JObject { ["level"] = c.Stadium.Level, ["capacity"] = c.Stadium.Capacity },
            ["fans"] = c.Fans,
            ["trainingCenterLevel"] = c.TrainingCenterLevel,
            ["balance"] = c.Balance,
            ["monthsNegativeCash"] = c.MonthsNegativeCash,
            ["cashAlert"] = c.CashAlert,
            ["transferLockout"] = c.TransferLockout,
        };

        private static JObject WritePlayer(Player p)
        {
            var cond = p.Condition;
            return new JObject
            {
                ["id"] = p.Id.Value,
                ["firstName"] = p.FirstName,
                ["lastName"] = p.LastName,
                ["nationality"] = p.Nationality,
                ["birthDate"] = Date(p.BirthDate.Year, p.BirthDate.Month, p.BirthDate.Day),
                ["heightCm"] = p.HeightCm,
                ["preferredFoot"] = p.PreferredFoot.ToString(),
                ["weakFoot"] = p.WeakFoot,
                ["avatarSeed"] = ULong(p.AvatarSeed),
                ["attributes"] = Array(p.AttributeSpan),
                ["potential"] = p.Potential,
                ["mainPosition"] = p.MainPosition.ToString(),
                ["secondaryPositions"] = ArrayEnum(p.SecondaryPositionSpan),
                ["condition"] = new JObject
                {
                    ["energy"] = cond.Energy,
                    ["energyDate"] = DateOpt(cond.EnergyDate),
                    ["morale"] = cond.Morale,
                    ["recentRatings"] = Array(cond.RecentRatings, r => (JToken)r),
                    ["injury"] = cond.Injury.ToString(),
                    ["injuryMatchesLeft"] = cond.InjuryMatchesLeft,
                    ["injuredUntil"] = DateOpt(cond.InjuredUntil),
                    ["yellowCount"] = ArrayInt(cond.YellowCount),
                    ["suspendedMatchCount"] = ArrayInt(cond.SuspendedMatchCount),
                    ["seasonAppearances"] = cond.SeasonAppearances,
                    ["seasonMinutes"] = cond.SeasonMinutes,
                    ["seasonGoals"] = cond.SeasonGoals,
                    ["seasonRatingSum"] = cond.SeasonRatingSum,
                    ["previousMinutesShare"] = cond.PreviousMinutesShare,
                    ["developmentProgress"] = cond.DevelopmentProgress,
                },
            };
        }

        private static JObject WriteContract(Contract c) => new JObject
        {
            ["id"] = c.Id.Value,
            ["playerId"] = c.PlayerId.Value,
            ["clubId"] = c.ClubId.Value,
            ["wage"] = c.Wage,
            ["startYear"] = c.StartYear,
            ["endYear"] = c.EndYear,
        };

        private static JObject WriteManager(Manager m) => new JObject
        {
            ["id"] = m.Id.Value,
            ["clubId"] = m.ClubId.Value,
            ["confidence"] = m.Confidence,
            ["dismissals"] = m.Dismissals,
        };

        private static JObject WriteStaff(StaffMember s) => new JObject
        {
            ["id"] = s.Id.Value,
            ["clubId"] = s.ClubId.Value,
            ["role"] = s.Role.ToString(),
            ["level"] = s.Level,
        };

        private static JObject WriteFacilityProject(FacilityProject p) => new JObject
        {
            ["id"] = p.Id.Value,
            ["clubId"] = p.ClubId.Value,
            ["type"] = p.Type.ToString(),
            ["targetLevel"] = p.TargetLevel,
            ["completesOn"] = Date(p.CompletesOn.Year, p.CompletesOn.Month, p.CompletesOn.Day),
        };

        // ---------------- Season ----------------

        private static JObject WriteSeason(Season s) => new JObject
        {
            ["year"] = s.Year,
            ["calendar"] = Array(s.Calendar, WriteCalendarEntry),
            ["nextEntry"] = s.NextEntry,
            ["leagues"] = Array(s.Leagues, WriteLeagueEdition),
            ["cup"] = WriteCupEdition(s.Cup),
            ["clubFormations"] = IdMap(s.ClubFormations, v => (JToken)v),
            ["clubMatches"] = IdMap(s.ClubMatches, v => (JToken)v),
            ["ledger"] = Array(s.Ledger, WriteLedgerEntry),
            ["objectives"] = IdMap(s.Objectives, WriteObjective),
        };

        private static JObject WriteCalendarEntry(CalendarEntry e) => new JObject
        {
            ["date"] = Date(e.Date.Year, e.Date.Month, e.Date.Day),
            ["kind"] = e.Kind.ToString(),
            ["round"] = e.Round,
        };

        private static JObject WriteLeagueEdition(LeagueEdition l) => new JObject
        {
            ["divisionIndex"] = l.DivisionIndex,
            ["divisionName"] = l.DivisionName,
            ["clubIds"] = ArrayId(l.ClubIds),
            ["fixtures"] = Array(l.Fixtures, WriteFixture),
            ["tiebreakSeed"] = ULong(l.TiebreakSeed),
        };

        private static JObject WriteCupEdition(CupEdition c) => new JObject
        {
            ["qualified"] = ArrayId(c.Qualified),
            ["rounds"] = Array(c.Rounds, round => Array(round, WriteFixture)),
            ["roundCount"] = c.RoundCount,
            ["winner"] = c.Winner.Value,
            ["runnerUp"] = c.RunnerUp.Value,
        };

        private static JObject WriteFixture(Fixture f) => new JObject
        {
            ["id"] = f.Id.Value,
            ["kind"] = f.Kind.ToString(),
            ["divisionIndex"] = f.DivisionIndex,
            ["round"] = f.Round,
            ["date"] = Date(f.Date.Year, f.Date.Month, f.Date.Day),
            ["homeClubId"] = f.HomeClubId.Value,
            ["awayClubId"] = f.AwayClubId.Value,
            ["neutralVenue"] = f.NeutralVenue,
            ["seed"] = ULong(f.Seed),
            ["played"] = f.Played,
            ["homeGoals"] = f.HomeGoals,
            ["awayGoals"] = f.AwayGoals,
            ["homePenalties"] = f.HomePenalties.HasValue ? new JValue(f.HomePenalties.Value) : JValue.CreateNull(),
            ["awayPenalties"] = f.AwayPenalties.HasValue ? new JValue(f.AwayPenalties.Value) : JValue.CreateNull(),
            ["homeYellows"] = f.HomeYellows,
            ["homeReds"] = f.HomeReds,
            ["awayYellows"] = f.AwayYellows,
            ["awayReds"] = f.AwayReds,
        };

        private static JObject WriteLedgerEntry(LedgerEntry e) => new JObject
        {
            ["id"] = e.Id.Value,
            ["clubId"] = e.ClubId.Value,
            ["date"] = Date(e.Date.Year, e.Date.Month, e.Date.Day),
            ["category"] = e.Category.ToString(),
            ["amount"] = e.Amount,
        };

        private static JObject WriteObjective(Objective o) => new JObject
        {
            ["clubId"] = o.ClubId.Value,
            ["year"] = o.Year,
            ["type"] = o.Type.ToString(),
            ["achieved"] = o.Achieved.HasValue ? new JValue(o.Achieved.Value) : JValue.CreateNull(),
        };

        private static JObject WriteSeasonSummary(SeasonSummary s) => new JObject
        {
            ["year"] = s.Year,
            ["finalTables"] = Array(s.FinalTables, ArrayId),
            ["promoted"] = ArrayId(s.Promoted),
            ["relegated"] = ArrayId(s.Relegated),
            ["cupWinner"] = s.CupWinner.Value,
            ["cupRunnerUp"] = s.CupRunnerUp.Value,
            ["cupQualified"] = ArrayId(s.CupQualified),
            ["retired"] = ArrayId(s.Retired),
            ["seasonNetByClub"] = IdMap(s.SeasonNetByClub, v => (JToken)v),
            ["dismissed"] = ArrayId(s.Dismissed),
        };

        private static JObject WriteProposal(TransferProposal p) => new JObject
        {
            ["id"] = p.Id.Value,
            ["fromClubId"] = p.FromClubId.Value,
            ["playerId"] = p.PlayerId.Value,
            ["offerAmount"] = p.OfferAmount,
            ["date"] = Date(p.Date.Year, p.Date.Month, p.Date.Day),
        };

        // ---------------- Scalar helpers ----------------

        private static JValue ULong(ulong value) => new JValue(value.ToString(CultureInfo.InvariantCulture));

        private static JValue Date(int year, int month, int day) =>
            new JValue($"{year:D4}-{month:D2}-{day:D2}");

        private static JToken DateOpt(System.DateTime? date) => date.HasValue ? Date(date.Value.Year, date.Value.Month, date.Value.Day) : JValue.CreateNull();

        private static JArray Array<T>(IEnumerable<T> items, System.Func<T, JToken> write)
        {
            var arr = new JArray();
            foreach (var item in items) arr.Add(write(item));
            return arr;
        }

        private static JArray Array(System.ReadOnlySpan<int> values)
        {
            var arr = new JArray();
            foreach (var v in values) arr.Add(v);
            return arr;
        }

        private static JArray ArrayInt(IReadOnlyList<int> values)
        {
            var arr = new JArray();
            foreach (var v in values) arr.Add(v);
            return arr;
        }

        private static JArray ArrayEnum<T>(System.ReadOnlySpan<T> values) where T : struct, System.Enum
        {
            var arr = new JArray();
            foreach (var v in values) arr.Add(v.ToString());
            return arr;
        }

        private static JArray ArrayId(IReadOnlyList<Id> ids)
        {
            var arr = new JArray();
            foreach (var id in ids) arr.Add(id.Value);
            return arr;
        }

        private static JObject IdMap<T>(IEnumerable<KeyValuePair<Id, T>> map, System.Func<T, JToken> write)
        {
            var obj = new JObject();
            foreach (var kv in map) obj[kv.Key.Value.ToString(CultureInfo.InvariantCulture)] = write(kv.Value);
            return obj;
        }
    }
}
