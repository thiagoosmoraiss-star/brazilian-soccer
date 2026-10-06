using System.Numerics;
using Game.Match.Geometry;

namespace Game.Match
{
    /// <summary>
    /// Field geometry (TECHNICAL_SPEC §6: "Pitch | Geometria (105×68 m inicial), áreas, gols, zonas | Config").
    /// Centered on the origin; x = length (the two goal lines at ±<see cref="HalfLength"/>), y = width, z = height.
    /// Areas/zones beyond the goal mouth are added by the stage that first needs them (A4+); A1 only needs enough
    /// geometry for the ball: boundaries, goal mouth and the post/crossbar bars.
    /// </summary>
    public sealed class Pitch
    {
        public float Length { get; }
        public float Width { get; }
        public float GoalWidth { get; }
        public float GoalHeight { get; }
        public float PostRadius { get; }

        public float HalfLength { get; }
        public float HalfWidth { get; }
        public float HalfGoalWidth { get; }

        /// <summary>Goal line x position for the home end (negative x).</summary>
        public float HomeGoalLineX { get; }
        /// <summary>Goal line x position for the away end (positive x).</summary>
        public float AwayGoalLineX { get; }

        public readonly Segment3 HomeLeftPost, HomeRightPost, HomeCrossbar;
        public readonly Segment3 AwayLeftPost, AwayRightPost, AwayCrossbar;

        public Pitch(float length, float width, float goalWidth, float goalHeight, float postRadius)
        {
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

        /// <summary>True if (y, z) is within the open goal mouth (strictly between the posts, at or below the crossbar).</summary>
        public bool WithinGoalMouth(float y, float z) => y > -HalfGoalWidth && y < HalfGoalWidth && z >= 0f && z <= GoalHeight;
    }
}
