using System;
using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader for Data/VerticalSlice/teams.json: 11 players per team in formation slot order, each with
    /// the role of his slot and one 1-99 value per attribute (the file names the order, which must be the Attr enum's).</summary>
    public static class VerticalSliceReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidVerticalSlice = "INVALID_VERTICAL_SLICE";

        public static Result<VerticalSliceDefinition> Read(string json, GameDatabase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            var j = new StrictJson(GameDataLoader.VerticalSliceTeamsFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "attributeOrder", "teams");
            if (root == null) return Result<VerticalSliceDefinition>.Fail(j.Errors);

            var order = root["attributeOrder"] as Newtonsoft.Json.Linq.JArray;
            if (order == null || order.Count != AttrInfo.Count) j.Fail(InvalidVerticalSlice, "attributeOrder must list the " + AttrInfo.Count + " attributes.");
            else
                for (int a = 0; a < AttrInfo.Count; a++)
                    if (j.AsString(order[a], "attributeOrder[" + a + "]") != ((Attr)a).ToString())
                        j.Fail(InvalidVerticalSlice, $"attributeOrder[{a}] must be {(Attr)a}.");

            var teams = new List<VerticalSliceTeam>();
            var keys = new HashSet<string>();
            var arr = j.Array(root, "teams", "root");
            for (int t = 0; arr != null && t < arr.Count; t++)
            {
                string path = "teams[" + t + "]";
                var to = j.AsObject(arr[t], path);
                if (to == null) continue;
                j.Keys(to, path, "key", "id", "name", "formation", "players");
                var team = new VerticalSliceTeam
                {
                    Key = j.String(to, "key", path),
                    Id = j.Int(to, "id", path),
                    Name = j.String(to, "name", path),
                    Formation = j.String(to, "formation", path),
                };
                if (team.Key != null && !keys.Add(team.Key)) j.Fail(InvalidVerticalSlice, $"{path}: duplicate key '{team.Key}'.");
                var formation = team.Formation == null ? null : db.Formation(team.Formation);
                if (team.Formation != null && formation == null) j.Fail(InvalidVerticalSlice, $"{path}: unknown formation '{team.Formation}'.");

                var players = new List<VerticalSlicePlayer>();
                var pa = j.Array(to, "players", path);
                for (int i = 0; pa != null && i < pa.Count; i++)
                {
                    string pp = $"{path}.players[{i}]";
                    var po = j.AsObject(pa[i], pp);
                    if (po == null) continue;
                    j.Keys(po, pp, "id", "name", "role", "leftFooted", "weakFoot", "attributes");
                    var p = new VerticalSlicePlayer
                    {
                        Id = j.Int(po, "id", pp),
                        Name = j.String(po, "name", pp),
                        LeftFooted = j.Bool(po, "leftFooted", pp),
                        WeakFoot = j.Int(po, "weakFoot", pp),
                        Attributes = j.IntList(po, "attributes", pp),
                    };
                    if (j.TryEnum(j.String(po, "role", pp), pp + ".role", out FormationRole role)) p.Role = role;
                    if (p.WeakFoot < 1 || p.WeakFoot > 5) j.Fail(InvalidVerticalSlice, $"{pp}.weakFoot must be 1-5.");
                    if (p.Attributes == null || p.Attributes.Count != AttrInfo.Count) j.Fail(InvalidVerticalSlice, $"{pp}.attributes must have {AttrInfo.Count} values.");
                    else
                        for (int a = 0; a < p.Attributes.Count; a++)
                            if (p.Attributes[a] < 1 || p.Attributes[a] > 99) j.Fail(InvalidVerticalSlice, $"{pp}.attributes[{a}] must be 1-99.");
                    if (formation != null && i < formation.Slots.Count && formation.Slots[i].Role != p.Role)
                        j.Fail(InvalidVerticalSlice, $"{pp}.role {p.Role} does not match slot {i} of {team.Formation} ({formation.Slots[i].Role}).");
                    players.Add(p);
                }
                if (players.Count != Core.Contracts.Match.MatchTeamSetup.StarterCount)
                    j.Fail(InvalidVerticalSlice, $"{path} must have {Core.Contracts.Match.MatchTeamSetup.StarterCount} players.");
                team.Players = players;
                teams.Add(team);
            }
            if (!j.Ok) return Result<VerticalSliceDefinition>.Fail(j.Errors);
            return Result<VerticalSliceDefinition>.Ok(new VerticalSliceDefinition { Teams = teams });
        }
    }
}
