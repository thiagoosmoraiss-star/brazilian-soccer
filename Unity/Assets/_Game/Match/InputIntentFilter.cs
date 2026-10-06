using System;
using System.Numerics;

namespace Game.Match
{
    /// <summary>
    /// Sprint memory (GAME_DESIGN §17: "0,4 s após soltar se o analógico continua empurrado"): the pure, testable
    /// half of <c>InputAdapter</c> (Unity touch/mouse reading stays in the Input assembly, which cannot be
    /// dotnet-tested; TECHNICAL_SPEC §7 marks InputAdapter itself "testável com toques gravados" instead).
    /// </summary>
    public sealed class InputIntentFilter
    {
        private float _sprintMemoryRemaining;

        /// <param name="rawMove">This frame's raw stick direction (zero when not moving).</param>
        /// <param name="rawSprintHeld">True while the sprint control is actually held.</param>
        public (Vector2 Move, bool Sprint) Step(Vector2 rawMove, bool rawSprintHeld, float sprintMemorySeconds, float dt)
        {
            bool moving = rawMove.LengthSquared() > 0f;
            if (rawSprintHeld) _sprintMemoryRemaining = sprintMemorySeconds;
            else _sprintMemoryRemaining = MathF.Max(0f, _sprintMemoryRemaining - dt);

            bool sprint = moving && (rawSprintHeld || _sprintMemoryRemaining > 0f);
            return (rawMove, sprint);
        }
    }
}
