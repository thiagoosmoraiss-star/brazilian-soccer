namespace Game.Data.Loading
{
    /// <summary>
    /// Immutable game definitions loaded from Data/: effect catalog (<see cref="Effects.Balance"/>),
    /// OVR weights and world-generation definitions. Other definitions are added by the stages that need them.
    /// </summary>
    public sealed class GameDatabase
    {
        public string SourceDescription { get; }
        public Effects.Balance Balance { get; }
        public Ovr.OvrDefinition Ovr { get; }
        public World.WorldDefinition World { get; }

        internal GameDatabase(string sourceDescription, Effects.Balance balance, Ovr.OvrDefinition ovr, World.WorldDefinition world)
        {
            SourceDescription = sourceDescription;
            Balance = balance;
            Ovr = ovr;
            World = world;
        }
    }
}
