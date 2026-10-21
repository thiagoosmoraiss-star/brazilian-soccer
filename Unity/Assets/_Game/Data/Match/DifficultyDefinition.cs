using System.Collections.Generic;

namespace Game.Data.Match
{
    /// <summary>Match difficulty (Data/Balance/difficulty.json, F1c, D-23): Fácil, Normal and Difícil. A level only changes
    /// the decisions and reactions of the AI the user plays against, never attributes (GAME_DESIGN §24), and, in the QuickSim,
    /// only the calculation of the user's own match (D-23b). The reference level is the current AI exactly (D-23a): every
    /// factor 1.</summary>
    public sealed class DifficultyDefinition
    {
        /// <summary>The level that reproduces the AI without difficulty (all factors 1).</summary>
        public string ReferenceLevel { get; internal set; }
        /// <summary>The level of a new career.</summary>
        public string DefaultLevel { get; internal set; }
        /// <summary>Easiest first.</summary>
        public IReadOnlyList<DifficultyLevelDefinition> Levels { get; internal set; }

        /// <summary>Level by id, or null.</summary>
        public DifficultyLevelDefinition Level(string id)
        {
            foreach (var l in Levels) if (l.Id == id) return l;
            return null;
        }
    }

    /// <summary>One level's factors on the opposing AI. Each multiplies an existing value, so 1 changes nothing.</summary>
    public sealed class DifficultyLevelDefinition
    {
        /// <summary>No difficulty: what every match without one (calibration, AI-vs-AI) uses.</summary>
        public static readonly DifficultyLevelDefinition Neutral = new DifficultyLevelDefinition
        {
            Id = "", DecisionNoiseScale = 1f, DecisionIntervalScale = 1f, ReactionScale = 1f, PositionErrorScale = 1f,
            KeeperReactionScale = 1f, QuickSimStrengthScale = 1f,
        };

        public string Id { get; internal set; }
        /// <summary>× ai.json onBall.decisionNoise: how far the carrier's pick strays from the best option.</summary>
        public float DecisionNoiseScale { get; internal set; }
        /// <summary>× ai.json onBall.decisionIntervalSeconds: how often the carrier decides.</summary>
        public float DecisionIntervalScale { get; internal set; }
        /// <summary>× LooseBallReaction and AiCorrectionDelay: how late the players react to a loose ball and to a big move.</summary>
        public float ReactionScale { get; internal set; }
        /// <summary>× AiTargetError: how far from the ideal spot the players stand.</summary>
        public float PositionErrorScale { get; internal set; }
        /// <summary>× the goalkeeper's reaction to a shot (GkReactionTime plus the not-set penalty).</summary>
        public float KeeperReactionScale { get; internal set; }
        /// <summary>QuickSim: × the opposing club's sector strengths in the user's match only.</summary>
        public float QuickSimStrengthScale { get; internal set; }

        public bool IsNeutral => DecisionNoiseScale == 1f && DecisionIntervalScale == 1f && ReactionScale == 1f && PositionErrorScale == 1f
                                 && KeeperReactionScale == 1f && QuickSimStrengthScale == 1f;
    }
}
