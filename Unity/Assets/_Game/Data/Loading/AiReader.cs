using Game.Core.Results;
using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict readers + semantic checks for Data/Balance/ai.json and tactics.json.</summary>
    public static class AiReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidAi = "INVALID_AI";
        public const string InvalidTactics = "INVALID_TACTICS";

        public static Result<TacticsDefinition> ReadTactics(string json)
        {
            var j = new StrictJson(GameDataLoader.TacticsFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "default");
            if (root == null) return Result<TacticsDefinition>.Fail(j.Errors);

            var d = j.Object(root, "default", "root");
            if (d == null) return Result<TacticsDefinition>.Fail(j.Errors);
            j.Keys(d, "default", "phases", "minLine", "lateralShift", "pressers", "pressTriggerDistance");

            var t = new TacticsDefinition
            {
                Phases = new PhaseBlock[TeamPhaseInfo.Count],
                MinLine = j.Float(d, "minLine", "default"),
                LateralShift = j.Float(d, "lateralShift", "default"),
                Pressers = j.Int(d, "pressers", "default"),
                PressTriggerDistance = j.Float(d, "pressTriggerDistance", "default"),
            };
            var phases = j.Object(d, "phases", "default");
            if (phases != null)
            {
                j.Keys(phases, "default.phases", TeamPhaseInfo.Keys);
                for (int i = 0; i < TeamPhaseInfo.Count; i++)
                {
                    string key = TeamPhaseInfo.Keys[i];
                    string path = "default.phases." + key;
                    var p = j.Object(phases, key, "default.phases");
                    if (p == null) continue;
                    j.Keys(p, path, "depth", "lineBehindBall", "maxLine", "widthScale");
                    t.Phases[i] = new PhaseBlock
                    {
                        Depth = j.Float(p, "depth", path),
                        LineBehindBall = j.Float(p, "lineBehindBall", path),
                        MaxLine = j.Float(p, "maxLine", path),
                        WidthScale = j.Float(p, "widthScale", path),
                    };
                }
            }
            if (!j.Ok) return Result<TacticsDefinition>.Fail(j.Errors);

            bool ok = t.MinLine > 0f && t.LateralShift >= 0f && t.LateralShift <= 1f && t.Pressers >= 0 && t.PressTriggerDistance > 0f;
            foreach (var p in t.Phases)
                ok &= p != null && p.Depth > 0f && p.LineBehindBall >= 0f && p.MaxLine > t.MinLine && p.WidthScale > 0f && p.WidthScale <= 1f;
            if (!ok) { j.Fail(InvalidTactics, "tactics values invalid (every phase needs depth > 0, maxLine > minLine, widthScale 0-1)."); return Result<TacticsDefinition>.Fail(j.Errors); }
            return Result<TacticsDefinition>.Ok(t);
        }

        public static Result<AiDefinition> ReadAi(string json)
        {
            var j = new StrictJson(GameDataLoader.AiFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "rates", "phases", "role", "individual", "onBall", "motion");
            if (root == null) return Result<AiDefinition>.Fail(j.Errors);
            var a = new AiDefinition();

            var r = j.Object(root, "rates", "root");
            if (r != null)
            {
                j.Keys(r, "rates", "teamHz", "roleHz", "individualHz");
                a.TeamHz = j.Float(r, "teamHz", "rates");
                a.RoleHz = j.Float(r, "roleHz", "rates");
                a.IndividualHz = j.Float(r, "individualHz", "rates");
            }
            var ph = j.Object(root, "phases", "root");
            if (ph != null)
            {
                j.Keys(ph, "phases", "transitionAttackSeconds", "transitionDefenseSeconds", "buildMaxBallFraction");
                a.TransitionAttackSeconds = j.Float(ph, "transitionAttackSeconds", "phases");
                a.TransitionDefenseSeconds = j.Float(ph, "transitionDefenseSeconds", "phases");
                a.BuildMaxBallFraction = j.Float(ph, "buildMaxBallFraction", "phases");
            }
            var ro = j.Object(root, "role", "root");
            if (ro != null)
            {
                j.Keys(ro, "role", "offsideMargin", "goalkeeperDistance", "goalkeeperLateralShift", "sidelineMargin",
                    "errorResampleSeconds", "correctionThreshold");
                a.OffsideMargin = j.Float(ro, "offsideMargin", "role");
                a.GoalkeeperDistance = j.Float(ro, "goalkeeperDistance", "role");
                a.GoalkeeperLateralShift = j.Float(ro, "goalkeeperLateralShift", "role");
                a.SidelineMargin = j.Float(ro, "sidelineMargin", "role");
                a.ErrorResampleSeconds = j.Float(ro, "errorResampleSeconds", "role");
                a.CorrectionThreshold = j.Float(ro, "correctionThreshold", "role");
            }
            var ind = j.Object(root, "individual", "root");
            if (ind != null)
            {
                j.Keys(ind, "individual", "supportPlayers", "supportCandidates", "supportRadius", "supportOpennessCap",
                    "supportOpennessWeight", "supportForwardWeight", "supportShapeWeight", "supportCommitSeconds",
                    "markZoneRadius", "markGoalSideDistance", "containDistance", "minCommitSeconds", "assignmentHysteresis",
                    "interceptHorizonSeconds", "interceptStepSeconds");
                a.SupportPlayers = j.Int(ind, "supportPlayers", "individual");
                a.SupportCandidates = j.Int(ind, "supportCandidates", "individual");
                a.SupportRadius = j.Float(ind, "supportRadius", "individual");
                a.SupportOpennessCap = j.Float(ind, "supportOpennessCap", "individual");
                a.SupportOpennessWeight = j.Float(ind, "supportOpennessWeight", "individual");
                a.SupportForwardWeight = j.Float(ind, "supportForwardWeight", "individual");
                a.SupportShapeWeight = j.Float(ind, "supportShapeWeight", "individual");
                a.SupportCommitSeconds = j.Float(ind, "supportCommitSeconds", "individual");
                a.MarkZoneRadius = j.Float(ind, "markZoneRadius", "individual");
                a.MarkGoalSideDistance = j.Float(ind, "markGoalSideDistance", "individual");
                a.ContainDistance = j.Float(ind, "containDistance", "individual");
                a.MinCommitSeconds = j.Float(ind, "minCommitSeconds", "individual");
                a.AssignmentHysteresis = j.Float(ind, "assignmentHysteresis", "individual");
                a.InterceptHorizonSeconds = j.Float(ind, "interceptHorizonSeconds", "individual");
                a.InterceptStepSeconds = j.Float(ind, "interceptStepSeconds", "individual");
            }
            var ob = j.Object(root, "onBall", "root");
            if (ob != null)
            {
                j.Keys(ob, "onBall", "decisionIntervalSeconds", "minDribbleSeconds", "dribbleStep", "dribbleHeadings", "dribbleSpreadDegrees", "dribbleClearance",
                    "passMinDistance", "passMaxDistance", "passLaneClearance", "passDistanceRisk", "receiverPressureRadius", "shotRange",
                    "shotPower", "shootChanceThreshold", "lateralValuePenalty", "progressWeight", "dribbleRiskFactor", "decisionNoise");
                a.DecisionIntervalSeconds = j.Float(ob, "decisionIntervalSeconds", "onBall");
                a.MinDribbleSeconds = j.Float(ob, "minDribbleSeconds", "onBall");
                a.DribbleStep = j.Float(ob, "dribbleStep", "onBall");
                a.DribbleHeadings = j.Int(ob, "dribbleHeadings", "onBall");
                a.DribbleSpreadDegrees = j.Float(ob, "dribbleSpreadDegrees", "onBall");
                a.DribbleClearance = j.Float(ob, "dribbleClearance", "onBall");
                a.PassMinDistance = j.Float(ob, "passMinDistance", "onBall");
                a.PassMaxDistance = j.Float(ob, "passMaxDistance", "onBall");
                a.PassLaneClearance = j.Float(ob, "passLaneClearance", "onBall");
                a.PassDistanceRisk = j.Float(ob, "passDistanceRisk", "onBall");
                a.ReceiverPressureRadius = j.Float(ob, "receiverPressureRadius", "onBall");
                a.ShotRange = j.Float(ob, "shotRange", "onBall");
                a.ShotPower = j.Float(ob, "shotPower", "onBall");
                a.ShootChanceThreshold = j.Float(ob, "shootChanceThreshold", "onBall");
                a.LateralValuePenalty = j.Float(ob, "lateralValuePenalty", "onBall");
                a.ProgressWeight = j.Float(ob, "progressWeight", "onBall");
                a.DribbleRiskFactor = j.Float(ob, "dribbleRiskFactor", "onBall");
                a.DecisionNoise = j.Float(ob, "decisionNoise", "onBall");
            }
            var m = j.Object(root, "motion", "root");
            if (m != null)
            {
                j.Keys(m, "motion", "arriveRadius", "restartRadius", "sprintDistance");
                a.ArriveRadius = j.Float(m, "arriveRadius", "motion");
                a.RestartRadius = j.Float(m, "restartRadius", "motion");
                a.SprintDistance = j.Float(m, "sprintDistance", "motion");
            }
            if (!j.Ok) return Result<AiDefinition>.Fail(j.Errors);

            bool ok = a.TeamHz > 0f && a.RoleHz > 0f && a.IndividualHz > 0f
                      && a.TransitionAttackSeconds >= 0f && a.TransitionDefenseSeconds >= 0f
                      && a.BuildMaxBallFraction > 0f && a.BuildMaxBallFraction < 1f
                      && a.OffsideMargin >= 0f && a.GoalkeeperDistance > 0f && a.GoalkeeperLateralShift >= 0f && a.GoalkeeperLateralShift <= 1f
                      && a.SidelineMargin >= 0f && a.ErrorResampleSeconds > 0f && a.CorrectionThreshold >= 0f
                      && a.SupportPlayers >= 0 && a.SupportCandidates > 0 && a.SupportRadius > 0f && a.SupportOpennessCap > 0f
                      && a.SupportOpennessWeight >= 0f && a.SupportForwardWeight >= 0f && a.SupportShapeWeight >= 0f
                      && a.SupportCommitSeconds >= 0f && a.MarkZoneRadius > 0f && a.MarkGoalSideDistance >= 0f
                      && a.ContainDistance > 0f && a.MinCommitSeconds >= 0f
                      && a.AssignmentHysteresis >= 0f && a.AssignmentHysteresis < 1f
                      && a.InterceptHorizonSeconds > 0f && a.InterceptStepSeconds > 0f
                      && a.DecisionIntervalSeconds > 0f && a.MinDribbleSeconds >= 0f && a.DribbleStep > 0f && a.DribbleHeadings > 0 && a.DribbleSpreadDegrees >= 0f && a.DribbleClearance > 0f
                      && a.PassMinDistance >= 0f && a.PassMaxDistance > a.PassMinDistance && a.PassLaneClearance > 0f && a.PassDistanceRisk >= 0f && a.ReceiverPressureRadius >= 0f
                      && a.ShotRange > 0f && a.ShotPower > 0f && a.ShotPower <= 1f && a.ShootChanceThreshold > 0f && a.ShootChanceThreshold <= 1f && a.LateralValuePenalty >= 0f && a.LateralValuePenalty < 1f
                      && a.ProgressWeight >= 0f && a.ProgressWeight <= 1f && a.DribbleRiskFactor > 0f && a.DribbleRiskFactor <= 1f
                      && a.DecisionNoise >= 0f
                      && a.ArriveRadius > 0f && a.RestartRadius > a.ArriveRadius && a.SprintDistance > 0f;
            if (!ok) { j.Fail(InvalidAi, "ai values invalid (rates > 0, restartRadius > arriveRadius, shotPower 0-1...)."); return Result<AiDefinition>.Fail(j.Errors); }
            return Result<AiDefinition>.Ok(a);
        }
    }
}
