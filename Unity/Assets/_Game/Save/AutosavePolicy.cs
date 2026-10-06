using Game.Career.Season;

namespace Game.Save
{
    /// <summary>
    /// When to autosave (TECHNICAL_SPEC §14: "Após data com partida, transferência, fim de temporada, ida ao
    /// segundo plano"). Pure decision helper: whoever drives the calendar loop (today the CareerSim tool; later
    /// the App layer, Track C) calls <see cref="SaveService.Save"/> when <see cref="ShouldAutosave"/> is true for
    /// the entry just processed. "Ida ao segundo plano" is a platform lifecycle event with no pure-zone
    /// equivalent: the App layer calls Save directly from Unity's <c>OnApplicationPause</c> when that exists.
    /// </summary>
    public static class AutosavePolicy
    {
        public static bool ShouldAutosave(CalendarEntryKind kind) =>
            kind == CalendarEntryKind.LeagueRound || kind == CalendarEntryKind.CupRound ||
            kind == CalendarEntryKind.TransferWindowOpen || kind == CalendarEntryKind.TransferWindowClose ||
            kind == CalendarEntryKind.SeasonEnd;

        /// <summary>True right after a season transition: <see cref="CareerState.Season"/> already points at the
        /// new season, so this save is the one to also keep as the season-start backup.</summary>
        public static bool IsSeasonStart(CalendarEntryKind kind) => kind == CalendarEntryKind.SeasonEnd;
    }
}
