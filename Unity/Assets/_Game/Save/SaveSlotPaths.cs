namespace Game.Save
{
    /// <summary>
    /// File names for one save slot (TECHNICAL_SPEC §14: <c>slot.meta.json</c> + <c>slot.save.gz</c>). The MVP
    /// keeps a single local slot ("Não implementar: múltiplos saves além do MVP"), but the slot name is already
    /// a parameter so nothing here special-cases it.
    /// </summary>
    public static class SaveSlotPaths
    {
        public static string Meta(string slot) => slot + ".meta.json";
        public static string Body(string slot) => slot + ".save.gz";

        /// <summary>
        /// One of the 3 backups (TECHNICAL_SPEC §14): "last" and "previous" roll on every save; "season" is only
        /// replaced by a save flagged as a season start, so it always holds the state at the start of the current
        /// season — a stable rewind point the rolling pair does not give you.
        /// </summary>
        public static string MetaBackup(string slot, string suffix) => $"{slot}.{suffix}.meta.json";
        public static string BodyBackup(string slot, string suffix) => $"{slot}.{suffix}.save.gz";
    }
}
