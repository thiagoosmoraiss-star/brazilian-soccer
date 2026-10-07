using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>
    /// A4 acceptance (ROADMAP / TEST_PLAN §IA) on full headless 11×11 matches: the match always ends, no intention
    /// oscillation, no bunching, compactness per phase, low Posicionamento → larger target error, determinism, zero
    /// allocation. Ranges and seeds live in Data/TestRanges/ai.json; failures name the seed.
    /// </summary>
    public class AiMatchTests
    {
        private sealed class Recorder : IAiMatchObserver
        {
            public readonly List<(int Player, AiIntention From, AiIntention To, float Time)> Switches = new List<(int, AiIntention, AiIntention, float)>();
            public readonly List<float> PossessionChanges = new List<float>();
            public void OnIntentionChanged(int player, AiIntention from, AiIntention to, float time) => Switches.Add((player, from, to, time));
            public void OnPossessionChanged(MatchSide side, float time) => PossessionChanges.Add(time);
        }

        private sealed class MatchRun
        {
            public ulong Seed;
            public long Milliseconds;
            public bool Finished;
            public int Steps;
            public int ExpectedSteps;
            public int Reversals;
            public float PlayerMinutes;
            public int BunchSamples, BunchViolations;
            public readonly List<float> DefendDepth = new List<float>();
            public readonly List<float> AttackDepth = new List<float>();
            public bool PlayersStayedNearThePitch = true;
            public int Passes, Shots;
        }

        private static JObject _ranges;
        private static List<MatchRun> _runs;

        private static JObject Ranges() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "ai.json"))));

        private static (float Min, float Max) Range(JToken t) => (t[0].Value<float>(), t[1].Value<float>());

        internal static MatchSetup Setup(ulong seed, int homePositioning = -1)
        {
            var m = Ranges()["match"];
            int attrs = m["attributes"].Value<int>();
            var tactic = new TacticSetup(m["formation"].Value<string>(), 3, 2, 2);
            MatchTeamSetup Team(int club, int positioning) => new MatchTeamSetup(new Id(club), tactic,
                Enumerable.Range(0, 11).Select(_ => positioning < 0 ? Player(attrs) : Player(attrs, overrides: (Attr.Positioning, positioning))).ToArray(),
                new MatchPlayerSetup[0]);
            return new MatchSetup(Team(1, homePositioning), Team(2, -1), false, m["durationMinutes"].Value<int>(), 5, seed);
        }

        private static List<MatchRun> Runs()
        {
            if (_runs != null) return _runs;
            var r = Ranges();
            int sampleEvery = r["match"]["sampleEverySteps"].Value<int>();
            float window = r["oscillation"]["windowSeconds"].Value<float>();
            float bunchRadius = r["bunch"]["radius"].Value<float>();
            int bunchMax = r["bunch"]["maxPlayers"].Value<int>();
            var runs = new List<MatchRun>();
            foreach (var seed in r["match"]["seeds"].Values<ulong>())
            {
                var rec = new Recorder();
                var setup = Setup(seed);
                var match = new AiMatch(Db(), setup, Dt, rec);
                var run = new MatchRun { Seed = seed, ExpectedSteps = (int)Math.Ceiling(match.DurationSeconds / Dt) };
                var sw = Stopwatch.StartNew();
                int guard = run.ExpectedSteps * 2;
                while (!match.Finished && run.Steps < guard)
                {
                    var ev = match.Step();
                    run.Steps++;
                    if (ev == AiMatchEvent.Pass) run.Passes++;
                    if (ev == AiMatchEvent.Shot) run.Shots++;
                    if (run.Steps % sampleEvery == 0) Sample(match, run, bunchRadius, bunchMax);
                }
                run.Milliseconds = sw.ElapsedMilliseconds;
                run.Finished = match.Finished;
                run.PlayerMinutes = match.Players.Length * match.ElapsedSeconds / 60f;
                run.Reversals = CountReversals(rec, window);
                runs.Add(run);
            }
            return _runs = runs;
        }

        private static void Sample(AiMatch m, MatchRun run, float bunchRadius, int bunchMax)
        {
            foreach (var team in new[] { m.Home, m.Away })
            {
                float min = float.MaxValue, max = float.MinValue;
                foreach (var p in team.Players)
                {
                    var pos = p.Body.Position;
                    if (MathF.Abs(pos.X) > m.Pitch.HalfLength + 3f || MathF.Abs(pos.Y) > m.Pitch.HalfWidth + 3f) run.PlayersStayedNearThePitch = false;
                    if (p.IsGoalkeeper) continue;
                    float u = team.Frame.U(pos);
                    min = MathF.Min(min, u);
                    max = MathF.Max(max, u);
                }
                if (team.Phase == TeamPhase.Defend) run.DefendDepth.Add(max - min);
                if (team.Phase == TeamPhase.Attack) run.AttackDepth.Add(max - min);

                if (m.Ball.State != BallState.Controlled) continue; // a loose ball is a contest: crowding is allowed
                run.BunchSamples++;
                if (team.Players.Count(p => Vector3.Distance(p.Body.Position, m.Ball.Position) < bunchRadius) > bunchMax) run.BunchViolations++;
            }
        }

        private static bool Positional(AiIntention i) =>
            i == AiIntention.HoldShape || i == AiIntention.Support || i == AiIntention.Mark || i == AiIntention.Press;

        /// <summary>A→B→A within the window between positional intentions with no change of possession in between.</summary>
        private static int CountReversals(Recorder rec, float window)
        {
            int count = 0;
            foreach (var g in rec.Switches.GroupBy(s => s.Player))
            {
                var list = g.ToList();
                for (int i = 1; i < list.Count; i++)
                {
                    var a = list[i - 1];
                    var b = list[i];
                    if (b.To != a.From || b.Time - a.Time >= window) continue;
                    if (!Positional(a.From) || !Positional(a.To)) continue;
                    if (rec.PossessionChanges.Any(t => t >= a.Time && t <= b.Time)) continue;
                    count++;
                }
            }
            return count;
        }

        [Test]
        public void HeadlessMatch_AlwaysEnds_OnTime_AndPlaysFootball()
        {
            long maxMs = Ranges()["match"]["maxMillisecondsPerMatch"].Value<long>();
            foreach (var run in Runs())
            {
                Assert.IsTrue(run.Finished, $"seed {run.Seed}: the headless match did not end.");
                Assert.AreEqual(run.ExpectedSteps, run.Steps, 1, $"seed {run.Seed}: steps.");
                Assert.LessOrEqual(run.Milliseconds, maxMs, $"seed {run.Seed}: {run.Milliseconds} ms.");
                Assert.IsTrue(run.PlayersStayedNearThePitch, $"seed {run.Seed}: a player wandered off the pitch.");
                Assert.Greater(run.Passes, 0, $"seed {run.Seed}: the AI never passed.");
                Assert.Greater(run.Shots, 0, $"seed {run.Seed}: the AI never shot.");
            }
        }

        [Test]
        public void NoIntentionOscillation()
        {
            float max = Ranges()["oscillation"]["maxReversalsPerPlayerMinute"].Value<float>();
            foreach (var run in Runs())
            {
                float rate = run.Reversals / run.PlayerMinutes;
                Assert.LessOrEqual(rate, max, $"seed {run.Seed}: {run.Reversals} reversals ({rate:F2} per player-minute).");
            }
        }

        [Test]
        public void NoBunchingAroundTheBall()
        {
            float max = Ranges()["bunch"]["maxViolatingFraction"].Value<float>();
            foreach (var run in Runs())
            {
                float fraction = run.BunchViolations / (float)Math.Max(1, run.BunchSamples);
                Assert.LessOrEqual(fraction, max, $"seed {run.Seed}: {run.BunchViolations}/{run.BunchSamples} samples bunched.");
            }
        }

        [Test]
        public void Compactness_StaysWithinThePhaseLimits_AndIsTighterDefending()
        {
            var (dMin, dMax) = Range(Ranges()["compactness"]["defendDepth"]);
            var (aMin, aMax) = Range(Ranges()["compactness"]["attackDepth"]);
            foreach (var run in Runs())
            {
                float defend = run.DefendDepth.Average();
                float attack = run.AttackDepth.Average();
                Assert.That(defend, Is.InRange(dMin, dMax), $"seed {run.Seed}: defending depth {defend:F1} m.");
                Assert.That(attack, Is.InRange(aMin, aMax), $"seed {run.Seed}: attacking depth {attack:F1} m.");
                Assert.Less(defend, attack, $"seed {run.Seed}: the block must be tighter when defending.");
            }
        }

        [Test]
        public void LowPositioning_MeansLargerTargetError()
        {
            var p = Ranges()["positioning"];
            ulong seed = Ranges()["match"]["seeds"].First.Value<ulong>();
            float low = MeanHomeTargetError(Setup(seed, p["low"].Value<int>()));
            float high = MeanHomeTargetError(Setup(seed, p["high"].Value<int>()));
            Assert.GreaterOrEqual(low, high * p["minErrorRatio"].Value<float>(),
                $"seed {seed}: Posicionamento {p["low"]} error {low:F2} m vs {p["high"]} error {high:F2} m.");
        }

        private static float MeanHomeTargetError(MatchSetup setup)
        {
            var m = new AiMatch(Db(), setup, Dt);
            double sum = 0;
            int n = 0;
            while (!m.Finished)
            {
                m.Step();
                if (m.ElapsedSeconds < 5f) continue; // let the first error draws land
                foreach (var pl in m.Home.Players)
                {
                    if (pl.IsGoalkeeper) continue;
                    sum += Vector3.Distance(pl.RoleTarget, pl.IdealTarget);
                    n++;
                }
            }
            return (float)(sum / n);
        }

        [Test]
        public void SameSeed_SameMatch()
        {
            ulong seed = Ranges()["match"]["seeds"].First.Value<ulong>();
            var a = new AiMatch(Db(), Setup(seed), Dt);
            var b = new AiMatch(Db(), Setup(seed), Dt);
            for (int i = 0; i < 50 * 60; i++) { a.Step(); b.Step(); }
            Assert.AreEqual(a.Ball.Position, b.Ball.Position, $"seed {seed}");
            Assert.AreEqual(a.Home.Goals, b.Home.Goals);
            for (int i = 0; i < a.Players.Length; i++) Assert.AreEqual(a.Players[i].Body.Position, b.Players[i].Body.Position, $"seed {seed}, player {i}");
        }

        [Test]
        public void Step_AllocatesNoMemory()
        {
            var m = new AiMatch(Db(), Setup(1), Dt);
            for (int i = 0; i < 50 * 20; i++) m.Step(); // warm-up: JIT, first kicks
            // Measured around each Step call only, so runtime work on the test's own loop (tiered JIT / OSR) is not counted.
            long allocated = 0;
            for (int i = 0; i < 50 * 60; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                m.Step();
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Assert.AreEqual(0, allocated, "CLAUDE.md §10: zero allocation per step in the match loop.");
        }
    }
}
