using System;
using Game.Core.Contracts.Match;
using Game.Data.Definitions;
using Game.Data.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// One side's tactic during the match (A9c; TECHNICAL_SPEC §7 "TacticsRuntime | Tática e presets em jogo", §9):
    /// the default block of tactics.json changed by the side's mentality (1-5), defensive line (1-3) and pressure (1-3).
    /// Mentality moves the whole block and the depth in possession, and the on-ball risk; the line moves where the
    /// defensive line sits when defending and how high it may push; pressure sets how many press and from how far, and
    /// how long the counter-press lasts. Fatigue intensity is the same table QuickSim uses (match_rules.json). A change
    /// during the match is eased in over <see cref="TacticModifiers.ChangeSmoothSeconds"/> ("suavização de 1-2 s").
    /// Zero allocation after construction.
    /// </summary>
    public sealed class TacticsRuntime
    {
        private readonly TacticsDefinition _def;
        private readonly FatigueRules _fatigue;

        // Targets for the current tactic and the eased values the role layer reads, per TeamPhase.
        private readonly float[] _depthTarget = new float[TeamPhaseInfo.Count];
        private readonly float[] _behindTarget = new float[TeamPhaseInfo.Count];
        private readonly float[] _maxTarget = new float[TeamPhaseInfo.Count];
        private readonly float[] _depth = new float[TeamPhaseInfo.Count];
        private readonly float[] _behind = new float[TeamPhaseInfo.Count];
        private readonly float[] _max = new float[TeamPhaseInfo.Count];
        private float _minTarget, _min;

        public TacticSetup Setup { get; private set; }
        public int Pressers { get; private set; }
        public float PressTriggerDistance { get; private set; }
        public float ProgressWeightScale { get; private set; }
        public float ShootThresholdScale { get; private set; }
        public float RunCooldownScale { get; private set; }
        public float PressedOutletValue { get; private set; }
        public int BoxAttackersOffset { get; private set; }
        public float TransitionDefenseScale { get; private set; }
        /// <summary>Energy drain multiplier of this tactic (match_rules.json fatigue intensity, shared with QuickSim).</summary>
        public float FatigueIntensity { get; private set; }

        public TacticsRuntime(TacticsDefinition def, FatigueRules fatigue, TacticSetup setup)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            _fatigue = fatigue;
            Apply(setup, immediate: true);
        }

        public float Depth(TeamPhase p) => _depth[(int)p];
        public float LineBehindBall(TeamPhase p) => _behind[(int)p];
        public float MaxLine(TeamPhase p) => _max[(int)p];
        public float MinLine => _min;
        public float WidthScale(TeamPhase p) => _def.Phases[(int)p].WidthScale;
        public float LateralShift => _def.LateralShift;

        /// <summary>Sets the tactic; the block reaches it over the smoothing time unless <paramref name="immediate"/>.</summary>
        public void Apply(TacticSetup setup, bool immediate)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            var m = _def.Modifiers;
            int me = setup.Mentality - 1, li = setup.DefensiveLine - 1, pr = setup.Pressure - 1;
            for (int i = 0; i < TeamPhaseInfo.Count; i++)
            {
                var b = _def.Phases[i];
                bool inPossession = TeamPhaseInfo.InPossession((TeamPhase)i);
                _depthTarget[i] = b.Depth * (inPossession ? m.MentalityAttackDepthScale[me] : m.MentalityDefendDepthScale[me]);
                _behindTarget[i] = b.LineBehindBall - m.MentalityLineOffset[me] + (inPossession ? 0f : m.LineBehindBallOffset[li]);
                _maxTarget[i] = b.MaxLine + m.MentalityLineOffset[me] + m.LineMaxOffset[li];
            }
            _minTarget = _def.MinLine + m.LineMinOffset[li];
            Pressers = Math.Max(0, _def.Pressers + m.PressersOffset[pr]);
            PressTriggerDistance = _def.PressTriggerDistance * m.PressTriggerScale[pr];
            ProgressWeightScale = m.MentalityProgressWeightScale[me];
            ShootThresholdScale = m.MentalityShootThresholdScale[me];
            RunCooldownScale = m.MentalityRunCooldownScale[me];
            PressedOutletValue = m.MentalityPressedOutletValue[me];
            BoxAttackersOffset = m.MentalityBoxAttackersOffset[me];
            TransitionDefenseScale = m.TransitionDefenseScale[pr];
            FatigueIntensity = _fatigue == null ? 1f : _fatigue.MentalityIntensity[me] * _fatigue.PressureIntensity[pr];
            if (immediate) Snap();
        }

        /// <summary>Eases the block toward the tactic; <paramref name="dt"/> = time since the last call.</summary>
        public void Tick(float dt)
        {
            float smooth = _def.Modifiers.ChangeSmoothSeconds;
            float k = smooth <= 1e-4f ? 1f : Math.Min(1f, dt / smooth);
            for (int i = 0; i < TeamPhaseInfo.Count; i++)
            {
                _depth[i] += (_depthTarget[i] - _depth[i]) * k;
                _behind[i] += (_behindTarget[i] - _behind[i]) * k;
                _max[i] += (_maxTarget[i] - _max[i]) * k;
            }
            _min += (_minTarget - _min) * k;
        }

        /// <summary>Jumps to the tactic (kick-off, restore).</summary>
        public void Snap()
        {
            Array.Copy(_depthTarget, _depth, _depth.Length);
            Array.Copy(_behindTarget, _behind, _behind.Length);
            Array.Copy(_maxTarget, _max, _max.Length);
            _min = _minTarget;
        }

        // Snapshot access (the eased state).
        internal float[] DepthState => _depth;
        internal float[] BehindState => _behind;
        internal float[] MaxState => _max;
        internal float MinState { get => _min; set => _min = value; }
    }
}
