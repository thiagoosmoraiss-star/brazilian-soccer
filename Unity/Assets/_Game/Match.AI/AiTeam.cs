using System;
using Game.Core.Contracts.Match;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Match.AI
{
    /// <summary>One side of an <see cref="AiMatch"/>: its players, formation, frame and team-layer state.</summary>
    public sealed class AiTeam
    {
        public readonly MatchSide Side;
        public readonly TeamFrame Frame;
        public readonly FormationDefinition Formation;
        public readonly TacticsDefinition Tactics;
        public readonly AiPlayer[] Players = new AiPlayer[MatchTeamSetup.StarterCount];
        /// <summary>Same body instances as <see cref="Players"/> (PassSystem/ShotSystem work on team-local arrays).</summary>
        public readonly PlayerBody[] Bodies = new PlayerBody[MatchTeamSetup.StarterCount];
        internal readonly Rng Rng;

        public TeamPhase Phase = TeamPhase.Build;
        public float TransitionTimer;
        public int Goals;

        // Team-layer assignments (refreshed at the team rate, read by the individual layer).
        internal readonly bool[] Supporter = new bool[MatchTeamSetup.StarterCount];
        internal readonly bool[] Presser = new bool[MatchTeamSetup.StarterCount];
        internal int Chaser = -1;
        /// <summary>Team-local index of the teammate an own pass in flight is meant for (he goes to meet it at once), or -1.</summary>
        public int Receiver { get; internal set; } = -1;
        /// <summary>Global index of the player who made that pass.</summary>
        internal int ReceiverPasser = -1;
        /// <summary>This side has the ball or touched it last (a pass in flight still counts).</summary>
        public bool HasPossession { get; internal set; }
        internal float OffsideU;

        /// <summary>Normalized x of the deepest and highest outfield slot (the formation's own vertical extent).</summary>
        internal readonly float SlotMinX;
        internal readonly float SlotMaxX;

        public AiTeam(MatchSide side, TeamFrame frame, FormationDefinition formation, TacticsDefinition tactics, MatchTeamSetup setup,
            int globalOffset, Rng rng)
        {
            Side = side;
            Frame = frame;
            Formation = formation ?? throw new ArgumentNullException(nameof(formation));
            Tactics = tactics;
            Rng = rng;
            if (formation.Slots.Count != MatchTeamSetup.StarterCount) throw new ArgumentException("Formation must have 11 slots.");

            SlotMinX = float.MaxValue;
            SlotMaxX = float.MinValue;
            for (int i = 0; i < Players.Length; i++)
            {
                var slot = formation.Slots[i];
                Players[i] = new AiPlayer(globalOffset + i, i, this, slot, setup.Starters[i]);
                Players[i].Body.Energy = setup.Starters[i].Energy;
                Bodies[i] = Players[i].Body;
                if (slot.Role == FormationRole.GK) continue;
                SlotMinX = Math.Min(SlotMinX, slot.X);
                SlotMaxX = Math.Max(SlotMaxX, slot.X);
            }
        }

        public bool InPossession => TeamPhaseInfo.InPossession(Phase);

        public bool Owns(int globalIndex) => globalIndex >= Players[0].Global && globalIndex < Players[0].Global + Players.Length;
    }
}
