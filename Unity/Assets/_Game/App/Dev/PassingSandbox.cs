using System.Linq;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Input;
using Game.Match;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.App.Dev
{
    /// <summary>
    /// A3 dev sandbox (ROADMAP A3: "2–3 jogadores trocando passes e chutando a gol vazio"). Three teammates with no
    /// AI; the controlled one is yellow. Touch: left side = stick, right-hand buttons = CHUTE / PASSE / ENFIADA /
    /// SPRINT. Editor: mouse on the same buttons, or WASD + Shift + J (passe) + K (enfiada) + Space (chute); R resets
    /// the ball. Attach to an empty GameObject in an empty scene (Editor only; not part of any shipped scene).
    /// </summary>
    public sealed class PassingSandbox : MonoBehaviour
    {
        private const float FixedDt = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz
        private const float PlayerVisualHalfHeight = 0.9f; // primitive capsule: local half-height 1, Y scale 0.9
        private const float MessageSeconds = 1.5f;
        private const float LineWidth = 0.12f;

        [Header("Players (uniform attributes, 1-99)")]
        public int Ovr = 70;
        public bool LeftFooted;
        [Range(1, 5)] public int WeakFoot = 3;
        public ulong Seed = 1;

        private PracticeSession _session;
        private InputAdapter _input;
        private GameObject[] _playerVisuals;
        private Renderer[] _playerRenderers;
        private GameObject _ballVisual;
        private CinemachineCamera _vcam;
        private int _followed = -1;
        private float _accumulator;
        private string _message = "";
        private float _messageTimer;
        private bool _autoReset;
        private ActionCommand _pendingCommand = ActionCommand.None;
        private string _error;

        private void Start()
        {
            var sourceResult = UnityDataRoot.Resolve();
            if (!sourceResult.IsSuccess) { Fail(sourceResult.ToString()); return; }
            var dbResult = GameDataLoader.Load(sourceResult.Value);
            if (!dbResult.IsSuccess) { Fail(dbResult.ToString()); return; }
            var db = dbResult.Value;

            var pitch = Pitch.From(db.Ball.Pitch);
            var players = new MatchPlayerSetup[3];
            for (int i = 0; i < players.Length; i++)
                players[i] = new MatchPlayerSetup(new Id(i + 1), Enumerable.Repeat(Ovr, AttrInfo.Count).ToArray(), 0, System.Array.Empty<int>(),
                    100f, 3, null, LeftFooted, WeakFoot);
            var starts = new[]
            {
                new System.Numerics.Vector3(5f, 0f, 0f),
                new System.Numerics.Vector3(22f, 12f, 0f),
                new System.Numerics.Vector3(26f, -10f, 0f),
            };
            _session = new PracticeSession(db.Balance, pitch, db.Ball.Ball, db.Movement, db.Fatigue, db.Kicking, players, starts, Seed);

            _input = gameObject.AddComponent<InputAdapter>();
            _input.Configure(new KickTimings(db.Kicking.Common.TapMaxSeconds, db.Kicking.Pass.PowerBarSeconds, db.Kicking.Shot.PowerBarSeconds),
                db.Movement.SprintMemorySeconds);

            BuildPitch(pitch);
            _playerVisuals = new GameObject[players.Length];
            _playerRenderers = new Renderer[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Player " + (i + 1) + " (A3 sandbox)";
                go.transform.localScale = new Vector3(db.Movement.PlayerRadius * 2f, PlayerVisualHalfHeight, db.Movement.PlayerRadius * 2f);
                Destroy(go.GetComponent<Collider>());
                _playerVisuals[i] = go;
                _playerRenderers[i] = go.GetComponent<Renderer>();
            }
            _ballVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ballVisual.name = "Ball (A3 sandbox)";
            _ballVisual.transform.localScale = Vector3.one * (db.Ball.Ball.Radius * 2f);
            Destroy(_ballVisual.GetComponent<Collider>());
            _ballVisual.GetComponent<Renderer>().material.color = Color.red;

            SetupLightAndCamera();
        }

        private void Fail(string error)
        {
            _error = error;
            Debug.LogError("PassingSandbox: " + error);
        }

        private void Update()
        {
            if (_session == null) return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) ResetBall();

            var move = _input.MoveIntent;
            bool sprint = _input.SprintHeld;
            var released = _input.TakeCommand();
            if (released.Kind != ActionKind.None) _pendingCommand = released; // kept until the next fixed step runs

            _accumulator += Time.deltaTime;
            while (_accumulator >= FixedDt)
            {
                var ev = _session.Step(move, sprint, _pendingCommand, move, FixedDt);
                _pendingCommand = ActionCommand.None;
                Report(ev);
                _accumulator -= FixedDt;
            }

            if (_messageTimer > 0f)
            {
                _messageTimer -= Time.deltaTime;
                if (_messageTimer <= 0f && _autoReset) ResetBall();
            }

            for (int i = 0; i < _playerVisuals.Length; i++)
            {
                _playerVisuals[i].transform.position = ToUnity(_session.Bodies[i].Position) + Vector3.up * PlayerVisualHalfHeight;
                _playerRenderers[i].material.color = i == _session.Control.Controlled ? Color.yellow : Color.blue;
            }
            _ballVisual.transform.position = ToUnity(_session.Ball.Position) + Vector3.up * _ballVisual.transform.localScale.y * 0.5f;

            if (_followed != _session.Control.Controlled)
            {
                _followed = _session.Control.Controlled;
                _vcam.Follow = _playerVisuals[_followed].transform;
            }
        }

        private void Report(PracticeEvent ev)
        {
            switch (ev)
            {
                case PracticeEvent.Goal: Show("GOL!", true); break;
                case PracticeEvent.Out: Show("Fora", true); break;
                case PracticeEvent.PostHit: Show("Trave!", false); break;
                case PracticeEvent.Pass:
                    Show(_session.LastPass.Target >= 0 ? $"Passe para o {_session.LastPass.Target + 1}" : "Passe no espaço", false);
                    break;
                case PracticeEvent.Shot:
                    Show($"Chute ({_session.LastShot.Speed:F0} m/s{(_session.LastShot.WeakFoot ? ", pé ruim" : "")})", false);
                    break;
            }
        }

        private void Show(string message, bool autoReset)
        {
            _message = message;
            _messageTimer = MessageSeconds;
            _autoReset = autoReset;
        }

        private void ResetBall()
        {
            _autoReset = false;
            _session.PlaceBallAtFeet(_session.Control.Controlled);
        }

        private void OnGUI()
        {
            if (_error != null) { GUI.Label(new Rect(10, 10, 800, 60), "PassingSandbox error: " + _error); return; }
            if (_session == null) return;
            var b = _session.Bodies[_session.Control.Controlled];
            GUI.Label(new Rect(10, 10, 640, 140),
                "A3 sandbox - toque: esquerda = analogico, direita = botoes\n" +
                "Editor: WASD/mouse, Shift sprint, J passe, K enfiada, Space chute (segure p/ forca), R reseta\n" +
                $"Controlado: {_session.Control.Controlled + 1}  HasBall={_session.Ball.Owner == _session.Control.Controlled}  Energia={b.Energy:F0}\n" +
                $"Ultimo passe: alvo={(_session.LastPass.Target >= 0 ? (_session.LastPass.Target + 1).ToString() : "espaco")} erro={_session.LastPass.AngleErrorDegrees:F1} graus" +
                (_session.LastKickFirstTime ? " (de primeira)" : ""));
            if (_messageTimer > 0f)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.06f), alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, Screen.height * 0.1f), _message, style);
            }
        }

        /// <summary>Visible field: grass plane, white lines (touchlines, goal lines, halfway, penalty areas) and both goals.</summary>
        private static void BuildPitch(Pitch p)
        {
            var root = new GameObject("Pitch (A3 sandbox)").transform;

            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Grass";
            grass.transform.SetParent(root);
            grass.transform.localScale = new Vector3((p.Length + 10f) / 10f, 1f, (p.Width + 10f) / 10f); // Unity plane = 10×10 units
            grass.GetComponent<Renderer>().material.color = new Color(0.2f, 0.55f, 0.2f);
            Destroy(grass.GetComponent<Collider>());

            float hl = p.HalfLength, hw = p.HalfWidth;
            Line(root, -hl, -hw, hl, -hw);
            Line(root, -hl, hw, hl, hw);
            Line(root, -hl, -hw, -hl, hw);
            Line(root, hl, -hw, hl, hw);
            Line(root, 0f, -hw, 0f, hw);
            foreach (float sign in new[] { 1f, -1f })
            {
                float goalX = sign * hl, boxX = sign * (hl - p.PenaltyAreaDepth), bw = p.PenaltyAreaWidth * 0.5f;
                Line(root, boxX, -bw, boxX, bw);
                Line(root, boxX, -bw, goalX, -bw);
                Line(root, boxX, bw, goalX, bw);
                Bar(root, new Vector3(goalX, p.GoalHeight * 0.5f, -p.HalfGoalWidth), new Vector3(p.PostRadius * 2f, p.GoalHeight * 0.5f, p.PostRadius * 2f), false);
                Bar(root, new Vector3(goalX, p.GoalHeight * 0.5f, p.HalfGoalWidth), new Vector3(p.PostRadius * 2f, p.GoalHeight * 0.5f, p.PostRadius * 2f), false);
                Bar(root, new Vector3(goalX, p.GoalHeight, 0f), new Vector3(p.PostRadius * 2f, p.HalfGoalWidth, p.PostRadius * 2f), true);
            }
        }

        // Simulation (x, y) on the ground → a thin white strip in Unity (x, z).
        private static void Line(Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Line";
            go.transform.SetParent(parent);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = new Vector3((x0 + x1) * 0.5f, 0.01f, (y0 + y1) * 0.5f);
            go.transform.localScale = new Vector3(Mathf.Max(LineWidth, Mathf.Abs(x1 - x0)), 0.01f, Mathf.Max(LineWidth, Mathf.Abs(y1 - y0)));
            go.GetComponent<Renderer>().material.color = Color.white;
        }

        private static void Bar(Transform parent, Vector3 center, Vector3 cylinderScale, bool crossbar)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // unit cylinder: height 2 along Y
            go.name = crossbar ? "Crossbar" : "Post";
            go.transform.SetParent(parent);
            Destroy(go.GetComponent<Collider>());
            go.transform.position = center;
            if (crossbar) go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lay it along Unity z (= simulation y)
            go.transform.localScale = cylinderScale;
            go.GetComponent<Renderer>().material.color = Color.white;
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
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 20f, -18f);
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Z, v.Y);
    }
}
