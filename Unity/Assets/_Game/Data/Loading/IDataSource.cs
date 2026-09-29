namespace Game.Data.Loading
{
    /// <summary>
    /// Read-only access to the game data files (the repository-root Data/ folder, D-09).
    /// Paths are relative to the Data root and always use '/' separators (e.g. "Balance/effects.json").
    /// </summary>
    public interface IDataSource
    {
        /// <summary>Human-readable location, for diagnostics.</summary>
        string Description { get; }

        bool Exists(string relativePath);

        string ReadAllText(string relativePath);
    }
}
