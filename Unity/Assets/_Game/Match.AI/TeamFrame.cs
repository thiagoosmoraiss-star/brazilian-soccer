using System.Numerics;

namespace Game.Match.AI
{
    /// <summary>
    /// A team's own frame: <c>u</c> = meters from its own goal line along its attack (0 … pitch length),
    /// <c>v</c> = meters to its left of the center line. All AI reasoning happens here so both teams share the
    /// same code regardless of which end they attack.
    /// </summary>
    public readonly struct TeamFrame
    {
        public readonly bool AttackingPositiveX;
        public readonly float HalfLength;

        public TeamFrame(bool attackingPositiveX, float halfLength)
        {
            AttackingPositiveX = attackingPositiveX;
            HalfLength = halfLength;
        }

        public float U(Vector3 world) => AttackingPositiveX ? world.X + HalfLength : HalfLength - world.X;
        public float V(Vector3 world) => AttackingPositiveX ? world.Y : -world.Y;

        public Vector3 World(float u, float v) =>
            AttackingPositiveX ? new Vector3(u - HalfLength, v, 0f) : new Vector3(HalfLength - u, -v, 0f);

        /// <summary>Unit direction of the attack in world X/Y.</summary>
        public Vector2 Forward => AttackingPositiveX ? Vector2.UnitX : -Vector2.UnitX;

        /// <summary>Center of the goal this team defends.</summary>
        public Vector3 OwnGoal => World(0f, 0f);
        /// <summary>Center of the goal this team attacks.</summary>
        public Vector3 TargetGoal => World(2f * HalfLength, 0f);
    }
}
