using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Career;
using Game.Data.Definitions;
using Game.Data.Loading;
using Game.Rules.Market;

namespace Game.Career.Market
{
    /// <summary>
    /// Transfer-window AI (B5, X-43): renews or lets expire contracts ending this year, signs free agents and trades
    /// between clubs to keep squads within Data/Career/market.json's size band, trims surplus, and occasionally sells
    /// a high-potential youth abroad. Never auto-buys, auto-sells or auto-trims for
    /// <see cref="CareerState.ManagedClubId"/>: AI interest in one of its players becomes a
    /// <see cref="TransferProposal"/> for the human to decide, instead of a completed transfer.
    /// </summary>
    public sealed class TransferWindow : ITransferWindow
    {
        public void Run(CareerState state, GameDatabase db, DateTime date, ulong seed)
        {
            var rng = new Rng(RngStreams.DeriveSeed(seed, "Career.Market", (ulong)(date.Year * 16 + date.Month)));
            RenewOrLetExpire(state, db, date, rng);
            SignFreeAgentsBelowFloor(state, db, date, rng);
            TradeBetweenClubs(state, db, date, rng);
            TrimAboveCeiling(state, db, date, rng);
            SellYoungTalentAbroad(state, db, date, rng);
        }

        private static int Ovr(GameDatabase db, Player p) => Negotiation.Ovr(db, p);
        private static bool IsManaged(CareerState state, Id clubId) => state.ManagedClubId.HasValue && state.ManagedClubId.Value == clubId;

        /// <summary>Contracts ending this year: important players (top `renewalImportanceRank` by OVR) are renewed.</summary>
        private static void RenewOrLetExpire(CareerState state, GameDatabase db, DateTime date, Rng rng)
        {
            var world = state.World;
            var m = db.Market;
            foreach (var club in world.Clubs)
            {
                var squad = world.SquadOf(club.Id).OrderByDescending(p => Ovr(db, p)).ToList();
                for (int i = 0; i < squad.Count; i++)
                {
                    var p = squad[i];
                    var contract = world.ContractOf(p.Id);
                    if (contract == null || contract.EndYear != date.Year || i >= m.Contracts.RenewalImportanceRank) continue;
                    int length = rng.NextInt(m.Contracts.RenewalYears.Min, m.Contracts.RenewalYears.Max + 1);
                    long wage = MarketRules.WageReference(m, club.DivisionIndex, club.Reputation, Ovr(db, p));
                    world.SetContract(new Contract
                    {
                        Id = state.Ids.Next(), PlayerId = p.Id, ClubId = club.Id, Wage = wage,
                        StartYear = date.Year, EndYear = date.Year + length - 1,
                    });
                }
            }
        }

        /// <summary>Clubs below the squad floor sign the closest-fit free agent, at no fee (GAME_DESIGN §10).</summary>
        private static void SignFreeAgentsBelowFloor(CareerState state, GameDatabase db, DateTime date, Rng rng)
        {
            var world = state.World;
            var m = db.Market;
            foreach (var club in world.Clubs.OrderBy(c => c.Id.Value))
            {
                if (IsManaged(state, club.Id)) continue;
                int signed = 0;
                while (world.SquadOf(club.Id).Count < m.Squad.Min && signed < m.Ai.MaxSigningsPerClubPerWindow)
                {
                    var freeAgents = world.FreeAgents();
                    if (freeAgents.Count == 0) break;
                    float clubMean = SquadMeanOvr(db, world, club.Id);
                    Player best = null;
                    float bestDist = float.MaxValue;
                    foreach (var fa in freeAgents)
                    {
                        float dist = Math.Abs(Ovr(db, fa) - clubMean);
                        if (dist < bestDist) { bestDist = dist; best = fa; }
                    }
                    long wage = MarketRules.WageReference(m, club.DivisionIndex, club.Reputation, Ovr(db, best));
                    int length = rng.NextInt(m.Contracts.NewSigningYears.Min, m.Contracts.NewSigningYears.Max + 1);
                    world.SetContract(new Contract
                    {
                        Id = state.Ids.Next(), PlayerId = best.Id, ClubId = club.Id, Wage = wage,
                        StartYear = date.Year, EndYear = date.Year + length - 1,
                    });
                    signed++;
                }
            }
        }

        /// <summary>Clubs still short buy from clubs with surplus; a managed club in the way draws a proposal instead.</summary>
        private static void TradeBetweenClubs(CareerState state, GameDatabase db, DateTime date, Rng rng)
        {
            var world = state.World;
            var m = db.Market;
            var spent = new Dictionary<Id, long>();
            int proposalsToManaged = 0;
            foreach (var buyer in world.Clubs.OrderBy(c => c.Id.Value))
            {
                if (IsManaged(state, buyer.Id)) continue;
                int need = m.Squad.Min - world.SquadOf(buyer.Id).Count;
                int signed = 0;
                while (need > 0 && signed < m.Ai.MaxSigningsPerClubPerWindow)
                {
                    var seller = FindSellerWithSurplus(state, world, m, buyer.Id);
                    float buyerMean = SquadMeanOvr(db, world, buyer.Id);
                    var player = seller == null ? null : PickClosestFitSellablePlayer(db, world, seller, buyerMean);
                    if (player == null) break;
                    var result = Negotiation.Negotiate(db, world, buyer, seller, player, date, spent, rng);
                    if (!result.Success) break;
                    int length = rng.NextInt(m.Contracts.NewSigningYears.Min, m.Contracts.NewSigningYears.Max + 1);
                    world.SetContract(new Contract
                    {
                        Id = state.Ids.Next(), PlayerId = player.Id, ClubId = buyer.Id, Wage = result.Wage,
                        StartYear = date.Year, EndYear = date.Year + length - 1,
                    });
                    seller.Budget += result.Fee;
                    buyer.Budget -= result.Fee;
                    spent[buyer.Id] = (spent.TryGetValue(buyer.Id, out var s) ? s : 0) + result.Fee;
                    need--; signed++;
                }

                if (need > 0 && state.ManagedClubId.HasValue && proposalsToManaged < m.Ai.MaxProposalsToManagedClubPerWindow)
                {
                    var managed = state.Club(state.ManagedClubId.Value);
                    var target = PickClosestFitSellablePlayer(db, world, managed, SquadMeanOvr(db, world, buyer.Id));
                    if (target == null) continue;
                    var offer = Negotiation.Negotiate(db, world, buyer, managed, target, date, new Dictionary<Id, long>(), rng);
                    if (!offer.Success) continue;
                    state.Proposals.Add(new TransferProposal
                    {
                        Id = state.Ids.Next(), FromClubId = buyer.Id, PlayerId = target.Id, OfferAmount = offer.Fee, Date = date,
                    });
                    proposalsToManaged++;
                }
            }
        }

        /// <summary>Clubs above the squad ceiling release their weakest players to free agency (no fee).</summary>
        private static void TrimAboveCeiling(CareerState state, GameDatabase db, DateTime date, Rng rng)
        {
            var world = state.World;
            var m = db.Market;
            foreach (var club in world.Clubs)
            {
                if (IsManaged(state, club.Id)) continue;
                int released = 0;
                while (world.SquadOf(club.Id).Count > m.Squad.Max && released < m.Ai.MaxReleasesPerClubPerWindow)
                {
                    var player = PickSellablePlayer(db, world, club);
                    if (player == null) break;
                    world.ReleasePlayer(player.Id);
                    released++;
                }
            }
        }

        /// <summary>A few high-potential youths leave for fictional foreign clubs (GAME_DESIGN §10), like retirement.</summary>
        private static void SellYoungTalentAbroad(CareerState state, GameDatabase db, DateTime date, Rng rng)
        {
            var world = state.World;
            var m = db.Market.YouthForeignSale;
            foreach (var p in world.Players.ToList())
            {
                var clubId = world.ClubOf(p.Id);
                if (clubId.IsNone || IsManaged(state, clubId)) continue;
                int age = p.BirthDate.AgeOn(date.Year, date.Month, date.Day);
                if (p.Potential < m.MinPotential || age > m.MaxAge || rng.NextDouble() >= m.ChancePerWindow) continue;
                // Never sell the club below development.json's goalkeeper minimum (no window-level top-up for it).
                if (p.MainPosition == Position.GOL
                    && world.SquadOf(clubId).Count(sq => sq.MainPosition == Position.GOL) <= db.Development.MinGoalkeepers)
                    continue;

                var club = state.Club(clubId);
                var contract = world.ContractOf(p.Id);
                int ovr = Ovr(db, p);
                float form = p.Condition.Form ?? 6f;
                long fee = (long)(MarketRules.Value(db.Market, club.DivisionIndex, ovr, age, p.Potential,
                    contract.YearsLeft(date.Year), form) * m.ValueMultiplier);
                club.Budget += fee; // fictional sale abroad; B6 replaces this proxy with a real ledger entry.
                world.RemovePlayer(p.Id);
            }
        }

        private static Club FindSellerWithSurplus(CareerState state, WorldState world, MarketDefinition m, Id excludeClubId)
        {
            Club best = null;
            int bestSurplus = 0;
            foreach (var c in world.Clubs)
            {
                if (c.Id == excludeClubId || IsManaged(state, c.Id)) continue;
                int surplus = world.SquadOf(c.Id).Count - m.Squad.Min;
                if (surplus > bestSurplus) { bestSurplus = surplus; best = c; }
            }
            return best;
        }

        /// <summary>Lowest-OVR squad player, keeping at least `development.json`'s minimum goalkeepers.</summary>
        private static Player PickSellablePlayer(GameDatabase db, WorldState world, Club club)
        {
            Player best = null;
            float bestOvr = float.MaxValue;
            foreach (var p in SellableCandidates(db, world, club))
            {
                float ovr = Ovr(db, p);
                if (ovr < bestOvr) { bestOvr = ovr; best = p; }
            }
            return best;
        }

        /// <summary>
        /// Sellable squad player closest in OVR to <paramref name="targetOvr"/> (keeping the goalkeeper minimum): a
        /// club gives up a player that roughly fits the buyer, not necessarily its weakest (B5, X-43).
        /// </summary>
        private static Player PickClosestFitSellablePlayer(GameDatabase db, WorldState world, Club club, float targetOvr)
        {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (var p in SellableCandidates(db, world, club))
            {
                float dist = Math.Abs(Ovr(db, p) - targetOvr);
                if (dist < bestDist) { bestDist = dist; best = p; }
            }
            return best;
        }

        private static IEnumerable<Player> SellableCandidates(GameDatabase db, WorldState world, Club club)
        {
            var squad = world.SquadOf(club.Id);
            int keepers = 0;
            foreach (var p in squad) if (p.MainPosition == Position.GOL) keepers++;
            foreach (var p in squad)
            {
                if (p.MainPosition == Position.GOL && keepers <= db.Development.MinGoalkeepers) continue;
                yield return p;
            }
        }

        private static float SquadMeanOvr(GameDatabase db, WorldState world, Id clubId)
        {
            var squad = world.SquadOf(clubId);
            if (squad.Count == 0) return 50f;
            float sum = 0f;
            foreach (var p in squad) sum += Ovr(db, p);
            return sum / squad.Count;
        }
    }
}
