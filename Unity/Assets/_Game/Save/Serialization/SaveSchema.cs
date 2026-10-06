namespace Game.Save.Serialization
{
    /// <summary>Save format version (TECHNICAL_SPEC §14): bump together with a migration step and a fixture (D-17/B8, X-46).</summary>
    public static class SaveSchema
    {
        public const int CurrentVersion = 1;
    }
}
