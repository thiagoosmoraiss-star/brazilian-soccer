using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/duels.json.</summary>
    public static class DuelsReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidDuels = "INVALID_DUELS";

        public static Result<DuelsDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.DuelsFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "slide", "secondDefender", "body", "cover", "runs", "keeperRush", "shotBlock", "firstTouch");
            if (root == null) return Result<DuelsDefinition>.Fail(j.Errors);
            var d = new DuelsDefinition();

            var s = j.Object(root, "slide", "root");
            if (s != null)
            {
                j.Keys(s, "slide", "distance", "minSpeed", "maxSpeed", "reachFactor", "ballMaxHeight", "pokeSpeed", "groundSeconds",
                    "recoverSeconds", "contactDistance", "assistAngleDegrees", "behindYellowChance", "aiChancePerSecond", "aiMaxApproachDegrees");
                d.SlideDistance = j.Float(s, "distance", "slide");
                d.SlideMinSpeed = j.Float(s, "minSpeed", "slide");
                d.SlideMaxSpeed = j.Float(s, "maxSpeed", "slide");
                d.SlideReachFactor = j.Float(s, "reachFactor", "slide");
                d.SlideBallMaxHeight = j.Float(s, "ballMaxHeight", "slide");
                d.SlidePokeSpeed = j.Float(s, "pokeSpeed", "slide");
                d.SlideGroundSeconds = j.Float(s, "groundSeconds", "slide");
                d.SlideRecoverSeconds = j.Float(s, "recoverSeconds", "slide");
                d.SlideContactDistance = j.Float(s, "contactDistance", "slide");
                d.SlideAssistAngleDegrees = j.Float(s, "assistAngleDegrees", "slide");
                d.SlideBehindYellowChance = j.Float(s, "behindYellowChance", "slide");
                d.AiSlideChancePerSecond = j.Float(s, "aiChancePerSecond", "slide");
                d.AiSlideMaxApproachDegrees = j.Float(s, "aiMaxApproachDegrees", "slide");
            }
            var sd = j.Object(root, "secondDefender", "root");
            if (sd != null)
            {
                j.Keys(sd, "secondDefender", "maxDistance");
                d.SecondDefenderMaxDistance = j.Float(sd, "maxDistance", "secondDefender");
            }
            var b = j.Object(root, "body", "root");
            if (b != null)
            {
                j.Keys(b, "body", "closenessWeight", "pushExponent");
                d.BodyClosenessWeight = j.Float(b, "closenessWeight", "body");
                d.BodyPushExponent = j.Float(b, "pushExponent", "body");
            }
            var c = j.Object(root, "cover", "root");
            if (c != null)
            {
                j.Keys(c, "cover", "depth", "maxDistance");
                d.CoverDepth = j.Float(c, "depth", "cover");
                d.CoverMaxDistance = j.Float(c, "maxDistance", "cover");
            }
            var r = j.Object(root, "runs", "root");
            if (r != null)
            {
                j.Keys(r, "runs", "minSpaceBehind", "spaceReference", "depthBehindLine", "maxSeconds", "onsideMargin", "maxStartBehindLine", "cooldownSeconds",
                    "maxCarrierDistance", "throughValue");
                d.RunMinSpaceBehind = j.Float(r, "minSpaceBehind", "runs");
                d.RunSpaceReference = j.Float(r, "spaceReference", "runs");
                d.RunDepthBehindLine = j.Float(r, "depthBehindLine", "runs");
                d.RunMaxSeconds = j.Float(r, "maxSeconds", "runs");
                d.RunOnsideMargin = j.Float(r, "onsideMargin", "runs");
                d.RunMaxStartBehindLine = j.Float(r, "maxStartBehindLine", "runs");
                d.RunCooldownSeconds = j.Float(r, "cooldownSeconds", "runs");
                d.RunMaxCarrierDistance = j.Float(r, "maxCarrierDistance", "runs");
                d.RunThroughValue = j.Float(r, "throughValue", "runs");
            }
            var k = j.Object(root, "keeperRush", "root");
            if (k != null)
            {
                j.Keys(k, "keeperRush", "range", "smotherReachFactor", "hesitationSeconds", "throughBallMargin", "stopDistance");
                d.RushRange = j.Float(k, "range", "keeperRush");
                d.RushSmotherReachFactor = j.Float(k, "smotherReachFactor", "keeperRush");
                d.RushHesitationSeconds = j.Float(k, "hesitationSeconds", "keeperRush");
                d.RushThroughBallMargin = j.Float(k, "throughBallMargin", "keeperRush");
                d.RushStopDistance = j.Float(k, "stopDistance", "keeperRush");
            }
            var sb = j.Object(root, "shotBlock", "root");
            if (sb != null)
            {
                j.Keys(sb, "shotBlock", "reach", "maxHeight", "minSpeed", "chanceScale", "speedFactorMin", "speedFactorMax", "maxDeflectDegrees", "maxUpSpeed");
                d.BlockReach = j.Float(sb, "reach", "shotBlock");
                d.BlockMaxHeight = j.Float(sb, "maxHeight", "shotBlock");
                d.BlockMinSpeed = j.Float(sb, "minSpeed", "shotBlock");
                d.BlockChanceScale = j.Float(sb, "chanceScale", "shotBlock");
                d.BlockSpeedFactorMin = j.Float(sb, "speedFactorMin", "shotBlock");
                d.BlockSpeedFactorMax = j.Float(sb, "speedFactorMax", "shotBlock");
                d.BlockMaxDeflectDegrees = j.Float(sb, "maxDeflectDegrees", "shotBlock");
                d.BlockMaxUpSpeed = j.Float(sb, "maxUpSpeed", "shotBlock");
            }
            var ft = j.Object(root, "firstTouch", "root");
            if (ft != null)
            {
                j.Keys(ft, "firstTouch", "scale", "threshold", "minSpeed", "referenceSpeed", "speedFactorMin", "speedFactorMax",
                    "pressureRadius", "escapeDistanceScale", "maxEscapeDegrees");
                d.TouchScale = j.Float(ft, "scale", "firstTouch");
                d.TouchThreshold = j.Float(ft, "threshold", "firstTouch");
                d.TouchMinSpeed = j.Float(ft, "minSpeed", "firstTouch");
                d.TouchReferenceSpeed = j.Float(ft, "referenceSpeed", "firstTouch");
                d.TouchSpeedFactorMin = j.Float(ft, "speedFactorMin", "firstTouch");
                d.TouchSpeedFactorMax = j.Float(ft, "speedFactorMax", "firstTouch");
                d.TouchPressureRadius = j.Float(ft, "pressureRadius", "firstTouch");
                d.TouchEscapeDistanceScale = j.Float(ft, "escapeDistanceScale", "firstTouch");
                d.TouchMaxEscapeDegrees = j.Float(ft, "maxEscapeDegrees", "firstTouch");
            }
            if (!j.Ok) return Result<DuelsDefinition>.Fail(j.Errors);

            bool ok = d.SlideDistance > 0f && d.SlideMinSpeed > 0f && d.SlideMaxSpeed >= d.SlideMinSpeed && d.SlideReachFactor > 0f
                      && d.SlideBallMaxHeight > 0f && d.SlidePokeSpeed >= 0f && d.SlideGroundSeconds >= 0f && d.SlideRecoverSeconds >= 0f
                      && d.SlideContactDistance > 0f && d.SlideAssistAngleDegrees >= 0f && d.SlideAssistAngleDegrees <= 180f
                      && Chance(d.SlideBehindYellowChance) && d.AiSlideChancePerSecond >= 0f
                      && d.AiSlideMaxApproachDegrees >= 0f && d.AiSlideMaxApproachDegrees <= 180f
                      && d.SecondDefenderMaxDistance > 0f && d.BodyClosenessWeight >= 0f && d.BodyPushExponent >= 0f
                      && d.CoverDepth > 0f && d.CoverMaxDistance > 0f
                      && d.RunMinSpaceBehind >= 0f && d.RunSpaceReference > 0f && d.RunDepthBehindLine > 0f && d.RunMaxSeconds > 0f
                      && d.RunOnsideMargin >= 0f && d.RunMaxStartBehindLine > 0f && d.RunCooldownSeconds >= 0f && d.RunMaxCarrierDistance > 0f && d.RunThroughValue >= 0f
                      && d.RushRange > 0f && d.RushSmotherReachFactor > 0f && d.RushHesitationSeconds >= 0f && d.RushStopDistance >= 0f
                      && d.BlockReach > 0f && d.BlockMaxHeight > 0f && d.BlockMinSpeed >= 0f && d.BlockChanceScale >= 0f
                      && d.BlockSpeedFactorMin >= 0f && d.BlockSpeedFactorMax >= d.BlockSpeedFactorMin && d.BlockSpeedFactorMax <= 1f
                      && d.BlockMaxDeflectDegrees >= 0f && d.BlockMaxDeflectDegrees <= 180f && d.BlockMaxUpSpeed >= 0f
                      && d.TouchScale >= 0f && d.TouchThreshold > 0f && d.TouchMinSpeed >= 0f && d.TouchReferenceSpeed > 0f
                      && d.TouchSpeedFactorMin >= 0f && d.TouchSpeedFactorMax >= d.TouchSpeedFactorMin && d.TouchPressureRadius >= 0f
                      && d.TouchEscapeDistanceScale > 0f && d.TouchMaxEscapeDegrees >= 0f && d.TouchMaxEscapeDegrees <= 180f;
            if (!ok)
            {
                j.Fail(InvalidDuels, "duels values invalid (distances and speeds > 0, maxSpeed >= minSpeed, chances in [0, 1], angles in [0, 180]).");
                return Result<DuelsDefinition>.Fail(j.Errors);
            }
            return Result<DuelsDefinition>.Ok(d);
        }

        private static bool Chance(float p) => p >= 0f && p <= 1f;
    }
}
