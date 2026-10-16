using System;
using System.Collections.Generic;
using Game.Core.Contracts.Match;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// Tactics from the pause (A9c; GAME_DESIGN §27: formação, mentalidade, linha, pressão; TECHNICAL_SPEC §15): one row
    /// per control with ◀ ▶, "Aplicar" sends the tactic, "Voltar" closes. Placeholder art (D-02), UI Toolkit, built in code.
    /// </summary>
    public sealed class TacticsPanel
    {
        private static readonly string[] MentalityNames = { "Muito defensiva", "Defensiva", "Equilibrada", "Ofensiva", "Muito ofensiva" };
        private static readonly string[] LevelNames = { "Baixa", "Média", "Alta" };

        public event Action<TacticSetup> Applied;
        public event Action Closed;

        public VisualElement Root { get; }

        private readonly IReadOnlyList<string> _formations;
        private int _formation, _mentality = 3, _line = 2, _pressure = 2;
        private readonly Label _formationValue, _mentalityValue, _lineValue, _pressureValue;

        public TacticsPanel(IReadOnlyList<string> formationIds)
        {
            _formations = formationIds ?? throw new ArgumentNullException(nameof(formationIds));
            Root = new VisualElement();
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.top = Root.style.right = Root.style.bottom = 0;
            Root.style.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            Root.style.display = DisplayStyle.None;

            Root.Add(Text("TÁTICA", 56));
            _formationValue = Row("Formação", () => Cycle(ref _formation, -1, _formations.Count), () => Cycle(ref _formation, 1, _formations.Count));
            _mentalityValue = Row("Mentalidade", () => Step(ref _mentality, -1, 5), () => Step(ref _mentality, 1, 5));
            _lineValue = Row("Linha", () => Step(ref _line, -1, 3), () => Step(ref _line, 1, 3));
            _pressureValue = Row("Pressão", () => Step(ref _pressure, -1, 3), () => Step(ref _pressure, 1, 3));

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 24;
            buttons.Add(Button("Aplicar", 300, () =>
            {
                Applied?.Invoke(new TacticSetup(_formations[_formation], _mentality, _line, _pressure));
                Hide();
                Closed?.Invoke();
            }));
            buttons.Add(Button("Voltar", 300, () => { Hide(); Closed?.Invoke(); }));
            Root.Add(buttons);
        }

        public void Show(TacticSetup current)
        {
            _formation = 0;
            for (int i = 0; i < _formations.Count; i++) if (_formations[i] == current.FormationId) _formation = i;
            _mentality = current.Mentality;
            _line = current.DefensiveLine;
            _pressure = current.Pressure;
            Refresh();
            Root.style.display = DisplayStyle.Flex;
        }

        public void Hide() => Root.style.display = DisplayStyle.None;

        private void Cycle(ref int value, int delta, int count)
        {
            value = (value + delta + count) % count;
            Refresh();
        }

        private void Step(ref int value, int delta, int max)
        {
            value = Mathf.Clamp(value + delta, 1, max);
            Refresh();
        }

        private void Refresh()
        {
            _formationValue.text = _formations[_formation];
            _mentalityValue.text = MentalityNames[_mentality - 1];
            _lineValue.text = LevelNames[_line - 1];
            _pressureValue.text = LevelNames[_pressure - 1];
        }

        private Label Row(string name, Action previous, Action next)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 10;
            var title = Text(name, 34);
            title.style.width = 280;
            row.Add(title);
            row.Add(Button("◀", 90, previous));
            var value = Text("", 34);
            value.style.width = 360;
            value.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(value);
            row.Add(Button("▶", 90, next));
            Root.Add(row);
            return value;
        }

        private static Label Text(string text, int size)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = Color.white;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        private static Button Button(string text, float width, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.width = width;
            b.style.height = 80;
            b.style.fontSize = 34;
            b.style.marginLeft = b.style.marginRight = 8;
            return b;
        }
    }
}
