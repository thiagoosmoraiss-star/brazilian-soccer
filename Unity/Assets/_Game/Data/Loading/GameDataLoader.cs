using System;
using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Competitions;
using Game.Data.Effects;
using Game.Data.Match;
using Game.Data.Ovr;
using Game.Data.World;

namespace Game.Data.Loading
{
    /// <summary>
    /// Loads the <see cref="GameDatabase"/> from an <see cref="IDataSource"/>:
    /// JSON files -> deserialization (Newtonsoft, D-11) -> definitions -> validators -> GameDatabase
    /// (effects -> BalanceValidator -> Balance; ovr.json; Data/World/*.json -> WorldDefinitionValidator).
    /// </summary>
    public static class GameDataLoader
    {
        public const string EffectsFile = "Balance/effects.json";
        public const string OvrFile = "Balance/ovr.json";
        public const string NamesFile = "World/names.json";
        public const string CitiesFile = "World/cities.json";
        public const string ClubTemplatesFile = "World/club_templates.json";
        public const string CrestTemplatesFile = "World/crest_templates.json";
        public const string GenerationFile = "World/generation.json";
        public const string FormationsFile = "Formations/formations.json";
        public const string MatchRulesFile = "Balance/match_rules.json";
        public const string QuickSimFile = "Balance/quicksim.json";
        public const string CompetitionsFile = "Competitions/competitions.json";
        public const string CalendarFile = "Competitions/calendar.json";
        public const string DevelopmentFile = "Career/development.json";
        public const string MarketFile = "Career/market.json";
        public const string EconomyFile = "Career/economy.json";
        public const string BoardFile = "Board/board.json";
        public const string FacilitiesFile = "Career/facilities.json";
        public const string StaffFile = "Career/staff.json";
        public const string BallFile = "Balance/ball.json";
        public const string MovementFile = "Balance/movement.json";
        public const string FatigueFile = "Balance/fatigue.json";
        public const string KickingFile = "Balance/kicking.json";
        public const string AiFile = "Balance/ai.json";
        public const string TacticsFile = "Balance/tactics.json";
        public const string DefenseFile = "Balance/defense.json";
        public const string GoalkeeperFile = "Balance/goalkeeper.json";
        public const string RestartsFile = "Balance/restarts.json";
        /// <summary>Vertical-slice teams (A7a): demo/test data, not part of the career database.</summary>
        public const string VerticalSliceTeamsFile = "VerticalSlice/teams.json";
        /// <summary>Match-scene presentation tunables (A7b): camera, radar, placeholder animation, HUD.</summary>
        public const string MatchViewFile = "Presentation/match_view.json";

        public const string MissingFile = "MISSING_FILE";
        public const string ReadFailed = "READ_FAILED";

        public static readonly IReadOnlyList<string> RequiredFiles = new[]
        {
            EffectsFile, OvrFile, NamesFile, CitiesFile, ClubTemplatesFile, CrestTemplatesFile, GenerationFile,
            FormationsFile, MatchRulesFile, QuickSimFile, CompetitionsFile, CalendarFile, DevelopmentFile, MarketFile, EconomyFile,
            BoardFile, FacilitiesFile, StaffFile, BallFile, MovementFile, FatigueFile, KickingFile, AiFile, TacticsFile, DefenseFile, GoalkeeperFile, RestartsFile,
        };

        /// <summary>Reads and deserializes the effect definitions (syntax and structure only).</summary>
        public static Result<IReadOnlyList<EffectDefinition>> LoadEffectDefinitions(IDataSource source)
        {
            var text = ReadText(source, EffectsFile);
            return text.IsSuccess ? EffectsJsonReader.Read(text.Value) : Result<IReadOnlyList<EffectDefinition>>.Fail(text.Errors);
        }

        /// <summary>Reads and validates Data/Balance/ovr.json.</summary>
        public static Result<OvrDefinition> LoadOvr(IDataSource source)
        {
            var text = ReadText(source, OvrFile);
            return text.IsSuccess ? OvrJsonReader.Read(text.Value) : Result<OvrDefinition>.Fail(text.Errors);
        }

        /// <summary>Reads Data/World/*.json and runs <see cref="WorldDefinitionValidator"/>.</summary>
        public static Result<WorldDefinition> LoadWorld(IDataSource source)
        {
            var files = new[] { NamesFile, CitiesFile, ClubTemplatesFile, CrestTemplatesFile, GenerationFile };
            var texts = new string[files.Length];
            var errors = new List<Error>();
            for (int i = 0; i < files.Length; i++)
            {
                var t = ReadText(source, files[i]);
                if (t.IsSuccess) texts[i] = t.Value; else errors.AddRange(t.Errors);
            }
            if (errors.Count > 0) return Result<WorldDefinition>.Fail(errors);

            var world = WorldJsonReader.Read(texts[0], texts[1], texts[2], texts[3], texts[4]);
            if (!world.IsSuccess) return world;
            var semantic = WorldDefinitionValidator.Validate(world.Value);
            return semantic.Count > 0 ? Result<WorldDefinition>.Fail(semantic) : world;
        }

        public static Result<IReadOnlyList<FormationDefinition>> LoadFormations(IDataSource source)
        {
            var text = ReadText(source, FormationsFile);
            return text.IsSuccess ? MatchDataReaders.ReadFormations(text.Value) : Result<IReadOnlyList<FormationDefinition>>.Fail(text.Errors);
        }

        public static Result<MatchRulesDefinition> LoadMatchRules(IDataSource source)
        {
            var text = ReadText(source, MatchRulesFile);
            return text.IsSuccess ? MatchDataReaders.ReadMatchRules(text.Value) : Result<MatchRulesDefinition>.Fail(text.Errors);
        }

        public static Result<QuickSimDefinition> LoadQuickSim(IDataSource source)
        {
            var text = ReadText(source, QuickSimFile);
            return text.IsSuccess ? MatchDataReaders.ReadQuickSim(text.Value) : Result<QuickSimDefinition>.Fail(text.Errors);
        }

        public static Result<CompetitionsDefinition> LoadCompetitions(IDataSource source)
        {
            var text = ReadText(source, CompetitionsFile);
            return text.IsSuccess ? CompetitionReaders.ReadCompetitions(text.Value) : Result<CompetitionsDefinition>.Fail(text.Errors);
        }

        public static Result<CalendarDefinition> LoadCalendar(IDataSource source)
        {
            var text = ReadText(source, CalendarFile);
            return text.IsSuccess ? CompetitionReaders.ReadCalendar(text.Value) : Result<CalendarDefinition>.Fail(text.Errors);
        }

        public static Result<Career.DevelopmentDefinition> LoadDevelopment(IDataSource source)
        {
            var text = ReadText(source, DevelopmentFile);
            return text.IsSuccess ? DevelopmentReader.Read(text.Value) : Result<Career.DevelopmentDefinition>.Fail(text.Errors);
        }

        public static Result<Career.MarketDefinition> LoadMarket(IDataSource source)
        {
            var text = ReadText(source, MarketFile);
            return text.IsSuccess ? MarketReader.Read(text.Value) : Result<Career.MarketDefinition>.Fail(text.Errors);
        }

        public static Result<Career.EconomyDefinition> LoadEconomy(IDataSource source)
        {
            var text = ReadText(source, EconomyFile);
            return text.IsSuccess ? EconomyReader.Read(text.Value) : Result<Career.EconomyDefinition>.Fail(text.Errors);
        }

        public static Result<Board.BoardDefinition> LoadBoard(IDataSource source)
        {
            var text = ReadText(source, BoardFile);
            return text.IsSuccess ? BoardReader.Read(text.Value) : Result<Board.BoardDefinition>.Fail(text.Errors);
        }

        public static Result<Career.FacilitiesDefinition> LoadFacilities(IDataSource source)
        {
            var text = ReadText(source, FacilitiesFile);
            return text.IsSuccess ? FacilitiesReader.Read(text.Value) : Result<Career.FacilitiesDefinition>.Fail(text.Errors);
        }

        public static Result<Career.StaffDefinition> LoadStaff(IDataSource source)
        {
            var text = ReadText(source, StaffFile);
            return text.IsSuccess ? StaffReader.Read(text.Value) : Result<Career.StaffDefinition>.Fail(text.Errors);
        }

        public static Result<BallDefinition> LoadBall(IDataSource source)
        {
            var text = ReadText(source, BallFile);
            return text.IsSuccess ? BallReader.Read(text.Value) : Result<BallDefinition>.Fail(text.Errors);
        }

        public static Result<MovementDefinition> LoadMovement(IDataSource source)
        {
            var text = ReadText(source, MovementFile);
            return text.IsSuccess ? MovementReader.ReadMovement(text.Value) : Result<MovementDefinition>.Fail(text.Errors);
        }

        public static Result<FatigueDefinition> LoadFatigue(IDataSource source)
        {
            var text = ReadText(source, FatigueFile);
            return text.IsSuccess ? MovementReader.ReadFatigue(text.Value) : Result<FatigueDefinition>.Fail(text.Errors);
        }

        public static Result<KickingDefinition> LoadKicking(IDataSource source)
        {
            var text = ReadText(source, KickingFile);
            return text.IsSuccess ? KickingReader.Read(text.Value) : Result<KickingDefinition>.Fail(text.Errors);
        }

        public static Result<AiDefinition> LoadAi(IDataSource source)
        {
            var text = ReadText(source, AiFile);
            return text.IsSuccess ? AiReader.ReadAi(text.Value) : Result<AiDefinition>.Fail(text.Errors);
        }

        public static Result<TacticsDefinition> LoadTactics(IDataSource source)
        {
            var text = ReadText(source, TacticsFile);
            return text.IsSuccess ? AiReader.ReadTactics(text.Value) : Result<TacticsDefinition>.Fail(text.Errors);
        }

        public static Result<DefenseDefinition> LoadDefense(IDataSource source)
        {
            var text = ReadText(source, DefenseFile);
            return text.IsSuccess ? DefenseReader.Read(text.Value) : Result<DefenseDefinition>.Fail(text.Errors);
        }

        public static Result<GoalkeeperDefinition> LoadGoalkeeper(IDataSource source)
        {
            var text = ReadText(source, GoalkeeperFile);
            return text.IsSuccess ? GoalkeeperReader.Read(text.Value) : Result<GoalkeeperDefinition>.Fail(text.Errors);
        }

        public static Result<RestartsDefinition> LoadRestarts(IDataSource source)
        {
            var text = ReadText(source, RestartsFile);
            return text.IsSuccess ? RestartsReader.Read(text.Value) : Result<RestartsDefinition>.Fail(text.Errors);
        }

        /// <summary>The vertical-slice teams, checked against the loaded formations.</summary>
        public static Result<VerticalSliceDefinition> LoadVerticalSlice(IDataSource source, GameDatabase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            var text = ReadText(source, VerticalSliceTeamsFile);
            return text.IsSuccess ? VerticalSliceReader.Read(text.Value, db) : Result<VerticalSliceDefinition>.Fail(text.Errors);
        }

        public static Result<Presentation.MatchViewDefinition> LoadMatchView(IDataSource source)
        {
            var text = ReadText(source, MatchViewFile);
            return text.IsSuccess ? MatchViewReader.Read(text.Value) : Result<Presentation.MatchViewDefinition>.Fail(text.Errors);
        }

        private static Result<string> ReadText(IDataSource source, string file)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.Exists(file))
                return Result<string>.Fail(MissingFile, $"'{file}' not found in {source.Description}.");
            try
            {
                return Result<string>.Ok(source.ReadAllText(file));
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                return Result<string>.Fail(ReadFailed, $"'{file}': {e.Message}");
            }
        }

        /// <summary>Full pipeline against the game's <see cref="Effect"/> catalog.</summary>
        public static Result<GameDatabase> Load(IDataSource source)
        {
            var catalog = LoadBalanceCatalog(source, EffectSchema.FromEffectEnum());
            var ovr = LoadOvr(source);
            var world = LoadWorld(source);
            var formations = LoadFormations(source);
            var matchRules = LoadMatchRules(source);
            var quickSim = LoadQuickSim(source);
            var competitions = LoadCompetitions(source);
            var calendar = LoadCalendar(source);
            var development = LoadDevelopment(source);
            var market = LoadMarket(source);
            var economy = LoadEconomy(source);
            var board = LoadBoard(source);
            var facilities = LoadFacilities(source);
            var staff = LoadStaff(source);
            var ball = LoadBall(source);
            var movement = LoadMovement(source);
            var fatigue = LoadFatigue(source);
            var kicking = LoadKicking(source);
            var ai = LoadAi(source);
            var tactics = LoadTactics(source);
            var defense = LoadDefense(source);
            var goalkeeper = LoadGoalkeeper(source);
            var restarts = LoadRestarts(source);

            var errors = new List<Error>();
            errors.AddRange(catalog.Errors);
            errors.AddRange(ovr.Errors);
            errors.AddRange(world.Errors);
            errors.AddRange(formations.Errors);
            errors.AddRange(matchRules.Errors);
            errors.AddRange(quickSim.Errors);
            errors.AddRange(competitions.Errors);
            errors.AddRange(calendar.Errors);
            errors.AddRange(development.Errors);
            errors.AddRange(market.Errors);
            errors.AddRange(economy.Errors);
            errors.AddRange(board.Errors);
            errors.AddRange(facilities.Errors);
            errors.AddRange(staff.Errors);
            errors.AddRange(ball.Errors);
            errors.AddRange(movement.Errors);
            errors.AddRange(fatigue.Errors);
            errors.AddRange(kicking.Errors);
            errors.AddRange(ai.Errors);
            errors.AddRange(tactics.Errors);
            errors.AddRange(defense.Errors);
            errors.AddRange(goalkeeper.Errors);
            errors.AddRange(restarts.Errors);
            if (errors.Count > 0) return Result<GameDatabase>.Fail(errors);

            return Result<GameDatabase>.Ok(new GameDatabase(source.Description, new Balance(catalog.Value), ovr.Value, world.Value,
                formations.Value, matchRules.Value, quickSim.Value, competitions.Value, calendar.Value, development.Value, market.Value,
                economy.Value, board.Value, facilities.Value, staff.Value, ball.Value, movement.Value, fatigue.Value,
                kicking.Value, ai.Value, tactics.Value, defense.Value, goalkeeper.Value, restarts.Value));
        }

        /// <summary>Deserializes and validates the effect definitions against <paramref name="schema"/>.</summary>
        public static Result<BalanceCatalog> LoadBalanceCatalog(IDataSource source, EffectSchema schema)
        {
            var definitions = LoadEffectDefinitions(source);
            return definitions.IsSuccess
                ? BalanceCatalog.Create(schema, definitions.Value)
                : Result<BalanceCatalog>.Fail(definitions.Errors);
        }
    }
}
