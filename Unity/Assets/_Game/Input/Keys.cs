using UnityEngine.InputSystem;

namespace Game.Input
{
    /// <summary>Single-key shortcuts through the Input System (CLAUDE.md §3), so the project runs with the legacy input
    /// manager disabled. On Android the system back button arrives as <see cref="Key.Escape"/>.</summary>
    public static class Keys
    {
        public static bool Pressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }
    }
}
