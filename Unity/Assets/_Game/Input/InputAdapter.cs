using System.Numerics;
using Game.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    /// <summary>
    /// Touch → commands (TECHNICAL_SPEC §7: "InputAdapter (Unity) | Toques → comandos"). A2 slice: a floating
    /// left analog (GAME_DESIGN §17 — press anywhere to plant it, drag for direction) and a sprint hold. Reads
    /// the new Input System's <c>Touchscreen</c> on device, <c>Mouse</c> in the Editor (same code either way);
    /// <c>Keyboard</c> Left Shift is a dev-only sprint stand-in until a second on-screen control exists (A3+
    /// adds the button cluster). The buffering/sprint-memory math itself lives in the pure
    /// <see cref="InputIntentFilter"/> so it can be dotnet-tested; this class only reads devices.
    /// </summary>
    public sealed class InputAdapter : MonoBehaviour
    {
        [Header("Floating stick")]
        public float MaxRadiusPixels = 150f;
        [Range(0f, 1f)] public float Deadzone = 0.15f;
        public float SprintMemorySeconds = 0.4f;

        private readonly InputIntentFilter _filter = new InputIntentFilter();
        private UnityEngine.Vector2 _origin;
        private bool _dragging;

        /// <summary>Direction only (not clamped to length 1 beyond the stick's own radius); Z is always 0.</summary>
        public Vector2 MoveIntent { get; private set; }
        public bool SprintHeld { get; private set; }

        private void Update()
        {
            bool pressed = false;
            UnityEngine.Vector2 pointerPos = default;

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                pressed = true;
                pointerPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                pressed = true;
                pointerPos = Mouse.current.position.ReadValue();
            }

            var rawMove = Vector2.Zero;
            if (pressed)
            {
                if (!_dragging) { _dragging = true; _origin = pointerPos; }
                UnityEngine.Vector2 delta = pointerPos - _origin;
                if (delta.magnitude > Deadzone * MaxRadiusPixels)
                    rawMove = new Vector2(delta.x / MaxRadiusPixels, delta.y / MaxRadiusPixels);
            }
            else
            {
                _dragging = false;
            }

            bool rawSprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            var (move, sprint) = _filter.Step(rawMove, rawSprint, SprintMemorySeconds, Time.deltaTime);
            MoveIntent = move;
            SprintHeld = sprint;
        }
    }
}
