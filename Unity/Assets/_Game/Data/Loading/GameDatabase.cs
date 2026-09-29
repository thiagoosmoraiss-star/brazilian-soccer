namespace Game.Data.Loading
{
    /// <summary>
    /// Immutable game definitions loaded from Data/. Stage 0 content: the validated effect catalog
    /// (<see cref="Effects.Balance"/>). Other definitions are added by the stages that need them.
    /// </summary>
    public sealed class GameDatabase
    {
        public string SourceDescription { get; }
        public Effects.Balance Balance { get; }

        internal GameDatabase(string sourceDescription, Effects.Balance balance)
        {
            SourceDescription = sourceDescription;
            Balance = balance;
        }
    }
}
