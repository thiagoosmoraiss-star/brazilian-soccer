using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Newtonsoft.Json.Linq;

namespace Game.Save.Serialization
{
    /// <summary>
    /// Strict reader mirroring <see cref="CareerStateWriter"/>: reuses the <c>Data</c> readers' reflection-free
    /// helper (<see cref="StrictJson"/>, D-11) so every structural mistake is a recorded error with its JSON path
    /// instead of an exception. Assumes <paramref name="root"/> is already at <see cref="SaveSchema.CurrentVersion"/>
    /// (migrated by <see cref="SaveMigrations"/> beforehand).
    /// </summary>
    public static class CareerStateReader
    {
        public static Result<CareerState> Read(JObject root)
        {
            var sj = new StrictJson("save");
            sj.Keys(root, "root", "schemaVersion", "seed", "ids", "managedClubId", "proposals", "world", "season", "history");

            var state = new CareerState
            {
                Seed = ReadULong(sj, root, "seed", "root"),
                Ids = new IdAllocator(ReadIdAllocator(sj, root)),
                ManagedClubId = ReadIdOpt(sj, root["managedClubId"], "root.managedClubId"),
            };

            var proposalsArr = sj.Array(root, "proposals", "root");
            if (proposalsArr != null)
                for (int i = 0; i < proposalsArr.Count; i++)
                {
                    var p = ReadProposal(sj, sj.AsObject(proposalsArr[i], $"root.proposals[{i}]"), $"root.proposals[{i}]");
                    if (p != null) state.Proposals.Add(p);
                }

            var worldObj = sj.Object(root, "world", "root");
            var world = worldObj != null ? ReadWorld(sj, worldObj) : null;
            if (world != null) state.World = world;

            var seasonObj = sj.Object(root, "season", "root");
            if (seasonObj != null && world != null)
            {
                var season = ReadSeason(sj, seasonObj, world);
                if (season != null) state.Season = season;
            }

            var historyArr = sj.Array(root, "history", "root");
            if (historyArr != null)
                for (int i = 0; i < historyArr.Count; i++)
                {
                    var s = ReadSeasonSummary(sj, sj.AsObject(historyArr[i], $"root.history[{i}]"), $"root.history[{i}]");
                    if (s != null) state.History.Add(s);
                }

            return sj.Ok ? Result<CareerState>.Ok(state) : Result<CareerState>.Fail(sj.Errors);
        }

        private static int ReadIdAllocator(StrictJson sj, JObject root)
        {
            var ids = sj.Object(root, "ids", "root");
            if (ids == null) return 0;
            sj.Keys(ids, "root.ids", "lastIssued");
            return sj.Int(ids, "lastIssued", "root.ids");
        }

        // ---------------- World ----------------

        private static WorldState ReadWorld(StrictJson sj, JObject obj)
        {
            sj.Keys(obj, "world", "seed", "startYear", "lastIssuedId", "divisionNames", "clubs", "players", "contracts",
                "managers", "staff", "facilityProjects");
            var world = new WorldState
            {
                Seed = ReadULong(sj, obj, "seed", "world"),
                StartYear = sj.Int(obj, "startYear", "world"),
                LastIssuedId = sj.Int(obj, "lastIssuedId", "world"),
                DivisionNames = sj.StringList(obj, "divisionNames", "world"),
            };
            var clubs = new List<Club>();
            var clubsArr = sj.Array(obj, "clubs", "world");
            if (clubsArr != null)
                for (int i = 0; i < clubsArr.Count; i++)
                {
                    var c = ReadClub(sj, sj.AsObject(clubsArr[i], $"world.clubs[{i}]"), $"world.clubs[{i}]");
                    if (c != null) clubs.Add(c);
                }
            world.Clubs = clubs;

            var playersArr = sj.Array(obj, "players", "world");
            if (playersArr != null)
                for (int i = 0; i < playersArr.Count; i++)
                {
                    var p = ReadPlayer(sj, sj.AsObject(playersArr[i], $"world.players[{i}]"), $"world.players[{i}]");
                    if (p != null) world.PlayerList.Add(p);
                }

            var contractsArr = sj.Array(obj, "contracts", "world");
            if (contractsArr != null)
                for (int i = 0; i < contractsArr.Count; i++)
                {
                    var c = ReadContract(sj, sj.AsObject(contractsArr[i], $"world.contracts[{i}]"), $"world.contracts[{i}]");
                    if (c != null) world.ContractList.Add(c);
                }

            var managersArr = sj.Array(obj, "managers", "world");
            if (managersArr != null)
                for (int i = 0; i < managersArr.Count; i++)
                {
                    var m = ReadManager(sj, sj.AsObject(managersArr[i], $"world.managers[{i}]"), $"world.managers[{i}]");
                    if (m != null) world.ManagerList.Add(m);
                }

            var staffArr = sj.Array(obj, "staff", "world");
            if (staffArr != null)
                for (int i = 0; i < staffArr.Count; i++)
                {
                    var s = ReadStaff(sj, sj.AsObject(staffArr[i], $"world.staff[{i}]"), $"world.staff[{i}]");
                    if (s != null) world.StaffList.Add(s);
                }

            var projectsArr = sj.Array(obj, "facilityProjects", "world");
            if (projectsArr != null)
                for (int i = 0; i < projectsArr.Count; i++)
                {
                    var p = ReadFacilityProject(sj, sj.AsObject(projectsArr[i], $"world.facilityProjects[{i}]"), $"world.facilityProjects[{i}]");
                    if (p != null) world.FacilityProjectList.Add(p);
                }

            return world;
        }

        private static Club ReadClub(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "name", "shortName", "city", "uf", "colors", "crest", "divisionIndex", "divisionName",
                "reputation", "stars", "budget", "stadium", "fans", "trainingCenterLevel", "balance", "monthsNegativeCash",
                "cashAlert", "transferLockout");
            var colorsObj = sj.Object(o, "colors", path);
            var crestObj = sj.Object(o, "crest", path);
            var stadiumObj = sj.Object(o, "stadium", path);
            if (colorsObj != null) sj.Keys(colorsObj, path + ".colors", "primaryId", "primaryHex", "secondaryId", "secondaryHex");
            if (crestObj != null) sj.Keys(crestObj, path + ".crest", "shapeId", "symbolId", "primaryColorId", "secondaryColorId");
            if (stadiumObj != null) sj.Keys(stadiumObj, path + ".stadium", "level", "capacity");
            return new Club
            {
                Id = ReadId(sj, o, "id", path),
                Name = sj.String(o, "name", path),
                ShortName = sj.String(o, "shortName", path),
                City = sj.String(o, "city", path),
                Uf = sj.String(o, "uf", path),
                Colors = colorsObj == null ? null : new ClubColors
                {
                    PrimaryId = sj.String(colorsObj, "primaryId", path + ".colors"),
                    PrimaryHex = sj.String(colorsObj, "primaryHex", path + ".colors"),
                    SecondaryId = sj.String(colorsObj, "secondaryId", path + ".colors"),
                    SecondaryHex = sj.String(colorsObj, "secondaryHex", path + ".colors"),
                },
                Crest = crestObj == null ? null : new Crest
                {
                    ShapeId = sj.String(crestObj, "shapeId", path + ".crest"),
                    SymbolId = sj.String(crestObj, "symbolId", path + ".crest"),
                    PrimaryColorId = sj.String(crestObj, "primaryColorId", path + ".crest"),
                    SecondaryColorId = sj.String(crestObj, "secondaryColorId", path + ".crest"),
                },
                DivisionIndex = sj.Int(o, "divisionIndex", path),
                DivisionName = sj.String(o, "divisionName", path),
                Reputation = sj.Int(o, "reputation", path),
                Stars = sj.Int(o, "stars", path),
                Budget = sj.Long(o, "budget", path),
                Stadium = stadiumObj == null ? null : new Stadium
                {
                    Level = sj.Int(stadiumObj, "level", path + ".stadium"),
                    Capacity = sj.Int(stadiumObj, "capacity", path + ".stadium"),
                },
                Fans = sj.Int(o, "fans", path),
                TrainingCenterLevel = sj.Int(o, "trainingCenterLevel", path),
                Balance = sj.Long(o, "balance", path),
                MonthsNegativeCash = sj.Int(o, "monthsNegativeCash", path),
                CashAlert = sj.Bool(o, "cashAlert", path),
                TransferLockout = sj.Bool(o, "transferLockout", path),
            };
        }

        private static Player ReadPlayer(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "firstName", "lastName", "nationality", "birthDate", "heightCm", "preferredFoot",
                "weakFoot", "avatarSeed", "attributes", "potential", "mainPosition", "secondaryPositions", "condition");
            var birth = ReadDate(sj, o, "birthDate", path);
            var player = new Player
            {
                Id = ReadId(sj, o, "id", path),
                FirstName = sj.String(o, "firstName", path),
                LastName = sj.String(o, "lastName", path),
                Nationality = sj.String(o, "nationality", path),
                BirthDate = new BirthDate(birth.Year, birth.Month, birth.Day),
                HeightCm = sj.Int(o, "heightCm", path),
                PreferredFoot = ReadEnum<Foot>(sj, o, "preferredFoot", path),
                WeakFoot = sj.Int(o, "weakFoot", path),
                AvatarSeed = ReadULong(sj, o, "avatarSeed", path),
                Potential = sj.Int(o, "potential", path),
                MainPosition = ReadEnum<Position>(sj, o, "mainPosition", path),
            };
            player.AttributeValues = sj.IntList(o, "attributes", path).ToArray();
            if (player.AttributeValues.Length != AttrInfo.Count)
                sj.Fail(StrictJson.InvalidStructure, $"{path}.attributes: expected {AttrInfo.Count} values, got {player.AttributeValues.Length}.");
            var secondary = new List<Position>();
            var secArr = sj.Array(o, "secondaryPositions", path);
            if (secArr != null)
                for (int i = 0; i < secArr.Count; i++)
                    if (sj.TryEnum(sj.AsString(secArr[i], $"{path}.secondaryPositions[{i}]"), $"{path}.secondaryPositions[{i}]", out Position pos))
                        secondary.Add(pos);
            player.SecondaryPositionValues = secondary.ToArray();

            var condObj = sj.Object(o, "condition", path);
            if (condObj != null) ReadCondition(sj, condObj, path + ".condition", player.Condition);
            return player;
        }

        private static void ReadCondition(StrictJson sj, JObject o, string path, PlayerCondition cond)
        {
            sj.Keys(o, path, "energy", "energyDate", "morale", "recentRatings", "injury", "injuryMatchesLeft",
                "injuredUntil", "yellowCount", "suspendedMatchCount", "seasonAppearances", "seasonMinutes",
                "seasonGoals", "seasonRatingSum", "previousMinutesShare", "developmentProgress");
            cond.Energy = sj.Float(o, "energy", path);
            cond.EnergyDate = ReadDateOpt(sj, o["energyDate"], path + ".energyDate");
            cond.Morale = sj.Int(o, "morale", path);
            foreach (var r in sj.FloatList(o, "recentRatings", path)) cond.RecentRatingValues.Add(r);
            cond.Injury = ReadEnum<Game.Core.Contracts.Match.InjurySeverity>(sj, o, "injury", path);
            cond.InjuryMatchesLeft = sj.Int(o, "injuryMatchesLeft", path);
            cond.InjuredUntil = ReadDateOpt(sj, o["injuredUntil"], path + ".injuredUntil");
            CopyFixed(sj, sj.IntList(o, "yellowCount", path), cond.YellowCount, path + ".yellowCount");
            CopyFixed(sj, sj.IntList(o, "suspendedMatchCount", path), cond.SuspendedMatchCount, path + ".suspendedMatchCount");
            cond.SeasonAppearances = sj.Int(o, "seasonAppearances", path);
            cond.SeasonMinutes = sj.Int(o, "seasonMinutes", path);
            cond.SeasonGoals = sj.Int(o, "seasonGoals", path);
            cond.SeasonRatingSum = sj.Float(o, "seasonRatingSum", path);
            cond.PreviousMinutesShare = sj.Float(o, "previousMinutesShare", path);
            cond.DevelopmentProgress = sj.Float(o, "developmentProgress", path);
        }

        private static void CopyFixed(StrictJson sj, List<int> values, int[] target, string path)
        {
            if (values.Count != target.Length)
            {
                sj.Fail(StrictJson.InvalidStructure, $"{path}: expected {target.Length} values, got {values.Count}.");
                return;
            }
            for (int i = 0; i < target.Length; i++) target[i] = values[i];
        }

        private static Contract ReadContract(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "playerId", "clubId", "wage", "startYear", "endYear");
            return new Contract
            {
                Id = ReadId(sj, o, "id", path),
                PlayerId = ReadId(sj, o, "playerId", path),
                ClubId = ReadId(sj, o, "clubId", path),
                Wage = sj.Long(o, "wage", path),
                StartYear = sj.Int(o, "startYear", path),
                EndYear = sj.Int(o, "endYear", path),
            };
        }

        private static Manager ReadManager(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "clubId", "confidence", "dismissals");
            return new Manager
            {
                Id = ReadId(sj, o, "id", path),
                ClubId = ReadId(sj, o, "clubId", path),
                Confidence = sj.Int(o, "confidence", path),
                Dismissals = sj.Int(o, "dismissals", path),
            };
        }

        private static StaffMember ReadStaff(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "clubId", "role", "level");
            return new StaffMember
            {
                Id = ReadId(sj, o, "id", path),
                ClubId = ReadId(sj, o, "clubId", path),
                Role = ReadEnum<StaffRole>(sj, o, "role", path),
                Level = sj.Int(o, "level", path),
            };
        }

        private static FacilityProject ReadFacilityProject(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "clubId", "type", "targetLevel", "completesOn");
            var completes = ReadDate(sj, o, "completesOn", path);
            return new FacilityProject
            {
                Id = ReadId(sj, o, "id", path),
                ClubId = ReadId(sj, o, "clubId", path),
                Type = ReadEnum<FacilityType>(sj, o, "type", path),
                TargetLevel = sj.Int(o, "targetLevel", path),
                CompletesOn = new DateTime(completes.Year, completes.Month, completes.Day),
            };
        }

        // ---------------- Season ----------------

        private static Season ReadSeason(StrictJson sj, JObject o, WorldState world)
        {
            sj.Keys(o, "season", "year", "calendar", "nextEntry", "leagues", "cup", "clubFormations", "clubMatches",
                "ledger", "objectives");
            var season = new Season
            {
                Year = sj.Int(o, "year", "season"),
                NextEntry = sj.Int(o, "nextEntry", "season"),
            };
            var calendar = new List<CalendarEntry>();
            var calArr = sj.Array(o, "calendar", "season");
            if (calArr != null)
                for (int i = 0; i < calArr.Count; i++)
                {
                    var e = ReadCalendarEntry(sj, sj.AsObject(calArr[i], $"season.calendar[{i}]"), $"season.calendar[{i}]");
                    if (e != null) calendar.Add(e);
                }
            season.Calendar = calendar;

            var leagues = new List<LeagueEdition>();
            var leaguesArr = sj.Array(o, "leagues", "season");
            if (leaguesArr != null)
                for (int i = 0; i < leaguesArr.Count; i++)
                {
                    var l = ReadLeagueEdition(sj, sj.AsObject(leaguesArr[i], $"season.leagues[{i}]"), $"season.leagues[{i}]");
                    if (l != null) leagues.Add(l);
                }
            season.Leagues = leagues;

            var cupObj = sj.Object(o, "cup", "season");
            if (cupObj != null) season.Cup = ReadCupEdition(sj, cupObj, "season.cup");

            var formations = new Dictionary<Id, string>();
            var formationsObj = sj.Object(o, "clubFormations", "season");
            if (formationsObj != null)
                foreach (var prop in formationsObj.Properties())
                    formations[ReadIdKey(sj, prop.Name, "season.clubFormations")] = sj.AsString(prop.Value, $"season.clubFormations.{prop.Name}");
            season.ClubFormations = formations;

            var matchesObj = sj.Object(o, "clubMatches", "season");
            if (matchesObj != null)
                foreach (var prop in matchesObj.Properties())
                    season.ClubMatches[ReadIdKey(sj, prop.Name, "season.clubMatches")] = sj.AsInt(prop.Value, $"season.clubMatches.{prop.Name}");

            var ledgerArr = sj.Array(o, "ledger", "season");
            if (ledgerArr != null)
                for (int i = 0; i < ledgerArr.Count; i++)
                {
                    var e = ReadLedgerEntry(sj, sj.AsObject(ledgerArr[i], $"season.ledger[{i}]"), $"season.ledger[{i}]");
                    if (e != null) season.Ledger.Add(e);
                }

            var objectivesObj = sj.Object(o, "objectives", "season");
            if (objectivesObj != null)
                foreach (var prop in objectivesObj.Properties())
                {
                    var obj = ReadObjective(sj, sj.AsObject(prop.Value, $"season.objectives.{prop.Name}"), $"season.objectives.{prop.Name}");
                    if (obj != null) season.Objectives[ReadIdKey(sj, prop.Name, "season.objectives")] = obj;
                }

            return season;
        }

        private static CalendarEntry ReadCalendarEntry(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "date", "kind", "round");
            var date = ReadDate(sj, o, "date", path);
            return new CalendarEntry
            {
                Date = new DateTime(date.Year, date.Month, date.Day),
                Kind = ReadEnum<CalendarEntryKind>(sj, o, "kind", path),
                Round = sj.Int(o, "round", path),
            };
        }

        private static LeagueEdition ReadLeagueEdition(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "divisionIndex", "divisionName", "clubIds", "fixtures", "tiebreakSeed");
            var edition = new LeagueEdition
            {
                DivisionIndex = sj.Int(o, "divisionIndex", path),
                DivisionName = sj.String(o, "divisionName", path),
                ClubIds = ReadIdList(sj, o, "clubIds", path),
                TiebreakSeed = ReadULong(sj, o, "tiebreakSeed", path),
            };
            var fixturesArr = sj.Array(o, "fixtures", path);
            if (fixturesArr != null)
                for (int i = 0; i < fixturesArr.Count; i++)
                {
                    var f = ReadFixture(sj, sj.AsObject(fixturesArr[i], $"{path}.fixtures[{i}]"), $"{path}.fixtures[{i}]");
                    if (f != null) edition.Fixtures.Add(f);
                }
            return edition;
        }

        private static CupEdition ReadCupEdition(StrictJson sj, JObject o, string path)
        {
            sj.Keys(o, path, "qualified", "rounds", "roundCount", "winner", "runnerUp");
            var cup = new CupEdition
            {
                Qualified = ReadIdList(sj, o, "qualified", path),
                RoundCount = sj.Int(o, "roundCount", path),
                Winner = ReadId(sj, o, "winner", path),
                RunnerUp = ReadId(sj, o, "runnerUp", path),
            };
            var roundsArr = sj.Array(o, "rounds", path);
            if (roundsArr != null)
                for (int i = 0; i < roundsArr.Count; i++)
                {
                    var roundArr = sj.AsArray(roundsArr[i], $"{path}.rounds[{i}]");
                    var round = new List<Fixture>();
                    if (roundArr != null)
                        for (int j = 0; j < roundArr.Count; j++)
                        {
                            var f = ReadFixture(sj, sj.AsObject(roundArr[j], $"{path}.rounds[{i}][{j}]"), $"{path}.rounds[{i}][{j}]");
                            if (f != null) round.Add(f);
                        }
                    cup.Rounds.Add(round);
                }
            return cup;
        }

        private static Fixture ReadFixture(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "kind", "divisionIndex", "round", "date", "homeClubId", "awayClubId", "neutralVenue",
                "seed", "played", "homeGoals", "awayGoals", "homePenalties", "awayPenalties", "homeYellows", "homeReds",
                "awayYellows", "awayReds");
            var date = ReadDate(sj, o, "date", path);
            return new Fixture
            {
                Id = ReadId(sj, o, "id", path),
                Kind = ReadEnum<CompetitionKind>(sj, o, "kind", path),
                DivisionIndex = sj.Int(o, "divisionIndex", path),
                Round = sj.Int(o, "round", path),
                Date = new DateTime(date.Year, date.Month, date.Day),
                HomeClubId = ReadId(sj, o, "homeClubId", path),
                AwayClubId = ReadId(sj, o, "awayClubId", path),
                NeutralVenue = sj.Bool(o, "neutralVenue", path),
                Seed = ReadULong(sj, o, "seed", path),
                Played = sj.Bool(o, "played", path),
                HomeGoals = sj.Int(o, "homeGoals", path),
                AwayGoals = sj.Int(o, "awayGoals", path),
                HomePenalties = ReadIntOpt(sj, o["homePenalties"], path + ".homePenalties"),
                AwayPenalties = ReadIntOpt(sj, o["awayPenalties"], path + ".awayPenalties"),
                HomeYellows = sj.Int(o, "homeYellows", path),
                HomeReds = sj.Int(o, "homeReds", path),
                AwayYellows = sj.Int(o, "awayYellows", path),
                AwayReds = sj.Int(o, "awayReds", path),
            };
        }

        private static LedgerEntry ReadLedgerEntry(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "clubId", "date", "category", "amount");
            var date = ReadDate(sj, o, "date", path);
            return new LedgerEntry
            {
                Id = ReadId(sj, o, "id", path),
                ClubId = ReadId(sj, o, "clubId", path),
                Date = new DateTime(date.Year, date.Month, date.Day),
                Category = ReadEnum<LedgerCategory>(sj, o, "category", path),
                Amount = sj.Long(o, "amount", path),
            };
        }

        private static Objective ReadObjective(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "clubId", "year", "type", "achieved");
            return new Objective
            {
                ClubId = ReadId(sj, o, "clubId", path),
                Year = sj.Int(o, "year", path),
                Type = ReadEnum<ObjectiveType>(sj, o, "type", path),
                Achieved = ReadBoolOpt(sj, o["achieved"], path + ".achieved"),
            };
        }

        private static SeasonSummary ReadSeasonSummary(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "year", "finalTables", "promoted", "relegated", "cupWinner", "cupRunnerUp", "cupQualified",
                "retired", "seasonNetByClub", "dismissed");
            var tables = new List<IReadOnlyList<Id>>();
            var tablesArr = sj.Array(o, "finalTables", path);
            if (tablesArr != null)
                for (int i = 0; i < tablesArr.Count; i++)
                    tables.Add(ReadIdArray(sj, tablesArr[i], $"{path}.finalTables[{i}]"));
            var seasonNet = new Dictionary<Id, long>();
            var netObj = sj.Object(o, "seasonNetByClub", path);
            if (netObj != null)
                foreach (var prop in netObj.Properties())
                    seasonNet[ReadIdKey(sj, prop.Name, path + ".seasonNetByClub")] = sj.AsLong(prop.Value, $"{path}.seasonNetByClub.{prop.Name}");
            return new SeasonSummary
            {
                Year = sj.Int(o, "year", path),
                FinalTables = tables,
                Promoted = ReadIdList(sj, o, "promoted", path),
                Relegated = ReadIdList(sj, o, "relegated", path),
                CupWinner = ReadId(sj, o, "cupWinner", path),
                CupRunnerUp = ReadId(sj, o, "cupRunnerUp", path),
                CupQualified = ReadIdList(sj, o, "cupQualified", path),
                Retired = ReadIdList(sj, o, "retired", path),
                SeasonNetByClub = seasonNet,
                Dismissed = ReadIdList(sj, o, "dismissed", path),
            };
        }

        private static TransferProposal ReadProposal(StrictJson sj, JObject o, string path)
        {
            if (o == null) return null;
            sj.Keys(o, path, "id", "fromClubId", "playerId", "offerAmount", "date");
            var date = ReadDate(sj, o, "date", path);
            return new TransferProposal
            {
                Id = ReadId(sj, o, "id", path),
                FromClubId = ReadId(sj, o, "fromClubId", path),
                PlayerId = ReadId(sj, o, "playerId", path),
                OfferAmount = sj.Long(o, "offerAmount", path),
                Date = new DateTime(date.Year, date.Month, date.Day),
            };
        }

        // ---------------- Scalar helpers ----------------

        private static Id ReadId(StrictJson sj, JToken parent, string name, string path) => new Id(Math.Max(0, sj.Int(parent, name, path)));

        private static Id ReadIdKey(StrictJson sj, string key, string path)
        {
            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 0) return new Id(value);
            sj.Fail(StrictJson.InvalidStructure, $"{path}: '{key}' is not a valid Id key.");
            return Id.None;
        }

        private static Id? ReadIdOpt(StrictJson sj, JToken token, string path)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return new Id(Math.Max(0, sj.AsInt(token, path)));
        }

        private static List<Id> ReadIdList(StrictJson sj, JToken parent, string name, string path)
        {
            var result = new List<Id>();
            var arr = sj.Array(parent, name, path);
            if (arr == null) return result;
            for (int i = 0; i < arr.Count; i++) result.Add(new Id(Math.Max(0, sj.AsInt(arr[i], $"{path}.{name}[{i}]"))));
            return result;
        }

        private static List<Id> ReadIdArray(StrictJson sj, JToken token, string path)
        {
            var result = new List<Id>();
            if (!(token is JArray arr)) { sj.Fail(StrictJson.InvalidStructure, path + " must be an array."); return result; }
            for (int i = 0; i < arr.Count; i++) result.Add(new Id(Math.Max(0, sj.AsInt(arr[i], $"{path}[{i}]"))));
            return result;
        }

        private static ulong ReadULong(StrictJson sj, JToken parent, string name, string path)
        {
            string text = sj.String(parent, name, path);
            if (text != null && ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value)) return value;
            if (text != null) sj.Fail(StrictJson.InvalidStructure, $"{path}.{name}: '{text}' is not a valid unsigned 64-bit integer.");
            return 0;
        }

        private static (int Year, int Month, int Day) ReadDate(StrictJson sj, JToken parent, string name, string path) =>
            ParseDate(sj, sj.String(parent, name, path), $"{path}.{name}");

        private static DateTime? ReadDateOpt(StrictJson sj, JToken token, string path)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            var d = ParseDate(sj, sj.AsString(token, path), path);
            return new DateTime(d.Year, d.Month, d.Day);
        }

        private static (int Year, int Month, int Day) ParseDate(StrictJson sj, string text, string path)
        {
            if (text != null && DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return (d.Year, d.Month, d.Day);
            if (text != null) sj.Fail(StrictJson.InvalidStructure, $"{path}: '{text}' is not a yyyy-MM-dd date.");
            return (1, 1, 1);
        }

        private static int? ReadIntOpt(StrictJson sj, JToken token, string path)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return sj.AsInt(token, path);
        }

        private static bool? ReadBoolOpt(StrictJson sj, JToken token, string path)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            sj.Fail(StrictJson.InvalidStructure, path + " must be a boolean.");
            return null;
        }

        private static T ReadEnum<T>(StrictJson sj, JToken parent, string name, string path) where T : struct, Enum
        {
            sj.TryEnum(sj.String(parent, name, path), $"{path}.{name}", out T value);
            return value;
        }
    }
}
