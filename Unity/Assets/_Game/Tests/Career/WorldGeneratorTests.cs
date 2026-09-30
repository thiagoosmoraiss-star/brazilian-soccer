using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Career.World;
using Game.Core.Ids;
using Game.Rules.Ovr;
using NUnit.Framework;

namespace Game.Tests.Career
{
    /// <summary>B1 acceptance tests (ROADMAP): valid world for many seeds, OVR per division inside the GDD ranges,
    /// unique Ids, same seed = same world.</summary>
    public class WorldGeneratorTests
    {
        private static IEnumerable<ulong> Seeds() => CareerTestData.Seeds();

        [TestCaseSource(nameof(Seeds))]
        public void World_IsValid(ulong seed)
        {
            var db = CareerTestData.Db();
            var world = WorldGenerator.Generate(db, seed);
            var errors = WorldValidator.Validate(world, db);
            Assert.IsEmpty(errors, $"seed={seed}\n" + string.Join("\n", errors.Take(20)));

            var g = db.World.Generation;
            Assert.AreEqual(g.Divisions.Count * g.ClubsPerDivision, world.Clubs.Count, $"seed={seed}");
            Assert.AreEqual(64, world.Clubs.Count, $"seed={seed}: D-12 = 64 clubs (4 x 16)");
            Assert.AreEqual(world.Clubs.Count * CareerTestData.Int("squadSize"), world.Players.Count, $"seed={seed}");
            Assert.AreEqual(world.Players.Count, world.Contracts.Count, $"seed={seed}");
        }

        [TestCaseSource(nameof(Seeds))]
        public void SquadOvr_PerDivision_IsInsideTheGddRanges(ulong seed)
        {
            var db = CareerTestData.Db();
            var world = WorldGenerator.Generate(db, seed);
            var divisionMeans = new List<double>();
            foreach (var division in world.DivisionNames)
            {
                var (min, max) = CareerTestData.SquadOvrRange(division);
                var averages = world.Clubs.Where(c => c.DivisionName == division)
                    .Select(c => WorldValidator.SquadAverageOvr(world, db, c.Id)).ToList();
                foreach (var avg in averages)
                    Assert.That(avg, Is.InRange((double)min, (double)max), $"seed={seed} division={division}");
                divisionMeans.Add(averages.Average());
            }
            for (int i = 1; i < divisionMeans.Count; i++)
                Assert.Greater(divisionMeans[i - 1], divisionMeans[i], $"seed={seed}: divisions must be ordered by strength");
        }

        [TestCaseSource(nameof(Seeds))]
        public void Ids_AreUniqueAcrossAllEntities(ulong seed)
        {
            var world = WorldGenerator.Generate(CareerTestData.Db(), seed);
            var all = world.Clubs.Select(c => c.Id).Concat(world.Players.Select(p => p.Id)).Concat(world.Contracts.Select(c => c.Id)).ToList();
            CollectionAssert.AllItemsAreUnique(all, $"seed={seed}");
            Assert.IsFalse(all.Any(id => id.IsNone), $"seed={seed}");
            Assert.AreEqual(all.Max(id => id.Value), world.LastIssuedId, $"seed={seed}");
        }

        [Test]
        public void SameSeed_ProducesTheSameWorld()
        {
            var db = CareerTestData.Db();
            foreach (var seed in Seeds().Take(5))
                Assert.AreEqual(Fingerprint(WorldGenerator.Generate(db, seed)), Fingerprint(WorldGenerator.Generate(db, seed)), $"seed={seed}");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentWorlds()
        {
            var db = CareerTestData.Db();
            Assert.AreNotEqual(Fingerprint(WorldGenerator.Generate(db, 1)), Fingerprint(WorldGenerator.Generate(db, 2)));
        }

        [TestCaseSource(nameof(Seeds))]
        public void Squads_FollowTheInitialSquadProfile(ulong seed)
        {
            var db = CareerTestData.Db();
            var world = WorldGenerator.Generate(db, seed);
            var sq = db.World.Generation.Squad;
            var youthRange = CareerTestData.Range("promisingYouthPerSquad");
            var veteranRange = CareerTestData.Range("veteransPerSquad");
            foreach (var club in world.Clubs)
            {
                var squad = world.SquadOf(club.Id).ToList();
                int youth = squad.Count(p => world.AgeAtStart(p) <= sq.YouthAge.Max);
                int veterans = squad.Count(p => world.AgeAtStart(p) >= sq.VeteranAge.Min);
                Assert.That(youth, Is.InRange(youthRange.Min, youthRange.Max), $"seed={seed} club={club.ShortName}");
                Assert.That(veterans, Is.InRange(veteranRange.Min, veteranRange.Max), $"seed={seed} club={club.ShortName}");
                Assert.IsTrue(squad.Any(p => p.MainPosition == Data.Definitions.Position.GOL), $"seed={seed} club={club.ShortName}");
            }
        }

        [Test]
        public void GoalkeepingAttributes_AreLowForOutfieldPlayers_AndTheReverse()
        {
            var db = CareerTestData.Db();
            var world = WorldGenerator.Generate(db, 7);
            var range = db.World.Generation.Attributes.GoalkeepingAttributesForOutfield;
            foreach (var p in world.Players.Where(p => p.MainPosition != Data.Definitions.Position.GOL))
                for (int a = (int)Data.Effects.Attr.GkReflexes; a <= (int)Data.Effects.Attr.GkHandling; a++)
                    Assert.That(p.Attributes[a], Is.InRange(range.Min, range.Max), $"seed=7 player={p.Id}");
        }

        [Test]
        public void Players_HaveNoTraits()
        {
            // X-36: B1 generates players without traits.
            Assert.IsFalse(typeof(Player).GetProperties().Any(p => p.Name.Contains("Trait")));
            Assert.IsFalse(typeof(Player).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                                    System.Reflection.BindingFlags.NonPublic).Any(f => f.Name.Contains("Trait")));
        }

        [Test]
        public void ShortNames_AreDerivedAndUnique()
        {
            var used = new HashSet<string>();
            Assert.AreEqual("SER", WorldGenerator.ShortName("Serra dos Ventos", used));
            Assert.AreEqual("CDO", WorldGenerator.ShortName("Campo Dourado do Oeste", used));
            Assert.AreEqual("SEV", WorldGenerator.ShortName("Serra Verde", used)); // SER taken
        }

        /// <summary>Canonical text dump of every generated field (order-sensitive).</summary>
        private static string Fingerprint(WorldState w)
        {
            var sb = new StringBuilder();
            sb.Append(w.Seed).Append('|').Append(w.StartYear).Append('|').Append(w.LastIssuedId).AppendLine();
            foreach (var c in w.Clubs)
                sb.Append(c.Id).Append(c.Name).Append(c.ShortName).Append(c.City).Append(c.Uf).Append(c.Colors.PrimaryId).Append(c.Colors.SecondaryId)
                  .Append(c.Crest.ShapeId).Append(c.Crest.SymbolId).Append(c.DivisionIndex).Append('|').Append(c.Reputation).Append('|')
                  .Append(c.Stars).Append('|').Append(c.Budget).Append('|').Append(c.Stadium.Level).Append('|').Append(c.Stadium.Capacity)
                  .Append('|').Append(c.Fans).AppendLine();
            foreach (var p in w.Players)
                sb.Append(p.Id).Append(p.Name).Append(p.Nationality).Append(p.BirthDate).Append(p.HeightCm).Append(p.PreferredFoot)
                  .Append(p.WeakFoot).Append(p.AvatarSeed).Append(string.Join(",", p.Attributes)).Append('|').Append(p.Potential)
                  .Append(p.MainPosition).Append(string.Join(",", p.SecondaryPositions)).AppendLine();
            foreach (var c in w.Contracts) sb.Append(c.Id).Append('>').Append(c.PlayerId).Append('>').Append(c.ClubId).AppendLine();
            return sb.ToString();
        }
    }
}
