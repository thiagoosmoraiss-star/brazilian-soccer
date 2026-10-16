using System.Text;
using Game.Career.Season;
using Game.Data.Competitions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.App
{
    /// <summary>
    /// Shell scene, placeholder (C1; TECHNICAL_SPEC §15 "Shell | todas as telas de menu e carreira, retrato"; the real
    /// screens are C3): the managed club, its next match with <b>Jogar</b> (loads the Match scene) and <b>Simular</b> (QuickSim,
    /// stays here), <b>Avançar</b> to the next match, the last result and the division table. UI Toolkit built in code.
    /// </summary>
    public sealed class ShellScreen : MonoBehaviour
    {
        private const float RefWidth = 1080f, RefHeight = 1920f;

        private Label _club, _date, _next, _last, _table;
        private Button _play, _simulate, _advance;

        private void Start()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            if (!CareerContext.Ready) { SceneManager.LoadScene("Boot"); return; }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("AcessoRuntimeTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int((int)RefWidth, (int)RefHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            var go = new GameObject("Shell UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            go.SetActive(true);

            // The content scrolls instead of shrinking (a short landscape Game view squeezed the buttons on top of each other).
            doc.rootVisualElement.style.backgroundColor = new Color(0.08f, 0.12f, 0.1f);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            doc.rootVisualElement.Add(scroll);
            var root = scroll.contentContainer;
            root.style.paddingLeft = root.style.paddingRight = 40;
            root.style.paddingTop = 60;
            root.style.paddingBottom = 60;

            _club = MakeText(root, 56, true);
            _date = MakeText(root, 34, false);
            _next = MakeText(root, 40, true);
            _next.style.marginTop = 40;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 20;
            row.style.flexShrink = 0;
            _play = MakeButton(row, "Jogar", () => SceneManager.LoadScene(BootLoader.MatchScene));
            _simulate = MakeButton(row, "Simular", () => { CareerContext.SimulateMatch(); Refresh(); });
            root.Add(row);
            _advance = MakeButton(root, "Avançar até a próxima partida", () => { CareerContext.Career.AdvanceToUserMatch(); CareerContext.Save(); Refresh(); });
            _last = MakeText(root, 34, false);
            _last.style.marginTop = 30;
            _table = MakeText(root, 30, false);
            _table.style.marginTop = 30;
            var bottom = new VisualElement();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.marginTop = 30;
            bottom.style.flexShrink = 0;
            MakeButton(bottom, "Nova carreira", () => { CareerContext.NewCareer((ulong)System.DateTime.UtcNow.Ticks); Refresh(); });
            root.Add(bottom);
            Refresh();
        }

        private void Refresh()
        {
            var career = CareerContext.Career;
            var state = career.State;
            var club = CareerContext.ManagedClub;
            var season = state.Season;
            _club.text = $"{club.Name} — {club.DivisionName}";
            var date = season.NextEntry < season.Calendar.Count ? season.Calendar[season.NextEntry].Date : season.Calendar[season.Calendar.Count - 1].Date;
            _date.text = $"Temporada {season.Year}  ·  {date:dd/MM/yyyy}";

            var pending = career.PendingUserMatch;
            if (pending != null)
            {
                var f = pending.Fixture;
                string competition = f.Kind == CompetitionKind.Cup ? $"Copa, fase {f.Round + 1}" : $"Liga, rodada {f.Round + 1}";
                _next.text = $"Próxima partida ({competition}):\n{CareerContext.ClubName(f.HomeClubId)} x {CareerContext.ClubName(f.AwayClubId)}";
            }
            else _next.text = "Nenhuma partida agora. Avance o calendário.";
            // Only the buttons that apply now are shown: Jogar/Simular with a pending match, Avançar without one.
            _play.parent.style.display = pending != null ? DisplayStyle.Flex : DisplayStyle.None;
            _advance.style.display = pending == null ? DisplayStyle.Flex : DisplayStyle.None;

            var last = CareerContext.LastResult;
            if (state.History.Count > 0 && state.History[state.History.Count - 1].Year == season.Year - 1 && last == null)
                last = $"Temporada {season.Year - 1} encerrada.";
            _last.text = last == null ? "" : "Último resultado: " + last;
            _table.text = Table(season, club.DivisionIndex);
        }

        private static string Table(Season season, int division)
        {
            LeagueEdition league = null;
            foreach (var l in season.Leagues) if (l.DivisionIndex == division) league = l;
            if (league == null) return "";
            var sb = new StringBuilder();
            sb.AppendLine($"{league.DivisionName}");
            sb.AppendLine(" #  Clube                      J   SG  Pts");
            var rows = Standings.Table(CareerContext.Db.Competitions.League, league);
            var managed = CareerContext.Career.State.ManagedClubId.Value;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                string name = CareerContext.ClubName(r.ClubId);
                if (name.Length > 24) name = name.Substring(0, 24);
                sb.AppendLine($"{(r.ClubId == managed ? ">" : " ")}{i + 1,2} {name,-24} {r.Played,3} {r.GoalDifference,4} {r.Points,4}");
            }
            return sb.ToString();
        }

        private static Label MakeText(VisualElement parent, int size, bool bold)
        {
            var l = new Label();
            l.style.fontSize = size;
            l.style.color = Color.white;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.flexShrink = 0;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            parent.Add(l);
            return l;
        }

        private static Button MakeButton(VisualElement parent, string text, System.Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.height = 110;
            b.style.fontSize = 40;
            b.style.flexGrow = 1;
            b.style.flexShrink = 0;
            b.style.marginRight = 12;
            b.style.marginTop = 12;
            parent.Add(b);
            return b;
        }
    }
}
