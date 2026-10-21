using System;
using System.Collections.Generic;
using Game.Career.World;
using Game.Core.Ids;
using Game.Data.Loading;

namespace Game.Career.Season
{
    /// <summary>
    /// Everything the save will persist (B8): world, current season, history, Id allocator and seed.
    /// </summary>
    public sealed class CareerState
    {
        public ulong Seed { get; internal set; }
        public WorldState World { get; internal set; }
        public Season Season { get; internal set; }
        public List<SeasonSummary> History { get; } = new List<SeasonSummary>();
        public IdAllocator Ids { get; internal set; }

        /// <summary>
        /// The human-managed club, if any (B5, X-43). The market AI never force-sells or force-buys for it;
        /// instead, incoming offers for its players are queued in <see cref="Proposals"/>. Null = every club is AI-run
        /// (every headless test and tool uses this).
        /// </summary>
        public Id? ManagedClubId { get; set; }

        /// <summary>The managed club's match that was being played when the career was saved (C1: "retomar partida após
        /// fechar o app"), or null. The match is rebuilt from the same fixture and restored from the snapshot.</summary>
        public MatchInProgress MatchInProgress { get; set; }

        /// <summary>The user's tactic and (optionally) lineup for the managed club (C3b, X-97), or null: the same automatic
        /// choices as the AI clubs.</summary>
        public UserTeamPlan UserPlan { get; set; }

        /// <summary>The match difficulty level (F1c, D-23c: chosen in the new career, changeable in Settings at any time), or
        /// null: the data's default level. Acts only on the managed club's matches (D-23b, D-23d).</summary>
        public string Difficulty { get; set; }

        /// <summary>Offers from AI clubs for a player of the managed club, awaiting a human decision (B5).</summary>
        public List<TransferProposal> Proposals { get; } = new List<TransferProposal>();

        public Club Club(Id id)
        {
            foreach (var c in World.Clubs) if (c.Id == id) return c;
            return null;
        }
    }

    /// <summary>
    /// Drives a transfer window (B5): implemented by <c>Game.Career.Market.TransferWindow</c>. Injected like the
    /// match resolver (D-19) so `Career` never depends on `Career.Market`. Null in <see cref="CareerSimulator"/> = no
    /// market (a career can still run without one, e.g. earlier-stage tests).
    /// </summary>
    /// <summary>
    /// The managed club's team plan (C3b, X-97; GAME_DESIGN §27, MVP_SCOPE "formações, mentalidade, linha, pressão"):
    /// the tactic, and the starters (in formation slot order) and bench the user picked. No starters = the lineup is
    /// picked automatically for every match. Player Ids that are no longer in the squad are simply ignored.
    /// </summary>
    public sealed class UserTeamPlan
    {
        public Game.Core.Contracts.Match.TacticSetup Tactic { get; }
        public IReadOnlyList<Id> Starters { get; }
        public IReadOnlyList<Id> Bench { get; }
        public bool ManualLineup => Starters.Count > 0;

        public UserTeamPlan(Game.Core.Contracts.Match.TacticSetup tactic, IReadOnlyList<Id> starters, IReadOnlyList<Id> bench)
        {
            Tactic = tactic ?? throw new ArgumentNullException(nameof(tactic));
            Starters = starters == null ? new List<Id>() : new List<Id>(starters);
            Bench = bench == null ? new List<Id>() : new List<Id>(bench);
        }
    }

    public interface ITransferWindow
    {
        void Run(CareerState state, GameDatabase db, DateTime date, ulong seed);
    }

    /// <summary>A played match interrupted by closing the app (C1): its fixture and the match engine's snapshot.</summary>
    public sealed class MatchInProgress
    {
        public Id FixtureId { get; }
        public byte[] Snapshot { get; }

        public MatchInProgress(Id fixtureId, byte[] snapshot)
        {
            FixtureId = fixtureId;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }

    /// <summary>An AI club's offer for a managed-club player, awaiting a human decision (B5).</summary>
    public sealed class TransferProposal
    {
        public Id Id { get; internal set; }
        public Id FromClubId { get; internal set; }
        public Id PlayerId { get; internal set; }
        public long OfferAmount { get; internal set; }
        public DateTime Date { get; internal set; }
    }

    /// <summary>
    /// Drives the club economy (B6): implemented by <c>Game.Career.Economy.EconomySystem</c>. Injected like
    /// <see cref="ITransferWindow"/> so `Career` never depends on `Career.Economy`. Null in
    /// <see cref="CareerSimulator"/> = no economy (a career can still run without one, e.g. earlier-stage tests).
    /// </summary>
    public interface IEconomySystem
    {
        /// <summary>TV, gate, sponsorship, wages and maintenance for the month just ended.</summary>
        void MonthEnd(CareerState state, GameDatabase db, DateTime date);

        /// <summary>League and cup prizes for the season just ended.</summary>
        void SeasonEnd(CareerState state, GameDatabase db, IReadOnlyList<IReadOnlyList<Id>> finalTables,
            Id cupWinner, Id cupRunnerUp, int year);
    }

    /// <summary>
    /// The one way money moves (B6, X-44): every revenue or expense goes through here, so
    /// <see cref="Club.Balance"/> is always exactly the sum of the season's ledger (plus archived past seasons'
    /// nets, TEST_PLAN). Both `Career.Market` (transfer fees) and `Career.Economy` (TV, gate, wages, ...) call it.
    /// </summary>
    public static class LedgerBook
    {
        public static void Post(CareerState state, Id clubId, DateTime date, LedgerCategory category, long amount)
        {
            state.Season.Ledger.Add(new LedgerEntry { Id = state.Ids.Next(), ClubId = clubId, Date = date, Category = category, Amount = amount });
            state.Club(clubId).Balance += amount;
        }
    }
}
