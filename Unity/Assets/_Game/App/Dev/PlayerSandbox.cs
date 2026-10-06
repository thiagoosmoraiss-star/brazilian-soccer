using System;
using System.Linq;
using Game.Core.Contracts.Match;
using Game.Core.Diagnostics;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Input;
using Game.Match;
using Game.Presentation;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.App.Dev
{
    /// <summary>
    /// A2 dev sandbox (TECHNICAL_SPEC §15 "Dev: sandboxes"): drag the left mouse button to steer the floating
    /// stick, hold Left Shift to sprint. Shows the player (blue capsule) dribbling the ball (red sphere) from
    /// A1, with a Cinemachine broadcast-style camera (GAME_DESIGN §16, D-10/X-48) following it. Attach to an
    /// empty GameObject in an empty scene (Editor only; not part of any shipped scene).
    /// </summary>
    public sealed class PlayerSandbox : MonoBehaviour
    {
        private const float FixedDt = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz

        [Header("Player attributes (uniform, 1-99)")]
        public int Ovr = 70;

        private Pitch _pitch;
        private Data.Match.BallParameters _ballCfg;
        private Data.Match.MovementDefinition _mv;
        private Data.Match.FatigueDefinition _fatigue;
        private Game.Data.Effects.Balance _balance;
        private MatchPlayerSetup _player;

        private PlayerBody _body;
        private Ball _ball;
        private InputAdapter _input;

        private GameObject _playerVisual;
        private GameObject _ballVisual;
        private IDebugDraw _draw;
        private float _accumulator;
        private string _error;

        private void Start()
        {
            var sourceResult = UnityDataRoot.Resolve();
            if (!sourceResult.IsSuccess) { _error = sourceResult.ToString(); Debug.LogError("PlayerSandbox: " + _error); return; }
            var dbResult = GameDataLoader.Load(sourceResult.Value);
            if (!dbResult.IsSuccess) { _error = dbResult.ToString(); Debug.LogError("PlayerSandbox: " + _error); return; }
            var db = dbResult.Value;

            _pitch = new Pitch(db.Ball.Pitch.Length, db.Ball.Pitch.Width, db.Ball.Pitch.GoalWidth, db.Ball.Pitch.GoalHeight, db.Ball.Pitch.PostRadius);
            _ballCfg = db.Ball.Ball;
            _mv = db.Movement;
            _fatigue = db.Fatigue;
            _balance = db.Balance;
            _player = new MatchPlayerSetup(new Id(1), Enumerable.Repeat(Ovr, AttrInfo.Count).ToArray(), 0, Array.Empty<int>(), 100f, 3, null);

            _body = new PlayerBody { Position = new System.Numerics.Vector3(0f, 0f, 0f) };
            _ball = new Ball { Position = new System.Numerics.Vector3(2f, 0f, 0f), State = BallState.Dead };
            _draw = new UnityDebugDraw();

            _input = gameObject.AddComponent<InputAdapter>();

            _playerVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _playerVisual.name = "Player (A2 sandbox)";
            _playerVisual.transform.localScale = new Vector3(_mv.PlayerRadius * 2f, 0.9f, _mv.PlayerRadius * 2f);
            Destroy(_playerVisual.GetComponent<Collider>());
            _playerVisual.GetComponent<Renderer>().material.color = Color.blue;

            _ballVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ballVisual.name = "Ball (A2 sandbox)";
            _ballVisual.transform.localScale = Vector3.one * (_ballCfg.Radius * 2f);
            Destroy(_ballVisual.GetComponent<Collider>());
            _ballVisual.GetComponent<Renderer>().material.color = Color.red;

            SetupLightAndCamera();
        }

        /// <summary>A brand-new empty scene has neither light nor a camera actually pointed at the action;
        /// the Cinemachine Brain goes on the real camera, a separate CinemachineCamera (virtual camera, X-48)
        /// follows the player with a fixed broadcast-style offset (GAME_DESIGN §16).</summary>
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
            vcam.Follow = _playerVisual.transform;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 16f, -14f);
        }

        private void Update()
        {
            if (_body == null) return;
            var move = _input.MoveIntent;
            bool sprint = _input.SprintHeld;

            _accumulator += Time.deltaTime;
            while (_accumulator >= FixedDt)
            {
                bool hasBall = _ball.State == BallState.Controlled;
                bool longTouch = DribbleSystem.IsLongTouch(_body);
                Movement.Step(_body, _balance, _player, move, sprint, hasBall, longTouch, _mv, _fatigue, FixedDt);
                Fatigue.Drain(_body, _balance, _player, sprint, _fatigue, 6f, FixedDt);
                Possession.Step(_body, _ball, _mv);
                DribbleSystem.Step(_body, _ball, _balance, _player, _mv);
                if (_ball.State != BallState.Controlled) BallPhysics.Step(_ball, _pitch, _ballCfg, FixedDt, _draw);
                _accumulator -= FixedDt;
            }

            _playerVisual.transform.position = ToUnity(_body.Position) + Vector3.up * 0.45f;
            _ballVisual.transform.position = ToUnity(_ball.Position);
        }

        private void OnGUI()
        {
            if (_error != null) { GUI.Label(new Rect(10, 10, 600, 40), "PlayerSandbox error: " + _error); return; }
            GUI.Label(new Rect(10, 10, 520, 150),
                "A2 sandbox - segure o botao esquerdo do mouse e arraste pra mover, Shift pra sprintar\n" +
                $"HasBall={_ball?.State == BallState.Controlled} Sprinting={_body?.Sprinting}\n" +
                $"Energy={_body?.Energy:F0}\n" +
                $"Pos={_body?.Position}");
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Z, v.Y);
    }
}
