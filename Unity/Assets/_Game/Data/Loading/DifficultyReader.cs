using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/difficulty.json (F1c).</summary>
    public static class DifficultyReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidDifficulty = "INVALID_DIFFICULTY";

        public static Result<DifficultyDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.DifficultyFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "reference", "default", "levels");
            if (root == null) return Result<DifficultyDefinition>.Fail(j.Errors);
            var d = new DifficultyDefinition
            {
                ReferenceLevel = j.String(root, "reference", "root"),
                DefaultLevel = j.String(root, "default", "root"),
            };
            var levels = new List<DifficultyLevelDefinition>();
            var arr = j.Array(root, "levels", "root");
            if (arr != null)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    string path = $"levels[{i}]";
                    var o = j.AsObject(arr[i], path);
                    if (o == null) continue;
                    j.Keys(o, path, "id", "match", "quickSim");
                    var l = new DifficultyLevelDefinition { Id = j.String(o, "id", path) };
                    var m = j.Object(o, "match", path);
                    if (m != null)
                    {
                        string mp = path + ".match";
                        j.Keys(m, mp, "decisionNoiseScale", "decisionIntervalScale", "reactionScale", "positionErrorScale", "keeperReactionScale");
                        l.DecisionNoiseScale = j.Float(m, "decisionNoiseScale", mp);
                        l.DecisionIntervalScale = j.Float(m, "decisionIntervalScale", mp);
                        l.ReactionScale = j.Float(m, "reactionScale", mp);
                        l.PositionErrorScale = j.Float(m, "positionErrorScale", mp);
                        l.KeeperReactionScale = j.Float(m, "keeperReactionScale", mp);
                    }
                    var q = j.Object(o, "quickSim", path);
                    if (q != null)
                    {
                        j.Keys(q, path + ".quickSim", "opponentStrengthScale");
                        l.QuickSimStrengthScale = j.Float(q, "opponentStrengthScale", path + ".quickSim");
                    }
                    levels.Add(l);
                }
            }
            d.Levels = levels;
            if (!j.Ok) return Result<DifficultyDefinition>.Fail(j.Errors);

            bool ok = levels.Count > 0;
            var ids = new HashSet<string>();
            foreach (var l in levels)
                ok &= !string.IsNullOrEmpty(l.Id) && ids.Add(l.Id) && l.DecisionNoiseScale >= 0f && l.DecisionIntervalScale > 0f
                      && l.ReactionScale >= 0f && l.PositionErrorScale >= 0f && l.KeeperReactionScale >= 0f && l.QuickSimStrengthScale > 0f;
            var reference = d.Level(d.ReferenceLevel ?? "");
            ok &= reference != null && reference.IsNeutral && d.Level(d.DefaultLevel ?? "") != null;
            if (!ok)
            {
                j.Fail(InvalidDifficulty, "difficulty values invalid (unique non-empty ids, factors >= 0 and interval/strength > 0, "
                                          + "reference and default must be levels, the reference level's factors all exactly 1).");
                return Result<DifficultyDefinition>.Fail(j.Errors);
            }
            return Result<DifficultyDefinition>.Ok(d);
        }
    }
}
