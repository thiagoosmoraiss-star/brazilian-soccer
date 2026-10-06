using Game.Core.Diagnostics;
using Game.Data.Loading;
using Game.Match;
using Game.Match.Geometry;
using Game.Presentation;
using UnityEngine;

namespace Game.App.Dev
{
    /// <summary>
    /// A1 dev sandbox (TECHNICAL_SPEC §15 "Dev: sandboxes"): Space kicks the ball, R resets it; draws the field,
    /// the goal bars and the predicted trajectory (yellow) against the ball actually moving. Attach to an empty
    /// GameObject in an empty scene (Editor only; not part of any shipped scene).
    /// </summary>
    public sealed class BallSandbox : MonoBehaviour
    {
        private const float FixedDt = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz

        [Header("Kick (Space to shoot, R to reset)")]
        public float Power = 20f;
        [Range(-45f, 45f)] public float AimYawDegrees = 0f;
        [Range(0f, 60f)] public float LoftDegrees = 15f;
        [Range(-10f, 10f)] public float Spin = 0f;

        private Pitch _pitch;
        private BallParameters _cfg;
        private Ball _ball;
        private GameObject _visual;
        private IDebugDraw _draw;
        private float _accumulator;
        private string _error;

        private void Start()
        {
            var sourceResult = UnityDataRoot.Resolve();
            if (!sourceResult.IsSuccess) { _error = sourceResult.ToString(); Debug.LogError("BallSandbox: " + _error); return; }
            var dbResult = GameDataLoader.Load(sourceResult.Value);
            if (!dbResult.IsSuccess) { _error = dbResult.ToString(); Debug.LogError("BallSandbox: " + _error); return; }

            var def = dbResult.Value.Ball;
            _pitch = new Pitch(def.Pitch.Length, def.Pitch.Width, def.Pitch.GoalWidth, def.Pitch.GoalHeight, def.Pitch.PostRadius);
            _cfg = def.Ball;
            _draw = new UnityDebugDraw();
            ResetBall();

            _visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _visual.name = "Ball (A1 sandbox)";
            _visual.transform.localScale = Vector3.one * (_cfg.Radius * 2f);
            Destroy(_visual.GetComponent<Collider>());
        }

        private void ResetBall()
        {
            _ball = new Ball { Position = new System.Numerics.Vector3(-_pitch.HalfLength + 11f, 0f, 0f) };
        }

        private void Update()
        {
            if (_ball == null) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                float yaw = AimYawDegrees * Mathf.Deg2Rad;
                float loft = LoftDegrees * Mathf.Deg2Rad;
                var dir = new System.Numerics.Vector3(Mathf.Cos(loft) * Mathf.Cos(yaw), Mathf.Cos(loft) * Mathf.Sin(yaw), Mathf.Sin(loft));
                _ball.Kick(dir * Power, Spin);
            }
            if (Input.GetKeyDown(KeyCode.R)) ResetBall();

            _accumulator += Time.deltaTime;
            while (_accumulator >= FixedDt)
            {
                BallPhysics.Step(_ball, _pitch, _cfg, FixedDt, _draw);
                _accumulator -= FixedDt;
            }

            BallPhysics.Predict(_ball, _pitch, _cfg, horizonSeconds: 3f, sampleDt: FixedDt, _draw);
            DrawPitch();
            _visual.transform.position = ToUnity(_ball.Position);
        }

        private void DrawPitch()
        {
            float hl = _pitch.HalfLength, hw = _pitch.HalfWidth;
            var a = new System.Numerics.Vector3(-hl, -hw, 0f);
            var b = new System.Numerics.Vector3(hl, -hw, 0f);
            var c = new System.Numerics.Vector3(hl, hw, 0f);
            var d = new System.Numerics.Vector3(-hl, hw, 0f);
            _draw.Line(a, b, DebugColor.White);
            _draw.Line(b, c, DebugColor.White);
            _draw.Line(c, d, DebugColor.White);
            _draw.Line(d, a, DebugColor.White);
            DrawBar(_pitch.HomeLeftPost);
            DrawBar(_pitch.HomeRightPost);
            DrawBar(_pitch.HomeCrossbar);
            DrawBar(_pitch.AwayLeftPost);
            DrawBar(_pitch.AwayRightPost);
            DrawBar(_pitch.AwayCrossbar);
        }

        private void DrawBar(Segment3 bar) => _draw.Line(bar.A, bar.B, DebugColor.Blue);

        private void OnGUI()
        {
            if (_error != null) { GUI.Label(new Rect(10, 10, 600, 40), "BallSandbox error: " + _error); return; }
            GUI.Label(new Rect(10, 10, 500, 160),
                "A1 sandbox - Space: kick, R: reset\n" +
                $"Power={Power:F1} Yaw={AimYawDegrees:F0} Loft={LoftDegrees:F0} Spin={Spin:F1}\n" +
                $"State={_ball?.State}\n" +
                $"Pos={_ball?.Position}\n" +
                $"Vel={_ball?.Velocity}");
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Z, v.Y);
    }
}
