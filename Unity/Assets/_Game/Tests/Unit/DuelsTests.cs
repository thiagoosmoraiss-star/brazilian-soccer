using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A9b (ROADMAP: "carrinho, 2º defensor, disputa de corpo básica, ruptura e cobertura"; GAME_DESIGN §22
    /// "carrinho (2-3 m; falta se contato antes da bola; por trás cartão provável; erro = 1 s no chão)", §23 "sai no 1×1
    /// com bola exposta; sai na enfiada só se chega antes"; X-75). Ranges in Data/TestRanges/duels.json.</summary>
    public class DuelsTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "duels.json"))));

        private static MatchSetup Setup(ulong seed, (Attr, int)[] home = null, (Attr, int)[] away = null, (Attr, int)[] awayKeeper = null)
        {
            var formation = Db().Formation("4-4-2");
            var tactic = new TacticSetup("4-4-2", 3, 2, 2);
            MatchTeamSetup Team(int club, (Attr, int)[] o, (Attr, int)[] gk) => new MatchTeamSetup(new Id(club), tactic,
                Enumerable.Range(0, 11).Select(i => formation.Slots[i].Role == Game.Data.Definitions.FormationRole.GK
                    ? Player(70, overrides: gk ?? new (Attr, int)[0]) : Player(70, overrides: o ?? new (Attr, int)[0])).ToArray(),
                new MatchPlayerSetup[0]);
            return new MatchSetup(Team(1, home, null), Team(2, away, awayKeeper), false, 6, 5, seed);
        }

        /// <summary>Open play; every outfield player parked out wide except the ones the scenario places.</summary>
        private static AiMatch Open(MatchSetup setup)
        {
            var m = new AiMatch(Db(), setup, Dt);
            m.CancelRestart();
            foreach (var p in m.Players)
                if (!p.IsGoalkeeper) p.Body.Position = new Vector3(p.Body.Position.X * 0.3f, (p.Local % 2 == 0 ? 1f : -1f) * 30f, 0f);
            return m;
        }

        /// <summary>Gives <paramref name="carrier"/> the ball at his feet (along his facing), standing still.</summary>
        private static void GiveBall(AiMatch m, AiPlayer carrier, Vector3 at, Vector3 facing)
        {
            carrier.Body.Position = at;
            carrier.Body.Velocity = Vector3.Zero;
            carrier.Body.Facing = facing;
            m.Ball.Position = at + facing * 0.6f;
            m.Ball.Velocity = Vector3.Zero;
            m.Ball.State = BallState.Controlled;
            m.Ball.Owner = carrier.Global;
            m.Ball.LastTouch = carrier.Global;
            carrier.DecisionTimer = 100f;
            carrier.DribbleTarget = at;
        }

        private static AiPlayer Outfield(AiTeam t, int n = 0) => t.Players.Where(p => !p.IsGoalkeeper).ElementAt(n);

        private static HumanInput Input(Vector2 move, bool slide = false, bool press = false) =>
            new HumanInput(move, false, ActionCommand.None, false, false, slide, press);

        /// <summary>The user's home defender at <paramref name="defenderAt"/>; an away carrier at the centre spot facing -x.</summary>
        private static (AiMatch m, AiPlayer defender, AiPlayer carrier) SlideScene(ulong seed, Vector3 defenderAt)
        {
            var m = Open(Setup(seed));
            var defender = Outfield(m.Home);
            var carrier = Outfield(m.Away);
            defender.Body.Position = defenderAt;
            m.Ball.LastTouch = defender.Global;
            m.EnableHuman(MatchSide.Home);
            Assert.AreSame(defender, m.Controlled);
            GiveBall(m, carrier, Vector3.Zero, -Vector3.UnitX);
            return (m, defender, carrier);
        }

        // ---------------- data ----------------

        [Test]
        public void DuelsFile_Loads_AndRejectsUnknownKeysAndBadValues()
        {
            Assert.IsNotNull(Db().Duels);
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "duels.json"));
            var j = JObject.Parse(json);
            j["slide"]["extra"] = 1;
            Assert.IsFalse(DuelsReader.Read(j.ToString()).IsSuccess, "unknown key");
            j = JObject.Parse(json);
            j["slide"]["maxSpeed"] = 1.0;
            Assert.IsFalse(DuelsReader.Read(j.ToString()).IsSuccess, "maxSpeed below minSpeed");
            j = JObject.Parse(json);
            j["slide"]["behindYellowChance"] = 1.5;
            Assert.IsFalse(DuelsReader.Read(j.ToString()).IsSuccess, "chance above 1");
        }

        // ---------------- carrinho ----------------

        [Test]
        public void ASlide_CoversItsDistance_ThenAMissLeavesHimOnTheGroundForAboutASecond()
        {
            var d = Db().Duels;
            var body = new PlayerBody { Velocity = new Vector3(6f, 0f, 0f) };
            DefenseSystem.StartSlide(body, Vector3.UnitX, d);
            float t = 0f;
            while (body.SlideRemaining > 0f) { DefenseSystem.SlideStep(body, d, Dt); t += Dt; }
            Assert.That(body.Position.X, Is.EqualTo(d.SlideDistance).Within(0.15f), "GAME_DESIGN §22: 2-3 m");
            float ground = 0f;
            while (body.Down) { DefenseSystem.SlideStep(body, d, Dt); ground += Dt; }
            Assert.That(ground, Is.EqualTo(d.SlideGroundSeconds).Within(2f * Dt), "erro = 1 s no chão");
        }

        [Test]
        public void TheUsersSlide_FromTheFront_WinsTheBallCleanly()
        {
            var (m, defender, carrier) = SlideScene(1, new Vector3(-3f, 0.5f, 0f));
            m.Step(Input(new Vector2(1f, 0f), slide: true));
            Assert.IsTrue(defender.Body.Down, "the slide started");
            for (int i = 0; i < 60 && m.HomeStats.SlidesWon == 0; i++) m.Step(HumanInput.None);
            Assert.AreEqual(1, m.HomeStats.SlidesWon, "the leg reached the ball first");
            Assert.AreEqual(0, m.HomeStats.Fouls);
            Assert.AreNotEqual(carrier.Global, m.Ball.Owner, "the carrier lost it");
        }

        [Test]
        public void TheUsersSlide_IsAimedAtTheBall_WhenTheStickPointsRoughlyThere()
        {
            var (m, defender, _) = SlideScene(2, new Vector3(-3f, 0f, 0f));
            m.Step(Input(new Vector2(1f, 0.6f), slide: true)); // ~31° off the ball: within the assist angle
            Assert.Less(MathF.Abs(defender.Body.SlideDirection.Y), 0.15f, "turned onto the ball");
        }

        [Test]
        public void ASlideFromBehind_HitsTheManFirst_IsAFoul_AndUsuallyBooked()
        {
            int fouls = 0, yellows = 0, n = R()["slide"]["behindTrials"].Value<int>();
            for (int s = 1; s <= n; s++)
            {
                var (m, defender, carrier) = SlideScene((ulong)s * 7919UL, new Vector3(2.2f, 0f, 0f)); // behind the carrier (he faces -x)
                defender.Body.Facing = -Vector3.UnitX;
                var ev = m.Step(Input(new Vector2(-1f, 0f), slide: true));
                for (int i = 0; i < 60 && ev != AiMatchEvent.Foul && m.HomeStats.SlidesWon == 0; i++) ev = m.Step(HumanInput.None);
                Assert.AreEqual(0, m.HomeStats.SlidesWon, $"seed {s}: the carrier's body is between him and the ball");
                if (ev == AiMatchEvent.Foul) fouls++;
                yellows += m.HomeStats.YellowCards + m.HomeStats.RedCards;
            }
            Assert.AreEqual(n, fouls, "contato antes da bola = falta");
            Assert.GreaterOrEqual((double)yellows / n, R()["slide"]["behindCardMin"].Value<double>(), "por trás cartão provável");
        }

        [Test]
        public void AMissedSlide_LeavesTheUserStuckOnTheGround()
        {
            var (m, defender, _) = SlideScene(3, new Vector3(-20f, 10f, 0f));
            m.Step(Input(new Vector2(0f, 1f), slide: true));
            int steps = 0;
            while (defender.Body.SlideRemaining > 0f) { m.Step(Input(new Vector2(0f, 1f))); steps++; }
            var lying = defender.Body.Position;
            int grounded = 0;
            while (defender.Body.Down) { m.Step(Input(new Vector2(-1f, 0f))); grounded++; }
            Assert.That(grounded * Dt, Is.EqualTo(Db().Duels.SlideGroundSeconds).Within(3f * Dt));
            Assert.Less(Vector3.Distance(lying, defender.Body.Position), 0.05f, "the stick does nothing while he is down");
        }

        [Test]
        public void TheAi_SlidesInMatches_AndOnlyRarelyFouls()
        {
            var r = R()["headless"];
            var vs = GameDataLoader.LoadVerticalSlice(new DirectoryDataSource(TestPaths.DataRoot()), Db()).Value;
            var h = HeadlessReport.Run(Db(), vs.Team("strong").ToSetup(), vs.Team("weak").ToSetup(), r["matches"].Value<int>(),
                r["minutes"].Value<int>(), r["seed"].Value<ulong>(), Dt);
            TestContext.WriteLine($"slides {h.PerMatch(h.Slides):F2} (won {h.PerMatch(h.SlidesWon):F2}, fouls {h.PerMatch(h.SlideFouls):F2}), "
                                  + $"body duels {h.PerMatch(h.BodyDuels):F2}, runs {h.PerMatch(h.Runs):F2}, through balls {h.PerMatch(h.ThroughBalls):F2}, "
                                  + $"keeper rushes {h.PerMatch(h.KeeperRushes):F2}, smothers {h.PerMatch(h.Smothers):F2}");
            void InRange(string name, double v) => Assert.That(v, Is.InRange(r[name][0].Value<double>(), r[name][1].Value<double>()), name);
            InRange("slidesPerMatch", h.PerMatch(h.Slides));
            InRange("slideFoulFraction", h.Slides > 0 ? h.SlideFouls / h.Slides : 0);
            InRange("runsPerMatch", h.PerMatch(h.Runs));
            InRange("throughBallsPerMatch", h.PerMatch(h.ThroughBalls));
            InRange("keeperRushesPerMatch", h.PerMatch(h.KeeperRushes));
        }

        // ---------------- second defender ----------------

        [Test]
        public void HoldingPressionar_SendsTheNearestTeammateStraightAtTheBall_UntilReleased()
        {
            var m = Open(Setup(4));
            var user = Outfield(m.Home);
            var mate = Outfield(m.Home, 1);
            user.Body.Position = new Vector3(-12f, 8f, 0f);
            mate.Body.Position = new Vector3(-8f, -4f, 0f);
            m.Ball.LastTouch = user.Global;
            m.EnableHuman(MatchSide.Home);
            Assert.AreSame(user, m.Controlled);
            var carrier = Outfield(m.Away);
            GiveBall(m, carrier, Vector3.Zero, -Vector3.UnitX);

            float closest = float.MaxValue;
            for (int i = 0; i < 100 && m.Ball.Owner == carrier.Global; i++)
            {
                m.Step(Input(Vector2.Zero, press: true));
                if (m.Ball.Owner != carrier.Global) { closest = 0f; break; } // won it
                Assert.GreaterOrEqual(m.Home.SecondDefender, 0);
                var second = m.Home.Players[m.Home.SecondDefender];
                Assert.AreNotSame(m.Controlled, second, "a teammate, never the user's own player");
                Assert.AreEqual(AiIntention.Press, second.Intention);
                closest = MathF.Min(closest, Vector3.Distance(second.Body.Position, m.Ball.Position));
            }
            Assert.Less(closest, Db().Ai.ContainDistance, "he goes for the ball, not just goal-side of it");
            m.Step(Input(Vector2.Zero));
            Assert.AreEqual(-1, m.Home.SecondDefender, "released");
        }

        // ---------------- body duel ----------------

        [Test]
        public void ALooseBallBetweenTwo_GoesMostlyToTheStrongerBody()
        {
            int trials = R()["body"]["trials"].Value<int>(), strong = 0, taken = 0;
            for (int s = 1; s <= trials; s++)
            {
                var m = Open(Setup((ulong)s, home: new[] { (Attr.Strength, 95) }, away: new[] { (Attr.Strength, 20) }));
                var h = Outfield(m.Home);
                var a = Outfield(m.Away);
                h.Body.Position = new Vector3(-0.6f, 0f, 0f);
                a.Body.Position = new Vector3(0.6f, 0f, 0f);
                m.Ball.Position = Vector3.Zero;
                m.Ball.Velocity = Vector3.Zero;
                m.Ball.State = BallState.Rolling;
                m.Ball.Owner = Ball.NoPlayer;
                m.Ball.LastTouch = m.HomeKeeper.Player.Global;
                m.Step();
                if (m.Ball.Owner == h.Global) { strong++; taken++; }
                else if (m.Ball.Owner == a.Global) taken++;
            }
            Assert.AreEqual(trials, taken, "one of the two took it");
            Assert.GreaterOrEqual((double)strong / trials, R()["body"]["strongWinMin"].Value<double>());
            Assert.Less(strong, trials, "the weaker one still wins some");
        }

        [Test]
        public void OverlappingBodies_TheStrongerOneGivesLessGround()
        {
            var a = new PlayerBody { Position = Vector3.Zero };
            var b = new PlayerBody { Position = new Vector3(0.4f, 0f, 0f) };
            Contact.Separate(new[] { a, b }, 2, 0.4f, new[] { 0.8f, 0.2f });
            Assert.That(b.Position.X - a.Position.X, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.Less(-a.Position.X, b.Position.X - 0.4f, "the strong one (a) moved less");
        }

        // ---------------- cover and runs ----------------

        [Test]
        public void ABeatenPresser_IsBackedByTheNearestGoalSideDefender()
        {
            var m = Open(Setup(5));
            var carrier = Outfield(m.Away);
            GiveBall(m, carrier, Vector3.Zero, -Vector3.UnitX); // away attacks -x: home's goal is at -x
            var beaten = Outfield(m.Home);
            var cover = Outfield(m.Home, 1);
            beaten.Body.Position = new Vector3(3f, 0.5f, 0f); // behind the carrier
            cover.Body.Position = new Vector3(-9f, 2f, 0f);
            for (int i = 0; i < 12; i++) m.Step();
            Assert.That(cover.Intention, Is.EqualTo(AiIntention.Press).Or.EqualTo(AiIntention.Cover), "the goal-side defender steps in");
            Assert.AreEqual(AiIntention.Press, cover.Intention);
        }

        private static AiMatch RunScene(ulong seed, int carrierVision, float lineX, out AiPlayer forward)
        {
            var m = Open(Setup(seed, home: new[] { (Attr.Vision, carrierVision) }));
            var carrier = Outfield(m.Home);
            GiveBall(m, carrier, new Vector3(lineX - 25f, 0f, 0f), Vector3.UnitX);
            foreach (var p in m.Away.Players)
                if (!p.IsGoalkeeper) p.Body.Position = new Vector3(lineX, -20f + 4f * p.Local, 0f);
            forward = Outfield(m.Home, 1);
            forward.Body.Position = new Vector3(lineX - 6f, 10f, 0f);
            return m;
        }

        [Test]
        public void WithSpaceBehindTheLine_AnOnsideAttackerRunsInBehind_IfTheCarriersVisionSeesIt()
        {
            // 15 m between the line and the goal line: a space score that good vision sees and poor vision does not.
            float L = Db().Ball.Pitch.Length * 0.5f;
            var m = RunScene(1, 90, L - 15f, out var forward);
            for (int i = 0; i < 15; i++) m.Step();
            Assert.AreEqual(forward.Local, m.Home.Runner);
            Assert.AreEqual(AiIntention.Run, forward.Intention);
            Assert.Greater(forward.RunTarget.X, L - 15f, "the run goes beyond the line");

            m = RunScene(1, 15, L - 15f, out forward);
            for (int i = 0; i < 15; i++) m.Step();
            Assert.AreEqual(-1, m.Home.Runner, "poor Visão: no run triggered");
        }

        // ---------------- keeper 1×1 ----------------

        /// <summary>A home carrier alone 14 m in front of the away goal (+x), every away outfielder behind him.</summary>
        private static AiMatch OneOnOne(ulong seed, int gkPositioning, bool defenderGoalSide = false)
        {
            float L = Db().Ball.Pitch.Length * 0.5f;
            var m = Open(Setup(seed, awayKeeper: new[] { (Attr.GkPositioning, gkPositioning) }));
            foreach (var p in m.Away.Players) if (!p.IsGoalkeeper) p.Body.Position = new Vector3(L - 35f, -20f + 4f * p.Local, 0f);
            if (defenderGoalSide) Outfield(m.Away).Body.Position = new Vector3(L - 6f, 0f, 0f);
            m.AwayKeeper.Player.Body.Position = new Vector3(L - 1f, 0f, 0f);
            GiveBall(m, Outfield(m.Home), new Vector3(L - 14f, 0f, 0f), Vector3.UnitX);
            return m;
        }

        [Test]
        public void AloneInFrontOfGoal_TheKeeperRushes_AGoodOneSoonerAndHeSmothersAnExposedBall()
        {
            int seeds = R()["rush"]["seeds"].Value<int>();
            double good = 0, poor = 0;
            int smothers = 0;
            foreach (var (pos, total) in new[] { (95, 0), (5, 1) })
            {
                double sum = 0;
                for (int s = 1; s <= seeds; s++)
                {
                    var m = OneOnOne((ulong)s, pos);
                    int step = 0;
                    while (step < 150 && m.AwayKeeper.State != KeeperState.Rushing) { m.Step(); step++; }
                    Assert.Less(step, 150, $"seed {s}: he came out");
                    sum += step * Dt;
                    for (int i = 0; i < 150 && m.AwayStats.Smothers == 0; i++) m.Step();
                    smothers += m.AwayStats.Smothers;
                }
                if (total == 0) good = sum / seeds; else poor = sum / seeds;
            }
            Assert.Less(good, poor, "GkRushDecisionQuality: the better keeper hesitates less");
            Assert.Greater(smothers, 0, "and smothers the ball at the carrier's feet");
        }

        [Test]
        public void WithADefenderGoalSide_TheKeeperStaysHome()
        {
            for (ulong s = 1; s <= 5; s++)
            {
                var m = OneOnOne(s, 95, defenderGoalSide: true);
                for (int i = 0; i < 20; i++)
                {
                    m.Step();
                    Assert.AreNotEqual(KeeperState.Rushing, m.AwayKeeper.State, $"seed {s}");
                }
            }
        }

        [Test]
        public void AThroughBall_TheKeeperComesOnlyIfHeGetsThereFirst()
        {
            float L = Db().Ball.Pitch.Length * 0.5f;
            foreach (bool attackerClose in new[] { false, true })
            {
                var m = Open(Setup(6));
                var attacker = Outfield(m.Home);
                attacker.Body.Position = attackerClose ? new Vector3(L - 11f, 1f, 0f) : new Vector3(L - 28f, 6f, 0f);
                m.AwayKeeper.Player.Body.Position = new Vector3(L - 1f, 0f, 0f);
                m.Ball.Position = new Vector3(L - 25f, 0f, 0f);
                m.Ball.Kick(new Vector3(14f, 0f, 0f), 0f);
                m.Ball.State = BallState.Rolling;
                m.Ball.LastTouch = Outfield(m.Home, 2).Global;
                bool rushed = false;
                for (int i = 0; i < 25; i++) { m.Step(); rushed |= m.AwayKeeper.State == KeeperState.Rushing; }
                Assert.AreEqual(!attackerClose, rushed, attackerClose ? "the attacker gets there first: he stays" : "he gets there first: he comes");
            }
        }

        // ---------------- domínio (D-36) ----------------

        /// <summary>A home pass arrives at <paramref name="speed"/> to a home receiver standing at the centre spot, an away
        /// marker 1.6 m off him (within the pressure radius, out of capture range). Returns how many of the trials' first
        /// touches escaped him; every trial ends with the receiver touching the ball.</summary>
        private static int Escapes(int trials, int touch, float speed, bool human, bool pressed = true)
        {
            int escaped = 0;
            for (int s = 1; s <= trials; s++)
            {
                var m = Open(Setup((ulong)s, home: new[] { (Attr.Dribbling, touch), (Attr.Composure, touch) }));
                var receiver = Outfield(m.Home);
                var passer = Outfield(m.Home, 2);
                receiver.Body.Position = Vector3.Zero;
                receiver.Body.Velocity = Vector3.Zero;
                receiver.Body.Facing = -Vector3.UnitX;
                if (pressed) Outfield(m.Away).Body.Position = new Vector3(0.4f, 1.55f, 0f);
                if (human) { m.Ball.LastTouch = receiver.Global; m.EnableHuman(MatchSide.Home); } // control starts on him
                // From where the pass still reaches him (rolling friction), at most 4 m out.
                float from = MathF.Min(4f, 0.6f * speed * speed / (2f * Db().Ball.Ball.RollingFrictionDeceleration));
                m.Ball.Position = new Vector3(-from, 0f, 0f);
                m.Ball.Kick(new Vector3(speed, 0f, 0f), 0f);
                m.Ball.LastTouch = passer.Global;
                bool touched = false;
                for (int i = 0; i < 25 && !touched; i++)
                {
                    var ev = m.Step(human ? Input(Vector2.Zero) : HumanInput.None);
                    if (human && (ev == AiMatchEvent.Miscontrol || m.Ball.State == BallState.Controlled))
                        Assert.AreSame(receiver, m.Controlled, $"seed {s}: the user drives the receiver");
                    if (ev == AiMatchEvent.Miscontrol)
                    {
                        Assert.AreEqual(receiver.Global, m.Ball.LastTouch, $"seed {s}: his touch");
                        Assert.AreNotEqual(BallState.Controlled, m.Ball.State, $"seed {s}: the ball ran loose");
                        escaped++;
                        touched = true;
                    }
                    else if (m.Ball.State == BallState.Controlled) { Assert.AreEqual(receiver.Global, m.Ball.Owner, $"seed {s}"); touched = true; }
                }
                Assert.IsTrue(touched, $"seed {s}: the pass reached him");
            }
            return escaped;
        }

        [Test]
        public void AFastPassUnderPressure_EscapesAPoorFirstTouchOften_AndAGoodOneRarely()
        {
            Assert.Greater(Db().Duels.TouchScale, 0f, "duels.json firstTouch is on (D-36)");
            var r = R()["firstTouch"];
            int trials = r["trials"].Value<int>();
            float speed = r["passSpeed"].Value<float>();
            int poor = Escapes(trials, r["poorTouch"].Value<int>(), speed, false);
            int good = Escapes(trials, r["goodTouch"].Value<int>(), speed, false);
            Assert.GreaterOrEqual((double)poor / trials, r["poorEscapeMin"].Value<double>(), "FirstTouchError (Drible + Compostura): a poor touch slips");
            Assert.LessOrEqual((double)good / trials, r["goodEscapeMax"].Value<double>(), "a good one hardly ever");
            Assert.Less(poor, trials, "not every time");
        }

        [Test]
        public void ASlowBall_OrNobodyClose_LeavesTheFirstTouchMostlyClean()
        {
            var r = R()["firstTouch"];
            int trials = r["trials"].Value<int>(), poorTouch = r["poorTouch"].Value<int>();
            Assert.AreEqual(0, Escapes(trials, poorTouch, Db().Duels.TouchMinSpeed * 0.75f, false), "below minSpeed every ball is controlled");
            int pressed = Escapes(trials, poorTouch, r["passSpeed"].Value<float>(), false);
            int free = Escapes(trials, poorTouch, r["passSpeed"].Value<float>(), false, pressed: false);
            Assert.Less(free, pressed, "pressure (PressureErrorMult, Compostura) makes it worse");
        }

        [Test]
        public void TheUsersPlayer_HasTheSameFirstTouch_AsTheAi()
        {
            var r = R()["firstTouch"];
            int trials = r["trials"].Value<int>();
            float speed = r["passSpeed"].Value<float>();
            int poor = Escapes(trials, r["poorTouch"].Value<int>(), speed, true);
            Assert.GreaterOrEqual((double)poor / trials, r["poorEscapeMin"].Value<double>(), "the controlled player's touch is his attributes too");
            Assert.LessOrEqual((double)Escapes(trials, r["goodTouch"].Value<int>(), speed, true) / trials, r["goodEscapeMax"].Value<double>());
        }

        [Test]
        public void FirstTouchValues_AreValidated()
        {
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "duels.json"));
            var j = JObject.Parse(json);
            j["firstTouch"]["threshold"] = 0.0;
            Assert.IsFalse(DuelsReader.Read(j.ToString()).IsSuccess, "threshold must be > 0");
            j = JObject.Parse(json);
            j["firstTouch"]["speedFactorMax"] = 0.1;
            Assert.IsFalse(DuelsReader.Read(j.ToString()).IsSuccess, "speedFactorMax below speedFactorMin");
            j = JObject.Parse(json);
            j["firstTouch"]["scale"] = 0.0;
            Assert.IsTrue(DuelsReader.Read(j.ToString()).IsSuccess, "scale 0 = every reception clean (the rule before D-36)");
        }
    }
}
