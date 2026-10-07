using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>
    /// Which teammate the user controls (GAME_DESIGN §17 "Seleção"). With the ball, control is on the holder; on a pass,
    /// it goes to the receiver as the ball leaves the foot. Without the ball (A5): automatic switch to the smallest
    /// "tempo até interceptar" weighted by position, only on a trigger (possession lost, opponent pass, loose ball,
    /// controlled player beaten), with ~25% hysteresis, an intention lock (the user is already steering towards the
    /// ball), a 1 s lock after a manual switch, never while containing; manual switch by tap goes to the ring candidate
    /// (<see cref="Next"/>). Indices are team-local. Zero allocation.
    /// </summary>
    public sealed class ControlSelection
    {
        public const int None = -1;

        public int Controlled { get; private set; }
        /// <summary>Best candidate other than the controlled player (the ring); <see cref="None"/> when there is none.</summary>
        public int Next { get; private set; } = None;

        private float _manualLock;
        private float _autoCooldown;

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

        public void Tick(float dt)
        {
            _manualLock = MathF.Max(0f, _manualLock - dt);
            _autoCooldown = MathF.Max(0f, _autoCooldown - dt);
        }

        /// <summary>Tap on "Trocar": control goes to the ring candidate; automatic switching pauses for a moment.</summary>
        public bool ManualSwitch(DefenseDefinition d)
        {
            if (Next == None) return false;
            Controlled = Next;
            _manualLock = d.PostManualLockSeconds;
            return true;
        }

        /// <summary>"Tempo até interceptar" of one candidate: time to reach the ball at sprint speed, penalised when he is
        /// on the wrong side of the ball (further from his own goal than the ball).</summary>
        public static float InterceptScore(PlayerBody body, MatchPlayerSetup setup, Vector3 ball, Vector3 ownGoal, Balance balance, DefenseDefinition d)
        {
            float dx = body.Position.X - ball.X, dy = body.Position.Y - ball.Y;
            float time = MathF.Sqrt(dx * dx + dy * dy) / MathF.Max(0.1f, balance.Eval(Effect.SprintSpeed, setup.Attributes));
            bool behind = Distance2D(body.Position, ownGoal) > Distance2D(ball, ownGoal);
            return behind ? time * (1f + d.BehindBallPenalty) : time;
        }

        /// <summary>
        /// Refreshes the ring and, on a trigger, switches automatically when allowed.
        /// <paramref name="eligible"/> excludes e.g. the goalkeeper. Returns true when control moved.
        /// </summary>
        public bool UpdateWithoutBall(PlayerBody[] bodies, MatchPlayerSetup[] setups, bool[] eligible, int count, Vector3 ball, Vector3 ownGoal,
            Vector2 moveIntent, bool containing, bool trigger, Balance balance, DefenseDefinition d)
        {
            int best = None;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (i == Controlled || !eligible[i]) continue;
                float s = InterceptScore(bodies[i], setups[i], ball, ownGoal, balance, d);
                if (s < bestScore) { bestScore = s; best = i; }
            }
            Next = best;

            if (!trigger || best == None || containing || _manualLock > 0f || _autoCooldown > 0f) return false;
            if (SteeringTowards(bodies[Controlled], ball, moveIntent, d.IntentionAlignment)) return false;

            float current = InterceptScore(bodies[Controlled], setups[Controlled], ball, ownGoal, balance, d);
            if (bestScore >= current * (1f - d.SwitchHysteresis)) return false;

            Next = Controlled;
            Controlled = best;
            _autoCooldown = d.AutoSwitchCooldownSeconds;
            return true;
        }

        /// <summary>GAME_DESIGN §17 trigger "jogador batido/2 m atrás": the carrier is this much closer to our goal.</summary>
        public static bool IsBeaten(PlayerBody controlled, Vector3 carrier, Vector3 ownGoal, DefenseDefinition d) =>
            Distance2D(controlled.Position, ownGoal) - Distance2D(carrier, ownGoal) >= d.BeatenDistance;

        private static bool SteeringTowards(PlayerBody body, Vector3 ball, Vector2 moveIntent, float alignment)
        {
            float len = moveIntent.Length();
            if (len < 0.1f) return false;
            var toBall = new Vector2(ball.X - body.Position.X, ball.Y - body.Position.Y);
            float d = toBall.Length();
            if (d < 1e-3f) return true;
            return Vector2.Dot(moveIntent / len, toBall / d) > alignment;
        }

        private static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }
}
