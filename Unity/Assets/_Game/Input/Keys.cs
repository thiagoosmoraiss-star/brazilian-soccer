using UnityEngine.InputSystem;

namespace Game.Input
{
    /// <summary>The single-key shortcuts the app and the dev sandboxes use.</summary>
    public enum Shortcut { Escape, P, R, Space, Digit1, Digit2, Digit4, L, K }

    /// <summary>Single-key shortcuts through the Input System (CLAUDE.md §3), so the project runs with the legacy input
    /// manager disabled; only this assembly references the Input System. On Android the system back button arrives as
    /// <see cref="Shortcut.Escape"/>.</summary>
    public static class Keys
    {
        public static bool Pressed(Shortcut shortcut)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[ToKey(shortcut)].wasPressedThisFrame;
        }

        private static Key ToKey(Shortcut shortcut)
        {
            switch (shortcut)
            {
                case Shortcut.Escape: return Key.Escape;
                case Shortcut.P: return Key.P;
                case Shortcut.R: return Key.R;
                case Shortcut.Space: return Key.Space;
                case Shortcut.Digit1: return Key.Digit1;
                case Shortcut.Digit2: return Key.Digit2;
                case Shortcut.L: return Key.L;
                case Shortcut.K: return Key.K;
                default: return Key.Digit4;
            }
        }
    }
}
