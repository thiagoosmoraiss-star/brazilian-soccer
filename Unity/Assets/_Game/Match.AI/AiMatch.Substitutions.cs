using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Match;
using SubRules = Game.Rules.Match.Substitutions;

namespace Game.Match.AI
{
    /// <summary>
    /// Substitutions in the playable match (A9c, X-77), with the rules shared with QuickSim (<see cref="SubRules"/>;
    /// TECHNICAL_SPEC §10 "Substituições | Mesmas regras e IA"; GAME_DESIGN §27 "5 em 3 paradas + intervalo"). A substitute
    /// takes over the slot of the player who leaves: same formation slot, same <see cref="AiPlayer.Global"/>, his own
    /// setup and energy. The user substitutes his side (<see cref="Substitute"/>, during the pause or a dead ball); the
    /// AI substitutes its side at half-time and at the data's windows (displayed clock), at the next dead ball. Zero
    /// allocation per step.
    /// </summary>
    public sealed partial class AiMatch
    {
        /// <summary>One player who left the pitch (his slot now holds someone else): kept for <see cref="Result"/>.</summary>
        private struct Departed
        {
            public int Global;
            /// <summary>Bench index of the player, or -1 for the slot's starter.</summary>
            public int BenchIndex;
            public int MinuteOn, MinuteOff;
            public int Goals, Assists, Shots, ShotsOnTarget, Fouls, Yellows;
            public float Energy;
        }

        private MatchSetup _matchSetup;
        private SubstitutionRules SubstitutionRules => _rules.Definition.Substitutions;
        // Per side (0 home, 1 away).
        private readonly int[] _subsMade = new int[2];
        private readonly int[] _stoppagesUsed = new int[2];
        /// <summary>Step at which the side last substituted: further changes in the same pause are the same stoppage.</summary>
        private readonly long[] _stoppageStep = { -1L, -1L };
        private readonly int[] _tiredSubs = new int[2];
        /// <summary>Bit i set: the AI already handled window i of the data (or it was not one for the AI).</summary>
        private readonly int[] _windowsDone = new int[2];
        private bool[][] _benchUsed;
        /// <summary>Unused substitutes per side, in bench order (the AI's choice; removing never allocates).</summary>
        private List<MatchPlayerSetup>[] _available;
        // Per slot (global index).
        /// <summary>Bench index of the slot's current player, or -1 for the starter.</summary>
        private readonly int[] _slotBench = new int[2 * MatchTeamSetup.StarterCount];
        private readonly int[] _slotMinuteOn = new int[2 * MatchTeamSetup.StarterCount];
        private Departed[] _departed;
        private int _departedCount;

        /// <summary>The break between halves: the clock stopped at the end of a half, the next one not started. A
        /// substitution here does not use one of the stoppages.</summary>
        public bool IsHalfTimeBreak => _step > 0 && !Finished && _step % _halfSteps == 0;

        public IReadOnlyList<MatchPlayerSetup> Bench(MatchSide side) => _matchSetup.Team(side).Bench;
        public bool BenchUsed(MatchSide side, int benchIndex) => _benchUsed[(int)side][benchIndex];
        public int SubstitutionsMade(MatchSide side) => _subsMade[(int)side];
        public int SubstitutionsLeft(MatchSide side) => Math.Max(0, _matchSetup.MaxSubstitutions - _subsMade[(int)side]);
        /// <summary>Stoppages (besides half-time) the side may still use to substitute.</summary>
        public int StoppagesLeft(MatchSide side) => Math.Max(0, SubstitutionRules.MaxWindows - _stoppagesUsed[(int)side]);
        /// <summary>True when the side already substituted in this pause: more changes now do not use another stoppage.</summary>
        public bool StoppageInProgress(MatchSide side) => _stoppageStep[(int)side] == _step;
        /// <summary>True when the slot holds a substitute.</summary>
        public bool CameOn(AiPlayer p) => _slotBench[p.Global] >= 0;

        private void InitSubstitutions(MatchSetup setup)
        {
            _matchSetup = setup;
            _benchUsed = new[] { new bool[setup.Home.Bench.Count], new bool[setup.Away.Bench.Count] };
            _available = new[] { new List<MatchPlayerSetup>(setup.Home.Bench), new List<MatchPlayerSetup>(setup.Away.Bench) };
            _departed = new Departed[setup.Home.Bench.Count + setup.Away.Bench.Count];
            for (int i = 0; i < _slotBench.Length; i++) _slotBench[i] = -1;
            var windows = SubstitutionRules.Windows;
            for (int i = 0; i < windows.Count && i < 31; i++)
                if (windows[i] == SubstitutionRules.HalftimeMinute) { _windowsDone[0] |= 1 << i; _windowsDone[1] |= 1 << i; }
        }

        /// <summary>The checks of <see cref="Substitute"/> without applying it (the panel greys out what is not allowed).</summary>
        public bool CanSubstitute(MatchSide side, int outLocal, int benchIndex)
        {
            int s = (int)side;
            var team = side == MatchSide.Home ? Home : Away;
            var bench = _matchSetup.Team(side).Bench;
            if (Finished || outLocal < 0 || outLocal >= team.Players.Length || benchIndex < 0 || benchIndex >= bench.Count) return false;
            if (!SubRules.HasSubstitutionLeft(_subsMade[s], _matchSetup.MaxSubstitutions)) return false;
            if (!StoppageAvailable(s)) return false;
            var off = team.Players[outLocal];
            if (!SubRules.CanLeave(!off.Body.OffPitch, _sentOffMinute[off.Global] > 0)) return false;
            return SubRules.CanEnter(_benchUsed[s][benchIndex], false); // nobody on the bench is booked in this engine
        }

        /// <summary>Takes <paramref name="outLocal"/> (team slot) off for substitute <paramref name="benchIndex"/>. False,
        /// and nothing changes, when not allowed: limits, a dismissed player, a substitute already used. Call it between
        /// steps; <see cref="MatchSession.Substitute"/> also requires the pause or a dead ball.</summary>
        public bool Substitute(MatchSide side, int outLocal, int benchIndex)
        {
            if (!CanSubstitute(side, outLocal, benchIndex)) return false;
            Apply(side == MatchSide.Home ? Home : Away, outLocal, benchIndex);
            return true;
        }

        private bool StoppageAvailable(int s) =>
            _stoppageStep[s] == _step || SubRules.HasStoppageLeft(_stoppagesUsed[s], IsHalfTimeBreak, SubstitutionRules);

        private int SubMinute => Math.Max(0, Math.Min(TotalDisplayMinutes, (int)ClockMinutes));

        private void Apply(AiTeam team, int local, int benchIndex)
        {
            int s = (int)team.Side;
            var p = team.Players[local];
            int g = p.Global;
            var on = _matchSetup.Team(team.Side).Bench[benchIndex];
            int minute = SubMinute;

            ref var d = ref _departed[_departedCount++];
            d.Global = g;
            d.BenchIndex = _slotBench[g];
            d.MinuteOn = _slotMinuteOn[g];
            d.MinuteOff = minute;
            d.Goals = _playerGoals[g];
            d.Assists = _playerAssists[g];
            d.Shots = _playerShots[g];
            d.ShotsOnTarget = _playerShotsOnTarget[g];
            d.Fouls = _playerFouls[g];
            d.Yellows = _playerYellows[g];
            d.Energy = p.Body.Energy;
            RecordEvent(MatchEventType.Substitution, team.Side, p.Setup.PlayerId, on.PlayerId);
            _playerGoals[g] = _playerAssists[g] = _playerShots[g] = _playerShotsOnTarget[g] = _playerFouls[g] = _playerYellows[g] = 0;
            if (_assistCandidate == g) _assistCandidate = -1;
            if (_pendingShot == g) _pendingShot = -1;

            _benchUsed[s][benchIndex] = true;
            _available[s].Remove(on);
            _slotBench[g] = benchIndex;
            _slotMinuteOn[g] = minute;
            _subsMade[s]++;
            if (_stoppageStep[s] != _step && !IsHalfTimeBreak) _stoppagesUsed[s]++;
            _stoppageStep[s] = _step;

            EnterSlot(p, on);
            ResetForEntry(p);
            if (team == HumanTeam && Control.Controlled == local)
                Control.OnCaptured(NearestOutfield(HumanTeam, Ball.Position).Local); // the user's player left
        }

        /// <summary>Puts <paramref name="setup"/> in <paramref name="p"/>'s slot: attributes, contact weight, the user's
        /// control candidates.</summary>
        private void EnterSlot(AiPlayer p, MatchPlayerSetup setup)
        {
            p.Setup = setup;
            _bodyWeight[p.Global] = BodyWeight(setup);
            if (p.Team == HumanTeam) _humanSetups[p.Local] = setup;
        }

        /// <summary>The substitute comes on where the other one was, fresh: his own energy, no slide, no stumble, holding
        /// his position.</summary>
        private void ResetForEntry(AiPlayer p)
        {
            var b = p.Body;
            b.Energy = p.Setup.Energy;
            b.Velocity = Vector3.Zero;
            b.Sprinting = false;
            b.TurnStunRemaining = 0f;
            b.TackleCooldown = 0f;
            b.SlideRemaining = b.SlideSpeed = b.SlideDeceleration = b.GroundRemaining = 0f;
            b.SlideDirection = Vector3.Zero;
            b.SlideWonBall = false;
            p.Moving = false;
            p.DecisionTimer = p.DribbleTimer = 0f;
            p.DribbleTarget = b.Position;
            p.RunRemaining = p.RunCooldown = 0f;
            var team = p.Team;
            if (team.Runner == p.Local) team.Runner = -1;
            if (team.Cover == p.Local) team.Cover = -1;
            if (team.SecondDefender == p.Local) team.SecondDefender = -1;
            if (Ball.Owner != p.Global) SetIntention(p, AiIntention.HoldShape); // a taker / carrier keeps the ball
            p.ChaseReactionTimer = -1f;
        }

        // ---------------- AI ----------------

        /// <summary>Called every step: while a dead ball is being set up, an AI side whose window (displayed clock) has come
        /// substitutes its tired players. Cheap and allocation-free when nothing is due.</summary>
        private void SubstitutionTick()
        {
            if (Restart == RestartKind.None || RestartReady || Finished) return;
            int minute = EventMinute; // the minute being played, numbered like QuickSim's
            AiWindow(Home, minute);
            AiWindow(Away, minute);
        }

        private void AiWindow(AiTeam team, int minute)
        {
            if (team == HumanTeam) return; // only the user substitutes his side
            int s = (int)team.Side;
            var windows = SubstitutionRules.Windows;
            bool due = false;
            for (int i = 0; i < windows.Count && i < 31; i++)
            {
                if ((_windowsDone[s] & (1 << i)) != 0 || windows[i] > minute) continue;
                _windowsDone[s] |= 1 << i;
                due = true; // windows passed together (no dead ball in between) are one stoppage
            }
            if (due) AiSubstitutions(team, halftime: false);
        }

        /// <summary>At half-time (after the kick-off is set) both AI sides may substitute without using a stoppage.</summary>
        private void HalfTimeSubstitutions()
        {
            if (HumanTeam != Home) AiSubstitutions(Home, halftime: true);
            if (HumanTeam != Away) AiSubstitutions(Away, halftime: true);
        }

        /// <summary>QuickSim's window, on the shared rules: the most tired player below the threshold is replaced by the
        /// best substitute for his slot, while the substitute is not much worse and the limits allow.</summary>
        private void AiSubstitutions(AiTeam team, bool halftime)
        {
            int s = (int)team.Side;
            var rules = SubstitutionRules;
            if (!SubRules.HasStoppageLeft(_stoppagesUsed[s], halftime, rules)) return;
            while (SubRules.AiMayReplaceTired(_subsMade[s], _matchSetup.MaxSubstitutions, _tiredSubs[s], rules))
            {
                AiPlayer tired = null;
                for (int i = 0; i < team.Players.Length; i++)
                {
                    var p = team.Players[i];
                    if (!SubRules.CanLeave(!p.Body.OffPitch, _sentOffMinute[p.Global] > 0) || !SubRules.IsTired(p.Body.Energy, rules)) continue;
                    if (tired == null || SubRules.MoreTired(p.Body.Energy, p.Setup.PlayerId, tired.Body.Energy, tired.Setup.PlayerId)) tired = p;
                }
                if (tired == null) break;
                var on = SubRules.BestReplacement(_rules, _available[s], tired.Slot.Position);
                if (on == null || !SubRules.WorthReplacing(_rules, on, tired.Setup, tired.Slot.Position, tired.Body.Energy)) break;
                Apply(team, tired.Local, BenchIndexOf(team.Side, on));
                _tiredSubs[s]++;
            }
        }

        private int BenchIndexOf(MatchSide side, MatchPlayerSetup player)
        {
            var bench = _matchSetup.Team(side).Bench;
            for (int i = 0; i < bench.Count; i++)
                if (bench[i] == player) return i;
            throw new InvalidOperationException("Substitute not on the bench.");
        }

        // ---------------- result ----------------

        /// <summary>Everybody who played: the players now in the slots, then those who left (in order).</summary>
        private PlayerMatchStats[] PlayerResults()
        {
            var players = new PlayerMatchStats[Players.Length + _departedCount];
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                int off = _sentOffMinute[i] > 0 ? _sentOffMinute[i] : TotalDisplayMinutes;
                players[i] = new PlayerMatchStats(p.Setup.PlayerId, p.Team.Side, _slotBench[i] < 0, Math.Max(0, off - _slotMinuteOn[i]),
                    _playerGoals[i], _playerAssists[i], _playerShots[i], _playerShotsOnTarget[i], _playerFouls[i], _playerYellows[i],
                    p.Body.OffPitch, null, p.Body.Energy, InjurySeverity.None);
            }
            for (int k = 0; k < _departedCount; k++)
            {
                var d = _departed[k];
                var side = Players[d.Global].Team.Side;
                players[Players.Length + k] = new PlayerMatchStats(DepartedSetup(d).PlayerId, side, d.BenchIndex < 0,
                    Math.Max(0, d.MinuteOff - d.MinuteOn), d.Goals, d.Assists, d.Shots, d.ShotsOnTarget, d.Fouls, d.Yellows, false, null,
                    d.Energy, InjurySeverity.None);
            }
            return players;
        }

        private MatchPlayerSetup DepartedSetup(in Departed d) => SlotSetup(d.Global, d.BenchIndex);

        /// <summary>The starter of slot <paramref name="global"/> (bench index -1) or that side's substitute.</summary>
        private MatchPlayerSetup SlotSetup(int global, int benchIndex)
        {
            bool home = global < MatchTeamSetup.StarterCount;
            var team = home ? _matchSetup.Home : _matchSetup.Away;
            return benchIndex < 0 ? team.Starters[home ? global : global - MatchTeamSetup.StarterCount] : team.Bench[benchIndex];
        }

        // ---------------- snapshot (v6 block, after the events) ----------------

        private void WriteSubstitutions(BinaryWriter w)
        {
            for (int s = 0; s < 2; s++)
            {
                w.Write(_subsMade[s]);
                w.Write(_stoppagesUsed[s]);
                w.Write(_stoppageStep[s]);
                w.Write(_tiredSubs[s]);
                w.Write(_windowsDone[s]);
                w.Write(_benchUsed[s].Length);
                for (int i = 0; i < _benchUsed[s].Length; i++) w.Write(_benchUsed[s][i]);
            }
            for (int g = 0; g < _slotBench.Length; g++)
            {
                w.Write(_slotBench[g]);
                w.Write(_slotMinuteOn[g]);
            }
            w.Write(_departedCount);
            for (int k = 0; k < _departedCount; k++)
            {
                var d = _departed[k];
                w.Write(d.Global);
                w.Write(d.BenchIndex);
                w.Write(d.MinuteOn);
                w.Write(d.MinuteOff);
                w.Write(d.Goals);
                w.Write(d.Assists);
                w.Write(d.Shots);
                w.Write(d.ShotsOnTarget);
                w.Write(d.Fouls);
                w.Write(d.Yellows);
                w.Write(d.Energy);
            }
        }

        private void ReadSubstitutions(BinaryReader r)
        {
            for (int s = 0; s < 2; s++)
            {
                _subsMade[s] = r.ReadInt32();
                _stoppagesUsed[s] = r.ReadInt32();
                _stoppageStep[s] = r.ReadInt64();
                _tiredSubs[s] = r.ReadInt32();
                _windowsDone[s] = r.ReadInt32();
                if (r.ReadInt32() != _benchUsed[s].Length) throw new ArgumentException("Corrupt match snapshot (bench).");
                var bench = _matchSetup.Team((MatchSide)s).Bench;
                _available[s].Clear();
                for (int i = 0; i < _benchUsed[s].Length; i++)
                {
                    _benchUsed[s][i] = r.ReadBoolean();
                    if (!_benchUsed[s][i]) _available[s].Add(bench[i]);
                }
            }
            for (int g = 0; g < _slotBench.Length; g++)
            {
                int bench = r.ReadInt32();
                _slotMinuteOn[g] = r.ReadInt32();
                var p = Players[g];
                if (bench < -1 || bench >= _benchUsed[(int)p.Team.Side].Length) throw new ArgumentException("Corrupt match snapshot (slot).");
                _slotBench[g] = bench;
                EnterSlot(p, SlotSetup(g, bench));
            }
            _departedCount = r.ReadInt32();
            if (_departedCount < 0 || _departedCount > _departed.Length) throw new ArgumentException("Corrupt match snapshot (substitutions).");
            for (int k = 0; k < _departedCount; k++)
            {
                ref var d = ref _departed[k];
                d.Global = r.ReadInt32();
                d.BenchIndex = r.ReadInt32();
                d.MinuteOn = r.ReadInt32();
                d.MinuteOff = r.ReadInt32();
                d.Goals = r.ReadInt32();
                d.Assists = r.ReadInt32();
                d.Shots = r.ReadInt32();
                d.ShotsOnTarget = r.ReadInt32();
                d.Fouls = r.ReadInt32();
                d.Yellows = r.ReadInt32();
                d.Energy = r.ReadSingle();
                if (d.Global < 0 || d.Global >= Players.Length) throw new ArgumentException("Corrupt match snapshot (substitutions).");
            }
        }

        /// <summary>Id of the player who started in slot <paramref name="global"/> (the snapshot's fingerprint).</summary>
        private int StarterId(int global) => SlotSetup(global, -1).PlayerId.Value;
    }
}
