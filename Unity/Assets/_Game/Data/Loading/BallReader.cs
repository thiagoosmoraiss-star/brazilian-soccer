using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/ball.json.</summary>
    public static class BallReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidBall = "INVALID_BALL";

        public static Result<BallDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.BallFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "pitch", "ball");
            if (root == null) return Result<BallDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidBall, m); }

            var def = new BallDefinition();

            var p = j.Object(root, "pitch", "root");
            if (p != null)
            {
                j.Keys(p, "pitch", "length", "width", "goalWidth", "goalHeight", "postRadius",
                    "penaltyAreaDepth", "penaltyAreaWidth");
                def.Pitch = new PitchParameters
                {
                    Length = j.Float(p, "length", "pitch"),
                    Width = j.Float(p, "width", "pitch"),
                    GoalWidth = j.Float(p, "goalWidth", "pitch"),
                    GoalHeight = j.Float(p, "goalHeight", "pitch"),
                    PostRadius = j.Float(p, "postRadius", "pitch"),
                    PenaltyAreaDepth = j.Float(p, "penaltyAreaDepth", "pitch"),
                    PenaltyAreaWidth = j.Float(p, "penaltyAreaWidth", "pitch"),
                };
                Check(def.Pitch.Length > 0f && def.Pitch.Width > 0f && def.Pitch.GoalWidth > 0f
                      && def.Pitch.GoalWidth < def.Pitch.Width && def.Pitch.GoalHeight > 0f && def.Pitch.PostRadius > 0f
                      && def.Pitch.PenaltyAreaDepth > 0f && def.Pitch.PenaltyAreaDepth < def.Pitch.Length * 0.5f
                      && def.Pitch.PenaltyAreaWidth > def.Pitch.GoalWidth && def.Pitch.PenaltyAreaWidth < def.Pitch.Width,
                    "pitch values invalid (goalWidth < penaltyAreaWidth < width, penaltyAreaDepth < half length).");
            }

            var b = j.Object(root, "ball", "root");
            if (b != null)
            {
                j.Keys(b, "ball", "radius", "gravity", "rollingFrictionDeceleration", "minRollingSpeed",
                    "airDragCoefficient", "bounceVerticalRestitution", "bounceHorizontalRetention",
                    "minBounceSpeedToStayAirborne", "postRestitution", "netDampingFactor",
                    "spinLateralAccelCoefficient", "spinDecayPerSecond", "substepDistance", "maxSubsteps");
                def.Ball = new BallParameters
                {
                    Radius = j.Float(b, "radius", "ball"),
                    Gravity = j.Float(b, "gravity", "ball"),
                    RollingFrictionDeceleration = j.Float(b, "rollingFrictionDeceleration", "ball"),
                    MinRollingSpeed = j.Float(b, "minRollingSpeed", "ball"),
                    AirDragCoefficient = j.Float(b, "airDragCoefficient", "ball"),
                    BounceVerticalRestitution = j.Float(b, "bounceVerticalRestitution", "ball"),
                    BounceHorizontalRetention = j.Float(b, "bounceHorizontalRetention", "ball"),
                    MinBounceSpeedToStayAirborne = j.Float(b, "minBounceSpeedToStayAirborne", "ball"),
                    PostRestitution = j.Float(b, "postRestitution", "ball"),
                    NetDampingFactor = j.Float(b, "netDampingFactor", "ball"),
                    SpinLateralAccelCoefficient = j.Float(b, "spinLateralAccelCoefficient", "ball"),
                    SpinDecayPerSecond = j.Float(b, "spinDecayPerSecond", "ball"),
                    SubstepDistance = j.Float(b, "substepDistance", "ball"),
                    MaxSubsteps = j.Int(b, "maxSubsteps", "ball"),
                };
                Check(def.Ball.Radius > 0f && def.Ball.Gravity > 0f && def.Ball.RollingFrictionDeceleration > 0f
                      && def.Ball.MinRollingSpeed > 0f && def.Ball.AirDragCoefficient >= 0f
                      && def.Ball.BounceVerticalRestitution > 0f && def.Ball.BounceVerticalRestitution < 1f
                      && def.Ball.BounceHorizontalRetention > 0f && def.Ball.BounceHorizontalRetention <= 1f
                      && def.Ball.MinBounceSpeedToStayAirborne > 0f && def.Ball.PostRestitution > 0f && def.Ball.PostRestitution < 1f
                      && def.Ball.NetDampingFactor > 0f && def.Ball.NetDampingFactor < 1f && def.Ball.SpinDecayPerSecond >= 0f
                      && def.Ball.SubstepDistance > 0f && def.Ball.MaxSubsteps > 0,
                    "ball values invalid.");
            }

            return j.Ok ? Result<BallDefinition>.Ok(def) : Result<BallDefinition>.Fail(j.Errors);
        }
    }
}
