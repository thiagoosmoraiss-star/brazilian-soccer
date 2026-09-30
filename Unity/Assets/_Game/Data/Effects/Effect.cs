namespace Game.Data.Effects
{
    /// <summary>
    /// Catalog of effects evaluated through <see cref="Balance.Eval"/>, derived from the attribute -> effect
    /// table in TECHNICAL_SPEC.md §8 (D-20). Every member must have a definition in Data/Balance/effects.json
    /// (baseline v1 values, recalibrated in data only). Values are contiguous from 0 and never reordered.
    /// Context-qualified rows of the table are separate members: "ShotAngleError (área/fora)" ->
    /// ShotAngleErrorInBox / ShotAngleErrorOutOfBox; "TurnSpeedLoss com bola" -> TurnSpeedLossWithBall.
    /// </summary>
    public enum Effect
    {
        SprintSpeed = 0,
        JogSpeed = 1,
        AccelTime = 2,
        DecelTime = 3,
        TurnSpeedLoss = 4,
        TurnRecoverTime = 5,
        LooseBallReaction = 6,
        EnergyDrainMult = 7,
        LateMatchPenalty = 8,
        InjuryChance = 9,
        BodyDuelWin = 10,
        ShieldStrength = 11,
        AerialDuel = 12,
        PassAngleError = 13,
        PassPowerError = 14,
        PassBallSpeed = 15,
        CrossAngleError = 16,
        CrossQuality = 17,
        DribbleTouchDistance = 18,
        FeintSuccess = 19,
        FirstTouchError = 20,
        TurnSpeedLossWithBall = 21,
        ShotAngleErrorInBox = 22,
        FinesseAccuracy = 23,
        ShotAngleErrorOutOfBox = 24,
        ShotPowerMax = 25,
        FreeKickAccuracy = 26,
        HeaderAccuracy = 27,
        HeaderPower = 28,
        TackleWinChance = 29,
        FoulChance = 30,
        ShotBlockChance = 31,
        ThroughBallError = 32,
        LeadCalcError = 33,
        AiPassOptionsCount = 34,
        RunTriggerThreshold = 35,
        AiTargetError = 36,
        AiCorrectionDelay = 37,
        PressureErrorMult = 38,
        BigMatchErrorMult = 39,
        PenaltyAimWobble = 40,
        GkReactionTime = 41,
        GkDiveReach = 42,
        GkAngleError = 43,
        GkRushDecisionQuality = 44,
        GkCrossClaimRange = 45,
        GkCrossDecisionQuality = 46,
        GkCatchChance = 47,
        GkDistributionError = 48,
    }
}
