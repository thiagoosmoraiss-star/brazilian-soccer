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
        /// <summary>Current formation (it may change during the match, A9c).</summary>
        public FormationDefinition Formation { get; internal set; }
        public readonly TacticsDefinition Tactics;
        /// <summary>The side's tactic in force: the block, pressers and risk it sets (A9c).</summary>
        public readonly TacticsRuntime Runtime;
        public readonly AiPlayer[] Players = new AiPlayer[MatchTeamSetup.StarterCount];
        /// <summary>Same body instances as <see cref="Players"/> (PassSystem/ShotSystem work on team-local arrays).</summary>
        public readonly PlayerBody[] Bodies = new PlayerBody[MatchTeamSetup.StarterCount];
        internal readonly Rng Rng;
        /// <summary>The difficulty acting on this side (F1c): the user's opponent's level, else neutral (all factors 1).</summary>
        public DifficultyLevelDefinition Difficulty { get; internal set; } = DifficultyLevelDefinition.Neutral;

        public TeamPhase Phase = TeamPhase.Build;
        public float TransitionTimer;
        public int Goals;

        // Team-layer assignments (refreshed at the team rate, read by the individual layer).
        internal readonly bool[] Supporter = new bool[MatchTeamSetup.StarterCount];
        internal readonly bool[] Presser = new bool[MatchTeamSetup.StarterCount];
        /// <summary>Goes for a high ball's drop zone besides the chaser/receiver (A9a).</summary>
        internal readonly bool[] DropZone = new bool[MatchTeamSetup.StarterCount];
        /// <summary>Goes into the box while the ball is in the final third (F1b "atacar a área").</summary>
        internal readonly bool[] BoxAttacker = new bool[MatchTeamSetup.StarterCount];
        public bool AttacksTheBox(int local) => BoxAttacker[local];
        internal int Chaser = -1;
        /// <summary>"Pressionar" (A9b): team-local index of the teammate the user sent at the carrier, or -1.</summary>
        public int SecondDefender { get; internal set; } = -1;
        /// <summary>Team-local index of the defender covering behind the presser (A9b), or -1.</summary>
        public int Cover { get; internal set; } = -1;
        /// <summary>Team-local index of the attacker running in behind (A9b), or -1.</summary>
        public int Runner { get; internal set; } = -1;
        /// <summary>Seconds before this side starts another run in behind.</summary>
        internal float RunCooldown;
        /// <summary>Team-local index of the teammate an own pass in flight is meant for (he goes to meet it at once), or -1.</summary>
        public int Receiver { get; internal set; } = -1;
        /// <summary>Global index of the player who made that pass.</summary>
        internal int ReceiverPasser = -1;
        /// <summary>This side has the ball or touched it last (a pass in flight still counts).</summary>
        public bool HasPossession { get; internal set; }
        internal float OffsideU;

        /// <summary>Normalized x of the deepest and highest outfield slot (the formation's own vertical extent).</summary>
        internal float SlotMinX;
        internal float SlotMaxX;

        public AiTeam(MatchSide side, TeamFrame frame, FormationDefinition formation, TacticsDefinition tactics, MatchTeamSetup setup,
            int globalOffset, Rng rng, FatigueRules fatigueRules = null)
        {
            Side = side;
            Frame = frame;
            Formation = formation ?? throw new ArgumentNullException(nameof(formation));
            Tactics = tactics;
            Runtime = new TacticsRuntime(tactics, fatigueRules, setup.Tactic);
            Rng = rng;
            if (formation.Slots.Count != MatchTeamSetup.StarterCount) throw new ArgumentException("Formation must have 11 slots.");

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i] = new AiPlayer(globalOffset + i, i, this, formation.Slots[i], setup.Starters[i]);
                Players[i].Body.Energy = setup.Starters[i].Energy;
                Bodies[i] = Players[i].Body;
            }
            UpdateSlotExtent();
        }

        /// <summary>Normalized x of the deepest and highest outfield slot of the current formation.</summary>
        internal void UpdateSlotExtent()
        {
            SlotMinX = float.MaxValue;
            SlotMaxX = float.MinValue;
            for (int i = 0; i < Players.Length; i++)
            {
                var slot = Players[i].Slot;
                if (slot.Role == FormationRole.GK) continue;
                SlotMinX = Math.Min(SlotMinX, slot.X);
                SlotMaxX = Math.Max(SlotMaxX, slot.X);
            }
        }

        public bool InPossession => TeamPhaseInfo.InPossession(Phase);

        public bool Owns(int globalIndex) => globalIndex >= Players[0].Global && globalIndex < Players[0].Global + Players.Length;
    }
}
