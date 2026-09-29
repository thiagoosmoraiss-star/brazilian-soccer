namespace Game.Data.Loading
{
    /// <summary>
    /// Immutable game definitions loaded from Data/. Empty in Stage 0 (ROADMAP): content is added by the
    /// stages that need it. Carries the data-source description for diagnostics.
    /// </summary>
    public sealed class GameDatabase
    {
        public string SourceDescription { get; }

        internal GameDatabase(string sourceDescription)
        {
            SourceDescription = sourceDescription;
        }
    }
}
