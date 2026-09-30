using System;
using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Effects;
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

        public const string MissingFile = "MISSING_FILE";
        public const string ReadFailed = "READ_FAILED";

        public static readonly IReadOnlyList<string> RequiredFiles = new[]
        {
            EffectsFile, OvrFile, NamesFile, CitiesFile, ClubTemplatesFile, CrestTemplatesFile, GenerationFile,
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

            var errors = new List<Error>();
            errors.AddRange(catalog.Errors);
            errors.AddRange(ovr.Errors);
            errors.AddRange(world.Errors);
            if (errors.Count > 0) return Result<GameDatabase>.Fail(errors);

            return Result<GameDatabase>.Ok(new GameDatabase(source.Description, new Balance(catalog.Value), ovr.Value, world.Value));
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
