using System;
using System.Collections.Generic;
using Game.Core.Ids;
using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Rules.Ovr;

namespace Game.Career.World
{
    /// <summary>
    /// World invariants (TEST_PLAN §3): unique Ids, every referenced Id exists, one contract per player,
    /// squad sizes, clubs per division, attributes 1-99, potential 40-99 and never below the current OVR, no traits.
    /// </summary>
    public static class WorldValidator
    {
        public const string InvalidWorldState = "INVALID_WORLD_STATE";

        public static IReadOnlyList<Error> Validate(WorldState world, GameDatabase db)
        {
            var e = new List<Error>();
            void Fail(string m) => e.Add(new Error(InvalidWorldState, m));
            var g = db.World.Generation;

            var ids = new HashSet<Id>();
            void Register(Id id, string what)
            {
                if (id.IsNone) Fail($"{what} has no Id.");
                else if (!ids.Add(id)) Fail($"Duplicate Id {id} ({what}).");
                if (id.Value > world.LastIssuedId) Fail($"Id {id} is above LastIssuedId {world.LastIssuedId}.");
            }

            var clubIds = new HashSet<Id>();
            var perDivision = new int[g.Divisions.Count];
            var clubNames = new HashSet<string>(StringComparer.Ordinal);
            var shortNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in world.Clubs)
            {
                Register(c.Id, "club " + c.Name);
                clubIds.Add(c.Id);
                if (!clubNames.Add(c.Name)) Fail($"Duplicate club name {c.Name}.");
                if (c.ShortName == null || c.ShortName.Length != 3 || !shortNames.Add(c.ShortName)) Fail($"Invalid or duplicate short name {c.ShortName}.");
                if (!Uf.IsValid(c.Uf)) Fail($"Club {c.Name} has invalid UF {c.Uf}.");
                if (c.DivisionIndex < 0 || c.DivisionIndex >= g.Divisions.Count) { Fail($"Club {c.Name} has invalid division."); continue; }
                perDivision[c.DivisionIndex]++;
                var d = g.Divisions[c.DivisionIndex];
                if (c.DivisionName != d.Name) Fail($"Club {c.Name}: division name mismatch.");
                if (c.Colors.PrimaryId == c.Colors.SecondaryId) Fail($"Club {c.Name} has identical colors.");
                if (!d.Reputation.Contains(c.Reputation)) Fail($"Club {c.Name}: reputation {c.Reputation} outside {d.Reputation}.");
                if (c.Stars < 1 || c.Stars > 5) Fail($"Club {c.Name}: stars {c.Stars} outside 1-5.");
                if (c.Budget < d.Budget.Min || c.Budget > d.Budget.Max) Fail($"Club {c.Name}: budget outside range.");
                if (!d.StadiumLevel.Contains(c.Stadium.Level)) Fail($"Club {c.Name}: stadium level {c.Stadium.Level} outside {d.StadiumLevel}.");
                if (!d.Fans.Contains(c.Fans)) Fail($"Club {c.Name}: fans {c.Fans} outside {d.Fans}.");
            }
            for (int i = 0; i < perDivision.Length; i++)
                if (perDivision[i] != g.ClubsPerDivision) Fail($"Division {g.Divisions[i].Name} has {perDivision[i]} clubs, expected {g.ClubsPerDivision}.");

            var playerIds = new HashSet<Id>();
            var sq = g.Squad;
            int minAge = Math.Min(sq.YouthAge.Min, Math.Min(sq.RegularAge.Min, sq.VeteranAge.Min));
            int maxAge = Math.Max(sq.YouthAge.Max, Math.Max(sq.RegularAge.Max, sq.VeteranAge.Max));
            foreach (var p in world.Players)
            {
                Register(p.Id, "player " + p.Name);
                playerIds.Add(p.Id);
                if (p.Attributes.Count != AttrInfo.Count) { Fail($"Player {p.Id} does not have {AttrInfo.Count} attributes."); continue; }
                foreach (var v in p.Attributes)
                    if (v < AttrInfo.MinValue || v > AttrInfo.MaxValue) Fail($"Player {p.Id} has attribute {v} outside 1-99.");
                int ovr = OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition);
                if (p.Potential < g.Potential.Min || p.Potential > g.Potential.Max) Fail($"Player {p.Id}: potential {p.Potential} outside {g.Potential.Min}-{g.Potential.Max}.");
                if (p.Potential < ovr) Fail($"Player {p.Id}: potential {p.Potential} below OVR {ovr}.");
                int age = world.AgeAtStart(p);
                if (age < minAge || age > maxAge) Fail($"Player {p.Id}: age {age} outside {minAge}-{maxAge}.");
                if (p.WeakFoot < 1 || p.WeakFoot > 5) Fail($"Player {p.Id}: weak foot {p.WeakFoot} outside 1-5.");
                if (p.SecondaryPositions.Count > 2) Fail($"Player {p.Id} has more than 2 secondary positions.");
                foreach (var s in p.SecondaryPositions)
                    if (s == p.MainPosition) Fail($"Player {p.Id}: secondary position equals the main one.");
            }

            var contractsPerPlayer = new Dictionary<Id, int>();
            var squadSize = new Dictionary<Id, int>();
            foreach (var c in world.Contracts)
            {
                Register(c.Id, "contract");
                if (!playerIds.Contains(c.PlayerId)) Fail($"Contract {c.Id} references missing player {c.PlayerId}.");
                if (!clubIds.Contains(c.ClubId)) Fail($"Contract {c.Id} references missing club {c.ClubId}.");
                contractsPerPlayer[c.PlayerId] = contractsPerPlayer.TryGetValue(c.PlayerId, out int n) ? n + 1 : 1;
                squadSize[c.ClubId] = squadSize.TryGetValue(c.ClubId, out int m) ? m + 1 : 1;
            }
            foreach (var p in world.Players)
                if (!contractsPerPlayer.TryGetValue(p.Id, out int n) || n != 1) Fail($"Player {p.Id} must have exactly one contract.");
            foreach (var c in world.Clubs)
                if (!squadSize.TryGetValue(c.Id, out int n) || n != sq.Slots.Count) Fail($"Club {c.Name} squad size {n}, expected {sq.Slots.Count}.");

            return e;
        }

        /// <summary>Squad average OVR at each player's main position (GDD "OVR elenco", X-37).</summary>
        public static double SquadAverageOvr(WorldState world, GameDatabase db, Id clubId)
        {
            double sum = 0; int n = 0;
            foreach (var p in world.SquadOf(clubId)) { sum += OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition); n++; }
            return n == 0 ? 0 : sum / n;
        }
    }
}
