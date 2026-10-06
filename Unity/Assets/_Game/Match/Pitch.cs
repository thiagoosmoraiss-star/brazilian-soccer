using System;
using System.Numerics;
using Game.Data.Match;
using Game.Match.Geometry;

namespace Game.Match
{
    /// <summary>
    /// Field geometry (TECHNICAL_SPEC §6: "Pitch | Geometria (105×68 m inicial), áreas, gols, zonas | Config").
    /// Centered on the origin; x = length (the two goal lines at ±<see cref="HalfLength"/>), y = width, z = height.
    /// A1 added the boundaries, goal mouth and post/crossbar bars; A3 the penalty areas (shot error inside vs
    /// outside the box). Other zones are added by the stage that first needs them.
    /// </summary>
    public sealed class Pitch
    {
        public float Length { get; }
        public float Width { get; }
        public float GoalWidth { get; }
        public float GoalHeight { get; }
        public float PostRadius { get; }
        public float PenaltyAreaDepth { get; }
        public float PenaltyAreaWidth { get; }

        public float HalfLength { get; }
        public float HalfWidth { get; }
        public float HalfGoalWidth { get; }

        /// <summary>Goal line x position for the home end (negative x).</summary>
        public float HomeGoalLineX { get; }
        /// <summary>Goal line x position for the away end (positive x).</summary>
        public float AwayGoalLineX { get; }

        public readonly Segment3 HomeLeftPost, HomeRightPost, HomeCrossbar;
        public readonly Segment3 AwayLeftPost, AwayRightPost, AwayCrossbar;

        public Pitch(float length, float width, float goalWidth, float goalHeight, float postRadius,
            float penaltyAreaDepth, float penaltyAreaWidth)
        {
            PenaltyAreaDepth = penaltyAreaDepth;
            PenaltyAreaWidth = penaltyAreaWidth;
            Length = length;
            Width = width;
            GoalWidth = goalWidth;
            GoalHeight = goalHeight;
            PostRadius = postRadius;

            HalfLength = length * 0.5f;
            HalfWidth = width * 0.5f;
            HalfGoalWidth = goalWidth * 0.5f;
            HomeGoalLineX = -HalfLength;
            AwayGoalLineX = HalfLength;

            HomeLeftPost = new Segment3(new Vector3(HomeGoalLineX, -HalfGoalWidth, 0f), new Vector3(HomeGoalLineX, -HalfGoalWidth, GoalHeight));
            HomeRightPost = new Segment3(new Vector3(HomeGoalLineX, HalfGoalWidth, 0f), new Vector3(HomeGoalLineX, HalfGoalWidth, GoalHeight));
            HomeCrossbar = new Segment3(new Vector3(HomeGoalLineX, -HalfGoalWidth, GoalHeight), new Vector3(HomeGoalLineX, HalfGoalWidth, GoalHeight));

            AwayLeftPost = new Segment3(new Vector3(AwayGoalLineX, -HalfGoalWidth, 0f), new Vector3(AwayGoalLineX, -HalfGoalWidth, GoalHeight));
            AwayRightPost = new Segment3(new Vector3(AwayGoalLineX, HalfGoalWidth, 0f), new Vector3(AwayGoalLineX, HalfGoalWidth, GoalHeight));
            AwayCrossbar = new Segment3(new Vector3(AwayGoalLineX, -HalfGoalWidth, GoalHeight), new Vector3(AwayGoalLineX, HalfGoalWidth, GoalHeight));
        }

        public static Pitch From(PitchParameters p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            return new Pitch(p.Length, p.Width, p.GoalWidth, p.GoalHeight, p.PostRadius, p.PenaltyAreaDepth, p.PenaltyAreaWidth);
        }

        /// <summary>Goal line x of the end a side attacks (positive x = the away end).</summary>
        public float AttackedGoalLineX(bool attackingPositiveX) => attackingPositiveX ? AwayGoalLineX : HomeGoalLineX;

        /// <summary>True if (x, y) is inside the penalty area of the attacked end.</summary>
        public bool InPenaltyArea(float x, float y, bool attackingPositiveX)
        {
            if (MathF.Abs(y) > PenaltyAreaWidth * 0.5f) return false;
            return attackingPositiveX ? x >= HalfLength - PenaltyAreaDepth && x <= HalfLength
                                      : x <= -HalfLength + PenaltyAreaDepth && x >= -HalfLength;
        }

        /// <summary>True if (y, z) is within the open goal mouth (strictly between the posts, at or below the crossbar).</summary>
        public bool WithinGoalMouth(float y, float z) => y > -HalfGoalWidth && y < HalfGoalWidth && z >= 0f && z <= GoalHeight;
    }
}
