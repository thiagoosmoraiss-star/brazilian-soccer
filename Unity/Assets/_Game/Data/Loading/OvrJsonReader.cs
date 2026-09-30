using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Ovr;

namespace Game.Data.Loading
{
    /// <summary>Reads and validates Data/Balance/ovr.json.</summary>
    public static class OvrJsonReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidOvr = "INVALID_OVR";

        public static Result<OvrDefinition> Read(string json, string file = GameDataLoader.OvrFile)
        {
            var j = new StrictJson(file);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "secondaryPositionFactor", "outOfPositionFactor", "positions");
            if (root == null) return Result<OvrDefinition>.Fail(j.Errors);

            float secondary = j.Float(root, "secondaryPositionFactor", "root");
            float outOf = j.Float(root, "outOfPositionFactor", "root");
            var weights = new float[PositionInfo.Count][];
            var positions = j.Array(root, "positions", "root");
            if (positions != null)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    string path = $"positions[{i}]";
                    var p = j.AsObject(positions[i], path);
                    if (p == null) continue;
                    j.Keys(p, path, "position", "weights");
                    if (!j.TryEnum(j.String(p, "position", path), path + ".position", out Position pos)) continue;
                    if (weights[(int)pos] != null) { j.Fail(InvalidOvr, $"{path}: duplicate position {pos}."); continue; }
                    var w = new float[AttrInfo.Count];
                    var list = j.Array(p, "weights", path);
                    if (list == null) continue;
                    if (list.Count == 0) j.Fail(InvalidOvr, $"{path}: at least one weight is required.");
                    for (int k = 0; k < list.Count; k++)
                    {
                        string wp = $"{path}.weights[{k}]";
                        var entry = j.AsObject(list[k], wp);
                        if (entry == null) continue;
                        j.Keys(entry, wp, "attribute", "weight");
                        float value = j.Float(entry, "weight", wp);
                        if (!j.TryEnum(j.String(entry, "attribute", wp), wp + ".attribute", out Attr attr)) continue;
                        if (w[(int)attr] != 0f) j.Fail(InvalidOvr, $"{wp}: duplicate attribute {attr}.");
                        if (!(value > 0f)) j.Fail(InvalidOvr, $"{wp}: weight must be > 0.");
                        w[(int)attr] = value;
                    }
                    weights[(int)pos] = w;
                }
            }

            for (int p = 0; p < PositionInfo.Count; p++)
                if (weights[p] == null) j.Fail(InvalidOvr, $"Position {(Position)p} has no OVR weights.");
            if (!(secondary > 0f && secondary <= 1f)) j.Fail(InvalidOvr, "secondaryPositionFactor must be in (0, 1].");
            if (!(outOf > 0f && outOf <= secondary)) j.Fail(InvalidOvr, "outOfPositionFactor must be in (0, secondaryPositionFactor].");

            return j.Ok
                ? Result<OvrDefinition>.Ok(new OvrDefinition(weights, secondary, outOf))
                : Result<OvrDefinition>.Fail(j.Errors);
        }
    }
}
