using System;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Data.Match;
using Game.Match.AI;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>X-91 (D-21 option a): injuries in the playable match with QuickSim's chance and severity, the forced
    /// substitution (the AI's at once, the user's chosen in the pause), one short without a substitution left, the
    /// injury in the result, snapshot/restore and determinism. The rate against QuickSim is in CalibrationTests
    /// (injuriesPerMatch).</summary>
    public class InjuryTests
    {
        private static MatchPlayerSetup P(int id, Position position)
        {
            var a = Enumerable.Repeat(70, AttrInfo.Count).ToArray();
            return new MatchPlayerSetup(new Id(id), a, (int)position, new int[0], 100f, 3, null);
        }

        /// <summary>4-4-2 sides of 70s, a bench of 7 (ids club*100+50+k, positions of the slots in order).</summary>
        private static MatchSetup Setup(ulong seed, int maxSubs = 5, int bench = 7, int minutes = 4)
        {
            var formation = Db().Formation("4-4-2");
            var tactic = new TacticSetup("4-4-2", 3, 2, 2);
            MatchTeamSetup Team(int club) => new MatchTeamSetup(new Id(club), tactic,
                Enumerable.Range(0, 11).Select(i => P(club * 100 + i, formation.Slots[i].Position)).ToArray(),
                Enumerable.Range(0, bench).Select(k => P(club * 100 + 50 + k, formation.Slots[k % 11].Position)).ToArray());
            return new MatchSetup(Team(1), Team(2), false, minutes, maxSubs, seed);
        }

        private static MatchSetup SliceSetup(ulong seed, int minutes)
        {
            var r = GameDataLoader.LoadVerticalSlice(new DirectoryDataSource(TestPaths.DataRoot()), Db());
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return new MatchSetup(r.Value.Team("strong").ToSetup(), r.Value.Team("weak").ToSetup(), false, minutes, 5, seed);
        }

        private static void Steps(AiMatch m, int n)
        {
            for (int i = 0; i < n && !m.Finished; i++) m.Step();
        }

        /// <summary>Plays the session to the end; a later injury of the user's side is replaced by the first free substitute.</summary>
        private static void PlayOut(MatchSession s)
        {
            var m = s.Match;
            int guard = 0;
            while (!m.Finished)
            {
                Assert.Less(++guard, 100000, "the session got stuck");
                if (s.Paused)
                {
                    int injured = s.PendingInjury;
                    if (injured >= 0)
                        for (int k = 0; k < m.Bench(m.HumanTeam.Side).Count; k++)
                            if (s.Substitute(m.HumanTeam.Side, injured, k)) break;
                    s.Resume();
                }
                s.Advance(0.5f, Vector2.Zero, false, false);
            }
        }

        private static PlayerMatchStats StatsOf(MatchResult r, Id id) => r.PlayerStats.Single(s => s.PlayerId == id);

        [Test]
        public void TheAisInjuredPlayer_IsReplacedAtOnce_WithoutUsingAStoppage_AndTheResultCarriesTheInjury()
        {
            var m = new AiMatch(Db(), Setup(3), Dt);
            Steps(m, 3000);
            var injured = m.Away.Players[6];
            var id = injured.Setup.PlayerId;
            int stoppages = m.StoppagesLeft(MatchSide.Away), made = m.SubstitutionsMade(MatchSide.Away);
            Assert.IsTrue(m.Injure(MatchSide.Away, 6, InjurySeverity.Medium));

            Assert.AreNotEqual(id, injured.Setup.PlayerId, "a substitute took the slot at once");
            Assert.IsFalse(m.Injured(injured), "the slot's new player is fit");
            Assert.AreEqual(made + 1, m.SubstitutionsMade(MatchSide.Away));
            Assert.AreEqual(stoppages, m.StoppagesLeft(MatchSide.Away), "a forced change uses no stoppage (as in QuickSim)");
            Assert.AreEqual(-1, m.PendingInjury(MatchSide.Away));
            Assert.AreEqual(1, m.Injuries(MatchSide.Away));
            Assert.AreEqual(11, m.PlayersOnPitch(m.Away));

            while (!m.Finished) m.Step();
            var r = m.Result();
            Assert.AreEqual(InjurySeverity.Medium, StatsOf(r, id).Injury);
            Assert.IsFalse(StatsOf(r, id).RedCard);
            Assert.IsTrue(r.Events.Any(e => e.Type == MatchEventType.Injury && e.PlayerId == id));
            var sub = r.Events.First(e => e.Type == MatchEventType.Substitution && e.PlayerId == id);
            Assert.IsFalse(StatsOf(r, sub.OtherPlayerId).Started);
            Assert.AreEqual(InjurySeverity.None, StatsOf(r, sub.OtherPlayerId).Injury);
        }

        [Test]
        public void TheUsersInjury_PausesTheSession_AndOnlyTheInjuredPlayerMayLeave_UntilReplaced()
        {
            var s = new MatchSession(Db(), Setup(4), Dt, MatchSide.Home) { PauseOnInjury = true };
            var m = s.Match;
            for (int i = 0; i < 100; i++) s.Advance(0.1f, Vector2.Zero, false, false);
            int stoppages = m.StoppagesLeft(MatchSide.Home);
            Assert.IsTrue(m.Injure(MatchSide.Home, 3, InjurySeverity.Light));
            var injured = m.Home.Players[3];
            Assert.AreEqual(3, s.PendingInjury, "the user chooses who comes on");
            Assert.IsTrue(m.Injured(injured));

            Assert.AreEqual(0, s.Advance(0.1f, Vector2.Zero, false, false), "no step past the user's choice");
            Assert.IsTrue(s.Paused);
            Assert.IsTrue(s.InjuryStop);
            s.Resume();
            Assert.IsTrue(s.Paused, "resume is refused while the injured player waits");
            Assert.IsFalse(s.Substitute(MatchSide.Home, 4, 0), "only the injured player may leave now");
            Assert.IsFalse(m.CanSubstitute(MatchSide.Home, 4, 0));
            Assert.IsTrue(m.CanSubstitute(MatchSide.Home, 3, 0));

            var id = injured.Setup.PlayerId;
            Assert.IsTrue(s.Substitute(MatchSide.Home, 3, 0));
            Assert.AreEqual(-1, s.PendingInjury);
            Assert.AreEqual(stoppages, m.StoppagesLeft(MatchSide.Home), "the forced change uses no stoppage");
            Assert.IsTrue(s.Substitute(MatchSide.Home, 5, 1), "another change in the same pause is a normal one");
            Assert.AreEqual(stoppages - 1, m.StoppagesLeft(MatchSide.Home));
            s.Resume();
            Assert.IsFalse(s.Paused);
            Assert.IsFalse(s.InjuryStop);
            Assert.Greater(s.Advance(0.1f, Vector2.Zero, false, false), 0);

            PlayOut(s);
            Assert.AreEqual(InjurySeverity.Light, StatsOf(m.Result(), id).Injury);
        }

        [Test]
        public void TheUsersInjury_StepsTakenWithoutTheSession_MakeTheAisChoice()
        {
            var m = new AiMatch(Db(), Setup(5), Dt);
            m.EnableHuman(MatchSide.Home);
            Steps(m, 500);
            var injured = m.Home.Players[2];
            var id = injured.Setup.PlayerId;
            Assert.IsTrue(m.Injure(MatchSide.Home, 2, InjurySeverity.Severe));
            Assert.AreEqual(2, m.PendingInjury(MatchSide.Home));
            m.Step();
            Assert.AreEqual(-1, m.PendingInjury(MatchSide.Home));
            Assert.AreNotEqual(id, injured.Setup.PlayerId);
            Assert.AreEqual(1, m.SubstitutionsMade(MatchSide.Home));
        }

        [Test]
        public void WithoutASubstitutionLeft_TheTeamPlaysOneShort_AndTheKeeperPlaysOn()
        {
            var s = new MatchSession(Db(), Setup(6, maxSubs: 0), Dt, MatchSide.Home) { PauseOnInjury = true };
            var m = s.Match;
            for (int i = 0; i < 100; i++) s.Advance(0.1f, Vector2.Zero, false, false);
            var injured = m.Home.Players[7];
            Assert.IsTrue(m.Injure(MatchSide.Home, 7, InjurySeverity.Medium));
            Assert.AreEqual(-1, s.PendingInjury, "nothing to choose");
            Assert.IsTrue(injured.Body.OffPitch);
            Assert.AreEqual(10, m.PlayersOnPitch(m.Home));
            Assert.IsFalse(m.SentOff(injured), "injured, not dismissed");
            Assert.AreEqual(1, s.Advance(0.1f, Vector2.Zero, false, false), "the step in progress ends, then the warning");
            Assert.IsTrue(s.Paused && s.InjuryStop, "the user is warned");
            s.Resume();
            Assert.IsFalse(s.Paused);
            Assert.IsFalse(m.Injure(MatchSide.Home, 7, InjurySeverity.Light), "already off");

            var keeper = m.Home.Players.First(p => p.IsGoalkeeper);
            Assert.IsTrue(m.Injure(MatchSide.Home, keeper.Local, InjurySeverity.Light));
            Assert.IsFalse(keeper.Body.OffPitch, "the keeper plays on: this engine always has one in goal");
            Assert.AreEqual(10, m.PlayersOnPitch(m.Home));

            int minute = m.Events.First(e => e.Type == MatchEventType.Injury && e.PlayerId == injured.Setup.PlayerId).Minute;
            PlayOut(s);
            var r = m.Result();
            var st = StatsOf(r, injured.Setup.PlayerId);
            Assert.AreEqual(InjurySeverity.Medium, st.Injury);
            Assert.IsFalse(st.RedCard);
            Assert.AreEqual(minute, st.MinutesPlayed, "he played until the injury");
            Assert.AreEqual(InjurySeverity.Light, StatsOf(r, keeper.Setup.PlayerId).Injury);
            Assert.AreEqual(0, r.HomeStats.Substitutions);
        }

        [Test]
        public void AnInjuredTakerWithoutReplacement_HandsTheRestartToATeammate()
        {
            var m = new AiMatch(Db(), Setup(8, maxSubs: 0), Dt);
            m.AwardFreeKick(MatchSide.Home, new Vector3(-10f, 5f, 0f), direct: false);
            var taker = m.Taker;
            Assert.IsNotNull(taker);
            Assert.IsTrue(m.Injure(MatchSide.Home, taker.Local, InjurySeverity.Light));
            Assert.IsTrue(taker.Body.OffPitch);
            Assert.IsNotNull(m.Taker);
            Assert.AreNotSame(taker, m.Taker);
            Assert.IsFalse(m.Taker.Body.OffPitch);
            int guard = 0;
            while (m.Restart != RestartKind.None) { m.Step(); Assert.Less(++guard, 5000, "the restart is taken"); }
            while (!m.Finished) m.Step();
        }

        /// <summary>Natural injuries (data chance): the same seed gives the same injuries, and a snapshot taken after one
        /// restores to the same match.</summary>
        [Test]
        [Category("Slow")]
        public void NaturalInjuries_AreDeterministic_AndSurviveSnapshotRestore()
        {
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var setup = SliceSetup(seed, 4);
                var a = new AiMatch(Db(), setup, Dt);
                a.EnableHuman(MatchSide.Home);
                while (!a.Finished && a.Injuries(MatchSide.Home) + a.Injuries(MatchSide.Away) == 0) a.Step();
                if (a.Finished) continue;
                TestContext.WriteLine($"first injury with seed {seed} at minute {a.Events.Last(e => e.Type == MatchEventType.Injury).Minute}");

                var b = AiMatch.Restore(Db(), setup, Dt, a.Snapshot());
                CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot(), $"seed {seed}");
                Assert.AreEqual(a.PendingInjury(MatchSide.Home), b.PendingInjury(MatchSide.Home), $"seed {seed}");
                for (int i = 0; i < a.Players.Length; i++) Assert.AreEqual(a.Injured(a.Players[i]), b.Injured(b.Players[i]), $"seed {seed} slot {i}");
                while (!a.Finished) { a.Step(); b.Step(); }
                CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot(), $"restored match diverged, seed {seed}");

                var c = new AiMatch(Db(), setup, Dt);
                c.EnableHuman(MatchSide.Home);
                c.RunToEnd();
                var ra = a.Result();
                var rc = c.Result();
                Assert.AreEqual(ra.Events.Count, rc.Events.Count, $"seed {seed}");
                for (int i = 0; i < ra.Events.Count; i++)
                    Assert.AreEqual((ra.Events[i].Minute, ra.Events[i].Type, ra.Events[i].PlayerId), (rc.Events[i].Minute, rc.Events[i].Type, rc.Events[i].PlayerId),
                        $"seed {seed} event {i}");
                Assert.IsTrue(ra.PlayerStats.Any(p => p.Injury != InjurySeverity.None), $"seed {seed}");
                return;
            }
            Assert.Fail("no injury in 80 matches of 4 min: the chance per player-minute is not reaching the played match");
        }

        [Test]
        public void SnapshotAndRestore_WhileTheUsersInjuredPlayerWaits_PlaysTheSameMatch()
        {
            var setup = Setup(9);
            var s = new MatchSession(Db(), setup, Dt, MatchSide.Away) { PauseOnInjury = true };
            for (int i = 0; i < 200; i++) s.Advance(0.1f, Vector2.Zero, false, false);
            Assert.IsTrue(s.Match.Injure(MatchSide.Away, 9, InjurySeverity.Medium));
            s.Advance(0.1f, Vector2.Zero, false, false);
            Assert.IsTrue(s.Paused);

            var r = MatchSession.Restore(Db(), setup, Dt, s.Snapshot());
            r.PauseOnInjury = true;
            Assert.AreEqual(9, r.PendingInjury, "the restored match still waits for the user");
            Assert.IsTrue(r.Paused);
            Assert.IsTrue(s.Substitute(MatchSide.Away, 9, 2));
            Assert.IsTrue(r.Substitute(MatchSide.Away, 9, 2));
            s.Resume();
            r.Resume();
            s.PauseOnInjury = r.PauseOnInjury = false; // later injuries of the user: the AI's choice by stepping on
            while (!s.Match.Finished)
            {
                s.Advance(0.3f, Vector2.Zero, false, false);
                r.Advance(0.3f, Vector2.Zero, false, false);
            }
            CollectionAssert.AreEqual(s.Snapshot(), r.Snapshot());
        }
    }
}
