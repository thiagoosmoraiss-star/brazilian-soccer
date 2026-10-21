using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
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

        /// <summary>Home carrier at <paramref name="u"/> (own frame) in open play, the away side back near its own goal line
        /// and out wide (no pressure, a deep offside line); a few steps run.</summary>
        private static AiMatch CarrierAt(float u)
        {
            var m = NewMatch();
            m.CancelRestart();
            foreach (var o in m.Away.Players) o.Body.Position = m.Away.Frame.World(4f, 30f);
            var carrier = m.Home.Players[5];
            carrier.Body.Position = m.Home.Frame.World(u, 0f);
            m.PlaceBall(carrier);
            for (int i = 0; i < 25; i++) m.Step();
            return m;
        }

        [Test]
        public void WithTheBallUpThePitch_TheMostAdvancedPlayers_AttackTheBox_InsteadOfSupporting()
        {
            var ai = Db().Ai;
            var m = CarrierAt(80f);
            var team = m.Home;
            Assert.IsTrue(team.HasPossession);
            int carrier = m.Ball.Owner - team.Players[0].Global;
            var expected = team.Players.Where(p => !p.IsGoalkeeper && p.Local != carrier)
                .OrderByDescending(p => p.Slot.X).ThenBy(p => p.Local).Take(ai.BoxAttackers).Select(p => p.Local).ToList();
            CollectionAssert.AreEquivalent(expected, team.Players.Where(p => team.AttacksTheBox(p.Local)).Select(p => p.Local),
                "F1b: the most advanced slots attack the box.");
            foreach (int i in expected)
            {
                var p = team.Players[i];
                Assert.AreNotEqual(AiIntention.Support, p.Intention, "a box attacker does not drop to support.");
                Assert.LessOrEqual(MathF.Abs(team.Frame.V(p.IdealTarget)), ai.BoxAttackHalfWidth + 1e-3f);
                Assert.Greater(team.Frame.U(p.IdealTarget), 80f, "he goes ahead of the ball, towards the box.");
            }
        }

        [Test]
        public void WithTheBallInTheOwnHalf_NobodyAttacksTheBox()
        {
            var m = CarrierAt(30f);
            Assert.IsTrue(m.Home.HasPossession);
            Assert.IsFalse(m.Home.Players.Any(p => m.Home.AttacksTheBox(p.Local)));
        }

        [Test]
        public void PossessionValue_MakesThePressedCarrier_PassMoreInsteadOfRunningIntoThePresser()
        {
            // Forward, every dribble heading ends ~2.5 m from a defender (a risky run worth more ground); 15 m behind him
            // an open teammate (a safe pass worth less). Without a value for keeping the ball the run wins more often.
            var withValue = Db().Ai;
            Assert.Greater(withValue.PossessionValue, 0f, "ai.json gives keeping the ball a value (F1b).");
            var without = AiReader.ReadAi(Read("ai.json").Replace($"\"possessionValue\": {withValue.PossessionValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                "\"possessionValue\": 0.0")).Value;
            Assert.AreEqual(0f, without.PossessionValue);
            int Passes(Game.Data.Match.AiDefinition ai)
            {
                var m = NewMatch();
                m.Step(); // the team layer has set the offside line
                var team = m.Home;
                var carrier = team.Players[5];
                carrier.Body.Position = team.Frame.World(60f, 0f);
                foreach (var p in team.Players) if (p != carrier) p.Body.Position = team.Frame.World(5f, 33f);
                team.Players[6].Body.Position = team.Frame.World(45f, 0f);
                foreach (var o in m.Away.Players) o.Body.Position = team.Frame.World(100f, -33f);
                m.Away.Players[3].Body.Position = team.Frame.World(68.5f, 0f);
                m.Away.Players[4].Body.Position = team.Frame.World(67f, 5f);
                m.Away.Players[5].Body.Position = team.Frame.World(67f, -5f);
                int passes = 0;
                for (int i = 0; i < 300; i++)
                    if (OnBallDecision.Decide(carrier, m.Away, m.Pitch, Db().Balance, ai).Choice == OnBallChoice.Pass) passes++;
                return passes;
            }
            Assert.Greater(Passes(withValue), Passes(without));
        }

        [Test]
        public void ABallRollingOut_IsChasedToTheLine_NotBeyondIt()
        {
            var m = NewMatch();
            m.CancelRestart();
            var chaser = m.Home.Players[5];
            chaser.Body.Position = new Vector3(m.Pitch.HalfLength - 15f, 0f, 0f);
            m.Ball.State = BallState.Rolling;
            m.Ball.Owner = Ball.NoPlayer;
            m.Ball.Position = new Vector3(m.Pitch.HalfLength - 3f, 0f, 0f);
            m.Ball.Velocity = new Vector3(20f, 0f, 0f); // it will cross the goal line long before he gets there
            chaser.Intention = AiIntention.ChaseBall;
            var target = IndividualLayer.Target(chaser, m.Away, m.Ball, m.Pitch, Db().Ai, 0.1f, Db().Balance, Db().Ball.Ball.RollingFrictionDeceleration);
            Assert.LessOrEqual(target.X, m.Pitch.HalfLength + 1e-3f);
        }

        /// <summary>The user shoots from 20 m straight at goal; an away defender stands on the line of the shot 6 m ahead (or
        /// is parked far away). Returns how many of <paramref name="trials"/> shots he blocked (F1b shot block).</summary>
        private static int BlockedShots(int trials, bool defenderInTheWay)
        {
            int blocked = 0;
            for (int t = 0; t < trials; t++)
            {
                var m = new AiMatch(Db(), AiMatchTests.Setup((ulong)(t + 1)), Dt);
                m.CancelRestart();
                float goalX = m.Pitch.HalfLength;
                for (int i = 0; i < m.Players.Length; i++) m.Players[i].Body.Position = new Vector3(-45f + (i % 4), -30f + 2.5f * i, 0f);
                var shooter = m.Home.Players[9];
                shooter.Body.Position = new Vector3(goalX - 20f, 0f, 0f);
                shooter.Body.Facing = Vector3.UnitX;
                var defender = m.Away.Players[3];
                defender.Body.Position = defenderInTheWay ? new Vector3(goalX - 14f, 0f, 0f) : new Vector3(-40f, 30f, 0f);
                m.AwayKeeper.Player.Body.Position = new Vector3(goalX - 1f, 0f, 0f);
                m.PlaceBall(shooter);
                m.EnableHuman(MatchSide.Home);
                for (int i = 0; i < 10 && m.Ball.Owner != shooter.Global; i++) m.Step();
                Assert.AreEqual(shooter.Global, m.Ball.Owner, "the shooter has the ball");
                m.Step(new HumanInput(Vector2.UnitX, false, new ActionCommand(ActionKind.Shot, 0.8f), false, false));
                for (int i = 0; i < 40; i++)
                {
                    var ev = m.Step();
                    if (ev == AiMatchEvent.Block)
                    {
                        blocked++;
                        Assert.AreEqual(defender.Global, m.Ball.LastTouch, "the blocker touched it last (a corner if it goes out)");
                        break;
                    }
                    if (ev == AiMatchEvent.Goal || ev == AiMatchEvent.Save || ev == AiMatchEvent.Out) break;
                }
            }
            return blocked;
        }

        [Test]
        public void AShotThroughADefender_IsSometimesBlocked_AndNeverWithNobodyInTheWay()
        {
            Assert.Greater(Db().Duels.BlockChanceScale, 0f, "duels.json shotBlock is on (F1b)");
            int inTheWay = BlockedShots(40, true);
            Assert.Greater(inTheWay, 0, "ShotBlockChance (Desarme) blocks some shots");
            Assert.Less(inTheWay, 40, "but not every one");
            Assert.AreEqual(0, BlockedShots(10, false));
        }

        [Test]
        public void AiWithInvalidAttackValues_IsRejected()
        {
            Assert.IsFalse(AiReader.ReadAi(Read("ai.json").Replace("\"boxAttackers\": 3", "\"boxAttackers\": 5")).IsSuccess);
            Assert.IsFalse(AiReader.ReadAi(Read("ai.json").Replace("\"boxAttackFraction\": 0.5", "\"boxAttackFraction\": 1.5")).IsSuccess);
            Assert.IsFalse(AiReader.ReadAi(Read("ai.json").Replace("\"possessionValue\": 0.1", "\"possessionValue\": -0.1")).IsSuccess);
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
