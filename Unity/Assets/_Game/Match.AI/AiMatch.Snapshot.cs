using System;
using System.IO;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Loading;
using Game.Match;

namespace Game.Match.AI
{
    public sealed partial class AiMatch
    {
        private const int SnapshotMagic = 0x534D4341; // "ACMS"
        /// <summary>Bump when the snapshot layout changes; older snapshots are then rejected (a match in progress is
        /// short-lived, it is not migrated like a career save).</summary>
        public const int SnapshotVersion = 1;

        /// <summary>
        /// The whole match state, binary (TECHNICAL_SPEC §5 "Snapshot e pausa": "MatchState inteiro serializável: ao ir
        /// para segundo plano, salva; ao voltar, restaura pausado"). Restoring it with the same data and MatchSetup and
        /// stepping on with the same inputs gives exactly the same match. Allocates: call it on pause, not per step.
        /// </summary>
        public byte[] Snapshot()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(SnapshotMagic);
                w.Write(SnapshotVersion);
                WriteFingerprint(w);

                w.Write(_step);
                w.Write(TeamRef(_possession));
                w.Write(_secureTimer);
                w.Write(_buffered.Kind == ActionKind.None ? 0 : (int)_buffered.Kind);
                w.Write(_buffered.Power);
                w.Write(_bufferedAge);
                w.Write(_bufferedWithoutBall);
                w.Write(_ballWasLoose);
                w.Write(LastKickFirstTime);
                w.Write(TeamRef(_firstKickoff));
                Write(w, _passRng.GetState());
                Write(w, _shotRng.GetState());
                Write(w, _keeperRng.GetState());

                w.Write(TeamRef(HumanTeam));
                if (HumanTeam != null)
                {
                    w.Write(Control.Controlled);
                    w.Write(Control.Next);
                    w.Write(Control.ManualLockRemaining);
                    w.Write(Control.AutoCooldownRemaining);
                }

                w.Write((int)Restart);
                w.Write(TeamRef(RestartTeam));
                w.Write(Taker == null ? -1 : Taker.Global);
                w.Write(RestartReady);
                Write(w, RestartSpot);
                Write(w, _takerStand);
                Write(w, _takerFacing);
                w.Write(_restartTimer);

                Write(w, Ball.Position);
                Write(w, Ball.Velocity);
                w.Write(Ball.Spin);
                w.Write((int)Ball.State);
                w.Write(Ball.Owner);
                w.Write(Ball.LastTouch);

                WriteTeam(w, Home);
                WriteTeam(w, Away);
                for (int i = 0; i < Players.Length; i++) WritePlayer(w, Players[i]);
                WriteKeeper(w, HomeKeeper);
                WriteKeeper(w, AwayKeeper);

                WriteStats(w, HomeStats);
                WriteStats(w, AwayStats);
                w.Write(_pendingShot);
                w.Write(_assistCandidate);
                for (int i = 0; i < Players.Length; i++)
                {
                    w.Write(_playerGoals[i]);
                    w.Write(_playerAssists[i]);
                    w.Write(_playerShots[i]);
                    w.Write(_playerShotsOnTarget[i]);
                }
                w.Write(_events.Count);
                for (int i = 0; i < _events.Count; i++)
                {
                    var e = _events[i];
                    w.Write(e.Minute);
                    w.Write((int)e.Type);
                    w.Write((int)e.Side);
                    w.Write(e.PlayerId.Value);
                    w.Write(e.OtherPlayerId.Value);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Rebuilds a match from <see cref="Snapshot"/>: same data, same <paramref name="setup"/>, same step.
        /// Throws <see cref="ArgumentException"/> when the snapshot is from another version, setup or step.</summary>
        public static AiMatch Restore(GameDatabase db, MatchSetup setup, float stepSeconds, byte[] snapshot, IAiMatchObserver observer = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var m = new AiMatch(db, setup, stepSeconds, observer);
            try
            {
                using (var r = new BinaryReader(new MemoryStream(snapshot, false)))
                    m.Read(r);
            }
            catch (EndOfStreamException e)
            {
                throw new ArgumentException("Truncated match snapshot.", nameof(snapshot), e);
            }
            return m;
        }

        private void Read(BinaryReader r)
        {
            if (r.ReadInt32() != SnapshotMagic) throw new ArgumentException("Not a match snapshot.");
            int version = r.ReadInt32();
            if (version != SnapshotVersion) throw new ArgumentException($"Match snapshot version {version}, expected {SnapshotVersion}.");
            CheckFingerprint(r);

            _step = r.ReadInt64();
            _possession = TeamFrom(r.ReadInt32());
            _secureTimer = r.ReadSingle();
            int kind = r.ReadInt32();
            float power = r.ReadSingle();
            _buffered = kind == 0 ? ActionCommand.None : new ActionCommand((ActionKind)kind, power);
            _bufferedAge = r.ReadSingle();
            _bufferedWithoutBall = r.ReadBoolean();
            _ballWasLoose = r.ReadBoolean();
            LastKickFirstTime = r.ReadBoolean();
            _firstKickoff = TeamFrom(r.ReadInt32());
            _passRng.SetState(ReadRng(r));
            _shotRng.SetState(ReadRng(r));
            _keeperRng.SetState(ReadRng(r));

            var human = TeamFrom(r.ReadInt32());
            if (human != null)
            {
                EnableHuman(human.Side);
                int controlled = r.ReadInt32(), next = r.ReadInt32();
                Control.Restore(controlled, next, r.ReadSingle(), r.ReadSingle());
            }
            else
            {
                HumanTeam = null;
                Control = null;
            }

            Restart = (RestartKind)r.ReadInt32();
            RestartTeam = TeamFrom(r.ReadInt32());
            int taker = r.ReadInt32();
            Taker = taker < 0 ? null : Players[taker];
            RestartReady = r.ReadBoolean();
            RestartSpot = ReadV3(r);
            _takerStand = ReadV3(r);
            _takerFacing = ReadV3(r);
            _restartTimer = r.ReadSingle();

            Ball.Position = ReadV3(r);
            Ball.Velocity = ReadV3(r);
            Ball.Spin = r.ReadSingle();
            Ball.State = (BallState)r.ReadInt32();
            Ball.Owner = r.ReadInt32();
            Ball.LastTouch = r.ReadInt32();

            ReadTeam(r, Home);
            ReadTeam(r, Away);
            for (int i = 0; i < Players.Length; i++) ReadPlayer(r, Players[i]);
            ReadKeeper(r, HomeKeeper);
            ReadKeeper(r, AwayKeeper);

            ReadStats(r, HomeStats);
            ReadStats(r, AwayStats);
            _pendingShot = r.ReadInt32();
            _assistCandidate = r.ReadInt32();
            for (int i = 0; i < Players.Length; i++)
            {
                _playerGoals[i] = r.ReadInt32();
                _playerAssists[i] = r.ReadInt32();
                _playerShots[i] = r.ReadInt32();
                _playerShotsOnTarget[i] = r.ReadInt32();
            }
            int events = r.ReadInt32();
            if (events < 0 || events > MaxEvents) throw new ArgumentException("Corrupt match snapshot (events).");
            _events.Clear();
            for (int i = 0; i < events; i++)
                _events.Add(new MatchEvent(r.ReadInt32(), (MatchEventType)r.ReadInt32(), (MatchSide)r.ReadInt32(), new Id(r.ReadInt32()), new Id(r.ReadInt32())));
            if (r.BaseStream.Position != r.BaseStream.Length) throw new ArgumentException("Corrupt match snapshot (trailing data).");
        }

        // ---- identity: a snapshot only restores onto the same match ----

        private void WriteFingerprint(BinaryWriter w)
        {
            w.Write(_seed);
            w.Write(StepSeconds);
            w.Write(_totalSteps);
            for (int i = 0; i < Players.Length; i++) w.Write(Players[i].Setup.PlayerId.Value);
        }

        private void CheckFingerprint(BinaryReader r)
        {
            bool same = r.ReadUInt64() == _seed;
            same &= r.ReadSingle() == StepSeconds;
            same &= r.ReadInt64() == _totalSteps;
            for (int i = 0; i < Players.Length; i++) same &= r.ReadInt32() == Players[i].Setup.PlayerId.Value;
            if (!same) throw new ArgumentException("The snapshot belongs to another match (seed, step, duration or players differ).");
        }

        // ---- pieces ----

        private int TeamRef(AiTeam t) => t == null ? -1 : t == Home ? 0 : 1;
        private AiTeam TeamFrom(int v) => v < 0 ? null : v == 0 ? Home : Away;

        private static void WriteTeam(BinaryWriter w, AiTeam t)
        {
            w.Write((int)t.Phase);
            w.Write(t.TransitionTimer);
            w.Write(t.Goals);
            for (int i = 0; i < t.Supporter.Length; i++) w.Write(t.Supporter[i]);
            for (int i = 0; i < t.Presser.Length; i++) w.Write(t.Presser[i]);
            w.Write(t.Chaser);
            w.Write(t.HasPossession);
            w.Write(t.OffsideU);
            w.Write(t.Receiver);
            w.Write(t.ReceiverPasser);
            Write(w, t.Rng.GetState());
        }

        private static void ReadTeam(BinaryReader r, AiTeam t)
        {
            t.Phase = (TeamPhase)r.ReadInt32();
            t.TransitionTimer = r.ReadSingle();
            t.Goals = r.ReadInt32();
            for (int i = 0; i < t.Supporter.Length; i++) t.Supporter[i] = r.ReadBoolean();
            for (int i = 0; i < t.Presser.Length; i++) t.Presser[i] = r.ReadBoolean();
            t.Chaser = r.ReadInt32();
            t.HasPossession = r.ReadBoolean();
            t.OffsideU = r.ReadSingle();
            t.Receiver = r.ReadInt32();
            t.ReceiverPasser = r.ReadInt32();
            t.Rng.SetState(ReadRng(r));
        }

        private static void WritePlayer(BinaryWriter w, AiPlayer p)
        {
            var b = p.Body;
            Write(w, b.Position);
            Write(w, b.Velocity);
            Write(w, b.Facing);
            w.Write(b.Energy);
            w.Write(b.Sprinting);
            w.Write(b.TurnStunRemaining);
            w.Write(b.IgnoreBallUntilClear);
            w.Write(b.TackleCooldown);

            Write(w, p.IdealTarget);
            Write(w, p.RoleTarget);
            Write(w, p.PendingTarget);
            w.Write(p.CorrectionTimer);
            w.Write(p.ErrorOffset.X);
            w.Write(p.ErrorOffset.Y);
            w.Write(p.ErrorTimer);
            w.Write((int)p.Intention);
            w.Write(p.IntentionTime);
            w.Write(p.ChaseReactionTimer);
            Write(w, p.IntentTarget);
            w.Write(p.SupportTimer);
            w.Write(p.Moving);
            w.Write(p.DecisionTimer);
            w.Write(p.DribbleTimer);
            Write(w, p.DribbleTarget);
        }

        private static void ReadPlayer(BinaryReader r, AiPlayer p)
        {
            var b = p.Body;
            b.Position = ReadV3(r);
            b.Velocity = ReadV3(r);
            b.Facing = ReadV3(r);
            b.Energy = r.ReadSingle();
            b.Sprinting = r.ReadBoolean();
            b.TurnStunRemaining = r.ReadSingle();
            b.IgnoreBallUntilClear = r.ReadBoolean();
            b.TackleCooldown = r.ReadSingle();

            p.IdealTarget = ReadV3(r);
            p.RoleTarget = ReadV3(r);
            p.PendingTarget = ReadV3(r);
            p.CorrectionTimer = r.ReadSingle();
            p.ErrorOffset = new Vector2(r.ReadSingle(), r.ReadSingle());
            p.ErrorTimer = r.ReadSingle();
            p.Intention = (AiIntention)r.ReadInt32();
            p.IntentionTime = r.ReadSingle();
            p.ChaseReactionTimer = r.ReadSingle();
            p.IntentTarget = ReadV3(r);
            p.SupportTimer = r.ReadSingle();
            p.Moving = r.ReadBoolean();
            p.DecisionTimer = r.ReadSingle();
            p.DribbleTimer = r.ReadSingle();
            p.DribbleTarget = ReadV3(r);
        }

        private static void WriteKeeper(BinaryWriter w, Keeper k)
        {
            w.Write((int)k.State);
            w.Write(k.Timer);
            Write(w, k.Target);
            w.Write(k.Dove);
            w.Write(k.AngleErrorDegrees);
            w.Write(k.ErrorTimer);
            w.Write(k.LastReactionSeconds);
            w.Write(k.Saves);
            w.Write(k.Catches);
            w.Write(k.Parries);
        }

        private static void ReadKeeper(BinaryReader r, Keeper k)
        {
            k.State = (KeeperState)r.ReadInt32();
            k.Timer = r.ReadSingle();
            k.Target = ReadV3(r);
            k.Dove = r.ReadBoolean();
            k.AngleErrorDegrees = r.ReadSingle();
            k.ErrorTimer = r.ReadSingle();
            k.LastReactionSeconds = r.ReadSingle();
            k.Saves = r.ReadInt32();
            k.Catches = r.ReadInt32();
            k.Parries = r.ReadInt32();
        }

        private static void WriteStats(BinaryWriter w, AiTeamStats s)
        {
            w.Write(s.Shots);
            w.Write(s.ShotsOnTarget);
            w.Write(s.Passes);
            w.Write(s.Tackles);
            w.Write(s.Saves);
            w.Write(s.Corners);
            w.Write(s.ThrowIns);
            w.Write(s.GoalKicks);
            w.Write(s.PossessionSteps);
        }

        private static void ReadStats(BinaryReader r, AiTeamStats s)
        {
            s.Shots = r.ReadInt32();
            s.ShotsOnTarget = r.ReadInt32();
            s.Passes = r.ReadInt32();
            s.Tackles = r.ReadInt32();
            s.Saves = r.ReadInt32();
            s.Corners = r.ReadInt32();
            s.ThrowIns = r.ReadInt32();
            s.GoalKicks = r.ReadInt32();
            s.PossessionSteps = r.ReadInt64();
        }

        private static void Write(BinaryWriter w, Vector3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }

        private static Vector3 ReadV3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        private static void Write(BinaryWriter w, RngState s)
        {
            w.Write(s.S0);
            w.Write(s.S1);
            w.Write(s.S2);
            w.Write(s.S3);
        }

        private static RngState ReadRng(BinaryReader r) => new RngState(r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64());
    }
}
