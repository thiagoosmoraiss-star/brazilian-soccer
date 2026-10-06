namespace Game.Data.Match
{
    /// <summary>Coefficients shared by passes and shots (Data/Balance/kicking.json "common", A3, baseline v1, X-50).</summary>
    public sealed class KickingCommon
    {
        /// <summary>A button released within this time is a tap (the game resolves the force, GAME_DESIGN §19).</summary>
        public float TapMaxSeconds { get; internal set; }
        /// <summary>A kick aimed more than this far toward the preferred-foot side of the body uses the weak foot (X-50).</summary>
        public float WeakFootAngleDegrees { get; internal set; }
        /// <summary>Kicks within this angle of the body's facing carry no orientation/balance penalty.</summary>
        public float OrientationFreeAngleDegrees { get; internal set; }
        /// <summary>Energy (0-100) below which precision degrades (GAME_DESIGN §18: &lt; 40%).</summary>
        public float LowEnergyThreshold { get; internal set; }
    }

    /// <summary>Pass coefficients (GAME_DESIGN §19). Attribute-dependent values come from the Effect catalog
    /// (PassAngleError, PassPowerError, PassBallSpeed, ThroughBallError, LeadCalcError, PressureErrorMult).</summary>
    public sealed class PassParameters
    {
        /// <summary>Semi assistance cone half-angle (GAME_DESIGN §19: ±25°).</summary>
        public float ConeHalfAngleDegrees { get; internal set; }
        /// <summary>Cone target score = angle (deg) + distance × this; lowest wins.</summary>
        public float ConeDistanceWeightDegPerMeter { get; internal set; }
        public float MaxTargetDistance { get; internal set; }
        /// <summary>Distance of a tapped pass into space when no teammate is in the cone.</summary>
        public float SpacePassDistance { get; internal set; }
        /// <summary>Speed the ball should still have when it reaches the receiver on an auto-force pass.</summary>
        public float ArrivalSpeed { get; internal set; }
        public float MinSpeed { get; internal set; }
        public float MaxSpeed { get; internal set; }
        /// <summary>Hold time for 100% power on a manual (held) pass.</summary>
        public float PowerBarSeconds { get; internal set; }
        /// <summary>Lead ahead of a static receiver on a through ball (along the attack direction).</summary>
        public float ThroughLeadDistance { get; internal set; }
        public float ThroughArrivalSpeed { get; internal set; }
        /// <summary>An opponent closer than this pressures the passer (GAME_DESIGN §19: &lt; 2 m).</summary>
        public float PressureRadius { get; internal set; }
        public float OrientationMaxErrorPenalty { get; internal set; }
        public float WeakFootMaxErrorPenalty { get; internal set; }
        public float FirstTimeErrorPenalty { get; internal set; }
        public float LowEnergyMaxErrorPenalty { get; internal set; }
        public float DistanceReference { get; internal set; }
        public float DistanceErrorPerMeter { get; internal set; }
    }

    /// <summary>Shot coefficients (GAME_DESIGN §20). Attribute-dependent values come from the Effect catalog
    /// (ShotAngleErrorInBox, ShotAngleErrorOutOfBox, ShotPowerMax, PressureErrorMult).</summary>
    public sealed class ShotParameters
    {
        /// <summary>Hold time for a full power bar (GAME_DESIGN §20: ~0.8 s).</summary>
        public float PowerBarSeconds { get; internal set; }
        public float MinSpeed { get; internal set; }
        /// <summary>Above this power fraction the vertical error grows (GAME_DESIGN §20: ideal 40-75%).</summary>
        public float IdealPowerMax { get; internal set; }
        /// <summary>Extra upward error at 100% power, scaled by how far above the ideal band the bar was.</summary>
        public float OverPowerVerticalErrorDegrees { get; internal set; }
        /// <summary>Vertical error = horizontal angle error × this.</summary>
        public float VerticalErrorFraction { get; internal set; }
        /// <summary>Height aimed at when picking a corner (m).</summary>
        public float TargetHeight { get; internal set; }
        /// <summary>How far inside the post the chosen corner is aimed (m).</summary>
        public float CornerInset { get; internal set; }
        /// <summary>Stick sideways component (relative to the goal) below this = neutral, assistance picks the corner.</summary>
        public float AimNeutralThreshold { get; internal set; }
        /// <summary>GAME_DESIGN §20: pressure &lt; 1.5 m.</summary>
        public float PressureRadius { get; internal set; }
        /// <summary>Scales the Composure pressure penalty (+30-80% for passes) to the shot range (+40-100%).</summary>
        public float PressureErrorScale { get; internal set; }
        public float PressurePowerLoss { get; internal set; }
        /// <summary>"Equilíbrio": penalty for shooting across/behind the body.</summary>
        public float OrientationMaxErrorPenalty { get; internal set; }
        public float WeakFootMaxErrorPenalty { get; internal set; }
        /// <summary>Weak-foot power loss at weak foot 4 (min) to weak foot 1 (max); none at 5 (GAME_DESIGN §20: -10-25%).</summary>
        public float WeakFootPowerLossMin { get; internal set; }
        public float WeakFootPowerLossMax { get; internal set; }
        public float FirstTimeErrorPenalty { get; internal set; }
        public float LowEnergyMaxErrorPenalty { get; internal set; }
        /// <summary>Error grows linearly beyond this shot distance (X-50: keeps a shot from midfield rarely on target).</summary>
        public float DistanceReference { get; internal set; }
        public float DistanceErrorPerMeter { get; internal set; }
    }

    /// <summary>Data/Balance/kicking.json root (A3, baseline v1, X-50).</summary>
    public sealed class KickingDefinition
    {
        public KickingCommon Common { get; internal set; }
        public PassParameters Pass { get; internal set; }
        public ShotParameters Shot { get; internal set; }
    }
}
