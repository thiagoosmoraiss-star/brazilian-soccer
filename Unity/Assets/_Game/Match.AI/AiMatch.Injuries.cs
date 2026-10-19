using System;
using System.IO;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using SubRules = Game.Rules.Match.Substitutions;

namespace Game.Match.AI
{
    /// <summary>
    /// Injuries in the playable match (X-91, D-21 option a; GAME_DESIGN §29 "lesão em jogo com substituição forçada";
    /// TECHNICAL_SPEC §10 "falta/cartão/lesão: mesmas probabilidades base em Rules"): at every minute of the displayed
    /// clock each player on the pitch is injured with QuickSim's chance per player-minute
    /// (<see cref="Game.Rules.Match.MatchRules.InjuryHazardPerMinute"/>, his energy included) and QuickSim's severity
    /// (<see cref="Game.Rules.Match.MatchRules.InjurySeverityRoll"/>), on its own RNG stream (<see cref="InjuriesStream"/>).
    /// The injured player goes down (a ball at his feet runs loose) and is replaced at once by the best substitute for his
    /// slot (QuickSim's forced substitution: only the substitution limit counts, not the stoppages). The user's side waits
    /// for the user instead (<see cref="PendingInjury"/>; <see cref="MatchSession"/> pauses): a step taken before he chooses
    /// makes the AI's choice. With no substitution left the team plays one short, except the goalkeeper, who plays on
    /// (this engine always has a keeper in goal). Zero allocation per step.
    /// </summary>
    public sealed partial class AiMatch
    {
        public const string InjuriesStream = "Injuries";

        private Rng _injuryRng;
        /// <summary>Displayed minutes already rolled (0 to <see cref="TotalDisplayMinutes"/>).</summary>
        private int _injuryMinutes;
        /// <summary>Injury of the slot's current player (None while fit).</summary>
        private readonly InjurySeverity[] _slotInjury = new InjurySeverity[2 * MatchTeamSetup.StarterCount];
        /// <summary>Display minute an injured player left without a replacement (0 while he is on).</summary>
        private readonly int[] _injuredOffMinute = new int[2 * MatchTeamSetup.StarterCount];
        /// <summary>Per side: injuries so far.</summary>
        private readonly int[] _injuryCount = new int[2];
        /// <summary>Per side: slot (team-local) of the injured player waiting for the user's substitution, or -1.</summary>
        private readonly int[] _pendingInjury = { -1, -1 };

        /// <summary>True when the slot's current player is injured (being replaced, playing on or off the pitch).</summary>
        public bool Injured(AiPlayer p) => _slotInjury[p.Global] != InjurySeverity.None;
        public InjurySeverity InjuryOf(AiPlayer p) => _slotInjury[p.Global];
        /// <summary>Injuries of <paramref name="side"/> so far (the session pauses when the user's count grows).</summary>
        public int Injuries(MatchSide side) => _injuryCount[(int)side];
        /// <summary>Team slot of the user's injured player waiting to be replaced (<see cref="Substitute"/> with that slot), or -1.</summary>
        public int PendingInjury(MatchSide side) => _pendingInjury[(int)side];

        private void InitInjuries(RngStreams streams) => _injuryRng = streams.Get(InjuriesStream);

        /// <summary>Injures <paramref name="local"/> of <paramref name="side"/> now (scenario tests, dev tools). False when he
        /// is already injured or off the pitch.</summary>
        public bool Injure(MatchSide side, int local, InjurySeverity severity)
        {
            var team = side == MatchSide.Home ? Home : Away;
            if (Finished || severity == InjurySeverity.None || local < 0 || local >= team.Players.Length) return false;
            var p = team.Players[local];
            if (p.Body.OffPitch || Injured(p)) return false;
            Injure(p, severity);
            return true;
        }

        /// <summary>After the clock moved: one roll per player on the pitch for each displayed minute just completed
        /// (QuickSim rolls once per minute, 90 per match).</summary>
        private void InjuryTick()
        {
            int minute = Math.Min(TotalDisplayMinutes, (int)ClockMinutes);
            while (_injuryMinutes < minute)
            {
                _injuryMinutes++;
                RollInjuries(Home);
                RollInjuries(Away);
            }
        }

        private void RollInjuries(AiTeam team)
        {
            for (int i = 0; i < team.Players.Length; i++)
            {
                var p = team.Players[i];
                if (p.Body.OffPitch || Injured(p)) continue;
                if (_injuryRng.NextDouble() >= _rules.InjuryHazardPerMinute(p.Setup.Attributes, p.Body.Energy)) continue;
                Injure(p, _rules.InjurySeverityRoll(_injuryRng));
            }
        }

        private void Injure(AiPlayer p, InjurySeverity severity)
        {
            var team = p.Team;
            int s = (int)team.Side;
            _slotInjury[p.Global] = severity;
            _injuryCount[s]++;
            RecordEvent(MatchEventType.Injury, team.Side, p.Setup.PlayerId, Id.None);
            // He goes down: a ball at his feet runs loose (a restart he was taking is handed over below if he leaves).
            if (Restart == RestartKind.None && Ball.State == BallState.Controlled && Ball.Owner == p.Global)
            {
                Ball.Owner = Ball.NoPlayer;
                Ball.State = BallState.Rolling;
            }
            if (Finished) return; // the final whistle: nobody comes on
            if (team == HumanTeam && ForcedReplacementAvailable(s))
            {
                _pendingInjury[s] = p.Local; // the user chooses (MatchSession pauses)
                return;
            }
            ForcedSubstitution(p);
        }

        private bool ForcedReplacementAvailable(int s) =>
            SubRules.HasSubstitutionLeft(_subsMade[s], _matchSetup.MaxSubstitutions) && _available[s].Count > 0;

        /// <summary>QuickSim's forced substitution: the best substitute for the slot, whatever his level; otherwise the
        /// injured player leaves (the goalkeeper plays on).</summary>
        private void ForcedSubstitution(AiPlayer p)
        {
            int s = (int)p.Team.Side;
            if (ForcedReplacementAvailable(s))
            {
                var on = SubRules.BestReplacement(_rules, _available[s], p.Slot.Position);
                if (on != null)
                {
                    Apply(p.Team, p.Local, BenchIndexOf(p.Team.Side, on), forced: true);
                    return;
                }
            }
            if (!p.IsGoalkeeper) LeaveInjured(p);
        }

        /// <summary>A step taken while the user's injured player still waits: the AI's choice for him.</summary>
        private void ResolvePendingInjuries()
        {
            for (int s = 0; s < 2; s++)
            {
                int local = _pendingInjury[s];
                if (local < 0) continue;
                _pendingInjury[s] = -1;
                ForcedSubstitution((s == 0 ? Home : Away).Players[local]);
            }
        }

        /// <summary>No replacement: off the pitch like a dismissed player; a restart he was taking goes to the nearest
        /// teammate, who sets it up again.</summary>
        private void LeaveInjured(AiPlayer p)
        {
            _injuredOffMinute[p.Global] = EventMinute;
            Park(p);
            if (Ball.Owner == p.Global && Restart == RestartKind.None) { Ball.Owner = Ball.NoPlayer; Ball.State = BallState.Rolling; }
            if (HumanTeam == p.Team)
            {
                _eligible[p.Local] = false;
                if (Control.Controlled == p.Local) Control.OnCaptured(NearestOutfield(HumanTeam, Ball.Position).Local);
            }
            if (Restart != RestartKind.None && Taker == p)
            {
                Taker = NearestOutfield(p.Team, RestartSpot);
                if (RestartReady)
                {
                    RestartReady = false;
                    _restartTimer = 0f;
                    Ball.Position = RestartSpot;
                    Ball.Velocity = System.Numerics.Vector3.Zero;
                    Ball.State = BallState.Dead;
                    Ball.Owner = Ball.NoPlayer;
                }
                Ball.LastTouch = Taker.Global;
            }
        }

        // ---------------- snapshot (v8 block, after the substitutions) ----------------

        private void WriteInjuries(BinaryWriter w)
        {
            Write(w, _injuryRng.GetState());
            w.Write(_injuryMinutes);
            for (int g = 0; g < _slotInjury.Length; g++)
            {
                w.Write((int)_slotInjury[g]);
                w.Write(_injuredOffMinute[g]);
            }
            for (int s = 0; s < 2; s++)
            {
                w.Write(_injuryCount[s]);
                w.Write(_pendingInjury[s]);
            }
            for (int k = 0; k < _departedCount; k++) w.Write((int)_departed[k].Injury);
        }

        private void ReadInjuries(BinaryReader r)
        {
            _injuryRng.SetState(ReadRng(r));
            _injuryMinutes = r.ReadInt32();
            for (int g = 0; g < _slotInjury.Length; g++)
            {
                _slotInjury[g] = ReadSeverity(r);
                _injuredOffMinute[g] = r.ReadInt32();
            }
            for (int s = 0; s < 2; s++)
            {
                _injuryCount[s] = r.ReadInt32();
                _pendingInjury[s] = r.ReadInt32();
                if (_pendingInjury[s] < -1 || _pendingInjury[s] >= MatchTeamSetup.StarterCount) throw new ArgumentException("Corrupt match snapshot (injuries).");
            }
            for (int k = 0; k < _departedCount; k++) _departed[k].Injury = ReadSeverity(r);
        }

        private static InjurySeverity ReadSeverity(BinaryReader r)
        {
            int v = r.ReadInt32();
            if (v < (int)InjurySeverity.None || v > (int)InjurySeverity.Severe) throw new ArgumentException("Corrupt match snapshot (injuries).");
            return (InjurySeverity)v;
        }
    }
}
