using Game.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Input
{
    /// <summary>
    /// Touch → commands (TECHNICAL_SPEC §7: "InputAdapter (Unity) | Toques → comandos"; GAME_DESIGN §17). Multitouch:
    /// a finger that lands outside the buttons plants the floating left stick; fingers on the right-hand cluster
    /// hold Chute (big), Passe, Enfiada and Sprint. The same code reads the mouse in the Editor, plus dev-only
    /// keyboard stand-ins (WASD, Shift, J = passe, K = enfiada, Space = chute). Tap/hold/power-bar timing and
    /// sprint memory live in the pure <see cref="ActionInputFilter"/>/<see cref="InputIntentFilter"/> and come from
    /// Data via <see cref="Configure"/>; this class only reads devices and draws the placeholder buttons
    /// (the real HUD is A7).
    /// </summary>
    public sealed class InputAdapter : MonoBehaviour
    {
        private enum Button { None = -1, Shot = 0, Pass = 1, Through = 2, Sprint = 3 }
        private const int ButtonCount = 4;
        private const int NoFinger = int.MinValue;
        private const int MouseFinger = int.MaxValue;

        [Header("Floating stick (screen-height fractions)")]
        public float StickRadius = 0.12f;
        [Range(0f, 1f)] public float Deadzone = 0.15f;

        [Header("Placeholder buttons")]
        public bool ShowButtons = true;

        private readonly InputIntentFilter _moveFilter = new InputIntentFilter();
        private readonly ActionInputFilter _actionFilter = new ActionInputFilter();
        private KickTimings _timings;
        private float _sprintMemorySeconds;

        private int _stickFinger = NoFinger;
        private Vector2 _stickOrigin;
        private readonly int[] _buttonFinger = { NoFinger, NoFinger, NoFinger, NoFinger };
        private readonly bool[] _buttonHeld = new bool[ButtonCount];
        private ActionCommand _pending = ActionCommand.None;
        private GUIStyle _labelStyle;

        public System.Numerics.Vector2 MoveIntent { get; private set; }
        public bool SprintHeld { get; private set; }
        public ActionKind Charging => _actionFilter.Charging;
        public float ChargeFraction => _actionFilter.ChargeFraction;

        /// <summary>Timings from Data/Balance (kicking.json, movement.json); the Input assembly cannot read Data itself.</summary>
        public void Configure(KickTimings timings, float sprintMemorySeconds)
        {
            _timings = timings;
            _sprintMemorySeconds = sprintMemorySeconds;
        }

        /// <summary>The last released action, once (the sandbox consumes it on its next fixed step).</summary>
        public ActionCommand TakeCommand()
        {
            var c = _pending;
            _pending = ActionCommand.None;
            return c;
        }

        private void Update()
        {
            var rawMove = System.Numerics.Vector2.Zero;
            bool stickActive = false;
            for (int b = 0; b < ButtonCount; b++) _buttonHeld[b] = false;

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (TouchControl touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    int id = touch.touchId.ReadValue();
                    Track(id, touch.position.ReadValue(), ref rawMove, ref stickActive);
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) Track(MouseFinger, mouse.position.ReadValue(), ref rawMove, ref stickActive);

            ReleaseLiftedFingers(touchscreen, mouse);
            if (!stickActive) _stickFinger = NoFinger;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float kx = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
                float ky = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
                if (!stickActive && (kx != 0f || ky != 0f)) rawMove = System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(kx, ky));
                _buttonHeld[(int)Button.Sprint] |= keyboard.leftShiftKey.isPressed;
                _buttonHeld[(int)Button.Pass] |= keyboard.jKey.isPressed;
                _buttonHeld[(int)Button.Through] |= keyboard.kKey.isPressed;
                _buttonHeld[(int)Button.Shot] |= keyboard.spaceKey.isPressed;
            }

            var (move, sprint) = _moveFilter.Step(rawMove, _buttonHeld[(int)Button.Sprint], _sprintMemorySeconds, Time.deltaTime);
            MoveIntent = move;
            SprintHeld = sprint;

            var command = _actionFilter.Step(_buttonHeld[(int)Button.Pass], _buttonHeld[(int)Button.Through],
                _buttonHeld[(int)Button.Shot], _timings, Time.deltaTime);
            if (command.Kind != ActionKind.None) _pending = command;
        }

        private void Track(int finger, Vector2 pos, ref System.Numerics.Vector2 rawMove, ref bool stickActive)
        {
            for (int b = 0; b < ButtonCount; b++)
            {
                if (_buttonFinger[b] != finger) continue;
                _buttonHeld[b] = true;
                return;
            }

            if (_stickFinger == finger)
            {
                stickActive = true;
                float radius = StickRadius * Screen.height;
                Vector2 delta = pos - _stickOrigin;
                if (delta.magnitude > Deadzone * radius)
                    rawMove = new System.Numerics.Vector2(delta.x / radius, delta.y / radius);
                return;
            }

            // A new finger: a button if it lands on one, otherwise the stick (if free).
            var hit = HitButton(pos);
            if (hit != Button.None && _buttonFinger[(int)hit] == NoFinger)
            {
                _buttonFinger[(int)hit] = finger;
                _buttonHeld[(int)hit] = true;
            }
            else if (_stickFinger == NoFinger && hit == Button.None)
            {
                _stickFinger = finger;
                _stickOrigin = pos;
                stickActive = true;
            }
        }

        private void ReleaseLiftedFingers(Touchscreen touchscreen, Mouse mouse)
        {
            for (int b = 0; b < ButtonCount; b++)
            {
                int f = _buttonFinger[b];
                if (f == NoFinger) continue;
                if (!IsDown(f, touchscreen, mouse)) _buttonFinger[b] = NoFinger;
            }
        }

        private static bool IsDown(int finger, Touchscreen touchscreen, Mouse mouse)
        {
            if (finger == MouseFinger) return mouse != null && mouse.leftButton.isPressed;
            if (touchscreen == null) return false;
            foreach (TouchControl t in touchscreen.touches)
                if (t.press.isPressed && t.touchId.ReadValue() == finger) return true;
            return false;
        }

        // Layout in screen pixels (origin bottom-left, like the Input System), scaled by screen height.
        private static Vector2 ButtonCenter(Button b)
        {
            float h = Screen.height, w = Screen.width;
            switch (b)
            {
                case Button.Shot: return new Vector2(w - 0.16f * h, 0.18f * h);
                case Button.Pass: return new Vector2(w - 0.40f * h, 0.12f * h);
                case Button.Through: return new Vector2(w - 0.17f * h, 0.42f * h);
                default: return new Vector2(w - 0.42f * h, 0.36f * h);
            }
        }

        private static float ButtonRadius(Button b) => (b == Button.Shot ? 0.12f : 0.075f) * Screen.height;

        private static Button HitButton(Vector2 pos)
        {
            for (int b = 0; b < ButtonCount; b++)
            {
                var button = (Button)b;
                if ((pos - ButtonCenter(button)).sqrMagnitude <= ButtonRadius(button) * ButtonRadius(button)) return button;
            }
            return Button.None;
        }

        private void OnGUI()
        {
            if (!ShowButtons) return;
            if (_labelStyle == null)
                _labelStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.025f) };

            for (int b = 0; b < ButtonCount; b++)
            {
                var button = (Button)b;
                Vector2 c = ButtonCenter(button);
                float r = ButtonRadius(button);
                var rect = new Rect(c.x - r, Screen.height - c.y - r, 2f * r, 2f * r); // OnGUI y grows downwards
                var old = GUI.color;
                GUI.color = _buttonHeld[b] ? Color.yellow : new Color(1f, 1f, 1f, 0.6f);
                GUI.Box(rect, Label(button), _labelStyle);
                GUI.color = old;
            }

            if (Charging != ActionKind.None)
            {
                Vector2 c = ButtonCenter(Button.Shot);
                float r = ButtonRadius(Button.Shot);
                var bar = new Rect(c.x - r, Screen.height - c.y - r - 0.05f * Screen.height, 2f * r * ChargeFraction, 0.03f * Screen.height);
                var old = GUI.color;
                GUI.color = Color.green;
                GUI.Box(bar, GUIContent.none);
                GUI.color = old;
            }
        }

        private static string Label(Button b)
        {
            switch (b)
            {
                case Button.Shot: return "CHUTE";
                case Button.Pass: return "PASSE";
                case Button.Through: return "ENFIADA";
                default: return "SPRINT";
            }
        }
    }
}
