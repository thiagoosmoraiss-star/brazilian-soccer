using System;
using System.Collections.Generic;
using Game.Core.Results;

namespace Game.Data.Loading
{
    /// <summary>
    /// Loads the <see cref="GameDatabase"/> from an <see cref="IDataSource"/>.
    ///
    /// Stage 0 scope: verifies the required files exist and are readable as UTF-8 text.
    /// JSON parsing into definitions is NOT implemented: the JSON serializer is pending decision D-11
    /// (DECISIONS.md). The validation rules themselves live in <see cref="Effects.BalanceValidator"/> and
    /// run on the parsed model once D-11 is decided.
    /// </summary>
    public static class GameDataLoader
    {
        public const string EffectsFile = "Balance/effects.json";
        public const string EffectsSchemaFile = "Balance/effects.schema.json";

        public const string MissingFile = "MISSING_FILE";
        public const string ReadFailed = "READ_FAILED";
        public const string EmptyFile = "EMPTY_FILE";

        public static readonly IReadOnlyList<string> RequiredFiles = new[] { EffectsFile, EffectsSchemaFile };

        public static Result<GameDatabase> Load(IDataSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var errors = new List<Error>();
            foreach (var file in RequiredFiles)
            {
                if (!source.Exists(file))
                {
                    errors.Add(new Error(MissingFile, $"'{file}' not found in {source.Description}."));
                    continue;
                }

                string text;
                try
                {
                    text = source.ReadAllText(file);
                }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
                {
                    errors.Add(new Error(ReadFailed, $"'{file}': {e.Message}"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(text))
                    errors.Add(new Error(EmptyFile, $"'{file}' is empty."));
            }

            return errors.Count > 0
                ? Result<GameDatabase>.Fail(errors)
                : Result<GameDatabase>.Ok(new GameDatabase(source.Description));
        }
    }
}
