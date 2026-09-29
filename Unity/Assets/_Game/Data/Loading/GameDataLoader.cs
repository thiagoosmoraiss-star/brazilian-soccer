using System;
using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Effects;

namespace Game.Data.Loading
{
    /// <summary>
    /// Loads the <see cref="GameDatabase"/> from an <see cref="IDataSource"/>:
    /// JSON file -> deserialization (Newtonsoft, D-11) -> EffectDefinitions -> BalanceValidator -> Balance.
    /// </summary>
    public static class GameDataLoader
    {
        public const string EffectsFile = "Balance/effects.json";

        public const string MissingFile = "MISSING_FILE";
        public const string ReadFailed = "READ_FAILED";

        public static readonly IReadOnlyList<string> RequiredFiles = new[] { EffectsFile };

        /// <summary>Reads and deserializes the effect definitions (syntax and structure only).</summary>
        public static Result<IReadOnlyList<EffectDefinition>> LoadEffectDefinitions(IDataSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.Exists(EffectsFile))
                return Result<IReadOnlyList<EffectDefinition>>.Fail(MissingFile, $"'{EffectsFile}' not found in {source.Description}.");

            string text;
            try
            {
                text = source.ReadAllText(EffectsFile);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                return Result<IReadOnlyList<EffectDefinition>>.Fail(ReadFailed, $"'{EffectsFile}': {e.Message}");
            }

            return EffectsJsonReader.Read(text);
        }

        /// <summary>Full pipeline against the game's <see cref="Effect"/> catalog.</summary>
        public static Result<GameDatabase> Load(IDataSource source)
        {
            var catalog = LoadBalanceCatalog(source, EffectSchema.FromEffectEnum());
            return catalog.IsSuccess
                ? Result<GameDatabase>.Ok(new GameDatabase(source.Description, new Balance(catalog.Value)))
                : Result<GameDatabase>.Fail(catalog.Errors);
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
