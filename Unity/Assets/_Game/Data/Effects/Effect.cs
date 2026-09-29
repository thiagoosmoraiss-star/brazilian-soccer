namespace Game.Data.Effects
{
    /// <summary>
    /// Catalog of gameplay effects evaluated through <see cref="Balance.Eval"/>.
    /// Intentionally empty in Stage 0: no system consumes effects yet, and the production curves are
    /// not defined in the documentation (see DECISIONS.md D-20). Members are added together with their
    /// definition in Data/Balance/effects.json; validation fails if a member has no definition.
    /// </summary>
    public enum Effect
    {
    }
}
