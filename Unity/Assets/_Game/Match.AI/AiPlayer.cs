using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Match;

namespace Game.Match.AI
{
    /// <summary>What a player is trying to do (individual layer, TECHNICAL_SPEC §7).</summary>
    public enum AiIntention
    {
        HoldShape = 0,
        Support = 1,
        Mark = 2,
        Press = 3,
        ChaseBall = 4,
        OnBall = 5,
    }

    /// <summary>Per-player AI state. Plain mutable holder; the layers in <see cref="AiMatch"/> update it.</summary>
    public sealed class AiPlayer
    {
        /// <summary>Index among all 22 players (home 0-10, away 11-21); what <see cref="Ball.Owner"/> holds.</summary>
        public readonly int Global;
        /// <summary>Index within the team (formation slot order).</summary>
        public readonly int Local;
        public readonly AiTeam Team;
        public readonly FormationSlot Slot;
        public readonly MatchPlayerSetup Setup;
        public readonly PlayerBody Body = new PlayerBody();

        /// <summary>Role-layer target without any positioning error (what a perfect positioner would aim at).</summary>
        public Vector3 IdealTarget;
        /// <summary>Role-layer target the player actually follows (error + correction delay applied).</summary>
        public Vector3 RoleTarget;
        public Vector3 PendingTarget;
        public float CorrectionTimer;
        public Vector2 ErrorOffset;
        public float ErrorTimer;

        public AiIntention Intention;
        public float IntentionTime;
        public float ChaseReactionTimer = -1f;
        /// <summary>Where the current intention wants the player to be.</summary>
        public Vector3 IntentTarget;
        public float SupportTimer;
        public bool Moving;

        public float DecisionTimer;
        public float DribbleTimer;
        public Vector3 DribbleTarget;

        public AiPlayer(int global, int local, AiTeam team, FormationSlot slot, MatchPlayerSetup setup)
        {
            Global = global;
            Local = local;
            Team = team;
            Slot = slot;
            Setup = setup;
        }

        public bool IsGoalkeeper => Slot.Role == Game.Data.Definitions.FormationRole.GK;
    }
}
