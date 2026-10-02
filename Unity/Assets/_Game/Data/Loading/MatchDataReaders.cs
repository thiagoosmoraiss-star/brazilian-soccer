using System;
using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Match;
using Newtonsoft.Json.Linq;

namespace Game.Data.Loading
{
    /// <summary>Strict readers + semantic checks for formations.json, match_rules.json and quicksim.json.</summary>
    public static class MatchDataReaders
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidMatchData = "INVALID_MATCH_DATA";

        // ---------------- formations.json ----------------

        public static Result<IReadOnlyList<FormationDefinition>> ReadFormations(string json)
        {
            var j = new StrictJson(GameDataLoader.FormationsFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "formations");
            var list = new List<FormationDefinition>();
            var arr = root == null ? null : j.Array(root, "formations", "root");
            if (arr != null)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < arr.Count; i++)
                {
                    string path = $"formations[{i}]";
                    var o = j.AsObject(arr[i], path);
                    if (o == null) continue;
                    j.Keys(o, path, "id", "slots");
                    string id = j.String(o, "id", path);
                    if (id != null && !ids.Add(id)) j.Fail(InvalidMatchData, $"{path}: duplicate formation '{id}'.");
                    var slots = new List<FormationSlot>();
                    var sarr = j.Array(o, "slots", path);
                    if (sarr != null)
                        for (int k = 0; k < sarr.Count; k++)
                        {
                            string sp = $"{path}.slots[{k}]";
                            var so = j.AsObject(sarr[k], sp);
                            if (so == null) continue;
                            j.Keys(so, sp, "position", "role", "x", "y");
                            j.TryEnum(j.String(so, "position", sp), sp + ".position", out Position pos);
                            j.TryEnum(j.String(so, "role", sp), sp + ".role", out FormationRole role);
                            var slot = new FormationSlot { Position = pos, Role = role, X = j.Float(so, "x", sp), Y = j.Float(so, "y", sp) };
                            if (slot.X < 0f || slot.X > 1f || slot.Y < 0f || slot.Y > 1f) j.Fail(InvalidMatchData, $"{sp}: x/y must be within 0-1.");
                            slots.Add(slot);
                        }
                    if (slots.Count != 11) j.Fail(InvalidMatchData, $"{path}: a formation needs 11 slots, got {slots.Count}.");
                    int keepers = 0;
                    foreach (var s in slots) if (s.Role == FormationRole.GK) keepers++;
                    if (keepers != 1) j.Fail(InvalidMatchData, $"{path}: a formation needs exactly one GK slot.");
                    list.Add(new FormationDefinition { Id = id, Slots = slots });
                }
                if (list.Count == 0) j.Fail(InvalidMatchData, "At least one formation is required.");
            }
            return j.Ok ? Result<IReadOnlyList<FormationDefinition>>.Ok(list) : Result<IReadOnlyList<FormationDefinition>>.Fail(j.Errors);
        }

        // ---------------- match_rules.json ----------------

        public static Result<MatchRulesDefinition> ReadMatchRules(string json)
        {
            var j = new StrictJson(GameDataLoader.MatchRulesFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "sectors", "roleSectorWeights", "sectorReferenceWeights",
                "condition", "fatigue", "discipline", "injuries", "ratings", "substitutions");
            if (root == null) return Result<MatchRulesDefinition>.Fail(j.Errors);
            var d = new MatchRulesDefinition();
            void Check(bool ok, string message) { if (!ok) j.Fail(InvalidMatchData, message); }

            // Sectors: attribute weights, normalized.
            var sectors = j.Object(root, "sectors", "root");
            d.SectorAttributeWeights = new float[SectorInfo.Count][];
            var seenAttr = new bool[AttrInfo.Count];
            if (sectors != null)
            {
                foreach (var prop in sectors.Properties())
                {
                    if (!j.TryEnum(prop.Name, "sectors." + prop.Name, out Sector sector)) continue;
                    var w = j.AttributeWeights(sectors, prop.Name, "sectors");
                    float sum = 0f;
                    for (int a = 0; a < w.Length; a++) { sum += w[a]; if (w[a] > 0f) seenAttr[a] = true; }
                    Check(sum > 0f, $"sectors.{prop.Name} needs at least one weight.");
                    if (sum > 0f) for (int a = 0; a < w.Length; a++) w[a] /= sum;
                    d.SectorAttributeWeights[(int)sector] = w;
                }
                for (int s = 0; s < SectorInfo.Count; s++)
                    Check(d.SectorAttributeWeights[s] != null, $"sectors is missing {(Sector)s}.");
                for (int a = 0; a < AttrInfo.Count; a++)
                    Check(seenAttr[a], $"Attribute {(Attr)a} does not feed any sector (every attribute must matter in the QuickSim).");
            }

            // Role -> sector contributions.
            d.RoleSectorWeights = new float[FormationRoleInfo.Count][];
            var roles = j.Object(root, "roleSectorWeights", "root");
            if (roles != null)
            {
                foreach (var prop in roles.Properties())
                    if (j.TryEnum(prop.Name, "roleSectorWeights." + prop.Name, out FormationRole role))
                        d.RoleSectorWeights[(int)role] = j.FloatsByEnum<Sector>(roles, prop.Name, "roleSectorWeights", requireAll: false);
                for (int ri = 0; ri < FormationRoleInfo.Count; ri++)
                    Check(d.RoleSectorWeights[ri] != null, $"roleSectorWeights is missing {(FormationRole)ri}.");
            }
            d.SectorReferenceWeights = j.FloatsByEnum<Sector>(root, "sectorReferenceWeights", "root");
            foreach (var w in d.SectorReferenceWeights) Check(w > 0f, "sectorReferenceWeights must be > 0.");

            var c = j.Object(root, "condition", "root");
            if (c != null)
            {
                j.Keys(c, "condition", "moraleFactors", "formNeutral", "formFactorPerPoint", "formFactorRange", "lowEnergyThreshold");
                var range = j.FloatList(c, "formFactorRange", "condition");
                d.Condition = new ConditionRules
                {
                    MoraleFactors = j.FloatList(c, "moraleFactors", "condition"),
                    FormNeutral = j.Float(c, "formNeutral", "condition"),
                    FormFactorPerPoint = j.Float(c, "formFactorPerPoint", "condition"),
                    FormFactorMin = range.Count == 2 ? range[0] : 0f,
                    FormFactorMax = range.Count == 2 ? range[1] : 0f,
                    LowEnergyThreshold = j.Float(c, "lowEnergyThreshold", "condition"),
                };
                Check(d.Condition.MoraleFactors.Count == 5, "condition.moraleFactors needs 5 values (morale 1-5).");
                Check(range.Count == 2 && range[0] <= 1f && range[1] >= 1f, "condition.formFactorRange must be [min <= 1, max >= 1].");
            }

            var f = j.Object(root, "fatigue", "root");
            if (f != null)
            {
                j.Keys(f, "fatigue", "baseDrainPerMinute", "mentalityIntensity", "pressureIntensity", "halftimeRecovery");
                d.Fatigue = new FatigueRules
                {
                    BaseDrainPerMinute = j.Float(f, "baseDrainPerMinute", "fatigue"),
                    MentalityIntensity = j.FloatList(f, "mentalityIntensity", "fatigue"),
                    PressureIntensity = j.FloatList(f, "pressureIntensity", "fatigue"),
                    HalftimeRecovery = j.Float(f, "halftimeRecovery", "fatigue"),
                };
                Check(d.Fatigue.MentalityIntensity.Count == 5 && d.Fatigue.PressureIntensity.Count == 3, "fatigue intensity tables need 5 (mentality) and 3 (pressure) values.");
                Check(d.Fatigue.BaseDrainPerMinute > 0f, "fatigue.baseDrainPerMinute must be > 0.");
            }

            var di = j.Object(root, "discipline", "root");
            if (di != null)
            {
                j.Keys(di, "discipline", "yellowPerFoul", "straightRedPerFoul", "bookedCardFactor");
                d.Discipline = new DisciplineRules
                {
                    YellowPerFoul = j.Float(di, "yellowPerFoul", "discipline"),
                    StraightRedPerFoul = j.Float(di, "straightRedPerFoul", "discipline"),
                    BookedCardFactor = j.Float(di, "bookedCardFactor", "discipline"),
                };
                Check(d.Discipline.YellowPerFoul >= 0f && d.Discipline.StraightRedPerFoul >= 0f &&
                      d.Discipline.YellowPerFoul + d.Discipline.StraightRedPerFoul <= 1f &&
                      d.Discipline.BookedCardFactor >= 0f && d.Discipline.BookedCardFactor <= 1f, "discipline probabilities must be within 0-1.");
            }

            var inj = j.Object(root, "injuries", "root");
            if (inj != null)
            {
                j.Keys(inj, "injuries", "hazardPerPlayerMinute", "lowEnergyThreshold", "lowEnergyMultiplier", "severityWeights");
                var sev = j.Object(inj, "severityWeights", "injuries");
                if (sev != null) j.Keys(sev, "injuries.severityWeights", "Light", "Medium", "Severe");
                d.Injuries = new InjuryRules
                {
                    HazardPerPlayerMinute = j.Float(inj, "hazardPerPlayerMinute", "injuries"),
                    LowEnergyThreshold = j.Float(inj, "lowEnergyThreshold", "injuries"),
                    LowEnergyMultiplier = j.Float(inj, "lowEnergyMultiplier", "injuries"),
                    LightWeight = sev == null ? 0 : j.Int(sev, "Light", "injuries.severityWeights"),
                    MediumWeight = sev == null ? 0 : j.Int(sev, "Medium", "injuries.severityWeights"),
                    SevereWeight = sev == null ? 0 : j.Int(sev, "Severe", "injuries.severityWeights"),
                };
                Check(d.Injuries.HazardPerPlayerMinute >= 0f && d.Injuries.HazardPerPlayerMinute < 0.05f, "injuries.hazardPerPlayerMinute out of range.");
                Check(d.Injuries.LightWeight >= 0 && d.Injuries.MediumWeight >= 0 && d.Injuries.SevereWeight >= 0 &&
                      d.Injuries.LightWeight + d.Injuries.MediumWeight + d.Injuries.SevereWeight > 0, "injury severity weights must be >= 0 with a positive sum.");
            }

            var r = j.Object(root, "ratings", "root");
            if (r != null)
            {
                j.Keys(r, "ratings", "base", "goal", "assist", "shotOnTarget", "shotOffTarget", "foul", "yellow", "red", "win", "loss",
                    "cleanSheet", "goalConceded", "defensiveRoles", "noise", "fullImpactMinutes", "min", "max");
                var defRoles = new List<FormationRole>();
                foreach (var name in j.StringList(r, "defensiveRoles", "ratings"))
                    if (j.TryEnum(name, "ratings.defensiveRoles", out FormationRole role)) defRoles.Add(role);
                d.Ratings = new RatingRules
                {
                    Base = j.Float(r, "base", "ratings"), Goal = j.Float(r, "goal", "ratings"), Assist = j.Float(r, "assist", "ratings"),
                    ShotOnTarget = j.Float(r, "shotOnTarget", "ratings"), ShotOffTarget = j.Float(r, "shotOffTarget", "ratings"),
                    Foul = j.Float(r, "foul", "ratings"), Yellow = j.Float(r, "yellow", "ratings"), Red = j.Float(r, "red", "ratings"),
                    Win = j.Float(r, "win", "ratings"), Loss = j.Float(r, "loss", "ratings"), CleanSheet = j.Float(r, "cleanSheet", "ratings"),
                    GoalConceded = j.Float(r, "goalConceded", "ratings"), DefensiveRoles = defRoles, Noise = j.Float(r, "noise", "ratings"),
                    FullImpactMinutes = j.Int(r, "fullImpactMinutes", "ratings"), Min = j.Float(r, "min", "ratings"), Max = j.Float(r, "max", "ratings"),
                };
                Check(d.Ratings.Min >= 0f && d.Ratings.Max <= 10f && d.Ratings.Min < d.Ratings.Max, "ratings min/max must be within 0-10 (X-38).");
                Check(d.Ratings.Base > d.Ratings.Min && d.Ratings.Base < d.Ratings.Max, "ratings.base must be inside min-max.");
                Check(d.Ratings.FullImpactMinutes > 0 && d.Ratings.Noise >= 0f, "ratings.fullImpactMinutes > 0 and noise >= 0.");
            }

            var sub = j.Object(root, "substitutions", "root");
            if (sub != null)
            {
                j.Keys(sub, "substitutions", "windows", "maxWindows", "halftimeMinute", "energyThreshold", "maxTiredSubstitutions", "benchOvrMargin");
                d.Substitutions = new SubstitutionRules
                {
                    Windows = j.IntList(sub, "windows", "substitutions"),
                    MaxWindows = j.Int(sub, "maxWindows", "substitutions"),
                    HalftimeMinute = j.Int(sub, "halftimeMinute", "substitutions"),
                    EnergyThreshold = j.Float(sub, "energyThreshold", "substitutions"),
                    MaxTiredSubstitutions = j.Int(sub, "maxTiredSubstitutions", "substitutions"),
                    BenchOvrMargin = j.Float(sub, "benchOvrMargin", "substitutions"),
                };
                for (int i = 1; i < d.Substitutions.Windows.Count; i++)
                    Check(d.Substitutions.Windows[i] > d.Substitutions.Windows[i - 1], "substitutions.windows must be increasing.");
                Check(d.Substitutions.MaxWindows >= 0, "substitutions.maxWindows must be >= 0.");
            }

            return j.Ok ? Result<MatchRulesDefinition>.Ok(d) : Result<MatchRulesDefinition>.Fail(j.Errors);
        }

        // ---------------- quicksim.json ----------------

        public static Result<QuickSimDefinition> ReadQuickSim(string json)
        {
            var j = new StrictJson(GameDataLoader.QuickSimFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "minutes", "homeAdvantage", "possessionExponent",
                "attackChancePerMinute", "attackRatioExponent", "playTypes", "aerialDuelExponent", "blockChance", "corners",
                "penalties", "accuracyExponent", "keeperExponent", "composureExponent", "foulsPerMinute", "defendingTeamFoulShare",
                "pressureFoulMult", "mentalityAttack", "mentalityDefense", "lineDefense", "linePossession", "pressurePossession",
                "pressureDefense", "maxGoalsPerTeam", "noKeeperGoalMultiplier", "maxAttackChance", "maxShotProbability", "shooterWeights", "headerWeights", "assistWeights", "crossWeights", "foulWeights");
            if (root == null) return Result<QuickSimDefinition>.Fail(j.Errors);
            void Check(bool ok, string message) { if (!ok) j.Fail(InvalidMatchData, message); }
            bool Prob(float p) => p >= 0f && p <= 1f;

            var d = new QuickSimDefinition
            {
                Minutes = j.Int(root, "minutes", "root"),
                HomeAdvantage = j.Float(root, "homeAdvantage", "root"),
                PossessionExponent = j.Float(root, "possessionExponent", "root"),
                AttackChancePerMinute = j.Float(root, "attackChancePerMinute", "root"),
                AttackRatioExponent = j.Float(root, "attackRatioExponent", "root"),
                AerialDuelExponent = j.Float(root, "aerialDuelExponent", "root"),
                BlockChance = j.Float(root, "blockChance", "root"),
                AccuracyExponent = j.Float(root, "accuracyExponent", "root"),
                KeeperExponent = j.Float(root, "keeperExponent", "root"),
                ComposureExponent = j.Float(root, "composureExponent", "root"),
                FoulsPerMinute = j.Float(root, "foulsPerMinute", "root"),
                DefendingTeamFoulShare = j.Float(root, "defendingTeamFoulShare", "root"),
                PressureFoulMult = j.FloatList(root, "pressureFoulMult", "root"),
                MentalityAttack = j.FloatList(root, "mentalityAttack", "root"),
                MentalityDefense = j.FloatList(root, "mentalityDefense", "root"),
                LineDefense = j.FloatList(root, "lineDefense", "root"),
                LinePossession = j.FloatList(root, "linePossession", "root"),
                PressurePossession = j.FloatList(root, "pressurePossession", "root"),
                PressureDefense = j.FloatList(root, "pressureDefense", "root"),
                MaxGoalsPerTeam = j.Int(root, "maxGoalsPerTeam", "root"),
                NoKeeperGoalMultiplier = j.Float(root, "noKeeperGoalMultiplier", "root"),
                MaxAttackChance = j.Float(root, "maxAttackChance", "root"),
                MaxShotProbability = j.Float(root, "maxShotProbability", "root"),
                ShooterWeights = j.FloatsByEnum<Position>(root, "shooterWeights", "root"),
                HeaderWeights = j.FloatsByEnum<Position>(root, "headerWeights", "root"),
                AssistWeights = j.FloatsByEnum<Position>(root, "assistWeights", "root"),
                CrossWeights = j.FloatsByEnum<Position>(root, "crossWeights", "root"),
                FoulWeights = j.FloatsByEnum<Position>(root, "foulWeights", "root"),
            };

            var types = new PlayTypeParameters[3];
            var arr = j.Array(root, "playTypes", "root");
            if (arr != null)
                for (int i = 0; i < arr.Count; i++)
                {
                    string path = $"playTypes[{i}]";
                    var o = j.AsObject(arr[i], path);
                    if (o == null) continue;
                    j.Keys(o, path, "type", "weight", "onTarget", "goalGivenOnTarget", "assistChance");
                    if (!j.TryEnum(j.String(o, "type", path), path + ".type", out PlayType t)) continue;
                    var p = new PlayTypeParameters
                    {
                        Type = t, Weight = j.Float(o, "weight", path), OnTarget = j.Float(o, "onTarget", path),
                        GoalGivenOnTarget = j.Float(o, "goalGivenOnTarget", path), AssistChance = j.Float(o, "assistChance", path),
                    };
                    Check(types[(int)t] == null, $"{path}: duplicate play type {t}.");
                    Check(p.Weight >= 0f && Prob(p.OnTarget) && Prob(p.GoalGivenOnTarget) && Prob(p.AssistChance), $"{path}: invalid probabilities.");
                    types[(int)t] = p;
                }
            for (int t = 0; t < types.Length; t++) Check(types[t] != null, $"playTypes is missing {(PlayType)t}.");
            d.PlayTypes = types;

            var c = j.Object(root, "corners", "root");
            if (c != null)
            {
                j.Keys(c, "corners", "fromBlock", "fromSave", "fromAttackWithoutShot", "shotChance", "onTarget", "goalGivenOnTarget", "assistChance");
                d.Corners = new CornerParameters
                {
                    FromBlock = j.Float(c, "fromBlock", "corners"), FromSave = j.Float(c, "fromSave", "corners"),
                    FromAttackWithoutShot = j.Float(c, "fromAttackWithoutShot", "corners"), ShotChance = j.Float(c, "shotChance", "corners"),
                    OnTarget = j.Float(c, "onTarget", "corners"), GoalGivenOnTarget = j.Float(c, "goalGivenOnTarget", "corners"),
                    AssistChance = j.Float(c, "assistChance", "corners"),
                };
                Check(Prob(d.Corners.FromBlock) && Prob(d.Corners.FromSave) && Prob(d.Corners.FromAttackWithoutShot) && Prob(d.Corners.ShotChance)
                      && Prob(d.Corners.OnTarget) && Prob(d.Corners.GoalGivenOnTarget) && Prob(d.Corners.AssistChance), "corners: probabilities must be within 0-1.");
            }
            var pen = j.Object(root, "penalties", "root");
            if (pen != null)
            {
                j.Keys(pen, "penalties", "perDefensiveFoul", "onTarget", "goalGivenOnTarget");
                d.Penalties = new PenaltyParameters
                {
                    PerDefensiveFoul = j.Float(pen, "perDefensiveFoul", "penalties"), OnTarget = j.Float(pen, "onTarget", "penalties"),
                    GoalGivenOnTarget = j.Float(pen, "goalGivenOnTarget", "penalties"),
                };
                Check(Prob(d.Penalties.PerDefensiveFoul) && Prob(d.Penalties.OnTarget) && Prob(d.Penalties.GoalGivenOnTarget), "penalties: probabilities must be within 0-1.");
            }

            Check(d.Minutes > 0, "minutes must be > 0.");
            Check(Prob(d.AttackChancePerMinute) && Prob(d.BlockChance) && Prob(d.DefendingTeamFoulShare), "probabilities must be within 0-1.");
            Check(d.FoulsPerMinute >= 0f && d.FoulsPerMinute < 1f, "foulsPerMinute must be within 0-1.");
            Check(d.MentalityAttack.Count == 5 && d.MentalityDefense.Count == 5, "mentality tables need 5 values.");
            Check(d.LineDefense.Count == 3 && d.LinePossession.Count == 3 && d.PressurePossession.Count == 3 &&
                  d.PressureDefense.Count == 3 && d.PressureFoulMult.Count == 3, "line/pressure tables need 3 values.");
            Check(d.MaxGoalsPerTeam > 0, "maxGoalsPerTeam must be > 0.");
            Check(Prob(d.MaxAttackChance) && Prob(d.MaxShotProbability) && d.NoKeeperGoalMultiplier >= 1f, "caps must be within 0-1; noKeeperGoalMultiplier >= 1.");

            return j.Ok ? Result<QuickSimDefinition>.Ok(d) : Result<QuickSimDefinition>.Fail(j.Errors);
        }
    }
}
