using System.Linq;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Input;
using Game.Match;
using Game.Match.AI;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.App.Dev
{
    /// <summary>
    /// A5 dev sandbox (ROADMAP A5: "defender e recuperar a bola no aparelho"): the user plays the home side (blue) of an
    /// 11×11 against the AI. Attack as in A3 (PASSE / ENFIADA / CHUTE); without the ball the buttons become TROCAR (tap)
    /// and CONTENÇÃO (hold); standing tackles are automatic. Yellow = controlled player, white disc = the next one
    /// (the ring). Editor: WASD/mouse, Shift, J, K, Space; R restarts. Editor-only dev scene, not shipped.
    /// </summary>
    public sealed class PlayableMatchSandbox : MonoBehaviour
    {
        private const float FixedDt = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz
        private const float PlayerVisualHalfHeight = 0.9f; // primitive capsule: local half-height 1, Y scale 0.9
        private const int MaxStepsPerFrame = 10;

        [Header("Teams (uniform attributes, 1-99)")]
        public int HomeAttributes = 70;
        public int AwayAttributes = 70;
        public string Formation = "4-4-2";
        public int DurationMinutes = 6;
        public ulong Seed = 1;

        private GameDatabase _db;
        private AiMatch _match;
        private InputAdapter _input;
        private GameObject[] _players;
        private Renderer[] _renderers;
        private GameObject _ball;
        private GameObject _ring;
        private float _accumulator;
        private ActionCommand _pendingAction = ActionCommand.None;
        private bool _pendingSwitch;
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

            SandboxPitch.Build(Pitch.From(_db.Ball.Pitch), "Pitch (A5 sandbox)");

            _input = gameObject.AddComponent<InputAdapter>();
            _input.Configure(new KickTimings(_db.Kicking.Common.TapMaxSeconds, _db.Kicking.Pass.PowerBarSeconds, _db.Kicking.Shot.PowerBarSeconds),
                _db.Movement.SprintMemorySeconds);

            _players = new GameObject[2 * MatchTeamSetup.StarterCount];
            _renderers = new Renderer[_players.Length];
            for (int i = 0; i < _players.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.localScale = new Vector3(_db.Movement.PlayerRadius * 2f, PlayerVisualHalfHeight, _db.Movement.PlayerRadius * 2f);
                Destroy(go.GetComponent<Collider>());
                _players[i] = go;
                _renderers[i] = go.GetComponent<Renderer>();
            }
            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ball.name = "Ball (A5 sandbox)";
            _ball.transform.localScale = Vector3.one * (_db.Ball.Ball.Radius * 2f) * 2f; // drawn twice the size to read from the broadcast height
            Destroy(_ball.GetComponent<Collider>());
            _ball.GetComponent<Renderer>().material.color = Color.white;

            _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ring.name = "Next player ring";
            _ring.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
            Destroy(_ring.GetComponent<Collider>());
            _ring.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, 0.8f);

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
            _match.EnableHuman(MatchSide.Home);
            _accumulator = 0f;
            _last = "";
            for (int i = 0; i < _players.Length; i++)
            {
                var p = _match.Players[i];
                _players[i].name = (p.Team == _match.Home ? "Home " : "Away ") + p.Slot.Position + " " + (p.Local + 1);
            }
        }

        private void Fail(string error)
        {
            _error = error;
            Debug.LogError("PlayableMatchSandbox: " + error);
        }

        private void Update()
        {
            if (_match == null) return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) { Seed++; NewMatch(); }

            _input.DefenseMode = !_match.Home.HasPossession;
            var released = _input.TakeCommand();
            if (released.Kind != ActionKind.None) _pendingAction = released; // kept until the next fixed step runs
            if (_input.TakeSwitch()) _pendingSwitch = true;

            if (!_match.Finished)
            {
                _accumulator += Time.deltaTime;
                int steps = 0;
                while (_accumulator >= FixedDt && steps < MaxStepsPerFrame)
                {
                    var input = new HumanInput(_input.MoveIntent, _input.SprintHeld, _pendingAction, _input.ContainHeld, _pendingSwitch);
                    _pendingAction = ActionCommand.None;
                    _pendingSwitch = false;
                    Report(_match.Step(input));
                    _accumulator -= FixedDt;
                    steps++;
                }
                if (steps == MaxStepsPerFrame) _accumulator = 0f;
            }

            var controlled = _match.Controlled;
            var next = _match.NextCandidate;
            for (int i = 0; i < _players.Length; i++)
            {
                var p = _match.Players[i];
                _players[i].transform.position = ToUnity(p.Body.Position) + Vector3.up * PlayerVisualHalfHeight;
                var color = p.Team == _match.Home ? new Color(0.15f, 0.3f, 0.95f) : new Color(0.9f, 0.15f, 0.15f);
                if (p.IsGoalkeeper) color *= 0.45f;
                if (p == controlled) color = Color.yellow;
                _renderers[i].material.color = color;
            }
            _ring.SetActive(next != null && _input.DefenseMode);
            if (next != null) _ring.transform.position = ToUnity(next.Body.Position) + Vector3.up * 0.02f;
            _ball.transform.position = ToUnity(_match.Ball.Position) + Vector3.up * _ball.transform.localScale.y * 0.5f;
        }

        private void Report(AiMatchEvent ev)
        {
            switch (ev)
            {
                case AiMatchEvent.Goal: _last = $"GOL! {_match.Home.Goals} x {_match.Away.Goals}"; break;
                case AiMatchEvent.Tackle: _last = _match.Home.HasPossession ? "Desarme! Bola recuperada" : "Desarmado"; break;
                case AiMatchEvent.Out: _last = "Saiu (reinício provisório)"; break;
                case AiMatchEvent.Finished: _last = "Fim de jogo"; break;
            }
        }

        private void OnGUI()
        {
            if (_error != null) { GUI.Label(new Rect(10, 10, 800, 60), "PlayableMatchSandbox error: " + _error); return; }
            if (_match == null) return;
            int seconds = (int)_match.ElapsedSeconds;
            GUI.Label(new Rect(10, 10, 820, 120),
                $"A5 sandbox - Você (azul) {_match.Home.Goals} x {_match.Away.Goals} IA (vermelho)   {seconds / 60:00}:{seconds % 60:00} / {DurationMinutes:00}:00\n" +
                (_input.DefenseMode ? "DEFENDENDO: segure CONTENÇÃO (K), toque TROCAR (J); o desarme é automático" : "ATACANDO: PASSE (J), ENFIADA (K), CHUTE (Espaço)") + "\n" +
                "WASD/mouse move, Shift sprint, R nova partida\n" + _last);
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
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = _ball.transform;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 26f, -24f);
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Z, v.Y);
    }
}
