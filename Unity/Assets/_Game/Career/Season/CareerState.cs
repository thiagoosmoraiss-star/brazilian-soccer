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
    public interface ITransferWindow
    {
        void Run(CareerState state, GameDatabase db, DateTime date, ulong seed);
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
}
