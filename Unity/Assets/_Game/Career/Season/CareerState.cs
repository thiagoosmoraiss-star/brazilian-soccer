using System.Collections.Generic;
using Game.Career.World;
using Game.Core.Ids;

namespace Game.Career.Season
{
    /// <summary>
    /// Everything the save will persist (B8): world, current season, history, Id allocator and seed.
    /// B3 scope: no manager, finances, market or player development yet.
    /// </summary>
    public sealed class CareerState
    {
        public ulong Seed { get; internal set; }
        public WorldState World { get; internal set; }
        public Season Season { get; internal set; }
        public List<SeasonSummary> History { get; } = new List<SeasonSummary>();
        public IdAllocator Ids { get; internal set; }

        public Club Club(Id id)
        {
            foreach (var c in World.Clubs) if (c.Id == id) return c;
            return null;
        }
    }
}
