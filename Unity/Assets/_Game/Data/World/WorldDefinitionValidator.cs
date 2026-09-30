using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.Effects;

namespace Game.Data.World
{
    /// <summary>
    /// Semantic validation of Data/World/*.json: pools large enough for the configured world, valid UFs and colors,
    /// consistent ranges, a squad that covers every position, and per-position tables complete.
    /// </summary>
    public static class WorldDefinitionValidator
    {
        public const string InvalidWorld = "INVALID_WORLD";
        public const string CityPlaceholder = "{city}";

        private static readonly Regex HexColor = new Regex("^#[0-9A-Fa-f]{6}$");

        public static IReadOnlyList<Error> Validate(WorldDefinition w)
        {
            var e = new List<Error>();
            void Fail(string message) => e.Add(new Error(InvalidWorld, message));
            var g = w.Generation;

            // Names
            if (w.Names.FirstNames.Count == 0 || w.Names.LastNames.Count == 0) Fail("Name pools cannot be empty.");
            CheckUnique(w.Names.FirstNames, "firstNames", Fail);
            CheckUnique(w.Names.LastNames, "lastNames", Fail);
            if (w.Names.Nationalities.Count == 0) Fail("At least one nationality is required.");
            foreach (var n in w.Names.Nationalities)
            {
                if (string.IsNullOrEmpty(n.Code) || n.Code.Length != 3) Fail($"Nationality code '{n.Code}' must have 3 letters.");
                if (n.Weight <= 0) Fail($"Nationality '{n.Code}' weight must be > 0.");
            }

            // Cities
            int totalClubs = g.Divisions.Count * g.ClubsPerDivision;
            if (w.Cities.Count < totalClubs) Fail($"{w.Cities.Count} cities for {totalClubs} clubs: one city per club is required.");
            var cityNames = new List<string>();
            foreach (var c in w.Cities)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) Fail("City without name.");
                if (!Uf.IsValid(c.Uf)) Fail($"City '{c.Name}' has invalid UF '{c.Uf}'.");
                cityNames.Add(c.Name);
            }
            CheckUnique(cityNames, "cities", Fail);

            // Club templates and crests
            if (w.ClubTemplates.NamePatterns.Count == 0) Fail("At least one club name pattern is required.");
            foreach (var p in w.ClubTemplates.NamePatterns)
                if (p == null || p.IndexOf(CityPlaceholder, StringComparison.Ordinal) < 0) Fail($"Name pattern '{p}' must contain {CityPlaceholder}.");
            if (w.ClubTemplates.Colors.Count < 2) Fail("At least two colors are required.");
            var colorIds = new List<string>();
            foreach (var c in w.ClubTemplates.Colors)
            {
                if (c.Hex == null || !HexColor.IsMatch(c.Hex)) Fail($"Color '{c.Id}' must be #RRGGBB.");
                colorIds.Add(c.Id);
            }
            CheckUnique(colorIds, "colors", Fail);
            if (w.CrestTemplates.Shapes.Count == 0 || w.CrestTemplates.Symbols.Count == 0) Fail("Crest shapes and symbols cannot be empty.");
            CheckUnique(w.CrestTemplates.Shapes, "shapes", Fail);
            CheckUnique(w.CrestTemplates.Symbols, "symbols", Fail);

            // Generation
            if (g.StartYear < 1900) Fail("startYear must be a plausible year.");
            if (g.Divisions.Count == 0) Fail("At least one division is required.");
            if (g.ClubsPerDivision < 2) Fail("clubsPerDivision must be >= 2.");
            var divNames = new List<string>();
            foreach (var d in g.Divisions)
            {
                divNames.Add(d.Name);
                if (!d.SquadOvrMean.IsValid || d.SquadOvrMean.Min < AttrInfo.MinValue || d.SquadOvrMean.Max > AttrInfo.MaxValue) Fail($"Division {d.Name}: invalid squadOvrMean {d.SquadOvrMean}.");
                if (!d.Reputation.IsValid || d.Reputation.Min < 0 || d.Reputation.Max > 1000) Fail($"Division {d.Name}: reputation must be within 0-1000.");
                if (!d.Budget.IsValid || d.Budget.Min < 0) Fail($"Division {d.Name}: invalid budget range.");
                if (!d.StadiumLevel.IsValid || d.StadiumLevel.Min < 1 || d.StadiumLevel.Max > g.StadiumCapacityByLevel.Count) Fail($"Division {d.Name}: stadium level outside 1-{g.StadiumCapacityByLevel.Count}.");
                if (!d.Fans.IsValid || d.Fans.Min < 0) Fail($"Division {d.Name}: invalid fans range.");
            }
            CheckUnique(divNames, "divisions", Fail);
            for (int i = 1; i < g.Divisions.Count; i++)
                if (g.Divisions[i].SquadOvrMean.Max > g.Divisions[i - 1].SquadOvrMean.Max)
                    Fail("Divisions must be listed from the strongest to the weakest.");

            CheckIncreasing(g.ReputationStarThresholds, "reputationStarThresholds", Fail);
            if (g.ReputationStarThresholds.Count != 5 || (g.ReputationStarThresholds.Count > 0 && g.ReputationStarThresholds[0] != 0))
                Fail("reputationStarThresholds must have 5 entries starting at 0 (1-5 stars).");
            CheckIncreasing(g.StadiumCapacityByLevel, "stadiumCapacityByLevel", Fail);
            if (g.StadiumCapacityByLevel.Count != 10) Fail("stadiumCapacityByLevel must have 10 levels (GAME_DESIGN §8).");
            if (g.ClubProfileNoise < 0f || g.ClubProfileNoise > 0.5f) Fail("clubProfileNoise must be within 0-0.5.");

            var sq = g.Squad;
            var covered = new bool[PositionInfo.Count];
            foreach (var s in sq.Slots) covered[(int)s.Position] = true;
            for (int p = 0; p < PositionInfo.Count; p++)
                if (!covered[p]) Fail($"Squad has no slot for {(Position)p}.");
            if (sq.YouthCount.Min < 0 || sq.VeteranCount.Min < 0 || !sq.YouthCount.IsValid || !sq.VeteranCount.IsValid) Fail("Invalid youth/veteran counts.");
            if (sq.YouthCount.Max + sq.VeteranCount.Max > sq.Slots.Count) Fail("Youth + veterans exceed the squad size.");
            foreach (var (r, name) in new[] { (sq.YouthAge, "youthAge"), (sq.VeteranAge, "veteranAge"), (sq.RegularAge, "regularAge") })
                if (!r.IsValid || r.Min < 15 || r.Max > 45) Fail($"{name} must be a valid age range.");
            if (sq.PlayerOvrNoise < 0) Fail("playerOvrNoise must be >= 0.");

            var at = g.Attributes;
            if (at.Min < AttrInfo.MinValue || at.Max > AttrInfo.MaxValue || at.Min >= at.Max) Fail("attributes.min/max must be within 1-99.");
            if (!at.GoalkeepingAttributesForOutfield.IsValid || at.GoalkeepingAttributesForOutfield.Min < at.Min) Fail("goalkeepingAttributesForOutfield invalid.");
            if (at.RoleNoise < 0 || at.OffRoleNoise < 0 || at.OffRoleGap < 0 || at.OutfieldGapForGoalkeepers < 0) Fail("Attribute noise/gaps must be >= 0.");
            if (at.OvrCorrectionPasses < 1) Fail("ovrCorrectionPasses must be >= 1.");

            var pot = g.Potential;
            if (pot.Min < AttrInfo.MinValue || pot.Max > AttrInfo.MaxValue || pot.Min > pot.Max) Fail("potential min/max must be within 1-99.");
            if (!pot.YouthBonus.IsValid || pot.YouthBonus.Min < 0) Fail("potential.youthBonus invalid.");
            if (pot.BonusByAge.Count == 0) Fail("potential.bonusByAge cannot be empty.");
            for (int i = 0; i < pot.BonusByAge.Count; i++)
            {
                var b = pot.BonusByAge[i];
                if (!b.Bonus.IsValid || b.Bonus.Min < 0) Fail($"potential.bonusByAge[{i}] invalid bonus.");
                if (i > 0 && b.MaxAge <= pot.BonusByAge[i - 1].MaxAge) Fail("potential.bonusByAge must be ordered by maxAge.");
            }
            if (pot.BonusByAge.Count > 0 && pot.BonusByAge[pot.BonusByAge.Count - 1].MaxAge < Math.Max(sq.VeteranAge.Max, sq.RegularAge.Max))
                Fail("potential.bonusByAge must cover every generated age.");

            if (g.SecondaryCountWeights.Count != 3 || Sum(g.SecondaryCountWeights) <= 0) Fail("secondaryPositions.countWeights must have 3 entries (0, 1, 2) with a positive sum.");
            foreach (var x in g.SecondaryCountWeights) if (x < 0) Fail("secondaryPositions.countWeights must be >= 0.");
            for (int p = 0; p < PositionInfo.Count; p++)
            {
                var c = g.SecondaryCandidates[p];
                if (c == null) { Fail($"secondaryPositions.candidates is missing {(Position)p}."); continue; }
                foreach (var to in c) if ((int)to == p) Fail($"{(Position)p} cannot be its own secondary position.");
                CheckUnique(ToStrings(c), "secondary candidates of " + (Position)p, Fail);
            }

            for (int p = 0; p < PositionInfo.Count; p++)
            {
                var h = g.HeightCm[p];
                if (!h.IsValid || h.Min < 150 || h.Max > 210) Fail($"heightCm.{(Position)p} must be within 150-210.");
                float left = g.LeftFootedChance[p];
                if (left < 0f || left > 1f) Fail($"leftFootedChance.{(Position)p} must be within 0-1.");
            }
            if (g.WeakFootWeights.Count != 5 || Sum(g.WeakFootWeights) <= 0) Fail("weakFootWeights must have 5 entries (1-5) with a positive sum.");
            foreach (var x in g.WeakFootWeights) if (x < 0) Fail("weakFootWeights must be >= 0.");

            return e;
        }

        private static int Sum(IReadOnlyList<int> xs) { int s = 0; foreach (var x in xs) s += x; return s; }

        private static List<string> ToStrings(IReadOnlyList<Position> ps)
        {
            var l = new List<string>();
            foreach (var p in ps) l.Add(p.ToString());
            return l;
        }

        private static void CheckUnique(IReadOnlyList<string> items, string what, Action<string> fail)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var i in items)
                if (!seen.Add(i ?? string.Empty)) fail($"Duplicate entry '{i}' in {what}.");
        }

        private static void CheckIncreasing(IReadOnlyList<int> xs, string what, Action<string> fail)
        {
            for (int i = 1; i < xs.Count; i++)
                if (xs[i] <= xs[i - 1]) fail($"{what} must be strictly increasing.");
        }
    }
}
