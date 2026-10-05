using System;
using System.Collections.Generic;
using System.Text;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Data.Ovr;
using Game.Data.World;
using Game.Rules.Market;
using Game.Rules.Ovr;

namespace Game.Career.World
{
    /// <summary>
    /// Generates a fictional world (clubs, players, minimal contracts) from Data/World and Data/Balance/ovr.json.
    /// Deterministic: same seed + same data = same world. Uses its own RNG streams.
    /// </summary>
    public static class WorldGenerator
    {
        public const string ClubStream = "WorldGen.Clubs";
        public const string PlayerStream = "WorldGen.Players";
        public const string ContractStream = "WorldGen.Contracts";

        private static readonly Attr[] GoalkeepingAttributes =
            { Attr.GkReflexes, Attr.GkPositioning, Attr.GkAerial, Attr.GkHandling };

        public static WorldState Generate(GameDatabase db, ulong seed)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            var def = db.World;
            var g = def.Generation;
            var streams = new RngStreams(seed);
            var clubRng = streams.Get(ClubStream);
            var playerRng = streams.Get(PlayerStream);
            var contractRng = streams.Get(ContractStream);
            var ids = new IdAllocator();

            var clubs = new List<Club>();
            var players = new List<Player>();
            var contracts = new List<Contract>();
            var divisionNames = new List<string>();
            foreach (var d in g.Divisions) divisionNames.Add(d.Name);

            // One distinct city per club.
            var cityOrder = Shuffle(def.Cities.Count, clubRng);
            var usedShortNames = new HashSet<string>(StringComparer.Ordinal);
            var clubMeans = new List<float>();
            int cityCursor = 0;

            for (int di = 0; di < g.Divisions.Count; di++)
            {
                var div = g.Divisions[di];
                for (int k = 0; k < g.ClubsPerDivision; k++)
                {
                    var city = def.Cities[cityOrder[cityCursor++]];
                    float strength = (float)clubRng.NextDouble();
                    clubs.Add(CreateClub(def, div, di, city, strength, clubRng, ids, usedShortNames));
                    clubMeans.Add(Lerp(div.SquadOvrMean.Min, div.SquadOvrMean.Max, strength));
                }
            }

            for (int i = 0; i < clubs.Count; i++)
                GenerateSquad(db, def, clubs[i], clubMeans[i], playerRng, contractRng, ids, players, contracts);

            return new WorldState
            {
                Seed = seed,
                StartYear = g.StartYear,
                LastIssuedId = ids.LastIssued,
                DivisionNames = divisionNames,
                Clubs = clubs,
                PlayerList = players,
                ContractList = contracts,
            };
        }

        // ---------------- Clubs ----------------

        private static Club CreateClub(WorldDefinition def, DivisionParameters div, int divisionIndex, CityDefinition city,
            float strength, Rng rng, IdAllocator ids, HashSet<string> usedShortNames)
        {
            var g = def.Generation;
            var patterns = def.ClubTemplates.NamePatterns;
            string name = patterns[rng.NextInt(0, patterns.Count)].Replace(WorldDefinitionValidator.CityPlaceholder, city.Name);

            var colors = def.ClubTemplates.Colors;
            int c1 = rng.NextInt(0, colors.Count);
            int c2 = rng.NextInt(0, colors.Count - 1);
            if (c2 >= c1) c2++;

            float Profile() => Clamp01(strength + ((float)rng.NextDouble() * 2f - 1f) * g.ClubProfileNoise);

            int reputation = (int)Math.Round(Lerp(div.Reputation.Min, div.Reputation.Max, Profile()));
            long budget = (long)Math.Round(div.Budget.Min + (div.Budget.Max - div.Budget.Min) * (double)Profile());
            int stadiumLevel = (int)Math.Round(Lerp(div.StadiumLevel.Min, div.StadiumLevel.Max, Profile()));
            // Fan bases span orders of magnitude: interpolate on a log scale.
            double fansLog = Math.Log(div.Fans.Min) + (Math.Log(div.Fans.Max) - Math.Log(div.Fans.Min)) * Profile();
            int fans = (int)Math.Round(Math.Exp(fansLog));
            fans = Math.Max(div.Fans.Min, Math.Min(div.Fans.Max, fans));

            int stars = 1;
            for (int s = 0; s < g.ReputationStarThresholds.Count; s++)
                if (reputation >= g.ReputationStarThresholds[s]) stars = s + 1;

            return new Club
            {
                Id = ids.Next(),
                Name = name,
                ShortName = ShortName(city.Name, usedShortNames),
                City = city.Name,
                Uf = city.Uf,
                Colors = new ClubColors
                {
                    PrimaryId = colors[c1].Id, PrimaryHex = colors[c1].Hex,
                    SecondaryId = colors[c2].Id, SecondaryHex = colors[c2].Hex,
                },
                Crest = new Crest
                {
                    ShapeId = def.CrestTemplates.Shapes[rng.NextInt(0, def.CrestTemplates.Shapes.Count)],
                    SymbolId = def.CrestTemplates.Symbols[rng.NextInt(0, def.CrestTemplates.Symbols.Count)],
                    PrimaryColorId = colors[c1].Id,
                    SecondaryColorId = colors[c2].Id,
                },
                DivisionIndex = divisionIndex,
                DivisionName = div.Name,
                Reputation = reputation,
                Stars = stars,
                Budget = budget,
                Stadium = new Stadium { Level = stadiumLevel, Capacity = g.StadiumCapacityByLevel[stadiumLevel - 1] },
                Fans = fans,
            };
        }

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal) { "DO", "DA", "DOS", "DAS", "DE" };

        /// <summary>Unique 3-letter short name derived from the city (initials, else first letters).</summary>
        public static string ShortName(string city, HashSet<string> used)
        {
            var words = new List<string>();
            foreach (var w in RemoveAccents(city).ToUpperInvariant().Split(' '))
                if (w.Length > 0 && !StopWords.Contains(w)) words.Add(w);

            var candidates = new List<string>();
            if (words.Count >= 3) candidates.Add("" + words[0][0] + words[1][0] + words[2][0]);
            string first = words[0];
            if (first.Length >= 3) candidates.Add(first.Substring(0, 3));
            if (words.Count >= 2) candidates.Add(first.Substring(0, Math.Min(2, first.Length)) + words[words.Count - 1][0]);
            string letters = string.Concat(words);
            for (int i = 1; i < letters.Length; i++)
                for (int j = i + 1; j < letters.Length; j++)
                    candidates.Add("" + letters[0] + letters[i] + letters[j]);

            foreach (var c in candidates)
                if (c.Length == 3 && used.Add(c)) return c;
            throw new InvalidOperationException("Could not derive a unique short name for " + city);
        }

        // Explicit table (not Unicode normalization, whose behaviour depends on the platform's globalization mode).
        private const string Accented = "ÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑáàâãäéèêëíìîïóòôõöúùûüçñ";
        private const string Plain = "AAAAAEEEEIIIIOOOOOUUUUCNaaaaaeeeeiiiiooooouuuucn";

        private static string RemoveAccents(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                int i = Accented.IndexOf(ch);
                char c = i >= 0 ? Plain[i] : ch;
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == ' ') sb.Append(c);
            }
            return sb.ToString();
        }

        // ---------------- Players ----------------

        private enum SquadRole { Regular, Youth, Veteran }

        private static void GenerateSquad(GameDatabase db, WorldDefinition def, Club club, float clubMean, Rng rng, Rng contractRng,
            IdAllocator ids, List<Player> players, List<Contract> contracts)
        {
            var ovr = db.Ovr;
            var sq = def.Generation.Squad;
            int n = sq.Slots.Count;
            var roles = new SquadRole[n];

            // Promising youths go to reserve slots first; veterans anywhere else.
            int youth = rng.NextInt(sq.YouthCount.Min, sq.YouthCount.Max + 1);
            int veterans = rng.NextInt(sq.VeteranCount.Min, sq.VeteranCount.Max + 1);
            var reserveSlots = new List<int>();
            var starterSlots = new List<int>();
            for (int i = 0; i < n; i++) (sq.Slots[i].Starter ? starterSlots : reserveSlots).Add(i);
            var youthPool = new List<int>(ShuffledCopy(reserveSlots, rng));
            youthPool.AddRange(ShuffledCopy(starterSlots, rng));
            for (int i = 0; i < youth; i++) roles[youthPool[i]] = SquadRole.Youth;
            var rest = new List<int>();
            for (int i = 0; i < n; i++) if (roles[i] == SquadRole.Regular) rest.Add(i);
            rest = ShuffledCopy(rest, rng);
            for (int i = 0; i < veterans; i++) roles[rest[i]] = SquadRole.Veteran;

            // OVR targets: role offsets + noise, re-centred so the squad mean equals the club mean exactly.
            var offsets = new float[n];
            float offsetSum = 0f;
            for (int i = 0; i < n; i++)
            {
                int baseOffset = roles[i] == SquadRole.Youth ? sq.YouthOvrOffset
                    : roles[i] == SquadRole.Veteran ? sq.VeteranOvrOffset
                    : sq.Slots[i].Starter ? sq.StarterOvrOffset : sq.ReserveOvrOffset;
                offsets[i] = baseOffset + rng.NextInt(-sq.PlayerOvrNoise, sq.PlayerOvrNoise + 1);
                offsetSum += offsets[i];
            }
            float meanOffset = offsetSum / n;

            for (int i = 0; i < n; i++)
            {
                var slot = sq.Slots[i];
                var age = roles[i] == SquadRole.Youth ? sq.YouthAge : roles[i] == SquadRole.Veteran ? sq.VeteranAge : sq.RegularAge;
                var player = CreatePlayer(ovr, def, slot.Position, clubMean + offsets[i] - meanOffset,
                    rng.NextInt(age.Min, age.Max + 1), roles[i] == SquadRole.Youth, rng, ids);
                players.Add(player);
                contracts.Add(NewContract(db, club, player, def.Generation.StartYear, contractRng, ids));
            }
        }

        /// <summary>Initial contract terms (B5, X-43): reference wage by OVR/reputation/division, a random length.</summary>
        internal static Contract NewContract(GameDatabase db, Club club, Player player, int startYear, Rng rng, IdAllocator ids)
        {
            int ovrRating = OvrCalculator.Rating(db.Ovr, player.AttributeSpan, player.MainPosition);
            long wage = MarketRules.WageReference(db.Market, club.DivisionIndex, club.Reputation, ovrRating);
            var years = db.Market.Contracts.NewSigningYears;
            int length = rng.NextInt(years.Min, years.Max + 1);
            return new Contract
            {
                Id = ids.Next(),
                PlayerId = player.Id,
                ClubId = club.Id,
                Wage = wage,
                StartYear = startYear,
                EndYear = startYear + length - 1,
            };
        }

        internal static Player CreatePlayer(OvrDefinition ovr, WorldDefinition def, Position position, float targetOvr,
            int age, bool promisingYouth, Rng rng, IdAllocator ids, int? birthYearAgeReference = null)
        {
            var g = def.Generation;
            var at = g.Attributes;
            var attrs = GenerateAttributes(ovr, at, position, targetOvr, rng);
            int rating = OvrCalculator.Rating(ovr, attrs, position);

            var pot = g.Potential;
            IntRange bonusRange = promisingYouth ? pot.YouthBonus : BonusFor(pot, age);
            int potential = rating + rng.NextInt(bonusRange.Min, bonusRange.Max + 1);
            potential = Math.Max(Math.Max(rating, pot.Min), Math.Min(pot.Max, potential));

            // Birth date such that the age on 1 January of the start year equals `age`.
            int birthYear = (birthYearAgeReference ?? g.StartYear) - age - 1;
            // Valid window: birthYear-01-02 .. (birthYear+1)-01-01, i.e. as many days as birthYear has.
            var birth = new DateTime(birthYear, 1, 2).AddDays(rng.NextInt(0, DateTime.IsLeapYear(birthYear) ? 366 : 365));

            var height = g.HeightCm[(int)position];
            var names = def.Names;
            return new Player
            {
                Id = ids.Next(),
                FirstName = names.FirstNames[rng.NextInt(0, names.FirstNames.Count)],
                LastName = names.LastNames[rng.NextInt(0, names.LastNames.Count)],
                Nationality = PickWeighted(names.Nationalities, rng),
                BirthDate = new BirthDate(birth.Year, birth.Month, birth.Day),
                HeightCm = rng.NextInt(height.Min, height.Max + 1),
                PreferredFoot = rng.NextDouble() < g.LeftFootedChance[(int)position] ? Foot.Left : Foot.Right,
                WeakFoot = 1 + PickIndex(g.WeakFootWeights, rng),
                AvatarSeed = rng.NextULong(),
                AttributeValues = attrs,
                Potential = potential,
                MainPosition = position,
                SecondaryPositionValues = PickSecondary(g, position, rng),
            };
        }

        /// <summary>
        /// Role attributes (weight &gt; 0 at the position) around the target; the others lower. Then shifts the role
        /// attributes until the weighted OVR matches the target (OVR is linear in the attributes).
        /// </summary>
        internal static int[] GenerateAttributes(OvrDefinition ovr, AttributeParameters at, Position position, float target, Rng rng)
        {
            var values = new int[AttrInfo.Count];
            bool goalkeeper = position == Position.GOL;
            for (int a = 0; a < AttrInfo.Count; a++)
            {
                var attr = (Attr)a;
                float v;
                if (ovr.Weight(position, attr) > 0f)
                    v = target + rng.NextInt(-at.RoleNoise, at.RoleNoise + 1);
                else if (!goalkeeper && IsGoalkeeping(attr))
                    v = rng.NextInt(at.GoalkeepingAttributesForOutfield.Min, at.GoalkeepingAttributesForOutfield.Max + 1);
                else
                    v = target - (goalkeeper ? at.OutfieldGapForGoalkeepers : at.OffRoleGap) + rng.NextInt(-at.OffRoleNoise, at.OffRoleNoise + 1);
                values[a] = ClampAttr(v, at);
            }

            for (int pass = 0; pass < at.OvrCorrectionPasses; pass++)
            {
                float delta = target - OvrCalculator.Exact(ovr, values, position);
                if (Math.Abs(delta) < 0.5f) break;
                for (int a = 0; a < AttrInfo.Count; a++)
                    if (ovr.Weight(position, (Attr)a) > 0f) values[a] = ClampAttr(values[a] + delta, at);
            }
            return values;
        }

        private static bool IsGoalkeeping(Attr a) => Array.IndexOf(GoalkeepingAttributes, a) >= 0;

        private static int ClampAttr(float v, AttributeParameters at)
        {
            int r = (int)Math.Round(v, MidpointRounding.AwayFromZero);
            return r < at.Min ? at.Min : (r > at.Max ? at.Max : r);
        }

        private static IntRange BonusFor(PotentialParameters pot, int age)
        {
            foreach (var b in pot.BonusByAge) if (age <= b.MaxAge) return b.Bonus;
            return pot.BonusByAge[pot.BonusByAge.Count - 1].Bonus;
        }

        private static Position[] PickSecondary(GenerationParameters g, Position main, Rng rng)
        {
            var candidates = g.SecondaryCandidates[(int)main];
            int count = Math.Min(PickIndex(g.SecondaryCountWeights, rng), candidates.Count);
            var order = Shuffle(candidates.Count, rng);
            var result = new Position[count];
            for (int i = 0; i < count; i++) result[i] = candidates[order[i]];
            return result;
        }

        // ---------------- Helpers ----------------

        private static int PickIndex(IReadOnlyList<int> weights, Rng rng)
        {
            int total = 0;
            foreach (var w in weights) total += w;
            int roll = rng.NextInt(0, total);
            for (int i = 0; i < weights.Count; i++)
            {
                if (roll < weights[i]) return i;
                roll -= weights[i];
            }
            return weights.Count - 1;
        }

        private static string PickWeighted(IReadOnlyList<WeightedCode> items, Rng rng)
        {
            var weights = new int[items.Count];
            for (int i = 0; i < items.Count; i++) weights[i] = items[i].Weight;
            return items[PickIndex(weights, rng)].Code;
        }

        private static int[] Shuffle(int count, Rng rng)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        private static List<int> ShuffledCopy(List<int> items, Rng rng)
        {
            var order = Shuffle(items.Count, rng);
            var result = new List<int>(items.Count);
            foreach (var i in order) result.Add(items[i]);
            return result;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
