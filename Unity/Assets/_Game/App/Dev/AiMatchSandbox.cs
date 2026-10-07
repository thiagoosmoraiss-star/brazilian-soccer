using System.Linq;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using Game.Match.AI;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.App.Dev
{
    /// <summary>
    /// A4 dev sandbox (ROADMAP A4: "11×11 headless e renderizado com movimento coerente"): watch both AI teams play
    /// the headless <see cref="AiMatch"/>. Keys 1 / 2 / 4 set the speed, P pauses, R restarts. Attach to an empty
    /// GameObject in an empty scene (Editor only; not part of any shipped scene).
    /// </summary>
    public sealed class AiMatchSandbox : MonoBehaviour
    {
        private const float FixedDt = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz
        private const float PlayerVisualHalfHeight = 0.9f; // primitive capsule: local half-height 1, Y scale 0.9
        private const int MaxStepsPerFrame = 40; // keeps a slow frame at 4x from spiralling

        [Header("Teams (uniform attributes, 1-99)")]
        public int HomeAttributes = 70;
        public int AwayAttributes = 70;
        public string Formation = "4-4-2";
        public int DurationMinutes = 6;
        public ulong Seed = 1;

        private GameDatabase _db;
        private AiMatch _match;
        private GameObject[] _players;
        private GameObject _ball;
        private CinemachineCamera _vcam;
        private float _accumulator;
        private float _speed = 1f;
        private bool _paused;
        private string _last = "";
        private string _error;

        private void Start()
        {
            var source = UnityDataRoot.Resolve();
            if (!source.IsSuccess) { Fail(source.ToString()); return; }
            var db = GameDataLoader.Load(source.Value);
            if (!db.IsSuccess) { Fail(db.ToString()); return; }
            _db = db.Value;
            if (_db.Formation(Formation) == null) { Fail("unknown formation " + Formation); return; }

            var pitch = Pitch.From(_db.Ball.Pitch);
            SandboxPitch.Build(pitch, "Pitch (A4 sandbox)");

            _players = new GameObject[2 * MatchTeamSetup.StarterCount];
            for (int i = 0; i < _players.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.localScale = new Vector3(_db.Movement.PlayerRadius * 2f, PlayerVisualHalfHeight, _db.Movement.PlayerRadius * 2f);
                Destroy(go.GetComponent<Collider>());
                _players[i] = go;
            }
            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ball.name = "Ball (A4 sandbox)";
            _ball.transform.localScale = Vector3.one * (_db.Ball.Ball.Radius * 2f) * 2f; // drawn twice the size so it reads from the broadcast height
            Destroy(_ball.GetComponent<Collider>());
            _ball.GetComponent<Renderer>().material.color = Color.yellow;

            SetupLightAndCamera();
            NewMatch();
        }

        private void NewMatch()
        {
            var tactic = new TacticSetup(Formation, 3, 2, 2);
            MatchTeamSetup Team(int club, int attrs) => new MatchTeamSetup(new Id(club), tactic,
                Enumerable.Range(0, MatchTeamSetup.StarterCount)
                    .Select(i => new MatchPlayerSetup(new Id(club * 100 + i), Enumerable.Repeat(attrs, AttrInfo.Count).ToArray(), 0,
                        System.Array.Empty<int>(), 100f, 3, null)).ToArray(),
                System.Array.Empty<MatchPlayerSetup>());
            var setup = new MatchSetup(Team(1, HomeAttributes), Team(2, AwayAttributes), false, DurationMinutes, 5, Seed);
            _match = new AiMatch(_db, setup, FixedDt);
            _accumulator = 0f;
            _last = "";
            for (int i = 0; i < _players.Length; i++)
            {
                var p = _match.Players[i];
                bool home = p.Team == _match.Home;
                _players[i].name = (home ? "Home " : "Away ") + p.Slot.Position + " " + (p.Local + 1);
                var color = home ? new Color(0.15f, 0.3f, 0.95f) : new Color(0.9f, 0.15f, 0.15f);
                _players[i].GetComponent<Renderer>().material.color = p.IsGoalkeeper ? color * 0.45f : color;
            }
        }

        private void Fail(string error)
        {
            _error = error;
            Debug.LogError("AiMatchSandbox: " + error);
        }

        private void Update()
        {
            if (_match == null) return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) _speed = 1f;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) _speed = 2f;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4)) _speed = 4f;
            if (UnityEngine.Input.GetKeyDown(KeyCode.P)) _paused = !_paused;
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) { Seed++; NewMatch(); }

            if (!_paused && !_match.Finished)
            {
                _accumulator += Time.deltaTime * _speed;
                int steps = 0;
                while (_accumulator >= FixedDt && steps < MaxStepsPerFrame)
                {
                    Report(_match.Step());
                    _accumulator -= FixedDt;
                    steps++;
                }
                if (steps == MaxStepsPerFrame) _accumulator = 0f;
            }

            for (int i = 0; i < _players.Length; i++)
                _players[i].transform.position = ToUnity(_match.Players[i].Body.Position) + Vector3.up * PlayerVisualHalfHeight;
            _ball.transform.position = ToUnity(_match.Ball.Position) + Vector3.up * _ball.transform.localScale.y * 0.5f;
        }

        private void Report(AiMatchEvent ev)
        {
            switch (ev)
            {
                case AiMatchEvent.Goal: _last = $"GOL! {_match.Home.Goals} x {_match.Away.Goals}"; break;
                case AiMatchEvent.Shot: _last = "Chute"; break;
                case AiMatchEvent.Out: _last = "Saiu (reinício provisório)"; break;
                case AiMatchEvent.Finished: _last = "Fim de jogo"; break;
            }
        }

        private void OnGUI()
        {
            if (_error != null) { GUI.Label(new Rect(10, 10, 800, 60), "AiMatchSandbox error: " + _error); return; }
            if (_match == null) return;
            int seconds = (int)_match.ElapsedSeconds;
            GUI.Label(new Rect(10, 10, 760, 120),
                $"A4 sandbox - Casa (azul) {_match.Home.Goals} x {_match.Away.Goals} Visitante (vermelho)   {seconds / 60:00}:{seconds % 60:00} / {DurationMinutes:00}:00\n" +
                $"Fase casa: {_match.Home.Phase}   Fase visitante: {_match.Away.Phase}   Velocidade {_speed:0}x{(_paused ? " (pausado)" : "")}\n" +
                "Teclas: 1 / 2 / 4 velocidade, P pausa, R nova partida (semente seguinte)\n" + _last);
        }

        private void SetupLightAndCamera()
        {
            var light = new GameObject("Sandbox Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var brainCamera = Camera.main;
            if (brainCamera == null) brainCamera = new GameObject("Main Camera").AddComponent<Camera>();
            if (brainCamera.GetComponent<CinemachineBrain>() == null) brainCamera.gameObject.AddComponent<CinemachineBrain>();

            var vcamGo = new GameObject("Sandbox Broadcast VCam");
            vcamGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f); // lateral elevada ~30-45°
            _vcam = vcamGo.AddComponent<CinemachineCamera>();
            _vcam.Follow = _ball.transform;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 34f, -32f);
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Z, v.Y);
    }
}
