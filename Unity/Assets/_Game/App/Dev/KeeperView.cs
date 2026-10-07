using Game.Match.AI;
using UnityEngine;

namespace Game.App.Dev
{
    /// <summary>Dev-sandbox drawing of the A6 goalkeepers: the capsule lies down along the dive while diving or on the
    /// ground, and a short label tells what each keeper is doing. Placeholder until real animations (A7).</summary>
    public static class KeeperView
    {
        /// <summary>Poses a player's capsule; keepers diving or grounded lie along their dive direction.</summary>
        public static void Pose(Transform t, AiMatch match, AiPlayer p, Vector3 groundPosition, float halfHeight, float radius)
        {
            if (!p.IsGoalkeeper)
            {
                t.position = groundPosition + Vector3.up * halfHeight;
                t.rotation = Quaternion.identity;
                return;
            }
            var k = match.KeeperOf(p.Team);
            bool down = k.State == KeeperState.Diving || k.State == KeeperState.Grounded;
            if (!down)
            {
                t.position = groundPosition + Vector3.up * halfHeight;
                t.rotation = Quaternion.identity;
                return;
            }
            var v = p.Body.Velocity;
            var dir = new Vector3(v.X, 0f, v.Y); // sim (x, y) → Unity (x, z)
            if (k.State == KeeperState.Diving && dir.sqrMagnitude > 1e-4f) t.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            t.position = groundPosition + Vector3.up * radius;
        }

        public static string Label(Keeper k)
        {
            switch (k.State)
            {
                case KeeperState.Reacting: return "reagindo";
                case KeeperState.Tracking: return "indo na bola";
                case KeeperState.Diving: return "mergulhando";
                case KeeperState.Grounded: return "no chão";
                case KeeperState.Holding: return "com a bola";
                default: return "posicionado";
            }
        }

        public static string Summary(AiMatch m) =>
            $"Goleiro casa: {Label(m.HomeKeeper)} ({m.HomeKeeper.Saves} def.)   Goleiro visitante: {Label(m.AwayKeeper)} ({m.AwayKeeper.Saves} def.)";
    }
}
