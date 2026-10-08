using Game.Match.AI;
using UnityEngine;

namespace Game.App.Dev
{
    /// <summary>Dev-sandbox text for the A7a clock, score and restarts (the real HUD is A7b).</summary>
    public static class MatchHud
    {
        private static GUIStyle _text;
        private static Texture2D _shade;

        /// <summary>Text on a dark translucent panel sized to it, readable over the green pitch and the white lines
        /// (font scaled with the screen height).</summary>
        public static void Panel(float x, float y, string text)
        {
            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false, padding = new RectOffset(10, 10, 6, 6) };
                _text.normal.textColor = Color.white;
                _shade = new Texture2D(1, 1);
                _shade.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
                _shade.Apply();
            }
            _text.fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height * 0.024f));
            var size = _text.CalcSize(new GUIContent(text));
            var rect = new Rect(x, y, size.x, size.y);
            GUI.DrawTexture(rect, _shade);
            GUI.Label(rect, text, _text);
        }

        public static string Clock(AiMatch m)
        {
            int minute = (int)m.ClockMinutes;
            return $"{minute:00}' ({m.Half}º tempo)";
        }

        public static string RestartName(RestartKind kind)
        {
            switch (kind)
            {
                case RestartKind.Kickoff: return "Saída de bola";
                case RestartKind.ThrowIn: return "Lateral";
                case RestartKind.GoalKick: return "Tiro de meta";
                case RestartKind.Corner: return "Escanteio";
                default: return "";
            }
        }

        /// <summary>What the restart line says, or empty in open play.</summary>
        public static string RestartLine(AiMatch m)
        {
            if (m.Restart == RestartKind.None) return "";
            string who = m.RestartTeam == m.HumanTeam ? "seu" : (m.HumanTeam == null ? (m.RestartTeam == m.Home ? "casa" : "visitante") : "adversário");
            if (!m.RestartReady) return $"{RestartName(m.Restart)} ({who}) — posicionando";
            return m.RestartTeam == m.HumanTeam
                ? $"{RestartName(m.Restart)}: mire com o analógico e toque PASSE (J) ou ENFIADA (K)"
                : $"{RestartName(m.Restart)} ({who})";
        }
    }
}
