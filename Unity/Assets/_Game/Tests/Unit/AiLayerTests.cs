using System.IO;
using System.Numerics;
using Game.Data.Definitions;
using Game.Data.Loading;
using Game.Match;
using Game.Match.AI;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A4 unit tests: team phases, function-layer targets, on-ball choice, Data/Balance/ai.json + tactics.json.</summary>
    public class AiLayerTests
    {
        private static AiMatch NewMatch() => new AiMatch(Db(), AiMatchTests.Setup(1), Dt);

        [Test]
        public void WinningTheBall_OpensAnOffensiveTransition_ThenBuildOrAttack()
        {
            var ai = Db().Ai;
            var team = NewMatch().Home;
            TeamBrain.OnPossessionChanged(team, true, ai);
            Assert.AreEqual(TeamPhase.TransitionAttack, TeamBrain.UpdatePhase(team, true, 20f, 105f, ai));

            team.TransitionTimer = 0f;
            Assert.AreEqual(TeamPhase.Build, TeamBrain.UpdatePhase(team, true, 20f, 105f, ai), "ball low in the own frame: build.");
            Assert.AreEqual(TeamPhase.Attack, TeamBrain.UpdatePhase(team, true, 80f, 105f, ai));
        }

        [Test]
        public void LosingTheBall_OpensADefensiveTransition_ThenDefend()
        {
            var ai = Db().Ai;
            var team = NewMatch().Home;
            TeamBrain.OnPossessionChanged(team, false, ai);
            Assert.AreEqual(ai.TransitionDefenseSeconds, team.TransitionTimer, 1e-5f);
            Assert.AreEqual(TeamPhase.TransitionDefense, TeamBrain.UpdatePhase(team, false, 50f, 105f, ai));
            team.TransitionTimer = 0f;
            Assert.AreEqual(TeamPhase.Defend, TeamBrain.UpdatePhase(team, false, 50f, 105f, ai));
        }

        [Test]
        public void DefensiveLine_DropsWhenTheBallComesCloser()
        {
            var m = NewMatch();
            var cb = CentreBack(m.Home);
            m.Home.Phase = TeamPhase.Defend;
            float far = m.Home.Frame.U(RoleLayer.IdealTarget(cb, 70f, 0f, 105f, 105f, 34f, Db().Ai));
            float near = m.Home.Frame.U(RoleLayer.IdealTarget(cb, 30f, 0f, 105f, 105f, 34f, Db().Ai));
            Assert.Less(near, far, "the line sits behind the ball (GAME_DESIGN §24).");
            Assert.GreaterOrEqual(near, Db().Tactics.MinLine - 1e-3f, "but never deeper than the minimum line.");
        }

        [Test]
        public void Block_ShiftsTowardsTheBallSide()
        {
            var m = NewMatch();
            var cb = CentreBack(m.Home);
            m.Home.Phase = TeamPhase.Defend;
            float centre = m.Home.Frame.V(RoleLayer.IdealTarget(cb, 50f, 0f, 105f, 105f, 34f, Db().Ai));
            float left = m.Home.Frame.V(RoleLayer.IdealTarget(cb, 50f, 20f, 105f, 105f, 34f, Db().Ai));
            Assert.AreEqual(Db().Tactics.LateralShift * 20f, left - centre, 1e-3f, "GAME_DESIGN §24: ~40-60% toward the ball side.");
        }

        [Test]
        public void Attackers_StopAtTheOffsideLine()
        {
            var m = NewMatch();
            var striker = m.Home.Players[0];
            foreach (var p in m.Home.Players) if (p.Slot.X > striker.Slot.X) striker = p;
            m.Home.Phase = TeamPhase.Attack;
            float offside = 75f;
            float u = m.Home.Frame.U(RoleLayer.IdealTarget(striker, 70f, 0f, offside, 105f, 34f, Db().Ai));
            Assert.LessOrEqual(u, offside - Db().Ai.OffsideMargin + 1e-3f);
        }

        [Test]
        public void ClearChance_IsWorthMoreThanTheByline()
        {
            var m = NewMatch();
            var team = m.Home;
            var ai = Db().Ai;
            var penaltySpot = team.Frame.World(105f - 11f, 0f);
            var bylineWide = team.Frame.World(104f, 25f);
            Assert.Greater(OnBallDecision.Threat(penaltySpot, team, m.Pitch, ai), OnBallDecision.Threat(bylineWide, team, m.Pitch, ai),
                "running to the corner flag must not beat a central chance.");
            Assert.AreEqual(0f, OnBallDecision.ShotChance(team.Frame.World(40f, 0f), team, m.Pitch, ai), "no shot beyond the shot range.");
        }

        [Test]
        public void UnmarkedCarrierInFrontOfGoal_Shoots()
        {
            var m = NewMatch();
            var team = m.Home;
            var carrier = team.Players[9];
            carrier.Body.Position = team.Frame.World(105f - 9f, 0f);
            foreach (var o in m.Away.Players) o.Body.Position = m.Away.Frame.World(90f, 30f); // everyone far away
            var d = OnBallDecision.Decide(carrier, m.Away, m.Pitch, Db().Balance, Db().Ai);
            Assert.AreEqual(OnBallChoice.Shot, d.Choice);
        }

        [Test]
        public void RealAiAndTacticsFiles_Load()
        {
            var source = new DirectoryDataSource(TestPaths.DataRoot());
            var ai = GameDataLoader.LoadAi(source);
            var tactics = GameDataLoader.LoadTactics(source);
            Assert.IsTrue(ai.IsSuccess, ai.ToString());
            Assert.IsTrue(tactics.IsSuccess, tactics.ToString());
            Assert.AreEqual(5f, ai.Value.TeamHz, "TECHNICAL_SPEC §7: camada do time 5 Hz.");
            Assert.AreEqual(10f, ai.Value.RoleHz, "camada da função 10 Hz.");
            Assert.That(ai.Value.TransitionAttackSeconds, Is.InRange(2f, 4f), "GAME_DESIGN §24: transição ofensiva 2-4 s.");
            Assert.That(ai.Value.TransitionDefenseSeconds, Is.InRange(2f, 3f), "transição defensiva 2-3 s.");
            Assert.That(tactics.Value.LateralShift, Is.InRange(0.4f, 0.6f));
        }

        [Test]
        public void TacticsMissingAPhase_IsRejected() =>
            Assert.IsFalse(AiReader.ReadTactics(Read("tactics.json").Replace("\"defend\":", "\"defending\":")).IsSuccess);

        [Test]
        public void AiWithRestartInsideArrive_IsRejected() =>
            Assert.IsFalse(AiReader.ReadAi(Read("ai.json").Replace("\"restartRadius\": 1.2", "\"restartRadius\": 0.1")).IsSuccess);

        [Test]
        public void AiUnknownKey_IsRejected() =>
            Assert.IsFalse(AiReader.ReadAi(Read("ai.json").Replace("\"teamHz\"", "\"teamHertz\"")).IsSuccess);

        private static string Read(string file) => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", file));

        private static AiPlayer CentreBack(AiTeam team)
        {
            foreach (var p in team.Players) if (p.Slot.Role == FormationRole.CB) return p;
            Assert.Fail("no CB slot");
            return null;
        }
    }
}
