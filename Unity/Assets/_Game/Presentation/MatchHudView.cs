using System;
using Game.Core.Contracts.Match;
using Game.Data.Presentation;
using Game.Match.AI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// Minimal match HUD (A7b; TECHNICAL_SPEC §15 "Match HUD / Pause", UI Toolkit): scoreboard and clock, event banner,
    /// translucent collapsible radar (GAME_DESIGN §16), pause button, pause menu and the final screen. Built in code on a
    /// runtime <see cref="UIDocument"/>. Texts are rewritten only when their value changes (no allocation in a normal
    /// frame). The on-screen control buttons stay in <c>InputAdapter</c> until the real HUD art.
    /// </summary>
    public sealed class MatchHudView
    {
        // Layout in reference pixels (1920×1080, scaled with the screen height): placeholder art, not tuning.
        private const float RefWidth = 1920f, RefHeight = 1080f, Margin = 16f;

        public event Action PauseRequested;
        public event Action ResumeRequested;
        public event Action RestartRequested;
        /// <summary>"Tática" in the pause menu (A9c).</summary>
        public event Action TacticsRequested;
        /// <summary>"Substituições" in the pause menu (A9c).</summary>
        public event Action SubstitutionsRequested;

        private readonly MatchViewDefinition _v;
        private readonly string _homeName, _awayName;
        private readonly float _halfLength, _halfWidth, _side;
        private readonly VisualElement _root, _radar, _radarField, _pauseMenu, _final;
        private readonly Label _score, _clock, _banner, _finalScore, _finalStats, _radarToggle, _fps, _hint, _card;
        private readonly Func<int, string> _playerName;
        private Button _restartButton, _finalButton;
        private int _shownHint = -1, _seenEvents = -1;
        private float _cardUntil;
        private readonly VisualElement[] _dots;
        private readonly VisualElement _ballDot;
        private readonly float _radarW, _radarH;
        private int _shownHome = -1, _shownAway = -1, _shownMinute = -1, _shownHalf = -1;
        private float _lastBannerEvent = float.PositiveInfinity;
        private bool _radarOpen = true;

        /// <summary>The HUD's UI Toolkit root (other match panels — substitutions — are added to it).</summary>
        public VisualElement Root => _root;

        /// <summary>Fraction of the screen height the HUD occupies at the top (touches there are not the stick).</summary>
        public float ReservedTopFraction => (_radarH + 2f * Margin + 40f) / RefHeight;

        /// <param name="playerName">Display name of a player by his id value (card notices).</param>
        public MatchHudView(MatchViewDefinition view, ThemeStyleSheet theme, string homeName, string awayName, Color homeColor, Color awayColor,
            float pitchHalfLength, float pitchHalfWidth, bool userAttacksPositiveX, int players, Func<int, string> playerName)
        {
            _v = view;
            _playerName = playerName;
            _homeName = homeName;
            _awayName = awayName;
            _halfLength = pitchHalfLength;
            _halfWidth = pitchHalfWidth;
            _side = userAttacksPositiveX ? 1f : -1f;

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int((int)RefWidth, (int)RefHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f;
            var go = new GameObject("Match HUD");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            go.SetActive(true);
            _root = doc.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;

            // Scoreboard (top left): HOME 0 x 0 AWAY   12'
            var board = Box(new Color(0f, 0f, 0f, 0.7f));
            board.style.position = Position.Absolute;
            board.style.left = Margin;
            board.style.top = Margin;
            board.style.flexDirection = FlexDirection.Row;
            board.style.alignItems = Align.Center;
            board.Add(Text(homeName, 30, homeColor));
            _score = Text("0 x 0", 34, Color.white);
            _score.style.marginLeft = 14;
            _score.style.marginRight = 14;
            board.Add(_score);
            board.Add(Text(awayName, 30, awayColor));
            _clock = Text("00'", 30, new Color(1f, 0.9f, 0.4f));
            _clock.style.marginLeft = 24;
            board.Add(_clock);
            _root.Add(board);

            // Pause button (top right).
            var pause = new Button(() => PauseRequested?.Invoke()) { text = "II" };
            pause.style.position = Position.Absolute;
            pause.style.right = Margin;
            pause.style.top = Margin;
            pause.style.width = 90;
            pause.style.height = 70;
            pause.style.fontSize = 34;
            _root.Add(pause);

            // Radar (top centre), collapsible by tapping its label.
            _radarW = view.RadarWidthFraction * RefWidth;
            _radarH = _radarW * (pitchHalfWidth / pitchHalfLength);
            _radar = new VisualElement();
            _radar.style.position = Position.Absolute;
            _radar.style.top = Margin;
            _radar.style.left = (RefWidth - _radarW) * 0.5f;
            _radar.style.width = _radarW;
            _radarToggle = Text("RADAR", 18, Color.white);
            _radarToggle.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _radarToggle.pickingMode = PickingMode.Position;
            _radarToggle.RegisterCallback<ClickEvent>(_ => ToggleRadar());
            _radar.Add(_radarToggle);
            _radarField = new VisualElement();
            _radarField.style.width = _radarW;
            _radarField.style.height = _radarH;
            _radarField.style.backgroundColor = new Color(0.1f, 0.35f, 0.1f, view.RadarOpacity);
            _radarField.style.borderTopWidth = _radarField.style.borderBottomWidth = _radarField.style.borderLeftWidth = _radarField.style.borderRightWidth = 2;
            var line = new Color(1f, 1f, 1f, view.RadarOpacity);
            _radarField.style.borderTopColor = _radarField.style.borderBottomColor = _radarField.style.borderLeftColor = _radarField.style.borderRightColor = line;
            _radarField.pickingMode = PickingMode.Ignore;
            var halfway = new VisualElement();
            halfway.style.position = Position.Absolute;
            halfway.style.left = _radarW * 0.5f;
            halfway.style.top = 0;
            halfway.style.width = 1;
            halfway.style.height = _radarH;
            halfway.style.backgroundColor = line;
            _radarField.Add(halfway);
            _dots = new VisualElement[players];
            for (int i = 0; i < players; i++)
            {
                _dots[i] = Dot(i < players / 2 ? homeColor : awayColor, view.RadarDotSize);
                _radarField.Add(_dots[i]);
            }
            _ballDot = Dot(Color.white, view.RadarDotSize * 0.8f);
            _radarField.Add(_ballDot);
            _radar.Add(_radarField);
            _root.Add(_radar);

            // Frame-rate readout (bottom left, for the device acceptance; GAME_DESIGN / TECHNICAL_SPEC §19).
            _fps = Text("", 22, new Color(1f, 1f, 1f, 0.85f));
            _fps.style.position = Position.Absolute;
            _fps.style.left = Margin;
            _fps.style.bottom = Margin;
            _fps.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _root.Add(_fps);

            // Event banner (centre).
            _banner = Text("", 56, Color.white);
            _banner.style.position = Position.Absolute;
            _banner.style.top = RefHeight * 0.32f;
            _banner.style.left = 0;
            _banner.style.right = 0;
            _banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            _banner.style.display = DisplayStyle.None;
            _root.Add(_banner);

            // Card notice (A8b), under the banner.
            _card = Text("", 40, Color.black);
            _card.style.paddingLeft = _card.style.paddingRight = 24;
            _card.style.paddingTop = _card.style.paddingBottom = 8;
            _card.style.display = DisplayStyle.None;
            var cardRow = new VisualElement();
            cardRow.style.position = Position.Absolute;
            cardRow.style.left = 0;
            cardRow.style.right = 0;
            cardRow.style.top = RefHeight * 0.44f;
            cardRow.style.alignItems = Align.Center;
            cardRow.pickingMode = PickingMode.Ignore;
            cardRow.Add(_card);
            _root.Add(cardRow);

            // Set-piece instructions for the user (A8b), lower centre.
            _hint = Text("", 28, Color.white);
            _hint.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            _hint.style.paddingLeft = _hint.style.paddingRight = 18;
            _hint.style.paddingTop = _hint.style.paddingBottom = 6;
            _hint.style.display = DisplayStyle.None;
            var hintRow = new VisualElement();
            hintRow.style.position = Position.Absolute;
            hintRow.style.left = 0;
            hintRow.style.right = 0;
            hintRow.style.bottom = RefHeight * 0.06f;
            hintRow.style.alignItems = Align.Center;
            hintRow.pickingMode = PickingMode.Ignore;
            hintRow.Add(_hint);
            _root.Add(hintRow);

            // Pause menu.
            _pauseMenu = Overlay();
            _pauseMenu.Add(Text("PAUSA", 64, Color.white));
            _pauseMenu.Add(MenuButton("Continuar", () => ResumeRequested?.Invoke()));
            _pauseMenu.Add(MenuButton("Tática", () => TacticsRequested?.Invoke()));
            _pauseMenu.Add(MenuButton("Substituições", () => SubstitutionsRequested?.Invoke()));
            _restartButton = MenuButton("Recomeçar partida", () => RestartRequested?.Invoke());
            _pauseMenu.Add(_restartButton);
            _root.Add(_pauseMenu);

            // Final screen.
            _final = Overlay();
            _final.Add(Text("FIM DE JOGO", 64, Color.white));
            _finalScore = Text("", 52, Color.white);
            _final.Add(_finalScore);
            _finalStats = Text("", 28, Color.white);
            _finalStats.style.unityTextAlign = TextAnchor.MiddleCenter;
            _finalStats.style.marginTop = 16;
            _finalStats.style.marginBottom = 24;
            _final.Add(_finalStats);
            _finalButton = MenuButton("Jogar de novo", () => RestartRequested?.Invoke());
            _final.Add(_finalButton);
            _root.Add(_final);
        }

        /// <summary>Shows the frame rate of the last measuring window (called about once a second, not per frame).</summary>
        public void SetFps(float average, float worst) => _fps.text = $"FPS {average:0} (pior {worst:0})";

        /// <summary>Career match (C1): no restart from the pause menu; after the final whistle "Continuar" (back to the career).</summary>
        public void SetCareerMode()
        {
            _restartButton.style.display = DisplayStyle.None;
            _finalButton.text = "Continuar";
        }

        /// <summary>Adds a full-screen panel (tactics, substitutions) over the HUD and the pause menu.</summary>
        public void AddOverlay(VisualElement panel) => _root.Add(panel);

        public void ShowPause(bool on) => _pauseMenu.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Scoreboard, clock, banner and radar for this frame.</summary>
        public void UpdateLive(MatchSession session)
        {
            var m = session.Match;
            if (m.Home.Goals != _shownHome || m.Away.Goals != _shownAway)
            {
                _shownHome = m.Home.Goals;
                _shownAway = m.Away.Goals;
                _score.text = $"{_shownHome} x {_shownAway}";
            }
            int minute = (int)m.ClockMinutes;
            if (minute != _shownMinute || m.Half != _shownHalf)
            {
                _shownMinute = minute;
                _shownHalf = m.Half;
                _clock.text = $"{minute:00}'  {m.Half}ºT";
            }
            UpdateBanner(session);
            UpdateHint(m);
            UpdateCards(m, session.Paused);
            if (_radarOpen) UpdateRadar(session);
        }

        private void UpdateBanner(MatchSession s)
        {
            var m = s.Match;
            float goal = s.SecondsSince(AiMatchEvent.Goal), half = s.SecondsSince(AiMatchEvent.HalfTime), outEv = s.SecondsSince(AiMatchEvent.Out);
            float foul = s.SecondsSince(AiMatchEvent.Foul), offside = s.SecondsSince(AiMatchEvent.Offside); // A8a
            float newest = Mathf.Min(Mathf.Min(goal, Mathf.Min(half, outEv)), Mathf.Min(foul, offside));
            if (newest >= _v.BannerSeconds)
            {
                if (_banner.style.display != DisplayStyle.None) _banner.style.display = DisplayStyle.None;
                _lastBannerEvent = float.PositiveInfinity;
                return;
            }
            // A new event (its age went down) rewrites the text once.
            if (newest < _lastBannerEvent)
            {
                if (newest == goal) _banner.text = $"GOL!  {_homeName} {m.Home.Goals} x {m.Away.Goals} {_awayName}";
                else if (newest == half) _banner.text = "INTERVALO";
                else if (newest == offside) _banner.text = "IMPEDIMENTO";
                else _banner.text = RestartName(m.Restart);
                _banner.style.display = DisplayStyle.Flex;
            }
            _lastBannerEvent = newest;
        }

        // What the user does at a set piece (A8b; Alto A9a): 0 none, 1 penalty, 2 direct free kick, 3 corner, 4 other
        // restarts, 5 choose the dive against a penalty.
        private void UpdateHint(AiMatch m)
        {
            int hint = 0;
            if (m.RestartReady && m.HumanTeam != null && !m.Finished)
            {
                if (m.RestartTeam == m.HumanTeam)
                    hint = m.Restart == RestartKind.Penalty ? 1 : m.Restart == RestartKind.DirectFreeKick ? 2 : m.Restart == RestartKind.Corner ? 3 : 4;
                else if (m.Restart == RestartKind.Penalty) hint = 5;
            }
            if (hint == _shownHint) return;
            _shownHint = hint;
            if (hint == 0) { _hint.style.display = DisplayStyle.None; return; }
            switch (hint)
            {
                case 1: _hint.text = "Mire com o analógico  ·  segure CHUTE para a força"; break;
                case 2: _hint.text = "Mire com o analógico  ·  CHUTE bate  ·  ALTO levanta na área  ·  PASSE toca curto"; break;
                case 3: _hint.text = "Mire a região com o analógico  ·  ALTO cruza  ·  PASSE curto"; break;
                case 4: _hint.text = "Mire com o analógico  ·  PASSE ou ENFIADA  ·  ALTO longo"; break;
                default: _hint.text = "Pênalti contra: segure o analógico para cima ou para baixo para o goleiro escolher o lado"; break;
            }
            _hint.style.display = DisplayStyle.Flex;
        }

        /// <summary>A yellow or red card shows the player's name for a few seconds (A8b); so does an AI substitution (A9c) and
        /// an opponent's injury (X-91).</summary>
        /// <param name="paused">While paused (pause menu, half-time panel) new notices wait: a notice shown behind a panel
        /// would expire unseen (the AI makes most of its substitutions at half-time).</param>
        private void UpdateCards(AiMatch m, bool paused)
        {
            var events = m.Events;
            if (_seenEvents < 0) _seenEvents = events.Count; // a resumed match does not replay old cards
            if (paused)
            {
                if (_card.style.display == DisplayStyle.Flex) _cardUntil += Time.unscaledDeltaTime; // the clock of a shown notice stops too
                return;
            }
            string subs = null;
            for (; _seenEvents < events.Count; _seenEvents++)
            {
                var e = events[_seenEvents];
                string team = e.Side == MatchSide.Home ? _homeName : _awayName;
                bool user = m.HumanTeam != null && e.Side == m.HumanTeam.Side;
                if (e.Type == MatchEventType.Injury || e.Type == MatchEventType.Substitution)
                {
                    if (user) continue; // the user's own: the injury panel told him, and he made the change himself
                    // Several at once (half-time; an injury and its forced change, X-91) are shown together.
                    string text = e.Type == MatchEventType.Injury
                        ? $"LESÃO ({team}): {_playerName(e.PlayerId.Value)}"
                        : $"SUBSTITUIÇÃO ({team}): sai {_playerName(e.PlayerId.Value)}, entra {_playerName(e.OtherPlayerId.Value)}";
                    subs = subs == null ? text : subs + "  ·  " + text;
                    _card.text = subs;
                    _card.style.backgroundColor = new Color(0.15f, 0.45f, 0.9f);
                    _card.style.color = Color.white;
                    _card.style.display = DisplayStyle.Flex;
                    _cardUntil = Time.unscaledTime + _v.BannerSeconds;
                    continue;
                }
                if (e.Type != MatchEventType.YellowCard && e.Type != MatchEventType.RedCard) continue;
                bool red = e.Type == MatchEventType.RedCard;
                _card.text = $"{(red ? "EXPULSO" : "CARTÃO AMARELO")}  {_playerName(e.PlayerId.Value)} ({team})";
                _card.style.backgroundColor = red ? new Color(0.85f, 0.12f, 0.18f) : new Color(0.98f, 0.82f, 0.1f);
                _card.style.color = red ? Color.white : Color.black;
                _card.style.display = DisplayStyle.Flex;
                _cardUntil = Time.unscaledTime + _v.BannerSeconds;
            }
            if (_card.style.display == DisplayStyle.Flex && Time.unscaledTime > _cardUntil) _card.style.display = DisplayStyle.None;
        }

        private void UpdateRadar(MatchSession s)
        {
            var m = s.Match;
            for (int i = 0; i < _dots.Length; i++) Place(_dots[i], s.PlayerPosition(i), _v.RadarDotSize);
            Place(_ballDot, s.BallPosition, _v.RadarDotSize * 0.8f);
        }

        // Simulation (x, y) → radar pixels: the user's attack to the right, the far touchline at the top.
        private void Place(VisualElement dot, System.Numerics.Vector3 p, float size)
        {
            float u = (_side * p.X + _halfLength) / (2f * _halfLength);
            float w = (_halfWidth - _side * p.Y) / (2f * _halfWidth);
            dot.style.left = u * _radarW - size * 0.5f;
            dot.style.top = w * _radarH - size * 0.5f;
        }

        /// <summary>The final screen: score, the main team statistics and the scorers.</summary>
        public void ShowFinal(MatchResult r, AiMatch m)
        {
            _finalScore.text = $"{_homeName} {r.HomeGoals} x {r.AwayGoals} {_awayName}";
            var h = r.HomeStats;
            var a = r.AwayStats;
            var scorers = new System.Text.StringBuilder();
            foreach (var e in r.Events)
            {
                if (e.Type != MatchEventType.Goal) continue;
                string who = e.PlayerId.IsNone ? "contra" : "#" + e.PlayerId.Value % 100;
                scorers.Append($"{e.Minute}' {(e.Side == MatchSide.Home ? _homeName : _awayName)} ({who})   ");
            }
            _finalStats.text =
                $"Chutes {h.Shots} x {a.Shots}\nNo alvo {h.ShotsOnTarget} x {a.ShotsOnTarget}\nPosse {h.Possession:0}% x {a.Possession:0}%\n" +
                $"Escanteios {h.Corners} x {a.Corners}\nDefesas dos goleiros {m.HomeStats.Saves} x {m.AwayStats.Saves}\n" + (scorers.Length > 0 ? scorers.ToString() : "Sem gols");
            _final.style.display = DisplayStyle.Flex;
            _pauseMenu.style.display = DisplayStyle.None;
        }

        public void HideFinal() => _final.style.display = DisplayStyle.None;

        private void ToggleRadar()
        {
            _radarOpen = !_radarOpen;
            _radarField.style.display = _radarOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string RestartName(RestartKind kind)
        {
            switch (kind)
            {
                case RestartKind.ThrowIn: return "LATERAL";
                case RestartKind.GoalKick: return "TIRO DE META";
                case RestartKind.Corner: return "ESCANTEIO";
                case RestartKind.Kickoff: return "SAÍDA DE BOLA";
                case RestartKind.FreeKick: return "FALTA INDIRETA";
                case RestartKind.DirectFreeKick: return "FALTA";
                case RestartKind.Penalty: return "PÊNALTI";
                default: return "";
            }
        }

        // ---- element helpers ----

        private static Label Text(string text, int size, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        private static VisualElement Box(Color background)
        {
            var e = new VisualElement();
            e.style.backgroundColor = background;
            e.style.paddingLeft = e.style.paddingRight = 18;
            e.style.paddingTop = e.style.paddingBottom = 8;
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        private static VisualElement Dot(Color color, float size)
        {
            var d = new VisualElement();
            d.style.position = Position.Absolute;
            d.style.width = size;
            d.style.height = size;
            d.style.backgroundColor = color;
            d.style.borderTopLeftRadius = d.style.borderTopRightRadius = d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = size * 0.5f;
            d.pickingMode = PickingMode.Ignore;
            return d;
        }

        private static VisualElement Overlay()
        {
            var o = new VisualElement();
            o.style.position = Position.Absolute;
            o.style.left = o.style.top = o.style.right = o.style.bottom = 0;
            o.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
            o.style.alignItems = Align.Center;
            o.style.justifyContent = Justify.Center;
            o.style.display = DisplayStyle.None;
            return o;
        }

        private static Button MenuButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.width = 520;
            b.style.height = 90;
            b.style.fontSize = 36;
            b.style.marginTop = 14;
            return b;
        }
    }
}
