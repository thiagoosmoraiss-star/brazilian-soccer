using Game.Data.Presentation;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Broadcast match camera (GAME_DESIGN §16; Cinemachine, D-10/X-48): side-on and elevated, follows the ball with
    /// damping and lookahead, zooms in near a goal and at set pieces and out in midfield, tilts up a little when play is
    /// on the far touchline, and always shows the user's side attacking left to right. Reads state only.
    /// </summary>
    public sealed class BroadcastCamera
    {
        private readonly MatchViewDefinition _v;
        private readonly Transform _focus;
        private readonly Transform _vcam;
        private readonly CinemachineFollow _follow;
        private readonly float _halfLength, _halfWidth;
        private readonly float _side;
        private float _distance;
        private bool _placed;

        /// <param name="userAttacksPositiveX">The user's side attacks +x (Unity x): the camera stands on the −z touchline.</param>
        public BroadcastCamera(MatchViewDefinition view, float pitchHalfLength, float pitchHalfWidth, bool userAttacksPositiveX)
        {
            _v = view;
            _halfLength = pitchHalfLength;
            _halfWidth = pitchHalfWidth;
            _side = userAttacksPositiveX ? 1f : -1f;
            _distance = view.FarDistance;

            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            if (cam.GetComponent<CinemachineBrain>() == null) cam.gameObject.AddComponent<CinemachineBrain>();
            cam.fieldOfView = view.FieldOfView;

            _focus = new GameObject("Camera focus").transform;
            var go = new GameObject("Broadcast VCam");
            _vcam = go.transform;
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = _focus;
            var lens = vcam.Lens;
            lens.FieldOfView = view.FieldOfView;
            vcam.Lens = lens;
            _follow = go.AddComponent<CinemachineFollow>();
        }

        /// <param name="ball">Interpolated ball position (Unity space).</param>
        /// <param name="ballVelocity">Ball velocity (Unity space).</param>
        /// <param name="setPiece">A throw-in, corner or goal kick is being taken: zoom in.</param>
        public void Update(Vector3 ball, Vector3 ballVelocity, bool setPiece, float dt)
        {
            var ground = new Vector3(ball.x, 0f, ball.z);
            var ahead = new Vector3(ballVelocity.x, 0f, ballVelocity.z) * _v.LookaheadSeconds;
            if (ahead.magnitude > _v.MaxLookahead) ahead = ahead.normalized * _v.MaxLookahead;
            var target = ground + ahead;
            target.x = Mathf.Clamp(target.x, -_halfLength, _halfLength);
            target.z = Mathf.Clamp(target.z, -_halfWidth, _halfWidth);

            float toGoal = _halfLength - Mathf.Abs(ball.x);
            float zoomOut = Mathf.Clamp01((toGoal - _v.ZoomInGoalDistance) / (_v.ZoomOutGoalDistance - _v.ZoomInGoalDistance));
            if (setPiece) zoomOut = 0f;
            float wanted = Mathf.Lerp(_v.NearDistance, _v.FarDistance, zoomOut);

            if (!_placed)
            {
                _focus.position = target;
                _distance = wanted;
                _placed = true;
            }
            else
            {
                _focus.position = Vector3.Lerp(_focus.position, target, 1f - Mathf.Exp(-dt / _v.FollowSeconds));
                _distance = Mathf.Lerp(_distance, wanted, 1f - Mathf.Exp(-dt / _v.ZoomSeconds));
            }

            // Far touchline (away from the camera): look down a little more so the play is not squeezed at the top.
            float farSide = Mathf.Clamp01(_side * _focus.position.z / _halfWidth);
            float pitch = (_v.PitchDegrees + _v.FarSideExtraPitchDegrees * farSide) * Mathf.Deg2Rad;
            _follow.FollowOffset = new Vector3(0f, Mathf.Sin(pitch) * _distance, -_side * Mathf.Cos(pitch) * _distance);
            _vcam.rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, _side > 0f ? 0f : 180f, 0f);
        }
    }
}
