using Game.Data.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App
{
    /// <summary>
    /// Boot scene (C1, C3a; TECHNICAL_SPEC §15 "Boot | Splash, GameDatabase, configurações, saves"): loads the validated
    /// data and texts from <c>Data/</c>, opens the career session (save + settings), and goes to the Shell — or straight
    /// back into the Match scene when the app was closed mid-match ("retomar partida após fechar o app").
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        public const string ShellScene = "Shell";
        public const string MatchScene = "Match";

        private string _error;

        private void Start()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            var source = UnityDataRoot.Resolve();
            if (!source.IsSuccess) { Fail(source.ToString()); return; }
            var db = GameDataLoader.Load(source.Value);
            if (!db.IsSuccess) { Fail(db.ToString()); return; }
            var strings = GameDataLoader.LoadStrings(source.Value);
            if (!strings.IsSuccess) { Fail(strings.ToString()); return; }
            var opened = CareerContext.Open(db.Value, strings.Value);
            if (!opened.IsSuccess) { Fail(opened.ToString()); return; }
            var session = CareerContext.Session;
            var pending = session.Pending;
            bool resumeMatch = session.HasCareer && session.Career.State.MatchInProgress != null && pending != null
                               && pending.Fixture.Id == session.Career.State.MatchInProgress.FixtureId;
            CareerContext.ReturningFromMatch = false;
            SceneManager.LoadScene(resumeMatch ? MatchScene : ShellScene);
        }

        private void Fail(string error)
        {
            _error = error;
            Debug.LogError("BootLoader: " + error);
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, Screen.width - 20, Screen.height - 20), _error == null ? "Carregando..." : "Erro ao iniciar: " + _error);
        }
    }
}
