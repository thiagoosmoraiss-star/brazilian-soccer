using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.World;
using Newtonsoft.Json.Linq;

namespace Game.Data.Loading
{
    /// <summary>Reads Data/World/*.json into a <see cref="WorldDefinition"/> (syntax and structure only).</summary>
    public static class WorldJsonReader
    {
        public const int SupportedSchemaVersion = 1;

        public static Result<WorldDefinition> Read(string names, string cities, string clubTemplates, string crestTemplates, string generation)
        {
            var errors = new List<Error>();
            var def = new WorldDefinition
            {
                Names = ReadNames(names, errors),
                Cities = ReadCities(cities, errors),
                ClubTemplates = ReadClubTemplates(clubTemplates, errors),
                CrestTemplates = ReadCrestTemplates(crestTemplates, errors),
                Generation = ReadGeneration(generation, errors),
            };
            return errors.Count > 0 ? Result<WorldDefinition>.Fail(errors) : Result<WorldDefinition>.Ok(def);
        }

        private static NamePools ReadNames(string json, List<Error> errors)
        {
            var j = new StrictJson(GameDataLoader.NamesFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "firstNames", "lastNames", "nationalities");
            var pools = new NamePools();
            if (root != null)
            {
                pools.FirstNames = j.StringList(root, "firstNames", "root");
                pools.LastNames = j.StringList(root, "lastNames", "root");
                var nat = new List<WeightedCode>();
                var arr = j.Array(root, "nationalities", "root");
                if (arr != null)
                    for (int i = 0; i < arr.Count; i++)
                    {
                        string path = $"nationalities[{i}]";
                        var o = j.AsObject(arr[i], path);
                        if (o == null) continue;
                        j.Keys(o, path, "code", "weight");
                        nat.Add(new WeightedCode(j.String(o, "code", path), j.Int(o, "weight", path)));
                    }
                pools.Nationalities = nat;
            }
            errors.AddRange(j.Errors);
            return pools;
        }

        private static IReadOnlyList<CityDefinition> ReadCities(string json, List<Error> errors)
        {
            var j = new StrictJson(GameDataLoader.CitiesFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "cities");
            var list = new List<CityDefinition>();
            var arr = root == null ? null : j.Array(root, "cities", "root");
            if (arr != null)
                for (int i = 0; i < arr.Count; i++)
                {
                    string path = $"cities[{i}]";
                    var o = j.AsObject(arr[i], path);
                    if (o == null) continue;
                    j.Keys(o, path, "name", "uf");
                    list.Add(new CityDefinition { Name = j.String(o, "name", path), Uf = j.String(o, "uf", path) });
                }
            errors.AddRange(j.Errors);
            return list;
        }

        private static ClubTemplates ReadClubTemplates(string json, List<Error> errors)
        {
            var j = new StrictJson(GameDataLoader.ClubTemplatesFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "namePatterns", "colors");
            var t = new ClubTemplates();
            if (root != null)
            {
                t.NamePatterns = j.StringList(root, "namePatterns", "root");
                var colors = new List<ColorDefinition>();
                var arr = j.Array(root, "colors", "root");
                if (arr != null)
                    for (int i = 0; i < arr.Count; i++)
                    {
                        string path = $"colors[{i}]";
                        var o = j.AsObject(arr[i], path);
                        if (o == null) continue;
                        j.Keys(o, path, "id", "hex");
                        colors.Add(new ColorDefinition { Id = j.String(o, "id", path), Hex = j.String(o, "hex", path) });
                    }
                t.Colors = colors;
            }
            errors.AddRange(j.Errors);
            return t;
        }

        private static CrestTemplates ReadCrestTemplates(string json, List<Error> errors)
        {
            var j = new StrictJson(GameDataLoader.CrestTemplatesFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "shapes", "symbols");
            var t = new CrestTemplates();
            if (root != null)
            {
                t.Shapes = j.StringList(root, "shapes", "root");
                t.Symbols = j.StringList(root, "symbols", "root");
            }
            errors.AddRange(j.Errors);
            return t;
        }

        private static GenerationParameters ReadGeneration(string json, List<Error> errors)
        {
            var j = new StrictJson(GameDataLoader.GenerationFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "startYear", "divisions", "clubsPerDivision",
                "reputationStarThresholds", "stadiumCapacityByLevel", "clubProfileNoise", "squad", "attributes",
                "potential", "secondaryPositions", "heightCm", "leftFootedChance", "weakFootWeights");
            var g = new GenerationParameters();
            if (root == null)
            {
                errors.AddRange(j.Errors);
                return g;
            }

            g.StartYear = j.Int(root, "startYear", "root");
            g.ClubsPerDivision = j.Int(root, "clubsPerDivision", "root");
            g.ReputationStarThresholds = j.IntList(root, "reputationStarThresholds", "root");
            g.StadiumCapacityByLevel = j.IntList(root, "stadiumCapacityByLevel", "root");
            g.ClubProfileNoise = j.Float(root, "clubProfileNoise", "root");
            g.WeakFootWeights = j.IntList(root, "weakFootWeights", "root");

            var divisions = new List<DivisionParameters>();
            var divArr = j.Array(root, "divisions", "root");
            if (divArr != null)
                for (int i = 0; i < divArr.Count; i++)
                {
                    string path = $"divisions[{i}]";
                    var o = j.AsObject(divArr[i], path);
                    if (o == null) continue;
                    j.Keys(o, path, "name", "squadOvrMean", "reputation", "budget", "stadiumLevel", "fans");
                    var budget = o["budget"] as JArray;
                    LongRange budgetRange = default;
                    if (budget != null && budget.Count == 2 && budget[0].Type == JTokenType.Integer && budget[1].Type == JTokenType.Integer)
                        budgetRange = new LongRange(budget[0].Value<long>(), budget[1].Value<long>());
                    else
                        j.Fail(StrictJson.InvalidStructure, path + ".budget must be an [min, max] integer pair.");
                    divisions.Add(new DivisionParameters
                    {
                        Name = j.String(o, "name", path),
                        SquadOvrMean = j.IntRange(o, "squadOvrMean", path),
                        Reputation = j.IntRange(o, "reputation", path),
                        Budget = budgetRange,
                        StadiumLevel = j.IntRange(o, "stadiumLevel", path),
                        Fans = j.IntRange(o, "fans", path),
                    });
                }
            g.Divisions = divisions;

            var sq = j.Object(root, "squad", "root");
            if (sq != null)
            {
                j.Keys(sq, "squad", "slots", "starterOvrOffset", "reserveOvrOffset", "youthOvrOffset", "veteranOvrOffset",
                    "playerOvrNoise", "youthCount", "youthAge", "veteranCount", "veteranAge", "regularAge");
                var slots = new List<SquadSlot>();
                var slotArr = j.Array(sq, "slots", "squad");
                if (slotArr != null)
                    for (int i = 0; i < slotArr.Count; i++)
                    {
                        string path = $"squad.slots[{i}]";
                        var o = j.AsObject(slotArr[i], path);
                        if (o == null) continue;
                        j.Keys(o, path, "position", "starter");
                        j.TryEnum(j.String(o, "position", path), path + ".position", out Position pos);
                        slots.Add(new SquadSlot { Position = pos, Starter = j.Bool(o, "starter", path) });
                    }
                g.Squad = new SquadParameters
                {
                    Slots = slots,
                    StarterOvrOffset = j.Int(sq, "starterOvrOffset", "squad"),
                    ReserveOvrOffset = j.Int(sq, "reserveOvrOffset", "squad"),
                    YouthOvrOffset = j.Int(sq, "youthOvrOffset", "squad"),
                    VeteranOvrOffset = j.Int(sq, "veteranOvrOffset", "squad"),
                    PlayerOvrNoise = j.Int(sq, "playerOvrNoise", "squad"),
                    YouthCount = j.IntRange(sq, "youthCount", "squad"),
                    YouthAge = j.IntRange(sq, "youthAge", "squad"),
                    VeteranCount = j.IntRange(sq, "veteranCount", "squad"),
                    VeteranAge = j.IntRange(sq, "veteranAge", "squad"),
                    RegularAge = j.IntRange(sq, "regularAge", "squad"),
                };
            }

            var at = j.Object(root, "attributes", "root");
            if (at != null)
            {
                j.Keys(at, "attributes", "roleNoise", "offRoleGap", "offRoleNoise", "goalkeepingAttributesForOutfield",
                    "outfieldGapForGoalkeepers", "min", "max", "ovrCorrectionPasses");
                g.Attributes = new AttributeParameters
                {
                    RoleNoise = j.Int(at, "roleNoise", "attributes"),
                    OffRoleGap = j.Int(at, "offRoleGap", "attributes"),
                    OffRoleNoise = j.Int(at, "offRoleNoise", "attributes"),
                    GoalkeepingAttributesForOutfield = j.IntRange(at, "goalkeepingAttributesForOutfield", "attributes"),
                    OutfieldGapForGoalkeepers = j.Int(at, "outfieldGapForGoalkeepers", "attributes"),
                    Min = j.Int(at, "min", "attributes"),
                    Max = j.Int(at, "max", "attributes"),
                    OvrCorrectionPasses = j.Int(at, "ovrCorrectionPasses", "attributes"),
                };
            }

            var pot = j.Object(root, "potential", "root");
            if (pot != null)
            {
                j.Keys(pot, "potential", "min", "max", "youthBonus", "bonusByAge");
                var bonuses = new List<AgeBonus>();
                var arr = j.Array(pot, "bonusByAge", "potential");
                if (arr != null)
                    for (int i = 0; i < arr.Count; i++)
                    {
                        string path = $"potential.bonusByAge[{i}]";
                        var o = j.AsObject(arr[i], path);
                        if (o == null) continue;
                        j.Keys(o, path, "maxAge", "bonus");
                        bonuses.Add(new AgeBonus { MaxAge = j.Int(o, "maxAge", path), Bonus = j.IntRange(o, "bonus", path) });
                    }
                g.Potential = new PotentialParameters
                {
                    Min = j.Int(pot, "min", "potential"),
                    Max = j.Int(pot, "max", "potential"),
                    YouthBonus = j.IntRange(pot, "youthBonus", "potential"),
                    BonusByAge = bonuses,
                };
            }

            var sec = j.Object(root, "secondaryPositions", "root");
            var candidates = new IReadOnlyList<Position>[PositionInfo.Count];
            if (sec != null)
            {
                j.Keys(sec, "secondaryPositions", "countWeights", "candidates");
                g.SecondaryCountWeights = j.IntList(sec, "countWeights", "secondaryPositions");
                var cand = j.Object(sec, "candidates", "secondaryPositions");
                if (cand != null)
                    foreach (var prop in cand.Properties())
                    {
                        string path = "secondaryPositions.candidates." + prop.Name;
                        if (!j.TryEnum(prop.Name, path, out Position from)) continue;
                        var list = new List<Position>();
                        foreach (var name in j.StringList(cand, prop.Name, "secondaryPositions.candidates"))
                            if (j.TryEnum(name, path, out Position to)) list.Add(to);
                        candidates[(int)from] = list;
                    }
            }
            g.SecondaryCandidates = candidates;

            g.HeightCm = ReadPerPosition(j, root, "heightCm", (t, path) => j.AsIntRange(t, path));
            g.LeftFootedChance = ReadPerPosition(j, root, "leftFootedChance", (t, path) => j.AsFloat(t, path));

            errors.AddRange(j.Errors);
            return g;
        }

        private static T[] ReadPerPosition<T>(StrictJson j, JObject root, string name, System.Func<JToken, string, T> read)
        {
            var result = new T[PositionInfo.Count];
            var seen = new bool[PositionInfo.Count];
            var obj = j.Object(root, name, "root");
            if (obj == null) return result;
            foreach (var prop in obj.Properties())
            {
                string path = name + "." + prop.Name;
                if (!j.TryEnum(prop.Name, path, out Position pos)) continue;
                result[(int)pos] = read(prop.Value, path);
                seen[(int)pos] = true;
            }
            for (int p = 0; p < PositionInfo.Count; p++)
                if (!seen[p]) j.Fail(StrictJson.InvalidStructure, $"{name} is missing position {(Position)p}.");
            return result;
        }
    }
}
