using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// Team layer (TECHNICAL_SPEC §7 "Camada do time", 5 Hz): picks the phase. Winning the ball opens an offensive
    /// transition (GAME_DESIGN §24: 2-4 s), losing it a defensive one (2-3 s); after that the team builds while the
    /// ball is low in its own frame and attacks once it is past <see cref="AiDefinition.BuildMaxBallFraction"/>.
    /// </summary>
    public static class TeamBrain
    {
        /// <summary>Called on the step possession changes hands.</summary>
        public static void OnPossessionChanged(AiTeam team, bool nowInPossession, AiDefinition ai)
        {
            team.Phase = nowInPossession ? TeamPhase.TransitionAttack : TeamPhase.TransitionDefense;
            team.TransitionTimer = nowInPossession ? ai.TransitionAttackSeconds : ai.TransitionDefenseSeconds;
        }

        public static TeamPhase UpdatePhase(AiTeam team, bool inPossession, float ballU, float pitchLength, AiDefinition ai)
        {
            if (team.TransitionTimer > 0f)
                return team.Phase = inPossession ? TeamPhase.TransitionAttack : TeamPhase.TransitionDefense;
            if (!inPossession) return team.Phase = TeamPhase.Defend;
            return team.Phase = ballU < ai.BuildMaxBallFraction * pitchLength ? TeamPhase.Build : TeamPhase.Attack;
        }
    }
}
