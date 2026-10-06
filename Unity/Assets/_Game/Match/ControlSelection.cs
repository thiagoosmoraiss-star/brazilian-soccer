using System.Numerics;

namespace Game.Match
{
    /// <summary>
    /// Which teammate the user controls (GAME_DESIGN §17 "Seleção"). A3 slice: with the ball, control is on the
    /// holder; on a pass, control goes to the receiver as the ball leaves the foot (into space: the teammate
    /// closest to where the ball will stop). Automatic switching without the ball, hysteresis and manual switching
    /// come with DefenseSystem (A5).
    /// </summary>
    public sealed class ControlSelection
    {
        public int Controlled { get; private set; }

        public ControlSelection(int initial)
        {
            Controlled = initial;
        }

        public void OnCaptured(int holder) => Controlled = holder;

        public void OnPassReleased(int kicker, int target, Vector3 predictedStop, PlayerBody[] bodies, int count)
        {
            if (target >= 0)
            {
                Controlled = target;
                return;
            }

            int best = kicker;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (i == kicker) continue;
                float dx = bodies[i].Position.X - predictedStop.X;
                float dy = bodies[i].Position.Y - predictedStop.Y;
                float d = dx * dx + dy * dy;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            Controlled = best;
        }
    }
}
