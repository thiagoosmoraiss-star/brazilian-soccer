using System;
using Game.Core.Contracts.Match;
using Game.Match.AI;
using UnityEngine;
using UnityEngine.UIElements;
using DataPosition = Game.Data.Definitions.Position;

namespace Game.Presentation
{
    /// <summary>
    /// The user's substitutions (A9c, X-77; GAME_DESIGN §27 "Substituições na pausa"): from the pause menu or at half-time,
    /// the 11 on the pitch (position, name, energy) and the bench; tap who leaves, tap who comes on, confirm. Shows the
    /// substitutions and stoppages left. After an injury of the user's side (X-91) it opens in the injury mode: with a
    /// substitution left the injured player is the one who leaves and the panel only closes once he is replaced; without
    /// one it says the team plays one short. Placeholder art, built in code on the HUD's UI Toolkit root; it only reads the
    /// match and calls <see cref="MatchSession.Substitute"/>. Rebuilt on open and after each change (paused: allocation
    /// is fine here).
    /// </summary>
    public sealed class SubstitutionPanel
    {
        /// <summary>The user closed the panel (back to the pause menu, or the second half at half-time).</summary>
        public event Action Closed;

        private static readonly Color Selected = new Color(1f, 0.85f, 0.1f);

        private readonly MatchSession _session;
        private readonly MatchSide _side;
        private readonly Func<int, string> _playerName;
        private readonly VisualElement _overlay, _field, _bench;
        private readonly Label _title, _info, _message;
        private readonly Button _confirm, _close;
        private int _out = -1, _in = -1;
        private bool _halftime;
        /// <summary>Injury mode: the injured slot (-1 outside it).</summary>
        private int _injured = -1;

        public bool IsOpen => _overlay.style.display == DisplayStyle.Flex;
        /// <summary>Open in the injury mode (X-91): closing it resumes the match.</summary>
        public bool InjuryMode => _injured >= 0;

        /// <param name="playerName">Display name of a player by his id value.</param>
        public SubstitutionPanel(VisualElement root, MatchSession session, MatchSide side, Func<int, string> playerName)
        {
            _session = session;
            _side = side;
            _playerName = playerName;

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = _overlay.style.top = _overlay.style.right = _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.paddingTop = 24;
            _overlay.style.display = DisplayStyle.None;

            _title = Text("", 44, Color.white);
            _overlay.Add(_title);
            _info = Text("", 26, new Color(1f, 0.9f, 0.4f));
            _info.style.marginBottom = 12;
            _overlay.Add(_info);

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.justifyContent = Justify.Center;
            _field = Column();
            _bench = Column();
            columns.Add(_field);
            columns.Add(_bench);
            _overlay.Add(columns);

            _message = Text("", 26, Color.white);
            _message.style.marginTop = 8;
            _overlay.Add(_message);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 8;
            _confirm = BigButton("Confirmar troca", Confirm);
            _close = BigButton("Voltar", () => { Close(); Closed?.Invoke(); });
            buttons.Add(_confirm);
            buttons.Add(_close);
            _overlay.Add(buttons);
            root.Add(_overlay);
        }

        public void Open(bool halftime)
        {
            _halftime = halftime;
            _injured = -1;
            _out = _in = -1;
            _message.text = "Toque em quem sai e em quem entra.";
            Refresh();
            _overlay.style.display = DisplayStyle.Flex;
        }

        /// <summary>Injury mode (X-91) for slot <paramref name="injured"/> of the user's side.</summary>
        public void OpenInjury(int injured)
        {
            _halftime = false;
            _injured = injured;
            _in = -1;
            var m = _session.Match;
            var team = _side == MatchSide.Home ? m.Home : m.Away;
            string name = _playerName(team.Players[injured].Setup.PlayerId.Value);
            bool forced = _session.PendingInjury == injured;
            _out = forced ? injured : -1;
            _message.text = forced ? $"{name} se lesionou. Escolha quem entra no lugar dele."
                : $"{name} se lesionou e não há mais trocas: o time fica com um a menos.";
            Refresh();
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void Close() => _overlay.style.display = DisplayStyle.None;

        private void Refresh()
        {
            var m = _session.Match;
            bool forced = _injured >= 0 && _session.PendingInjury == _injured;
            _title.text = _halftime ? "INTERVALO · SUBSTITUIÇÕES" : _injured >= 0 ? "LESÃO · SUBSTITUIÇÕES" : "SUBSTITUIÇÕES";
            string stoppage = _halftime ? "  (o intervalo não gasta parada)"
                : m.StoppageInProgress(_side) ? "  (esta parada já está contada)" : "";
            _info.text = $"Trocas restantes: {m.SubstitutionsLeft(_side)}   ·   Paradas restantes: {m.StoppagesLeft(_side)}{stoppage}";

            _field.Clear();
            _field.Add(Header("EM CAMPO"));
            var team = _side == MatchSide.Home ? m.Home : m.Away;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var p = team.Players[i];
                bool sentOff = m.SentOff(p);
                bool injured = m.Injured(p);
                int local = i;
                string state = sentOff ? "EXPULSO" : injured ? "LESIONADO" : $"{p.Body.Energy:0}%";
                var row = Row($"{p.Slot.Position,-4} {_playerName(p.Setup.PlayerId.Value)}   {state}", () => { _out = local; Refresh(); });
                row.SetEnabled(!sentOff && !p.Body.OffPitch && (!forced || local == _injured));
                if (local == _out) Highlight(row);
                _field.Add(row);
            }

            _bench.Clear();
            _bench.Add(Header("BANCO"));
            var bench = m.Bench(_side);
            if (bench.Count == 0) _bench.Add(Text("Sem reservas", 24, Color.white));
            for (int k = 0; k < bench.Count; k++)
            {
                bool used = m.BenchUsed(_side, k);
                int index = k;
                var row = Row($"{(DataPosition)bench[k].MainPosition,-4} {_playerName(bench[k].PlayerId.Value)}{(used ? "   JÁ ENTROU" : "")}",
                    () => { _in = index; Refresh(); });
                row.SetEnabled(!used);
                if (index == _in) Highlight(row);
                _bench.Add(row);
            }

            _confirm.SetEnabled(_out >= 0 && _in >= 0 && m.CanSubstitute(_side, _out, _in));
            _close.text = _halftime ? "Começar o 2º tempo" : _injured >= 0 ? "Continuar o jogo" : "Voltar";
            _close.SetEnabled(!forced); // the injured player must be replaced first
        }

        private void Confirm()
        {
            var m = _session.Match;
            var team = _side == MatchSide.Home ? m.Home : m.Away;
            if (_out < 0 || _in < 0) return;
            string leaving = _playerName(team.Players[_out].Setup.PlayerId.Value);
            string entering = _playerName(m.Bench(_side)[_in].PlayerId.Value);
            _message.text = _session.Substitute(_side, _out, _in) ? $"Sai {leaving}, entra {entering}." : "Troca não permitida.";
            _out = _in = -1;
            if (_injured >= 0 && _session.PendingInjury == _injured) _out = _injured; // refused: still his turn
            Refresh();
        }

        // ---- element helpers (placeholder art) ----

        private static VisualElement Column()
        {
            var c = new VisualElement();
            c.style.width = 700;
            c.style.marginLeft = c.style.marginRight = 20;
            return c;
        }

        private static Label Header(string text)
        {
            var l = Text(text, 28, new Color(0.6f, 0.85f, 1f));
            l.style.marginBottom = 6;
            return l;
        }

        private static Button Row(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.height = 52;
            b.style.fontSize = 24;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.marginTop = b.style.marginBottom = 2;
            return b;
        }

        private static void Highlight(Button b)
        {
            b.style.backgroundColor = Selected;
            b.style.color = Color.black;
        }

        private static Button BigButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.width = 420;
            b.style.height = 80;
            b.style.fontSize = 32;
            b.style.marginLeft = b.style.marginRight = 14;
            return b;
        }

        private static Label Text(string text, int size, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }
    }
}
