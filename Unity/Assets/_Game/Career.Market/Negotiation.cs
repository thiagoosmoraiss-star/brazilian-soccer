using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.World;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Loading;
using Game.Rules.Market;
using Game.Rules.Ovr;

namespace Game.Career.Market
{
    /// <summary>Outcome of a transfer negotiation (GAME_DESIGN §10, B5, X-43).</summary>
    public readonly struct NegotiationResult
    {
        public readonly bool Success;
        public readonly long Fee;
        public readonly long Wage;

        public NegotiationResult(bool success, long fee, long wage) { Success = success; Fee = fee; Wage = wage; }

        public static readonly NegotiationResult Failed = new NegotiationResult(false, 0, 0);
    }

    /// <summary>
    /// Two-stage transfer negotiation (GAME_DESIGN §10: ClubStage then PlayerStage, up to `negotiation.maxRounds`
    /// rounds). The club raises its offer and the wage each round; the seller accepts once the offer clears its
    /// minimum (importance, remaining contract) and the buyer can afford it; the player accepts once the wage,
    /// relative reputation and division clear the interest threshold.
    /// </summary>
    public static class Negotiation
    {
        public static int Ovr(GameDatabase db, Player p) => OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition);

        public static NegotiationResult Negotiate(GameDatabase db, WorldState world, Club buyer, Club seller, Player player,
            DateTime date, Dictionary<Id, long> spentThisWindow, Rng rng)
        {
            var m = db.Market;
            int ovr = Ovr(db, player);
            int age = player.BirthDate.AgeOn(date.Year, date.Month, date.Day);
            int yearsLeft = world.ContractOf(player.Id)?.YearsLeft(date.Year) ?? 0;
            float form = player.Condition.Form ?? 6f;
            long value = MarketRules.Value(m, seller.DivisionIndex, ovr, age, player.Potential, yearsLeft, form);

            var squad = world.SquadOf(seller.Id).OrderByDescending(x => Ovr(db, x)).ToList();
            int rank = squad.FindIndex(x => x.Id == player.Id);
            float importance01 = squad.Count <= 1 ? 1f : 1f - (float)rank / (squad.Count - 1);
            long minAccept = (long)(value * MarketRules.SellerAcceptFactor(m, importance01, yearsLeft, financialHealth01: 0.5f));

            long offer = value;
            long wageOffer = MarketRules.WageReference(m, buyer.DivisionIndex, buyer.Reputation, ovr);
            long sellerReferenceWage = MarketRules.WageReference(m, seller.DivisionIndex, seller.Reputation, ovr);

            int rounds = Math.Max(1, m.Negotiation.MaxRounds);
            for (int round = 0; round < rounds; round++)
            {
                bool clubOk = offer >= minAccept && CanAfford(world, m, buyer, offer, wageOffer, spentThisWindow);
                float interest = MarketRules.Interest(m, buyer.Reputation, seller.Reputation, buyer.DivisionIndex,
                    seller.DivisionIndex, wageOffer, sellerReferenceWage, promisedMinutesShare: 0.6f);
                bool playerOk = interest >= m.Interest.AcceptThreshold;
                if (clubOk && playerOk) return new NegotiationResult(true, offer, wageOffer);
                offer = (long)(offer * (1f + m.Negotiation.ClubRaisePerRound));
                wageOffer = (long)(wageOffer * (1f + m.Negotiation.WageRaisePerRound));
            }
            return NegotiationResult.Failed;
        }

        private static bool CanAfford(WorldState world, Data.Career.MarketDefinition m, Club buyer, long fee, long wage,
            Dictionary<Id, long> spentThisWindow)
        {
            long spent = spentThisWindow.TryGetValue(buyer.Id, out var s) ? s : 0L;
            long fundCap = (long)(buyer.Budget * m.Budget.TransferFundShare);
            if (spent + fee > fundCap) return false;

            long wageCap = (long)(buyer.Budget * m.Budget.WageShareOfBudget);
            long currentWageBill = 0;
            foreach (var p in world.SquadOf(buyer.Id)) currentWageBill += world.ContractOf(p.Id)?.Wage ?? 0;
            return currentWageBill + wage <= wageCap;
        }
    }
}
