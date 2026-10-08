using Game.Match.AI;

namespace Game.App.Dev
{
    /// <summary>Dev-sandbox text for the A7a clock, score and restarts (the real HUD is A7b).</summary>
    public static class MatchHud
    {
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
