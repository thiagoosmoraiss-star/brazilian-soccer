using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/referee.json.</summary>
    public static class RefereeReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidReferee = "INVALID_REFEREE";

        public static Result<RefereeDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.RefereeFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "foul", "cards", "offside", "freeKick", "penalty");
            if (root == null) return Result<RefereeDefinition>.Fail(j.Errors);
            var d = new RefereeDefinition();

            var f = j.Object(root, "foul", "root");
            if (f != null)
            {
                j.Keys(f, "foul", "strictness", "foulScale", "contactDistance", "minClosingSpeed", "speedReference", "speedMultiplier",
                    "behindAngleDegrees", "behindMultiplier", "challengeCooldownSeconds", "minPlayersOnPitch");
                d.Strictness = j.Float(f, "strictness", "foul");
                d.FoulScale = j.Float(f, "foulScale", "foul");
                d.ContactDistance = j.Float(f, "contactDistance", "foul");
                d.MinClosingSpeed = j.Float(f, "minClosingSpeed", "foul");
                d.SpeedReference = j.Float(f, "speedReference", "foul");
                d.SpeedMultiplier = j.Float(f, "speedMultiplier", "foul");
                d.BehindAngleDegrees = j.Float(f, "behindAngleDegrees", "foul");
                d.BehindMultiplier = j.Float(f, "behindMultiplier", "foul");
                d.ChallengeCooldownSeconds = j.Float(f, "challengeCooldownSeconds", "foul");
                d.MinPlayersOnPitch = j.Int(f, "minPlayersOnPitch", "foul");
            }
            var c = j.Object(root, "cards", "root");
            if (c != null)
            {
                j.Keys(c, "cards", "denialDistance");
                d.DenialDistance = j.Float(c, "denialDistance", "cards");
            }
            var o = j.Object(root, "offside", "root");
            if (o != null)
            {
                j.Keys(o, "offside", "tolerance");
                d.OffsideTolerance = j.Float(o, "tolerance", "offside");
            }
            var k = j.Object(root, "freeKick", "root");
            if (k != null)
            {
                j.Keys(k, "freeKick", "setupSeconds", "directShotDistance", "directShotMaxAngleDegrees", "shotPower", "wallDistanceMargin",
                    "wallSpacing", "wall");
                d.FreeKickSetupSeconds = j.Float(k, "setupSeconds", "freeKick");
                d.DirectShotDistance = j.Float(k, "directShotDistance", "freeKick");
                d.DirectShotMaxAngleDegrees = j.Float(k, "directShotMaxAngleDegrees", "freeKick");
                d.FreeKickShotPower = j.Float(k, "shotPower", "freeKick");
                d.WallDistanceMargin = j.Float(k, "wallDistanceMargin", "freeKick");
                d.WallSpacing = j.Float(k, "wallSpacing", "freeKick");
                var wall = j.Array(k, "wall", "freeKick");
                if (wall != null)
                {
                    d.WallMaxDistance = new float[wall.Count];
                    d.WallPlayers = new int[wall.Count];
                    for (int i = 0; i < wall.Count; i++)
                    {
                        string path = $"freeKick.wall[{i}]";
                        var row = j.AsObject(wall[i], path);
                        if (row == null) continue;
                        j.Keys(row, path, "maxDistance", "players");
                        d.WallMaxDistance[i] = j.Float(row, "maxDistance", path);
                        d.WallPlayers[i] = j.Int(row, "players", path);
                    }
                }
            }
            var p = j.Object(root, "penalty", "root");
            if (p != null)
            {
                j.Keys(p, "penalty", "setupSeconds", "spotDistance", "shotPower", "keeperStayChance", "keeperDiveLateral", "areaClearance");
                d.PenaltySetupSeconds = j.Float(p, "setupSeconds", "penalty");
                d.PenaltySpotDistance = j.Float(p, "spotDistance", "penalty");
                d.PenaltyShotPower = j.Float(p, "shotPower", "penalty");
                d.KeeperStayChance = j.Float(p, "keeperStayChance", "penalty");
                d.KeeperDiveLateral = j.Float(p, "keeperDiveLateral", "penalty");
                d.AreaClearance = j.Float(p, "areaClearance", "penalty");
            }
            if (!j.Ok) return Result<RefereeDefinition>.Fail(j.Errors);

            bool ok = d.Strictness >= 0f && d.FoulScale >= 0f && d.ContactDistance > 0f && d.MinClosingSpeed >= 0f && d.SpeedReference > 0f
                      && d.SpeedMultiplier >= 0f && d.BehindAngleDegrees > 0f && d.BehindAngleDegrees <= 180f && d.BehindMultiplier >= 1f
                      && d.ChallengeCooldownSeconds >= 0f && d.MinPlayersOnPitch >= 1 && d.MinPlayersOnPitch <= 11
                      && d.DenialDistance >= 0f && d.OffsideTolerance >= 0f
                      && d.FreeKickSetupSeconds >= 0f && d.DirectShotDistance >= 0f && d.DirectShotMaxAngleDegrees >= 0f
                      && d.FreeKickShotPower > 0f && d.FreeKickShotPower <= 1f && d.WallDistanceMargin >= 0f && d.WallSpacing > 0f
                      && d.PenaltySetupSeconds >= 0f && d.PenaltySpotDistance > 0f && d.PenaltyShotPower > 0f && d.PenaltyShotPower <= 1f
                      && d.KeeperStayChance >= 0f && d.KeeperStayChance <= 1f && d.KeeperDiveLateral >= 0f && d.AreaClearance >= 0f;
            for (int i = 0; ok && i < d.WallPlayers.Length; i++)
                ok = d.WallPlayers[i] >= 0 && d.WallPlayers[i] <= 10 && d.WallMaxDistance[i] > 0f && (i == 0 || d.WallMaxDistance[i] > d.WallMaxDistance[i - 1]);
            if (!ok)
            {
                j.Fail(InvalidReferee, "referee values invalid (multipliers >= 0, behindAngleDegrees in (0, 180], behindMultiplier >= 1, "
                    + "powers in (0, 1], chances in [0, 1], minPlayersOnPitch in [1, 11], wall rows ascending by maxDistance with 0-10 players).");
                return Result<RefereeDefinition>.Fail(j.Errors);
            }
            return Result<RefereeDefinition>.Ok(d);
        }
    }
}
