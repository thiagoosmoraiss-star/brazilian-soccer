using System;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Match;
using Game.Match.AI;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A7a: the match's single output (MatchSetup → MatchResult, D-19) and the full-state snapshot behind pause
    /// and resume (TECHNICAL_SPEC §5: "MatchState inteiro serializável ... restaura pausado").</summary>
    public class MatchStateTests
    {
        /// <summary>A scripted user: runs around, passes and shoots now and then, switches, contains.</summary>
        private static HumanInput Script(long step)
        {
            var move = new Vector2(MathF.Cos(step * 0.013f), MathF.Sin(step * 0.021f));
            var action = step % 173 == 0 ? new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower)
                : step % 409 == 0 ? new ActionCommand(ActionKind.Shot, 0.6f) : ActionCommand.None;
            return new HumanInput(move, step % 7 < 3, action, step % 300 < 60, step % 251 == 0);
        }

        private static AiMatch NewMatch(ulong seed, bool human)
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
            if (human) m.EnableHuman(MatchSide.Home);
            return m;
        }

        [Test]
        public void RestoredMatch_PlaysOnExactlyLikeTheOriginal([Values(false, true)] bool human)
        {
            const ulong seed = 5;
            var a = NewMatch(seed, human);
            long step = 0;
            // Snapshot at several moments, including while a restart is being set up or waiting to be taken.
            int checkedRestart = 0, checkpoints = 0;
            while (!a.Finished)
            {
                bool inRestart = a.Restart != RestartKind.None && step > 0;
                if (step % 2500 == 1200 || (inRestart && checkedRestart < 2))
                {
                    if (inRestart) checkedRestart++;
                    checkpoints++;
                    var b = AiMatch.Restore(Db(), AiMatchTests.Setup(seed), Dt, a.Snapshot());
                    CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot(), $"step {step}: restore is not exact.");
                    for (int i = 0; i < 300 && !a.Finished; i++, step++)
                    {
                        var input = human ? Script(step) : HumanInput.None;
                        Assert.AreEqual(a.Step(input), b.Step(input), $"seed {seed}, step {step}: events diverged.");
                    }
                    CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot(), $"seed {seed}, step {step}: the restored match diverged.");
                    continue;
                }
                a.Step(human ? Script(step) : HumanInput.None);
                step++;
            }
            Assert.GreaterOrEqual(checkpoints, 3);
            Assert.Greater(checkedRestart, 0, "no snapshot was taken during a restart.");
        }

        [Test]
        public void Snapshot_OfAnotherMatch_IsRejected()
        {
            var a = NewMatch(5, false);
            for (int i = 0; i < 100; i++) a.Step();
            var snap = a.Snapshot();
            Assert.Throws<ArgumentException>(() => AiMatch.Restore(Db(), AiMatchTests.Setup(6), Dt, snap), "another seed");
            Assert.Throws<ArgumentException>(() => AiMatch.Restore(Db(), AiMatchTests.Setup(5), Dt * 2, snap), "another step");
            Assert.Throws<ArgumentException>(() => AiMatch.Restore(Db(), AiMatchTests.Setup(5), Dt, snap.Take(snap.Length - 9).ToArray()), "truncated");
        }

        [Test]
        public void Result_AddsUp()
        {
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                var m = NewMatch(seed, false);
                m.RunToEnd();
                var r = m.Result();
                Assert.AreEqual(m.Home.Goals, r.HomeGoals);
                Assert.AreEqual(m.Away.Goals, r.AwayGoals);
                Assert.AreEqual(r.HomeGoals + r.AwayGoals, r.Events.Count(e => e.Type == MatchEventType.Goal), $"seed {seed}: goal events.");
                foreach (var side in new[] { MatchSide.Home, MatchSide.Away })
                {
                    var s = r.Stats(side);
                    var players = r.PlayerStats.Where(p => p.Side == side).ToList();
                    Assert.AreEqual(11, players.Count);
                    Assert.AreEqual(s.Shots, players.Sum(p => p.Shots), $"seed {seed} {side}: shots.");
                    Assert.AreEqual(s.ShotsOnTarget, players.Sum(p => p.ShotsOnTarget), $"seed {seed} {side}: on target.");
                    Assert.LessOrEqual(s.ShotsOnTarget, s.Shots);
                    Assert.LessOrEqual(players.Sum(p => p.Goals), r.Goals(side), $"seed {seed} {side}: scorers (own goals have none).");
                    Assert.AreEqual(s.Corners, r.Events.Count(e => e.Type == MatchEventType.Corner && e.Side == side));
                    Assert.IsTrue(players.All(p => p.FinalEnergy >= 0f && p.FinalEnergy <= 100f));
                }
                Assert.AreEqual(100f, r.HomeStats.Possession + r.AwayStats.Possession, 1e-3f);
                Assert.IsTrue(r.Events.All(e => e.Minute >= 1 && e.Minute <= m.TotalDisplayMinutes), $"seed {seed}: event minutes.");
                Assert.IsTrue(r.Events.Select(e => e.Minute).Zip(r.Events.Select(e => e.Minute).Skip(1), (x, y) => x <= y).All(ok => ok), "events in order.");
            }
        }

        [Test]
        public void MatchWithRestartsAndClock_AllocatesNoMemoryPerStep()
        {
            var m = NewMatch(3, true);
            for (int i = 0; i < 50 * 30; i++) m.Step(Script(i)); // warm up
            long allocated = 0;
            int restarts = 0;
            for (int i = 50 * 30; i < 50 * 150 && !m.Finished; i++)
            {
                var input = Script(i);
                long before = GC.GetAllocatedBytesForCurrentThread();
                var ev = m.Step(input);
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                if (ev == AiMatchEvent.Out || ev == AiMatchEvent.HalfTime || ev == AiMatchEvent.Goal) restarts++;
            }
            Assert.Greater(restarts, 0, "the window had no restart.");
            Assert.AreEqual(0, allocated, "CLAUDE.md §10: zero allocation per step.");
        }
    }
}
